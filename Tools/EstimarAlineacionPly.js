#!/usr/bin/env node
// Estimates the rigid transform (yaw about Y + translation) that places map B on map A
// using only their sparse PLY point clouds. The output is a STARTING POINT for the manual
// alignment in Unity; it is not a physical measurement.
//
// Usage: node Tools/EstimarAlineacionPly.js <A.ply> <B.ply> <salida.json>
//
// Coordinates: the Immersal SDK shows PLY points in Unity as (-x, y, z) (right-handed file -> left-handed
// Unity). Everything below works in those Unity-local coordinates, so the result is the local pose of the
// "XR Map B" object under the XR Space when "XR Map A" is at identity:
//   pointInA = Rotation(Y, yawDeg) * pointInB + position
// with Unity's Quaternion.Euler(0, yaw, 0): x' = x cos + z sin, z' = -x sin + z cos. That rotation maps a
// horizontal direction angle a = atan2(z, x) to a - yaw.
//
// Method: (1) long wall directions by RANSAC lines in XZ; (2) only the yaws that make the long walls of B
// parallel to those of A (plus the 180 degree option) are tried; (3) grid search of (tx, tz) counting B
// points that land on occupied 1 m cells of A; (4) trimmed ICP refinement; (5) rank by inlier ratio and
// report how much better the winner is than the best clearly different solution.
const fs = require('fs');

function readPly(path) {
  const buf = fs.readFileSync(path);
  const end = buf.indexOf('end_header\n') + 'end_header\n'.length;
  const header = buf.slice(0, end).toString('latin1');
  const n = parseInt(/element vertex (\d+)/.exec(header)[1], 10);
  if (!/binary_little_endian/.test(header) || buf.length - end < n * 15) throw new Error(`PLY inesperado: ${path}`);
  const pts = [];
  for (let i = 0; i < n; i++) {
    const o = end + i * 15;
    pts.push([-buf.readFloatLE(o), buf.readFloatLE(o + 4), buf.readFloatLE(o + 8)]); // Unity-local
  }
  return pts;
}

let seed = 20260924;
const rnd = () => { seed = (seed * 1664525 + 1013904223) % 4294967296; return seed / 4294967296; };
const rot = (p, th) => [p[0] * Math.cos(th) + p[2] * Math.sin(th), p[1], -p[0] * Math.sin(th) + p[2] * Math.cos(th)];
const apply = (p, T) => { const r = rot(p, T.th); return [r[0] + T.t[0], r[1] + T.t[1], r[2] + T.t[2]]; };
const deg = r => r * 180 / Math.PI, radians = d => d * Math.PI / 180;
const wrap180 = d => ((d % 360) + 540) % 360 - 180;

// Ground level: centre of the densest 0.5 m vertical layer among the lowest points.
function groundLevel(pts) {
  const ys = pts.map(p => p[1]).sort((a, b) => a - b);
  const cutoff = ys[Math.floor(ys.length * 0.35)];
  const hist = new Map();
  for (const p of pts) if (p[1] <= cutoff) { const k = Math.round(p[1] * 2); hist.set(k, (hist.get(k) || 0) + 1); }
  let best = null;
  for (const [k, c] of hist) if (!best || c > best.c) best = { k, c };
  return best.k / 2;
}

// Vertical walls as dense line segments in XZ (RANSAC). Returns [{psi (deg, 0..180), length, count}] longest first.
function wallLines(pts, g) {
  let rest = pts.filter(p => p[1] - g > 0.4);
  const lines = [];
  const TOL = 0.6, GAP = 6;
  for (let k = 0; k < 6 && rest.length > 60; k++) {
    let best = null;
    for (let it = 0; it < 6000; it++) {
      const a = rest[Math.floor(rnd() * rest.length)], b = rest[Math.floor(rnd() * rest.length)];
      const dx = b[0] - a[0], dz = b[2] - a[2], l = Math.hypot(dx, dz);
      if (l < 3) continue;
      const nx = -dz / l, nz = dx / l;
      let c = 0;
      for (const p of rest) if (Math.abs((p[0] - a[0]) * nx + (p[2] - a[2]) * nz) < TOL) c++;
      if (!best || c > best.c) best = { a, dx: dx / l, dz: dz / l, nx, nz, c };
    }
    if (!best || best.c < 40) break;
    const near = p => Math.abs((p[0] - best.a[0]) * best.nx + (p[2] - best.a[2]) * best.nz) < TOL;
    const inl = rest.filter(near);
    const t = inl.map(p => (p[0] - best.a[0]) * best.dx + (p[2] - best.a[2]) * best.dz).sort((x, y) => x - y);
    const runs = []; let start = 0;
    for (let i = 1; i <= t.length; i++) if (i === t.length || t[i] - t[i - 1] > GAP) { runs.push([t[start], t[i - 1], i - start]); start = i; }
    runs.sort((x, y) => y[2] - x[2]);
    const psi = ((deg(Math.atan2(best.dz, best.dx)) % 180) + 180) % 180;
    lines.push({ psi, length: runs[0][1] - runs[0][0], count: runs[0][2] });
    rest = rest.filter(p => !near(p));
  }
  return lines.sort((x, y) => y.length - x.length);
}

