// ¿Cuánto tarda en aparecer la caja con cada tope de resolución? node Tools/TiempoPrimeraUbicacion.js <barrido.jsonl>
// Entrada: la salida de PaseoReplay con -replayPixels. Para cada sesión (= un punto) se toman sus intentos en orden y se simula
// que la búsqueda empieza en cada uno de ellos: se cuenta cuántos intentos y cuántos segundos pasan hasta
//   a) la primera ubicación del mapa correcto (el filtro la mostraría si confianza >= umbral de primera ubicación), y
//   b) la caja mostrada de verdad: una ubicación con confianza >= umbral, o dos que coinciden (<= 0,6 m entre sí).
// Duración de un intento en el teléfono = telefono_seg × (tiempo del Mac con ese tope / tiempo del Mac con el tope del teléfono).
// Uso: <barrido.jsonl> [umbral=20] [regex de puntos, p. ej. TEO-[ACF]]. Entre intentos van 0,5 s (PaseoPace.FastSeconds). Solo imágenes en que el teléfono buscó mapas del edificio apuntado: lo que
// pasa al buscar en el edificio equivocado es asunto del plan de búsqueda, no de la resolución.
const fs = require('fs');
const rows = fs.readFileSync(process.argv[2], 'utf8').split('\n').filter(Boolean).map(JSON.parse);
const umbral = +(process.argv[3] || 20);
const B = { 152192: 'CB', 152196: 'CB', 152257: 'CB', 152242: 'EIC', 152795: 'EIC', 152259: 'TEO', 152693: 'TEO', 152796: 'TEO', 152198: 'X1', 152200: 'X1', 152794: 'X1' };
const caps = [...new Set(rows.map(r => +r.variante.slice(2)))].sort((a, b) => (a || 1e12) - (b || 1e12));
const base = 691200;
const imgs = {};
for (const r of rows) (imgs[r.sesion + '|' + r.imagen] = imgs[r.sesion + '|' + r.imagen] || {})[+r.variante.slice(2)] = r;
const all = Object.values(imgs).filter(v => caps.every(c => v[c]));
const own = all.filter(v => v[base].mapas.every(m => B[m] === v[base].punto.split('-')[0]));
const sessions = {};
for (const v of own) (sessions[v[base].sesion] = sessions[v[base].sesion] || []).push(v);
for (const s of Object.values(sessions)) s.sort((a, b) => a[base].n - b[base].n);

const med = a => { a = a.slice().sort((x, y) => x - y); return a.length ? a[Math.floor((a.length - 1) / 2)] : NaN; };
const pct = (a, p) => { a = a.slice().sort((x, y) => x - y); return a.length ? a[Math.min(a.length - 1, Math.floor(p * a.length))] : NaN; };
const f = (x, d = 1) => Number.isFinite(x) ? x.toFixed(d) : '-';
const dist = (p, q) => Math.hypot(p[0] - q[0], p[1] - q[1], p[2] - q[2]);
const name = c => c === 0 ? 'completa' : `${(c / 1e6).toFixed(2)} MP`;


const punto = new RegExp(process.argv[4] || '.');
for (const k of Object.keys(sessions)) if (!punto.test(sessions[k][0][base].punto)) delete sessions[k];
const H = 1228800, H2 = 1440000;
// Una estrategia elige el tope de cada intento según cuántos van desde que empezó la búsqueda (k = 0 es el primero).
const strategies = [
  ...caps.map(c => ({ name: name(c), pick: () => c })),
  { name: `mixta: 0.69 ×2, luego ${name(H)}`, pick: k => k < 2 ? base : H },
  { name: `mixta: alterna 0.69 / ${name(H)}`, pick: k => k % 2 ? H : base },
  { name: `mixta: 0.69 ×2, luego ${name(H2)}`, pick: k => k < 2 ? base : H2 },
  { name: `mixta: alterna 0.69 / ${name(H2)}`, pick: k => k % 2 ? H2 : base }
];
console.log(`sesiones: ${Object.keys(sessions).length}, intentos propios: ${Object.values(sessions).reduce((n, s) => n + s.length, 0)}, umbral de primera ubicación: ${umbral}, puntos: ${punto}`);
console.log('Duración de un intento en el teléfono = telefono_seg (tope actual) × razón del Mac por imagen.\n');
console.log('estrategia | 1er intento ubica | intentos a 1ª ubic. med (p90) | s a 1ª ubic. med / p90 | s a caja mostrada med / p90 | nunca (de N inicios)');
for (const st of strategies) {
  const first = [], firstAtt = [], shown = [];
  let starts = 0, firstHit = 0, never = 0;
  for (const sess of Object.values(sessions)) {
    for (let i = 0; i < sess.length; i++) {
      starts++;
      let t = 0, tFirst = NaN, nFirst = NaN, tShown = NaN;
      const got = [];
      for (let j = i; j < sess.length; j++) {
        const c = st.pick(j - i);
        const v = sess[j];
        t += Math.max(0.05, v[base].telefono_seg * (v[c].seg / Math.max(0.02, v[base].seg)));
        const r = v[c];
        if (r.mapa) {
          if (j === i) firstHit++;
          if (Number.isNaN(tFirst)) { tFirst = t; nFirst = j - i + 1; }
          if (r.conf >= umbral || got.some(g => g.mapa === r.mapa && dist(g.pos, r.pos) <= 0.6)) { tShown = t; break; }
          got.push(r);
        }
        t += 0.5;
      }
      if (Number.isNaN(tShown)) never++;
      if (!Number.isNaN(tFirst)) { first.push(tFirst); firstAtt.push(nFirst); }
      if (!Number.isNaN(tShown)) shown.push(tShown);
    }
  }
  console.log(st.name.padEnd(34), '|', f(100 * firstHit / starts, 0) + '%', '|', f(med(firstAtt), 0) + ' (p90 ' + f(pct(firstAtt, 0.9), 0) + ')', '|',
    f(med(first)) + ' / ' + f(pct(first, 0.9)), '|', f(med(shown)) + ' / ' + f(pct(shown, 0.9)), '|', never + '/' + starts);
}
console.log('\nLa cola de una sesión corta cuenta como «nunca»: pocos intentos restantes pueden no alcanzar. Es una cota, no una medición en terreno.');
