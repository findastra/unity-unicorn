// Tool names and categories (Astra, 2026-10-24: "its a spawner if it mades 3D objects, its a painter if it changes
// existing object surfaces"): every tool the page lists has one category in docs/tool-categories-20261024.json, the
// page carries the same list, and every tool row starts with its category. Claude Opus 5.5 (claude-opus-5-5), 2026-10-24.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const html = readFileSync(new URL('../unity-unicorn-20261008.html', import.meta.url), 'utf8').replace(/\r\n/g, '\n');
const cats = JSON.parse(readFileSync(new URL('../docs/tool-categories-20261024.json', import.meta.url), 'utf8'));
const list = (name) => JSON.parse(new RegExp(`\\nconst ${name}=(\\[[\\s\\S]*?\\]);\\n`).exec(html)[1]);

test('Astra\'s two categories are hers, word for word', () => {
  const byId = Object.fromEntries(cats.categories.map((c) => [c.id, c]));
  assert.equal(byId.spawner.word, 'spawn');
  assert.equal(byId.spawner.from, 'Astra');
  assert.match(byId.spawner.means, /new 3D objects/);
  assert.equal(byId.painter.word, 'painter');
  assert.equal(byId.painter.from, 'Astra');
  assert.match(byId.painter.means, /surfaces of objects that already exist/);
  for (const c of cats.categories) if (!['spawner', 'painter'].includes(c.id)) assert.equal(c.from, 'proposed', c.id);
});

test('every tool on the page has exactly one known category', () => {
  const ids = new Set(cats.categories.map((c) => c.id));
  const page = [...list('TOOLS_MAIN'), ...list('UNMERGED'), ...list('IN_WORLDS')].map((t) => t[0]);
  assert.deepEqual([...new Set(page)].sort(), Object.keys(cats.tools).sort());
  for (const [tool, c] of Object.entries(cats.tools)) assert.ok(ids.has(c), tool + ' has category ' + c);
});

