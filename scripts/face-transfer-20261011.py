"""face-transfer: keep track of face tracking builds and move a face from one avatar to another.

A VRChat face tracking setup is three things that have to agree:

1. The OSC parameters VRCFaceTracking sends (in the avatar's VRCExpressionParameters asset). Their names say which
   standard the build uses: `FT/v2/...` is VRCFT v5 Unified Expressions, bare `v2/...` is VRCFT v4 Unified
   Expressions, and SRanipal names without a prefix (`JawOpen`, `EyesWiden`, ...) are the legacy v3 set.
2. The module that turns those parameters into animation: a template package (Adjerry91's VRCFT templates, Jerry's
   templates and similar), a VRCFury or Modular Avatar prefab, or a hand-made FX layer. Its animation clips name
   blendshapes as `blendShape.<name>` on a renderer path such as `Body`.
3. The face mesh's blendshapes (in the FBX). Every blendshape the module's clips animate has to exist there, on
   the renderer at that path, or that expression silently does nothing.

Commands (all read-only unless `transfer --apply` is given):

    scan      Read one avatar project and write a face profile: OSC parameters and their standard, the face
              tracking modules found (folders and asset GUIDs), the blendshapes their clips animate, and each
              FBX's blendshapes with the standards they cover. With --builds-log, append one line per scan
              to a private JSONL log of face tracking builds.
    builds    Print the builds log as a table (newest last).
    transfer  Copy face tracking module folders from a source project to a target project, with every
              in-project dependency found by GUID, keeping .meta files so references survive. Before copying
              it checks that the target FBX has every blendshape the module animates and reports what is
              missing. Dry run by default; --apply copies, backing up anything it would overwrite.

    python scripts/face-transfer-20261011.py scan --project "<project>" --out "<private>/face-profile.json" \
        [--builds-log "<private>/face-builds.jsonl"] [--label "Poppy 7 VRCFT"]
    python scripts/face-transfer-20261011.py builds --builds-log "<private>/face-builds.jsonl"
    python scripts/face-transfer-20261011.py transfer --source "<source project>" --target "<target project>" \
        --module "Assets/<FT module folder>" [--module ...] --target-fbx "Assets/<target>.fbx" \
        --backup-dir "<outside Assets>" [--report "<private>/transfer.json"] [--apply]

Unity must not be importing while --apply runs; switch to Unity afterwards so it imports the copied files, then
add the module to the target avatar (drop the prefab under the avatar root, or merge its FX layer and parameters)
and test with VRCFaceTracking running. It does not edit animator controllers or expression parameter assets.
Standard library only.
"""
import argparse, datetime, json, os, re, shutil, sys

# ---------------------------------------------------------------- standards

