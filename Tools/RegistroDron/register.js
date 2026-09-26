// Registers an Immersal sparse PLY (map-local Unity coords) against the RealityScan drone cloud.
// Model: drone ≈ R(yaw)·map + t  (gravity-aligned, 4 DOF), then reports residual tilt and optional scale.
const fs = require('fs');

function readDrone(path, mirror) {
  const lines = fs.readFileSync(path, 'utf8').split('\n');
  let i = 0; while (!lines[i].startsWith('end_header')) i++; i++;
  const pts = [];
  for (; i < lines.length; i++) {
    const a = lines[i].trim().split(/\s+/); if (a.length < 3) continue;
    const x = +a[0], y = +a[1], z = +a[2];
    if (x * x + y * y > 70 * 70) continue;
    pts.push([mirror ? -x : x, z, y]); // RealityScan z-up -> Unity y-up (swap flips handedness)
  }
  // voxel downsample 0.15 m
  const seen = new Set(), out = [];
  for (const p of pts) { const k = Math.floor(p[0] / .15) + ',' + Math.floor(p[1] / .15) + ',' + Math.floor(p[2] / .15); if (!seen.has(k)) { seen.add(k); out.push(p); } }
  return out;
}
function readImmersal(path) {
  const buf = fs.readFileSync(path); const m = 'end_header\n'; const end = buf.indexOf(m) + m.length;
  const n = +/element vertex (\d+)/.exec(buf.slice(0, end).toString('latin1'))[1];
  const P = []; for (let i = 0; i < n; i++) { const o = end + i * 15; P.push([-buf.readFloatLE(o), buf.readFloatLE(o + 4), buf.readFloatLE(o + 8)]); }
  return P;
}
const rot = (p, deg) => { const t = deg * Math.PI / 180, c = Math.cos(t), s = Math.sin(t); return [p[0] * c + p[2] * s, p[1], -p[0] * s + p[2] * c]; };
function ground(pts) {
  const ys = pts.map(p => p[1]).sort((a, b) => a - b); const cut = ys[Math.floor(ys.length * .35)];
  const h = new Map(); for (const p of pts) if (p[1] <= cut) { const k = Math.round(p[1] * 2); h.set(k, (h.get(k) || 0) + 1); }
  let best = null; for (const [k, c] of h) if (!best || c > best.c) best = { k, c }; return best.k / 2;
}
class Grid {
  constructor(pts, cell) { this.c = cell; this.m = new Map(); this.pts = pts; pts.forEach((p, i) => { const k = this.key(p); if (!this.m.has(k)) this.m.set(k, []); this.m.get(k).push(i); }); }
  key(p) { return Math.floor(p[0] / this.c) + ',' + Math.floor(p[1] / this.c) + ',' + Math.floor(p[2] / this.c); }
  nearest(q, maxd) {
    const r = Math.ceil(maxd / this.c), cx = Math.floor(q[0] / this.c), cy = Math.floor(q[1] / this.c), cz = Math.floor(q[2] / this.c);
    let best = -1, bd = maxd * maxd;
    for (let dx = -r; dx <= r; dx++) for (let dy = -r; dy <= r; dy++) for (let dz = -r; dz <= r; dz++) {
      const l = this.m.get((cx + dx) + ',' + (cy + dy) + ',' + (cz + dz)); if (!l) continue;
      for (const i of l) { const p = this.pts[i]; const d = (p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 + (p[2] - q[2]) ** 2; if (d < bd) { bd = d; best = i; } }
    }
    return best < 0 ? null : { i: best, d: Math.sqrt(bd) };
  }
}
const apply = (T, m) => { const r = rot(m, T.yaw); return [T.s * r[0] + T.t[0], T.s * r[1] + T.t[1], T.s * r[2] + T.t[2]]; };

function icp(M, grid, T0, schedule, fitScale) {
  let T = { ...T0, t: [...T0.t] };
  for (const th of schedule) for (let it = 0; it < 15; it++) {
    const pairs = [];
    for (const m of M) { const q = apply(T, m); const nn = grid.nearest(q, th); if (nn) pairs.push([m, grid.pts[nn.i]]); }
    if (pairs.length < 20) return { T, inl: 0 };
    const cm = [0, 0, 0], cd = [0, 0, 0];
    for (const [m, d] of pairs) for (let k = 0; k < 3; k++) { cm[k] += m[k]; cd[k] += d[k]; }
    for (let k = 0; k < 3; k++) { cm[k] /= pairs.length; cd[k] /= pairs.length; }
    let A = 0, B = 0;
    for (const [m, d] of pairs) { const mx = m[0] - cm[0], mz = m[2] - cm[2], dx = d[0] - cd[0], dz = d[2] - cd[2]; A += dx * mx + dz * mz; B += dx * mz - dz * mx; }
    const yaw = Math.atan2(B, A) * 180 / Math.PI;
    let s = 1;
    if (fitScale) { let num = 0, den = 0; for (const [m, d] of pairs) { const r = rot([m[0] - cm[0], m[1] - cm[1], m[2] - cm[2]], yaw); num += r[0] * (d[0] - cd[0]) + r[1] * (d[1] - cd[1]) + r[2] * (d[2] - cd[2]); den += r[0] ** 2 + r[1] ** 2 + r[2] ** 2; } s = Math.min(1.2, Math.max(0.8, num / den)); }
    const rc = rot(cm, yaw);
    const nT = { yaw, s, t: [cd[0] - s * rc[0], cd[1] - s * rc[1], cd[2] - s * rc[2]] };
    const moved = Math.hypot(nT.t[0] - T.t[0], nT.t[1] - T.t[1], nT.t[2] - T.t[2]) + Math.abs(nT.yaw - T.yaw) * 0.1;
    T = nT; if (moved < 1e-4) break;
  }
  return { T, ...score(M, grid, T) };
}
function score(M, grid, T) {
  let i25 = 0, i50 = 0, sq = 0; const res = [];
  for (const m of M) { const q = apply(T, m); const nn = grid.nearest(q, 0.5); if (nn) { i50++; sq += nn.d * nn.d; res.push({ q, d: grid.pts[nn.i] }); if (nn.d < .25) i25++; } }
  return { inl: i50, inl25: i25, rms: Math.sqrt(sq / Math.max(1, i50)), res };
}

function coarse(M, D, gM, gD) {
  const cell = 1;
  const occ = new Map();
  for (const p of D) if (p[1] - gD > 1 && p[1] - gD < 12) { const k = Math.floor(p[0] / cell) + ',' + Math.floor(p[2] / cell); occ.set(k, Math.min(5, (occ.get(k) || 0) + 1)); }
  const cells = [...occ.keys()].map(k => k.split(',').map(Number));
  const MS = M.filter(p => p[1] - gM > 1 && p[1] - gM < 12);
  const R = 160, W = 2 * R + 1; const cands = [];
  for (let yaw = 0; yaw < 360; yaw += 2) {
    const votes = new Uint16Array(W * W);
    for (const m of MS) { const r = rot(m, yaw); const rx = Math.floor(r[0]), rz = Math.floor(r[2]);
      for (const [cx, cz] of cells) { const tx = cx - rx + R, tz = cz - rz + R; if (tx >= 0 && tx < W && tz >= 0 && tz < W) votes[tx * W + tz]++; } }
    let best = 0, bi = 0; for (let i = 0; i < votes.length; i++) if (votes[i] > best) { best = votes[i]; bi = i; }
    cands.push({ yaw, v: best, t: [Math.floor(bi / W) - R + .5, gD - gM, (bi % W) - R + .5] });
  }
  cands.sort((a, b) => b.v - a.v);
  return { cands, nStruct: MS.length, nCells: cells.length };
}

function run(dronePath, mapPath, mirror) {
  const D = readDrone(dronePath, mirror), M = readImmersal(mapPath);
  const gD = ground(D), gM = ground(M);
  const grid = new Grid(D, 1);
  const { cands, nStruct, nCells } = coarse(M, D, gM, gD);
  const results = [];
  for (const c of cands.slice(0, 12)) {
    const r = icp(M, grid, { yaw: c.yaw, s: 1, t: c.t }, [4, 3, 2, 1.5, 1, .7, .5], false);
    results.push({ coarse: c, ...r });
  }
  results.sort((a, b) => b.inl - a.inl);
  return { D, M, gD, gM, grid, results, nStruct, nCells };
}

module.exports = { run, icp, score, apply, rot, readDrone, readImmersal, Grid, ground };

if (require.main === module) {
  const [drone, map] = process.argv.slice(2);
  for (const mirror of [false, true]) {
    const r = run(drone, map, mirror);
    console.log(`\n== ${map.split(/[\\/]/).pop()} mirror=${mirror} drone pts=${r.D.length} map pts=${r.M.length} gD=${r.gD} gM=${r.gM} struct=${r.nStruct} cells=${r.nCells}`);
    const seen = [];
    for (const x of r.results) {
      const dup = seen.some(y => Math.abs(((x.T.yaw - y.T.yaw + 540) % 360) - 180) < 3 && Math.hypot(x.T.t[0] - y.T.t[0], x.T.t[2] - y.T.t[2]) < 1);
      if (dup) continue; seen.push(x);
      console.log(` coarse yaw ${x.coarse.yaw} votes ${x.coarse.v} -> yaw ${x.T.yaw.toFixed(2)} t [${x.T.t.map(v => v.toFixed(2))}] inl50 ${x.inl} inl25 ${x.inl25} rms ${x.rms.toFixed(3)}`);
      if (seen.length >= 5) break;
    }
  }
}
