// Barrido de resolución de la reproducción: node Tools/AnalizarResolucion.js <barrido.jsonl>
// Entrada: la salida de PaseoReplay con -replayPixels (variantes px<tope>, solo los mapas que buscó el teléfono).
// Compara cada tope de LocalizationMaxPixels (0 = imagen completa) en tasa de ubicación, tiempo y estabilidad de la posición.
const fs = require('fs');
const rows = fs.readFileSync(process.argv[2], 'utf8').split('\n').filter(Boolean).map(JSON.parse);
const B = { 152192: 'CB', 152196: 'CB', 152257: 'CB', 152242: 'EIC', 152795: 'EIC', 152259: 'TEO', 152693: 'TEO', 152796: 'TEO', 152198: 'X1', 152200: 'X1', 152794: 'X1' };
const caps = [...new Set(rows.map(r => +r.variante.slice(2)))].sort((a, b) => (a || 1e12) - (b || 1e12));
const name = c => c === 0 ? 'completa' : `${c} (${(c / 1e6).toFixed(2)} MP)`;
const key = r => r.sesion + '|' + r.imagen;
const imgs = {};
for (const r of rows) (imgs[key(r)] = imgs[key(r)] || {})[+r.variante.slice(2)] = r;
const L = Object.values(imgs).filter(v => caps.every(c => v[c]));
const any = v => v[caps[0]];
const code = v => any(v).punto.split('-')[0];
// Solo imágenes en que el teléfono buscó únicamente mapas del edificio al que apunta (las demás no pueden ubicar bien).
const own = L.filter(v => any(v).mapas.every(m => B[m] === code(v)));
const med = a => { a = a.slice().sort((x, y) => x - y); return a.length ? a[Math.floor((a.length - 1) / 2)] : NaN; };
const pct = (a, p) => { a = a.slice().sort((x, y) => x - y); return a.length ? a[Math.min(a.length - 1, Math.floor(p * a.length))] : NaN; };
const f = (x, d = 2) => Number.isFinite(x) ? x.toFixed(d) : '-';
const dist = (p, q) => Math.hypot(p[0] - q[0], p[1] - q[1], p[2] - q[2]);

console.log(`imágenes con todos los topes: ${L.length}; con solo mapas del edificio apuntado: ${own.length}`);

// 1 tasa, confianza y tiempo
console.log('\n1) tasa de ubicación, confianza y tiempo por tope (tiempo del Mac, solo vale como razón entre topes)');
console.log('tope | ubica (todas) | ubica (mapas propios) | conf med | seg med | seg p90 | razón seg vs ' + name(caps[0]));
const base = med(L.map(v => v[caps[0]].seg));
for (const c of caps) {
  const s = L.map(v => v[c]);
  const ok = s.filter(r => r.mapa);
  const okOwn = own.filter(v => v[c].mapa);
  const t = s.map(r => r.seg);
  console.log(name(c).padEnd(22), '|', `${ok.length}/${L.length} ${f(100 * ok.length / L.length, 0)}%`, '|', `${okOwn.length}/${own.length} ${f(100 * okOwn.length / own.length, 0)}%`,
    '|', f(med(ok.map(r => r.conf)), 0), '|', f(med(t), 3), '|', f(pct(t, 0.9), 3), '|', f(med(t) / base, 2) + 'x');
}

// 2 mismo mapa y misma imagen contra la imagen completa: ¿se mueve la pose?
const full = caps.includes(0) ? 0 : caps[caps.length - 1];
console.log(`\n2) misma imagen contra «${name(full)}»: de las que ubican con ambos, ¿el mismo mapa?, ¿cuánto cambia la posición (m)?`);
console.log('tope | pares | otro mapa | cambio med | p90 | máx | > 1 m');
for (const c of caps) {
  if (c === full) continue;
  const pairs = L.filter(v => v[c].mapa && v[full].mapa);
  const same = pairs.filter(v => v[c].mapa === v[full].mapa);
  const d = same.map(v => dist(v[c].pos, v[full].pos));
  console.log(name(c).padEnd(22), '|', pairs.length, '|', pairs.length - same.length, '|', f(med(d)), '|', f(pct(d, 0.9)), '|', f(Math.max(...d, 0)), '|', d.filter(x => x > 1).length);
}

// 3 estabilidad: dispersión de la posición en el mapa dentro de un punto (misma sesión, punto y mapa). La referencia de cada grupo
// es la mediana de todas las poses de todos los topes; «suelta» = a más de 1 m de ella.
console.log('\n3) estabilidad: distancia de cada pose a la mediana de su punto y mapa (todos los topes juntos)');
const groups = {};
for (const v of L) for (const c of caps) {
  const r = v[c];
  if (!r.mapa || !r.pos) continue;
  const k = `${any(v).sesion}|${any(v).punto}|${r.mapa}`;
  (groups[k] = groups[k] || []).push({ c, pos: r.pos, v });
}
const ref = {};
for (const k of Object.keys(groups)) {
  const p = groups[k];
  if (p.length < 6) continue;
  ref[k] = [0, 1, 2].map(i => med(p.map(q => q.pos[i])));
}
console.log('tope | poses medidas | dist. med | p90 | máx | sueltas > 1 m');
const loose = {};
for (const c of caps) {
  const d = [];
  for (const k of Object.keys(ref)) for (const q of groups[k]) if (q.c === c) {
    const x = dist(q.pos, ref[k]);
    d.push(x);
    if (x > 1) (loose[c] = loose[c] || []).push(`${any(q.v).imagen} ${any(q.v).punto} mapa ${k.split('|')[2]} conf ${q.v[c].conf} a ${f(x, 1)} m`);
  }
  console.log(name(c).padEnd(22), '|', d.length, '|', f(med(d)), '|', f(pct(d, 0.9)), '|', f(Math.max(...d, 0)), '|', d.filter(x => x > 1).length);
}
for (const c of caps) if (loose[c]) { console.log(`\n  sueltas con ${name(c)}:`); loose[c].slice(0, 12).forEach(s => console.log('   ', s)); }

// 4 por punto, solo mapas propios: tasa por tope (dónde gana la imagen grande)
console.log('\n4) por punto (mapas propios): ubica por tope');
const by = {};
for (const v of own) { const k = `${any(v).punto} ${any(v).telefono.replace(/Xiaomi |samsung /, '')} ${any(v).sesion.slice(9)}`; (by[k] = by[k] || []).push(v); }
console.log('punto | N | ' + caps.map(name).join(' | '));
for (const k of Object.keys(by).sort()) {
  const a = by[k];
  console.log(k, '|', a.length, '|', caps.map(c => `${a.filter(v => v[c].mapa).length}/${a.length}`).join(' | '));
}

// 5 falsos: ubicaciones en un mapa de otro edificio
console.log('\n5) ubicaciones en un mapa de otro edificio (el teléfono buscó esos mapas)');
for (const c of caps) {
  const w = L.filter(v => v[c].mapa && B[v[c].mapa] !== code(v));
  console.log(name(c).padEnd(22), '|', w.length, w.slice(0, 6).map(v => `${any(v).imagen}→${v[c].mapa} conf ${v[c].conf}`).join('; '));
}
