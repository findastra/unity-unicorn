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

test('the page shows the same list in its tool-names tool panel, and a category on every tool row', () => {
  const line = html.split('\n').find((l) => l.startsWith('const TOOL_CATEGORIES='));
  assert.deepEqual(JSON.parse(line.slice('const TOOL_CATEGORIES='.length).replace(/;$/, '')), cats);
  assert.ok(html.includes('data-item="unity-unicorn-tool-names-tool-panel"></span>unity-unicorn-tool-names-tool-panel<'));
  assert.ok(html.includes('item(l,n,d,cat(n)+"on main",true)'));
  assert.ok(html.includes('item(l,n,d,cat(n)+"not merged · "'));
  assert.ok(html.includes('item(l,n,d,cat(n)+"in "+WORLD'));
});
