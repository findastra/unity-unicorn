"""Synthetic tests only; never scan owner projects or create usage receipts."""
import importlib.util
from datetime import datetime, timezone
import json
from pathlib import Path
import struct
import tempfile
import unittest
from unittest.mock import patch
import zlib

SOURCE = Path(__file__).resolve().parents[1]/"scripts/masonry-structural-audit-20261011.py"
spec = importlib.util.spec_from_file_location("masonry_audit", SOURCE)
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


def mesh_bytes(positions, normals=None, subs=None, uv=None, reserved=1):
    normals = normals or [(0, 0, 1)]*len(positions)
    subs = subs or [("Wall", [0, 1, 2])]
    result = bytearray(b"PMS2" if uv is not None else b"PMS1")
    result += struct.pack("<iii", len(positions), len(subs), reserved if uv is not None else 0)
    for values in (positions, normals):
        result += struct.pack("<"+"f"*len(values)*3, *[x for v in values for x in v])
    result += bytes([255]*len(positions)*4)
    if uv is not None:
        result += struct.pack("<"+"f"*len(uv)*2, *[x for v in uv for x in v])
    for name, indices in subs:
        encoded = name.encode()
        result += struct.pack("<i", len(encoded))+encoded+struct.pack("<i", len(indices))
        result += struct.pack("<"+"i"*len(indices), *indices)
    return result


