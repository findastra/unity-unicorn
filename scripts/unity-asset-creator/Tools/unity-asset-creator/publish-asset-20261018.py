# Claude, 2026-10-19 (planned 2026-10-18 as step 12 of the unity-asset-creator checklist). Publishes exported asset
# versions to findastra/unity-assets-library, Astra's public library of finished Unity world assets. It opens a pull
# request; Astra merges it (her choice, 2026-10-18).
#
# For each export folder Exports/unity-assets-library/<id>/<version>/ (written by Unity Unicorn/Asset Creator/Export
# every ready recipe) it:
#   - checks the manifest: checksums match the files, every checklist step is done, the licence can be public, no file
#     is over 95 MB, and nothing in it names a machine path or a user folder;
#   - copies the package, manifest and previews to assets/<id>/<version>/ in the library;
#   - updates library.json (the index) and the README's assets table;
#   - commits on a new branch, pushes, and opens the pull request (gh).
#
# Usage (from the world project):
#   python -I Tools/unity-asset-creator/publish-asset-20261018.py --library <clone of unity-assets-library> [--init]
#          [--exports Exports/unity-assets-library] [--only <id>@<version> ...] [--no-pr]
#   --init writes the library's first files (README, library.json, .gitattributes) into an empty clone; commit and push
#   those yourself.
import argparse, datetime, hashlib, json, os, re, shutil, subprocess, sys

SCHEMA = 'unity-unicorn.library/v1'
MAX_BYTES = 95 * 1024 * 1024
NOT_PUBLIC = {'unclear', 'research-only', 'store-personal', 'store-commercial'}
MACHINE = re.compile(r'([A-Za-z]:\\\\Users\\\\|[A-Za-z]:/Users/|/Users/|\\\\AppData\\\\|AppData/Local|/home/)', re.I)

README = """# Unity Assets Library

By Astra.

Finished assets from Astra's VRChat worlds, one folder per asset and version. Every version is exported by
**unity-asset-creator**, a tool of Unity Unicorn's program (Unity Stable). It ships only after its 12-step checklist
passes, including Astra's own review in the world, and arrives here as a pull request that Astra merges.

## What a version holds

`assets/<id>/<version>/`:

- `<id>-v<version>.unitypackage`: the prefab and everything it uses (meshes, materials, textures, and our own Unity
  Stable shaders), so it works in a world without anything else installed.
- `manifest.json` (`unity-unicorn.asset/v1`): what it is, who made it and from what (creator, source, licence,
  attribution), its stats, the shaders it needs from the shaders-shop, the finished checklist with notes, the Unity and
  VRChat SDK versions, and a SHA-256 for every file.
- `previews/`: front, three-quarter and top renders in the world's light.

`library.json` lists every asset and version for tools.

## Use one

1. Download the `.unitypackage` and import it (Assets > Import Package > Custom Package).
2. If its manifest lists `requires_shaders`, install those first with Unity Stable's shaders-shop.
3. Drag the prefab into your scene.

Made for Unity 2022.3 with the VRChat Worlds SDK (built-in render pipeline).

## Assets

<!-- assets:start -->
<!-- assets:end -->

## Rules

- No row, no ship: every version carries its provenance in its manifest.
- Paid store assets never go here, and neither do files with machine paths.
- Licence: not chosen yet. Until Astra picks one, ask before using anything outside her worlds.
"""

GITATTRIBUTES = "*.unitypackage binary\n*.png binary\n*.json text eol=lf\n*.md text eol=lf\n"


def sha(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for chunk in iter(lambda: f.read(1 << 20), b''): h.update(chunk)
    return h.hexdigest()


def run(cmd, cwd):
    r = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, encoding='utf-8')
    if r.returncode != 0: raise SystemExit('failed: ' + ' '.join(cmd) + '\n' + r.stdout + r.stderr)
    return r.stdout.strip()


