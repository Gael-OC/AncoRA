import sys,json,os,cv2,numpy as np
from pathlib import Path
sys.path.insert(0,'Tools/LocalizacionOffline')
from caracteristicas import Features,no_network
base=Path('Tools/LocalizacionOffline/salida/20261105-recorridos'); out=Path(sys.argv[1] if len(sys.argv)>1 else 'Tools/LocalizacionOffline/salida/20261105-revision-camaras');out.mkdir(parents=True,exist_ok=True)
if (out/'intermedias.json').exists(): raise FileExistsError(out/'intermedias.json')
frames=json.loads((base/'TEO-disponibles.json').read_text())['frames']; pairs=[('143744',1,5),('143744',5,11),('143744',20,25),('143744',25,30),('142828',11,15),('142828',15,20),('142828',106,111),('142828',111,116)]
rows=[]
with no_network():
 for method in ('xfeat-lighterglue','aliked-lightglue'):
  model=Features(method,1024,2048)
  for session,a,b in pairs:
   f=[next(x for x in frames if session in x['session'] and os.path.basename(x['path']).startswith(f'{n:04}')) for n in (a,b)]
   feat=[model.cached(x,base.parent/'cache') for x in f]; m=model.match(*feat); p,q=feat[0]['keypoints'][m[:,0]],feat[1]['keypoints'][m[:,1]]
   F,mask=cv2.findFundamentalMat(p,q,cv2.FM_RANSAC,3,0.999) if len(m)>=8 else (None,None); keep=mask.ravel().astype(bool) if mask is not None and len(mask)==len(m) else np.zeros(len(m),bool)
   rows.append(dict(method=method,session=session,images=[x['id'] for x in f],matches=len(m),fundamental_ransac_inliers=int(keep.sum()),warning='2D diagnostic only; includes unmasked foreground and is not identity verification'))
   np.savez_compressed(out/f'{method}-{session}-{a:04}-{b:04}.npz',p=p,q=q,inliers=keep)
   ims=[cv2.resize(cv2.imread(x['path']),(768,432)) for x in f];canvas=np.hstack(ims)
   selected=np.flatnonzero(keep);selected=selected[np.linspace(0,len(selected)-1,min(70,len(selected))).astype(int)] if len(selected) else []
   for k in selected:
    pt=tuple(np.round(p[k]*.4).astype(int));qt=tuple(np.round(q[k]*.4+[768,0]).astype(int));cv2.line(canvas,pt,qt,(0,255,0),1);cv2.circle(canvas,pt,3,(0,0,255),-1);cv2.circle(canvas,qt,3,(0,0,255),-1)
   cv2.imwrite(str(out/f'{method}-{session}-{a:04}-{b:04}.jpg'),canvas)
   print(method,session,a,b,len(m),int(keep.sum()),flush=True)
(out/'intermedias.json').write_text(json.dumps(rows,indent=2))
