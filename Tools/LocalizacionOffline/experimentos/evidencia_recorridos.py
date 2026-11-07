"""Render estimated track observations and cross-session support without metric claims."""
import argparse
from collections import Counter
from itertools import combinations
from pathlib import Path
import sys
import numpy as np
import pycolmap as pc
from PIL import Image,ImageDraw
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from datos import read_json,write_json
from sfm_diagnostico import max_angle,stats


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--source',required=True);a=p.parse_args();out=Path(a.source)
    frames={f['id']:f for f in read_json(out/'seleccion.json')['frames']};summaries=[]
    for path in sorted((out/'modelos').iterdir()):
        if not (path/'images.bin').exists():continue
        rec=pc.Reconstruction(path);cross=Counter();tracks=[]
        for pid,point in rec.points3D.items():
            elements=point.track.elements;sessions=sorted({frames[rec.images[e.image_id].name]['session'] for e in elements})
            for pair in combinations(sessions,2):cross[pair]+=1
            tracks.append(dict(id=pid,length=len(elements),sessions=len(sessions),angle=max_angle(point.xyz,[rec.images[e.image_id].projection_center() for e in elements])))
        eligible=sorted([t for t in tracks if t['sessions']>=3 and t['length']>=4],key=lambda t:t['angle'])
        selected=[]
        if eligible:
            selected=[eligible[0],eligible[len(eligible)//2],*eligible[-3:]]
        canvas=Image.new('RGB',(4*320,len(selected)*625 or 30),'#20252b');draw=ImageDraw.Draw(canvas);details=[]
        for row,t in enumerate(selected):
            point=rec.points3D[t['id']];elements=sorted(point.track.elements,key=lambda e:rec.images[e.image_id].name)
            # First observation per session, then additional views up to four.
            chosen=[];seen=set()
            for e in elements:
                session=frames[rec.images[e.image_id].name]['session']
                if session not in seen:chosen.append(e);seen.add(session)
            chosen=(chosen+[e for e in elements if e not in chosen])[:4]
            observations=[]
            for col,e in enumerate(chosen):
                im=rec.images[e.image_id];f=frames[im.name];xy=im.points2D[e.point2D_idx].xy-.5
                raw=Image.open(f['path']).convert('RGB');d=ImageDraw.Draw(raw);x,y=map(float,xy)
                d.ellipse((x-16,y-16,x+16,y+16),outline='cyan',width=4);d.line((x-26,y,x+26,y),fill='cyan',width=3);d.line((x,y-26,x,y+26),fill='cyan',width=3)
                raw=raw.rotate(-90,expand=True);raw.thumbnail((310,552));cx=col*320;cy=row*625;canvas.paste(raw,(cx,cy+65))
                draw.text((cx+3,cy+3),f'Track {t["id"]}, angle max {t["angle"]:.1f} deg',fill='cyan')
                draw.text((cx+3,cy+19),f['session'][13:28],fill='white');draw.text((cx+3,cy+35),Path(f['id']).name,fill='white')
                observations.append(dict(id=f['id'],xy=xy.tolist()))
            details.append({**t,'observations':observations})
        canvas.save(path/'tracks-muestras.jpg',quality=90)
        write_json(path/'tracks-muestras.json',details)
        summaries.append(dict(model=path.name,images=rec.num_reg_images(),points=rec.num_points3D(),
            estimated_angle=stats([t['angle'] for t in tracks]),length=stats([t['length'] for t in tracks]),
            cross_session_tracks=[dict(a=a,b=b,tracks=n) for (a,b),n in sorted(cross.items())],
            images_detail=[dict(id=im.name,session=frames[im.name]['session'],observations=sum(p.has_point3D() for p in im.points2D)) for im in rec.images.values()]))
    write_json(out/'soporte-sesiones.json',summaries)


if __name__=='__main__':main()
