// The parts of the page the panel tests read, found where the hub frame put them (2026-10-24): the Unity Unicorn
// panels start at <section class="wide uu"> and end with their scripts, the last one uu-fold-library.
// Before the frame they were one <section id="uu-panels">; the tests that cut that section found nothing.
// Claude Opus 5.5 (claude-opus-5-5), 2026-11-08.
import { readFileSync } from 'node:fs';

export const html = readFileSync(new URL('../unity-unicorn-20261008.html', import.meta.url), 'utf8');

const start = html.indexOf('<section class="wide uu">');
const keyAt = html.indexOf('const KEY="astra-pet-desk:unity-unicorn:modules:1"');
const scriptStart = html.lastIndexOf('<script>', keyAt);
const scriptEnd = html.indexOf('</script>', keyAt);
// The panels' own scripts (matcap, menu map, vine, the sides, the tool library) follow it; the last is uu-fold-library.
const lastAt = html.indexOf('<script id="uu-fold-library">');
const lastEnd = lastAt > keyAt ? html.indexOf('</script>', lastAt) : scriptEnd;

// Every Unity Unicorn panel and the script that draws them, as one piece of text ('' if the page changed shape).
export const panels = start > 0 && keyAt > start && scriptStart > start ? html.slice(start, lastEnd + '</script>'.length) : '';
// The script that draws the panels and keeps their lights.
export const panelScript = panels ? html.slice(scriptStart + '<script>'.length, scriptEnd) : '';
