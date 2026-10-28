const fs=require('fs');
const rows=fs.readFileSync(process.argv[2],'utf8').split('\n').filter(Boolean).map(JSON.parse);
const B={152192:'CB',152196:'CB',152257:'CB',152242:'EIC',152795:'EIC',152259:'TEO',152693:'TEO',152796:'TEO',152198:'X1',152200:'X1',152794:'X1'};
const key=r=>r.sesion+'|'+r.imagen;
const imgs={};for(const r of rows){(imgs[key(r)]=imgs[key(r)]||{})[r.variante]=r;}
const L=Object.values(imgs).filter(v=>v.igual&&v.edificio&&v.todos&&v.completa);
console.log('imágenes completas',L.length);
// 1 fidelidad
const same=L.filter(v=>v.igual.mapa===v.igual.telefono_mapa&&v.igual.conf===v.igual.telefono_conf).length;
console.log('1) reproduce lo del teléfono (mapa y conf iguales):',same,'/',L.length);
const dif=L.filter(v=>!(v.igual.mapa===v.igual.telefono_mapa&&v.igual.conf===v.igual.telefono_conf));
dif.slice(0,8).forEach(v=>console.log('  difiere',v.igual.imagen,'tel',v.igual.telefono_mapa,v.igual.telefono_conf,'replay',v.igual.mapa,v.igual.conf));
const code=v=>v.igual.punto.split('-')[0];
const med=a=>{a=a.slice().sort((x,y)=>x-y);return a.length?a[Math.floor((a.length-1)/2)]:NaN};
// 2 por punto: variante edificio (todos los mapas del edificio) y completa
const by={};for(const v of L){const k=v.igual.punto+' '+v.igual.telefono.replace(/Xiaomi |samsung /,'')+' '+v.igual.sesion.slice(9);(by[k]=by[k]||[]).push(v);}
console.log('\n2) por punto (cada imagen contra los mapas de SU edificio; reducida vs completa)');
console.log('punto | tel | N | edificio ok | conf med | completa* ok | conf med');
const tot={red:[0,0],full:[0,0]};
for(const k of Object.keys(by).sort()){const a=by[k];
  // completa variant uses phone's maps; use only images where phone searched own building to compare
  const own=a.filter(v=>v.igual.mapas.every(m=>B[m]===code(v)));
  const e=a.filter(v=>v.edificio.mapa);const cOk=own.filter(v=>v.completa.mapa);const rOk=own.filter(v=>v.igual.mapa);
  console.log(k,'|',a.length,'|',e.length+'/'+a.length,'|',med(e.map(v=>v.edificio.conf)),'| mismos mapas que el teléfono:',rOk.length+'/'+own.length,'reducida →',cOk.length+'/'+own.length,'completa | conf',med(rOk.map(v=>v.igual.conf)),'→',med(cOk.map(v=>v.completa.conf)));
  tot.red[0]+=rOk.length;tot.red[1]+=own.length;tot.full[0]+=cOk.length;}
console.log('TOTAL (mapas propios): reducida',tot.red[0]+'/'+tot.red[1],' completa',tot.full[0]+'/'+tot.red[1]);
// 3 falsos positivos con todos los mapas
console.log('\n3) variante "todos" (11 mapas): ¿el mapa que ubica es de otro edificio?');
let ok=0,wrong=0;const wl=[];
for(const v of L){const t=v.todos;if(!t.mapa)continue;ok++;if(B[t.mapa]!==code(v)){wrong++;wl.push(v.igual.imagen+' '+v.igual.punto+' → '+t.mapa+'('+B[t.mapa]+') conf '+t.conf)}}
console.log('ubicó',ok,'de',L.length,'; en otro edificio:',wrong);wl.slice(0,15).forEach(s=>console.log('  ',s));
// 4 dispersión de posición
console.log('\n4) dispersión de la posición en el mapa (m), mismo mapa, ubicaciones del punto (edificio variante / completa)');
const sd=a=>{const m=a.reduce((s,x)=>s+x,0)/a.length;return Math.sqrt(a.reduce((s,x)=>s+(x-m)**2,0)/a.length)};
for(const k of Object.keys(by).sort()){const a=by[k];
  for(const variant of ['edificio','completa']){
    const src=variant==='edificio'?a.map(v=>v.edificio):a.filter(v=>v.igual.mapas.every(m=>B[m]===code(v))).map(v=>v.completa);
    const g={};src.filter(r=>r.mapa&&r.pos).forEach(r=>(g[r.mapa]=g[r.mapa]||[]).push(r.pos));
    for(const m of Object.keys(g)){const p=g[m];if(p.length<4)continue;
      const mx=med(p.map(q=>q[0])),my=med(p.map(q=>q[1])),mz=med(p.map(q=>q[2]));
      const d=p.map(q=>Math.hypot(q[0]-mx,q[1]-my,q[2]-mz));
      console.log(k,variant.padEnd(8),m,'n',p.length,'desv. típica x/y/z',sd(p.map(q=>q[0])).toFixed(2),sd(p.map(q=>q[1])).toFixed(2),sd(p.map(q=>q[2])).toFixed(2),'| dist. a la mediana: med',med(d).toFixed(2),'máx',Math.max(...d).toFixed(2));}}}
