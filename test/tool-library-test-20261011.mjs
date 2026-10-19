import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const html = readFileSync(process.env.UNITY_UNICORN_HTML || new URL('../unity-unicorn-20261008.html', import.meta.url), 'utf8');
const script = /<script id="tool-library-app">([\s\S]*?)<\/script>/.exec(html)[1];
const key = 'astra-unity-tool-library:1';
const deskKey = 'astra-pet-desk:unity-unicorn:1';
class Element {
  constructor(tag) { this.tag = tag; this.children = []; this.dataset = {}; this.value = ''; this.files = []; this.textContent = ''; }
  append(...items) { this.children.push(...items); }
  replaceChildren(...items) { this.children = [...items]; }
  click() { this.clicked = true; }
  set innerHTML(_) { throw new Error('Imported content must never be rendered as HTML.'); }
}
const flatten = (e) => [e.textContent || '', ...(e.children || []).flatMap(c => typeof c === 'string' ? [c] : flatten(c))].join('\n');
function boot(storage = new Map(), seed, storageFailure = false) {
  const nodes = new Map();
  for (const m of html.matchAll(/\bid="([^"]+)"/g)) nodes.set(m[1], new Element('div'));
  let writes = 0;
  const sandbox = {
    document: { getElementById: id => { assert.ok(nodes.has(id), 'Missing DOM element ' + id); return nodes.get(id); }, createElement: tag => new Element(tag) },
    localStorage: { getItem: k => storage.get(k) || null, setItem: (k, value) => { if (storageFailure) throw new Error('Storage unavailable'); writes++; storage.set(k, value); } },
    navigator: { clipboard: { writeText: async () => {} } }, Intl, Date, Map, JSON, Number, String, Array, Error, Blob, URL, setTimeout: () => 0,
  };
  if (seed) sandbox.UnityUnicornLocalTools = seed;
  vm.createContext(sandbox); vm.runInContext(script, sandbox);
  return { api: sandbox.UnityToolLibrary, nodes, storage, writes: () => writes };
}
const catalog = (tools) => ({ schema: 'unity-unicorn.tool-catalog/v1', tools });
const tool = (over = {}) => ({ id: 'cloud-mesh-generator', name: 'Cloud mesh generator', kind: 'Generator', project: { name: 'Sample project' }, ...over });
const usage = (over = {}) => ({ schema: 'unity-unicorn.tool-usage/v1', tool_id: 'masonry-structural-audit', project: { name: 'Sample project' }, completed_at: '2026-10-10T18:06:00Z', status: 'completed', ...over });
const report = (over = {}) => ({ ...usage(), schema: 'unity-unicorn.structural-audit/v1', summary: { warnings: 1 }, findings: [{ id: 'sample-gap', severity: 'warning', title: 'Sample gap', evidence: 'A gap was measured.', suggested_fix: 'Inspect the joint.' }], metrics: {}, limitations: [], ...over });
const card = (app, id = 'masonry-structural-audit') => app.nodes.get('tool-grid').children.find(e => e.dataset.toolId === id);

test('every built-in tool has a real dated definition and a concise expanded summary below its name', () => {
  const app=boot();
  assert.equal(app.nodes.get('tool-grid').children.length,38);
  for (const item of app.nodes.get('tool-grid').children) {
    const title=item.children[0].children.find(e=>e.tag==='h3').textContent;
    const description=item.children[1].textContent;
    assert.match(title,/^[a-z0-9-]+-\d{8}\.json$/);
    assert.ok(description.split(/\s+/).length>=25 && description.split(/\s+/).length<=50);
    const definition=JSON.parse(readFileSync(new URL('../tools/'+title,import.meta.url),'utf8')).tools[0];
    assert.equal(definition.id,item.dataset.toolId);
    assert.equal(definition.description,description);
  }
});

test('older saved catalog cards receive new names and summaries without changing execution history', () => {
  const saved={catalog:[tool({description:'Old short summary.',last_used:{date:'2026-10-06',evidence:'A real dated completion report.'}})],runs:[]};
  const storage=new Map([[key,JSON.stringify(saved)]]);
  const app=boot(storage);
  const output=flatten(card(app,'cloud-mesh-generator'));
  assert.match(output,/cloud-mesh-generator-20261011.json/);
  assert.match(output,/Build connected cloud shapes from overlapping lobes/);
  assert.match(output,/Oct 6, 2026/);
  assert.equal(app.writes(),0);
});

