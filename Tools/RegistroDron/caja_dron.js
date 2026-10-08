// Fits a building's box in its georeferenced drone cloud and expresses it in the frame of each registered map.
//
//   node Tools/RegistroDron/caja_dron.js <dron.ply> <salida.json> --region=E0,N0,E1,N1 --techo=zMin,zMax
//        [--claro=minBrillo] [--frente=E,N] [--escribir=Assets/AncoRA/Paseo/<Edificio>/edificio.json]
//        <registro.json> <mapa-sparse.ply> ...
//   or, when the footprint was measured another way (walls only, no roof in the cloud):
//        --huella=cE,cN,largo,fondo,anguloLargo --base=z --tope=z  (anguloLargo: degrees from East, counter-clockwise)
//
// Footprint: minimum-area rectangle of the roof points (drone points inside the East/North region whose height is in
// the roof band; with --claro, only light grey ones, which drops trees), trimmed 0.5 % per side against stray points. Top: 99th percentile of the roof. Base: median ground
// in the 1-6 m strip in front of the front face. Front (+Z of the box, the face with the X): the face looking towards
// the registered map points, i.e. where the map was scanned from, unless --frente gives a point to face.
//
// Each map gets the same box in its own frame: drone (E, Up, N) = rot(map, yaw) + t from registrar_mapa.js, so
// map = rot(drone - t, -yaw) and giro = boxYaw - yaw (Unity yaw: local +Z = (sin, 0, cos) in (E, N)).
// --escribir sets tamano and, per map, caja.posicion / caja.giro / colocada = true in edificio.json, adding the map
// entry if it is missing. These are estimates from the drone, not checked on site.
const fs = require('fs');
const R = require('./register.js');

const norm180 = a => ((a % 360) + 540) % 360 - 180;
const pct = (sorted, q) => sorted[Math.min(sorted.length - 1, Math.max(0, Math.floor(sorted.length * q)))];

// Points as [E, N, z, r, g, b].
function readEnu(path) {
  const L = fs.readFileSync(path, 'utf8').split('\n'); let h = 0; while (!L[h].startsWith('end_header')) h++;
  const P = []; for (let i = h + 1; i < L.length; i++) { const a = L[i].split(' '); if (a.length >= 6) P.push(a.slice(0, 6).map(Number)); }
  return P;
}
// Light, unsaturated colour: fibre-cement, concrete or painted roofs, not trees or shadow.
const isLight = (p, minBright) => Math.max(p[3], p[4], p[5]) >= minBright && Math.max(p[3], p[4], p[5]) - Math.min(p[3], p[4], p[5]) <= 45;

function minAreaRect(pts) {
  let best = null;
  for (let a = 0; a < 180; a += 0.25) {
    const c = Math.cos(a * Math.PI / 180), s = Math.sin(a * Math.PI / 180);
    const u = pts.map(p => p[0] * c + p[1] * s).sort((x, y) => x - y), v = pts.map(p => -p[0] * s + p[1] * c).sort((x, y) => x - y);
    const u0 = pct(u, 0.005), u1 = pct(u, 0.995), v0 = pct(v, 0.005), v1 = pct(v, 0.995);
    const area = (u1 - u0) * (v1 - v0);
    if (!best || area < best.area) best = { area, a, u0, u1, v0, v1 };
  }
  const c = Math.cos(best.a * Math.PI / 180), s = Math.sin(best.a * Math.PI / 180);
  const um = (best.u0 + best.u1) / 2, vm = (best.v0 + best.v1) / 2;
  return { centre: [um * c - vm * s, um * s + vm * c], axisU: [c, s], axisV: [-s, c], lenU: best.u1 - best.u0, lenV: best.v1 - best.v0 };
}

