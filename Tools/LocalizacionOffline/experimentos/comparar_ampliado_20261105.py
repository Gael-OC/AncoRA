"""Compare the 71-view models against both baselines and each other."""
import json, pathlib, subprocess, sys
T=pathlib.Path('Tools/LocalizacionOffline');B=T/'salida/20261105-recorridos';R=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else T/'salida/20261105-revision-camaras')
data=R/'ampliado-xfeat/seleccion.json';anchor=json.loads(data.read_text())['frames'][0]['id']
for name,a,b in [
 ('71-xfeat-65',B/'teo-denso-xfeat-fachada-v2/modelos/0',R/'ampliado-xfeat-fachada/modelos/0'),
 ('71-aliked-65',B/'teo-denso-aliked-fachada-v2/modelos/0',R/'ampliado-aliked-fachada/modelos/0'),
 ('71-xfeat-completo-fachada',R/'ampliado-xfeat/modelos/0',R/'ampliado-xfeat-fachada/modelos/0'),
 ('71-aliked-completo-fachada',R/'ampliado-aliked/modelos/0',R/'ampliado-aliked-fachada/modelos/0'),
 ('71-metodos',R/'ampliado-xfeat-fachada/modelos/0',R/'ampliado-aliked-fachada/modelos/0')]:
 out=R/f'consistencia-{name}.json'
 if out.exists():raise FileExistsError(out)
 subprocess.run([str(T/'.venv/bin/python'),str(T/'sfm_diagnostico.py'),'comparar','--source',str(a),'--other',str(b),'--dataset',str(data),'--anchor',anchor,'--output',str(out)],check=True)
 d=json.loads(out.read_text());print(name,d['common_cameras'],d['centre_error_percent_diameter'])
 for c in d['cameras']:
  if ('143744' in c['id'] and any('/'+n+'-' in c['id'] for n in ['0001','0005','0011','0025','0030'])) or ('142828' in c['id'] and any('/'+n+'-' in c['id'] for n in ['0011','0015','0020','0111','0116'])) or ('143053' in c['id'] and '/0020-' in c['id']):
   print(c['id'].split('/')[-2:],round(c['centre_error_percent_diameter'],2),round(c['relative_rotation_delta_degrees'],2))