test('scripts parse and page loading preserves the legacy planning desk and does not create usage', () => {
  for (const m of html.matchAll(/<script(?:[^>]*)>([\s\S]*?)<\/script>/g)) new vm.Script(m[1]);
  const storage = new Map([[deskKey, '[{"id":"existing-task","title":"Existing notes"}]']]);
  const app = boot(storage);
  assert.equal(app.writes(), 0);
  assert.equal(storage.get(deskKey), '[{"id":"existing-task","title":"Existing notes"}]');
  assert.equal(app.api.getState().runs.length, 0);
  assert.match(flatten(card(app)), /Unknown/);
});

test('failed reports preserve last completed use and are labeled separately', () => {
  const app = boot();
  app.api.ingest(report());
  app.api.ingest(report({ completed_at: '2026-10-11T19:00:00Z', status: 'failed', findings: [], evidence: 'The input could not be read.' }));
  assert.equal(app.api.getState().runs.length, 2);
  const output = flatten(card(app));
  assert.match(output, /Last completed use\nOct 10, 2026/);
  assert.match(output, /Latest attempt: Oct 11, 2026.*failed/);
});

test('same-timestamp receipt and report show one activity while retaining findings in either import order', () => {
  for (const items of [[usage(), report()], [report(), usage()]]) {
    const app = boot();
    items.forEach(x => app.api.ingest(x));
    app.api.ingest(report());
    assert.equal(app.api.getState().runs.length, 2, 'one receipt and one full report retained');
    const output = flatten(card(app));
    assert.match(output, /A gap was measured/);
    assert.doesNotMatch(output, /Activity history/);
  }
});

test('local sidecar merges newer evidence and missing tools without replacing custom catalog entries or history', () => {
  const initial = boot();
  initial.api.ingest(catalog([tool({ name: 'My custom tool name', command_template: 'my-reviewed-command', last_used: { date: '2026-10-06', evidence: 'Older execution record.' } })]));
  initial.api.ingest(usage());
  const seed = { schema: 'unity-unicorn.tool-library-backup/v1', catalog: [tool({ name: 'Default name', command_template: 'default-command', last_used: { date: '2026-10-10', evidence: 'Newer execution report.' } }), tool({ id: 'other-tool', name: 'Other tool' })], runs: [usage()] };
  const app = boot(initial.storage, seed);
  const state = app.api.getState(), existing = state.catalog.find(x => x.id === 'cloud-mesh-generator');
  assert.equal(existing.name, 'My custom tool name');
  assert.equal(existing.command_template, 'my-reviewed-command');
  assert.equal(existing.last_used.date, '2026-10-10');
  assert.equal(state.catalog.length, 2);
  assert.equal(state.runs.length, 1);
  assert.deepEqual(JSON.parse(JSON.stringify(boot(initial.storage, seed).api.getState())), JSON.parse(JSON.stringify(state)));
});

test('catalog dates require execution evidence and malformed completion dates are rejected', () => {
  const app = boot();
  for (const completed_at of ['2026-02-30T12:00:00Z', '2026-10-11', 'yesterday', '2026-10-11T99:00:00Z']) assert.throws(() => app.api.normalizeRun(usage({ completed_at })));
  assert.throws(() => app.api.normalizeTool(tool({ last_used: { date: '2026-10-10' } })));
  assert.throws(() => app.api.normalizeTool(tool({ last_used: { date: '2026-02-30', evidence: 'invalid' } })));
  assert.equal(app.api.displayDate(app.api.normalizeRun(usage({ completed_at: '2026-10-12T02:06:00Z' }))), 'Oct 11, 2026, 8:06 PM · Denver');
});

test('whole-file imports are transactional, including malformed JSONL', async () => {
  const app = boot(); app.api.ingest(report());
  const before = JSON.stringify(app.api.getState()), writes = app.writes();
  assert.throws(() => app.api.ingest([catalog([tool()]), { schema: 'unsupported' }]));
  assert.equal(JSON.stringify(app.api.getState()), before);
  assert.equal(app.writes(), writes);
  app.nodes.get('tool-file').files = [{ name: 'bad-usage-20261011.jsonl', size: 300, text: async () => JSON.stringify(usage()) + '\n{bad json' }];
  await app.nodes.get('tool-file').onchange();
  assert.equal(JSON.stringify(app.api.getState()), before);
  assert.equal(app.writes(), writes);
});

