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

test('L1 shows 3rd party and native programs as pills with a count, like Wonderland Launchpad', () => {
  assert.ok(html.includes('3rd party programs · <span id="uu-n-third"></span></h4><ul class="uu-pills" id="uu-third">'));
  assert.ok(html.includes('Native programs · <span id="uu-n-programs"></span></h4><ul class="uu-pills" id="uu-programs">'));
  assert.ok(html.includes('THIRD.forEach(([n,d])=>{const o=otherOf(n);pill("uu-third",n,'));
});

test('the world-building toolset, inside the L1 tool panel, draws every tool with its colours and a send button', () => {
  assert.ok(html.includes('data-item="unity-unicorn-world-building-toolset"></span>unity-unicorn-world-building-toolset<'));
  // native tools are L1, so the toolset sits inside the L1 tool panel, before the panel closes
  const l1 = html.indexOf('unity-unicorn-L1-tool-panel<'), toolset = html.indexOf('unity-unicorn-world-building-toolset<');
  assert.ok(l1 > 0 && toolset > l1 && toolset < html.indexOf('id="uu-add"', l1), 'toolset inside L1');
  assert.ok(!html.includes('unity-unicorn-tools-tool-panel'), 'the old label is gone');
  assert.ok(html.includes('renderAssets();renderToolNames();renderToolChart();'));
  assert.ok(html.includes('schema:"unity-unicorn.send-tool-job/v1"'));
});

test('the page shows the same list in its tool-names tool panel, and a category on every tool row', () => {
  const line = html.split('\n').find((l) => l.startsWith('const TOOL_CATEGORIES='));
  assert.deepEqual(JSON.parse(line.slice('const TOOL_CATEGORIES='.length).replace(/;$/, '')), cats);
  assert.ok(html.includes('data-item="unity-unicorn-tool-names-tool-panel"></span>unity-unicorn-tool-names-tool-panel<'));
  assert.ok(html.includes('item(l,n,d,cat(n)+"on main",true)'));
  assert.ok(html.includes('item(l,n,d,cat(n)+"not merged · "'));
  assert.ok(html.includes('item(l,n,d,cat(n)+"in "+WORLD'));
});
