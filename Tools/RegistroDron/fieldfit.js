// Smooth-kernel registration of an Immersal sparse cloud onto the drone cloud, with bootstrap uncertainty.
// Params p = [yaw, tx, ty, tz, logS, pitch, roll]; drone ≈ Tilt(pitch, roll) · (s·R(yaw)·m + t)
const R = require('./register.js');
const fs = require('fs');

const SIG = 0.2, RES = 0.1, SPLAT = 3; // meters, meters, cells
function buildField(D, box) {
  const nx = Math.ceil((box.x1 - box.x0) / RES), ny = Math.ceil((box.y1 - box.y0) / RES), nz = Math.ceil((box.z1 - box.z0) / RES);
  const F = new Float32Array(nx * ny * nz);
  for (const p of D) {
    const cx = Math.round((p[0] - box.x0) / RES), cy = Math.round((p[1] - box.y0) / RES), cz = Math.round((p[2] - box.z0) / RES);
    for (let i = -SPLAT * 2; i <= SPLAT * 2; i++) for (let j = -SPLAT * 2; j <= SPLAT * 2; j++) for (let k = -SPLAT * 2; k <= SPLAT * 2; k++) {
      const x = cx + i, y = cy + j, z = cz + k; if (x < 0 || y < 0 || z < 0 || x >= nx || y >= ny || z >= nz) continue;
      const dx = box.x0 + x * RES - p[0], dy = box.y0 + y * RES - p[1], dz = box.z0 + z * RES - p[2];
      const v = Math.exp(-(dx * dx + dy * dy + dz * dz) / (2 * SIG * SIG)); const idx = (x * ny + y) * nz + z; if (v > F[idx]) F[idx] = v;
    }
  }
  return { F, nx, ny, nz, box };
}
function sample(field, q) {
  const { F, nx, ny, nz, box } = field;
  const x = Math.round((q[0] - box.x0) / RES), y = Math.round((q[1] - box.y0) / RES), z = Math.round((q[2] - box.z0) / RES);
  if (x < 0 || y < 0 || z < 0 || x >= nx || y >= ny || z >= nz) return 0; return F[(x * ny + y) * nz + z];
}
function transform(p, m, pivot) {
  const [yaw, tx, ty, tz, logS, pitch, roll] = p; const s = Math.exp(logS);
  const r = R.rot(m, yaw); let q = [s * r[0] + tx, s * r[1] + ty, s * r[2] + tz];
  if (pitch || roll) { // small tilt about drone X (pitch) then Z (roll), around pivot
    let x = q[0] - pivot[0], y = q[1] - pivot[1], z = q[2] - pivot[2];
    const a = pitch * Math.PI / 180, b = roll * Math.PI / 180;
    let y1 = y * Math.cos(a) - z * Math.sin(a), z1 = y * Math.sin(a) + z * Math.cos(a); y = y1; z = z1;
    let x1 = x * Math.cos(b) - y * Math.sin(b); y1 = x * Math.sin(b) + y * Math.cos(b); x = x1; y = y1;
    q = [x + pivot[0], y + pivot[1], z + pivot[2]];
  }
  return q;
}
function scoreFn(field, pts, w, pivot) { return p => { let s = 0; for (let i = 0; i < pts.length; i++) s += w[i] * sample(field, transform(p, pts[i], pivot)); return s; }; }
function nelderMead(f, x0, step, iters = 400) {
  const n = x0.length; let S = [x0.slice()]; for (let i = 0; i < n; i++) { const x = x0.slice(); x[i] += step[i]; S.push(x); }
  let V = S.map(x => -f(x));
  for (let it = 0; it < iters; it++) {
    const ord = V.map((v, i) => i).sort((a, b) => V[a] - V[b]); S = ord.map(i => S[i]); V = ord.map(i => V[i]);
    const c = Array(n).fill(0); for (let i = 0; i < n; i++) for (let k = 0; k < n; k++) c[k] += S[i][k] / n;
    const xr = c.map((v, k) => v + (v - S[n][k])), fr = -f(xr);
    if (fr < V[0]) { const xe = c.map((v, k) => v + 2 * (v - S[n][k])), fe = -f(xe); if (fe < fr) { S[n] = xe; V[n] = fe; } else { S[n] = xr; V[n] = fr; } }
    else if (fr < V[n - 1]) { S[n] = xr; V[n] = fr; }
    else { const xc = c.map((v, k) => v + 0.5 * (S[n][k] - v)), fc = -f(xc); if (fc < V[n]) { S[n] = xc; V[n] = fc; } else { for (let i = 1; i <= n; i++) { S[i] = S[i].map((v, k) => S[0][k] + 0.5 * (v - S[0][k])); V[i] = -f(S[i]); } } }
  }
  const i = V.indexOf(Math.min(...V)); return { x: S[i], f: -V[i] };
}