test('finding text is not HTML and exports, copies, reloads and searches do not mark use', async () => {
  const app = boot();
  app.api.ingest(report({ findings: [{ id: 'x', title: '<img src=x onerror=alert(1)>', severity: 'warning', evidence: { text: '<script>alert(1)</script>' }, suggested_fix: 'Use <safe>' }] }));
  assert.match(flatten(card(app)), /<img src=x onerror=alert\(1\)>/);
  const before = JSON.stringify(app.api.getState());
  app.nodes.get('tool-export').onclick();
  const commandDetails = card(app).children.find(e => e.tag === 'details');
  await commandDetails.children.find(e => e.tag === 'button').onclick();
  app.nodes.get('tool-search').value = 'masonry structural';
  app.nodes.get('tool-search').oninput();
  assert.equal(app.nodes.get('tool-grid').children.length, 1);
  assert.equal(JSON.stringify(app.api.getState()), before);
  assert.equal(JSON.stringify(boot(app.storage).api.getState()), before);
});

test('storage failure is visible and retains records for an immediate private export', () => {
  const app = boot(new Map(), undefined, true);
  app.api.ingest(report());
  assert.match(app.nodes.get('tool-status').textContent, /storage is unavailable or full/);
  assert.equal(app.api.getState().runs.length, 1);
  assert.equal(app.storage.has(key), false);
});

 test('failed catalog dates are rejected transactionally and failed usage keeps completed history', () => {
  const app = boot();
  app.api.ingest(catalog([tool({ last_used: { date: '2026-10-10', status: 'completed', evidence: 'Completed execution.' } })]));
  const bad = tool({ last_used: { date: '2026-10-11', status: 'failed', evidence: 'Failed execution.' } });
  const before = JSON.stringify(app.api.getState());
  assert.throws(() => app.api.ingest(catalog([bad])), /Import failed attempts as usage reports/);
  assert.throws(() => app.api.mergeLocalSeed({ schema: 'unity-unicorn.tool-library-backup/v1', catalog: [tool({ id: 'new-tool' }), bad], runs: [] }), /Import failed attempts as usage reports/);
  assert.equal(JSON.stringify(app.api.getState()), before);
  app.api.ingest(usage({ tool_id: 'cloud-mesh-generator', completed_at: '2026-10-11T19:00:00Z', status: 'failed' }));
  const output = flatten(card(app, 'cloud-mesh-generator'));
  assert.match(output, /Last completed use\nOct 10, 2026/);
  assert.match(output, /Latest attempt: Oct 11, 2026.*failed/);
});

test('optional installed copy loads its real local sidecar with evidenced dates and preserves private storage', { skip: !process.env.UNITY_UNICORN_LOCAL_SEED }, () => {
  const seedContext = {};
  vm.createContext(seedContext);
  const seedSource = readFileSync(process.env.UNITY_UNICORN_LOCAL_SEED, 'utf8');
  assert.match(seedSource, /^globalThis\.UnityUnicornLocalTools = /);
  vm.runInContext(seedSource, seedContext);
  const seed = seedContext.UnityUnicornLocalTools;
  assert.equal(seed.schema, 'unity-unicorn.tool-library-backup/v1');
  assert.ok(seed.catalog.length > 0);
  assert.ok(seed.runs.length > 0);
  const desk = '[{"id":"retained","title":"Existing private planning record"}]';
  const storage = new Map([[deskKey, desk]]);
  const app = boot(storage, seed);
  assert.equal(storage.get(deskKey), desk);
  assert.equal(app.api.getState().catalog.length, seed.catalog.length);
  for (const t of seed.catalog) {
    const output = flatten(card(app, t.id));
    assert.ok(output.includes(t.name));
    if (t.last_used) assert.ok(output.includes(app.api.displayDate(t.last_used)), 'Actual evidenced date should render for '+t.id);
  }
  const before = JSON.stringify(app.api.getState());
  assert.equal(JSON.stringify(boot(storage, seed).api.getState()), before);
  assert.ok(app.api.getState().runs.every(run => seed.runs.some(original => original.tool_id === run.tool_id && original.completed_at === run.completed_at)));
});

test('structural reports accept the no-windows tool and future valid tool IDs', () => {
  const app = boot();
  for (const id of ['no-windows-audit-tool', 'future-arch-audit']) {
    app.api.ingest(report({tool_id: id}));
    assert.ok(app.api.getState().runs.some(run => run.tool_id === id && run.findings.length === 1));
  }
});
