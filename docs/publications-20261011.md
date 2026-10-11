# Unity Unicorn source release — 2026-10-11

- Owner: Astra.
- Version: `0.2.0-20261011`.
- Immutable source tag: [`v0.2.0-20261011`](https://github.com/findastra/unity-unicorn/tree/v0.2.0-20261011).
- Release destination: [GitHub Releases](https://github.com/findastra/unity-unicorn/releases/tag/v0.2.0-20261011).
- Browser entry: `unity-unicorn-20261008.html`, copied unchanged into the desktop Cage's app folder.
- Built with Codex (GPT-6).

## What changed

The existing planning desk now has a tool library with commands, source references, audit findings and execution history. Dates use America/Denver. Completed uses and failed attempts are separate; opening the app, copying commands or importing a record never creates a new execution time. The optional private local sidecar makes an installed inventory visible without putting private project data into public source.

The reusable masonry auditor checks serialized mesh integrity, authored surface maps, duplicate geometry, sampled openings, texture seam rows and duplicate placements. The foreground recorder gives other synchronous tools the same usage-receipt format. Blender Python commands must propagate script failures through a nonzero Python exit code. Tests never populate real execution history.

The avatar tracker, plans, skill book and images from source commit `7a6ac96` are preserved. Existing planning records retain their original browser storage key.

## Validation

- 21 Python auditor regression tests and 7 foreground-runner tests passed.
- Nine generic Node library tests passed; a tenth optional test passed against the installed app and actual private sidecar, including date display and reload preservation.
- Browser script syntax, transactional imports, receipt/report deduplication, failed-run history, safe text rendering, storage failure and prior planner-data preservation were checked.
- New source filenames and staged content were checked for the owner's naming convention and private data exclusion.

The browser automation surface disallows local file URLs, so no interactive browser visual test is claimed for this release. The app has no direct Unity/VRChat connection, does not execute commands, and does not silently watch output directories. Import new receipts or update the private sidecar after running a tool.

## Publication and recovery

The tag names the exact source prepared for release. Remote push, tag and GitHub Release verification are recorded in a subsequent dated publication receipt; this source record alone is not evidence of those network actions or a website deployment.

Completed remote and local verification is recorded in [the source release receipt](source-release-receipt-20261011.md).

Keep all existing tags immutable. Rollback uses an earlier source revision and the corresponding app entry. Private tool history uses a separate storage key and a private export; older apps may not display it. Preserve that data and the existing planning desk during rollback.
