import json,subprocess,pathlib,sys
base=pathlib.Path('Tools/LocalizacionOffline/salida/20261105-recorridos'); out=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else 'Tools/LocalizacionOffline/salida/20261105-revision-camaras'); data=base/'TEO-seleccion-densa.json'; anchor=json.loads(data.read_text())['frames'][0]['id']
for name,a,b in [('xfeat',base/'teo-denso-xfeat-fachada-v2/modelos/0',out/'xfeat-temporal/modelos/0'),('aliked',base/'teo-denso-aliked-fachada-v2/modelos/0',out/'aliked-temporal/modelos/0'),('metodos',out/'xfeat-temporal/modelos/0',out/'aliked-temporal/modelos/0')]:
 result=out/f'consistencia-{name}.json'
 if result.exists(): raise FileExistsError(result)
 cmd=['Tools/LocalizacionOffline/.venv/bin/python','Tools/LocalizacionOffline/sfm_diagnostico.py','comparar','--source',str(a),'--other',str(b),'--dataset',str(data),'--anchor',anchor,'--output',str(result)];subprocess.run(cmd,check=True)
 d=json.loads((out/f'consistencia-{name}.json').read_text());print(name,d['common_cameras'],d['centre_error_percent_diameter']);
 for x in d['cameras']:
  if ('143744' in x['id'] and ('/0001-' in x['id'] or '/0011-' in x['id'])) or ('142828' in x['id'] and ('/0011-' in x['id'] or '/0020-' in x['id'] or '/0116-' in x['id'])):print(x['id'].split('/')[-2:],round(x['centre_error_percent_diameter'],2),round(x['relative_rotation_delta_degrees'],2))
