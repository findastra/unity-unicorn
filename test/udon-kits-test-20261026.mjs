// Udon kits: the manifest gate and the UdonSharp limits gate (the Cung List, item 1: Placeforge's strict gate). The C#
// tests run the cores and parity (scripts/udon-kits/Tests, `dotnet run`); this file checks, with no .NET needed, that the
// manifest is the fixed vocabulary and matches the code, that every wrapper stays inside what UdonSharp compiles, and
// that cores never touch Unity or VRChat. Claude Opus 5.5 (claude-opus-5-5), 2026-10-26.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync } from 'node:fs';

const root = new URL('../scripts/udon-kits/', import.meta.url);
const read = (rel) => readFileSync(new URL(rel, root), 'utf8').replace(/\r\n/g, '\n');
const manifest = JSON.parse(read('udon-kits-manifest.json'));
const kitsDir = 'Assets/UdonKits/Kits/';
const wrapperFiles = readdirSync(new URL(kitsDir, root)).filter((f) => f.endsWith('.cs'));
const coreFiles = readdirSync(new URL('Cores/', root)).filter((f) => f.endsWith('.cs'));

// Comments and string literals out, so the gate reads only code.
const code = (src) => src
  .replace(/\/\*[\s\S]*?\*\//g, ' ')
  .replace(/\/\/[^\n]*/g, ' ')
  .replace(/"(?:[^"\\\n]|\\.)*"/g, '""');

const FIXED_IDS = ['doorspace-door', 'teleport-pad', 'glass-animal-collectible', 'toggle-switch', 'synced-counter', 'timed-respawn', 'audio-zone', 'quest-checklist'];
const FIXED_FAMILIES = ['travel', 'collect', 'toggle', 'sync', 'spawn', 'audio', 'quest'];

test('the manifest is the fixed vocabulary: these ids and families, in this order, and nothing else', () => {
  assert.equal(manifest.schema, 'unity-unicorn.udon-kits/v1');
  assert.deepEqual(manifest.ids, FIXED_IDS, 'a new kit is added here, in the manifest and in the C# tests together');
  assert.deepEqual(manifest.families, FIXED_FAMILIES);
  assert.deepEqual(manifest.kits.map((k) => k.id), manifest.ids);
  for (const id of manifest.ids) assert.match(id, /^[a-z]+(-[a-z]+)*$/, id);
  for (const k of manifest.kits) {
    assert.ok(manifest.families.includes(k.family), k.id + ' family');
    for (const key of ['id', 'family', 'purpose', 'core', 'wrappers', 'seams', 'inputs', 'calls', 'events', 'tested']) assert.ok(key in k, k.id + ' has ' + key);
    assert.equal(k.tested, true, k.id + ': only tested kits are listed');
    for (const s of k.seams) assert.ok(s in manifest.seams, k.id + ' seam ' + s);
  }
  for (const f of manifest.families) assert.ok(manifest.kits.some((k) => k.family === f), 'family ' + f + ' is used');
  assert.ok(existsSync(new URL('../../' + manifest.trick, root)), 'the trick exists');
  assert.ok(existsSync(new URL('../../' + manifest.guide, root)), 'the guide exists');
});

test('every core, wrapper, input, call and event in the manifest is real, and every wrapper field is in the manifest', () => {
  const listed = new Set();
  for (const k of manifest.kits) {
    assert.ok(existsSync(new URL(k.core, root)), k.core);
    const classes = new Set();
    for (const w of k.wrappers) {
      assert.ok(existsSync(new URL(w, root)), w);
      assert.ok(!listed.has(w), w + ' belongs to one kit');
      listed.add(w);
      classes.add(w.split('/').pop().replace(/\.cs$/, ''));
    }
    const src = (cls) => code(read(kitsDir + cls + '.cs'));
    for (const i of k.inputs) {
      assert.ok(classes.has(i.on), `${k.id}: input ${i.name} is on one of its wrappers`);
      const type = i.type.replace(/[[\]]/g, '\\$&');
      assert.match(src(i.on), new RegExp(`public ${type} ${i.name}\\b`), `${k.id}: ${i.on}.${i.name} is a public ${i.type}`);
    }
    for (const e of k.events) assert.match(src(e.on), new RegExp(`public string ${e.name} = ""`), `${k.id}: ${e.on}.${e.name} event field`);
    for (const c of k.calls) assert.match(src(c.on), new RegExp(`public (void|bool|int) ${c.name}\\(\\)`), `${k.id}: ${c.on}.${c.name}() can be sent as an event`);
    for (const cls of classes) {
      const fields = [...src(cls).matchAll(/^\s*public (?!override|class|bool [A-Z]|int [A-Z]|float [A-Z]|void )[\w.[\]]+ (\w+)\s*(=[^;]*)?;/gm)].map((m) => m[1]);
      const named = new Set([...k.inputs, ...k.events].filter((x) => x.on === cls).map((x) => x.name).concat(['listener']));
      for (const f of fields) assert.ok(named.has(f), `${k.id}: ${cls}.${f} is in the manifest`);
    }
  }
  assert.deepEqual([...listed].map((w) => w.split('/').pop()).sort(), [...wrapperFiles].sort(), 'every wrapper file is in one kit');
});

