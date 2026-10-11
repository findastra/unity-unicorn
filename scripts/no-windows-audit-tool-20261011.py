"""Read-only open-arch audit for the Piranesi PMS mesh/layout profile.

Checks all material geometry, arch profiles, low tidal passages, neighboring
modules and obsolete window seats. Does not simulate water or launch Unity.
"""
import argparse
from collections import Counter
import hashlib
import importlib.util
import json
from pathlib import Path
import sys

spec = importlib.util.spec_from_file_location('masonry_audit', Path(__file__).with_name('masonry-structural-audit-20261011.py'))
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)
TOOL_ID = 'no-windows-audit-tool'


def overlaps(a, b):
    return all(a[i][0] <= b[i][1] + 1e-7 and a[i][1] >= b[i][0] - 1e-7 for i in range(3))


def bounds(points):
    return tuple((min(p[i] for p in points), max(p[i] for p in points)) for i in range(3))


class TriangleTree:
    """Small bounding-volume tree; all submesh materials participate in rays."""
    def __init__(self, triangles):
        self.bounds = tuple((min(t[2][a][0] for t in triangles), max(t[2][a][1] for t in triangles)) for a in range(3)) if triangles else ((0, 0),) * 3
        self.children = None
        self.triangles = triangles
        if len(triangles) > 24:
            axis = max(range(3), key=lambda a: self.bounds[a][1] - self.bounds[a][0])
            triangles.sort(key=lambda t: sum(t[2][axis]))
            middle = len(triangles) // 2
            self.children = TriangleTree(triangles[:middle]), TriangleTree(triangles[middle:])
            self.triangles = []

    @classmethod
    def from_mesh(cls, mesh):
        triangles = []
        for material, indices in mesh['submeshes']:
            for k in range(0, len(indices), 3):
                vertices = tuple(base.point(mesh['positions'], i) for i in indices[k:k+3])
                triangles.append((material, vertices, bounds(vertices)))
        return cls(triangles)

    def hit(self, start, end, segment_bounds=None):
        region = segment_bounds or bounds((start, end))
        if not overlaps(self.bounds, region): return None
        if self.children:
            return self.children[0].hit(start, end, region) or self.children[1].hit(start, end, region)
        for material, vertices, box in self.triangles:
            if overlaps(box, region):
                point = base.segment_triangle_hit(start, end, vertices)
                if point is not None: return {'material': material, 'point': point}
        return None


def profiles():
    result = []
    def arch(mesh, label, axis, center, bottom, spring, radius, depth):
        result.append(dict(mesh=mesh, label=label, axis=axis, center=center, bottom=bottom,
                           spring=spring, radius=radius, depth=depth, tidal=bottom == 0))
    for mesh in ('Vest_SideOpen', 'Vest_SideBlind', 'Vest_SideWindow'):
        arch(mesh, 'high-open-arch', 2, 0, 16, 18.2, 2.2, (9.6, 12.4))
    arch('Vest_SideWindow', 'sea-level-arch', 2, 0, 0, 7.2, 4, (9.5, 12.4))
    for side in (-1, 1):
        arch('Nave', f'end-arch-{side}', 2, 0, 16, 18.2, 2.2, (side*17.2, side*18.2))
        for k in range(7):
            arch('Nave', f'aisle-arch-{side}-{k}', 0, -13.5+4.5*k, 12.8, 12.95, 1.3, (side*5.7, side*10.2))
    for k in range(8):
        arch('Arcade_Win', f'sea-level-arch-{k}', 0, -15.75+4.5*k, 0, 4.2, 1.2, (4.15, 7.2))
    for height in (6, 12, 18, 23):
        for side in (-1, 1):
            arch('StairTower', f'tower-side-{side}-{height}', 0, 5, height, height+1.85, .35, (side*4.15, side*5.1))
        arch('StairTower', f'tower-back-{height}', 2, 0, height, height+1.85, .35, (9.15, 10.1))
    arch('StairTower', 'vestibule-high-arch', 2, 0, 16, 18.2, 2.2, (-.1, .85))
    return result


def samples(profile):
    p = profile
    def segment(offset, height):
        a = [0, height, 0]; b = a.copy()
        a[p['axis']], b[p['axis']] = p['depth']
        a[2-p['axis']] = b[2-p['axis']] = p['center']+offset
        return a, b
    heights = [p['bottom']+.05, (p['bottom']+p['spring'])/2, p['spring'],
               p['spring']+p['radius']*.55, p['spring']+p['radius']*.9]
    if p['tidal']: heights += [.3, .7]
    result = []
    for height in sorted(set(heights)):
        for offset in (-.75*p['radius'], 0, .75*p['radius']):
            if height > p['spring'] and offset**2+(height-p['spring'])**2 >= p['radius']**2*.98: continue
            start, end = segment(offset, height)
            result.append(dict(label=p['label'], start=start, end=end, clear=True, tidal=p['tidal'] and height <= .7))
    # A rectangular hole cannot pass merely because its centre is open.
    start, end = segment(.92*p['radius'], p['spring']+.92*p['radius'])
    result.append(dict(label=p['label']+'-curved-head-control', start=start, end=end, clear=False, tidal=False))
    return result


