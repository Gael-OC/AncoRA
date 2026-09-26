// Direct B->A registration of the two Immersal clouds (gravity aligned, 4 DOF), global search + ICP.
const R=require('./register.js');
const [a,b]=process.argv.slice(2);
const A=R.readImmersal(a), B=R.readImmersal(b); const gA=R.ground(A), gB=R.ground(B);
const grid=new R.Grid(A,1);
const occ=new Map(); for(const p of A) if(p[1]-gA>0.8){const k=Math.floor(p[0])+','+Math.floor(p[2]); occ.set(k,1);} const cells=[...occ.keys()].map(k=>k.split(',').map(Number));
const BS=B.filter(p=>p[1]-gB>0.8);
const Rr=120,W=2*Rr+1; const cands=[];
for(let yaw=0;yaw<360;yaw+=2){const votes=new Uint16Array(W*W);for(const m of BS){const r=R.rot(m,yaw);const rx=Math.floor(r[0]),rz=Math.floor(r[2]);for(const [cx,cz] of cells){const tx=cx-rx+Rr,tz=cz-rz+Rr;if(tx>=0&&tx<W&&tz>=0&&tz<W)votes[tx*W+tz]++;}}
 let best=0,bi=0;for(let i=0;i<votes.length;i++)if(votes[i]>best){best=votes[i];bi=i;} cands.push({yaw,v:best,t:[Math.floor(bi/W)-Rr+.5,gA-gB,(bi%W)-Rr+.5]});}
cands.sort((x,y)=>y.v-x.v);
const res=[];
for(const c of cands.slice(0,15)){const r=R.icp(B,grid,{yaw:c.yaw,s:1,t:c.t},[4,3,2,1.5,1,.7,.5,.35],false);res.push({c,...r});}
res.sort((x,y)=>y.inl25-x.inl25);
const seen=[];for(const x of res){if(seen.some(y=>Math.abs(((x.T.yaw-y.T.yaw+540)%360)-180)<2&&Math.hypot(x.T.t[0]-y.T.t[0],x.T.t[2]-y.T.t[2])<1))continue;seen.push(x);
 console.log(`coarse ${x.c.yaw} votes ${x.c.v} -> yaw ${x.T.yaw.toFixed(2)} t [${x.T.t.map(v=>v.toFixed(2))}] inl50 ${x.inl} inl25 ${x.inl25} of ${B.length} rms ${x.rms.toFixed(3)}`);if(seen.length>=6)break;}
