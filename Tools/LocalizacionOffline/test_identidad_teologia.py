"""Identity constraints preserve raw indices and expose unknown-mediated conflicts."""
import hashlib
import json
from pathlib import Path
import shutil
import tempfile
import unittest

import numpy as np
from PIL import Image

from experimentos.identidad_teologia_20261106 import (
    conflicting_identities, filter_pairs, identity_labels, incompatible, upright_coordinates)
from experimentos.cuarentena_comun_20261106 import common_quarantine, compatible_inputs
from experimentos.grafo_identidad_20261106 import components


class IdentityTests(unittest.TestCase):
    def test_native_database_reverses_columns_for_reversed_image_ids(self):
        import pycolmap as pc
        with tempfile.TemporaryDirectory() as tmp:
            with pc.Database.open(Path(tmp)/'database.db') as db:
                cam=pc.Camera(model='PINHOLE',width=100,height=50,params=[50,50,50.5,25.5])
                cam.camera_id=db.write_camera(cam)
                ids=[db.write_image(pc.Image(name=f'{i}.png',camera_id=cam.camera_id)) for i in range(2)]
                asymmetric=np.array([[0,2],[3,1]],dtype=np.uint32)
                geometry=pc.TwoViewGeometry();geometry.inlier_matches=asymmetric
                db.write_two_view_geometry(ids[1],ids[0],geometry)
                np.testing.assert_array_equal(db.read_two_view_geometry(ids[1],ids[0]).inlier_matches,asymmetric)
                np.testing.assert_array_equal(db.read_two_view_geometry(ids[0],ids[1]).inlier_matches,asymmetric[:,::-1])

    def test_components_quarantine_unknown_bridge_without_greedy_edge_order(self):
        sizes=[3,3,3]
        tags=[np.array(['P/U1','D/U2','']),np.array(['','D/U2','']),np.array(['P/U2','D/U2',''])]
        pairs={(0,1):np.array([[0,0],[1,1]],dtype=np.int32),
               (1,2):np.array([[0,0],[1,1]],dtype=np.int32),
               (0,2):np.empty((0,2),dtype=np.int32)}
        kept,roots,offsets,report=components(sizes,pairs,tags)
        self.assertEqual(report['rejected_features'],3)
        self.assertEqual(len(report['conflicting_components']),1)
        np.testing.assert_array_equal(kept[(0,1)],[[1,1]])
        np.testing.assert_array_equal(kept[(1,2)],[[1,1]])
        # An isolated unlabelled feature remains present, and reversing edge order
        # cannot pick which of the incompatible physical windows wins.
        self.assertNotIn(int(roots[2]),[c['root'] for c in report['conflicting_components']])
        reverse={key:value[::-1] for key,value in reversed(list(pairs.items()))}
        kept2,_,_,r2=components(sizes,reverse,tags)
        self.assertEqual(r2['rejected_features'],3)
        for key in pairs:np.testing.assert_array_equal(kept[key],kept2[key])
        with self.assertRaisesRegex(ValueError,'canónico'):
            components(sizes,{(1,0):np.array([[0,0]])},tags)

    def test_rotation_hierarchy_filter_and_transitive_quarantine(self):
        np.testing.assert_array_equal(upright_coordinates([[0,0],[99,0],[0,49],[99,49]],100,50),
                                      [[1,0],[1,1],[0,0],[0,1]])
        annotation=dict(face_bands=[['P',-.001,.4],['D',.6,1.001]],
                        elements=[['P/U1',[.1,.1,.3,.3]],['D/U1',[.7,.1,.9,.3]]])
        facade=dict(facade=[[0,0],[1,0],[1,1],[0,1]],vegetation=[],exclude=[])
        # Upright x=.2/.8/.5 and y=.2, all in raw coordinate convention.
        xy=np.array([[19.8,39.2],[19.8,9.8],[19.8,24.5]])
        frame=dict(width=100,height=50)
        faces,elements=identity_labels(xy,frame,annotation,facade)
        self.assertEqual(faces.tolist(),['P','D',''])
        self.assertEqual(elements.tolist(),['P/U1','D/U1',''])
        np.testing.assert_array_equal(incompatible((faces,elements),(faces[::-1],elements[::-1])),[False,False,False])
        self.assertTrue(incompatible((faces[:1],elements[:1]),(faces[1:2],elements[1:2]))[0])
        # A--unknown--B is still a conflicting track even after direct A--B rejection.
        self.assertTrue(conflicting_identities([dict(face='P',element='P/U1'),dict(face='',element=''),dict(face='D',element='')]))
        self.assertTrue(conflicting_identities([dict(face='P',element='P/U1'),dict(face='P',element='P/U2')]))
        self.assertFalse(conflicting_identities([dict(face='P',element='P/U1'),dict(face='',element=''),dict(face='P',element='')]))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);source=root/'source';source.mkdir();frames=[]
            for i in range(3):
                image=source/f'{i}.png';Image.new('L',(100,50),color=i).save(image)
                frames.append(dict(frame,id=image.name,path=str(image),session=f'build-{i}',phone='Xiaomi 25069PTEBG',
                    K=[[50,0,50],[0,50,25],[0,0,1]],sha256=hashlib.sha256(image.read_bytes()).hexdigest()))
                np.savez(source/f'keypoints-{i}.npz',keypoints=xy)
            np.savez(source/'matches-0-1.npz',matches=np.array([[0,0],[0,1],[0,2],[2,1]],dtype=np.int32))
            np.savez(source/'matches-0-2.npz',matches=np.array([[0,1]],dtype=np.int32))
            np.savez(source/'matches-1-2.npz',matches=np.array([[2,1]],dtype=np.int32))
            selection=dict(role='construction',frames=frames,excluded_sessions=['query'],excluded_hashes=['queryhash'])
            def write(path,data):path.write_text(json.dumps(data))
            write(source/'seleccion.json',selection);write(source/'condiciones.json',dict(method='test'))
            ann=root/'ann.json';mask=root/'facade.json'
            write(ann,dict(coordinate_system='upright_clockwise_normalized_pixel_centres',physical_faces=['P','D'],
                physical_elements=['P/U1','D/U1'],frames={f['id']:dict(annotation,sha256=f['sha256']) for f in frames}))
            write(mask,dict(frames={f['id']:facade for f in frames}))
            out=root/'filtered';filter_pairs(source,out,ann,mask)
            np.testing.assert_array_equal(np.load(out/'matches-0-1.npz')['matches'],[[0,0],[0,2],[2,1]])
            self.assertEqual((source/'keypoints-0.npz').read_bytes(),(out/'keypoints-0.npz').read_bytes())
            self.assertEqual(json.loads((out/'seleccion.json').read_text()),selection)
            digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
            q=root/'quarantine.json';write(q,dict(selection_sha256=digest(source/'seleccion.json'),
                keypoint_sha256={f['id']:digest(source/f'keypoints-{i}.npz') for i,f in enumerate(frames)},
                annotations_sha256=digest(ann),facade_sha256=digest(mask),
                conflicting_tracks=[dict(observations=[dict(id='0.png',keypoint=0),
                    dict(id='1.png',keypoint=2),dict(id='2.png',keypoint=1)])]))
            out2=root/'quarantined';filter_pairs(source,out2,ann,mask,q)
            np.testing.assert_array_equal(np.load(out2/'matches-0-1.npz')['matches'],[[2,1]])
            self.assertEqual(len(np.load(out2/'matches-1-2.npz')['matches']),0)
            with self.assertRaises(FileExistsError):filter_pairs(source,out,ann,mask)
            # A common quarantine may compare different K only for the declared 15T,
            # with the same raw indices and every input pair numerically identical.
            write(source/'condiciones.json',dict(method='test',optical_condition='baseline'))
            other=root/'radial';shutil.copytree(source,other)
            radial=json.loads(json.dumps(selection))
            for f in radial['frames']:
                f['recorded_K']=f['K'];f['K']=[[51,0,50],[0,51,25],[0,0,1]]
                f['distortion']=[.06,0,0,0,0]
                f['calibration_candidate']=dict(phone='Xiaomi 25069PTEBG',model='radial_k1')
            write(other/'seleccion.json',radial)
            write(other/'condiciones.json',dict(method='test',optical_condition='radial_k1'))
            groups=[[dict(id='0.png',keypoint=0),dict(id='1.png',keypoint=2),dict(id='2.png',keypoint=1)],
                    [dict(id='0.png',keypoint=0),dict(id='1.png',keypoint=1)]]
            for src,group in zip([source,other],groups):
                model=src/'modelos/0';model.mkdir(parents=True);(model/'images.bin').write_bytes(b'identity report source')
                write(model/'conflictos-identidad.json',dict(selection_sha256=digest(src/'seleccion.json'),
                    keypoint_sha256={f['id']:digest(src/f'keypoints-{i}.npz') for i,f in enumerate(frames)},
                    model_images_sha256=digest(model/'images.bin'),annotations_sha256=digest(ann),facade_sha256=digest(mask),
                    conflicting_tracks=[dict(observations=group)]))
            common_quarantine(source,other,root/'common-base',root/'common-radial',ann,mask,root/'union')
            union=json.loads((root/'union/resumen.json').read_text())
            self.assertEqual((union['union'],union['common'],union['baseline_only'],union['radial_only']),(4,1,2,1))
            compatible_inputs(root/'common-base',root/'common-radial')
            np.savez(other/'matches-0-1.npz',matches=np.array([[0,0]],dtype=np.int32))
            with self.assertRaisesRegex(ValueError,'Correspondencias fuente'):
                compatible_inputs(source,other)
            shutil.copy2(source/'matches-0-1.npz',other/'matches-0-1.npz')
            radial['frames'][0]['phone']='other-phone';write(other/'seleccion.json',radial)
            with self.assertRaisesRegex(ValueError,'metadatos distintos'):
                compatible_inputs(source,other)
            np.savez(source/'keypoints-1.npz',keypoints=xy+1)
            with self.assertRaisesRegex(ValueError,'características'):
                filter_pairs(source,root/'wrong-method',ann,mask,q)


if __name__=='__main__':unittest.main()
