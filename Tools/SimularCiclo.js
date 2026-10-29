// ¿Qué ciclo de búsqueda por edificio encuentra antes la caja? node Tools/SimularCiclo.js <propio.jsonl> <carpeta con las sesiones>
// propio.jsonl: salida de PaseoReplay con -replayPixels … -replayOwn (cada imagen buscada en TODOS los mapas de su edificio).
// Para cada sesión (= un punto, siempre el mismo edificio apuntado) se simula que la búsqueda empieza en cada imagen y que cada
// intento usa la imagen siguiente: el ciclo elige un rango (0 = edificio más cercano por GPS al centro de su caja, 1 = el
// segundo…) con el GPS de esa imagen, y solo si ese edificio es el apuntado el intento puede ubicar (su resultado sale de
// propio.jsonl); en otro edificio no ubica (los pocos falsos no se cuentan). Duración de un intento en el teléfono = lo que
// tardó la imagen en la grabación (telefono_seg) × razón del tope. Caja mostrada = confianza >= umbral del tope, o dos
// ubicaciones del mismo mapa a <= 0,6 m (PaseoPoseFilter en modo campus). Cota, no medición: 12 sesiones, todas a ~15 m.
const fs = require('fs'), path = require('path');
const rows = fs.readFileSync(process.argv[2], 'utf8').split('\n').filter(Boolean).map(JSON.parse);
const root = process.argv[3];
const repo = path.resolve(__dirname, '..');
const nm = { CienciasBasicas: 'CB', EIC: 'EIC', X1: 'X1', Teologia: 'TEO' };
const gpsB = {};
for (const d of Object.keys(nm)) gpsB[nm[d]] = JSON.parse(fs.readFileSync(path.join(repo, 'Assets/AncoRA/Paseo', d, 'edificio.json'), 'utf8')).gps;
const R = 6378137, rad = Math.PI / 180;
const dist = (la, lo, b) => Math.hypot((b.latitud - la) * rad * R, (b.longitud - lo) * rad * R * Math.cos((la + b.latitud) / 2 * rad));
const d3 = (p, q) => Math.hypot(p[0] - q[0], p[1] - q[1], p[2] - q[2]);

// GPS por imagen, de intentos*.jsonl
const gps = {};
for (const d of fs.readdirSync(root).filter(d => fs.statSync(path.join(root, d)).isDirectory()))
  for (const f of fs.readdirSync(path.join(root, d)).filter(f => /^intentos.*\.jsonl$/.test(f)))
    for (const l of fs.readFileSync(path.join(root, d, f), 'utf8').split('\n').filter(Boolean)) {
      const r = JSON.parse(l);
      gps[r.sesion + '|' + r.archivo] = r.gps && r.gps.util ? r.gps : null;
    }

const caps = [...new Set(rows.map(r => +r.variante.slice(2)))].sort((a, b) => a - b);
const factor = { 691200: 1, 1228800: 2.14, 1440000: 2.57, 0: 4.07 };
const umbral = { 691200: 20, 1228800: 30, 1440000: 35, 0: 45 };
const img = {};
for (const r of rows) (img[r.sesion + '|' + r.imagen] = img[r.sesion + '|' + r.imagen] || { meta: r })[+r.variante.slice(2)] = r;
const sessions = {};
for (const v of Object.values(img)) (sessions[v.meta.sesion] = sessions[v.meta.sesion] || []).push(v);
for (const s of Object.values(sessions)) s.sort((a, b) => a.meta.n - b.meta.n);
// Opcional: tercer argumento = expresión regular de los puntos a simular (p. ej. «TEO-C»).
if (process.argv[4]) for (const k of Object.keys(sessions)) if (!new RegExp(process.argv[4]).test(sessions[k][0].meta.punto)) delete sessions[k];

