// The VR asset closet: its own page (built from Dev's VR Closet, XxdevilishxX/Vr-Asset-Closet) and the small tool panel
// on Unity Stable that counts what it saved and opens it (Astra, 2026-10-19: pet programs are control panels).
// The closet's data rules and the panel's counting are run for real. Claude Opus 5.5 (claude-opus-5-5), 2026-10-19.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import vm from 'node:vm';

const PAGE = 'vr-asset-closet-20261019.html';
const closet = readFileSync(new URL('../' + PAGE, import.meta.url), 'utf8');
const html = readFileSync(new URL('../unity-unicorn-20261008.html', import.meta.url), 'utf8');
const pet = JSON.parse(readFileSync(new URL('../pet.json', import.meta.url), 'utf8'));
const KEY = 'astra-pet-desk:unity-unicorn:closet:1';
// Cut by plain string search: this reads two known pages, it does not filter HTML.
const scriptIn = (text, id) => {
  const open = `<script id="${id}">`, a = text.indexOf(open);
  return a < 0 ? '' : text.slice(a + open.length, text.indexOf('</script>', a));
};
const plain = (v) => JSON.parse(JSON.stringify(v)); // values made in a vm context have its own Array and Object
const core = () => { const box = {}; vm.createContext(box); vm.runInContext(scriptIn(closet, 'uu-closet-core'), box); return box.UUCloset; };
const start = html.indexOf('<details class="uu-panel" id="uu-p-vr-asset-closet"');
const panel = start < 0 ? '' : html.slice(start, html.indexOf('</details>', start));

test('the closet is a page of its own, credited to Dev, with a way back to Unity Stable', () => {
  assert.ok(scriptIn(closet, 'uu-closet-core') && scriptIn(closet, 'uu-closet-app'), 'its two scripts');
  assert.ok(closet.includes('href="unity-unicorn-20261008.html#uu-p-vr-asset-closet"'), 'back to its tool panel');
  assert.ok(closet.includes('href="https://github.com/XxdevilishxX/Vr-Asset-Closet" target="_blank" rel="noopener noreferrer"'));
  assert.ok(!/<iframe|fonts\.googleapis|<link /.test(closet), 'nothing loads from the web');
  assert.ok(!/innerHTML|insertAdjacentHTML|document\.write/.test(closet), 'text goes in with textContent');
});

test('Unity Stable keeps one small tool panel for it, at the end of the Avatar tools side', () => {
  assert.ok(panel, 'the page has the closet tool panel');
  assert.ok(panel.includes('data-item="unity-unicorn-vr-asset-closet-tool-panel"></span>unity-unicorn-vr-asset-closet-tool-panel<'));
  assert.ok(start > html.indexOf('id="uu-side-avatar"') && start < html.indexOf('id="uu-side-world"'), 'on the Avatar tools side');
  assert.ok(panel.includes(`id="uu-closet-open" href="${PAGE}"`) && existsSync(new URL('../' + PAGE, import.meta.url)), 'Open goes to the page');
  assert.ok(!panel.includes('id="cl-rails"') && !panel.includes('uu-closet-app'), 'the closet itself is not on the panel');
  assert.ok(html.includes('"world-assets","vr-asset-closet"];'), 'its light carries over like every other tool panel');
});

// A tiny stand-in for the page, enough for the counting script.
function countWith(saved) {
  const el = (id) => ({ id, children: [], textContent: '', className: '', append(...k) { this.children.push(...k); }, replaceChildren(...k) { this.children = [...k]; },
    set innerHTML(_) { throw new Error('no innerHTML'); } });
  const nodes = { 'uu-closet-stats': el('uu-closet-stats'), 'uu-n-closet': el('uu-n-closet') };
  const box = {
    document: { getElementById: (id) => { assert.ok(nodes[id], 'missing ' + id); return nodes[id]; }, createElement: (t) => el(t) },
    localStorage: { getItem: (k) => (k === KEY ? saved : null) },
    window: { addEventListener() {} },
  };
  vm.createContext(box);
  vm.runInContext(scriptIn(panel, 'uu-closet-count'), box);
  const rows = nodes['uu-closet-stats'].children.map((li) => li.children.map((s) => s.textContent).join(' '));
  return { badge: nodes['uu-n-closet'].textContent, rows };
}

