// The matcap palette panel and its PNG maker (Claude Opus 5.5, 2026-10-19).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { inflateSync } from 'node:zlib';
import { loadRecipes, encodePng, renderMatcap } from '../scripts/matcap-palette/make-matcaps-20261019.mjs';

const html = readFileSync(new URL('../unity-unicorn-20261008.html', import.meta.url), 'utf8');
const { MC_RECIPES, MC, MC_FAMILIES } = loadRecipes();
const sectionStart = html.indexOf('<section id="uu-panels"');
const section = html.slice(sectionStart, html.indexOf('</section>', sectionStart));

test('the matcap panel is a labelled uu-panel inside the panels section', () => {
  assert.ok(section.includes('data-item="unity-unicorn-matcap-tool-panel"></span>unity-unicorn-matcap-tool-panel<'));
  for (const id of ['mc-sphere', 'mc-ring', 'mc-pans', 'mc-filters', 'mc-search', 'mc-files', 'mc-save', 'mc-fav']) {
    assert.ok(section.includes(`id="${id}"`), id);
  }
});

test('48 recipes with unique, file-safe ids in known families', () => {
  assert.ok(MC_RECIPES.length >= 48, 'at least 48 matcaps');
  const ids = MC_RECIPES.map((r) => r.id);
  assert.equal(new Set(ids).size, ids.length, 'ids are unique');
  for (const r of MC_RECIPES) {
    assert.match(r.id, /^[a-z0-9-]+$/);
    assert.ok(MC_FAMILIES.includes(r.family), r.id + ' family');
    assert.ok(r.name && r.name.length < 30, r.id + ' name');
  }
  for (const f of MC_FAMILIES) assert.ok(MC_RECIPES.some((r) => r.family === f), f + ' has matcaps');
});

test('every recipe shades inside the circle with colours from 0 to 1, and nothing outside', () => {
  for (const r of MC_RECIPES) {
    assert.equal(MC.shade(r, 0.9, 0.9), null, r.id + ' outside');
    for (const [x, y] of [[0, 0], [0.5, 0.5], [-0.7, 0.2], [0.1, -0.95], [0.99, 0]]) {
      const c = MC.shade(r, x, y);
      assert.equal(c.length, 3);
      for (const v of c) assert.ok(Number.isFinite(v) && v >= 0 && v <= 1, `${r.id} at ${x},${y}`);
    }
  }
});

test('the PNG maker writes a valid square PNG', () => {
  const size = 24, png = encodePng(renderMatcap(MC, MC_RECIPES[0], size), size);
  assert.deepEqual([...png.subarray(0, 8)], [137, 80, 78, 71, 13, 10, 26, 10]);
  assert.equal(png.readUInt32BE(16), size);
  assert.equal(png.readUInt32BE(20), size);
  const idatLen = png.readUInt32BE(33);
  assert.equal(inflateSync(png.subarray(41, 41 + idatLen)).length, (size * 3 + 1) * size);
});

test('the panel script writes text with textContent, never as HTML', () => {
  const start = html.indexOf('<script id="uu-matcap-app">');
  const code = html.slice(start, html.indexOf('</script>', start));
  assert.ok(code.length > 1000);
  assert.doesNotMatch(code, /innerHTML|outerHTML|insertAdjacentHTML|document\.write/);
});
