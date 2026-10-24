// Who made each tool (Astra, 2026-10-19: "we need to distinguish whether we made the tools or somebody else made the
// tools"): every tool the page lists has one row in docs/made-by-20261019.json, and the page carries the same rows.
// Claude Opus 5.5 (claude-opus-5-5), 2026-10-19.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const html = readFileSync(new URL('../unity-unicorn-20261008.html', import.meta.url), 'utf8').replace(/\r\n/g, '\n');
const made = JSON.parse(readFileSync(new URL('../docs/made-by-20261019.json', import.meta.url), 'utf8'));
const pet = JSON.parse(readFileSync(new URL('../pet.json', import.meta.url), 'utf8'));
const list = (name) => JSON.parse(new RegExp(`\\nconst ${name}=(\\[[\\s\\S]*?\\]);\\n`).exec(html)[1]);

test('every tool on the page has exactly one made-by row', () => {
  const ids = made.ours.map((o) => o.id);
  assert.equal(new Set(ids).size, ids.length, 'one row per tool');
  const page = [...list('TOOLS_MAIN').map((t) => t[0]), ...list('UNMERGED').map((t) => t[0]), ...list('IN_WORLDS').map((t) => t[0])];
  assert.deepEqual([...new Set(page)].sort(), [...ids].sort());
});

test('each row says what it is for and who made it, and agrees with pet.json sides', () => {
  const sides = (pet.intelligence_levels || pet).L1.sides;
  for (const o of made.ours) {
    assert.ok(['avatar', 'world'].includes(o.side), o.id);
    assert.ok(o.made_with, o.id + ' made_with (or "not recorded")');
    if (sides.avatar.includes(o.id)) assert.equal(o.side, 'avatar', o.id);
    if (sides.world.includes(o.id)) assert.equal(o.side, 'world', o.id);
  }
  for (const o of made.others) assert.ok(['avatar', 'world', 'both'].includes(o.side) && o.what, o.name);
});

// Since 2026-10-24 the made-by panel is gone: who made each tool opens first in that tool's card in the world-building
// toolset, and what each program made by others does opens in its 3rd party pill (Astra: "this information to go inside
// the individual tool dropdown cards in an obvious way, and get rid of this panel after").
test('the page carries the same rows, in each tool card and 3rd party pill, with no made-by panel', () => {
  const line = html.split('\n').find((l) => l.startsWith('const MADE_BY='));
  assert.deepEqual(JSON.parse(line.slice('const MADE_BY='.length).replace(/;$/, '')), made);
  assert.ok(!html.includes('unity-unicorn-made-by-tool-panel<'), 'no made-by panel');
  assert.ok(html.includes('const m=made[old],box=el("div","uu-tc-made")') && html.includes('inf.prepend(box)'), 'made-by first in each tool card');
  assert.ok(html.includes('"Made by others: "'), 'made by others in the 3rd party pills');
});