// Dominant long-wall direction: mean of the long lines (>= 30 m) within 8 degrees of the longest one.
function longWallPsi(lines) {
  const long = lines.filter(l => l.length >= 30);
  if (!long.length) return null;
  const ref = long[0].psi;
  const same = long.filter(l => Math.abs(wrap180(l.psi - ref)) <= 8 || Math.abs(wrap180(l.psi - ref)) >= 172);
  const sum = same.reduce((s, l) => s + wrap180(l.psi - ref), 0);
  return { psi: ((ref + sum / same.length) % 180 + 180) % 180, lines: same.length };
}

const cellKey = (ix, iy, iz) => ((ix + 2048) * 4096 + (iy + 2048)) * 4096 + (iz + 2048);
const CELL = 1.0;

function occupancy(pts) {
  const s = new Set();
  for (const p of pts) s.add(cellKey(Math.floor(p[0] / CELL), Math.floor(p[1] / CELL), Math.floor(p[2] / CELL)));
  return s;
}

class Grid {
  constructor(pts, cell) {
    this.cell = cell; this.map = new Map(); this.pts = pts;
    pts.forEach((p, i) => { const k = this.key(p); (this.map.get(k) || this.map.set(k, []).get(k)).push(i); });
  }
  key(p) { return `${Math.floor(p[0] / this.cell)},${Math.floor(p[1] / this.cell)},${Math.floor(p[2] / this.cell)}`; }
  nearest(p, maxDist) {
    const cx = Math.floor(p[0] / this.cell), cy = Math.floor(p[1] / this.cell), cz = Math.floor(p[2] / this.cell);
    const r = Math.ceil(maxDist / this.cell);
    let best = null, bd = maxDist * maxDist;
    for (let x = cx - r; x <= cx + r; x++) for (let y = cy - r; y <= cy + r; y++) for (let z = cz - r; z <= cz + r; z++) {
      const l = this.map.get(`${x},${y},${z}`); if (!l) continue;
      for (const i of l) {
        const q = this.pts[i], d = (q[0] - p[0]) ** 2 + (q[1] - p[1]) ** 2 + (q[2] - p[2]) ** 2;
        if (d < bd) { bd = d; best = i; }
      }
    }
    return best === null ? null : { i: best, d: Math.sqrt(bd) };
  }
}

const [aPath, bPath, outPath] = process.argv.slice(2);
if (!outPath) { console.error('Uso: node EstimarAlineacionPly.js <A.ply> <B.ply> <salida.json>'); process.exit(2); }
const A = readPly(aPath), B = readPly(bPath);
const gA = groundLevel(A), gB = groundLevel(B);
const linesA = wallLines(A, gA), linesB = wallLines(B, gB);
const lwA = longWallPsi(linesA), lwB = longWallPsi(linesB);
console.log(`Puntos A=${A.length} B=${B.length}; suelo estimado A y=${gA}, B y=${gB}`);
console.log('Muros A:', linesA.map(l => `${l.length.toFixed(1)} m @${l.psi.toFixed(1)}°`).join(' | '));
console.log('Muros B:', linesB.map(l => `${l.length.toFixed(1)} m @${l.psi.toFixed(1)}°`).join(' | '));
if (!lwA || !lwB) { console.error('No se encontraron paredes largas en ambos mapas; no se puede estimar.'); process.exit(3); }
console.log(`Rumbo de la pared larga: A=${lwA.psi.toFixed(1)}° (${lwA.lines} líneas), B=${lwB.psi.toFixed(1)}° (${lwB.lines} líneas)`);

