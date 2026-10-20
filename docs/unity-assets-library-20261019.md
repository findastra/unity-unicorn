# unity-assets-library

The public library of finished assets from Astra's VRChat worlds: [findastra/unity-assets-library](https://github.com/findastra/unity-assets-library).
It's a **Unity Unicorn tool** (Unity Stable, `pet-program-5`): the database on GitHub that Astra asked
unity-asset-creator to end in (2026-10-18), shown on Unity Unicorn's page right below unity-asset-creator. Made
2026-10-19 by Claude Opus 5.5 (`claude-opus-5-5`).

## What it holds

- `assets/<id>/<version>/`: the `.unitypackage` (the prefab and everything it uses, our own Unity Stable shaders
  included), `manifest.json` (`unity-unicorn.asset/v1`: provenance, stats, the shaders it needs from the shaders-shop,
  the finished checklist with notes, Unity and SDK versions, a SHA-256 for every file) and `previews/`.
- `library.json` (`unity-unicorn.library/v1`): every asset and version, for tools.
- The README's assets table, written by the publisher between `assets:start` and `assets:end`.

## How a version gets in

Only through unity-asset-creator's step 12: `publish-asset-20261018.py` checks the export and opens a pull request,
and Astra merges it. Nobody pushes to its `main`.

## So far

| Asset | Version | Merged |
|---|---|---|
| `liminal-doors-phone-booth` | 1.0.0 | [PR #1](https://github.com/findastra/unity-assets-library/pull/1), 2026-10-19 |
| `liminal-doors-stop-01-sky` | 1.0.0 | [PR #1](https://github.com/findastra/unity-assets-library/pull/1), 2026-10-19 |

## Limits

- No licence chosen yet. Until Astra picks one, ask before using an asset outside her worlds.
- Paid store assets never go in it, and the publisher refuses files with machine paths or over 95 MB.