SRANIPAL = {
    'EyeLeftX', 'EyeRightX', 'EyesY', 'EyeLid', 'LeftEyeLid', 'RightEyeLid', 'EyesWiden', 'EyesSqueeze',
    'EyesDilation', 'JawOpen', 'JawX', 'JawForward', 'MouthApeShape', 'MouthUpperUpRight', 'MouthUpperUpLeft',
    'MouthLowerDownRight', 'MouthLowerDownLeft', 'MouthSmileRight', 'MouthSmileLeft', 'MouthSadRight',
    'MouthSadLeft', 'MouthPout', 'CheekPuffLeft', 'CheekPuffRight', 'CheekSuck', 'TongueOut', 'TongueX',
    'TongueY', 'SmileSadLeft', 'SmileSadRight', 'PuffSuckLeft', 'PuffSuckRight', 'MouthUpperOverturn',
    'MouthLowerOverturn', 'MouthUpper', 'MouthLower', 'JawLeft', 'JawRight',
}
UNIFIED = {
    'EyeLookUpRight', 'EyeLookDownRight', 'EyeLookInRight', 'EyeLookOutRight', 'EyeLookUpLeft', 'EyeLookDownLeft',
    'EyeLookInLeft', 'EyeLookOutLeft', 'EyeClosedRight', 'EyeClosedLeft', 'EyeSquintRight', 'EyeSquintLeft',
    'EyeWideRight', 'EyeWideLeft', 'BrowLowererRight', 'BrowLowererLeft', 'BrowInnerUpRight', 'BrowInnerUpLeft',
    'BrowOuterUpRight', 'BrowOuterUpLeft', 'BrowPinchRight', 'BrowPinchLeft', 'CheekSquintRight', 'CheekSquintLeft',
    'CheekPuffRight', 'CheekPuffLeft', 'CheekSuckRight', 'CheekSuckLeft', 'NoseSneerRight', 'NoseSneerLeft',
    'JawOpen', 'JawRight', 'JawLeft', 'JawForward', 'MouthClosed', 'LipSuckUpperRight', 'LipSuckUpperLeft',
    'LipSuckLowerRight', 'LipSuckLowerLeft', 'LipFunnelUpperRight', 'LipFunnelUpperLeft', 'LipFunnelLowerRight',
    'LipFunnelLowerLeft', 'LipPuckerUpperRight', 'LipPuckerUpperLeft', 'LipPuckerLowerRight', 'LipPuckerLowerLeft',
    'MouthUpperUpRight', 'MouthUpperUpLeft', 'MouthLowerDownRight', 'MouthLowerDownLeft', 'MouthUpperRight',
    'MouthUpperLeft', 'MouthLowerRight', 'MouthLowerLeft', 'MouthCornerPullRight', 'MouthCornerPullLeft',
    'MouthCornerSlantRight', 'MouthCornerSlantLeft', 'MouthFrownRight', 'MouthFrownLeft', 'MouthStretchRight',
    'MouthStretchLeft', 'MouthDimpleRight', 'MouthDimpleLeft', 'MouthRaiserUpper', 'MouthRaiserLower',
    'MouthPressRight', 'MouthPressLeft', 'MouthTightenerRight', 'MouthTightenerLeft', 'TongueOut', 'TongueUp',
    'TongueDown', 'TongueRight', 'TongueLeft', 'TongueRoll',
}
ARKIT = {
    'eyeBlinkLeft', 'eyeLookDownLeft', 'eyeLookInLeft', 'eyeLookOutLeft', 'eyeLookUpLeft', 'eyeSquintLeft',
    'eyeWideLeft', 'eyeBlinkRight', 'eyeLookDownRight', 'eyeLookInRight', 'eyeLookOutRight', 'eyeLookUpRight',
    'eyeSquintRight', 'eyeWideRight', 'jawForward', 'jawLeft', 'jawRight', 'jawOpen', 'mouthClose',
    'mouthFunnel', 'mouthPucker', 'mouthLeft', 'mouthRight', 'mouthSmileLeft', 'mouthSmileRight',
    'mouthFrownLeft', 'mouthFrownRight', 'mouthDimpleLeft', 'mouthDimpleRight', 'mouthStretchLeft',
    'mouthStretchRight', 'mouthRollLower', 'mouthRollUpper', 'mouthShrugLower', 'mouthShrugUpper',
    'mouthPressLeft', 'mouthPressRight', 'mouthLowerDownLeft', 'mouthLowerDownRight', 'mouthUpperUpLeft',
    'mouthUpperUpRight', 'browDownLeft', 'browDownRight', 'browInnerUp', 'browOuterUpLeft', 'browOuterUpRight',
    'cheekPuff', 'cheekSquintLeft', 'cheekSquintRight', 'noseSneerLeft', 'noseSneerRight', 'tongueOut',
}
SHAPE_STANDARDS = {'unified-expressions': UNIFIED, 'arkit': ARKIT, 'sranipal': SRANIPAL}

MODULE_HINTS = re.compile(r'vrcft|facetrack|face[ _-]?tracking|unified ?expressions|jerry|adjerry|sranipal|'
                          r'arkit|lip ?sync|eye ?tracking|\bft\b', re.I)