test('the tool panel counts what the closet saved, and says so when it was never opened', () => {
  assert.equal(countWith(null).badge, 'not opened yet');
  assert.equal(countWith('{not json').badge, 'not opened yet');
  assert.equal(countWith(JSON.stringify({ cats: [] })).badge, 'not opened yet');
  const saved = JSON.stringify({
    cats: ['Avatars', 'Tops', 'Shoes'],
    items: [
      { id: 'a', name: '<b>Fox</b>', cat: 'Avatars', img: 'art/avatars/poppy-20261009.jpg', fav: true },
      { id: 'b', name: 'Hoodie', cat: 'Tops', img: '' },
      { id: 'c', name: 'Boots', cat: 'Tops', img: 'data:image/jpeg;base64,AAAA', fav: 'yes' },
    ],
    outfits: [{ id: 'o', name: 'Night out', ids: ['a', 'b'] }],
  });
  const { badge, rows } = countWith(saved);
  assert.equal(badge, '3 items');
  assert.deepEqual(rows, ['3 items, hanging on 2 of 3 rails', '2 with a photo; the rest show a name card', '1 favorite', '1 outfit']);
  assert.ok(!rows.join(' ').includes('Fox'), 'it shows counts, never saved text');
});

test('the first time, the Avatars rail holds the 14 avatar thumbnails, and each picture is in the repo', () => {
  const C = core(), s = C.seed();
  assert.equal(s.cats[0], 'Avatars');
  assert.equal(s.items.length, 14);
  for (const it of s.items) {
    assert.equal(it.cat, 'Avatars');
    assert.ok(C.imgOk(it.img), it.img);
    assert.ok(existsSync(new URL('../' + it.img, import.meta.url)), it.img + ' exists');
  }
});

test('only pictures the page made, or the avatar thumbnails, may load', () => {
  const C = core();
  assert.ok(C.imgOk('data:image/jpeg;base64,AAAA'));
  assert.ok(C.imgOk('art/avatars/poppy-20261009.jpg'));
  for (const bad of ['javascript:alert(1)', 'https://example.com/a.jpg', 'data:text/html;base64,AAAA', 'file:///C:/x.jpg',
    'abc123.jpg', '../art/avatars/poppy-20261009.jpg', 'data:image/svg+xml;base64,AAAA', 42, null]) {
    assert.ok(!C.imgOk(bad), String(bad));
  }
});

test("a backup, or Dev's closet.json, is read safely: paths on her PC are dropped, and outfits keep only real pieces", () => {
  const C = core();
  const d = plain(C.clean({
    cats: ['Avatars', 'avatars', 'Tails'],
    items: [
      { id: 'a', name: '  Fox avatar ', cat: 'Avatars', img: 'k3x.jpg', src: 'D:\\Avatar Creations\\Fox.zip', key: 'fox', fav: true },
      { id: 'a', name: 'Same id twice', cat: 'Wings' },
      { name: '' }, null, 'text',
      { id: 'b', name: 'x'.repeat(200), cat: '', img: 'javascript:alert(1)', note: 7 },
    ],
    outfits: [{ id: 'o', name: 'Night out', ids: ['a', 'gone', 5] }],
  }));
  assert.deepEqual(d.cats, ['Avatars', 'Tails', 'Wings', 'Accessories']);
  assert.equal(d.items.length, 3);
  assert.deepEqual(d.items[0], { id: 'a', name: 'Fox avatar', cat: 'Avatars', note: '', img: '', fav: true });
  assert.notEqual(d.items[1].id, 'a', 'a repeated id gets a new one');
  assert.equal(d.items[2].name.length, 80);
  assert.equal(d.items[2].img, '');
  assert.deepEqual(d.outfits[0].ids, ['a']);
  assert.equal(C.clean({ cats: [] }), null);
  assert.equal(C.clean('nope'), null);
});

test('name cards are a picture, and their initials cannot carry markup', () => {
  const card = decodeURIComponent(core().nameCard('<b>Fox</b> "Hoodie"'));
  assert.ok(card.startsWith('data:image/svg+xml'));
  assert.ok(card.includes('>BH</text>'), 'only letters and digits make the initials');
});

test('pet.json and the L1 tool panel list the closet as one of our programs, and Dev\'s closet as 3rd party', () => {
  const L1 = pet.intelligence_levels.L1;
  assert.ok(L1.native.includes("VR asset closet (its own page, built from Dev's VR Closet)"));
  assert.ok(L1.third_party.includes("Dev's VR Closet (XxdevilishxX/Vr-Asset-Closet)"));
  assert.ok(html.includes(`["VR asset closet","its own page, ${PAGE}, built from Dev's VR Closet; its tool panel shows the counts and opens it"]];`));
  assert.ok(html.includes(`["Dev's VR Closet","made by Dev (XxdevilishxX); the VR asset closet is built from it"]];`));
  assert.ok(!L1.tools.some((t) => /closet/i.test(t)), 'a program, not a tool');
});
