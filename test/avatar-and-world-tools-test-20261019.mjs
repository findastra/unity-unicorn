// Avatar tools and world tools on two sides of the page, the world sparkle tools panel, and the vine panel's job file
// (Astra, 2026-10-19). Claude Opus 5.5 (claude-opus-5-5), 2026-10-19.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const read = (p) => readFileSync(new URL('../' + p, import.meta.url), 'utf8');
const html = read('unity-unicorn-20261008.html');
const pet = JSON.parse(read('pet.json'));
const L1 = pet.intelligence_levels.L1;
const sparkleDoc = JSON.parse(read('docs/world-sparkle-tools-20261019.json'));
const cs = read('scripts/unity-editor/vine-spawn-with-trumpet-flowers-20261019.cs');
const sectionStart = html.indexOf('<section id="uu-panels"');
const section = html.slice(sectionStart, html.indexOf('</section>', sectionStart));
const block = (id) => {
  const start = html.indexOf(`<script id="${id}">`);
  assert.ok(start > 0, id);
  return html.slice(start + `<script id="${id}">`.length, html.indexOf('</script>', start));
};
// One `const NAME=[...];` list from the page's main script (they are JSON).
const list = (name) => {
  const m = new RegExp(`\\nconst ${name}=(\\[[\\s\\S]*?\\]);\\n`).exec(html);
  assert.ok(m, name);
  return JSON.parse(m[1]);
};
const TOOLS_MAIN = list('TOOLS_MAIN'), UNMERGED = list('UNMERGED'), WORLDS = list('WORLDS'), IN_WORLDS = list('IN_WORLDS'), SPARKLES = list('SPARKLES');
const SIDE = (panel) => (/^(avatar-|own-avatar$)/.test(panel) ? 'avatar' : 'world');

test('every tool on main and every tool not merged is on exactly one side, as pet.json says', () => {
  const unmerged = Object.values(L1.tools_not_merged).flat();
  const all = [...L1.tools, ...unmerged];
  assert.deepEqual([...L1.sides.avatar, ...L1.sides.world].sort(), [...all].sort());
  assert.equal(new Set([...L1.sides.avatar, ...L1.sides.world]).size, all.length, 'no tool on both sides');
  for (const [n, , , panel] of TOOLS_MAIN) assert.ok(L1.sides[SIDE(panel)].includes(n), n + ' side');
  for (const [n, , , , panel] of UNMERGED) assert.ok(L1.sides[SIDE(panel)].includes(n), n + ' side');
  assert.deepEqual(UNMERGED.map((r) => r[0]).sort(), [...unmerged].sort());
});

