#!/usr/bin/env python3
"""Read-only Piranesi masonry audit. Python 3.10+, standard library only.

Does not open Unity, rebuild assets, or certify structural engineering. Reports
mesh-buffer integrity, directional texture projection, coincident geometry,
known generator rotations, PNG seam rows, and duplicate layout placements.
Prepared with Codex (GPT-6), 2026-10-11. Public credit: Astra.
"""
from __future__ import annotations

import argparse
import array
from collections import Counter, defaultdict
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import re
import struct
import subprocess
import sys
import tarfile
import tempfile
import zlib

SCHEMA = "unity-unicorn.structural-audit/v1"
TOOL_ID = "masonry-structural-audit"
MASONRY = {"Wall", "Stone"}
OUTPUT_NAME = re.compile(r"[a-z0-9]+(?:-[a-z0-9]+)*-\d{8}(?:-\d{6})?\.json$")


def utc_now():
    return datetime.now(timezone.utc).isoformat(timespec="microseconds").replace("+00:00", "Z")


def validate_output_name(path):
    path = Path(path)
    if not OUTPUT_NAME.fullmatch(path.name):
        raise ValueError("Output names must be lowercase hyphenated descriptive names ending -YYYYMMDD[-HHMMSS].json.")
    stamp = re.search(r"-(\d{8})(?:-(\d{6}))?\.json$", path.name)
    datetime.strptime(stamp[1]+(stamp[2] or ""), "%Y%m%d%H%M%S" if stamp[2] else "%Y%m%d")
    return path


def read_mesh(path):
    """Strictly decode the documented PMS1/PMS2 format without Unity."""
    data = Path(path).read_bytes()
    if len(data) < 16 or data[:4] not in (b"PMS1", b"PMS2"):
        raise ValueError("Missing PMS1/PMS2 header")
    nv, ns, reserved = struct.unpack_from("<iii", data, 4)
    if not 0 < nv <= 20_000_000 or not 0 < ns <= 65_535:
        raise ValueError("Invalid vertex or submesh count")
    offset = 16

    def take(length):
        nonlocal offset
        if length < 0 or length > len(data) - offset:
            raise ValueError(f"Truncated mesh at byte {offset}, need {length} bytes")
        result = memoryview(data)[offset:offset + length]
        offset += length
        return result

    def number():
        return struct.unpack("<i", take(4))[0]

    def floats(count):
        out = array.array("f")
        out.frombytes(take(count * 4))
        if sys.byteorder != "little":
            out.byteswap()
        if any(not math.isfinite(value) for value in out):
            raise ValueError("Non-finite vertex, normal, or UV coordinate")
        return out

    positions = floats(nv * 3)
    normals = floats(nv * 3)
    take(nv * 4)  # RGBA vertex colors
    uv = floats(nv * 2) if data[:4] == b"PMS2" else None
    subs = []
    for _ in range(ns):
        length = number()
        if not 0 < length <= 65_536:
            raise ValueError("Invalid submesh-name length")
        try:
            name = bytes(take(length)).decode("utf-8")
        except UnicodeDecodeError as error:
            raise ValueError("Invalid UTF-8 submesh name") from error
        count = number()
        if count <= 0 or count % 3:
            raise ValueError("Triangle-index count must be a positive multiple of three")
        indices = array.array("i")
        indices.frombytes(take(count * 4))
        if sys.byteorder != "little":
            indices.byteswap()
        if min(indices) < 0 or max(indices) >= nv:
            raise ValueError("Triangle index outside vertex buffer")
        subs.append((name, indices))
    if offset != len(data):
        raise ValueError(f"Unexpected {len(data) - offset} trailing bytes")
    return {"format": data[:4].decode(), "vertex_count": nv,
            "positions": positions, "normals": normals, "uv": uv,
            "submeshes": subs, "reserved": reserved}


def point(values, index, width=3):
    return tuple(values[index * width:index * width + width])


def subtract(a, b):
    return tuple(x - y for x, y in zip(a, b))


def cross(a, b):
    return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])


def dot(a, b):
    return sum(x*y for x, y in zip(a, b))


def triangle_area(vertices):
    normal = cross(subtract(vertices[1], vertices[0]), subtract(vertices[2], vertices[0]))
    magnitude = math.sqrt(dot(normal, normal))
    return magnitude / 2, tuple(x / magnitude for x in normal) if magnitude > 1e-12 else (0, 0, 0)


def inside(point2, tri):
    signs = []
    for a, b in zip(tri, tri[1:] + tri[:1]):
        signs.append((b[0]-a[0])*(point2[1]-a[1])-(b[1]-a[1])*(point2[0]-a[0]))
    return min(signs) > 1e-7 or max(signs) < -1e-7


