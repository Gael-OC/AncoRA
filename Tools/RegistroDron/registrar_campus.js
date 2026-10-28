// Moves one building's map registrations from its own drone cloud onto the campus cloud (one RealityScan model of
// every building), without the blind search of registrar_mapa.js: over a whole campus that search slides onto the
// wrong facade (152192: 33 % at a wrong yaw, against 74 % on its building cloud).
//
//   node Tools/RegistroDron/registrar_campus.js <campus-recorte.ply> <campus-georef.json> <edificio.ply> <edificio-georef.json>
//        <salida-dir> <registro-viejo.json> <mapa-sparse.ply> [<registro-viejo.json> <mapa-sparse.ply> ...]
//
// 1. Cloud to cloud: the building cloud onto the campus one (yaw + translation, scale 1). Both are drone clouds seen
//    from above, so they overlap far more than a map does; the start comes from the two GPS origins (lat0/lon0) and
//    the ground heights, and each flight's GPS bias (up to ~4 m) is what ICP removes.
// 2. Each map: its old registration composed with step 1 is the start, then the same fine ICP as registrar_mapa.js
//    on the campus cloud. Old alternatives are tried too and the best one wins.
// Writes <salida-dir>/<edificio>-campus.json (step 1) and <id>-registro.json per map, in registrar_mapa.js's format
// (campus (E, Up, N) = rot(map Unity local, yaw) + t), so caja_dron.js reads them unchanged.
const fs = require('fs'), path = require('path');
const R = require('./register.js');

const n180 = a => ((a % 360) + 540) % 360 - 180;
const compose = (outer, inner) => { const r = R.rot(inner.t, outer.yaw); return { yaw: n180(outer.yaw + inner.yaw), s: 1, t: [r[0] + outer.t[0], r[1] + outer.t[1], r[2] + outer.t[2]] }; };
const subsample = (P, n) => { const step = Math.max(1, Math.floor(P.length / n)); return P.filter((_, i) => i % step === 0); };
const pct = (a, b) => (100 * a / b).toFixed(0);

function enuOffset(from, to) {
  const k = Math.PI / 180, Re = 6378137;
  return [(from.lon0 - to.lon0) * k * Re * Math.cos(to.lat0 * k), (from.lat0 - to.lat0) * k * Re];
}

