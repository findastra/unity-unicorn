# Source publication · 2026-10-09

- App: Unity Unicorn
- Owner and credit: Astra
- Version: `0.1.1-20261009`
- Immutable source tag: `v0.1.1-20261009`
- Previous tag: `v0.1.0-20261008` (unchanged; see [publications-20261008.md](publications-20261008.md))
- Browser entry: `unity-unicorn-20261008.html`

## Change

The pet frame in the side rail is now square at every window width. Its height came from a minimum height while its width followed the rail, so it was 176×190 on wide windows, 151×190 on laptop widths (where the 165px pet also overflowed and was clipped) and 70×77 on phones. The frame now uses `aspect-ratio: 1/1` and the pet scales with it.

Layout only: records, storage keys, exports and behaviour are unchanged. The same fix was applied to all eight rail-style pet apps from one shared script in the Cage repository, `scripts/square-pet-well-20261009.mjs`.

## Validation

Rendered in headless Chromium at 1400, 1000, 700 and 390 pixels wide; the frame measured square at each width and the pet sat inside it. Embedded JavaScript is unchanged from `v0.1.0-20261008`.

## Publication boundary

Tagged only with Astra's OK. A source tag is not a GitHub Release or live website deployment; those are verified separately.
