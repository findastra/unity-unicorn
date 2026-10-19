// unity-asset-creator, unity-assets-library and shaders-shop (Claude Opus 5.5, 2026-10-19): every catalog file points
// at code that is in this repo, the trick walks the same steps as the tool's code, the three are registered in every
// list, and nothing that moved in names a machine path or holds a key.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';

const root = new URL('../', import.meta.url);
const read = (p) => readFileSync(new URL(p, root), 'utf8');
const TOOLS = ['unity-asset-creator', 'unity-assets-library', 'shaders-shop'];
const walk = (dir) => readdirSync(new URL(dir, root)).flatMap((n) => statSync(new URL(dir + n, root)).isDirectory() ? walk(dir + n + '/') : [dir + n]);

test('each catalog file is valid and its source is in this repo (or is the library on GitHub)', () => {
  for (const id of TOOLS) {
    const def = JSON.parse(read(`tools/${id}-20261019.json`));
    assert.equal(def.schema, 'unity-unicorn.tool-catalog/v1');
    const t = def.tools[0];
    assert.equal(t.id, id);
    assert.equal(t.name, `${id}-20261019.json`);
    if (/^https:\/\//.test(t.source_path)) assert.equal(t.source_path, 'https://github.com/findastra/unity-assets-library');
    else assert.ok(existsSync(new URL(t.source_path, root)), `${id}: ${t.source_path} is missing`);
  }
});

test('the tool code is all here, with its Unity .meta files', () => {
  for (const f of ['Assets/UnityAssetCreator/Editor/AssetChecklist.cs', 'Assets/UnityAssetCreator/Editor/AssetExporter.cs',
    'Assets/UnityAssetCreator/Editor/UnityAssetCreator.Editor.asmdef', 'Tools/unity-asset-creator/publish-asset-20261018.py',
    'Tools/unity-asset-creator/unity-asset-creator-checklist-20261018.md'])
    assert.ok(existsSync(new URL('scripts/unity-asset-creator/' + f, root)), f);
  for (const f of ['Assets/ShadersShop/shaders-catalog.json', 'Assets/ShadersShop/Editor/ShadersInstaller.cs',
    'Assets/ShadersShop/UnityStable/HandmadePBR.shader', 'Tools/shaders-shop/scan-world-shaders-20261018.py'])
    assert.ok(existsSync(new URL('scripts/shaders-shop/' + f, root)), f);
  for (const f of [...walk('scripts/unity-asset-creator/Assets/'), ...walk('scripts/shaders-shop/Assets/')])
    if (!f.endsWith('.meta')) assert.ok(existsSync(new URL(f + '.meta', root)), `${f} has no .meta file`);
});

test('the trick lists the same 12 steps, in the same order, as the tool checks them', () => {
  const code = [...read('scripts/unity-asset-creator/Assets/UnityAssetCreator/Editor/AssetChecklist.cs').matchAll(/new Step \{ id = "([a-z-]+)"/g)].map((m) => m[1]);
  const skill = read('skills/unity-asset-creator/SKILL.md');
  const rows = [...skill.matchAll(/^\| (\d+) \| `([a-z-]+)` \|/gm)].map((m) => [Number(m[1]), m[2]]);
  assert.equal(code.length, 12);
  assert.deepEqual(rows.map((r) => r[1]), code);
  assert.deepEqual(rows.map((r) => r[0]), code.map((_, i) => i + 1));
  assert.match(skill, /^name: unity-asset-creator$/m);
  assert.match(skill, /`unity-stable-v1\.0-unity-asset-creator-trick-v1\.0`/);
});

test('the three tools and the trick are registered in pet.json and on the page, each in one panel', () => {
  const pet = JSON.parse(read('pet.json'));
  const L1 = pet.intelligence_levels.L1, L2 = pet.intelligence_levels.L2;
  for (const id of TOOLS) {
    assert.equal(L1.tools.filter((t) => t === id).length, 1, id);
    assert.ok(!Object.values(L1.tools_not_merged).flat().includes(id), `${id} is merged`);
  }
  assert.deepEqual(L1.tools.slice(-3), TOOLS, 'unity-assets-library sits right below unity-asset-creator');
  assert.ok(L2.tricks.includes('unity-asset-creator'));
  const html = read('unity-unicorn-20261008.html');
  const main = JSON.parse(/const TOOLS_MAIN=(\[.*?\]\]);/.exec(html)[1]).map((t) => t[0]);
  assert.deepEqual(main.slice(-3), TOOLS);
  assert.ok(/const TRICKS=\[.*\["unity-asset-creator","skills\/unity-asset-creator\/SKILL\.md/.test(html));
});

test('nothing that moved in names a machine path or holds a key', () => {
  const machine = /([A-Za-z]:[\\/]+Users[\\/]|\/c\/Users\/|AppData[\\/]Local|\/home\/[a-z]|OneDrive)/;
  const secret = /(gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|sk-[A-Za-z0-9]{20,}|-----BEGIN [A-Z ]*PRIVATE KEY)/;
  const files = [...walk('scripts/unity-asset-creator/'), ...walk('scripts/shaders-shop/'), 'skills/unity-asset-creator/SKILL.md',
    ...TOOLS.map((id) => `tools/${id}-20261019.json`), ...TOOLS.map((id) => `docs/${id}-20261019.md`)];
  for (const f of files) {
    // The publisher's own guard spells the patterns it refuses; that line is the one allowed match.
    const text = read(f).split('\n').filter((l) => !/^MACHINE = re\.compile/.test(l)).join('\n');
    assert.doesNotMatch(text, machine, f);
    assert.doesNotMatch(text, secret, f);
  }
});