const policies = [
  { name: 'actual 0,1,0,2,0,3', cycle: [0, 1, 0, 2, 0, 3] },
  { name: '0,1 (alterna los dos más cercanos)', cycle: [0, 1] },
  { name: '0,1,0,1,0,1,2,0,1,3', cycle: [0, 1, 0, 1, 0, 1, 2, 0, 1, 3] },
  { name: '0,0,1', cycle: [0, 0, 1] },
  { name: '0,1,2,0,1,3 (reparte más)', cycle: [0, 1, 2, 0, 1, 3] },
  { name: '0,1,0,1,2,0,1,0,1,3', cycle: [0, 1, 0, 1, 2, 0, 1, 0, 1, 3] },
  { name: '0,1,2 (los tres más cercanos)', cycle: [0, 1, 2] },
  { name: 'solo 0 (el más cercano)', cycle: [0] },
  { name: 'cercanos <= 30 m, 1 de 6 fuera', pick: plausible(30, 6) },
  { name: 'cercanos <= 35 m, 1 de 6 fuera', pick: plausible(35, 6) },
  { name: 'cercanos <= 40 m, 1 de 6 fuera', pick: plausible(40, 6) },
  { name: 'cercanos <= 35 m, 1 de 10 fuera', pick: plausible(35, 10) },
  { name: 'ideal (siempre el apuntado)', cycle: null }
];
const med = a => { a = a.slice().sort((x, y) => x - y); return a.length ? a[Math.floor((a.length - 1) / 2)] : NaN; };
const pct = (a, p) => { a = a.slice().sort((x, y) => x - y); return a.length ? a[Math.min(a.length - 1, Math.floor(p * a.length))] : NaN; };
const f = (x, d = 1) => Number.isFinite(x) ? x.toFixed(d) : '-';

function rankedAt(v) {
  const g = gps[v.meta.sesion + '|' + v.meta.imagen];
  if (!g) return null;
  return Object.keys(gpsB).map(k => [k, dist(g.lat, g.lon, gpsB[k])]).sort((a, b) => a[1] - b[1]);
}

// Edificios dentro de `radius` m del centro de su caja (siempre al menos el más cercano), cerca primero. El intento k busca el
// (k mod n)-ésimo; cada `outsideEvery` intentos se prueba, en vez, uno de los que quedan fuera (rotando), para no dejarlos nunca.
function plausible(radius, outsideEvery) {
  return (ranked, k) => {
    const near = ranked.filter(x => x[1] <= radius), far = ranked.filter(x => x[1] > radius);
    const cand = near.length ? near : ranked.slice(0, 1);
    const outside = near.length ? far : ranked.slice(1);
    if (outsideEvery && outside.length && k % outsideEvery === outsideEvery - 1)
      return outside[Math.floor(k / outsideEvery) % outside.length][0];
    return cand[k % cand.length][0];
  };
}

for (const cap of caps) {
  console.log(`\n=== tope ${cap || 'completa'} (umbral de primera ubicación ${umbral[cap]}) ===`);
  console.log('ciclo | % intentos en el edificio apuntado | s a 1ª ubicación med / p90 | s a caja mostrada med / p90 | nunca (de N inicios)');
  for (const p of policies) {
    const first = [], shown = [];
    let starts = 0, never = 0, att = 0, right = 0;
    for (const s of Object.values(sessions)) {
      const pointed = s[0].meta.punto.split('-')[0];
      for (let i = 0; i < s.length; i++) {
        starts++;
        let t = 0, tFirst = NaN, tShown = NaN;
        const got = [];
        for (let j = i; j < s.length; j++) {
          const v = s[j], k = j - i;
          let aimed = true;
          if (p.cycle || p.pick) {
            const ranked = rankedAt(v);
            aimed = !ranked ? false
              : p.pick ? p.pick(ranked, k) === pointed
              : ranked[Math.min(p.cycle[k % p.cycle.length], ranked.length - 1)][0] === pointed;
          }
          att++;
          if (aimed) right++;
          t += Math.max(0.05, v.meta.telefono_seg) * (factor[cap] || 1);
          const r = v[cap];
          if (aimed && r && r.mapa) {
            if (Number.isNaN(tFirst)) tFirst = t;
            if (r.conf >= umbral[cap] || got.some(g => g.mapa === r.mapa && d3(g.pos, r.pos) <= 0.6)) { tShown = t; break; }
            got.push(r);
          }
          t += 0.5;
        }
        if (Number.isNaN(tShown)) never++;
        if (!Number.isNaN(tFirst)) first.push(tFirst);
        if (!Number.isNaN(tShown)) shown.push(tShown);
      }
    }
    console.log(p.name.padEnd(36), '|', f(100 * right / att, 0) + '%', '|', f(med(first)) + ' / ' + f(pct(first, 0.9)), '|', f(med(shown)) + ' / ' + f(pct(shown, 0.9)), '|', never + '/' + starts);
  }
}
console.log('\nLa cola de una sesión corta cuenta como «nunca»: pocos intentos restantes pueden no alcanzar.');
