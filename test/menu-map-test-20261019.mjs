// The menu-map panel, its model and sample (Claude Opus 5.5, 2026-10-19).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const html = readFileSync(new URL('../unity-unicorn-20261008.html', import.meta.url), 'utf8');
const sample = JSON.parse(readFileSync(new URL('../docs/menu-map-sample-20261019.json', import.meta.url), 'utf8'));
const sectionStart = html.indexOf('<section id="uu-panels"');
const section = html.slice(sectionStart, html.indexOf('</section>', sectionStart));
const block = (id) => {
  const start = html.indexOf(`<script id="${id}">`);
  assert.ok(start > 0, id);
  return html.slice(start + `<script id="${id}">`.length, html.indexOf('</script>', start));
};
const ctx = vm.createContext({});
vm.runInContext(block('uu-menumap-sample') + block('uu-menumap-model') + ';this.MM = MM; this.MM_SAMPLE = MM_SAMPLE;', ctx);
const { MM, MM_SAMPLE } = ctx;

test('the menu-map panel is a labelled uu-panel inside the panels section', () => {
  assert.ok(section.includes('data-item="unity-unicorn-menu-map-panel"></span>unity-unicorn-menu-map-panel<'));
  for (const id of ['mm-file', 'mm-sample', 'mm-radial', 'mm-crumbs', 'mm-detail', 'mm-total', 'mm-meter', 'mm-groups', 'mm-plist']) {
    assert.ok(section.includes(`id="${id}"`), id);
  }
  for (const id of ['uu-menumap-sample', 'uu-menumap-model', 'uu-menumap-app']) assert.ok(section.includes(`<script id="${id}">`), id);
});

test('the page carries the same sample as docs/menu-map-sample-20261019.json, with made-up names', () => {
  assert.deepEqual(JSON.parse(JSON.stringify(MM_SAMPLE)), sample);
  assert.equal(sample.schema, 'unity-unicorn.menu-map/v1');
  assert.match(sample.sample, /made-up/i);
  assert.doesNotMatch(JSON.stringify(sample), /Poppy|CooGee|Phawksea|Flowtoys/i);
});

test('the sample budget adds up: used is the sum of synced bits, under the limit', () => {
  const bits = sample.parameters.reduce((s, p) => s + p.bits, 0);
  assert.equal(sample.budget.used, bits);
  for (const p of sample.parameters) assert.equal(p.bits, p.synced ? (p.type === 'Bool' ? 1 : 8) : 0, p.name);
  assert.ok(sample.budget.used <= sample.budget.limit);
  assert.ok(sample.budget.before_compressor > sample.budget.used);
});

test('every menu parameter in the sample exists, and every menu_path names a real control', () => {
  const names = new Set(sample.parameters.map((p) => p.name));
  const paths = new Set();
  const walk = (page, path) => {
    assert.ok(page.controls.length <= 8, page.name + ' has at most 8 controls');
    for (const c of page.controls) {
      const here = path ? path + ' › ' + c.name : c.name;
      paths.add(here);
      if (c.parameter) assert.ok(names.has(c.parameter), c.parameter);
      for (const s of c.sub_parameters || []) assert.ok(names.has(s), s);
      if (c.submenu) walk(c.submenu, here);
    }
  };
  walk(sample.menu, '');
  for (const p of sample.parameters) if (p.menu_path) assert.ok(paths.has(p.menu_path), p.menu_path);
});

test('parse keeps the menu, and groups split the bits by add-on', () => {
  const map = MM.parse(JSON.stringify(sample));
  assert.equal(map.avatar, 'Sample Pony');
  assert.equal(map.menu.controls.length, sample.menu.controls.length);
  const effects = map.menu.controls.find((c) => c.name === 'Effects');
  assert.equal(effects.type, 'SubMenu');
  assert.equal(effects.submenu.controls[0].submenu.controls[0].parameter, 'FanLeft');
  const groups = MM.groups(map);
  assert.equal(groups.reduce((s, g) => s + g.bits, 0), sample.budget.used);
  const byName = Object.fromEntries(groups.map((g) => [g.name, g]));
  assert.equal(byName['VRCFury compressor'].bits, 18);
  assert.equal(byName['Not in the menu'].bits, 160);
  assert.equal(byName.Effects.bits, 0);
  assert.equal(byName.Effects.folded, 15);
  assert.equal(groups.reduce((s, g) => s + g.bits + g.folded, 0) - byName['VRCFury compressor'].bits, sample.budget.before_compressor);
});

