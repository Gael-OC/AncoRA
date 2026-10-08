// Georeferences a RealityScan reconstruction with the DJI GPS stored in each photo's XMP.
//
//   node Tools/RegistroDron/georef_dron.js <colmap images.txt> <in.ply> <out.ply> <out.json> <photo folder> [more folders]
//
// Why: DJI Fly exports carry no latitude/longitude in EXIF, only in XMP (drone-dji:GpsLatitude/...). RealityScan
// reads them as soft camera priors but the scale it ends with is not reliable (0.99 to 1.42 between buildings), while
// the GPS track itself matches the reconstructed cameras to ~0.1-0.2 m. So the scale and the vertical come from here:
// a similarity (Umeyama) from the reconstructed camera centres to local East-North-Up metres.
//
// Flights: the XMP absolute altitude carries a different offset on every flight (37 m between two days, 2.45 m between
// two flights of the same day) and the horizontal GPS bias also shifts between flights (~2 m on EIC), so every flight
// but the one with the most photos gets its own 3D offset. Flights are told apart by (AbsoluteAltitude -
// RelativeAltitude), constant within one flight. Up = RelativeAltitude of the main flight: z = 0 is its take-off point.
//
// Output PLY: same ASCII layout as RealityScan's (x y z red green blue), x = East, y = North, z = Up, metres, origin at
// (lat0, lon0) from the JSON. COLMAP axes are (x, -z, y) of RealityScan's PLY axes.
const fs = require('fs'), path = require('path');

function readXmpGps(dirs) {
  const out = {};
  for (const dir of dirs) for (const f of fs.readdirSync(dir).filter(n => /\.jpe?g$/i.test(n))) {
    const fd = fs.openSync(path.join(dir, f), 'r'); const buf = Buffer.alloc(200000);
    const n = fs.readSync(fd, buf, 0, buf.length, 0); fs.closeSync(fd);
    const s = buf.slice(0, n).toString('latin1');
    const tag = t => { const m = new RegExp('drone-dji:' + t + '="([^"]*)"').exec(s); return m ? +m[1] : NaN; };
    const abs = tag('AbsoluteAltitude'), rel = tag('RelativeAltitude');
    out[f] = { lat: tag('GpsLatitude'), lon: tag('GpsLongitude'), rel, flight: (abs - rel).toFixed(2) };
  }
  return out;
}

function readColmapCentres(imagesTxt) {
  const L = fs.readFileSync(imagesTxt, 'utf8').split('\n').filter(l => l.trim() && !l.startsWith('#'));
  const cams = [];
  for (let i = 0; i < L.length; i += 2) {
    const a = L[i].trim().split(/\s+/); const [qw, qx, qy, qz, tx, ty, tz] = a.slice(1, 8).map(Number);
    const R = [[1 - 2 * (qy * qy + qz * qz), 2 * (qx * qy - qz * qw), 2 * (qx * qz + qy * qw)],
      [2 * (qx * qy + qz * qw), 1 - 2 * (qx * qx + qz * qz), 2 * (qy * qz - qx * qw)],
      [2 * (qx * qz - qy * qw), 2 * (qy * qz + qx * qw), 1 - 2 * (qx * qx + qy * qy)]];
    cams.push({ name: a[9], C: [0, 1, 2].map(j => -(R[0][j] * tx + R[1][j] * ty + R[2][j] * tz)) }); // C = -R^T t
  }
  return cams;
}

// Jacobi eigen-decomposition of a symmetric 3x3 matrix (eigenvectors are the columns of V).
function eigSym(A) {
  const B = A.map(r => r.slice()); const V = [[1, 0, 0], [0, 1, 0], [0, 0, 1]];
  for (let sweep = 0; sweep < 60; sweep++) for (const [i, j] of [[0, 1], [0, 2], [1, 2]]) {
    if (Math.abs(B[i][j]) < 1e-14) continue;
    const th = 0.5 * Math.atan2(2 * B[i][j], B[j][j] - B[i][i]), c = Math.cos(th), s = Math.sin(th);
    for (let r = 0; r < 3; r++) { const x = B[r][i], y = B[r][j]; B[r][i] = c * x - s * y; B[r][j] = s * x + c * y; }
    for (let r = 0; r < 3; r++) { const x = B[i][r], y = B[j][r]; B[i][r] = c * x - s * y; B[j][r] = s * x + c * y; }
    for (let r = 0; r < 3; r++) { const x = V[r][i], y = V[r][j]; V[r][i] = c * x - s * y; V[r][j] = s * x + c * y; }
  }
  return { V, w: [B[0][0], B[1][1], B[2][2]] };
}

