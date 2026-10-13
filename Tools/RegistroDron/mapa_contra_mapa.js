// Registers map B directly onto map A (gravity aligned: yaw + translation, scale 1) and carries A's box into B.
//
//   node Tools/RegistroDron/mapa_contra_mapa.js <A-sparse.ply> <B-sparse.ply> <cajaA x,y,z> <giroA>
//
// Both clouds are centred on their median first: two Immersal maps of one building can have origins ~200 m apart,
// beyond the coarse search window of register.js. Points farther than 80 m (in plan) from the median are dropped
// (sky features triangulated kilometres away). Prints the best distinct solutions, where each puts the box in B, and
// the margin over the second one; a box that moves metres between solutions means the registration is ambiguous.
const R = require('./register.js');
const norm = a => ((a % 360) + 540) % 360 - 180;
const median = v => { const s = [...v].sort((x, y) => x - y); return s[Math.floor(s.length / 2)]; };

function load(path) {
  const P = R.readImmersal(path);
  const c = [median(P.map(q => q[0])), 0, median(P.map(q => q[2]))];
  const kept = P.filter(q => Math.hypot(q[0] - c[0], q[2] - c[2]) < 80).map(q => [q[0] - c[0], q[1], q[2] - c[2]]);
  return { c, P: kept, total: P.length };
}

function main() {
  const [aPath, bPath, boxArg, giroArg] = process.argv.slice(2);
  if (!giroArg) { console.error('Uso: node mapa_contra_mapa.js <A-sparse.ply> <B-sparse.ply> <cajaA x,y,z> <giroA>'); process.exit(2); }
  const A = load(aPath), B = load(bPath);
  const grid = new R.Grid(A.P, 1), fine = new R.Grid(A.P, 0.5);
  const step = Math.max(1, Math.floor(B.P.length / 3000)), BI = B.P.filter((_, i) => i % step === 0);
  const { cands } = R.coarse(BI, A.P, R.ground(B.P), R.ground(A.P));
  const results = cands.slice(0, 15).map(c => {
    const coarseFit = R.icp(BI, grid, { yaw: c.yaw, s: 1, t: c.t }, [4, 3, 2, 1.5, 1, .7, .5], false);
    const f = R.icp(BI, fine, coarseFit.T, [.5, .35, .25, .2, .15], false);
    return { T: f.T, ...R.score(B.P, fine, f.T) };
  }).sort((x, y) => y.inl25 - x.inl25);
  const distinct = [];
  for (const x of results)
    if (!distinct.some(y => Math.abs(norm(x.T.yaw - y.T.yaw)) < 2 && Math.hypot(x.T.t[0] - y.T.t[0], x.T.t[2] - y.T.t[2]) < 0.5))
      distinct.push(x);

  // Centred B -> centred A is T, so A = rot(B - cB, yaw) + t + cA, and a point of A goes to B as rot(pA - cA - t, -yaw) + cB.
  const boxA = boxArg.split(',').map(Number), giroA = +giroArg;
  const carry = T => {
    const r = R.rot([boxA[0] - A.c[0] - T.t[0], boxA[1] - T.t[1], boxA[2] - A.c[2] - T.t[2]], -T.yaw);
    return { pos: [r[0] + B.c[0], r[1], r[2] + B.c[2]], giro: norm(giroA - T.yaw) };
  };
  console.log(`A: ${A.P.length}/${A.total} puntos útiles; B: ${B.P.length}/${B.total} (descartados lejos: ${B.total - B.P.length})`);
  const best = carry(distinct[0].T);
  distinct.slice(0, 3).forEach((x, i) => {
    const b = carry(x.T);
    const d = Math.hypot(b.pos[0] - best.pos[0], b.pos[1] - best.pos[1], b.pos[2] - best.pos[2]);
    console.log(`${i === 0 ? 'mejor  ' : 'alterna'} yaw ${x.T.yaw.toFixed(2)}  <25cm ${x.inl25} (${(100 * x.inl25 / B.P.length).toFixed(0)} %) rms ${x.rms.toFixed(3)}` +
      `  -> caja en B [${b.pos.map(v => v.toFixed(2))}] giro ${b.giro.toFixed(2)}  (centro a ${d.toFixed(2)} m de la mejor)`);
  });
  console.log(`margen sobre la segunda: ${(distinct[0].inl25 / Math.max(1, distinct[1] ? distinct[1].inl25 : 1)).toFixed(2)}x`);
}
main();
