import json, pathlib, itertools, os, numpy as np, sys
base=pathlib.Path('Tools/LocalizacionOffline/salida/20261105-recorridos')
outbase=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else 'Tools/LocalizacionOffline/salida/20261105-revision-camaras')
outbase.mkdir(parents=True,exist_ok=True)
for method in ('xfeat','aliked'):
 src=base/f'teo-denso-{method}-fachada-v2'; out=outbase/f'{method}-temporal'; out.mkdir(exist_ok=False)
 data=json.loads((src/'seleccion.json').read_text()); fs=data['frames']; cond=json.loads((src/'condiciones.json').read_text()); cond['pair_selection']='Within session only adjacent selected frames; all cross-session pairs. Same v2 facade masks.'; cond['source_experiment']=str(src.resolve())
 (out/'seleccion.json').write_text(json.dumps(data,indent=2));(out/'condiciones.json').write_text(json.dumps(cond,indent=2))
 for i in range(len(fs)): os.link(src/f'keypoints-{i}.npz',out/f'keypoints-{i}.npz')
 n=0
 for i,j in itertools.combinations(range(len(fs)),2):
  a,b=fs[i],fs[j]; same=a['session']==b['session']; allowed=not same or j==i+1
  if allowed: os.link(src/f'matches-{i}-{j}.npz',out/f'matches-{i}-{j}.npz'); n+=1
  else: np.savez_compressed(out/f'matches-{i}-{j}.npz',matches=np.empty((0,2),dtype=np.int32))
 print(method,'retained',n,flush=True)
