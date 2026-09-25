#!/usr/bin/env node
// Writes an SVG top view (X right, Z up, Unity axes) with map A in green and map B, placed with the
// candidate alignments of alineacion-estimada.json, in magenta. Only for eyeballing the estimate.
// Usage: node Tools/VistaSuperiorAlineacion.js <A.ply> <B.ply> <alineacion-estimada.json> <salida.svg>
const fs = require('fs');
function readPly(path) {
  const b = fs.readFileSync(path), e = b.indexOf('end_header\n') + 11, n = parseInt(/element vertex (\d+)/.exec(b.slice(0, e).toString('latin1'))[1], 10);
  const p = []; for (let i = 0; i < n; i++) { const o = e + i * 15; p.push([-b.readFloatLE(o), b.readFloatLE(o + 4), b.readFloatLE(o + 8)]); } return p;
}
const [a, b, j, out] = process.argv.slice(2);
const A = readPly(a), B = readPly(b), est = JSON.parse(fs.readFileSync(j, 'utf8'));
const cands = [['Mejor candidato', est.best], ['Segundo candidato', est.secondBest]].filter(c => c[1]);
const rot = (p, th) => [p[0] * Math.cos(th) + p[2] * Math.sin(th), -p[0] * Math.sin(th) + p[2] * Math.cos(th)];
const W = 700, H = 700, S = 3.2; // px per metre
const cx = A.reduce((s, p) => s + p[0], 0) / A.length, cz = A.reduce((s, p) => s + p[2], 0) / A.length;
let svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W * cands.length}" height="${H + 40}" font-family="sans-serif" font-size="14"><rect width="100%" height="100%" fill="#fff"/>`;
cands.forEach(([name, c], i) => {
  const ox = i * W, th = c.yawDegrees * Math.PI / 180;
  const X = x => ox + W / 2 + (x - cx) * S, Z = z => H / 2 - (z - cz) * S + 30;
  svg += `<text x="${ox + 10}" y="20" font-weight="bold">${name}: giro ${c.yawDegrees}°, pos (${c.positionMeters[0]}, ${c.positionMeters[1]}, ${c.positionMeters[2]}) m, acierto ${(c.inlierRatio * 100).toFixed(0)} %</text>`;
  for (let g = -100; g <= 100; g += 10) svg += `<line x1="${X(cx + g)}" y1="30" x2="${X(cx + g)}" y2="${H + 30}" stroke="#eee"/><line x1="${ox}" y1="${Z(cz + g)}" x2="${ox + W}" y2="${Z(cz + g)}" stroke="#eee"/>`;
  for (const p of A) svg += `<circle cx="${X(p[0]).toFixed(1)}" cy="${Z(p[2]).toFixed(1)}" r="2" fill="#2a9d3a"/>`;
  for (const p of B) { const r = rot(p, th); svg += `<circle cx="${X(r[0] + c.positionMeters[0]).toFixed(1)}" cy="${Z(r[1] + c.positionMeters[2]).toFixed(1)}" r="2" fill="#c2188f" fill-opacity="0.8"/>`; }
  svg += `<text x="${ox + 10}" y="${H + 25}">verde = A (lejos) · magenta = B (cerca) · cuadrícula 10 m · +X derecha, +Z arriba</text>`;
});
fs.writeFileSync(out, svg + '</svg>\n'); console.log('SVG escrito en', out);
