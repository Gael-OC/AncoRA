// Puts every building's box in the campus frame and writes it as «campus» in its edificio.json (PaseoBuildingFrame:
// one XR Space for all buildings). The boxes and maps of each building stay exactly as they are: only the relation
// between buildings is new.
//
//   node Tools/RegistroDron/campus_edificios.js Escenas/Campus/campus.json [--escribir]
//
// campus.json lists, per building, the transform from its own drone cloud to the campus cloud (yaw + translation in
// (E, Up, N), as registrar_campus.js prints «viejo->campus» per map and per cloud) and the old registration of its
// reference map. The box in the campus frame is B_C = T_campus<-cloud · T_cloud<-map · B_map for the reference map (the
// first map with a placed box), which is the same box in every map of the building.
const fs = require('fs'), path = require('path');
const R = require('./register.js');

const n360 = a => ((a % 360) + 360) % 360;
const compose = (outer, inner) => { const r = R.rot(inner.t, outer.yaw); return { yaw: outer.yaw + inner.yaw, t: [r[0] + outer.t[0], r[1] + outer.t[1], r[2] + outer.t[2]] }; };

function main() {
  const [cfgPath] = process.argv.slice(2).filter(a => !a.startsWith('--'));
  const write = process.argv.includes('--escribir');
  const root = path.resolve(path.dirname(cfgPath), '..', '..');
  const campus = JSON.parse(fs.readFileSync(cfgPath, 'utf8'));
  for (const [folder, b] of Object.entries(campus.edificios)) {
    const file = path.join(root, 'Assets', 'AncoRA', 'Paseo', folder, 'edificio.json');
    const cfg = JSON.parse(fs.readFileSync(file, 'utf8'));
    const ref = cfg.mapas.find(m => m.caja && m.caja.colocada) || cfg.mapas[0];
    const reg = JSON.parse(fs.readFileSync(path.join(root, 'Escenas', folder, `${ref.id}-registro.json`), 'utf8'));
    // Box pose as (yaw, t): Unity yaw and centre of the box in the map frame.
    const box = { yaw: ref.caja.giro, t: ref.caja.posicion };
    const inCloud = compose({ yaw: reg.yaw, t: reg.t }, box);
    const inCampus = compose({ yaw: b.yaw, t: b.t }, inCloud);
    const pose = { posicion: inCampus.t.map(v => Math.round(v * 100) / 100), giro: Math.round(n360(inCampus.yaw) * 100) / 100, nube: campus.nube };
    console.log(`${folder}: mapa de referencia ${ref.id}; caja en la nube del edificio E ${inCloud.t[0].toFixed(2)} N ${inCloud.t[2].toFixed(2)} giro ${n360(inCloud.yaw).toFixed(2)}`
      + ` -> campus E ${pose.posicion[0]} arriba ${pose.posicion[1]} N ${pose.posicion[2]} giro ${pose.giro} (${b.fuente})`);
    if (write) { cfg.campus = pose; fs.writeFileSync(file, JSON.stringify(cfg, null, 2) + '\n'); }
  }
  if (write) console.log('escrito campus en cada edificio.json');
}
main();
