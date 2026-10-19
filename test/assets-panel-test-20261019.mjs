// The assets-we-created panel (Astra, 2026-10-19: "need an assets we created panel for each avatars and world asset
// types"). The page shows the same rows as docs/assets-we-created-20261019.json, by side and type, as text only.
// Claude Opus 5.5 (claude-opus-5-5), 2026-10-19.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const html = readFileSync(new URL('../unity-unicorn-20261008.html', import.meta.url), 'utf8');
const list = JSON.parse(readFileSync(new URL('../docs/assets-we-created-20261019.json', import.meta.url), 'utf8'));
// Cut by plain string search: this reads one known page, it does not filter HTML.
const between = (text, open, close) => {
  const start = text.indexOf(open);
  const end = start < 0 ? -1 : text.indexOf(close, start + open.length);
  return end < 0 ? '' : text.slice(start, end + close.length);
};
const section = between(html, '<section id="uu-panels"', '</section>');
const script = between(section, '<script>', '</script>').slice('<script>'.length, -'</script>'.length);

test('the panel is labelled unity-unicorn-assets-we-created, with an avatar side and a world side', () => {
  assert.ok(section.includes('data-item="unity-unicorn-assets-we-created"></span>unity-unicorn-assets-we-created<'));
  assert.ok(section.includes('id="uu-assets-avatar"') && section.includes('id="uu-assets-world"'));
});

test('the page carries exactly the rows of docs/assets-we-created-20261019.json', () => {
  const line = script.split('\n').find((l) => l.startsWith('const ASSETS_WE_CREATED='));
  assert.ok(line, 'the page defines ASSETS_WE_CREATED');
  assert.deepEqual(JSON.parse(line.slice('const ASSETS_WE_CREATED='.length).replace(/;$/, '')), list);
});

test('every asset has a known side and type, a licence, and says where it is used', () => {
  const ids = new Set();
  for (const a of list.assets) {
    assert.ok(!ids.has(a.id), 'one row per asset: ' + a.id); ids.add(a.id);
    assert.ok(['avatar', 'world'].includes(a.for), a.id + ' side');
    assert.ok(list.types[a.for].includes(a.type), a.id + ' type ' + a.type);
    assert.ok(a.license && a.where_used.length, a.id + ' licence and where used');
    for (const u of [a.preview, a.library]) assert.ok(u === '' || u.startsWith('https://'), a.id + ' links are https');
  }
  const booth = list.assets.find((a) => a.id === 'liminal-doors-phone-booth');
  assert.ok(booth.where_used.some((w) => w.startsWith('Liminal Doors')) && booth.where_used.some((w) => w.startsWith("Astra's Infinite Pole")));
});

class Element {
  constructor(tag) { this.tag = tag; this.children = []; this.dataset = {}; this.attrs = {}; this.textContent = ''; this.files = []; }
  append(...items) { this.children.push(...items); }
  replaceChildren(...items) { this.children = [...items]; }
  replaceWith(other) { this.replacedBy = other; }
  setAttribute(k, v) { this.attrs[k] = String(v); }
  remove() {}
  reset() {}
  set innerHTML(_) { throw new Error('Page text must never be rendered as HTML.'); }
}
const text = (e) => typeof e === 'string' ? e : [e.textContent, ...e.children.map(text)].join(' ');

test('the panel draws every asset under its side and type, and a type with none says so', () => {
  const nodes = new Map([...section.matchAll(/\bid="([^"]+)"/g)].map((m) => [m[1], new Element('div')]));
  const document = {
    getElementById: (id) => nodes.get(id), querySelectorAll: () => [],
    createElement: (tag) => new Element(tag), createTextNode: (t) => t,
  };
  const localStorage = { getItem: () => null, setItem: () => {} };
  vm.runInContext(script, vm.createContext({ document, localStorage, setInterval: () => 0, setTimeout: () => 0, confirm: () => true, alert: () => {}, URL, Blob }));
  const world = text(nodes.get('uu-assets-world')), avatar = text(nodes.get('uu-assets-avatar'));
  for (const a of list.assets) assert.ok((a.for === 'world' ? world : avatar).includes(a.name), a.name + ' is drawn');
  for (const t of list.types.avatar) assert.ok(avatar.includes(t), 'avatar type ' + t);
  if (!list.assets.some((a) => a.for === 'avatar')) assert.ok(avatar.includes('None yet.'));
});
