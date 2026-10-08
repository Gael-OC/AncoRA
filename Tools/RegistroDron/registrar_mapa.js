// Registers one Immersal map cloud onto the georeferenced drone cloud of its building (gravity aligned: yaw +
// translation, scale 1) and writes the transform map -> drone to a JSON.
//
//   node Tools/RegistroDron/registrar_mapa.js <dron.ply> <mapa-sparse.ply> <salida.json>
//
// Frames: the drone PLY is East-North-Up (georef_dron.js); register.js reads it as Unity-like (x, y, z) = (E, Up, N).
// The map PLY is in native Immersal axes; register.js reads it as Unity map-local (-x, y, z). So the result maps
// map-local Unity coordinates to drone (E, Up, N): drone = R(yaw) * map + t, with rot() as in register.js.
//
// It also refits with free scale from the best solution, as a diagnostic only: the map is metric through ARCore and
// the drone cloud through its GPS, so a scale near 1 backs the GPS scale.
const fs = require('fs');
const R = require('./register.js');

function main() {
  const [dronePath, mapPath, outPath] = process.argv.slice(2);
  if (!outPath) { console.error('Uso: node registrar_mapa.js <dron.ply> <mapa-sparse.ply> <salida.json>'); process.exit(2); }
  const D = R.readDrone(dronePath, false), M = R.readImmersal(mapPath);
  const gD = R.ground(D), gM = R.ground(M), grid = new R.Grid(D, 1);
  // Dense maps (up to ~23k points) make the wide-radius ICP slow: search with a subsample, score with every point.
  const step = Math.max(1, Math.floor(M.length / 4000)); const MI = M.filter((_, i) => i % step === 0);
  const { cands } = R.coarse(MI, D, gM, gD);
  const fineGrid = new R.Grid(D, 0.5);
  const results = cands.slice(0, 12).map(c => {
    const coarseFit = R.icp(MI, grid, { yaw: c.yaw, s: 1, t: c.t }, [4, 3, 2, 1.5, 1, .7, .5], false);
    const fine = R.icp(MI, fineGrid, coarseFit.T, [.5, .35, .25, .2, .15], false);
    return { T: fine.T, ...R.score(M, fineGrid, fine.T) };
  }).sort((a, b) => b.inl25 - a.inl25);
  const r = { D, M, gD, gM, grid: fineGrid };
  // Keep distinct solutions only: after the fine pass, the same basin lands within ~0.5 m and 2 degrees. A second
  // distinct solution that scores almost as well means the map slides along a plain facade.
  const distinct = [];
  for (const x of results) {
    const dup = distinct.some(y => Math.abs(((x.T.yaw - y.T.yaw + 540) % 360) - 180) < 2 && Math.hypot(x.T.t[0] - y.T.t[0], x.T.t[2] - y.T.t[2]) < 0.5);
    if (!dup) distinct.push(x);
  }
  const best = distinct[0], second = distinct[1];
  const scaled = R.icp(MI, r.grid, best.T, [0.7, 0.5, 0.35, 0.25], true);
  const name = mapPath.split(/[\\/]/).pop();
  console.log(`${name}: ${r.M.length} puntos del mapa, ${r.D.length} del dron (vóxel 0,15 m); suelo mapa ${r.gM}, dron ${r.gD}`);
  distinct.slice(0, 3).forEach((x, i) => console.log(`  ${i === 0 ? 'mejor ' : 'alterna'} yaw ${x.T.yaw.toFixed(2)} t [${x.T.t.map(v => v.toFixed(2))}]  <50cm ${x.inl} <25cm ${x.inl25} (${(100 * x.inl25 / r.M.length).toFixed(0)} %) rms ${x.rms.toFixed(3)}`));
  console.log(`  escala libre (diagnóstico): ${scaled.T.s.toFixed(4)} con <25cm ${scaled.inl25} de ${MI.length}`);
  const margin = second ? best.inl25 / Math.max(1, second.inl25) : Infinity;
  if (margin < 1.3) console.log(`ATENCIÓN: la segunda solución es casi tan buena (${margin.toFixed(2)}x): el registro es ambiguo.`);
  fs.writeFileSync(outPath, JSON.stringify({
    nota: 'dron (E, arriba, N) = rot(mapa Unity local, yaw) + t; rot() de register.js. Escala 1.',
    dron: dronePath.split(/[\\/]/).pop(), mapa: name,
    yaw: best.T.yaw, t: best.T.t,
    inliers50: best.inl, inliers25: best.inl25, puntosMapa: r.M.length, rms: best.rms,
    margenSobreSegunda: margin, escalaLibre: scaled.T.s,
    alternativas: distinct.slice(1, 3).map(x => ({ yaw: x.T.yaw, t: x.T.t, inliers25: x.inl25 })),
  }, null, 1));
}
main();
