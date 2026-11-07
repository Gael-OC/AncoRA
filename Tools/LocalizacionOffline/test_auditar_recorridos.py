"""Integrity and separation checks for the new export audit."""
import json
from pathlib import Path
import tempfile
import unittest
import zipfile
from PIL import Image
from auditar_recorridos import preserve, audit, sha
from datos import inventory, read_json


class RecordingAuditTests(unittest.TestCase):
    def test_numbered_log_copies_recover_images_without_hiding_roles_or_conflicts(self):
        with tempfile.TemporaryDirectory() as temp:
            out=Path(temp);folder=out/'originales/session';folder.mkdir(parents=True)
            rows=[]
            for n,kind in [(1,'construccion-arcore'),(2,'barrido-arcore')]:
                Image.new('L',(32,24),40+n).save(folder/f'{n}.png')
                rows.append(dict(archivo=f'{n}.png',sesion='session',tipo_captura=kind,telefono='phone',
                    edificio_punto='TEO',punto='TEO-A',luz='sol',ancho=32,alto=24,
                    intrinsecos=dict(fx=25,fy=25,cx=16,cy=12),captura_t=float(n),timestamp_sensor=float(n),
                    timestamp_pose_sensor=float(n),tracking='SessionTracking',epoca_tracking='session',hora=str(n)))
            (folder/'intentos.jsonl').write_text(json.dumps(rows[0]))
            alternate=folder/'intentos (1).jsonl';alternate.write_text('\n'.join(json.dumps(row) for row in rows))
            data=inventory(out/'originales')
            self.assertEqual(len(data['frames']),2)
            self.assertEqual(data['unreferenced_images'],[])
            audit(out)
            summary=read_json(out/'auditoria.json')['sessions']
            self.assertEqual(len(summary),1)
            self.assertEqual(summary[0]['role'],'ambiguous')
            self.assertEqual(summary[0]['overlapping_export_rows'],1)
            self.assertEqual(summary[0]['rows'],2)
            self.assertEqual(len(summary[0]['source_logs']),2)
            self.assertEqual(read_json(out/'TEO-disponibles.json')['frames'],[])
            # A numbered copy may add metadata, never silently override calibration.
            conflict=dict(rows[0],intrinsecos=dict(fx=26,fy=25,cx=16,cy=12))
            alternate.write_text(json.dumps(conflict))
            with self.assertRaisesRegex(ValueError,'Metadatos distintos'):
                inventory(out/'originales')

    def test_copy_and_zip_provenance_and_changed_copy_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);source=root/'source';source.mkdir();out=root/'out'
            (source/'original.txt').write_text('original')
            with zipfile.ZipFile(source/'export.zip','w') as z:
                z.writestr('../original.txt','original')
                z.writestr('only-in-zip.txt','unextracted')
            preserve([source],out)
            records=read_json(out/'procedencia.json')
            self.assertTrue(all(sha(r['source'])==sha(r['copy']) for r in records))
            members=read_json(out/'zip-miembros.json')
            self.assertTrue(members[0]['extracted_sources'])
            self.assertFalse(members[1]['extracted_sources'])
            self.assertFalse((root/'original.txt').exists())
            (out/'originales/0/original.txt').write_text('changed')
            with self.assertRaisesRegex(ValueError,'Copia distinta'):
                preserve([source],out)
            self.assertEqual((source/'original.txt').read_text(),'original')

    def test_roles_follow_metadata_and_session_hash_exclusion(self):
        with tempfile.TemporaryDirectory() as temp:
            out=Path(temp);original=out/'originales'
            for session,kind,color in [('build','construccion-arcore',40),('walk','recorrido-arcore',60),('query','barrido-arcore',40),('mixed','construccion-arcore',80)]:
                folder=original/session;folder.mkdir(parents=True)
                rows=[]
                for n,t in [(2,2.),(1,1.)]:
                    path=folder/f'{n}.png';Image.new('L',(32,24),color+n).save(path)
                    rows.append(dict(archivo=path.name,sesion=session,tipo_captura=kind if session!='mixed' or n==1 else 'barrido-arcore',
                        telefono='same phone',edificio_punto='TEO',punto='TEO-C',luz='sol',ancho=32,alto=24,
                        intrinsecos=dict(fx=25,fy=25,cx=16,cy=12),captura_t=t,timestamp_sensor=t,
                        timestamp_pose_sensor=t,tracking='SessionTracking',epoca_tracking=session,hora=str(t)))
                (folder/'intentos.jsonl').write_text('\n'.join(json.dumps(r) for r in rows))
            Image.new('L',(32,24),100).save(original/'mixed/orphan.png')
            audit(out)
            result=read_json(out/'auditoria.json')
            self.assertEqual({s['session']:s['role'] for s in result['sessions']},dict(build='construction',walk='construction',query='query',mixed='ambiguous'))
            self.assertTrue(all(not s['jsonl_order_monotonic'] and s['monotonic'] for s in result['sessions']))
            self.assertEqual(len(result['hash_role_collisions']),2)
            self.assertEqual(len(result['unreferenced']),1)
            available=read_json(out/'TEO-disponibles.json')['frames']
            self.assertEqual(len(available),2)
            self.assertTrue(all(frame['session']=='walk' for frame in available))
            self.assertEqual(len(read_json(out/'consultas.json')['frames']),2)


if __name__=='__main__':unittest.main()