FT_PARAM_BASES = {'EyeTrackingActive', 'LipTrackingActive', 'ExpressionTrackingActive', 'EyeDilationEnable'}

YAML_EXT = {'.asset', '.prefab', '.unity', '.controller', '.anim', '.mat', '.overrideController', '.mask',
            '.playable', '.physicMaterial', '.lighting'}
GUID_RE = re.compile(rb'guid: ([0-9a-f]{32})')

# ---------------------------------------------------------------- helpers


def read_bytes(path):
    with open(path, 'rb') as f:
        return f.read()


def is_yaml(path):
    return os.path.splitext(path)[1] in YAML_EXT


def rel(project, path):
    return os.path.relpath(path, project).replace('\\', '/')


def walk_assets(project, sub='Assets'):
    root = os.path.join(project, sub)
    for d, dirs, files in os.walk(root):
        dirs[:] = [x for x in dirs if not x.startswith('.')]
        for f in files:
            if not f.endswith('.meta'):
                yield os.path.join(d, f)
        # folders have .meta files too and their own GUIDs
        for x in dirs:
            yield os.path.join(d, x)


def meta_guid(path):
    try:
        m = re.search(rb'^guid: ([0-9a-f]{32})', read_bytes(path + '.meta'), re.M)
    except OSError:
        return None
    return m.group(1).decode() if m else None


def guid_index(project):
    idx = {}
    for p in walk_assets(project):
        g = meta_guid(p)
        if g:
            idx[g] = p
    return idx


def classify_param(name):
    """Return (standard, base expression name) for an OSC parameter name, or (None, None)."""
    for prefix, std in (('FT/v2/', 'vrcft-v5-unified'), ('v2/', 'vrcft-v4-unified')):
        if name.startswith(prefix):
            base = name[len(prefix):]
            # binary-encoded parameters: JawOpen1, JawOpen2, JawOpen4, JawOpenNegative
            base = re.sub(r'(Negative|\d+)$', '', base)
            return std, base
    if name in SRANIPAL or name in FT_PARAM_BASES:
        return 'vrcft-v3-sranipal', name
    return None, None


def fbx_blendshapes(path):
    """Blendshape channel names from a binary or ASCII FBX, without Blender or Unity.

    Binary FBX stores object names as 'name\\x00\\x01Class'. Blendshape channels are SubDeformers, and Unity
    names the channel after it, so those are what animation clips refer to."""
    data = read_bytes(path)
    names = set()
    if data.startswith(b'Kaydara FBX Binary'):
        for m in re.finditer(rb'([\x20-\x7e]{1,120})\x00\x01SubDeformer', data):
            names.add(m.group(1).decode('ascii', 'replace'))
        # Shape geometries are a fallback when channels are unnamed
        if not names:
            for m in re.finditer(rb'([\x20-\x7e]{1,120})\x00\x01Geometry', data):
                names.add(m.group(1).decode('ascii', 'replace'))
    else:
        for m in re.finditer(rb'(?:SubDeformer|Deformer)::\s*([^"\r\n]+)"\s*,\s*"BlendShapeChannel"', data):
            names.add(m.group(1).decode('utf-8', 'replace').strip())
    # Blender exports 'Key.001|JawOpen'-style prefixes on some versions; Unity keeps the part after the last '.'
    return sorted({n.split('|')[-1] for n in names if n and n not in ('Basis',)})


def shape_coverage(shapes):
    s = set(shapes)
    low = {x.lower() for x in s}
    out = {}
    for std, names in SHAPE_STANDARDS.items():
        hit = sum(1 for n in names if n in s or n.lower() in low)
        out[std] = {'found': hit, 'of': len(names)}
    return out


