// End-to-end check of campus mode with only what the app uses: each map's points, moved by the transform the scene
// gives its XR Map (T = B_C · B_M⁻¹ from edificio.json «campus» and «caja»), against the campus drone cloud.
//
//   node Tools/RegistroDron/verificar_campus.js <campus.ply>
//
// A map that lands on the cloud (a high share of points within 25 cm) is placed right in the shared frame; the box
// of its building is then where the drone cloud says, relative to every other building.
const fs = require('fs'), path = require('path');
const R = require('./register.js');

const root = path.resolve(__dirname, '..', '..');
const C = R.readDrone(process.argv[2], false), grid = new R.Grid(C, 0.5);
for (const folder of fs.readdirSync(path.join(root, 'Assets', 'AncoRA', 'Paseo')).filter(f => !f.startsWith('_'))) {
  const file = path.join(root, 'Assets', 'AncoRA', 'Paseo', folder, 'edificio.json');
  if (!fs.existsSync(file)) continue;
  const cfg = JSON.parse(fs.readFileSync(file, 'utf8'));
  if (!cfg.campus) { console.log(`${folder}: sin «campus»`); continue; }
  for (const m of cfg.mapas) {
    const ply = fs.readdirSync(path.join(root, 'Escenas', folder)).find(f => f.startsWith(`${m.id}-`) && f.endsWith('-sparse.ply'));
    if (!ply) { console.log(`  ${m.id}: sin nube del mapa en Escenas/${folder}`); continue; }
    const yaw = cfg.campus.giro - m.caja.giro, r = R.rot(m.caja.posicion, yaw);
    const T = { yaw, s: 1, t: cfg.campus.posicion.map((v, k) => v - r[k]) };
    const M = R.readImmersal(path.join(root, 'Escenas', folder, ply));
    const s = R.score(M, grid, T);
    console.log(`${folder} ${m.id}: ${(100 * s.inl25 / M.length).toFixed(0)} % a < 25 cm, ${(100 * s.inl / M.length).toFixed(0)} % a < 50 cm (rms ${s.rms.toFixed(2)} m, ${M.length} puntos)`);
  }
}