def segment_triangle_hit(start, end, vertices):
    """Two-sided Moller-Trumbore segment intersection, independent of winding."""
    direction = subtract(end, start)
    edge1, edge2 = subtract(vertices[1], vertices[0]), subtract(vertices[2], vertices[0])
    h = cross(direction, edge2)
    determinant = dot(edge1, h)
    if abs(determinant) < 1e-10: return None
    offset = subtract(start, vertices[0])
    u = dot(offset, h)/determinant
    if u < -1e-8 or u > 1+1e-8: return None
    q = cross(offset, edge1)
    v = dot(direction, q)/determinant
    if v < -1e-8 or u+v > 1+1e-8: return None
    t = dot(edge2, q)/determinant
    if not 1e-7 < t < 1-1e-7: return None
    return tuple(round(start[a]+t*direction[a], 6) for a in range(3))


def known_opening_probes(name):
    """Declared Piranesi generator design points in exported Unity coordinates.

    Through openings and shallow blind-niche entrances are different contracts.
    These sparse point checks do not certify full human/collider clearance.
    """
    probes = []
    if name == "Aqueduct":
        for bay in range(4):
            z = (bay+.5)*7.25
            for course, y in (("lower",3), ("upper",11)):
                probes.append((f"{course}-arch-{bay+1}", (-4,y,z), (4,y,z)))
    if name in ("Arcade", "Arcade_Win"):
        for bay in range(8):
            z = -15.75+4.5*bay
            probes.append((f"west-niche-{bay+1}", (-4,3,z), (-5.8,3,z)))
            probes.append((f"east-{'window' if name == 'Arcade_Win' else 'niche'}-{bay+1}",
                           (4,3,z), (8 if name == "Arcade_Win" else 5.8,3,z)))
    if name == "Nave":
        for bay in range(7):
            z = -13.5+4.5*bay
            for side in (-1,1):
                probes.append((f"{'west' if side < 0 else 'east'}-niche-{bay+1}", (side*7,3,z), (side*8.7,3,z)))
    if name == "PumpHouse":
        probes.append(("doorway", (-4,1.5,0), (-2,1.5,0)))
    if name in ("Vest_SideOpen", "Vest_SideBlind", "Vest_SideWindow"):
        probes.append(("circular-clerestory", (.5,18.7,9), (.5,18.7,13)))
    return probes