test('colours run red to pink, and second and third jobs are real categories', () => {
  assert.deepEqual(cats.categories.map((c) => c.color_name), ['red', 'orange', 'yellow', 'green', 'teal', 'blue', 'indigo', 'violet', 'light pink', 'hot pink']);
  for (const c of cats.categories) assert.match(c.color, /^#[0-9a-f]{6}$/, c.id);
  const ids = new Set(cats.categories.map((c) => c.id));
  for (const [tool, extra] of Object.entries(cats.also)) {
    assert.ok(tool in cats.tools, tool);
    assert.ok(extra.length >= 1 && extra.length <= 2, tool + ' does two or three jobs');
    for (const c of extra) assert.ok(ids.has(c) && c !== cats.tools[tool], tool + ' also ' + c);
  }
});

test('every rename keeps the main category word and no two tools share a name', () => {
  const word = Object.fromEntries(cats.categories.map((c) => [c.id, c.word]));
  const names = Object.keys(cats.tools).map((t) => cats.renames[t] || t);
  assert.equal(new Set(names).size, names.length);
  for (const t of Object.keys(cats.tools)) assert.ok((cats.renames[t] || t).endsWith('-' + word[cats.tools[t]]), t);
});

test('every tool is in one related group, and the toolset sorts by group, then A to Z', () => {
  assert.deepEqual(Object.keys(cats.groups).sort(), Object.keys(cats.tools).sort());
  for (const [t, g] of Object.entries(cats.groups)) assert.ok(cats.group_order.includes(g), t + ' group ' + g);
  assert.ok(html.includes('.sort((a,b)=>gi(a)-gi(b)||newName(a).localeCompare(newName(b)))'));
});

// Since 2026-10-24 the page is the hub's pet program frame (last night's Wonderland Launchpad layout), with Unity
// Stable's own tools in its L1 control panel, each listed once.
test('L1 is the hub frame: programs as pills with a count, then the native tools, each once', () => {
  assert.ok(html.includes('<h3>3rd party programs · 6</h3><ul class="items"><li class="pill">'));
  assert.ok(!html.includes('class="side-avatar"') && !html.includes('class="side-world"'), 'the hub pills for the tools are replaced by the toolset');
  assert.ok(html.includes('<section class="wide uu"><h3>Native tools · 85</h3>'));
  for (const k of ['avatar-build', 'avatar-sparkles', 'avatar-effects', 'avatar-records', 'every-world', 'piranesi-house', 'astras-infinite-pole', 'liminal-doors'])
    assert.ok(!html.includes(`id="uu-p-${k}"`), k + ' list panel folded into the cards');
  assert.ok(html.includes('el("p",null,"Made for: "+(MADE_FOR[t.panel]'), 'made for, in each card');
  assert.ok(html.includes('<script id="uu-fold-library">'), 'run records open in each card');
  assert.ok(html.includes('<link rel="stylesheet" href="pet-program-page-20261018.css">') && html.includes('<script src="pet-program-page-20261018.js"></script>'));
  assert.ok(html.includes(':root{--slate:#252a33;'), 'Unity Stable keeps its grays');
});

test('the world-building toolset, inside the L1 tool panel, draws every tool with its colours and a send button', () => {
  assert.ok(html.includes('data-item="unity-unicorn-world-building-toolset"></span>unity-unicorn-world-building-toolset<'));
  // native tools are L1, so the toolset sits inside the L1 tool panel, before the panel closes
  const l1 = html.indexOf('unity-unicorn-L1-control-panel<'), toolset = html.indexOf('unity-unicorn-world-building-toolset<');
  assert.ok(l1 > 0 && toolset > l1 && toolset < html.indexOf('unity-unicorn-L2-control-panel<'), 'toolset inside L1');
  assert.ok(!html.includes('unity-unicorn-tools-tool-panel'), 'the old label is gone');
  assert.ok(html.includes('renderAssets();renderToolNames();renderToolChart();'));
  assert.ok(html.includes('schema:"unity-unicorn.send-tool-job/v1"'));
});

test('the page carries the same list, and the naming rule and open questions sit in the toolset', () => {
  const line = html.split('\n').find((l) => l.startsWith('const TOOL_CATEGORIES='));
  assert.deepEqual(JSON.parse(line.slice('const TOOL_CATEGORIES='.length).replace(/;$/, '')), cats);
  assert.ok(!html.includes('unity-unicorn-tool-names-tool-panel<'), 'the tool-names panel is folded into the toolset');
  const ts = html.indexOf('id="uu-p-tools"'), lib = html.indexOf('id="uu-p-library"');
  for (const id of ['uu-names-rule', 'uu-names-questions']) { const i = html.indexOf(`id="${id}"`); assert.ok(i > ts && i < lib, id + ' inside the toolset'); }
});

// Wonderland Launchpad's standard panels (Astra, 2026-10-24: "where are all of the other panels that Wonderland
// Launchpad has"), dotted because they are standard; the old desk and tool library are dark tool panels now.
test('the standard panels are there, in Wonderland Launchpad order, with dotted borders', () => {
  const order = ['unity-stable-editorial-status-panel', 'unity-unicorn-cyberhome-panel', 'unity-stable-quick-summary-panel', 'unity-unicorn-system-map-panel',
    'unity-unicorn-intelligence-levels-panel', 'unity-unicorn-L1-control-panel', 'unity-unicorn-world-building-toolset', 'unity-unicorn-tool-library',
    'unity-unicorn-matcap-tool-panel', 'unity-unicorn-world-assets-tool-panel', 'unity-unicorn-worldbuilding-desk-tool-panel',
    'unity-unicorn-L2-control-panel', 'unity-unicorn-L3-control-panel', 'unity-unicorn-L4-control-panel', 'approvals-waiting-on-you', 'grants', 'keys',
    'unity-unicorn-required-maintenance', 'pet-id', 'unity-unicorn-rwx-panel', 'unity-unicorn-session-notes-panel', 'unity-unicorn-emotions-panel',
    'unity-unicorn-clicker-training-panel', 'unity-unicorn-friends-list-panel', 'unity-unicorn-unified-footer-panel'];
  for (const l of order) assert.equal(html.split(l + '<').length - html.split(l + '</a>').length, 1, l + ' once (links to it aside)');
  let at = -1;
  for (const l of order) { const i = html.search(new RegExp(l + '<(?!/a>)')); assert.ok(i > at, l + ' in order'); at = i; }
  // tools are never outside a control panel (Astra, 2026-10-24): every tool panel sits between L1 and L2
  const l1 = html.indexOf('unity-unicorn-L1-control-panel<'), l2 = html.indexOf('unity-unicorn-L2-control-panel<');
  for (const m of html.matchAll(/data-item="(unity-unicorn-[a-z0-9-]+-tool-panel)"/g)) assert.ok(m.index > l1 && m.index < l2, m[1] + ' inside L1');
  assert.ok(html.includes('details.panel:not(.editorial){border-style:dotted'));
  assert.ok(!html.includes('<p class="eyebrow">Worldbuilding desk'), 'the old white desk header is gone');
});

// Astra, 2026-10-24: L1's tools and toolsets follow the tool rule; the Avatar | World tabs move inside the toolset as a
// filter; Added tonight is the high-level tool unity-unicorn-tool-maker.
test('L1 tools are rounded tools, the tabs are a toolset filter, and the tool maker is a tool', () => {
  for (const id of ['uu-p-tools', 'uu-p-tool-maker', 'uu-p-library', 'uu-matcap', 'uu-menumap', 'uu-p-avatar-assets', 'uu-vine', 'uu-p-sparkles', 'uu-p-world-assets', 'uu-p-desk'])
    assert.match(html, new RegExp(`<details class="uu-panel[^"]* uu-tool"(?: open)? id="${id}">`), id + ' is a tool');
  assert.ok(html.includes('.uu details.uu-panel.uu-tool{border-radius:16px;background:var(--tool-bg)'));
  assert.ok(!html.includes('id="uu-sides"') && !html.includes('role="tabpanel"'), 'no tabs');
  const ts = html.indexOf('id="uu-p-tools"'), f = html.indexOf('id="uu-tc-filter"'), lib = html.indexOf('id="uu-p-library"');
  assert.ok(f > ts && f < lib, 'the filter sits inside the toolset');
  const maker = html.indexOf('unity-unicorn-tool-maker<'), add = html.indexOf('id="uu-add"');
  assert.ok(maker > ts && maker < lib && add > maker && add < lib, 'the add form is the tool maker, right under the toolset');
});