test('nothing is counted as folded when no compressor ran', () => {
  const map = MM.parse({ schema: 'unity-unicorn.menu-map/v1', menu: { name: 'A', controls: [] },
    parameters: [{ name: 'Hat', type: 'Bool', synced: false, menu_path: 'Clothes › Hat' }, { name: 'Coat', type: 'Bool', synced: true, menu_path: 'Clothes › Coat' }] });
  const clothes = MM.groups(map).find((g) => g.name === 'Clothes');
  assert.equal(clothes.bits, 1);
  assert.equal(clothes.folded, 0);
  assert.equal(map.budget.used, 1);
  assert.equal(map.budget.beforeCompressor, null);
});

test('bad files are refused and unsafe values are dropped', () => {
  assert.throws(() => MM.parse('{"schema":"something-else"}'), /not a menu-map file/);
  assert.throws(() => MM.parse('not json'));
  const map = MM.parse({
    schema: 'unity-unicorn.menu-map/v1',
    icons: { i0: 'javascript:alert(1)', i1: 'data:image/svg+xml;base64,PHN2Zz4=', i2: 'data:image/png;base64,iVBORw0KGgo=' },
    menu: { name: 'X'.repeat(500), controls: [
      { name: '<img src=x onerror=alert(1)>', type: 'Weird', icon: 'i0' },
      { name: 'ok', type: 'Toggle', parameter: 'P', value: 1, icon: 'i2' },
    ] },
    parameters: [{ name: 'P', type: 'Bool' }],
  });
  assert.deepEqual(Object.keys(map.icons), ['i2']);
  assert.equal(map.menu.name.length, 80);
  assert.equal(map.menu.controls[0].type, 'Unknown');
  assert.equal(map.menu.controls[0].icon, null);
  assert.equal(map.menu.controls[1].icon, 'i2');
  assert.equal(map.params[0].bits, 1, 'a parameter without synced: false counts as synced');
});

test('a menu that links back to itself ends instead of looping', () => {
  const loop = { name: 'Loop', controls: [] };
  loop.controls.push({ name: 'Again', type: 'SubMenu', submenu: loop });
  const map = MM.parse({ schema: 'unity-unicorn.menu-map/v1', menu: loop, parameters: [] });
  let depth = 0, page = map.menu;
  while (page.controls.length && page.controls[0].submenu) { page = page.controls[0].submenu; depth++; }
  assert.ok(depth <= 17);
  assert.equal(page.loop, true);
});

test('wedges start at the top and close into rings', () => {
  for (const n of [1, 2, 3, 4, 8]) {
    for (let i = 0; i < n; i++) {
      const w = MM.wedge(i, n);
      assert.match(w.d, /^M .* Z$/);
      assert.ok(Math.hypot(w.x, w.y) > 60 && Math.hypot(w.x, w.y) < 70);
    }
  }
  const top = MM.wedge(0, 8);
  assert.ok(Math.abs(top.x) < 1e-9 && top.y < 0, 'the first wedge is at the top');
  const right = MM.wedge(2, 8);
  assert.ok(right.x > 60 && Math.abs(right.y) < 1e-9, 'clockwise: the third of eight is on the right');
});

test('the panel scripts write text with textContent, never as HTML', () => {
  for (const id of ['uu-menumap-model', 'uu-menumap-app']) {
    assert.doesNotMatch(block(id), /innerHTML|outerHTML|insertAdjacentHTML|document\.write|eval\(|new Function/, id);
  }
});

test('the tool definition matches the embedded catalog entry and the Unity script', () => {
  const def = JSON.parse(readFileSync(new URL('../tools/menu-map-20261019.json', import.meta.url), 'utf8')).tools[0];
  assert.equal(def.id, 'menu-map');
  const words = def.description.split(/\s+/).length;
  assert.ok(words >= 25 && words <= 50, words + ' words');
  assert.ok(html.includes('"description": ' + JSON.stringify(def.description)));
  const cs = readFileSync(new URL('../' + def.source_path, import.meta.url), 'utf8');
  assert.match(cs, /\[MenuItem\("Tools\/Unity Unicorn\/menu-map"/);
  assert.match(cs, /OnPreprocessAvatar/);
  assert.match(cs, /DestroyImmediate\(clone\)/);
  assert.match(cs, /unity-unicorn\.tool-usage\/v1/);
  assert.match(cs, /unity-unicorn\.menu-map\/v1/);
  assert.doesNotMatch(cs, /SaveScene|SaveOpenScenes|MarkSceneDirty/);
  const pet = JSON.parse(readFileSync(new URL('../pet.json', import.meta.url), 'utf8'));
  assert.ok(pet.intelligence_levels.L1.tools.includes('menu-map'));
});
