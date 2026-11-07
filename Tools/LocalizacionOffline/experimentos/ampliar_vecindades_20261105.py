"""Select six construction intermediates and define conservative manual facade regions."""
import json,sys
from pathlib import Path
base=Path('Tools/LocalizacionOffline/salida/20261105-recorridos')
out=Path(sys.argv[1] if len(sys.argv)>1 else 'Tools/LocalizacionOffline/salida/20261105-revision-camaras')
out.mkdir(parents=True,exist_ok=True)
selection=out/'TEO-intermedias-seleccion.json'; annotations=out/'TEO-ampliada-regiones.json'
if selection.exists() or annotations.exists(): raise FileExistsError('Use a new output directory')
d=json.loads((base/'TEO-disponibles.json').read_text()); old=json.loads((base/'TEO-seleccion-densa.json').read_text()); regions=json.loads(Path('Tools/LocalizacionOffline/experimentos/teologia-recorridos-densa-regiones-v2-20261105.json').read_text())
# Coordinates are on the upright inspection sheet; transform back to sensor pixels.
def raw(poly): return [[round(y,5),round(1-x,5)] for x,y in poly]
rect=lambda x0,y0,x1,y1:[(x0,y0),(x1,y0),(x1,y1),(x0,y1)]
items=[
 ('143744',5,[(0,.23),(.99,.11),(.99,.82),(0,.83)],[rect(0,.58,.53,.88),rect(.65,.30,.86,.98),rect(.60,.60,.72,.98),rect(0,.70,1,1)]),
 ('143744',25,[(0,.13),(.48,.03),(.89,.46),(.89,.65),(0,.86)],[rect(0,.66,1,1)]),
 ('142828',15,[(0,.10),(.72,.15),(.72,.69),(0,.70)],[rect(0,.64,1,1)]),
 ('142828',111,[(0,.04),(.66,.29),(.66,.89),(0,.94)],[rect(0,.80,1,1)]),
 ('143053',15,[(0,.12),(.74,.28),(.74,.73),(0,.81)],[rect(.62,.65,1,1)]),
 ('143053',25,[(0,.09),(.68,.27),(.68,.82),(0,.88)],[rect(.4,.65,1,1)])]
chosen=[]
for session,n,poly,exclude in items:
 f=next(f for f in d['frames'] if session in f['session'] and Path(f['id']).name.startswith(f'{n:04}-'))
 chosen.append(f);regions['frames'][f['id']]={'facade':raw(poly),'vegetation':[],'exclude':[raw(p) for p in exclude]}
regions['description']+=' Six additional manually drawn conservative facade regions, reviewed on upright construction frames; no query images. Regions exclude much of foreground but are not semantic truth.'
selection.write_text(json.dumps({**d,'frames':chosen,'selection':'Six specified construction intermediates for camera sensitivity; no queries'},indent=2))
annotations.write_text(json.dumps(regions,indent=2))
print('selected',len(chosen),'base',len(old['frames']))
