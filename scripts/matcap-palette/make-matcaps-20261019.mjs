// Writes the matcap palette as PNGs for Unity (Claude Opus 5.5, 2026-10-19).
// The recipes are read from the page itself (<script id="uu-matcap-recipes"> in unity-unicorn-20261008.html), so the page
// and the PNGs never disagree. Node built-ins only.
//   node scripts/matcap-palette/make-matcaps-20261019.mjs <out-folder> [--size 512] [--only chrome,galaxy]
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';
import { deflateSync } from 'node:zlib';

export function loadRecipes(htmlPath = new URL('../../unity-unicorn-20261008.html', import.meta.url)) {
  const html = readFileSync(htmlPath, 'utf8');
  const open = '<script id="uu-matcap-recipes">', start = html.indexOf(open);
  if (start < 0) throw new Error('The page has no uu-matcap-recipes script.');
  const code = html.slice(start + open.length, html.indexOf('</script>', start));
  const box = {};
  vm.runInNewContext(code + '\nthis.MC_RECIPES = MC_RECIPES; this.MC = MC; this.MC_FAMILIES = MC_FAMILIES;', box);
  return box;
}

const CRC = new Int32Array(256).map((_, n) => { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; return c; });
const crc32 = (buf) => { let c = -1; for (const b of buf) c = CRC[(c ^ b) & 255] ^ (c >>> 8); return (c ^ -1) >>> 0; };
function chunk(type, data) {
  const len = Buffer.alloc(4); len.writeUInt32BE(data.length);
  const td = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(td));
  return Buffer.concat([len, td, crc]);
}

/** RGB pixels (Uint8Array, size*size*3) to a PNG buffer. */
export function encodePng(rgb, size) {
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(size, 0); ihdr.writeUInt32BE(size, 4); ihdr[8] = 8; ihdr[9] = 2;
  const row = size * 3 + 1, raw = Buffer.alloc(row * size);
  for (let y = 0; y < size; y++) Buffer.from(rgb.buffer, rgb.byteOffset + y * size * 3, size * 3).copy(raw, y * row + 1);
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ihdr), chunk('IDAT', deflateSync(raw, { level: 9 })), chunk('IEND', Buffer.alloc(0))]);
}

/** One matcap as RGB pixels, 2x2 samples per pixel. Square and opaque: outside the circle repeats the edge. */
export function renderMatcap(MC, R, size) {
  const rgb = new Uint8Array(size * size * 3);
  for (let py = 0; py < size; py++) for (let px = 0; px < size; px++) {
    let r = 0, g = 0, b = 0;
    for (let s = 0; s < 4; s++) {
      let x = ((px + (s & 1 ? 0.75 : 0.25)) / size) * 2 - 1, y = 1 - ((py + (s & 2 ? 0.75 : 0.25)) / size) * 2;
      const d = Math.hypot(x, y); if (d > 0.999) { x = x / d * 0.999; y = y / d * 0.999; }
      const c = MC.shade(R, x, y); r += c[0]; g += c[1]; b += c[2];
    }
    const i = (py * size + px) * 3;
    rgb[i] = Math.round(r / 4 * 255); rgb[i + 1] = Math.round(g / 4 * 255); rgb[i + 2] = Math.round(b / 4 * 255);
  }
  return rgb;
}

function main(args) {
  const valueOf = (flag) => (args.includes(flag) ? args[args.indexOf(flag) + 1] : null);
  const out = args.find((a, i) => !a.startsWith('--') && !['--size', '--only'].includes(args[i - 1]));
  if (!out) { console.error('Usage: node make-matcaps-20261019.mjs <out-folder> [--size 512] [--only id,id]'); process.exit(2); }
  const size = Number(valueOf('--size')) || 512, only = valueOf('--only')?.split(',') ?? null;
  const { MC_RECIPES, MC } = loadRecipes();
  mkdirSync(out, { recursive: true });
  const index = [];
  for (const R of MC_RECIPES) {
    if (only && !only.includes(R.id)) continue;
    const file = `matcap-${R.id}-20261019.png`;
    writeFileSync(join(out, file), encodePng(renderMatcap(MC, R, size), size));
    index.push({ id: R.id, name: R.name, family: R.family, file });
  }
  writeFileSync(join(out, 'matcap-palette-index-20261019.json'),
    JSON.stringify({ made_by: 'Unity Unicorn matcap palette, drawn from math: free to use', size, matcaps: index }, null, 1));
  console.log(`Wrote ${index.length} matcaps (${size} px) to ${out}`);
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) main(process.argv.slice(2));