function main() {
  const [drone, map, startJ, outPath, mirrorArg] = process.argv.slice(2);
  const D = R.readDrone(drone, mirrorArg === '1'), M = R.readImmersal(map); const gM = R.ground(M);
  const start = JSON.parse(startJ);
  const box = { x0: -45, x1: 45, y0: -4, y1: 16, z0: -45, z1: 45 };
  const t0 = Date.now(); const field = buildField(D.filter(p => p[0] > box.x0 && p[0] < box.x1 && p[2] > box.z0 && p[2] < box.z1 && p[1] > box.y0 && p[1] < box.y1), box);
  console.error(`field ${field.nx}x${field.ny}x${field.nz} in ${(Date.now() - t0) / 1000}s`);
  const pts = M.filter(p => p[1] - gM > -1 && p[1] - gM < 15);
  const w = pts.map(p => (p[1] - gM > 0.8 ? 1 : 0.4)); // ground points count less: they constrain height/tilt, not the slide
  const pivot = [0, 3, 0];
  const f = scoreFn(field, pts, w, pivot);
  const results = [];
  for (let yawOff = -6; yawOff <= 6; yawOff += 3) for (let du = -4; du <= 4; du += 1) {
    const th = 83.25 * Math.PI / 180, uDir = [Math.cos(th), Math.sin(th)];
    const x0 = [start.yaw + yawOff, start.t[0] + du * uDir[0], start.t[1], start.t[2] + du * uDir[1], 0, 0, 0];
    let r = nelderMead(p => f([...p.slice(0, 4), 0, 0, 0]), x0.slice(0, 4), [1.5, 0.7, 0.3, 0.7], 250);
    const r4 = { x: [...r.x, 0, 0, 0], f: r.f };
    const r7 = nelderMead(f, r4.x, [0.5, 0.3, 0.1, 0.3, 0.02, 0.5, 0.5], 500);
    results.push({ rigid: r4, full: r7 });
  }
  results.sort((a, b) => b.rigid.f - a.rigid.f);
  const fmt = x => `yaw ${x[0].toFixed(2)} t [${x.slice(1, 4).map(v => v.toFixed(2))}] s ${Math.exp(x[4]).toFixed(4)} pitch ${x[5].toFixed(2)} roll ${x[6].toFixed(2)}`;
  console.log(`${map.split(/[\\/]/).pop()} pts ${pts.length} (gM ${gM})`);
  const seen = [];
  for (const r of results) { if (seen.some(y => Math.abs(y.rigid.x[0] - r.rigid.x[0]) < 0.5 && Math.hypot(y.rigid.x[1] - r.rigid.x[1], y.rigid.x[3] - r.rigid.x[3]) < 0.3)) continue; seen.push(r);
    console.log(` rigid f ${r.rigid.f.toFixed(1)} ${fmt(r.rigid.x)}\n   full f ${r.full.f.toFixed(1)} ${fmt(r.full.x)}`); if (seen.length >= 6) break; }
  // bootstrap around the best rigid & full solutions
  const best = results[0];
  const boot = { rigid: [], full: [] };
  let seed = 12345; const rnd = () => (seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648;
  for (let b = 0; b < 25; b++) {
    const idx = pts.map(() => Math.floor(rnd() * pts.length)); const bp = idx.map(i => pts[i]), bw = idx.map(i => w[i]);
    const fb = scoreFn(field, bp, bw, pivot);
    // restart from several slides so the bootstrap can also jump between local optima
    let bestR = null;
    for (const du of [-2, -1, 0, 1, 2]) { const th = 83.25 * Math.PI / 180; const x0 = best.rigid.x.slice(0, 4); x0[1] += du * Math.cos(th); x0[3] += du * Math.sin(th);
      const r = nelderMead(p => fb([...p, 0, 0, 0]), x0, [0.5, 0.3, 0.1, 0.3], 200); if (!bestR || r.f > bestR.f) bestR = r; }
    boot.rigid.push([...bestR.x, 0, 0, 0]);
    boot.full.push(nelderMead(fb, best.full.x, [0.3, 0.2, 0.05, 0.2, 0.01, 0.3, 0.3], 300).x);
  }
  const stats = arr => arr[0].map((_, k) => { const v = arr.map(a => a[k]); const m = v.reduce((a, b) => a + b) / v.length; return [m, Math.sqrt(v.reduce((a, b) => a + (b - m) ** 2, 0) / v.length)]; });
  for (const kind of ['rigid', 'full']) { const st = stats(boot[kind]); console.log(` bootstrap ${kind}: ` + ['yaw', 'tx', 'ty', 'tz', 'logS', 'pitch', 'roll'].map((n, k) => `${n} ${st[k][0].toFixed(3)}±${st[k][1].toFixed(3)}`).join('  ')); }
  fs.writeFileSync(outPath, JSON.stringify({ best: { rigid: best.rigid.x, full: best.full.x, fRigid: best.rigid.f, fFull: best.full.f }, boot, pivot }, null, 1));
}
module.exports = { transform };
if (require.main === module) main();
