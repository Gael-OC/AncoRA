#!/usr/bin/env node
// Builds the 3D campus viewer (a folder with pagina.html + data files) that shows the app's campus frame:
// the drone cloud of 28-oct, each building's box, each Immersal map's points placed exactly as the scene places them
// (T = B_C · B_M⁻¹ from edificio.json «campus» and «caja», like verificar_campus.js), and the 1950/1980 era models
// stretched to each box like PaseoBuildingModelBuilder does.
//
//   node Tools/MapaCampus3D/armar.js <carpeta de salida>
//
// Frames. Unity / app: x east, y up, z north (left-handed). The page converts to three.js (right-handed) as
// (x, y, -z); a Unity yaw θ becomes rotation.y = -θ. Positions are written as Int16 centimetres.
const fs = require('fs'), path = require('path');
const R = require('../RegistroDron/register.js');

const REPO = path.resolve(__dirname, '..', '..');
const OUT = path.resolve(process.argv[2] || path.join(REPO, 'Registros', 'mapa-campus-3d'));
const PASEO = path.join(REPO, 'Assets', 'AncoRA', 'Paseo');
const MODELS = path.join(PASEO, '_FuentesModelos');
const CLOUD = path.join(REPO, 'Escenas', 'Campus', 'Campus.ply');
const RADIUS = 85;      // metres around the campus origin kept from the drone cloud
const VOXEL = 0.14;     // metres, drone cloud downsample

// What the field showed, by building (CLAUDE.md, field test of 29-oct with 1.4.3; nothing newer on site).
const FIELD = {
  Teologia: { estado: 'bien', resumen: 'El único edificio que busca la app de demo (un mapa a la vez, «Mapa demo»). El 29-oct quedó casi alineada, a ojo. La demo se hace desde aquí.', cara: 'Frente NO y costado NE' },
  X1: { estado: 'regular', resumen: 'La app no lo busca («buscar»: false): su caja aparece cuando ubica Teología. El 29-oct, cuando sí se buscaba, quedó desalineada a ojo; no hay medición.', cara: 'Frente SE' },
  CienciasBasicas: { estado: 'regular', resumen: 'La app no lo busca («buscar»: false): su caja aparece cuando ubica Teología. El 29-oct, cuando sí se buscaba, quedó desalineada a ojo; no hay medición.', cara: 'Frente SO' },
  EIC: { estado: 'mal', resumen: 'Oculta y sin búsqueda. Mapas débiles que no coinciden entre sí (2°, 1,5 m).', cara: 'Ninguna bien cubierta' }
};
const MAP_NOTES = {
  152693: 'sin probar en terreno', 152794: 'desde 1.3.8', 152795: 'provisional', 152242: '1 596 puntos'
};

fs.mkdirSync(OUT, { recursive: true });

// ---- drone cloud: ascii PLY, x east, y north, z up, rgb
const text = fs.readFileSync(CLOUD, 'utf8');
const lines = text.split('\n');
let i = 0; while (!lines[i].startsWith('end_header')) i++; i++;
const seen = new Set(), pos = [], col = [];
for (; i < lines.length; i++) {
  const a = lines[i].trim().split(/\s+/); if (a.length < 6) continue;
  const e = +a[0], n = +a[1], u = +a[2];
  if (e * e + n * n > RADIUS * RADIUS) continue;
  const k = Math.floor(e / VOXEL) + ',' + Math.floor(n / VOXEL) + ',' + Math.floor(u / VOXEL);
  if (seen.has(k)) continue; seen.add(k);
  pos.push(Math.round(e * 100), Math.round(u * 100), Math.round(-n * 100)); // three: (east, up, -north)
  col.push(+a[3], +a[4], +a[5]);
}
// Binary arrays go out as base64 text: artifact pages serve text and images, not raw binary.
const b64 = (name, typed) => fs.writeFileSync(path.join(OUT, name), Buffer.from(typed.buffer).toString('base64'));
b64('nube.txt', new Int16Array(pos));
b64('nube-color.txt', new Uint8Array(col));