// 2-3) Candidate yaws and grid search of the translation.
const occA = occupancy(A);
const Bsub = B.filter((_, i) => i % 2 === 0);
const dy = gA - gB;
const candidates = [];
for (const extra of [0, 180]) {
  const centreYaw = wrap180(lwB.psi - lwA.psi + extra);
  for (let off = -4; off <= 4; off += 1) {
    const yaw = centreYaw + off, th = radians(yaw);
    const rb = Bsub.map(p => rot(p, th));
    let top = [];
    for (let tx = -90; tx <= 90; tx += 1) for (let tz = -90; tz <= 90; tz += 1) {
      let n = 0;
      for (const p of rb) if (occA.has(cellKey(Math.floor((p[0] + tx) / CELL), Math.floor((p[1] + dy) / CELL), Math.floor((p[2] + tz) / CELL)))) n++;
      if (top.length < 4 || n > top[top.length - 1].n) { top.push({ yaw, tx, tz, n }); top.sort((x, y) => y.n - x.n); top = top.slice(0, 4); }
    }
    candidates.push(...top);
  }
}
candidates.sort((x, y) => y.n - x.n);
const seeds = [];
for (const c of candidates) {
  if (seeds.every(s => Math.hypot(s.tx - c.tx, s.tz - c.tz) > 3 || Math.abs(wrap180(s.yaw - c.yaw)) > 3)) seeds.push(c);
  if (seeds.length >= 16) break;
}

// 4) Trimmed ICP with yaw and translation on all points.
const gridA = new Grid(A, 1.5);
function inlierStats(T, thr) {
  let n = 0, sum = 0;
  for (const b of B) { const h = gridA.nearest(apply(b, T), thr); if (h) { n++; sum += h.d * h.d; } }
  return { inliers: n, ratio: n / B.length, rmse: n ? Math.sqrt(sum / n) : Infinity };
}
function icp(s) {
  let T = { th: radians(s.yaw), t: [s.tx, dy, s.tz] };
  for (let it = 0; it < 40; it++) {
    const thr = it < 10 ? 2 : it < 20 ? 1.2 : 0.7;
    const pairs = [];
    for (const b of B) { const h = gridA.nearest(apply(b, T), thr); if (h) pairs.push([b, A[h.i]]); }
    if (pairs.length < 60) break;
    const cb = [0, 0, 0], ca = [0, 0, 0];
    for (const [b, a] of pairs) for (let k = 0; k < 3; k++) { cb[k] += b[k] / pairs.length; ca[k] += a[k] / pairs.length; }
    let sN = 0, cN = 0;
    for (const [b, a] of pairs) {
      const bx = b[0] - cb[0], bz = b[2] - cb[2], ax = a[0] - ca[0], az = a[2] - ca[2];
      cN += ax * bx + az * bz; // maximise sum of dot(a, Rot(b))
      sN += ax * bz - az * bx;
    }
    const th = Math.atan2(sN, cN), rc = rot(cb, th);
    T = { th, t: [ca[0] - rc[0], ca[1] - rc[1], ca[2] - rc[2]] };
  }
  return T;
}
const results = seeds.map(s => { const T = icp(s); return { T, ...inlierStats(T, 0.6) }; }).sort((x, y) => y.ratio - x.ratio);

// 5) Distinct solutions and how clearly the best one wins.
const distinct = [];
for (const r of results) {
  const yaw = wrap180(deg(r.T.th));
  if (distinct.every(o => Math.hypot(o.T.t[0] - r.T.t[0], o.T.t[2] - r.T.t[2]) > 3 || Math.abs(wrap180(o.yaw - yaw)) > 5)) distinct.push({ ...r, yaw });
}
const fmt = r => ({
  positionMeters: r.T.t.map(v => +v.toFixed(3)),
  yawDegrees: +r.yaw.toFixed(2),
  bPointsWithin0_6mOfA: r.inliers,
  bPointsTotal: B.length,
  inlierRatio: +r.ratio.toFixed(3),
  rmseInliersMeters: +r.rmse.toFixed(3)
});
const best = distinct[0], second = distinct[1];
const output = {
  verified: false,
  warning: 'ESTIMADO por registro de nubes PLY; NO verificado contra medidas físicas. Punto de partida para la alineación manual.',
  method: 'Rumbo de paredes largas (RANSAC en XZ) + rejilla de traslación por ocupación + ICP recortado; coordenadas Unity (-x, y, z).',
  generatedWith: 'Tools/EstimarAlineacionPly.js',
  mapA: aPath.split(/[\\/]/).pop(), mapB: bPath.split(/[\\/]/).pop(),
  groundY: { A: gA, B: gB },
  longWallDirectionDegrees: { A: +lwA.psi.toFixed(1), B: +lwB.psi.toFixed(1) },
  best: fmt(best),
  secondBest: second ? fmt(second) : null,
  ratioBestToSecond: second ? +(best.ratio / Math.max(second.ratio, 1e-6)).toFixed(2) : null,
  otherCandidates: distinct.slice(2, 5).map(fmt)
};
fs.writeFileSync(outPath, JSON.stringify(output, null, 2) + '\n');
console.log(JSON.stringify(output, null, 2));
