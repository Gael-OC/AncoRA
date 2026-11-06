import json,collections, pathlib,pycolmap as pc,numpy as np
base=pathlib.Path('Tools/LocalizacionOffline/salida/20261105-recorridos'); out=pathlib.Path('Tools/LocalizacionOffline/salida/20261105-revision-camaras')
for label,p in [('xfeat',base/'teo-denso-xfeat-fachada-v2/modelos/0'),('aliked',base/'teo-denso-aliked-fachada-v2/modelos/0'),('xfeat-temporal',out/'xfeat-temporal/modelos/0'),('aliked-temporal',out/'aliked-temporal/modelos/0')]:
 r=pc.Reconstruction(p); imgs=r.images; print('\n',label)
 for im in imgs.values():
  if not any(s in im.name for s in ['143744','142828']):continue
  fn=im.name.split('/')[-1];num=int(fn[:4]); sess=im.name.split('/')[-2]
  if not (('143744' in sess and num in (1,11,30)) or ('142828' in sess and num in (11,20,116))):continue
  pts=[p for p in im.points2D if p.has_point3D()]; xy=np.array([p.xy for p in pts]); sessions=collections.Counter()
  for pt in pts:
   track=r.points3D[pt.point3D_id].track
   for s in set(imgs[e.image_id].name.split('/')[-2] for e in track.elements):sessions[s]+=1
  cells=len({(int(x*4/1920),int(y*4/1080)) for x,y in xy})
  print(sess[21:27],fn[:4],len(pts),'cells4x4',cells,'other sessions',sorted([v for k,v in sessions.items() if k!=sess],reverse=True))