class AuditTests(unittest.TestCase):
    def test_completion_times_preserve_distinct_observations_in_one_second(self):
        observed = [datetime(2026,10,11,19,38,20,123456,tzinfo=timezone.utc),
                    datetime(2026,10,11,19,38,20,123789,tzinfo=timezone.utc)]
        with patch.object(audit,"datetime") as clock:
            clock.now.side_effect = observed
            first,second = audit.utc_now(),audit.utc_now()
        self.assertEqual(first,"2026-10-11T19:38:20.123456Z")
        self.assertEqual(second,"2026-10-11T19:38:20.123789Z")
        self.assertNotEqual(first,second)

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="unicorn-synthetic-")
        self.root = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def mesh(self, content):
        path = self.root/"synthetic-mesh-20261011.bytes"
        path.write_bytes(content)
        return audit.read_mesh(path)

    def test_flat_wall_and_horizontal_floor_do_not_trigger_projection_risk(self):
        for pos, normal in [([(0,0,0),(1,0,0),(0,1,0)], (0,0,1)), ([(0,0,0),(1,0,0),(0,0,1)], (0,1,0))]:
            result = audit.mesh_metrics(self.mesh(mesh_bytes(pos, [normal]*3)), {})
            self.assertEqual(result["mixed_projection_triangles"], 0)
            self.assertEqual(result["duplicate_triangles"], 0)

    def test_sloped_masonry_blend_and_uv_opt_in(self):
        pos = [(0,0,0),(1,0,0),(0,1,1)]
        normal = (0, .7071, -.7071)
        mesh = self.mesh(mesh_bytes(pos, [normal]*3))
        self.assertEqual(audit.mesh_metrics(mesh, {})["mixed_projection_triangles"], 1)
        self.assertEqual(audit.mesh_metrics(mesh, {"Wall":True})["invalid_uv_triangles"], 1)
        mapped = self.mesh(mesh_bytes(pos, [normal]*3, uv=[(0,0),(1,0),(0,1)]))
        result = audit.mesh_metrics(mapped, {"Wall":True})
        self.assertEqual(result["mixed_projection_triangles"], 0)
        self.assertEqual(result["uv_mapped_triangles"], 1)

    def test_degenerate_uv_does_not_claim_correct_mapping(self):
        mesh = self.mesh(mesh_bytes([(0,0,0),(1,0,0),(0,1,0)], uv=[(0,0)]*3))
        self.assertEqual(audit.mesh_metrics(mesh, {"Wall":True})["invalid_uv_triangles"], 1)
        unmarked = self.mesh(mesh_bytes([(0,0,0),(1,0,0),(0,1,0)], uv=[(0,0),(1,0),(0,1)], reserved=0))
        self.assertEqual(audit.mesh_metrics(unmarked, {"Wall":True})["invalid_uv_triangles"], 1)

    def test_invalid_buffers_rejected(self):
        good = mesh_bytes([(0,0,0),(1,0,0),(0,1,0)])
        for invalid in [b"PMS1", b"BAD!"+good[4:], good[:-1], good+b"extra",
                        mesh_bytes([(0,0,0),(1,0,0),(0,1,0)], subs=[("Wall", [0,1,3])]),
                        mesh_bytes([(float("nan"),0,0),(1,0,0),(0,1,0)])]:
            with self.subTest(size=len(invalid)), self.assertRaises(ValueError): self.mesh(invalid)

    def test_duplicate_and_coplanar_vs_adjacent_geometry(self):
        pos = [(0,0,0),(4,0,0),(0,4,0),(.2,.2,0),(.8,.2,0),(.2,.8,0)]
        result = audit.mesh_metrics(self.mesh(mesh_bytes(pos, subs=[("Wall",[0,1,2,0,1,2]),("Stone",[3,4,5])])), {})
        self.assertEqual(result["duplicate_triangles"], 1)
        self.assertGreater(result["coplanar_overlap_candidates"], 0)
        adjacent = [(0,0,0),(1,0,0),(0,1,0),(1,1,0)]
        result = audit.mesh_metrics(self.mesh(mesh_bytes(adjacent, subs=[("Wall",[0,1,2]),("Stone",[1,3,2])])), {})
        self.assertEqual(result["coplanar_overlap_candidates"], 0)

    def test_opposite_facing_contacts_are_metrics_not_visual_warnings(self):
        pos = [(0,0,0),(4,0,0),(0,4,0),(.2,.2,0),(.8,.2,0),(.2,.8,0)]
        result = audit.mesh_metrics(self.mesh(mesh_bytes(pos, subs=[("Wall",[0,1,2]),("Stone",[5,4,3])])), {})
        self.assertEqual(result["coplanar_overlap_candidates"],0)
        self.assertEqual(result["opposite_facing_contacts"],1)
        result = audit.mesh_metrics(self.mesh(mesh_bytes(pos, subs=[("Wall",[0,1,2,2,1,0,0,1,2])])), {})
        self.assertEqual(result["duplicate_triangles"],1)
        self.assertEqual(result["opposite_facing_duplicate_triangles"],1)
        self.assertGreater(result["coplanar_overlap_candidates"],0)

    def test_same_material_partial_overlap_is_also_detected(self):
        pos = [(0,0,0),(4,0,0),(0,4,0),(.2,.2,0),(.8,.2,0),(.2,.8,0)]
        result = audit.mesh_metrics(self.mesh(mesh_bytes(pos, subs=[("Wall",[0,1,2,3,4,5])])), {})
        self.assertEqual(result["coplanar_overlap_candidates"],1)
        self.assertEqual(result["duplicate_triangles"],0)

    def test_opening_rays_find_real_mesh_blockage_and_ignore_winding(self):
        vertices = [(0,2,2),(0,4,2),(0,3,5)]
        probes = audit.known_opening_probes("Aqueduct")
        self.assertEqual(len(probes),8)
        for face in [[0,1,2],[2,1,0]]:
            mesh = self.mesh(mesh_bytes(vertices,subs=[("Wall",face)]))
            result = audit.opening_metrics(mesh,probes)
            self.assertEqual(result["blocked_segments"],1)
            self.assertEqual(result["blocked_samples"][0]["opening"],"lower-arch-1")
        outside = self.mesh(mesh_bytes([(5,y,z) for _,y,z in vertices]))
        self.assertEqual(audit.opening_metrics(outside,probes)["blocked_segments"],0)
        glass = self.mesh(mesh_bytes(vertices,subs=[("Glass",[0,1,2])]))
        self.assertEqual(audit.opening_metrics(glass,probes)["blocked_segments"],0)
        self.assertIsNone(audit.segment_triangle_hit((-4,20,20),(4,20,20),vertices))
        self.assertIsNone(audit.segment_triangle_hit((-4,3,3),( -4,4,4),vertices))

    def test_blind_niche_back_is_allowed_but_filled_front_is_blocked(self):
        probes = audit.known_opening_probes("Arcade")
        for x, expected in [(5,1),(6,0)]:
            mesh = self.mesh(mesh_bytes([(x,2,-17),(x,4,-17),(x,3,-14)]))
            self.assertEqual(audit.opening_metrics(mesh,probes)["blocked_segments"],expected)

    def test_closed_floor_proof_and_hidden_contacts_reject_incomplete_bounds(self):
        vertices = [(0,-1,0),(1,-1,0),(1,-1,1),(0,-1,1),(0,0,0),(1,0,0),(1,0,1),(0,0,1)]
        faces = [(0,1,2,3),(4,7,6,5),(0,4,5,1),(3,2,6,7),(0,3,7,4),(1,5,6,2)]
        floor_indices = [i for a,b,c,d in faces for i in (a,b,c,a,c,d)]
        vertices += [(.1,0,.1),(.8,0,.1),(.1,0,.8),(.2,0,.2),(.4,0,.2),(.2,0,.4)]
        wall = ("Wall",[8,9,10,11,12,13])
        mesh = self.mesh(mesh_bytes(vertices,subs=[("Floor",floor_indices),wall]))
        self.assertIsNotNone(audit.closed_floor_cuboid(mesh))
        result = audit.mesh_metrics(mesh,{})
        self.assertEqual(result["covered_floor_contacts"],1)
        self.assertEqual(result["coplanar_overlap_candidates"],0)
        for bad in [floor_indices[:-3],list(reversed(floor_indices))]:
            mesh = self.mesh(mesh_bytes(vertices,subs=[("Floor",bad),wall]))
            self.assertIsNone(audit.closed_floor_cuboid(mesh))
            result = audit.mesh_metrics(mesh,{})
            self.assertEqual(result["covered_floor_contacts"],0)
            self.assertEqual(result["coplanar_overlap_candidates"],1)

    def test_circular_clerestory_probe_detects_a_filled_cap(self):
        mesh = self.mesh(mesh_bytes([(-2,16,10),(2,16,10),(2,21,10)]))
        for name in ("Vest_SideOpen","Vest_SideBlind","Vest_SideWindow"):
            result = audit.opening_metrics(mesh,audit.known_opening_probes(name))
            self.assertEqual(result["tested_segments"],1)
            self.assertEqual(result["blocked_segments"],1)

    def test_assembled_junction_finds_neighbor_cap_when_window_module_is_clear(self):
        clear = self.mesh(mesh_bytes([(0,0,0),(1,0,0),(0,0,1)]))
        cap = self.mesh(mesh_bytes([(-2,17,-18),(2,17,-18),(0,21,-18),
                                   (-2,17,18),(2,17,18),(0,21,18)],subs=[("Wall",[0,1,2,3,4,5])]))
        layout = {"objects":[{"name":"Vestibule_11_Core","mesh":"Vest_Core","p":[0,0,0],"r":0},
            *[{"name":"Vestibule_11_"+name,"mesh":"Vest_SideOpen","p":[0,0,0],"r":yaw}
              for name,yaw in [("N",0),("E",90),("S",180),("W",270)]],
            {"name":"Nave_01_11","mesh":"Nave","p":[-30,0,0],"r":90},
            {"name":"Nave_11_21","mesh":"Nave","p":[30,0,0],"r":90}]}
        meshes = {"Vest_Core":clear,"Vest_SideOpen":clear,"Nave":cap}
        result = audit.assembled_junction_metrics(layout,meshes)
        self.assertEqual(result["status"],"completed")
        self.assertEqual(result["tested_segments"],4)
        self.assertEqual(result["blocked_segments"],2)
        self.assertEqual({sample["junction"] for sample in result["blocked_samples"]},{"east","west"})
        east = next(sample for sample in result["blocked_samples"] if sample["junction"] == "east")
        self.assertEqual(east["blocking_instances"][0]["name"],"Nave_11_21")
        self.assertEqual(east["blocking_instances"][0]["hits"][0]["point"],(12.0,18.7,.5))
        meshes["Nave"] = clear
        self.assertEqual(audit.assembled_junction_metrics(layout,meshes)["blocked_segments"],0)
        self.assertEqual(audit.assembled_junction_metrics({"objects":[]},meshes)["status"],"not-applicable")

    def test_layout_transform_matches_yaw_scale_inverse_and_rejects_tilt(self):
        transform = audit.layout_yaw_transform({"p":[30,2,4],"r":90,"s":[2,3,4]})
        world = audit.layout_point((1,2,3),transform)
        for actual,expected in zip(world,(42,8,2)):
            self.assertAlmostEqual(actual,expected)
        for actual,expected in zip(audit.layout_point(world,transform,True),(1,2,3)):
            self.assertAlmostEqual(actual,expected)
        self.assertEqual(audit.layout_yaw_transform({"r":180,"e":[0,90,0]}),audit.layout_yaw_transform({"r":90}))
        for obj in [{"lie":True},{"e":[10,0,0]},{"s":[0,1,1]}]:
            with self.assertRaises(ValueError): audit.layout_yaw_transform(obj)

    def test_specific_rotation_and_corrected_no_fire(self):
        bad = "mo.rotation_euler.z = (pi / 2 if sx > 0 else -pi / 2)"
        good = "mo.rotation_euler.z = (-pi / 2 if sx > 0 else pi / 2)"
        self.assertEqual(len(audit.source_rotation_findings(bad, "kit2.py")), 1)
        self.assertEqual(audit.source_rotation_findings(good, "kit2.py"), [])
        self.assertEqual(audit.source_rotation_findings("# "+bad, "kit2.py"), [])

    def test_duplicate_layout_and_intended_different_rotations(self):
        obj = {"mesh":"Wall", "p":[0,0,0], "r":0, "s":[1,1,1]}
        self.assertEqual(len(audit.layout_duplicates({"objects":[obj, dict(obj)]})), 1)
        self.assertEqual(audit.layout_duplicates({"objects":[obj, {**obj,"r":90}]}), [])

    def test_png_black_rows(self):
        def chunk(kind, value):
            return struct.pack(">I",len(value))+kind+value+struct.pack(">I",zlib.crc32(kind+value)&0xffffffff)
        path = self.root/"synthetic-texture-20261011.png"
        for pixels, expected in [(bytes([200]*6+[0]*6),[1]), (bytes([200]*12),[])]:
            raw = b"\0"+pixels[:6]+b"\0"+pixels[6:]
            image = b"\x89PNG\r\n\x1a\n"+chunk(b"IHDR",struct.pack(">IIBBBBB",2,2,8,2,0,0,0))+chunk(b"IDAT",zlib.compress(raw))+chunk(b"IEND",b"")
            path.write_bytes(image)
            self.assertEqual(audit.png_black_rows(path)["black_rows"], expected)

    def test_filename_contract(self):
        self.assertEqual(audit.validate_output_name("masonry-audit-20261011-142000.json").name,"masonry-audit-20261011-142000.json")
        for name in ["report.json","Bad-20261011.json","bad name-20261011.json","bad-20261301.json","bad-20261011-250000.json"]:
            with self.assertRaises(ValueError): audit.validate_output_name(name)

    def test_shader_and_material_both_required_to_suppress_projection(self):
        meshdir = self.root/"Assets/Piranesi/Meshes"
        meshdir.mkdir(parents=True)
        (meshdir/"synthetic-wall-20261011.bytes").write_bytes(mesh_bytes([(0,0,0),(1,0,0),(0,1,1)],
            [(0,.7071,-.7071)]*3, uv=[(0,0),(1,0),(0,1)]))
        materials = self.root/"Assets/Piranesi/Generated/Materials"
        materials.mkdir(parents=True)
        (materials/"M_Wall.mat").write_text("- _MasonryUV: 1\n")
        # Fixture paths mirror existing Unity contract filenames; these are
        # isolated temporary copies, never proposed owner deliverables.
        self.assertTrue(any(f["id"].startswith("mixed-projection") for f in audit.audit_project(self.root)["findings"]))
        shaderdir = self.root/"Assets/Piranesi/Shaders"
        shaderdir.mkdir(parents=True)
        (shaderdir/"Stone.shader").write_text("if (_MasonryUV > 0.5) { /* synthetic contract */ }")
        result = audit.audit_project(self.root)
        self.assertFalse(any(f["id"].startswith("mixed-projection") for f in result["findings"]))
        self.assertEqual(result["metrics"]["masonry_meshes"]["synthetic-wall-20261011"]["uv_mapped_triangles"],1)

    def test_png_all_scanline_filters(self):
        def chunk(kind, value):
            return struct.pack(">I",len(value))+kind+value+struct.pack(">I",zlib.crc32(kind+value)&0xffffffff)
        # A single RGB row exercises all predictor branches. Previous row is zero.
        desired = [11,22,33,44,55,66]
        path = self.root/"filtered-texture-20261011.png"
        for mode in range(5):
            encoded = []
            for index, value in enumerate(desired):
                left = desired[index-3] if index >= 3 else 0
                prediction = left if mode in (1,4) else left//2 if mode == 3 else 0
                encoded.append((value-prediction)&255)
            raw = bytes([mode]+encoded)
            path.write_bytes(b"\x89PNG\r\n\x1a\n"+chunk(b"IHDR",struct.pack(">IIBBBBB",2,1,8,2,0,0,0))
                +chunk(b"IDAT",zlib.compress(raw))+chunk(b"IEND",b""))
            with self.subTest(mode=mode):
                self.assertEqual(audit.png_black_rows(path)["black_rows"],[])

    def test_synthetic_project_report_and_failures(self):
        meshdir = self.root/"Assets/Piranesi/Meshes"
        meshdir.mkdir(parents=True)
        (meshdir/"synthetic-wall-20261011.bytes").write_bytes(mesh_bytes([(0,0,0),(1,0,0),(0,1,0)]))
        report = audit.audit_project(self.root)
        self.assertEqual(report["schema"],audit.SCHEMA)
        self.assertEqual(report["status"],"completed")
        self.assertEqual(report["summary"]["findings"],0)
        self.assertTrue(any("no Unity compilation" in item for item in report["limitations"]))
        output = self.root/"failed-attempt-20261011.json"
        self.assertEqual(audit.main(["--project",str(self.root/"missing"),"--output",str(output)]),2)
        self.assertEqual(json.loads(output.read_text())["status"],"failed")


if __name__ == "__main__":
    unittest.main()
