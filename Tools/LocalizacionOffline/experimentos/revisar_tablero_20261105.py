"""Visual sheets and conditional diagnostics from already frozen calibration results."""
import argparse
import json
from pathlib import Path
import shutil

import cv2
import numpy as np
from PIL import Image, ImageDraw
from scipy.spatial.transform import Rotation
from ajustar_tablero_20261105 import CHECK, K_of, pose_error


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--run',type=Path,required=True);ap.add_argument('--output',type=Path,required=True)
    ap.add_argument('--previous-run',type=Path)
    args=ap.parse_args();args.output.mkdir(parents=True,exist_ok=False)
    shutil.copy2(__file__,args.output/Path(__file__).name)
    fs=json.loads((args.run/'analisis/detecciones.json').read_text())
    models=json.loads((args.run/'ajuste/modelos.json').read_text())
    motion=json.loads((args.run/'ajuste/mouvement-et-couverture.json').read_text())
    out=[]
    for m in motion:
        if 'skipped' in m:
            continue
        ss=[f for f in fs if f['session']==m['session'] and f['detected']]
        rr=[r for r in models['registered']['per_frame'] if r['session']==m['session']]
        assert len(ss)==len(rr) and all(f['path']==r['path'] for f,r in zip(ss,rr))
        can=Image.new('RGB',(1320,1050),'white');draw=ImageDraw.Draw(can)
        for i,f in enumerate(ss):
            im=Image.open(f['path']).convert('RGB');pts=np.array(f['corners']);lo=pts.min(axis=0);hi=pts.max(axis=0)
            crop=im.crop((max(0,lo[0]-40),max(0,lo[1]-40),min(im.width,hi[0]+40),min(im.height,hi[1]+40))).rotate(-90,expand=True)
            crop.thumbnail((210,180));x=i%6*220;y=i//6*210;can.paste(crop,(x,y+20));draw.text((x+3,y+2),f['metadata']['archivo'],fill='black')
        can.save(args.output/('esquinas-'+m['session'][17:24].strip('-')+'.jpg'),quality=94)
        B=np.array(m['best_signed_basis_diagnostic']['basis']);errors=[]
        for i in range(len(ss)-3):
            j=i+3
            Ri=cv2.Rodrigues(np.array(rr[i]['all_corners']['rvec']))[0];Rj=cv2.Rodrigues(np.array(rr[j]['all_corners']['rvec']))[0]
            Ci=-Ri.T@np.array(rr[i]['all_corners']['tvec_m_provisional']);Cj=-Rj.T@np.array(rr[j]['all_corners']['tvec_m_provisional'])
            Ui=Rotation.from_quat(ss[i]['metadata']['rot_camara']).as_matrix()
            dp=np.array(ss[j]['metadata']['pos_camara'])-np.array(ss[i]['metadata']['pos_camara'])
            errors.append(float(np.linalg.norm(Cj-Ci-Ri.T@B@Ui.T@dp)))
        out.append({'session':m['session'],'condition':'Fixed board unconfirmed; square planar geometry assumed; internal consistency, not physical accuracy.',
                    'closure_median_m_provisional':float(np.median(errors)),'closure_p90_m_provisional':float(np.percentile(errors,90))})
    (args.output/'traslacion-condicional.json').write_text(json.dumps(out,indent=2))
    can=Image.new('RGB',(960,540),'#f5f5f5');draw=ImageDraw.Draw(can)
    for x in range(0,961,120):draw.line((x,0,x,539),fill='#bbbbbb')
    for y in range(0,541,90):draw.line((0,y,959,y),fill='#bbbbbb')
    for f in fs:
        if not f['detected']:
            continue
        for x,y in np.array(f['corners'])/2:draw.ellipse((x-1,y-1,x+1,y+1),fill=('#d8711c' if f['split']=='fit' else '#267ac0'))
    draw.text((12,12),'Sensor 1920x1080: orange fit / blue heldout',fill='black');can.save(args.output/'cobertura.png')
    distortion={}
    for n,m in models.items():
        K=np.array(m['K']);k1,k2,p1,p2,k3=m['distortion']
        xy=np.array([[x,y] for y in np.linspace(0,1079,31) for x in np.linspace(0,1919,51)])
        norm=(xy-K[:2,2])/np.array([K[0,0],K[1,1]])
        r2=(norm**2).sum(axis=1)
        radial_derivative=1+3*k1*r2+5*k2*r2**2+7*k3*r2**3
        xyz=np.column_stack([norm,np.ones(len(norm))])
        pred=cv2.projectPoints(xyz,np.zeros(3),np.zeros(3),K,np.array(m['distortion']))[0].reshape(-1,2)
        distortion[n]={'min_radial_derivative_on_sensor_grid':float(radial_derivative.min()),
                       'max_forward_distortion_px':float(np.linalg.norm(pred-xy,axis=1).max()),
                       'warning':'Extrapolation outside detected corner coverage; not a measured correction.'}
    assert distortion['registered']['max_forward_distortion_px'] < 1e-8
    (args.output/'distorsion-extrapolada.json').write_text(json.dumps(distortion,indent=2))
    if args.previous_run:
        old=json.loads((args.previous_run/'analisis/detecciones.json').read_text())
        assert not {f['sha256'] for f in old}&{f['sha256'] for f in fs}
        transfer={'status':'Retrospective diagnostic; earlier images already inspected, never a blind final test.', 'models':{}}
        for name,model in models.items():
            results=[]
            for f in old:
                if not f['detected']: continue
                K=K_of(f) if name=='registered' else np.array(model['K'])
                result=pose_error(f,K,np.array(model['distortion']),CHECK)
                results.append({'path':f['path'],'session':f['session'],'rms_px':result['rms_px']})
            transfer['models'][name]={'per_frame':results,'sessions':{}}
            for s in sorted({r['session'] for r in results}):
                e=[r['rms_px'] for r in results if r['session']==s]
                transfer['models'][name]['sessions'][s]={'median_rms_px':float(np.median(e)),'p90_rms_px':float(np.percentile(e,90))}
        (args.output/'transferencia-anterior.json').write_text(json.dumps(transfer,indent=2))
    print(json.dumps(distortion,indent=2))

if __name__=='__main__':main()