test('the tools that still live in the worlds are in pet.json, by world, and never clash with a Unity Unicorn tool', () => {
  for (const [id, , repo] of WORLDS) {
    assert.deepEqual(L1.tools_in_worlds[id].tools, IN_WORLDS.filter((t) => t[1] === id).map((t) => t[0]), id);
    assert.equal(L1.tools_in_worlds[id].repo, repo);
  }
  const ids = IN_WORLDS.map((t) => t[0]);
  assert.equal(new Set(ids).size, ids.length);
  const ours = new Set([...L1.tools, ...Object.values(L1.tools_not_merged).flat()]);
  assert.deepEqual(ids.filter((i) => ours.has(i)), []);
  for (const [, w, d, href] of IN_WORLDS) {
    assert.ok(WORLDS.some(([id]) => id === w), w);
    assert.match(href, /^https:\/\/github\.com\/findastra\/(liminal-doors|astras-infinite-pole|piranesi-house)\/(blob|tree)\//);
    assert.ok(d.length > 20 && d.length < 200, d);
  }
});

test('the world sparkle panel shows exactly docs/world-sparkle-tools-20261019.json', () => {
  assert.equal(sparkleDoc.schema, 'unity-unicorn.world-sparkles/v1');
  assert.deepEqual(SPARKLES.map(([id, world, name, what, source]) => ({ id, world, name, what, source })), sparkleDoc.sparkles);
  const ids = SPARKLES.map((s) => s[0]);
  assert.equal(new Set(ids).size, ids.length, 'sparkle ids are unique');
  for (const w of ['liminal-doors', 'astras-infinite-pole', 'piranesi-house']) assert.ok(SPARKLES.some((s) => s[1] === w), 'sparkles from ' + w);
  for (const s of SPARKLES) assert.match(s[4], /^https:\/\/github\.com\/findastra\/[a-z0-9-]+\/blob\//, s[0]);
  // a sparkle tool from the Liminal Doors ledger is in the sparkle panel, not also in the Liminal Doors panel
  assert.deepEqual(IN_WORLDS.map((t) => t[0]).filter((i) => ids.includes(i)), []);
});

class Element {
  constructor(tag) { this.tag = tag; this.children = []; this.dataset = {}; this.attrs = {}; this.textContent = ''; this.value = ''; this.files = []; }
  append(...items) { this.children.push(...items); }
  replaceChildren(...items) { this.children = [...items]; }
  replaceWith(other) { this.replacedBy = other; }
  setAttribute(k, v) { this.attrs[k] = String(v); }
  reset() {}
  set innerHTML(_) { throw new Error('Page text must never be rendered as HTML.'); }
}

test('the page puts every row in its own panel, once, on the right side', () => {
  const script = (() => { const s = section.indexOf('<script>'); return section.slice(s + 8, section.indexOf('</script>', s)); })();
  const nodes = new Map([...section.matchAll(/\bid="([^"]+)"/g)].map((m) => [m[1], new Element('div')]));
  const document = {
    getElementById: (id) => { assert.ok(nodes.has(id), 'Missing DOM element ' + id); return nodes.get(id); },
    querySelectorAll: () => [],
    createElement: (tag) => new Element(tag),
    createTextNode: (text) => text,
  };
  const sandbox = { document, localStorage: { getItem: () => null, setItem: () => {} }, setInterval: () => 0, setTimeout: () => 0, confirm: () => true, alert: () => {}, URL, Blob };
  vm.createContext(sandbox); vm.runInContext(script, sandbox);
  const rows = new Map();
  const toolList = (id) => /^uu-(avatar-(build|sparkles|effects|records)|every-world|world-|sparkle-)/.test(id);
  for (const [id, node] of nodes) if (toolList(id)) for (const li of node.children) if (li.tag === 'li') {
    const name = li.children.find((c) => c.className === 'uu-name');
    if (name) { assert.ok(!rows.has(name.textContent), name.textContent + ' is in two panels'); rows.set(name.textContent, id); }
  }
  const sideOfList = (id) => {
    const at = section.indexOf(`id="${id}"`);
    return at > section.indexOf('id="uu-side-world"') ? 'world' : at > section.indexOf('id="uu-side-avatar"') ? 'avatar' : 'neither';
  };
  for (const [n, , , panel] of TOOLS_MAIN) {
    if (panel.startsWith('own-')) { assert.ok(!rows.has(n), n + ' is its own panel, not a row'); continue; }
    assert.equal(sideOfList(rows.get(n)), SIDE(panel), n);
  }
  for (const [n, , , , panel] of UNMERGED) assert.equal(sideOfList(rows.get(n)), SIDE(panel), n);
  for (const [n, w] of IN_WORLDS) assert.equal(rows.get(n), 'uu-world-' + w, n);
  for (const [n, w] of SPARKLES) assert.equal(rows.get(n), 'uu-sparkle-' + w, n);
  // the tools that are their own panel sit on their side
  const at = (s) => section.indexOf(s);
  for (const [label, side] of [['unity-unicorn-matcap-tool-panel', 'avatar'], ['unity-unicorn-menu-map-tool-panel', 'avatar'], ['unity-unicorn-vine-spawn-tool-panel', 'world']]) {
    const p = at(`data-item="${label}"`);
    assert.ok(side === 'world' ? p > at('id="uu-side-world"') : p > at('id="uu-side-avatar"') && p < at('id="uu-side-world"'), label + ' on the ' + side + ' side');
  }
  assert.equal(nodes.get('uu-avatar-count').textContent, String(L1.sides.avatar.length));
  assert.equal(nodes.get('uu-world-count').textContent, String(L1.sides.world.length + IN_WORLDS.length));
  assert.match(nodes.get('uu-n-sparkles').textContent, new RegExp(`${SPARKLES.length} in 3 worlds`));
});

test('the two sides are tabs over plain divs, so the panels section is still one section', () => {
  assert.equal((section.match(/<section/g) || []).length, 1, 'no section inside the panels section');
  assert.ok(section.includes('role="tablist"') && section.includes('id="uu-tab-avatar"') && section.includes('id="uu-tab-world"'));
  assert.ok(section.includes('<div class="uu-side" id="uu-side-avatar"') && section.includes('<div class="uu-side" id="uu-side-world"'));
});

const vine = vm.createContext({});
vm.runInContext(block('uu-vine-model') + ';this.VINE = VINE;', vine);
const { VINE } = vine;

test('the vine panel uses the Unity tool\'s palette, worlds and defaults', () => {
  const csPalette = [...cs.matchAll(/\{ "(#[0-9a-f]{6})", "([^"]+)" \}/g)].map((m) => [m[1], m[2]]);
  assert.equal(csPalette.length, 30);
  assert.deepEqual(JSON.parse(JSON.stringify(VINE.PALETTE)), csPalette);
  assert.equal(new Set(csPalette.map((p) => p[0])).size, 30, 'no colour twice');
  assert.deepEqual([...VINE.WORLDS.map((w) => w[0])], ['piranesi-house', 'astras-infinite-pole', 'liminal-doors']);
  assert.ok(cs.includes('Ids = { "piranesi-house", "astras-infinite-pole", "liminal-doors" }'));
  assert.ok(cs.includes('public int vinesPerObject = 6'));
  assert.ok(cs.includes('public float lengthM = 3.5f, flowersPerM = 3f, leavesPerM = 12f, glow = 0.4f, leafGlow = 0f, flowerSize = 1f;'));
  assert.ok(cs.includes('public Color leafColor = new Color(0.20f, 0.42f, 0.18f);') && VINE.DEFAULTS.leaf_color === '#336b2e');
  assert.deepEqual([VINE.DEFAULTS.vines, VINE.DEFAULTS.length_m, VINE.DEFAULTS.flowers_per_m, VINE.DEFAULTS.leaves_per_m, VINE.DEFAULTS.glow, VINE.DEFAULTS.leaf_glow, VINE.DEFAULTS.flower_size], [6, 3.5, 3, 12, 0.4, 0, 1]);
  // the shader's own defaults are the same; Leaf glow 0 keeps the look of vines grown before a material had one
  assert.ok(cs.includes('_Glow (""Flower glow"", Range(0,3)) = 0.4') && cs.includes('_LeafGlow (""Leaf glow"", Range(0,1)) = 0\n'));
  assert.equal(VINE.MAX, 10);
  assert.ok(cs.includes('MaxColours = 10'));
});

test('colour squares: up to 10, a second click takes one out, nothing that is not a colour', () => {
  let l = [];
  for (const [hex] of VINE.PALETTE) l = VINE.toggle(l, hex);
  assert.equal(l.length, 10);
  assert.deepEqual(VINE.toggle(l, '#ff5a1f').length, 9);
  assert.deepEqual([...VINE.toggle([], 'red')], []);
  assert.deepEqual([...VINE.toggle([], '#FF3CC7')], ['#ff3cc7']);
});

test('the job file is what the Unity tool reads, with its limits', () => {
  const j = VINE.job('liminal-doors', ['#ff3cc7', '#2ef2ff'], { vines: 99, length_m: -4, flowers_per_m: 0, glow: 0, grow: 'sideways', seed: 1.5, leaf_color: 'green' }, new Date('2026-10-20T02:56:10.123Z'));
  assert.deepEqual(JSON.parse(JSON.stringify(j)), {
    schema: 'unity-unicorn.vine-spawn-job/v1', tool_id: 'vine-spawn-with-trumpet-flowers', world: 'liminal-doors', made_at: '2026-10-20T02:56:10Z',
    flower_colors: ['#ff3cc7', '#2ef2ff'],
    settings: { vines: 24, length_m: 0.5, flowers_per_m: 0, flower_size: 1, leaves_per_m: 12, glow: 0, leaf_glow: 0, grow: 'climb', seed: 20261019, leaf_color: '#336b2e' },
  });
  assert.equal(VINE.fileName(j), 'unity-unicorn-vine-spawn-liminal-doors-20261020-025610.json');
  assert.ok(cs.includes('GetFiles("unity-unicorn-vine-spawn-*.json")'), 'the Unity tool looks for the same names');
  for (const key of Object.keys(j.settings)) assert.ok(cs.includes(key), key + ' is read by the Unity tool');
  assert.ok(cs.includes('Schema = "unity-unicorn.vine-spawn-job/v1", ToolId = "vine-spawn-with-trumpet-flowers"'));
  assert.throws(() => VINE.job('the-moon', ['#ff3cc7']), /Pick a world/);
  assert.throws(() => VINE.job('piranesi-house', []), /1 to 10/);
  assert.throws(() => VINE.job('piranesi-house', VINE.PALETTE.slice(0, 11).map((p) => p[0])), /1 to 10/);
  assert.throws(() => VINE.job('piranesi-house', ['#ff3cc']), /#ff3cc7/);
});

test('every setting has the same limits on the page, in the Unity job reader and in the Unity window', () => {
  const num = (v) => String(v).replace('.', '\\.');
  const sliders = { vines: 'Vines per object', length_m: 'Vine length (m)', flowers_per_m: 'Flowers per metre', flower_size: 'Flower size', leaves_per_m: 'Leaves per metre', glow: 'Flower glow', leaf_glow: 'Leaf glow' };
  assert.deepEqual(Object.keys(VINE.LIMITS).sort(), Object.keys(sliders).sort());
  for (const [k, [lo, hi, def, , label]] of Object.entries(VINE.LIMITS)) {
    assert.equal(label, sliders[k], k + ' label');
    assert.equal(def, VINE.DEFAULTS[k], k + ' default');
    assert.match(cs, new RegExp(`Mathf\\.Clamp\\(j\\.${k}, ${num(lo)}f?, ${num(hi)}f?\\)`), k + ': the job reader clamps to ' + lo + ' to ' + hi);
    // EditorGUILayout.IntSlider("Vines per object", s.vinesPerObject, 1, 24) or .Slider(new GUIContent("Leaf glow", "tip"), s.leafGlow, 0f, 1f)
    const name = label.replace(/[()]/g, '\\$&');
    assert.match(cs, new RegExp(`Slider\\((new GUIContent\\()?"${name}"(, "[^"]*"\\))?, s\\.\\w+, ${num(lo)}f?, ${num(hi)}f?\\)`), k + ': the window slider runs ' + lo + ' to ' + hi);
  }
});

test('Leaf glow is optional in a job: an older job without it gets the default, in the page and in Unity', () => {
  const settings = { vines: 6, length_m: 3.5, flowers_per_m: 3, flower_size: 1, leaves_per_m: 12, glow: 1.5, grow: 'climb', seed: 7, leaf_color: '#8fe388' };
  assert.equal(VINE.job('astras-infinite-pole', ['#ff3cc7'], settings).settings.leaf_glow, 0, 'no Leaf glow: the look vines had before');
  assert.equal(VINE.job('astras-infinite-pole', ['#ff3cc7'], { ...settings, leaf_glow: 0.4 }).settings.leaf_glow, 0.4);
  assert.equal(VINE.job('astras-infinite-pole', ['#ff3cc7'], { ...settings, leaf_glow: 7 }).settings.leaf_glow, 1);
  assert.equal(VINE.job('astras-infinite-pole', ['#ff3cc7'], { ...settings, leaf_glow: 'bright' }).settings.leaf_glow, 0);
  // Unity marks a job that does not name leaf_glow with -1 and keeps its default for it (JsonUtility reads a missing number as 0)
  assert.ok(cs.includes('public float leaf_glow = -1f;'));
  assert.match(cs, /if \(job\.settings != null && !Regex\.IsMatch\(text, ".*leaf_glow.*"\)\) job\.settings\.leaf_glow = -1f;/);
  assert.ok(cs.includes('if (j.leaf_glow >= 0) s.leafGlow = Mathf.Clamp(j.leaf_glow, 0f, 1f);'));
  assert.ok(cs.includes('Schema = "unity-unicorn.vine-spawn-job/v1"'), 'the schema stays v1: old jobs still read');
});

// The vine panel's app in a pretend page, with what this browser saved; returns its elements by id.
const vineApp = (saved) => {
  const els = new Map();
  const make = (tag) => { const e = new Element(tag); e.style = {}; return e; };
  const document = { getElementById: (id) => { if (!els.has(id)) els.set(id, make('div')); return els.get(id); }, createElement: make };
  const store = { 'astra-pet-desk:unity-unicorn:vines:1': JSON.stringify({ colours: ['#ff3cc7'], world: 'astras-infinite-pole', ...saved }) };
  const ctx = vm.createContext({ document, localStorage: { getItem: (k) => store[k] ?? null, setItem: (k, v) => { store[k] = v; } }, Math, JSON, Number, Object, String, Array });
  vm.runInContext(block('uu-vine-model') + ';' + block('uu-vine-app'), ctx);
  const slider = (id) => Number(els.get('vn-options').children.find((c) => c.id === id).value);
  return { els, glows: () => ({ glow: slider('vn-glow'), leaf: slider('vn-leaf_glow') }), saved: () => JSON.parse(store['astra-pet-desk:unity-unicorn:vines:1']).settings };
};

test('the vine panel keeps a Flower glow chosen on purpose, and moves an old saved default (0.2) to the new one', () => {
  assert.deepEqual(vineApp({ settings: { glow: 0.2 } }).glows(), { glow: 0.4, leaf: 0 }, 'saved before Leaf glow, still on the old default');
  assert.deepEqual(vineApp({ settings: { glow: 1.5 } }).glows(), { glow: 1.5, leaf: 0 }, 'a glow chosen on purpose stays');
  assert.deepEqual(vineApp({ settings: { glow: 0.2, leaf_glow: 0 } }).glows(), { glow: 0.2, leaf: 0 }, 'saved after Leaf glow existed: kept as it is');
});

test('a world lit mostly by glowing materials (the Pole) is offered its glows, on the page and in Unity; worlds with a sun are not', () => {
  assert.deepEqual(JSON.parse(JSON.stringify(VINE.GLOW_WORLDS)), { 'astras-infinite-pole': { glow: 1.5, leaf_glow: 0.4 } });
  assert.ok(cs.includes('LitByGlow = { "astras-infinite-pole" };') && cs.includes('LitByGlowFlowerGlow = 1.5f, LitByGlowLeafGlow = 0.4f;'), 'the Unity window offers the same');
  const pole = vineApp({ world: 'astras-infinite-pole', settings: {} });
  assert.equal(pole.els.get('vn-hint').hidden, false, 'shown for the Pole while Leaf glow is 0');
  assert.match(pole.els.get('vn-hint-text').textContent, /leaves read black there/);
  pole.els.get('vn-hint-use').onclick();
  assert.deepEqual(pole.glows(), { glow: 1.5, leaf: 0.4 }, 'one click sets both glows');
  assert.equal(pole.saved().leaf_glow, 0.4, 'and they are kept');
  assert.equal(pole.els.get('vn-hint').hidden, true, 'then the hint goes away');
  for (const world of ['liminal-doors', 'piranesi-house']) assert.equal(vineApp({ world, settings: {} }).els.get('vn-hint').hidden, true, world + ': no hint');
});

test('the new scripts write text only, never HTML', () => {
  for (const id of ['uu-vine-model', 'uu-vine-app', 'uu-sides-app']) {
    assert.doesNotMatch(block(id), /innerHTML|outerHTML|insertAdjacentHTML|document\.write|\beval\(|new Function/, id);
  }
  assert.doesNotMatch(block('uu-vine-app'), /localStorage\.setItem\((?!KEY)/, 'only its own storage key');
});

test('the vine tool is registered: pet.json, the catalog file and the tool library card', () => {
  assert.ok(L1.tools.includes('vine-spawn-with-trumpet-flowers'));
  const card = JSON.parse(read('tools/vine-spawn-with-trumpet-flowers-20261019.json')).tools[0];
  assert.equal(card.id, 'vine-spawn-with-trumpet-flowers');
  assert.equal(card.source_path, 'scripts/unity-editor/vine-spawn-with-trumpet-flowers-20261019.cs');
  assert.ok(html.includes(`"description": ${JSON.stringify(card.description)}`), 'the tool library card has the same summary');
  assert.ok(cs.includes('[MenuItem("Tools/Unity Unicorn/vine-spawn-with-trumpet-flowers"'));
  assert.doesNotMatch(cs + read('test/unity/vine-spawn-smoke-test-20261019.cs'), /[A-Z]:\\\\Users|\/Users\/|audra/i, 'no machine paths');
});