def expression_params(project):
    """All VRCExpressionParameters assets and their parameter names."""
    found = []
    for p in walk_assets(project):
        if not p.endswith('.asset'):
            continue
        data = read_bytes(p)
        if b'parameters:' not in data or b'valueType' not in data:
            continue
        names = [m.group(1).decode('utf-8', 'replace').strip()
                 for m in re.finditer(rb'^\s*-?\s*name: (.+)$', data, re.M)]
        names = [n for n in names if n and n != os.path.splitext(os.path.basename(p))[0]]
        found.append({'path': rel(project, p), 'parameters': names})
    return found


def clip_bindings(paths):
    """(renderer path, blendshape) pairs animated by .anim files."""
    pairs = set()
    for p in paths:
        if not p.endswith('.anim'):
            continue
        data = read_bytes(p).decode('utf-8', 'replace')
        for m in re.finditer(r'attribute: blendShape\.(.+)\n\s*path: (.*)\n', data):
            pairs.add((m.group(2).strip(), m.group(1).strip()))
    return pairs


def files_under(project, folder):
    root = os.path.normpath(os.path.join(project, folder))
    if os.path.isfile(root):
        return [root]
    out = []
    for d, dirs, files in os.walk(root):
        out += [os.path.join(d, f) for f in files if not f.endswith('.meta')]
    return out


def find_modules(project):
    """Asset folders whose names look like face tracking modules (top-most match only)."""
    hits = []
    root = os.path.join(project, 'Assets')
    for d, dirs, files in os.walk(root):
        dirs[:] = [x for x in dirs if not x.startswith('.')]
        keep = []
        for x in dirs:
            if MODULE_HINTS.search(x):
                hits.append(os.path.join(d, x))
            else:
                keep.append(x)
        dirs[:] = keep
    return hits


# ---------------------------------------------------------------- scan