// What UdonSharp 1.x (VRChat Worlds SDK 3.10) compiles, kept stricter than the compiler so a wrapper never surprises it.
const FORBIDDEN = [
  [/\busing\s+System\b/, 'System namespaces (LINQ, generic collections)'],
  [/\b[A-Za-z_]\w*\s*<\s*[A-Za-z_][\w.]*(\s*,\s*[A-Za-z_][\w.]*)*\s*>/, 'generics (List<T>, GetComponent<T>)'],
  [/=>/, 'lambdas and => members'],
  [/\b(interface|struct|enum|delegate|event)\b/, 'interfaces, structs, enums, delegates and events'],
  [/\b(try|catch|finally|throw)\b/, 'exceptions'],
  [/\b(async|await|yield|goto|dynamic|unsafe|params|ref)\b/, 'async, iterators, goto, dynamic, unsafe, params and ref'],
  [/\b(static|abstract|virtual|sealed|partial)\b/, 'static, abstract, virtual, sealed and partial members'],
  [/\?\.|\?\?|\?\[/, 'null-conditional and null-coalescing operators'],
  [/\$"/, 'string interpolation'],
  [/\bnew\s+(List|Dictionary|HashSet|Queue|Stack)\b/, 'collections'],
];
const ALLOWED_USINGS = ['UdonSharp', 'UnityEngine', 'VRC.SDKBase', 'VRC.SDK3.Persistence', 'VRC.SDK3.Components'];

test('every wrapper is one UdonSharpBehaviour inside the UdonSharp subset', () => {
  assert.equal(wrapperFiles.length, 11, "eleven wrappers for eight kits");
  for (const f of wrapperFiles) {
    const raw = read(kitsDir + f);
    const c = code(raw);
    const name = f.replace(/\.cs$/, '');
    assert.match(c, new RegExp(`\\npublic class ${name} : UdonSharpBehaviour\\n`), f + ': class named like its file');
    assert.equal(c.match(/\bclass\b/g).length, 1, f + ': one class');
    assert.match(c, /\[UdonBehaviourSyncMode\(BehaviourSyncMode\.(None|Manual)\)\]/, f + ': says its sync mode');
    for (const u of c.matchAll(/^using ([\w.]+);/gm)) assert.ok(ALLOWED_USINGS.includes(u[1]), f + ': using ' + u[1]);
    for (const [re, what] of FORBIDDEN) assert.doesNotMatch(c, re, `${f}: no ${what}`);
    if (c.includes('[UdonSynced]')) {
      assert.match(c, /BehaviourSyncMode\.Manual/, f + ': synced fields need Manual sync');
      for (const m of c.matchAll(/\[UdonSynced\] private (\w+) /g)) assert.ok(['bool', 'int', 'float'].includes(m[1]), f + ': synced ' + m[1]);
      assert.match(c, /RequestSerialization\(\)/, f + ': sends what it changes');
      assert.match(c, /public override void OnDeserialization\(\)/, f + ': applies what arrives');
    }
    assert.match(raw, /Claude Opus 5\.5 \(claude-opus-5-5\), 2026-10-26/, f + ': credits the model');
  }
});

test('every core is plain C#: no Unity, VRChat or UdonSharp, built under the strict type gate', () => {
  for (const f of coreFiles) {
    const c = code(read('Cores/' + f));
    assert.doesNotMatch(c, /\b(UnityEngine|VRC|UdonSharp)\b/, f);
    assert.match(c, /namespace UdonKits\.Cores\b/, f);
  }
  for (const k of manifest.kits) assert.ok(coreFiles.includes(k.core.split('/').pop()), k.core);
  const proj = read('Cores/UdonKits.Cores.csproj');
  for (const s of ['<Nullable>enable</Nullable>', '<TreatWarningsAsErrors>true</TreatWarningsAsErrors>', '<LangVersion>9.0</LangVersion>', '<TargetFramework>netstandard2.1</TargetFramework>'])
    assert.ok(proj.includes(s), 'the cores project has ' + s);
  assert.doesNotMatch(proj + read('Tests/UdonKits.Tests.csproj'), /PackageReference/, 'no packages: the tests build offline');
});

test('every kit has core and parity tests, and the workflow runs them on push', () => {
  const tests = readdirSync(new URL('Tests/', root)).filter((f) => f.endsWith('.cs')).map((f) => read('Tests/' + f)).join('\n');
  for (const id of manifest.ids) {
    assert.ok(tests.includes(`[KitTest("${id}", TestKind.Core)]`), id + ' core test');
    assert.ok(tests.includes(`[KitTest("${id}", TestKind.Parity)]`), id + ' parity test');
  }
  for (const m of tests.matchAll(/\[KitTest\("([^"]+)"/g)) assert.ok(manifest.ids.includes(m[1]), 'test for unknown kit ' + m[1]);
  const wf = readFileSync(new URL('../.github/workflows/udon-kits-20261026.yml', import.meta.url), 'utf8');
  assert.match(wf, /on:\n\s+push:/);
  assert.match(wf, /dotnet run --project scripts\/udon-kits\/Tests/);
  assert.match(wf, /node --test test\/udon-kits-test-20261026\.mjs/);
});
