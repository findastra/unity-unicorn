// Panels, not modules (Astra, 2026-10-19): a bordered box on the page is a panel; a module is a file of code. And tool
// panels since the same day (Astra: "I do want all of it to say tool panel instead of just panel").
// Checks the page's labels and names, and that lights saved on the old -module and -panel labels carry over.
// Claude Opus 5.5 (claude-opus-5-5), 2026-10-19.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const html = readFileSync(new URL('../unity-unicorn-20261008.html', import.meta.url), 'utf8');
// Cut by plain string search: this reads one known page, it does not filter HTML.
const between = (text, open, close, from = 0) => {
  const start = text.indexOf(open, from);
  const end = start < 0 ? -1 : text.indexOf(close, start + open.length);
  return end < 0 ? '' : text.slice(start, end + close.length);
};
const section = between(html, '<section id="uu-panels"', '</section>');
const script = between(section, '<script>', '</script>').slice('<script>'.length, -'</script>'.length);
const KEY = 'astra-pet-desk:unity-unicorn:modules:1';
const LEVELS = ['L1', 'L2', 'L3', 'L4'];
// Every label on a box of the page, from its lights: <span class="uu-lights" data-item="label"></span>label
const LABELS = [...section.matchAll(/<span class="uu-lights" data-item="([^"]+)"><\/span>([^<]+)</g)].map((m) => [m[1], m[2]]);

test('the four level panels are labelled unity-unicorn-L1-tool-panel to -L4-tool-panel', () => {
  assert.ok(section, 'the page has a uu-panels section');
  for (const l of LEVELS) {
    assert.ok(section.includes(`data-item="unity-unicorn-${l}-tool-panel"></span>unity-unicorn-${l}-tool-panel<`), `${l} tool panel label`);
  }
  assert.ok((section.match(/<details class="uu-panel"/g) || []).length >= 8, 'every box is a details.uu-panel');
});

test('every box label that ends in panel says tool-panel, and its light has the same name', () => {
  const panels = LABELS.filter(([item]) => item.endsWith('panel'));
  assert.ok(panels.length >= 16, 'the four level panels and the tool panels on the two sides');
  for (const [item, text] of panels) {
    assert.match(item, /^unity-unicorn-[A-Za-z0-9-]+-tool-panel$/, item);
    assert.equal(text, item, 'the label and its light have one name');
  }
  assert.doesNotMatch(section, />Add to the L1 panel<|Click a panel to open it/);
});

test('no page box is called a module', () => {
  assert.doesNotMatch(html, /uu-modules|uu-mod\b/);
  assert.doesNotMatch(html, /pet program modules|L1 module/);
  // Only the comment that explains the carry-over may name the old labels.
  const code = section.split('\n').filter((line) => !line.trim().startsWith('//')).join('\n');
  assert.doesNotMatch(code, /unity-unicorn-L[1-4]-module/);
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

function boot(saved) {
  const storage = new Map(saved ? [[KEY, JSON.stringify(saved)]] : []);
  const nodes = new Map([...section.matchAll(/\bid="([^"]+)"/g)].map((m) => [m[1], new Element('div')]));
  const spans = [...section.matchAll(/<span class="uu-lights" data-item="([^"]+)"><\/span>/g)].map((m) => {
    const s = new Element('span'); s.dataset.item = m[1]; return s;
  });
  const document = {
    getElementById: (id) => { assert.ok(nodes.has(id), 'Missing DOM element ' + id); return nodes.get(id); },
    querySelectorAll: (sel) => { assert.equal(sel, '#uu-panels span.uu-lights[data-item]'); return spans; },
    createElement: (tag) => new Element(tag),
    createTextNode: (text) => text,
  };
  const localStorage = { getItem: (k) => storage.get(k) ?? null, setItem: (k, v) => storage.set(k, v) };
  const sandbox = { document, localStorage, setInterval: () => 0, setTimeout: () => 0, confirm: () => true, alert: () => {}, URL, Blob };
  vm.createContext(sandbox); vm.runInContext(script, sandbox);
  const lightsFor = (item) => spans.find((s) => s.dataset.item === item)?.replacedBy;
  return { stored: () => JSON.parse(storage.get(KEY)), lightsFor };
}

test('lights saved on the old -module labels carry over to the tool panel labels', () => {
  const app = boot({ lights: { 'unity-unicorn-L2-module': { on: false, fix: true }, 'unity-unicorn-cyberhome': { on: true, fix: true } }, added: [], lastOpened: null });
  const lights = app.stored().lights;
  assert.deepEqual(lights['unity-unicorn-L2-tool-panel'], { on: false, fix: true });
  assert.equal(lights['unity-unicorn-L2-module'], undefined);
  assert.deepEqual(lights['unity-unicorn-cyberhome'], { on: true, fix: true });
  const [power, care] = app.lightsFor('unity-unicorn-L2-tool-panel').children;
  assert.equal(power.attrs['aria-pressed'], 'false');
  assert.equal(care.attrs['aria-pressed'], 'true');
  assert.equal(power.title, 'unity-unicorn-L2-tool-panel: off');
});

test('lights saved on the old -panel labels carry over to the tool panel labels, on every box', () => {
  const old = Object.fromEntries(LABELS.filter(([item]) => item.endsWith('-tool-panel'))
    .map(([item]) => [item.replace(/-tool-panel$/, '-panel'), { on: false, fix: true }]));
  const app = boot({ lights: old, added: [], lastOpened: null });
  const lights = app.stored().lights;
  for (const [o] of Object.entries(old)) {
    const n = o.replace(/-panel$/, '-tool-panel');
    assert.deepEqual(lights[n], { on: false, fix: true }, n);
    assert.equal(lights[o], undefined, o);
    const [power, care] = app.lightsFor(n).children;
    assert.equal(power.attrs['aria-pressed'], 'false', n);
    assert.equal(care.attrs['aria-pressed'], 'true', n);
  }
});

test('a light already saved on a tool panel label is not overwritten by an older one', () => {
  const app = boot({ lights: {
    'unity-unicorn-L1-module': { on: false, fix: false }, 'unity-unicorn-L1-panel': { on: false, fix: true }, 'unity-unicorn-L1-tool-panel': { on: true, fix: true },
    'unity-unicorn-L3-module': { on: false, fix: false }, 'unity-unicorn-L3-panel': { on: true, fix: true },
  }, added: [], lastOpened: null });
  const lights = app.stored().lights;
  assert.deepEqual(lights['unity-unicorn-L1-tool-panel'], { on: true, fix: true });
  assert.deepEqual(lights['unity-unicorn-L3-tool-panel'], { on: true, fix: true }, 'the -panel light is newer than the -module one');
  for (const o of ['L1-module', 'L1-panel', 'L3-module', 'L3-panel']) assert.equal(lights['unity-unicorn-' + o], undefined, o);
});

test('a first visit starts with every tool panel light on and working', () => {
  const app = boot();
  for (const [item] of LABELS.filter(([i]) => i.endsWith('-tool-panel'))) {
    const [power, care] = app.lightsFor(item).children;
    assert.equal(power.attrs['aria-pressed'], 'true', item);
    assert.equal(care.attrs['aria-pressed'], 'false', item);
  }
});