// Umeyama on centred point sets: q ≈ s R p.
function similarity(p, q) {
  const H = [[0, 0, 0], [0, 0, 0], [0, 0, 0]];
  for (let i = 0; i < p.length; i++) for (let a = 0; a < 3; a++) for (let b = 0; b < 3; b++) H[a][b] += p[i][a] * q[i][b];
  const HtH = [0, 1, 2].map(i => [0, 1, 2].map(j => H[0][i] * H[0][j] + H[1][i] * H[1][j] + H[2][i] * H[2][j]));
  const { V, w } = eigSym(HtH); const S = w.map(x => Math.sqrt(Math.max(0, x)));
  const U = [0, 1, 2].map(r => [0, 1, 2].map(c => (H[r][0] * V[0][c] + H[r][1] * V[1][c] + H[r][2] * V[2][c]) / S[c]));
  const det = M => M[0][0] * (M[1][1] * M[2][2] - M[1][2] * M[2][1]) - M[0][1] * (M[1][0] * M[2][2] - M[1][2] * M[2][0]) + M[0][2] * (M[1][0] * M[2][1] - M[1][1] * M[2][0]);
  const build = sign => [0, 1, 2].map(r => [0, 1, 2].map(c => [0, 1, 2].reduce((acc, k) => acc + V[r][k] * U[c][k] * sign[k], 0)));
  const sign = [1, 1, 1]; let R = build(sign);
  if (det(R) < 0) { sign[S.indexOf(Math.min(...S))] = -1; R = build(sign); }
  const varP = p.reduce((acc, v) => acc + v[0] ** 2 + v[1] ** 2 + v[2] ** 2, 0);
  return { s: S.reduce((acc, x, k) => acc + x * sign[k], 0) / varP, R };
}

const mean = A => [0, 1, 2].map(j => A.reduce((a, v) => a + v[j], 0) / A.length);
const mulRv = (R, v) => [0, 1, 2].map(a => R[a][0] * v[0] + R[a][1] * v[1] + R[a][2] * v[2]);
const pct = (v, q) => v[Math.min(v.length - 1, Math.floor(v.length * q))];

function fit(cams, gps) {
  const k = Math.PI / 180, Re = 6378137;
  const lat0 = cams.reduce((a, c) => a + gps[c.name].lat, 0) / cams.length;
  const lon0 = cams.reduce((a, c) => a + gps[c.name].lon, 0) / cams.length;
  const count = {}; for (const c of cams) count[gps[c.name].flight] = (count[gps[c.name].flight] || 0) + 1;
  const flights = Object.keys(count).sort((a, b) => count[b] - count[a]);
  // GPS of a different flight carries its own bias (metres, horizontal too), so every flight but the main one gets a
  // 3D offset: it still constrains shape, scale and tilt, but not the absolute position.
  const offset = Object.fromEntries(flights.map(f => [f, [0, 0, 0]]));
  const P = cams.map(c => c.C);
  const Q0 = cams.map(c => { const g = gps[c.name]; return [(g.lon - lon0) * k * Re * Math.cos(lat0 * k), (g.lat - lat0) * k * Re, g.rel]; });
  const shifted = () => Q0.map((q, i) => { const o = offset[gps[cams[i].name].flight]; return [q[0] + o[0], q[1] + o[1], q[2] + o[2]]; });
  let T;
  for (let it = 0; it < 200; it++) {
    const Q = shifted();
    const mp = mean(P), mq = mean(Q);
    const { s, R } = similarity(P.map(v => v.map((x, j) => x - mp[j])), Q.map(v => v.map((x, j) => x - mq[j])));
    T = { s, R, mp, mq };
    // Offset update = mean residual of each flight, relative to the main flight's.
    const sum = {}, n = {};
    cams.forEach((c, i) => { const f = gps[c.name].flight; const pr = apply(T, P[i]); sum[f] = (sum[f] || [0, 0, 0]).map((v, a) => v + pr[a] - Q[i][a]); n[f] = (n[f] || 0) + 1; });
    const ref = sum[flights[0]].map(v => v / n[flights[0]]); let moved = 0;
    for (const f of flights.slice(1)) for (let a = 0; a < 3; a++) { const d = sum[f][a] / n[f] - ref[a]; offset[f][a] += d; moved += Math.abs(d); }
    if (moved < 1e-5) break;
  }
  const Q = shifted();
  const res = cams.map((c, i) => { const pr = apply(T, P[i]); return { name: c.name, d: Math.hypot(pr[0] - Q[i][0], pr[1] - Q[i][1], pr[2] - Q[i][2]), dz: pr[2] - Q[i][2] }; });
  return { lat0, lon0, flights, count, offset, T, res };
}
const apply = (T, c) => { const r = mulRv(T.R, [c[0] - T.mp[0], c[1] - T.mp[1], c[2] - T.mp[2]]); return [T.s * r[0] + T.mq[0], T.s * r[1] + T.mq[1], T.s * r[2] + T.mq[2]]; };