def scan(args):
    project = os.path.abspath(args.project)
    if not os.path.isdir(os.path.join(project, 'Assets')):
        sys.exit(f'not a Unity project (no Assets folder): {project}')

    param_assets = expression_params(project)
    standards = {}
    for a in param_assets:
        ft = []
        for n in a['parameters']:
            std, base = classify_param(n)
            if std:
                ft.append(n)
                standards.setdefault(std, set()).add(base)
        a['face_tracking_parameters'] = ft
    param_assets = [a for a in param_assets if a['face_tracking_parameters']] or param_assets

    modules = []
    for m in find_modules(project):
        files = files_under(project, rel(project, m))
        binds = clip_bindings(files)
        modules.append({
            'folder': rel(project, m),
            'guid': meta_guid(m),
            'files': len(files),
            'prefabs': sorted(rel(project, f) for f in files if f.endswith('.prefab')),
            'animated_blendshapes': sorted({b for _, b in binds}),
            'renderer_paths': sorted({p for p, _ in binds}),
        })

    fbxs = []
    for p in walk_assets(project):
        if p.lower().endswith('.fbx'):
            shapes = fbx_blendshapes(p)
            if shapes:
                fbxs.append({'path': rel(project, p), 'guid': meta_guid(p), 'blendshapes': len(shapes),
                             'coverage': shape_coverage(shapes), 'names': shapes})

    primary = max(standards, key=lambda s: len(standards[s])) if standards else None
    profile = {
        'schema': 'unity-unicorn.face-profile/v1',
        'tool_id': 'face-transfer',
        'label': args.label or os.path.basename(project),
        'project': {'name': os.path.basename(project)},
        'completed_at': datetime.datetime.now(datetime.timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ'),
        'status': 'completed',
        'osc_standard': primary,
        'osc_standards': {k: len(v) for k, v in standards.items()},
        'expression_parameters': param_assets,
        'modules': modules,
        'fbx': fbxs,
    }
    if args.out:
        os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
        with open(args.out, 'w', encoding='utf-8') as f:
            json.dump(profile, f, indent=2)
    if args.builds_log:
        line = {k: profile[k] for k in ('label', 'project', 'completed_at', 'osc_standard', 'osc_standards')}
        line['modules'] = [m['folder'] for m in modules]
        line['face_fbx'] = [{'path': x['path'], 'blendshapes': x['blendshapes']} for x in fbxs]
        line['profile'] = os.path.basename(args.out) if args.out else None
        os.makedirs(os.path.dirname(os.path.abspath(args.builds_log)), exist_ok=True)
        with open(args.builds_log, 'a', encoding='utf-8') as f:
            f.write(json.dumps(line) + '\n')

    print(f"{profile['label']}: OSC standard {primary or 'none found'} "
          f"({', '.join(f'{k} {v}' for k, v in profile['osc_standards'].items()) or 'no FT parameters'})")
    for m in modules:
        print(f"  module {m['folder']}: {len(m['animated_blendshapes'])} blendshapes on {m['renderer_paths']}")
    for x in fbxs:
        cov = ', '.join(f"{k} {v['found']}/{v['of']}" for k, v in x['coverage'].items())
        print(f"  fbx {x['path']}: {x['blendshapes']} blendshapes ({cov})")
    return profile


def builds(args):
    if not os.path.exists(args.builds_log):
        sys.exit('no builds log yet')
    with open(args.builds_log, encoding='utf-8') as f:
        rows = [json.loads(l) for l in f if l.strip()]
    print(f"{'scanned (UTC)':21} {'label':24} {'OSC standard':18} modules")
    for r in rows:
        print(f"{r['completed_at']:21} {r['label'][:24]:24} {str(r['osc_standard']):18} "
              f"{', '.join(os.path.basename(m) for m in r['modules']) or '-'}")


# ---------------------------------------------------------------- transfer


def dependency_closure(project, roots, idx):
    """Every in-project file the roots reference by GUID, recursively. Package files are left out:
    those come from VCC in the target and must be installed there."""
    seen, todo, missing = set(), list(roots), set()
    while todo:
        p = todo.pop()
        if p in seen:
            continue
        seen.add(p)
        if os.path.isfile(p) and is_yaml(p):
            for g in set(GUID_RE.findall(read_bytes(p))):
                g = g.decode()
                dep = idx.get(g)
                if dep is None:
                    missing.add(g)
                elif os.path.isfile(dep) and dep not in seen:
                    todo.append(dep)
    return seen, missing


def transfer(args):
    src, dst = os.path.abspath(args.source), os.path.abspath(args.target)
    for p in (src, dst):
        if not os.path.isdir(os.path.join(p, 'Assets')):
            sys.exit(f'not a Unity project (no Assets folder): {p}')

    src_idx, dst_idx = guid_index(src), guid_index(dst)
    roots = []
    for m in args.module:
        roots += files_under(src, m)
    if not roots:
        sys.exit('no files in the given module folders')
    files, unresolved = dependency_closure(src, roots, src_idx)
    # built-in Unity and package GUIDs are expected to be unresolved here
    unresolved -= {'0000000000000000e000000000000000', '0000000000000000f000000000000000'}

    binds = clip_bindings(files)
    target_shapes = set(fbx_blendshapes(os.path.join(dst, args.target_fbx))) if args.target_fbx else set()
    missing_shapes = sorted({b for _, b in binds if b not in target_shapes}) if args.target_fbx else []
    paths = sorted({p for p, _ in binds})

    plan, conflicts = [], []
    for f in sorted(files):
        r = rel(src, f)
        if not r.startswith('Assets/'):
            continue
        g = meta_guid(f)
        out = os.path.join(dst, r)
        other = dst_idx.get(g)
        if other and os.path.normcase(other) != os.path.normcase(out):
            # same GUID already in the target somewhere else: Unity would drop one of them
            conflicts.append({'file': r, 'guid': g, 'already_at': rel(dst, other)})
            continue
        action = 'same' if os.path.exists(out) and read_bytes(out) == read_bytes(f) else \
                 'overwrite' if os.path.exists(out) else 'copy'
        plan.append({'file': r, 'guid': g, 'action': action})

    report = {
        'schema': 'unity-unicorn.face-transfer/v1',
        'tool_id': 'face-transfer',
        'project': {'name': os.path.basename(dst), 'source': os.path.basename(src)},
        'completed_at': datetime.datetime.now(datetime.timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ'),
        'status': 'completed',
        'applied': bool(args.apply),
        'modules': args.module,
        'files': plan,
        'guid_conflicts': conflicts,
        'unresolved_guids': len(unresolved),
        'renderer_paths': paths,
        'animated_blendshapes': len({b for _, b in binds}),
        'missing_blendshapes_on_target': missing_shapes,
    }

    if args.apply:
        if conflicts:
            sys.exit(f'{len(conflicts)} GUID conflicts; resolve them first (see the dry-run report)')
        backup = os.path.abspath(args.backup_dir)
        if os.path.commonpath([backup, os.path.join(dst, 'Assets')]) == os.path.join(dst, 'Assets'):
            sys.exit('--backup-dir must be outside the target Assets folder')
        for item in plan:
            if item['action'] == 'same':
                continue
            s, d = os.path.join(src, item['file']), os.path.join(dst, item['file'])
            if item['action'] == 'overwrite':
                for x in (d, d + '.meta'):
                    if os.path.exists(x):
                        b = os.path.join(backup, item['file'] + x[len(d):])
                        os.makedirs(os.path.dirname(b), exist_ok=True)
                        shutil.copy2(x, b)
            os.makedirs(os.path.dirname(d), exist_ok=True)
            shutil.copy2(s, d)
            if os.path.exists(s + '.meta'):
                shutil.copy2(s + '.meta', d + '.meta')
            # parent folder metas keep folder GUIDs stable too
            fs, fd = os.path.dirname(s), os.path.dirname(d)
            while os.path.normcase(fs) != os.path.normcase(os.path.join(src, 'Assets')):
                if os.path.exists(fs + '.meta') and not os.path.exists(fd + '.meta'):
                    shutil.copy2(fs + '.meta', fd + '.meta')
                fs, fd = os.path.dirname(fs), os.path.dirname(fd)

    if args.report:
        os.makedirs(os.path.dirname(os.path.abspath(args.report)), exist_ok=True)
        with open(args.report, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2)

    counts = {a: sum(1 for i in plan if i['action'] == a) for a in ('copy', 'overwrite', 'same')}
    print(f"{'Applied' if args.apply else 'Dry run'}: {counts['copy']} to copy, {counts['overwrite']} to overwrite "
          f"(backed up), {counts['same']} already identical, {len(conflicts)} GUID conflicts")
    print(f"Module animates {report['animated_blendshapes']} blendshapes on renderer paths {paths}")
    if args.target_fbx:
        if missing_shapes:
            print(f"Target FBX is missing {len(missing_shapes)}: {', '.join(missing_shapes[:30])}"
                  f"{' ...' if len(missing_shapes) > 30 else ''}")
        else:
            print('Target FBX has every blendshape the module animates.')
    if unresolved:
        print(f'{len(unresolved)} referenced GUIDs are not in the source Assets (packages or missing files); '
              'install the same packages in the target through VCC.')
    return report


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    sub = p.add_subparsers(dest='cmd', required=True)
    s = sub.add_parser('scan')
    s.add_argument('--project', required=True)
    s.add_argument('--out')
    s.add_argument('--builds-log')
    s.add_argument('--label')
    b = sub.add_parser('builds')
    b.add_argument('--builds-log', required=True)
    t = sub.add_parser('transfer')
    t.add_argument('--source', required=True)
    t.add_argument('--target', required=True)
    t.add_argument('--module', action='append', required=True, help='Assets/... folder or file; repeatable')
    t.add_argument('--target-fbx', help='Assets/... path of the target face FBX')
    t.add_argument('--backup-dir', default='face-transfer-backup')
    t.add_argument('--report')
    t.add_argument('--apply', action='store_true')
    a = p.parse_args(argv)
    return {'scan': scan, 'builds': builds, 'transfer': transfer}[a.cmd](a)


if __name__ == '__main__':
    main()