def opening_metrics(mesh, probes):
    triangles = []
    for material, indices in mesh["submeshes"]:
        if material not in MASONRY: continue
        for k in range(0,len(indices),3):
            vertices = tuple(point(mesh["positions"],i) for i in indices[k:k+3])
            bounds = tuple((min(v[a] for v in vertices),max(v[a] for v in vertices)) for a in range(3))
            triangles.append((material,k//3,vertices,bounds))
    result = {"profile": "piranesi-openings-20261011", "tested_segments": len(probes), "blocked_segments": 0, "blocked_samples": []}
    for label, start, end in probes:
        seen, hits = set(), []
        lo, hi = [min(a,b) for a,b in zip(start,end)], [max(a,b) for a,b in zip(start,end)]
        for material, index, vertices, bounds in triangles:
            if any(bounds[a][0]>hi[a]+1e-8 or bounds[a][1]<lo[a]-1e-8 for a in range(3)): continue
            hit = segment_triangle_hit(start,end,vertices)
            if hit is not None and hit not in seen:
                seen.add(hit)
                hits.append({"material":material,"triangle_index":index,"point":hit})
        if hits:
            result["blocked_segments"] += 1
            result["blocked_samples"].append({"opening":label,"start":start,"end":end,"hits":hits[:6]})
    return result


def layout_yaw_transform(obj):
    """The verified junction profile supports Unity position/yaw/scale objects."""
    position, scale = obj.get("p",[0,0,0]), obj.get("s",[1,1,1])
    euler = obj.get("e")
    if euler is not None:
        if len(euler) != 3 or abs(euler[0] % 360) > 1e-7 or abs(euler[2] % 360) > 1e-7:
            raise ValueError("Junction profile does not support tilted Euler transforms.")
        yaw = euler[1]
    else:
        if obj.get("lie"): raise ValueError("Junction profile does not support lying transforms.")
        yaw = obj.get("r",0)
    if len(position) != 3 or len(scale) != 3 or any(abs(x) < 1e-12 for x in scale):
        raise ValueError("Invalid or singular layout position/scale.")
    if not all(math.isfinite(x) for x in [*position,*scale,yaw]):
        raise ValueError("Non-finite layout transform.")
    angle = math.radians(yaw)
    return position, scale, math.cos(angle), math.sin(angle)


def layout_point(value, transform, inverse=False):
    position, scale, cosine, sine = transform
    if inverse:
        x,y,z = subtract(value,position)
        return ((x*cosine-z*sine)/scale[0],y/scale[1],(x*sine+z*cosine)/scale[2])
    x,y,z = (value[a]*scale[a] for a in range(3))
    return (x*cosine+z*sine+position[0],y+position[1],-x*sine+z*cosine+position[2])


def assembled_junction_metrics(layout, meshes):
    """Four declared central-vestibule rays through actual placed masonry.

    This bounded regression catches a neighboring Nave cap closing an otherwise
    open vestibule window. It does not claim a general scene intersection audit.
    """
    probes = [("east",(7,18.7,.5),(14,18.7,.5)), ("west",(-7,18.7,.5),(-14,18.7,.5)),
              ("north",(.5,18.7,7),(.5,18.7,14)), ("south",(.5,18.7,-7),(.5,18.7,-14))]
    required = {"Vestibule_11_Core","Vestibule_11_E","Vestibule_11_W","Vestibule_11_N","Vestibule_11_S","Nave_01_11","Nave_11_21"}
    objects = layout.get("objects",[])
    present = {obj.get("name") for obj in objects}
    result = {"profile":"piranesi-central-clerestory-junctions-20261011", "status":"not-applicable",
              "tested_segments":0,"blocked_segments":0,"tested_instances":0,"blocked_samples":[]}
    if not required <= present:
        result["reason"] = "Known central vestibule/Nave placements are absent; this bounded profile cannot be applied."
        return result
    center = next(obj for obj in objects if obj.get("name") == "Vestibule_11_Core")
    if center.get("mesh") != "Vest_Core" or layout_yaw_transform(center) != ([0,0,0],[1,1,1],1.0,0.0):
        result["reason"] = "Known central vestibule frame changed; update the declared world-space probe profile."
        return result
    missing = sorted({obj.get("mesh") for obj in objects if obj.get("name") in required and obj.get("mesh") not in meshes})
    if missing:
        result["reason"] = "Required junction mesh buffers unavailable: "+", ".join(str(name) for name in missing)
        return result
    blocked = defaultdict(list)
    bounds = {name:tuple((min(mesh["positions"][a::3]),max(mesh["positions"][a::3])) for a in range(3)) for name,mesh in meshes.items()}
    for index,obj in enumerate(objects):
        name = obj.get("mesh")
        if name not in meshes: continue
        try:
            transform = layout_yaw_transform(obj)
        except (ValueError,TypeError) as error:
            result["reason"] = f"Masonry placement {obj.get('name',index)} has unsupported transform: {error}"
            return result
        local_probes = []
        for label,start,end in probes:
            start,end = layout_point(start,transform,True),layout_point(end,transform,True)
            if any(max(start[a],end[a]) < bounds[name][a][0]-1e-7 or min(start[a],end[a]) > bounds[name][a][1]+1e-7 for a in range(3)):
                continue
            local_probes.append((label,start,end))
        if not local_probes: continue
        result["tested_instances"] += 1
        detail = opening_metrics(meshes[name],local_probes)
        for sample in detail["blocked_samples"]:
            hits = [{**hit,"point":tuple(round(x,6) for x in layout_point(hit["point"],transform))} for hit in sample["hits"]]
            blocked[sample["opening"]].append({"layout_index":index,"name":obj.get("name"),"mesh":name,"hits":hits})
    result.update(status="completed",tested_segments=len(probes),blocked_segments=len(blocked))
    result["blocked_samples"] = [{"junction":label,"start":start,"end":end,"blocking_instances":blocked[label]}
                                 for label,start,end in probes if label in blocked]
    return result


def closed_floor_cuboid(mesh):
    """Prove the Floor submesh is one complete outward-facing cuboid.

    Do not infer coverage from a bounding box alone: require its eight corners,
    all six faces, and complementary triangle halves on each face.
    """
    indices = [i for material,sub in mesh["submeshes"] if material == "Floor" for i in sub]
    if len(indices) != 36: return None
    points = [tuple(round(c,5) for c in point(mesh["positions"],i)) for i in indices]
    corners = set(points)
    if len(corners) != 8: return None
    lo = tuple(min(p[a] for p in corners) for a in range(3))
    hi = tuple(max(p[a] for p in corners) for a in range(3))
    if any(hi[a]-lo[a] <= 1e-5 for a in range(3)): return None
    if any(p[a] not in (lo[a],hi[a]) for p in corners for a in range(3)): return None
    faces = defaultdict(list)
    for k in range(0,len(points),3):
        vertices = points[k:k+3]
        area, normal = triangle_area(vertices)
        axis = max(range(3),key=lambda a:abs(normal[a]))
        if area <= 1e-10 or abs(normal[axis]) < .99999: return None
        side = 1 if normal[axis] > 0 else 0
        if any(v[axis] != (hi if side else lo)[axis] for v in vertices): return None
        faces[(axis,side)].append(set(vertices))
    if len(faces) != 6: return None
    for (axis,side), triangles in faces.items():
        if len(triangles) != 2 or any(len(t) != 3 for t in triangles): return None
        shared = triangles[0] & triangles[1]
        if len(shared) != 2: return None
        a,b = tuple(shared)
        if any(a[j] == b[j] for j in range(3) if j != axis): return None
    return {"min":lo,"max":hi}


def mesh_metrics(mesh, mapping, blend=8.0):
    """Normals measure competing projections, not load bearing or desired courses."""
    result = {"masonry_triangles": 0, "masonry_area_m2": 0.0,
              "mixed_projection_triangles": 0, "mixed_projection_area_m2": 0.0,
              "uv_mapped_triangles": 0, "invalid_uv_triangles": 0,
              "duplicate_triangles": 0, "coplanar_overlap_candidates": 0,
              "opposite_facing_contacts": 0, "opposite_facing_duplicate_triangles": 0,
              "covered_floor_contacts": 0,
              "degenerate_triangles": 0, "overlap_samples": [], "duplicate_samples": []}
    exact = {}
    planes = defaultdict(list)
    floor = closed_floor_cuboid(mesh)
    for material, indices in mesh["submeshes"]:
        if material not in MASONRY:
            continue
        uses_uv = mapping.get(material, False)
        for k in range(0, len(indices), 3):
            ids = indices[k:k+3]
            vertices = tuple(point(mesh["positions"], i) for i in ids)
            area, geometric_normal = triangle_area(vertices)
            result["masonry_triangles"] += 1
            if area <= 1e-10:
                result["degenerate_triangles"] += 1
                continue
            result["masonry_area_m2"] += area
            key = tuple(sorted(tuple(round(x, 5) for x in vertex) for vertex in vertices))
            if key in exact:
                same = next((old for old in exact[key] if dot(geometric_normal, old[2]) > 0), None)
                if same:
                    old_material, old_index, old_normal = same
                    result["duplicate_triangles"] += 1
                    if len(result["duplicate_samples"]) < 6:
                        result["duplicate_samples"].append({"materials": [old_material, material],
                            "triangle_indices": [old_index, k//3], "vertices": key})
                else:
                    result["opposite_facing_duplicate_triangles"] += 1
            exact.setdefault(key, []).append((material, k//3, geometric_normal))
            if uses_uv:
                if mesh["uv"] is None or not mesh["reserved"] & 1:
                    result["invalid_uv_triangles"] += 1
                else:
                    uvs = [point(mesh["uv"], i, 2) for i in ids]
                    determinant = ((uvs[1][0]-uvs[0][0])*(uvs[2][1]-uvs[0][1])
                                   - (uvs[1][1]-uvs[0][1])*(uvs[2][0]-uvs[0][0]))
                    result["invalid_uv_triangles"] += abs(determinant) <= 1e-10
                    result["uv_mapped_triangles"] += abs(determinant) > 1e-10
            else:
                ns = [point(mesh["normals"], i) for i in ids]
                n = [sum(v[axis] for v in ns)/3 for axis in range(3)]
                weights = [abs(v)**blend for v in n]
                if sum(weights) > 1e-15 and max(weights)/sum(weights) < .9:
                    result["mixed_projection_triangles"] += 1
                    result["mixed_projection_area_m2"] += area
            # Axis-planar masonry overlaps. Opposite-facing contacts are reported
            # as metrics: with backface culling they cannot z-fight in one view.
            axis = max(range(3), key=lambda a: abs(geometric_normal[a]))
            if abs(geometric_normal[axis]) > .99999:
                axes = [a for a in range(3) if a != axis]
                tri2 = tuple(tuple(v[a] for a in axes) for v in vertices)
                centroid = tuple(sum(v[a] for v in tri2)/3 for a in range(2))
                bounds = (min(v[0] for v in tri2), min(v[1] for v in tri2),
                          max(v[0] for v in tri2), max(v[1] for v in tri2))
                planes[(axis, round(vertices[0][axis], 4))].append((material, k//3, tri2, centroid, bounds, geometric_normal))
    # Sweep on the first projected coordinate keeps broad walls inexpensive.
    for plane, tris in planes.items():
        active = []
        for current in sorted(tris, key=lambda t: t[4][0]):
            material, index, tri, center, bounds, normal = current
            active = [old for old in active if old[4][2] > bounds[0] + 1e-6]
            for omat, oi, ot, oc, ob, old_normal in active:
                if ob[3] <= bounds[1]+1e-6 or bounds[3] <= ob[1]+1e-6:
                    continue
                if inside(center, ot) or inside(oc, tri):
                    if dot(normal, old_normal) < 0:
                        result["opposite_facing_contacts"] += 1
                        continue
                    if (floor and plane[0] == 1 and normal[1] < 0
                            and abs(plane[1]-floor["max"][1]) < 1e-5
                            and all(b[0] >= floor["min"][0]-1e-5 and b[2] <= floor["max"][0]+1e-5
                                    and b[1] >= floor["min"][2]-1e-5 and b[3] <= floor["max"][2]+1e-5
                                    for b in (bounds,ob))):
                        result["covered_floor_contacts"] += 1
                        continue
                    result["coplanar_overlap_candidates"] += 1
                    if len(result["overlap_samples"]) < 6:
                        result["overlap_samples"].append({"plane_axis": "xyz"[plane[0]], "plane_coordinate": plane[1],
                            "materials": [omat, material], "triangle_indices": [oi, index]})
            active.append(current)
    for key in ("masonry_area_m2", "mixed_projection_area_m2"):
        result[key] = round(result[key], 4)
    if result["covered_floor_contacts"]:
        result["floor_contact_proof"] = {"material":"Floor", "closed_cuboid":floor,
            "test":"Both overlapping faces point down, lie on the verified cuboid top, and stay inside its full footprint; the solid floor occludes them from below."}
    return result


def png_black_rows(path):
    """Read 8-bit, non-interlaced grayscale/RGB/RGBA PNGs using stdlib."""
    data = Path(path).read_bytes()
    if not data.startswith(b"\x89PNG\r\n\x1a\n"):
        raise ValueError("Not a PNG")
    offset, compressed, header = 8, bytearray(), None
    while offset < len(data):
        if len(data)-offset < 12:
            raise ValueError("Truncated PNG chunk")
        length = struct.unpack_from(">I", data, offset)[0]
        if length > len(data)-offset-12:
            raise ValueError("PNG chunk exceeds buffer")
        kind = data[offset+4:offset+8]
        payload = data[offset+8:offset+8+length]
        crc = struct.unpack_from(">I", data, offset+8+length)[0]
        if zlib.crc32(kind+payload) & 0xffffffff != crc:
            raise ValueError("PNG checksum mismatch")
        if kind == b"IHDR": header = struct.unpack(">IIBBBBB", payload)
        if kind == b"IDAT": compressed.extend(payload)
        offset += length+12
        if kind == b"IEND": break
    if not header: raise ValueError("PNG missing IHDR")
    width, height, depth, color, compression, filtering, interlace = header
    channels = {0: 1, 2: 3, 6: 4}.get(color)
    if depth != 8 or not channels or interlace or compression or filtering:
        raise ValueError("Only 8-bit non-interlaced grayscale/RGB/RGBA PNG supported")
    if not 0 < width <= 8192 or not 0 < height <= 8192:
        raise ValueError("PNG dimensions exceed audit limit")
    stride = width*channels
    expected = (stride+1)*height
    inflater = zlib.decompressobj()
    raw = inflater.decompress(compressed, expected+1)
    if len(raw) != expected or not inflater.eof:
        raise ValueError("PNG decoded size differs from dimensions")
    previous = bytearray(stride)
    rows = []
    for y in range(height):
        start = y*(stride+1)
        mode = raw[start]
        row = bytearray(raw[start+1:start+1+stride])
        if mode not in range(5): raise ValueError("Unknown PNG filter")
        for x in range(stride):
            left = row[x-channels] if x >= channels else 0
            up = previous[x]
            upper_left = previous[x-channels] if x >= channels else 0
            if mode == 1: predictor = left
            elif mode == 2: predictor = up
            elif mode == 3: predictor = (left+up)//2
            elif mode == 4:
                p = left+up-upper_left
                distances = (abs(p-left), abs(p-up), abs(p-upper_left))
                predictor = (left, up, upper_left)[distances.index(min(distances))]
            else: predictor = 0
            row[x] = (row[x]+predictor) & 255
        if channels == 4:
            black = all(row[x] == 0 for x in range(stride) if x % 4 != 3)
        else: black = not any(row)
        if black: rows.append(y)
        previous = row
    return {"width": width, "height": height, "black_rows": rows}


def source_rotation_findings(text, path):
    findings = []
    for number, line in enumerate(text.splitlines(), 1):
        code = line.split("#", 1)[0]
        if re.search(r"rotation_euler\.z\s*=\s*\(?pi\s*/\s*2\s+if\s+sx\s*>\s*0\s+else\s*-pi\s*/\s*2", code):
            findings.append({"id": f"inward-arch-frame-{Path(path).stem}", "severity": "warning",
                "title": "Arcade niche frame projects into its wall",
                "evidence": {"path": path, "line": number, "source": line.strip(),
                    "reason": "arch_molding projects along local -Y; +90 degrees sends that into +X wall at x=5, with the mirrored defect at x=-5."},
                "suggested_fix": "Reverse the signed rotations for the two niche-wall sides and regenerate the affected mesh modules; verify the wall contact separately."})
    return findings


def layout_duplicates(layout):
    seen = defaultdict(list)
    for index, obj in enumerate(layout.get("objects", [])):
        if not obj.get("mesh"): continue
        # An explicit Euler vector overrides yaw/lie in the Unity builder.
        rotation = obj.get("e") if obj.get("e") is not None else [90 if obj.get("lie") else 0, obj.get("r", 0) % 360, 0]
        key = json.dumps([obj["mesh"], obj.get("p"), rotation, obj.get("s", [1, 1, 1])], sort_keys=True)
        seen[key].append({"index": index, "name": obj.get("name")})
    return [{"placement": json.loads(key), "objects": values} for key, values in seen.items() if len(values) > 1]


def audit_project(project):
    project = Path(project).resolve()
    mesh_dir = project/"Assets/Piranesi/Meshes"
    if not mesh_dir.is_dir(): raise ValueError("Project lacks Assets/Piranesi/Meshes")
    meshes = sorted(mesh_dir.glob("*.bytes"))
    if not meshes: raise ValueError("No PMS .bytes mesh inputs found")
    findings, limitations, metrics = [], [], {"mesh_files": len(meshes), "valid_mesh_files": 0, "masonry_meshes": {}}
    fingerprints = {}
    masonry_meshes = {}

    def evidence_file(path):
        path = Path(path)
        relative = path.relative_to(project).as_posix()
        fingerprints[relative] = hashlib.sha256(path.read_bytes()).hexdigest()
        return relative

    shader = project/"Assets/Piranesi/Shaders/Stone.shader"
    shader_text = shader.read_text(encoding="utf-8") if shader.exists() else ""
    shader_has_uv = bool(re.search(r"_MasonryUV", shader_text) and re.search(r"\bif\s*\(\s*_MasonryUV\s*>\s*0?\.5", shader_text))
    if shader.exists(): evidence_file(shader)
    mapping = {}
    for material in MASONRY:
        path = project/f"Assets/Piranesi/Generated/Materials/M_{material}.mat"
        content = path.read_text(encoding="utf-8") if path.exists() else ""
        if path.exists(): evidence_file(path)
        flag = re.search(r"-\s+_MasonryUV:\s*([0-9.eE+-]+)", content)
        mapping[material] = shader_has_uv and flag is not None and float(flag[1]) > .5
    metrics["masonry_uv_materials"] = mapping
    if not shader_text:
        limitations.append("Stone shader is missing; mapping mode cannot be verified.")
    metrics["projection_threshold"] = "warn where largest normalized abs(normal)^8 weight is below 0.90"
    for path in meshes:
        relative = path.relative_to(project).as_posix()
        try:
            mesh = read_mesh(path)
            metrics["valid_mesh_files"] += 1
            if not any(name in MASONRY for name, _ in mesh["submeshes"]): continue
            masonry_meshes[path.stem] = mesh
            evidence_file(path)
            detail = mesh_metrics(mesh, mapping)
            detail["format"] = mesh["format"]
            metrics["masonry_meshes"][path.stem] = detail
            if detail["covered_floor_contacts"]:
                findings.append({"id":f"covered-floor-contacts-{path.stem.lower()}", "severity":"note",
                    "title":"Downward construction contacts are covered by the floor",
                    "evidence":{"path":relative,"count":detail["covered_floor_contacts"],**detail["floor_contact_proof"]},
                    "suggested_fix":"No visible-surface correction is required for these contacts. Reassess if this Floor mesh is removed, made transparent, or separated from the walls."})
            probes = known_opening_probes(path.stem)
            if probes:
                opening = opening_metrics(mesh,probes)
                detail["known_openings"] = opening
                if opening["blocked_segments"]:
                    findings.append({"id": f"blocked-openings-{path.stem.lower()}", "severity": "error",
                        "title": "Masonry blocks an intended arch opening or niche entrance",
                        "evidence": {"path":relative, **opening},
                        "suggested_fix": "Check cutter winding after reflected coordinate transforms and regenerate the Boolean mesh; confirm through openings stay open and blind niches remain recessed."})
            for field, title, fix, severity in [
                ("mixed_projection_triangles", "Directional masonry blends incompatible projection grids", "Use explicitly opted-in UV mapping with valid per-surface UVs for patterned masonry; preserve intentional vault and arch directions.", "warning"),
                ("invalid_uv_triangles", "Masonry UV mode has missing, unmarked or degenerate coordinates", "Regenerate this module as PMS2 with authored metric-UV flag 1 and nondegenerate masonry UVs before enabling _MasonryUV.", "error"),
                ("duplicate_triangles", "Same-facing coincident masonry triangles within a module", "Remove accidental duplicate faces; these have the same winding and can compete for the same rendered pixels.", "warning"),
                ("coplanar_overlap_candidates", "Same-facing coplanar masonry surfaces overlap", "Inspect these contact planes for visible z-fighting. Move intended trim in front of the wall or remove accidental coplanar faces.", "warning"),
            ]:
                if detail[field]:
                    findings.append({"id": f"{field.replace('_', '-')}-{path.stem.lower()}", "severity": severity, "title": title,
                        "evidence": {"path": relative, "count": detail[field], "metric": field,
                            **({"mixed_area_m2": detail["mixed_projection_area_m2"]} if field == "mixed_projection_triangles" else {}),
                            **({"samples": detail["duplicate_samples"]} if field == "duplicate_triangles" else {}),
                            **({"samples": detail["overlap_samples"]} if field == "coplanar_overlap_candidates" else {})}, "suggested_fix": fix})
        except (ValueError, OSError, struct.error) as error:
            findings.append({"id": f"invalid-mesh-{path.stem.lower()}", "severity": "error", "title": "Invalid PMS mesh buffer",
                "evidence": {"path": relative, "error": str(error)}, "suggested_fix": "Regenerate or restore this mesh before importing or building Unity."})
    kit_path = project/"ArtSource/gen/kit.py"
    kit_text = kit_path.read_text(encoding="utf-8") if kit_path.exists() else ""
    # Only diagnose the known signed-rotation issue with proof of the generator's
    # local projection convention, rather than assigning meaning to arbitrary rotations.
    if "yface - depth" in kit_text:
        for name in ("kit.py", "kit2.py"):
            path = project/"ArtSource/gen"/name
            if path.exists():
                relative = evidence_file(path)
                findings.extend(source_rotation_findings(path.read_text(encoding="utf-8"), relative))
    else:
        limitations.append("Known niche-frame source check skipped: generator projection convention not recognized.")
    layout_path = project/"Assets/Piranesi/Data/HouseLayout.json"
    if layout_path.exists():
        relative = evidence_file(layout_path)
        layout = json.loads(layout_path.read_text(encoding="utf-8-sig"))
        duplicates = layout_duplicates(layout)
        metrics["duplicate_layout_placements"] = len(duplicates)
        instance_counts = Counter(obj.get("mesh") for obj in layout.get("objects",[]) if obj.get("mesh"))
        metrics["masonry_layout_instances"] = {name:instance_counts[name] for name in metrics["masonry_meshes"]}
        junctions = assembled_junction_metrics(layout,masonry_meshes)
        metrics["assembled_clerestory_junctions"] = junctions
        if junctions["status"] != "completed":
            limitations.append("Assembled clerestory check skipped: "+junctions["reason"])
        elif junctions["blocked_segments"]:
            findings.append({"id":"blocked-assembled-clerestory-junctions","severity":"error",
                "title":"Placed masonry blocks an adjoining clerestory window",
                "evidence":{"path":relative,**junctions},
                "suggested_fix":"Match neighboring Nave end-cap apertures to the vestibule clerestory openings, then retest the actual layout junctions; a clear hole in one module is insufficient."})
        for finding in findings:
            path = finding["evidence"].get("path","")
            if path.startswith("Assets/Piranesi/Meshes/"):
                finding["evidence"]["layout_instances"] = instance_counts[Path(path).stem]
        if duplicates:
            findings.append({"id": "duplicate-layout-placements", "severity": "warning", "title": "Repeated identical mesh placements",
                "evidence": {"path": relative, "groups": duplicates}, "suggested_fix": "Remove unintended duplicate placements in the layout generator, then rebuild."})
    else: limitations.append("HouseLayout.json missing: duplicate placement check skipped.")
    texture = project/"Assets/Piranesi/Textures/Wall_Albedo.png"
    if texture.exists():
        relative = evidence_file(texture)
        try:
            result = png_black_rows(texture)
            metrics["wall_albedo"] = result
            if result["black_rows"]:
                findings.append({"id": "wall-black-seam-rows", "severity": "warning", "title": "Entire ashlar texture rows are black",
                    "evidence": {"path": relative, **result}, "suggested_fix": "Ensure every texture pixel is assigned using periodic cell boundaries; regenerate matching albedo, normal and mask maps and inspect the repeat seam."})
        except (ValueError, OSError, zlib.error, struct.error) as error:
            limitations.append(f"Wall PNG seam check unavailable: {error}")
    else: limitations.append("Wall albedo missing: seam row check skipped.")
    limitations += [
        "Static source/data audit; no Unity compilation, asset import, scene rebuild, game rendering, or VRChat validation was performed.",
        "Projection risk uses local triangle-center shading normals, a 0.90 threshold and blend exponent 8; rotated/scaled instance shading is not exhaustively sampled.",
        "UV opt-in contract: shader branch if (_MasonryUV > 0.5), material _MasonryUV > 0.5, PMS2 authored-metric flag 1, and nondegenerate UVs. This checks availability, not intended UV direction or texture distortion.",
        "Coplanar checks cover axis-planar Wall/Stone triangles within each mesh using strict centroid containment. Same-facing overlaps are candidates, not exhaustive polygon intersection tests. Opposite-facing contacts are metrics only because the Stone shader uses backface culling; inspect separately if culling changes.",
        "Downward contacts on a proven closed, outward-facing 12-triangle Floor cuboid are informational notes when both faces fit its top footprint. This assumes the Floor remains solid and opaque; arbitrary floor bounding boxes are never accepted as proof.",
        "Only four declared central-vestibule clerestory segments are tested across placed modules (Unity position/yaw/scale). There is no exhaustive whole-scene cross-module intersection, swept player clearance, collider fidelity, load-bearing capacity, or physical engineering certification.",
        "Horizontal floors, roofs, arch voussoirs and curved vault courses are intentional surface types; orientation alone is never classified as an error.",
        "Opening probes use declared 2026-10-11 Piranesi module dimensions and sparse center segments. They exclude glass/iron and test blind niches only to their expected recess. Update this profile if module dimensions/design intent change; no full doorway, scene or collider clearance is certified.",
    ]
    counts = Counter(f["severity"] for f in findings)
    return {"schema": SCHEMA, "tool_id": TOOL_ID, "project": {"name": project.name},
        "completed_at": utc_now(), "status": "completed", "summary": {"findings": len(findings), "errors": counts["error"], "warnings": counts["warning"], "notes": counts["note"]},
        "findings": findings, "metrics": metrics, "input_sha256": fingerprints, "limitations": limitations}


def write_json(path, value):
    path = validate_output_name(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False)+"\n", encoding="utf-8")


def audit_git_ref(project, ref):
    """Audit committed inputs in a private temporary snapshot; never checkout."""
    project = Path(project).resolve()
    revision = subprocess.run(["git", "-C", str(project), "rev-parse", "--verify", "--end-of-options", ref+"^{commit}"],
        capture_output=True, text=True, check=True).stdout.strip()
    if not re.fullmatch(r"[0-9a-f]{40,64}", revision):
        raise ValueError("Git did not return a commit object id")
    paths = ["Assets/Piranesi/Meshes", "Assets/Piranesi/Shaders/Stone.shader",
        "Assets/Piranesi/Generated/Materials/M_Wall.mat", "Assets/Piranesi/Generated/Materials/M_Stone.mat",
        "Assets/Piranesi/Data/HouseLayout.json", "Assets/Piranesi/Textures/Wall_Albedo.png",
        "ArtSource/gen/kit.py", "ArtSource/gen/kit2.py"]
    with tempfile.TemporaryDirectory(prefix="unicorn-committed-snapshot-") as directory:
        destination = Path(directory).resolve()
        process = subprocess.Popen(["git", "-C", str(project), "archive", "--format=tar", revision, "--", *paths],
            stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        try:
            with tarfile.open(fileobj=process.stdout, mode="r|") as archive:
                for member in archive:
                    target = (destination/member.name).resolve()
                    if not target.is_relative_to(destination):
                        raise ValueError("Unsafe path in Git archive")
                    if member.isdir():
                        target.mkdir(parents=True, exist_ok=True)
                    elif member.isfile():
                        target.parent.mkdir(parents=True, exist_ok=True)
                        with archive.extractfile(member) as source, target.open("wb") as output:
                            while True:
                                chunk = source.read(1024*1024)
                                if not chunk: break
                                output.write(chunk)
                    else:
                        raise ValueError("Links and special files are unsupported in the audit snapshot")
            stderr = process.stderr.read().decode("utf-8", errors="replace")
            if process.wait() != 0:
                raise ValueError("Git archive failed: "+stderr.strip())
            report = audit_project(destination)
            report["project"]["name"] = project.name
            report["input_source"] = {"kind": "git-commit", "commit": revision}
            return report
        finally:
            if process.poll() is None:
                process.kill()
                process.wait()
            process.stdout.close()
            process.stderr.close()


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--git-ref", help="Audit this committed baseline via a temporary Git archive; do not change the checkout")
    parser.add_argument("--usage-log", type=Path, help="Optional private receipt JSON; written only for a completed real audit")
    args = parser.parse_args(argv)
    try:
        validate_output_name(args.output)
        if args.usage_log: validate_output_name(args.usage_log)
        if args.usage_log and args.usage_log.resolve() == args.output.resolve():
            raise ValueError("Report and usage receipt paths must differ")
    except ValueError as error:
        parser.error(str(error))
    try:
        report = audit_git_ref(args.project, args.git_ref) if args.git_ref else audit_project(args.project)
        if not args.git_ref: report["input_source"] = {"kind": "working-tree"}
    except Exception as error:
        report = {"schema": SCHEMA, "tool_id": TOOL_ID, "project": {"name": args.project.name},
            "completed_at": utc_now(), "status": "failed", "summary": {"findings": 0, "errors": 0, "warnings": 0, "notes": 0},
            "findings": [], "metrics": {}, "limitations": [f"Audit did not complete: {type(error).__name__}: {error}"]}
        write_json(args.output, report)
        print(f"Audit failed; attempt recorded in {args.output}", file=sys.stderr)
        return 2
    write_json(args.output, report)
    if args.usage_log:
        receipt = {"schema": "unity-unicorn.tool-usage/v1", "tool_id": TOOL_ID, "project": report["project"],
            "completed_at": report["completed_at"], "status": "completed", "report_filename": args.output.name,
            "report_sha256": hashlib.sha256(args.output.read_bytes()).hexdigest(), "summary": report["summary"]}
        write_json(args.usage_log, receipt)
    print(json.dumps({"status": report["status"], "summary": report["summary"], "report": str(args.output)}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