function main() {
  const [imagesTxt, inPly, outPly, outJson, ...dirs] = process.argv.slice(2);
  if (!dirs.length) { console.error('Uso: node georef_dron.js <images.txt> <in.ply> <out.ply> <out.json> <carpeta fotos> [...]'); process.exit(2); }
  const gps = readXmpGps(dirs);
  const all = readColmapCentres(imagesTxt);
  const cams = all.filter(c => gps[c.name] && isFinite(gps[c.name].lat) && isFinite(gps[c.name].rel));
  if (cams.length < 5) { console.error(`Solo ${cams.length} cámaras con GPS en el XMP.`); process.exit(1); }
  const F = fit(cams, gps);
  const { s, R } = F.T;
  const tilt = Math.acos(Math.min(1, Math.abs(R[2][1]))) * 180 / Math.PI; // COLMAP -y is RealityScan's up
  const d = F.res.map(r => r.d).sort((a, b) => a - b);
  const registered = new Set(all.map(c => c.name));
  const missing = Object.keys(gps).filter(n => !registered.has(n)).sort();

  const fmtOffset = o => o.every(v => v === 0) ? 'principal' : `corrección [${o.map(v => v.toFixed(2))}] m`;
  console.log(`${cams.length} cámaras con GPS (${missing.length} fotos sin alinear); vuelos: ${F.flights.map(f => `${f} (${F.count[f]} fotos, ${fmtOffset(F.offset[f])})`).join(', ')}`);
  console.log(`escala aplicada = ${s.toFixed(4)}; la vertical de RealityScan estaba inclinada ${tilt.toFixed(2)}°`);
  console.log(`residuo cámara↔GPS: mediana ${pct(d, .5).toFixed(2)} m, p90 ${pct(d, .9).toFixed(2)} m, máx ${d[d.length - 1].toFixed(2)} m`);
  if (F.flights.length > 1) for (const f of F.flights) {
    const df = F.res.filter(r => gps[r.name].flight === f).map(r => r.d).sort((a, b) => a - b);
    console.log(`  vuelo ${f}: residuo mediano ${pct(df, .5).toFixed(2)} m, máx ${df[df.length - 1].toFixed(2)} m`);
  }
  if (pct(d, .5) > 0.5) console.log('ATENCIÓN: el GPS no calza con la reconstrucción (residuo mediano > 0,5 m). Revisar antes de usar esta nube.');

  // Transform the PLY.
  const lines = fs.readFileSync(inPly, 'utf8').split('\n'); let h = 0; while (!lines[h].startsWith('end_header')) h++;
  const out = []; let zs = [];
  for (let i = h + 1; i < lines.length; i++) {
    const a = lines[i].trim().split(/\s+/); if (a.length < 3) continue;
    const e = apply(F.T, [+a[0], -a[2], +a[1]]); zs.push(e[2]);
    out.push(`${e[0].toFixed(4)} ${e[1].toFixed(4)} ${e[2].toFixed(4)} ${a.slice(3).join(' ')}`);
  }
  const header = lines.slice(0, h + 1).map(l => l.startsWith('element vertex') ? `element vertex ${out.length}` : l.startsWith('comment Created') ? `${l}\ncomment Georreferenciada con el GPS del dron (georef_dron.js): x este, y norte, z arriba, metros` : l);
  fs.writeFileSync(outPly, header.join('\n') + '\n' + out.join('\n') + '\n');
  zs.sort((a, b) => a - b);
  console.log(`PLY: ${out.length} puntos -> ${outPly}; suelo aproximado (z p5) ${pct(zs, .05).toFixed(2)} m`);

  fs.writeFileSync(outJson, JSON.stringify({
    nota: 'x = este, y = norte, z = arriba (metros), origen en lat0/lon0; z = 0 en el despegue del vuelo principal. ENU = escala * R * (colmap - centroColmap) + centroEnu; colmap = (x, -z, y) del PLY de RealityScan.',
    lat0: F.lat0, lon0: F.lon0,
    escala: s, R, centroColmap: F.T.mp, centroEnu: F.T.mq,
    inclinacionOriginalGrados: tilt,
    vuelos: F.flights.map(f => ({ absMenosRel: +f, fotos: F.count[f], correccionM: F.offset[f] })),
    residuoM: { mediana: pct(d, .5), p90: pct(d, .9), max: d[d.length - 1] },
    camaras: cams.length, fotosSinAlinear: missing,
  }, null, 1));
}
main();
