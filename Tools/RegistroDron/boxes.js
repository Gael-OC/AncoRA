const R=require('./register.js');
const th=83.25*Math.PI/180,c=Math.cos(th),s=Math.sin(th);
// Box in the drone cloud (RealityScan metres): walls u=-10.05 / +9.93, front face v=0.17, back ~9.8 (not photographed), base z=0.2, top z=7.6.
const u0=(-10.05+9.93)/2, W=20.0, v0=0.17+9.6/2, Dp=9.6, zb=0.2, zt=7.6, Hh=zt-zb;
const C=[u0*c-v0*s, (zb+zt)/2, u0*s+v0*c]; // Unity (x,y,z) with Unity z = RealityScan y
const front=[s,0,-c]; const psi=Math.atan2(front[0],front[2])*180/Math.PI;
const norm=a=>((a+540)%360)-180;
const maps={A:{yaw:-174.12,t:[8.66,2.8,11.199],file:'MapaA/151714-Teologia-sparse.ply'},A_libre:{yaw:-172.72,t:[9.17,2.77,12.80],file:'MapaA/151714-Teologia-sparse.ply'},B:{yaw:-79.57,t:[3.88,2.91,-15.58],file:'MapaB/151716-Teologia2-sparse.ply'}};
console.log('drone box centre',C.map(v=>v.toFixed(3)),'yaw',psi.toFixed(2),'size',W,Hh.toFixed(2),Dp);
for(const [k,T] of Object.entries(maps)){
  const d=[C[0]-T.t[0],C[1]-T.t[1],C[2]-T.t[2]]; const m=R.rot(d,-T.yaw); const yaw=norm(psi-T.yaw);
  // check: fraction of map points (above ground) within 0.4 m of the box surface (front + two short faces)
  const M=R.readImmersal('C:/Users/nicol/Documents/AncoRA/Assets/AncoRA/ImmersalTeologia/'+T.file); const gM=R.ground(M);
  let front_=0,ends=0,inside=0;
  for(const p of M){ if(p[1]-gM<0.8) continue; const q=R.rot([p[0]-m[0],p[1]-m[1],p[2]-m[2]],-yaw); // box-local
    const ax=Math.abs(q[0]), az=q[2], ay=Math.abs(q[1]);
    if(ay<Hh/2+0.3){ if(Math.abs(az-Dp/2)<0.4&&ax<W/2) front_++; if(Math.abs(ax-W/2)<0.4&&az<Dp/2&&az>-Dp/2) ends++; if(ax<W/2-0.5&&Math.abs(az)<Dp/2-0.5) inside++; } }
  console.log(`${k}: position [${m.map(v=>v.toFixed(2))}] yaw ${yaw.toFixed(2)} | map pts on front ${front_}, on ends ${ends}, inside box ${inside}`);
}