def audit_project(project, unity_probes=None):
    project = Path(project)
    meshdir = project/'Assets/Piranesi/Meshes'
    layout = json.loads((project/'Assets/Piranesi/Data/HouseLayout.json').read_text(encoding='utf-8-sig'))
    findings, meshes, trees, hashes = [], {}, {}, {}
    def finding(code, title, evidence):
        findings.append(dict(id=code, severity='error', title=title, evidence=evidence,
                             suggested_fix='Remove the obstruction or restore the declared open arch, then rerun the audit.'))
    scanned = 0
    for path in sorted(meshdir.glob('*.bytes')):
        mesh = base.read_mesh(path); scanned += 1
        if any(material in base.MASONRY for material, _ in mesh['submeshes']):
            meshes[path.stem] = mesh
            hashes[path.stem] = hashlib.sha256(path.read_bytes()).hexdigest()
            for material, indices in mesh['submeshes']:
                if ('glass' in material.lower() or 'window' in material.lower()) and len(indices):
                    finding('architectural-glazing', 'Architectural glazing remains', dict(mesh=path.stem, material=material, triangles=len(indices)//3))
            trees[path.stem] = TriangleTree.from_mesh(mesh)
    if not scanned: raise ValueError('No supported project meshes found')
    by_mesh = {}
    for profile in profiles(): by_mesh.setdefault(profile['mesh'], []).extend(samples(profile))
    by_mesh['Aqueduct'] = [
        dict(label='open-water-channel', start=[0,17.45,29], end=[0,16.95,0], clear=True, tidal=False),
        dict(label='open-water-spout', start=[0,16.95,0], end=[0,16.95,-2.2], clear=True, tidal=False)]
    missing = sorted(set(by_mesh)-set(meshes))
    if missing: finding('missing-profile-meshes', 'Required architectural modules are missing', missing)
    tested, blocked, shape_failures = 0, 0, 0
    for name, probes in by_mesh.items():
        if name not in trees: continue
        for probe in probes:
            hit = trees[name].hit(probe['start'], probe['end']); tested += 1
            if probe['clear'] and hit:
                blocked += 1
                finding('blocked-local-arch', 'A pane, bar, sill or wall blocks an opening', dict(mesh=name, **probe, hit=hit))
            elif not probe['clear'] and not hit:
                shape_failures += 1
                finding('nonarched-head', 'The opening lacks its curved masonry head', dict(mesh=name, **probe))
    instances = []
    for obj in layout.get('objects', []):
        if obj.get('mesh') in trees:
            transform = base.layout_yaw_transform(obj)
            instances.append((obj, transform))
    world_probes = []
    assembled_failures = []
    channel_supports = []
    for obj, transform in instances:
        for probe in by_mesh.get(obj['mesh'], []):
            if not probe['clear']: continue
            start, end = (base.layout_point(probe[k], transform) for k in ('start', 'end'))
            record = dict(name=obj['name']+'/'+probe['label'], start=start, end=end, tidal=probe['tidal'])
            # These two exact samples graze the aqueduct's retaining walls.
            # Keep those working water-channel walls; test the stream above its
            # floor separately. Unity confirmed both edge contacts explicitly.
            if (obj['name']=='Vestibule_22_N' and probe['label']=='high-open-arch'
                    and abs(start[1]-17.1)<1e-5 and abs(abs(start[0]-60)-1.65)<1e-5):
                channel_supports.append(dict(**record,support='Aqueduct channel side wall'))
                continue
            world_probes.append(record)
            for other, other_transform in instances:
                if other is obj: continue  # already checked against all own materials
                local_start, local_end = (base.layout_point(p, other_transform, True) for p in (start, end))
                hit = trees[other['mesh']].hit(local_start, local_end)
                if hit:
                    # The aqueduct deliberately delivers water through this one
                    # arch. Its support below the separately checked stream is
                    # not a pane or bar; retain and report this exact contact.
                    if (obj['name'] == 'Vestibule_22_N' and other['name'] == 'Aqueduct'
                            and probe['label'] == 'high-open-arch' and abs(start[0]-60) < 1e-5
                            and abs(start[1]-16.05) < 1e-5 and hit['material'] == 'Wall'):
                        channel_supports.append(dict(**record, support=other['name']))
                        world_probes.pop()  # collider probes use the open channel above this support
                        break
                    assembled_failures.append(dict(**record, blocking_instance=other['name'], hit=hit))
                    break
    if assembled_failures:
        finding('blocked-assembled-arches', 'Neighboring architecture blocks open passages', assembled_failures[:30])
    seats = [s for s in layout.get('seats', []) if s.get('kind') == 'window']
    if seats: finding('obsolete-layout-seats', 'Window-seat interactions remain in the layout', {'count': len(seats)})
    scene = project/'Assets/Piranesi/Scenes/PiranesiHouse.unity'
    if scene.exists():
        count = scene.read_text(encoding='utf-8-sig').count('m_Name: Seat (window)')
        if count: finding('obsolete-scene-seats', 'Window-seat interactions remain in the saved scene', {'count': count})
    if unity_probes:
        base.write_json(unity_probes, dict(schema='unity-unicorn.open-arch-probes/v1', mesh_sha256=hashes,
                                         meshes=[dict(name=k,sha256=v) for k,v in hashes.items()], probes=world_probes))
    return dict(schema=base.SCHEMA, tool_id=TOOL_ID, project={'name': project.name}, completed_at=base.utc_now(),
                status='completed', summary=dict(errors=len(findings), warnings=0, notes=0, findings=len(findings)),
                metrics=dict(mesh_buffers=scanned, architectural_modules=len(meshes), arch_profiles=len(profiles()),
                             architectural_glazing_triangles=sum(len(indices)//3 for m in meshes.values() for material,indices in m['submeshes'] if 'glass' in material.lower() or 'window' in material.lower()),
                             window_bar_triangles=sum(len(indices)//3 for name,m in meshes.items() if name in ('Nave','Vest_SideOpen','Vest_SideBlind','Vest_SideWindow','Arcade_Win') for material,indices in m['submeshes'] if material=='Iron'),
                             local_samples=tested, blocked_local_samples=blocked, nonarched_heads=shape_failures,
                             assembled_samples=len(world_probes), blocked_assembled_samples=len(assembled_failures),
                             tidal_passage_samples=sum(p['tidal'] for p in world_probes), retained_water_channel_supports=channel_supports, mesh_sha256=hashes),
                findings=findings, limitations=[
                    'Profile covers declared Piranesi wall openings; roof oculi are already open and are not windows.',
                    'Sparse samples and curved-head controls are not an exhaustive surface or player-clearance proof.',
                    'Source/layout checks do not certify collider cooking or the rendered scene; use the companion Unity refresh.',
                    'Checks unobstructed tidal passages, not fluid dynamics. Water remains the existing animated ocean surface.',
                    'Legacy asset identities and empty material slots may retain Window/Glass names without any window geometry.'])


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--usage-log', type=Path)
    parser.add_argument('--unity-probes', type=Path)
    parser.add_argument('--git-ref')
    args = parser.parse_args(argv)
    paths = [p for p in (args.output, args.usage_log, args.unity_probes) if p]
    for path in paths: base.validate_output_name(path)
    if len({p.resolve() for p in paths}) != len(paths): parser.error('Output paths must differ')
    if args.git_ref and args.unity_probes: parser.error('Unity probes require the current working tree')
    try:
        report = base.audit_git_ref(args.project, args.git_ref, auditor=audit_project) if args.git_ref else audit_project(args.project, args.unity_probes)
        report.setdefault('input_source', {'kind': 'working-tree'})
    except Exception as error:
        report = dict(schema=base.SCHEMA, tool_id=TOOL_ID, project={'name': args.project.name},
                      completed_at=base.utc_now(), status='failed', summary={}, metrics={}, findings=[],
                      limitations=[f'{type(error).__name__}: {error}'])
    base.write_json(args.output, report)
    if args.usage_log:
        base.write_json(args.usage_log, dict(schema='unity-unicorn.tool-usage/v1', tool_id=TOOL_ID,
            project=report['project'], completed_at=report['completed_at'], status=report['status'],
            report_filename=args.output.name, report_sha256=hashlib.sha256(args.output.read_bytes()).hexdigest(), summary=report['summary']))
    print(json.dumps({'status': report['status'], 'summary': report['summary'], 'metrics': {k:v for k,v in report['metrics'].items() if k != 'mesh_sha256'}}))
    return 2 if report['status'] == 'failed' else 1 if report['summary'].get('errors') else 0


if __name__ == '__main__':
    raise SystemExit(main())
