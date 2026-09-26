// Refines a map->drone transform keeping the position ALONG the facade fixed; free: yaw about the building centre, depth (v) and height.
const R=require('./register.js');
const fs=require('fs');
const [drone,map,Tj]=process.argv.slice(2); const T0=JSON.parse(Tj);
const th=83.25*Math.PI/180; const uDir=[Math.cos(th),0,Math.sin(th)], vDir=[-Math.sin(th),0,Math.cos(th)];
const C=[-4.963,3.8,0.527];
const D=R.readDrone(drone,false), M=R.readImmersal(map); const gM=R.ground(M);
// smooth field
const RES=.1,SIG=.2,box={x0:-45,x1:45,y0:-4,y1:16,z0:-45,z1:45};
const nx=900,ny=200,nz=900; const F=new Float32Array(nx*ny*nz);
for(const p of D){if(p[0]<box.x0||p[0]>box.x1||p[2]<box.z0||p[2]>box.z1||p[1]<box.y0||p[1]>box.y1)continue;const cx=Math.round((p[0]-box.x0)/RES),cy=Math.round((p[1]-box.y0)/RES),cz=Math.round((p[2]-box.z0)/RES);
 for(let i=-6;i<=6;i++)for(let j=-6;j<=6;j++)for(let k=-6;k<=6;k++){const x=cx+i,y=cy+j,z=cz+k;if(x<0||y<0||z<0||x>=nx||y>=ny||z>=nz)continue;const v=Math.exp(-((i*RES)**2+(j*RES)**2+(k*RES)**2)/(2*SIG*SIG));const id=(x*ny+y)*nz+z;if(v>F[id])F[id]=v;}}
const samp=q=>{const x=Math.round((q[0]-box.x0)/RES),y=Math.round((q[1]-box.y0)/RES),z=Math.round((q[2]-box.z0)/RES);if(x<0||y<0||z<0||x>=nx||y>=ny||z>=nz)return 0;return F[(x*ny+y)*nz+z];};
const pts=M.filter(p=>p[1]-gM>-1&&p[1]-gM<15), w=pts.map(p=>p[1]-gM>0.8?1:0.4);
const tf=(q,[dyaw,dv,dy])=>{const r=R.rot([q[0]-C[0],q[1]-C[1],q[2]-C[2]],dyaw);return [r[0]+C[0]+dv*vDir[0],r[1]+C[1]+dy,r[2]+C[2]+dv*vDir[2]];};
const f=p=>{let s=0;for(let i=0;i<pts.length;i++)s+=w[i]*samp(tf(R.apply(T0,pts[i]),p));return s;};
let best={p:[0,0,0],f:f([0,0,0])};
for(let a=-3;a<=3;a+=0.25)for(let dv=-1.2;dv<=1.2;dv+=0.1)for(let dy=-0.4;dy<=0.4;dy+=0.1){const p=[a,dv,dy];const v=f(p);if(v>best.f)best={p,f:v};}
console.log('start f',f([0,0,0]).toFixed(1),'best',best.f.toFixed(1),'dyaw',best.p[0],'dv',best.p[1].toFixed(2),'dy',best.p[2].toFixed(2));
// resulting transform: d = R(yaw0+dyaw) m + t
const yaw=T0.yaw+best.p[0]; const o=tf(R.apply(T0,[0,0,0]),best.p); console.log(JSON.stringify({yaw:+yaw.toFixed(3),s:1,t:o.map(v=>+v.toFixed(3))}));
// compare: free (own) fit score for reference
const T1=JSON.parse(process.argv[5]||'null'); if(T1){let s=0;for(let i=0;i<pts.length;i++)s+=w[i]*samp(R.apply(T1,pts[i]));console.log('reference transform score',s.toFixed(1));}