function main() {
  const args = process.argv.slice(2);
  const opt = k => { const a = args.find(x => x.startsWith(`--${k}=`)); return a ? a.slice(k.length + 3) : null; };
  const [dronPath, outPath, ...pairs] = args.filter(a => !a.startsWith('--'));
  const region = (opt('region') || '').split(',').map(Number), band = (opt('techo') || '').split(',').map(Number);
  const footprint = (opt('huella') || '').split(',').map(Number);
  const manual = footprint.length === 5 && opt('base') !== null && opt('tope') !== null;
  if (!outPath || (!manual && (region.length !== 4 || band.length !== 2)) || pairs.length % 2) {
    console.error('Uso: node caja_dron.js <dron.ply> <salida.json> (--region=E0,N0,E1,N1 --techo=zMin,zMax | --huella=cE,cN,largo,fondo,angulo --base=z --tope=z) [--claro=n] [--frente=E,N] [--escribir=edificio.json] <registro.json> <mapa.ply> ...');
    process.exit(2);
  }
  let rect, top, roofCount = 0, P = [];
  if (manual) {
    const [cE, cN, len, dep, ang] = footprint; const a = ang * Math.PI / 180;
    rect = { centre: [cE, cN], axisU: [Math.cos(a), Math.sin(a)], axisV: [-Math.sin(a), Math.cos(a)], lenU: len, lenV: dep };
    top = +opt('tope');
  } else {
    const [e0, n0, e1, n1] = region;
    const inRegion = p => p[0] >= Math.min(e0, e1) && p[0] <= Math.max(e0, e1) && p[1] >= Math.min(n0, n1) && p[1] <= Math.max(n0, n1);
    P = readEnu(dronPath).filter(inRegion);
    const minBright = opt('claro') === null ? 0 : +opt('claro');
    const roof = P.filter(p => p[2] >= band[0] && p[2] <= band[1] && (minBright === 0 || isLight(p, minBright)));
    if (roof.length < 200) throw new Error(`Solo ${roof.length} puntos de techo en la región: revisar --region y --techo.`);
    rect = minAreaRect(roof); roofCount = roof.length;
    top = pct(roof.map(p => p[2]).sort((a, b) => a - b), 0.99);
  }

  // Registered maps, their points in the drone frame (E, N) for the front direction.
  const maps = [];
  for (let k = 0; k < pairs.length; k += 2) {
    const reg = JSON.parse(fs.readFileSync(pairs[k], 'utf8'));
    const pts = R.readImmersal(pairs[k + 1]).map(m => R.apply({ yaw: reg.yaw, s: 1, t: reg.t }, m));
    const file = pairs[k + 1].split(/[\\/]/).pop();
    maps.push({ reg, id: +file.split('-')[0], archivo: file.replace(/-sparse\.ply$/, '.bytes'), pts });
  }
  let target = (opt('frente') || '').split(',').map(Number);
  if (target.length !== 2 || target.some(isNaN)) {
    const all = maps.flatMap(m => m.pts); if (!all.length) throw new Error('Sin mapas registrados: dar --frente=E,N.');
    target = [all.reduce((a, p) => a + p[0], 0) / all.length, all.reduce((a, p) => a + p[2], 0) / all.length];
  }
  const toTarget = [target[0] - rect.centre[0], target[1] - rect.centre[1]];
  const faces = [
    { n: rect.axisV, half: rect.lenV / 2, along: rect.lenU, across: rect.lenV },
    { n: rect.axisV.map(x => -x), half: rect.lenV / 2, along: rect.lenU, across: rect.lenV },
    { n: rect.axisU, half: rect.lenU / 2, along: rect.lenV, across: rect.lenU },
    { n: rect.axisU.map(x => -x), half: rect.lenU / 2, along: rect.lenV, across: rect.lenU },
  ];
  const front = faces.reduce((b, f) => (f.n[0] * toTarget[0] + f.n[1] * toTarget[1] > b.n[0] * toTarget[0] + b.n[1] * toTarget[1] ? f : b));
  // Ground: drone points 1-6 m in front of the front face, within its width, well below the roof band.
  const tangent = [-front.n[1], front.n[0]];
  const ground = manual ? [] : P.filter(p => {
    const d = [p[0] - rect.centre[0], p[1] - rect.centre[1]];
    const out = d[0] * front.n[0] + d[1] * front.n[1] - front.half, side = Math.abs(d[0] * tangent[0] + d[1] * tangent[1]);
    return out > 1 && out < 6 && side < front.along / 2 && p[2] < band[0] - 1.5;
  }).map(p => p[2]).sort((a, b) => a - b);
  if (!manual && ground.length < 50) throw new Error(`Solo ${ground.length} puntos de suelo frente a la fachada.`);
  const base = manual ? +opt('base') : pct(ground, 0.5);
  const size = [front.along, top - base, front.across]; // local X along the front face, Y up, Z through the building
  const boxYaw = Math.atan2(front.n[0], front.n[1]) * 180 / Math.PI;
  const centre = [rect.centre[0], (top + base) / 2, rect.centre[1]]; // drone (E, Up, N)

  console.log(manual
    ? `huella dada ${rect.lenU.toFixed(2)} x ${rect.lenV.toFixed(2)} m; base ${base.toFixed(2)}, tope ${top.toFixed(2)} -> alto ${(top - base).toFixed(2)} m`
    : `techo ${roofCount} puntos; huella ${rect.lenU.toFixed(2)} x ${rect.lenV.toFixed(2)} m; base ${base.toFixed(2)} (mediana de ${ground.length} puntos de suelo), techo ${top.toFixed(2)} -> alto ${(top - base).toFixed(2)} m`);
  console.log(`caja: ancho ${size[0].toFixed(2)} (fachada frontal), alto ${size[1].toFixed(2)}, fondo ${size[2].toFixed(2)}; centro E ${centre[0].toFixed(2)} N ${centre[2].toFixed(2)} z ${centre[1].toFixed(2)}; frente mira a ${norm180(90 - boxYaw).toFixed(1)}° desde el este (giro Unity ${boxYaw.toFixed(2)})`);
  const out = { nota: 'Estimada desde la nube del dron (caja_dron.js), sin verificar en terreno.', region, techo: band, huella: manual ? footprint : null, frente: target,
    huellaM: [rect.lenU, rect.lenV], baseZ: base, techoZ: top, tamano: size, centroEnu: [centre[0], centre[2], centre[1]], giroEnuUnity: boxYaw, mapas: [] };
  for (const m of maps) {
    const local = R.rot([centre[0] - m.reg.t[0], centre[1] - m.reg.t[1], centre[2] - m.reg.t[2]], -m.reg.yaw);
    const giro = norm180(boxYaw - m.reg.yaw);
    out.mapas.push({ id: m.id, archivo: m.archivo, posicion: local, giro, inliers25: m.reg.inliers25, puntosMapa: m.reg.puntosMapa });
    console.log(`  mapa ${m.id}: posicion [${local.map(v => v.toFixed(2))}] giro ${giro.toFixed(2)}  (registro: ${(100 * m.reg.inliers25 / m.reg.puntosMapa).toFixed(0)} % a < 25 cm)`);
  }
  fs.writeFileSync(outPath, JSON.stringify(out, null, 1));

  const target_ = opt('escribir');
  if (target_) {
    const cfg = JSON.parse(fs.readFileSync(target_, 'utf8'));
    const r2 = v => Math.round(v * 100) / 100;
    cfg.tamano = size.map(v => Math.round(v * 10) / 10);
    cfg.mapas = cfg.mapas || [];
    for (const m of out.mapas) {
      let entry = cfg.mapas.find(x => x.id === m.id);
      if (!entry) { entry = { id: m.id, archivo: m.archivo }; cfg.mapas.push(entry); }
      entry.caja = { posicion: m.posicion.map(r2), giro: r2(m.giro), colocada: true };
    }
    fs.writeFileSync(target_, JSON.stringify(cfg, null, 2) + '\n');
    console.log(`escrito ${target_}`);
  }
}
main();
