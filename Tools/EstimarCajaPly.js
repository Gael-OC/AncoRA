#!/usr/bin/env node
// Proposes a starting box (position, yaw and a default size) for the Teologia demo from the sparse PLY of the Immersal map.
// The output is a STARTING POINT to be corrected on the phone; it is not a physical measurement.
//
// Usage: node Tools/EstimarCajaPly.js <mapa-sparse.ply> <salida-medidas.json> [--tamano=ancho,alto,prof]
//
// Coordinates: the Immersal SDK shows PLY points in Unity as (-x, y, z), so everything here is in Unity-local
// coordinates (the local frame of the XR Map, which is at identity under the XR Space).
//
// Method: (1) ground = densest 0.5 m layer among the lowest points; (2) box yaw = the rotation that makes the points
// above the ground line up on axis-aligned planes (sharpest 0.5 m histograms on both horizontal axes);
// (3) in that frame, dense 2 m cells are grouped and the biggest group gives the box CENTRE (median).
// The SIZE is NOT taken from the cloud: with about a thousand points the extent of the dense group changes from 3 m
// to 27 m with the density threshold, so it is not reliable. The size comes from --tamano=ancho,alto,prof (default
// 20,8,12 m) and is written as a declared default, to be corrected on the phone.
const fs = require('fs');

function readPly(path) {
  const buf = fs.readFileSync(path);
  const marker = 'end_header\n';
  const end = buf.indexOf(marker) + marker.length;
  const header = buf.slice(0, end).toString('latin1');
  const n = parseInt(/element vertex (\d+)/.exec(header)[1], 10);
  if (!/binary_little_endian/.test(header) || buf.length - end < n * 15) throw new Error(`PLY inesperado: ${path}`);
  const pts = [];
  for (let i = 0; i < n; i++) {
    const o = end + i * 15;
    pts.push([-buf.readFloatLE(o), buf.readFloatLE(o + 4), buf.readFloatLE(o + 8)]);
  }
  return pts;
}

// Unity's Quaternion.Euler(0, yaw, 0) applied to a point.
const rot = (p, deg) => {
  const t = deg * Math.PI / 180, c = Math.cos(t), s = Math.sin(t);
  return [p[0] * c + p[2] * s, p[1], -p[0] * s + p[2] * c];
};
const percentile = (values, q) => {
  const s = [...values].sort((a, b) => a - b);
  return s[Math.min(s.length - 1, Math.max(0, Math.round(q * (s.length - 1))))];
};

function groundLevel(pts) {
  const ys = pts.map(p => p[1]).sort((a, b) => a - b);
  const cutoff = ys[Math.floor(ys.length * 0.35)];
  const hist = new Map();
  for (const p of pts) if (p[1] <= cutoff) { const k = Math.round(p[1] * 2); hist.set(k, (hist.get(k) || 0) + 1); }
  let best = null;
  for (const [k, c] of hist) if (!best || c > best.c) best = { k, c };
  return best.k / 2;
}

// Higher when many points share the same 0.5 m slab along x or along z after rotating by -yaw.
function alignmentScore(pts, yaw) {
  const hx = new Map(), hz = new Map();
  for (const p of pts) {
    const q = rot(p, -yaw);
    const kx = Math.round(q[0] * 2), kz = Math.round(q[2] * 2);
    hx.set(kx, (hx.get(kx) || 0) + 1);
    hz.set(kz, (hz.get(kz) || 0) + 1);
  }
  let score = 0;
  for (const c of hx.values()) score += c * c;
  for (const c of hz.values()) score += c * c;
  return score;
}

function largestDenseGroup(local, cell, minCount) {
  const cells = new Map();
  local.forEach((q, i) => {
    const key = Math.floor(q[0] / cell) + ',' + Math.floor(q[2] / cell);
    if (!cells.has(key)) cells.set(key, []);
    cells.get(key).push(i);
  });
  const dense = new Set([...cells].filter(([, list]) => list.length >= minCount).map(([key]) => key));
  const seen = new Set();
  let best = [];
  for (const start of dense) {
    if (seen.has(start)) continue;
    const group = [], stack = [start];
    seen.add(start);
    while (stack.length) {
      const key = stack.pop();
      group.push(key);
      const [cx, cz] = key.split(',').map(Number);
      for (let dx = -1; dx <= 1; dx++) for (let dz = -1; dz <= 1; dz++) {
        const next = (cx + dx) + ',' + (cz + dz);
        if (dense.has(next) && !seen.has(next)) { seen.add(next); stack.push(next); }
      }
    }
    const count = group.reduce((sum, key) => sum + cells.get(key).length, 0);
    if (count > best.count || best.length === 0) { best = group; best.count = count; }
  }
  return best.flatMap(key => cells.get(key));
}

const [plyPath, outPath] = process.argv.slice(2);
if (!plyPath || !outPath) {
  console.error('Uso: node Tools/EstimarCajaPly.js <mapa-sparse.ply> <salida-medidas.json>');
  process.exit(2);
}

const all = readPly(plyPath);
const ground = groundLevel(all);
const above = all.filter(p => p[1] - ground > 0.4);
if (above.length < 100) throw new Error(`Solo ${above.length} puntos sobre el suelo; la nube no alcanza para estimar una caja.`);

let bestYaw = 0, bestScore = -1;
for (let yaw = 0; yaw < 90; yaw += 1) {
  const score = alignmentScore(above, yaw);
  if (score > bestScore) { bestScore = score; bestYaw = yaw; }
}

const local = above.map(p => rot(p, -bestYaw));
const group = largestDenseGroup(local, 2, 3).map(i => ({ q: local[i], y: above[i][1] }));
if (group.length < 40) throw new Error(`El grupo denso tiene solo ${group.length} puntos; no hay un edificio claro en la nube.`);

const median = values => percentile(values, 0.5);
const argSize = (process.argv.find(a => a.startsWith('--tamano=')) || '--tamano=20,8,12').slice(9).split(',').map(Number);
if (argSize.length !== 3 || argSize.some(v => !(v >= 0.5))) throw new Error('--tamano debe ser ancho,alto,prof en metros (>= 0,5 cada uno)');
const [width, height, depth] = argSize;
const centerLocal = [median(group.map(g => g.q[0])), 0, median(group.map(g => g.q[2]))];
const centerMap = rot(centerLocal, bestYaw);
const round = v => Math.round(v * 100) / 100;

const result = {
  frameWidthMeters: width,
  frameHeightMeters: height,
  frameDepthMeters: depth,
  framePosition: [round(centerMap[0]), round(ground + height / 2), round(centerMap[2])],
  frameYawDegrees: bestYaw,
  estimated: true,
  note: `TAMAÑO POR DEFECTO (${width} x ${height} x ${depth} m), NO MEDIDO NI ESTIMADO. De la nube PLY salen solo el suelo (y=${round(ground)} m), el giro (${bestYaw} grados, por alineación de planos) y el centro del grupo denso (${group.length} de ${all.length} puntos). ` +
        `Corregir tamaño, posición y giro en el teléfono con el panel de ajuste (5 toques arriba a la izquierda) y copiar los valores.`
};
fs.writeFileSync(outPath, JSON.stringify(result, null, 2) + '\n');
console.log(JSON.stringify(result, null, 2));
