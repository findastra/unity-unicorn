"""Synthetic geometry only. These tests never write real usage history."""
import importlib.util
import json
from pathlib import Path
import struct
import tempfile
import unittest
from unittest.mock import patch

path = Path(__file__).resolve().parents[1]/'scripts/no-windows-audit-tool-20261011.py'
spec = importlib.util.spec_from_file_location('no_windows', path)
audit = importlib.util.module_from_spec(spec); spec.loader.exec_module(audit)


def mesh(material='Glass', z=0):
    return dict(positions=[-2,-2,z,2,-2,z,0,2,z], submeshes=[(material,[0,1,2])])


def write_mesh(path, material='Wall', z=0):
    data=bytearray(b'PMS1')+struct.pack('<iii',3,1,0)
    data+=struct.pack('<9f', *mesh(material,z)['positions'])+struct.pack('<9f', *([0,0,1]*3))+bytes([255]*12)
    data+=struct.pack('<i',len(material))+material.encode()+struct.pack('<i3i',3,0,1,2)
    path.write_bytes(data)


class OpenArchTests(unittest.TestCase):
    def test_glass_and_iron_are_tested_not_only_masonry(self):
        for material in ('Glass','Glass2','Iron','Marble','Wall'):
            hit=audit.TriangleTree.from_mesh(mesh(material)).hit((0,0,-1),(0,0,1))
            self.assertEqual(hit['material'],material)

    def test_ray_beside_pane_is_clear(self):
        self.assertIsNone(audit.TriangleTree.from_mesh(mesh()).hit((4,0,-1),(4,0,1)))

    def test_empty_material_slots_do_not_create_phantom_windows(self):
        m=mesh('Wall');m['submeshes'].insert(0,('Glass',[]))
        self.assertEqual(audit.TriangleTree.from_mesh(m).hit((0,0,-1),(0,0,1))['material'],'Wall')

    def test_tree_subdivision_keeps_the_same_hit(self):
        triangles=[]
        for i in range(60):
            points=((i*10-2,-2,0),(i*10+2,-2,0),(i*10,2,0))
            triangles.append(('Iron',points,audit.bounds(points)))
        tree=audit.TriangleTree(triangles)
        self.assertIsNotNone(tree.children)
        self.assertEqual(tree.hit((300,0,-1),(300,0,1))['material'],'Iron')
        self.assertIsNone(tree.hit((305,0,-1),(305,0,1)))

    def test_profile_checks_lower_waterline_and_curved_head(self):
        profile=next(p for p in audit.profiles() if p['mesh']=='Vest_SideWindow' and p['tidal'])
        samples=audit.samples(profile)
        self.assertEqual(len([p for p in samples if p['tidal']]),9)
        self.assertTrue(any(p['start'][1]==.05 for p in samples))
        self.assertEqual(len([p for p in samples if not p['clear']]),1)

    def test_missing_profile_does_not_pass_silently(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder); meshes=root/'Assets/Piranesi/Meshes';meshes.mkdir(parents=True)
            data=root/'Assets/Piranesi/Data';data.mkdir();(data/'HouseLayout.json').write_text('{"objects":[],"seats":[]}')
            write_mesh(meshes/'sample-mesh-20261011.bytes')
            result=audit.audit_project(root)
            self.assertTrue(any(f['id']=='missing-profile-meshes' for f in result['findings']))

    def test_neighboring_rotated_wall_is_checked(self):
        profile=dict(mesh='Opening',label='test-arch',axis=2,center=0,bottom=0,spring=1,radius=1,depth=(-1,1),tidal=True)
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder); meshes=root/'Assets/Piranesi/Meshes';meshes.mkdir(parents=True)
            data=root/'Assets/Piranesi/Data';data.mkdir()
            write_mesh(meshes/'Opening.bytes',z=50);write_mesh(meshes/'Neighbor.bytes',z=0)
            layout={'objects':[{'name':'Opening','mesh':'Opening','p':[0,0,0],'r':90,'s':[1,1,1]},
                               {'name':'Neighbor','mesh':'Neighbor','p':[0,0,0],'r':90,'s':[1,1,1]}],'seats':[]}
            (data/'HouseLayout.json').write_text(json.dumps(layout))
            with patch.object(audit,'profiles',return_value=[profile]): result=audit.audit_project(root)
            self.assertGreater(result['metrics']['blocked_assembled_samples'],0)

    def test_definition_output_naming_requires_date_and_lowercase(self):
        for name in ('no-windows-audit-tool.json','No-Windows-Audit-20261011.json','bad-name-20261332.json'):
            with self.assertRaises(ValueError): audit.base.validate_output_name(name)
        self.assertEqual(audit.base.validate_output_name('no-windows-audit-tool-20261011.json').name,'no-windows-audit-tool-20261011.json')


if __name__=='__main__': unittest.main()