// ---- buildings and maps
const grid = new R.Grid(R.readDrone(CLOUD, false), 0.5);
const buildings = [], mapPos = [], mapIdx = [], maps = [];
for (const folder of fs.readdirSync(PASEO).filter(f => !f.startsWith('_')).sort()) {
  const file = path.join(PASEO, folder, 'edificio.json');
  if (!fs.existsSync(file)) continue;
  const cfg = JSON.parse(fs.readFileSync(file, 'utf8'));
  if (!cfg.campus) continue;
  const b = {
    id: folder, nombre: cfg.nombre, centro: cfg.campus.posicion, giro: cfg.campus.giro, tamano: cfg.tamano,
    modelo: cfg.modelo || null, oculta: !!cfg.oculta, buscado: cfg.buscar !== false, frente: cfg.parque?.frente || null, mapas: [], ...FIELD[folder]
  };
  for (const m of cfg.mapas) {
    const ply = fs.readdirSync(path.join(REPO, 'Escenas', folder)).find(f => f.startsWith(`${m.id}-`) && f.endsWith('-sparse.ply'));
    if (!ply || !m.caja) continue;
    const yaw = cfg.campus.giro - m.caja.giro, r = R.rot(m.caja.posicion, yaw);
    const T = { yaw, s: 1, t: cfg.campus.posicion.map((v, k) => v - r[k]) };
    const M = R.readImmersal(path.join(REPO, 'Escenas', folder, ply));
    const s = R.score(M, grid, T);
    const index = maps.length;
    for (const p of M) {
      const q = R.rot(p, yaw);
      mapPos.push(Math.round((q[0] + T.t[0]) * 100), Math.round((q[1] + T.t[1]) * 100), Math.round(-(q[2] + T.t[2]) * 100));
      mapIdx.push(index);
    }
    const info = { id: m.id, edificio: folder, puntos: M.length, ajuste25: Math.round(100 * s.inl25 / M.length), nota: MAP_NOTES[m.id] || '' };
    maps.push(info); b.mapas.push(info);
  }
  buildings.push(b);
}
b64('mapas.txt', new Int16Array(mapPos));
b64('mapas-indice.txt', new Uint8Array(mapIdx));

// Demo viewpoint: in front of Teología's declared front, at the distance the Editor simulation settles on (~10.5 m:
// X1 stands ~13 m in front of that face).
const teo = buildings.find(b => b.id === 'Teologia');
let demo = null;
if (teo) {
  const t = teo.giro * Math.PI / 180, half = teo.tamano[2] / 2, d = half + 10.5;
  const fx = Math.sin(t), fz = Math.cos(t); // Unity +z local turned by the yaw
  demo = { x: teo.centro[0] + fx * d, z: teo.centro[2] + fz * d, distancia: 10.5 };
}

// ---- era models (unit box, floor at y = -0.5, Unity axes) and their textures
for (const era of ['1950', '1980']) {
  const dir = path.join(OUT, `modelo-${era}`);
  fs.mkdirSync(dir, { recursive: true });
  fs.copyFileSync(path.join(MODELS, era, 'modelo.json'), path.join(dir, 'modelo.json'));
  const tex = path.join(MODELS, era, 'Texturas');
  if (fs.existsSync(tex)) {
    fs.mkdirSync(path.join(dir, 'Texturas'), { recursive: true });
    for (const f of fs.readdirSync(tex).filter(f => f.endsWith('.jpg')))
      fs.copyFileSync(path.join(tex, f), path.join(dir, 'Texturas', f.replace(/ /g, '-')));
  }
}

const datos = { generado: new Date().toISOString().slice(0, 10), nubePuntos: pos.length / 3, edificios: buildings, mapas: maps, demo };
fs.writeFileSync(path.join(OUT, 'datos.json'), JSON.stringify(datos, null, 1));
fs.copyFileSync(path.join(__dirname, 'pagina.html'), path.join(OUT, 'pagina.html'));
console.log(`Listo en ${OUT}: ${pos.length / 3} puntos del dron, ${mapIdx.length} puntos de ${maps.length} mapas, ${buildings.length} edificios.`);
for (const m of maps) console.log(`  ${m.edificio} ${m.id}: ${m.ajuste25} % a < 25 cm de la nube (${m.puntos} puntos)`);
