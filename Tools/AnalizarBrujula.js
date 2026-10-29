// ¿Sirve la brújula para saber qué edificio se mira? node Tools/AnalizarBrujula.js <carpeta con las sesiones de grabaciones>
// Lee los intentos*.jsonl del modo Grabar (1.4.2 en adelante guarda «brujula»: norte_real, norte_magnetico, precision, campo_ut).
// Mide, por sesión (= un punto, un edificio apuntado):
//   1) si el rumbo del teléfono es estable respecto de la cámara de AR: brújula − rumbo de AR debería ser constante mientras el
//      teléfono gira; si varía, la brújula no mide hacia dónde mira la cámara (o está pegada o perturbada);
//   2) el campo magnético (µT): ~25–65 es normal; fijo en un valor raro (p. ej. 61) = sensor pegado, como en el Xiaomi 25069PTEBG el 26-oct;
//   3) con el rumbo y el GPS, ¿el edificio cuyo rumbo desde el teléfono queda más cerca de la brújula es el apuntado?
//      (se prueba la brújula tal cual y corrida por la mediana de (brújula − rumbo de AR) de la sesión, que absorbe un sesgo fijo).
const fs = require('fs'), path = require('path');
const root = process.argv[2];
if (!root) { console.error('Uso: node Tools/AnalizarBrujula.js <carpeta con las sesiones>'); process.exit(1); }
const repo = path.resolve(__dirname, '..');
const nm = { CienciasBasicas: 'CB', EIC: 'EIC', X1: 'X1', Teologia: 'TEO' };
const gpsB = {};
for (const d of Object.keys(nm)) gpsB[nm[d]] = JSON.parse(fs.readFileSync(path.join(repo, 'Assets/AncoRA/Paseo', d, 'edificio.json'), 'utf8')).gps;
const R = 6378137, rad = Math.PI / 180;
const bearing = (la, lo, b) => { const n = (b.latitud - la) * rad * R, e = (b.longitud - lo) * rad * R * Math.cos(la * rad); return (Math.atan2(e, n) / rad + 360) % 360; };
const diff = (a, b) => Math.abs(((a - b + 540) % 360) - 180);
const circMed = a => { // mediana circular aproximada: la muestra que minimiza la suma de distancias angulares
  let best = a[0], bestSum = Infinity;
  for (const x of a) { const s = a.reduce((t, y) => t + diff(x, y), 0); if (s < bestSum) { bestSum = s; best = x; } }
  return best;
};
const med = a => { a = a.slice().sort((x, y) => x - y); return a.length ? a[Math.floor((a.length - 1) / 2)] : NaN; };
const f = (x, d = 0) => Number.isFinite(x) ? x.toFixed(d) : '-';

const bySession = {};
for (const d of fs.readdirSync(root).filter(d => fs.statSync(path.join(root, d)).isDirectory())) {
  const seen = new Set();
  for (const file of fs.readdirSync(path.join(root, d)).filter(f => /^intentos.*\.jsonl$/.test(f)).sort())
    for (const line of fs.readFileSync(path.join(root, d, file), 'utf8').split('\n').filter(Boolean)) {
      const r = JSON.parse(line);
      if (seen.has(r.archivo)) continue;
      seen.add(r.archivo);
      (bySession[r.sesion] = bySession[r.sesion] || []).push(r);
    }
}
let withCompass = 0, total = 0;
const tot = { raw: [0, 0], shifted: [0, 0], near: [0, 0] };
console.log('sesión | punto | teléfono | con brújula | campo µT (min–max) | brújula − rumbo AR: mediana° / desv.° | acierta edificio: tal cual / corrida | el más cercano por GPS');
for (const s of Object.keys(bySession).sort()) {
  const a = bySession[s].sort((x, y) => x.n - y.n);
  total += a.length;
  const c = a.filter(r => r.brujula && r.gps && r.gps.util);
  withCompass += c.length;
  if (!c.length) { console.log(s.slice(9), a[0].punto.padEnd(9), a[0].telefono, '| sin brújula (grabado antes de la 1.4.2)'); continue; }
  const pt = a[0].punto.split('-')[0];
  const field = c.map(r => r.brujula.campo_ut).filter(Number.isFinite);
  const heading = r => Number.isFinite(r.brujula.norte_real) ? r.brujula.norte_real : r.brujula.norte_magnetico;
  const offs = c.map(r => (heading(r) - r.rumbo + 360) % 360);
  const off = circMed(offs), spread = Math.sqrt(offs.reduce((t, o) => t + diff(o, off) ** 2, 0) / offs.length);
  let okRaw = 0, okShift = 0, okNear = 0;
  for (const r of c) {
    const names = Object.keys(gpsB);
    const facing = h => names.map(k => [k, diff(h, bearing(r.gps.lat, r.gps.lon, gpsB[k]))]).sort((x, y) => x[1] - y[1])[0][0];
    if (facing(heading(r)) === pt) okRaw++;
    if (facing((r.rumbo + off) % 360) === pt) okShift++;
    const nearest = names.map(k => [k, Math.hypot((gpsB[k].latitud - r.gps.lat) * rad * R, (gpsB[k].longitud - r.gps.lon) * rad * R * Math.cos(r.gps.lat * rad))]).sort((x, y) => x[1] - y[1])[0][0];
    if (nearest === pt) okNear++;
  }
  tot.raw[0] += okRaw; tot.shifted[0] += okShift; tot.near[0] += okNear; for (const k of Object.keys(tot)) tot[k][1] += c.length;
  console.log(s.slice(9), a[0].punto.padEnd(9), a[0].telefono.replace(/Xiaomi |samsung /, '').padEnd(10), '|', c.length + '/' + a.length, '|',
    f(Math.min(...field)) + '–' + f(Math.max(...field)), '|', f(off) + ' / ' + f(spread), '|',
    f(100 * okRaw / c.length) + '% / ' + f(100 * okShift / c.length) + '%', '|', f(100 * okNear / c.length) + '%');
}
console.log(`\nintentos con brújula y GPS: ${withCompass} de ${total}`);
if (tot.raw[1]) console.log(`edificio apuntado acertado — brújula tal cual: ${f(100 * tot.raw[0] / tot.raw[1])} %, corrida por la sesión: ${f(100 * tot.shifted[0] / tot.shifted[1])} %, el más cercano por GPS: ${f(100 * tot.near[0] / tot.near[1])} %`);
console.log('Una desviación de (brújula − rumbo AR) de más de ~20° dentro de una sesión, o un campo clavado, quiere decir que la brújula no sirve en ese teléfono.');