function main() {
  const [campusPly, campusGeo, bPly, bGeo, outDir, ...pairs] = process.argv.slice(2);
  if (!pairs.length || pairs.length % 2) { console.error('Uso: ver la cabecera de registrar_campus.js'); process.exit(2); }
  fs.mkdirSync(outDir, { recursive: true });
  const C = R.readDrone(campusPly, false), B = R.readDrone(bPly, false);
  const gC = R.ground(C), gB = R.ground(B);
  const [dE, dN] = enuOffset(JSON.parse(fs.readFileSync(bGeo, 'utf8')), JSON.parse(fs.readFileSync(campusGeo, 'utf8')));
  const grid = new R.Grid(C, 1), fine = new R.Grid(C, 0.5);
  // Grid.nearest visits (2r+1)^3 cells per query, so the wide first passes use few points.
  const BC = subsample(B, 2500), BS = subsample(B, 10000);
  const name = path.basename(bPly, '.ply');

  // Step 1, from a few yaws around north: the two GPS frames share north to a degree or two.
  const tries = [-3, 0, 3].map(y => {
    const coarse = R.icp(BC, grid, { yaw: y, s: 1, t: [dE, gC - gB, dN] }, [4, 3, 2, 1.5, 1], false);
    const f = R.icp(BS, fine, coarse.T, [.7, .5, .35, .25, .2], false);
    return { T: f.T, ...R.score(BS, fine, f.T) };
  }).sort((a, b) => b.inl25 - a.inl25);
  const cloud = tries[0];
  const scaled = R.icp(BS, fine, cloud.T, [.5, .35, .25], true);
  console.log(`${name} -> campus: ${C.length} puntos campus, ${B.length} edificio; arranque GPS E ${dE.toFixed(2)} N ${dN.toFixed(2)} dz ${(gC - gB).toFixed(2)}`);
  console.log(`  giro ${cloud.T.yaw.toFixed(2)}° t [${cloud.T.t.map(v => v.toFixed(2))}]  <25cm ${pct(cloud.inl25, BS.length)} %  rms ${cloud.rms.toFixed(3)}  escala libre ${scaled.T.s.toFixed(4)}`);
  for (const t of tries.slice(1)) if (Math.hypot(t.T.t[0] - cloud.T.t[0], t.T.t[2] - cloud.T.t[2]) > 0.5 || Math.abs(n180(t.T.yaw - cloud.T.yaw)) > 1)
    console.log(`  otra solución: giro ${t.T.yaw.toFixed(2)}° t [${t.T.t.map(v => v.toFixed(2))}] <25cm ${pct(t.inl25, BS.length)} %`);
  fs.writeFileSync(path.join(outDir, `${name}-campus.json`), JSON.stringify({
    nota: 'campus (E, arriba, N) = rot(edificio (E, arriba, N), yaw) + t; rot() de register.js. Escala 1.',
    campus: path.basename(campusPly), edificio: path.basename(bPly), yaw: cloud.T.yaw, t: cloud.T.t,
    inliers25: cloud.inl25, puntos: BS.length, rms: cloud.rms, escalaLibre: scaled.T.s }, null, 1));

  // Step 2.
  for (let k = 0; k < pairs.length; k += 2) {
    const old = JSON.parse(fs.readFileSync(pairs[k], 'utf8')), mapPath = pairs[k + 1];
    const M = R.readImmersal(mapPath), MI = subsample(M, 4000);
    const starts = [{ yaw: old.yaw, t: old.t }, ...(old.alternativas || [])].map(o => compose(cloud.T, { yaw: o.yaw, t: o.t }));
    const results = starts.map(T0 => {
      const a = R.icp(MI, grid, T0, [2, 1.5, 1, .7, .5], false);
      const b = R.icp(MI, fine, a.T, [.5, .35, .25, .2, .15], false);
      return { T: b.T, ...R.score(M, fine, b.T), from: T0 };
    }).sort((a, b) => b.inl25 - a.inl25);
    const best = results[0];
    const distinct = results.filter(x => Math.hypot(x.T.t[0] - best.T.t[0], x.T.t[2] - best.T.t[2]) >= 0.5 || Math.abs(n180(x.T.yaw - best.T.yaw)) >= 2);
    const second = distinct[0];
    const scaled = R.icp(MI, fine, best.T, [0.7, 0.5, 0.35, 0.25], true);
    const drift = Math.hypot(best.T.t[0] - best.from.t[0], best.T.t[2] - best.from.t[2]);
    const id = path.basename(mapPath).split('-')[0];
    console.log(`  mapa ${id}: ${M.length} pts  <25cm ${pct(best.inl25, M.length)} % (antes ${pct(old.inliers25, old.puntosMapa)} %)  rms ${best.rms.toFixed(3)}  escala libre ${scaled.T.s.toFixed(3)}  ICP movió ${drift.toFixed(2)} m ${n180(best.T.yaw - best.from.yaw).toFixed(2)}°`
      + (second ? `  | otra a ${Math.hypot(second.T.t[0] - best.T.t[0], second.T.t[2] - best.T.t[2]).toFixed(1)} m: ${pct(second.inl25, M.length)} %` : ''));
    fs.writeFileSync(path.join(outDir, `${id}-registro.json`), JSON.stringify({
      nota: 'campus (E, arriba, N) = rot(mapa Unity local, yaw) + t; rot() de register.js. Escala 1. Desde el registro viejo y la nube del edificio (registrar_campus.js).',
      dron: path.basename(campusPly), mapa: path.basename(mapPath), yaw: best.T.yaw, t: best.T.t,
      inliers50: best.inl, inliers25: best.inl25, puntosMapa: M.length, rms: best.rms,
      margenSobreSegunda: second ? best.inl25 / Math.max(1, second.inl25) : null, escalaLibre: scaled.T.s,
      previo: { registro: path.basename(pairs[k]), inliers25: old.inliers25, nubeACampus: { yaw: cloud.T.yaw, t: cloud.T.t } },
      alternativas: distinct.slice(0, 3).map(x => ({ yaw: x.T.yaw, t: x.T.t, inliers25: x.inl25 })),
    }, null, 1));
  }
}
main();