def check(folder):
    """the reasons this export can't be published (empty = fine), and its manifest"""
    problems = []
    mpath = os.path.join(folder, 'manifest.json')
    if not os.path.exists(mpath): return ['no manifest.json'], None
    text = open(mpath, encoding='utf-8-sig').read(); m = json.loads(text)
    if MACHINE.search(text): problems.append('the manifest names a machine path')
    if m.get('license') in NOT_PUBLIC or not m.get('public_ok', False): problems.append("licence '%s' can't be public" % m.get('license'))
    steps = m.get('checklist', [])
    if not steps: problems.append('the manifest has no checklist')
    for step in steps:
        if not step.get('done_at'): problems.append("checklist step '%s' isn't done" % step.get('step'))
    if not any(st.get('step') == 'review' and st.get('done_at') for st in steps): problems.append("Astra's review isn't recorded")
    files = [m['files']['package']] + m['files'].get('previews', [])
    for f in files:
        p = os.path.join(folder, f['name'])
        if not os.path.exists(p): problems.append('missing ' + f['name']); continue
        if os.path.getsize(p) > MAX_BYTES: problems.append(f['name'] + ' is over 95 MB')
        if sha(p) != f['sha256']: problems.append(f['name'] + ' does not match its checksum')
    return problems, m


def human(n):
    return '%.1f MB' % (n / 1048576) if n >= 1048576 else '%d KB' % max(1, n // 1024)


def readme_table(index):
    """the README's assets table: one row per asset, its latest version"""
    rows = ['| Preview | Asset | Latest | Kind | Triangles | Package | Credits |', '|---|---|---|---|---|---|---|']
    for a in sorted(index['assets'], key=lambda x: x['id']):
        v = next(x for x in a['versions'] if x['version'] == a['latest'])
        base = 'assets/%s/%s/' % (a['id'], v['version'])
        prev = next((p for p in v['previews'] if 'three-quarter' in p), v['previews'][0] if v['previews'] else '')
        img = '<img src="%spreviews/%s" width="160">' % (base, prev) if prev else ''
        credits = a.get('attribution') or ''
        rows.append('| %s | **%s**<br>`%s`<br>%s | %s | %s | %s | [%s](%s%s) | %s |' % (
            img, a['name'], a['id'], a['description'].replace('|', '/'), v['version'], a['kind'],
            '{:,}'.format(v['stats'].get('triangles', 0)), human(v['bytes']), base, v['package'], credits.replace('|', '/')))
    return '\n'.join(rows)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--library', required=True, help='a clone of findastra/unity-assets-library')
    ap.add_argument('--exports', default='Exports/unity-assets-library')
    ap.add_argument('--only', nargs='*', default=None, help='id@version to publish (default: every version not in the library yet)')
    ap.add_argument('--init', action='store_true', help="write the library's first files")
    ap.add_argument('--no-pr', action='store_true')
    a = ap.parse_args()
    lib = os.path.abspath(a.library)

    if a.init:
        open(os.path.join(lib, 'README.md'), 'w', encoding='utf-8', newline='\n').write(README)
        open(os.path.join(lib, '.gitattributes'), 'w', encoding='utf-8', newline='\n').write(GITATTRIBUTES)
        json.dump({'schema': SCHEMA, 'assets': []}, open(os.path.join(lib, 'library.json'), 'w', encoding='utf-8', newline='\n'), indent=2)
        print('wrote README.md, .gitattributes, library.json in', lib); return

    index = json.load(open(os.path.join(lib, 'library.json'), encoding='utf-8'))
    have = {(x['id'], v['version']) for x in index['assets'] for v in x['versions']}
    todo = []
    for aid in sorted(os.listdir(a.exports)):
        for ver in sorted(os.listdir(os.path.join(a.exports, aid))):
            if (aid, ver) in have: continue
            if a.only is not None and '%s@%s' % (aid, ver) not in a.only: continue
            todo.append((aid, ver, os.path.join(a.exports, aid, ver)))
    if not todo: print('nothing new to publish'); return

    for aid, ver, folder in todo:
        problems, m = check(folder)
        if problems: raise SystemExit('%s v%s: not published:\n  - %s' % (aid, ver, '\n  - '.join(problems)))

    stamp = datetime.date.today().strftime('%Y%m%d')
    branch = 'export/%s-%s' % ('-'.join('%s-v%s' % (i, v) for i, v, _ in todo)[:80], stamp)
    run(['git', 'fetch', 'origin'], lib)
    run(['git', 'checkout', '-B', branch, 'origin/main'], lib)

    lines = []
    for aid, ver, folder in todo:
        m = json.load(open(os.path.join(folder, 'manifest.json'), encoding='utf-8-sig'))
        dest = os.path.join(lib, 'assets', aid, ver)
        if os.path.exists(dest): raise SystemExit(dest + ' already exists in the library')
        os.makedirs(os.path.join(dest, 'previews'))
        pkg = m['files']['package']
        shutil.copy2(os.path.join(folder, pkg['name']), os.path.join(dest, pkg['name']))
        shutil.copy2(os.path.join(folder, 'manifest.json'), os.path.join(dest, 'manifest.json'))
        for p in m['files'].get('previews', []): shutil.copy2(os.path.join(folder, p['name']), os.path.join(dest, p['name']))
        entry = next((x for x in index['assets'] if x['id'] == aid), None)
        if entry is None:
            entry = {'id': aid, 'versions': []}; index['assets'].append(entry)
        entry.update({'name': m['name'], 'kind': m['kind'], 'description': m['description'], 'creator': m['creator'],
                      'license': m['license'], 'attribution': m.get('attribution', ''), 'latest': ver})
        entry['versions'].append({
            'version': ver, 'exported_at': m['exported_at'], 'package': pkg['name'], 'bytes': pkg['bytes'], 'sha256': pkg['sha256'],
            'previews': [os.path.basename(p['name']) for p in m['files'].get('previews', [])],
            'stats': m['stats'], 'requires_shaders': m.get('requires_shaders', []), 'platforms': m.get('platforms', []),
            'made_in': {'project': m['made_in'].get('project', ''), 'git_commit': m['made_in'].get('git_commit', '')},
            'changelog': m.get('changelog', '')})
        st = m['stats']
        lines.append('- **%s** `%s` v%s (%s): %s triangles, %d materials, textures up to %d px, %s package. Licence: %s.%s' % (
            m['name'], aid, ver, m['kind'], '{:,}'.format(st.get('triangles', 0)), st.get('materials', 0), st.get('max_texture_px', 0),
            human(pkg['bytes']), m['license'], (' ' + m['attribution'] + '.') if m.get('attribution') else ''))
        lines.append('  - Review: ' + next((s.get('note', '') + ' (' + s.get('done_by', '') + ')' for s in m['checklist'] if s.get('step') == 'review'), ''))

    json.dump(index, open(os.path.join(lib, 'library.json'), 'w', encoding='utf-8', newline='\n'), indent=2)
    rp = os.path.join(lib, 'README.md'); readme = open(rp, encoding='utf-8').read()
    readme = re.sub(r'<!-- assets:start -->.*<!-- assets:end -->', '<!-- assets:start -->\n' + readme_table(index).replace('\\', '\\\\') + '\n<!-- assets:end -->', readme, flags=re.S)
    open(rp, 'w', encoding='utf-8', newline='\n').write(readme)

    title = 'Add ' + ', '.join('%s v%s' % (i, v) for i, v, _ in todo)
    run(['git', 'add', '-A', 'assets', 'library.json', 'README.md'], lib)
    run(['git', 'commit', '-m', title + '\n\nExported by unity-asset-creator; published by publish-asset-20261018.py.\n\nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>'], lib)
    run(['git', 'push', '-u', 'origin', branch], lib)
    body = ('New versions from unity-asset-creator. Each one passed all 12 checklist steps, including Astra\'s review in '
            'the world; checksums were verified against the manifests before copying.\n\n' + '\n'.join(lines) +
            '\n\nMerging adds them to the library. Nothing else changes.\n\n🤖 Generated with [Claude Code](https://claude.com/claude-code)\n')
    if a.no_pr: print('pushed', branch); return
    r = subprocess.run(['gh', 'pr', 'create', '--base', 'main', '--head', branch, '--title', title, '--body', body], cwd=lib, capture_output=True, text=True, encoding='utf-8')
    if r.returncode == 0: print(r.stdout.strip()); return
    # gh started from Python can miss the login a shell's gh has (seen 2026-10-19): leave the PR to the shell
    open(os.path.join(lib, '..', 'pr-body.md'), 'w', encoding='utf-8').write(body)
    print('pushed %s, but gh could not open the pull request here:' % branch)
    print((r.stdout + r.stderr).strip())
    print('Run from a shell in the library clone:')
    print('  gh pr create --base main --head %s --title "%s" --body-file ../pr-body.md' % (branch, title))


if __name__ == '__main__':
    main()
