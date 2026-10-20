// pet.json intelligence_levels and the page's L1 and L2 lists name the same items, and no item is in two panels.
// vcc-package-manager and vrchat-avatar-upload are tricks to write, not tools (Astra, astras-pet-os PR #114).
// Claude Opus 5.5 (claude-opus-5-5), 2026-10-19.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const html = readFileSync(new URL('../unity-unicorn-20261008.html', import.meta.url), 'utf8');
const levels = JSON.parse(readFileSync(new URL('../pet.json', import.meta.url), 'utf8')).intelligence_levels;
// The names of one `const NAME=[[...]];` list on the page, read by plain string search.
const pageList = (name) => {
  const open = `\nconst ${name}=[`;
  const start = html.indexOf(open);
  assert.ok(start >= 0, `the page has ${name}`);
  const line = html.slice(start + 1, html.indexOf('\n', start + 1));
  return [...line.matchAll(/\["([a-z0-9-]+)",/g)].map((m) => m[1]);
};
const sorted = (list) => [...list].sort();

test('the L1 panel lists exactly the tools on main in pet.json', () => {
  assert.deepEqual(sorted(pageList('TOOLS_MAIN')), sorted(levels.L1.tools));
});

test('the L2 panel lists exactly the tricks and tricks to write in pet.json', () => {
  assert.deepEqual(sorted(pageList('TRICKS')), sorted([...levels.L2.tricks, ...levels.L2.tricks_to_write]));
});

test('no item is both a tool and a trick', () => {
  const tricks = new Set([...levels.L2.tricks, ...levels.L2.tricks_to_write]);
  assert.deepEqual(levels.L1.tools.filter((t) => tricks.has(t)), []);
});

test('vcc-package-manager and vrchat-avatar-upload are tricks to write, and their catalog cards say so', () => {
  for (const id of ['vcc-package-manager', 'vrchat-avatar-upload']) {
    assert.ok(levels.L2.tricks_to_write.includes(id), id + ' is a trick to write');
    const card = JSON.parse(readFileSync(new URL(`../tools/${id}-20261011.json`, import.meta.url), 'utf8')).tools[0];
    assert.equal(card.kind, 'Trick to write', id + ' catalog kind');
    assert.ok(html.includes(`"id": "${id}",\n    "name": "${id}-20261011.json",\n    "kind": "Trick to write",`), id + ' tool library card');
  }
});
