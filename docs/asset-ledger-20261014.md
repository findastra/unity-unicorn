# asset-ledger: how Unity Unicorn acquires, audits, handles and credits every asset

*A tool of Unity Unicorn's pet program (`pet-program-5`, no name assigned yet).* Tool box label:
`pet-program-5-v1.0-asset-ledger-v1.0`. **Status: design (2026-10-14), with Astra's two worlds as worked examples.**
Started by Claude Opus 5.5 (`claude-opus-5-5`) at Astra's request. Follows the
[Pet Program Framework](https://github.com/findastra/astras-pet-os/blob/main/PET-PROGRAM-FRAMEWORK.md) and
[PROVENANCE.md](https://github.com/findastra/astras-pet-os/blob/main/PROVENANCE.md).

VRChat creators look hard at where assets come from. Unity Unicorn is the model pet for it: every asset in every world
or avatar has a ledger row **before it ships**, and nothing ships that the ledger has not cleared.

## A ledger row

| Field | What it holds | Public? |
|---|---|---|
| `id` | `<world-or-avatar>-<asset>`, lowercase with hyphens | Yes |
| `name`, `kind` | What it is: model, texture, shader, script, package, sky, particles, audio, video link, avatar base, clothing | Yes |
| `creator`, `source` | Who made it, and where it came from (store, repository, package registry, or Astra's own work) | Yes |
| `license` | An SPDX id where one exists (`CC0-1.0`, `MIT`, `CC-BY-4.0`), else `store-personal`, `store-commercial`, `research-only`, `unclear` or `own-work` | Yes |
| `attribution` | The exact credit text the license asks for, if any | Yes |
| `where_used` | Which worlds and avatars use it | Yes |
| `in_repo` | `tracked` (with its license file), `restored` (from a package manager, not stored) or `never` (paid or restricted) | Yes |
| `public_ok` | Whether a **public** upload is allowed, and why not | Yes |
| `status` | `cleared`, `placeholder` (private testing only), `needs-permission` or `blocked` | Yes |
| `proof` | Receipt, order number, store account, emailed permission | **No: stays on the PC, never in Git** |
| `checked` | Date and who checked it (Test Tortoise, then Astra) | Yes |

## Rules

1. **No row, no ship.** A world or avatar is published only when every asset it contains is `cleared`.
2. **Paid assets are never in a repo.** Store assets are `in_repo: never`; the ledger records them, the files stay
   local. GitHub push protection and File Master's checks back this up.
3. **Research-only and unclear assets are placeholders.** They may be used for private testing and are blocked from
   public uploads until replaced or permitted.
4. **Licenses travel with the asset.** Anything `tracked` keeps its license file next to it.
5. **Credits are generated, not remembered.** Each world's credits page is built from the rows that ask for attribution.
6. **Private stays private.** Receipts, order numbers and store accounts never leave the PC. Gone Ghost strips location
   and personal metadata from screenshots before anything is published.
7. **Inspiration is declared.** A world inspired by someone else's work says so, reproduces no text or characters,
   and is marked non-commercial fan work.
8. **Two checks.** Test Tortoise checks every row against its license before a publish; Astra (L4) approves.

## Worked examples

Rows: [docs/asset-ledger-example-20261014.json](asset-ledger-example-20261014.json).

**Astra's Infinite Pole: ready, with its credits.** Every asset is cleared: Kenney Particle Pack and Poly Haven skies
(CC0), USharpVideo (MIT, license kept), the VRChat SDK and Unity packages restored from their package managers rather
than stored, and playlist entries that are links, not bundled media.

**Piranesi House: blocked for public upload, correctly.** Its stand-in statues come from research scan collections
(Stanford 3D Scanning Repository: research use only) and scans with unclear or museum-specific terms. The ledger marks
them `placeholder` and `public_ok: no`, exactly as the world's own STATUE_REGISTRY already says, until each is
replaced with a no-copyright museum scan or Astra's own sculpt. The world is inspired by *Piranesi* (Susanna Clarke):
declared as inspiration, with no text or characters reproduced.

## Assets we created (2026-10-19)

Assets Astra and her assistants made are listed in [assets-we-created-20261019.json](assets-we-created-20261019.json) with the public fields of a ledger row (creator, licence, attribution, where used), and shown on the page's assets-we-created panel. Finished ones are exported to findastra/unity-assets-library.

## Credits page (generated per world)

```markdown
# Credits: <world name>
Made by Astra. Built with Unity and the VRChat SDK.

| Asset | Creator | License |
|---|---|---|
| <name> | <creator> | <license, linked> |

<attribution text required by each license>
```

## Downloads: everything an assistant fetches or installs (Astra, 2026-10-18)
Astra asked for one place that remembers what the AI downloaded to get work done, so any pet can find and reuse it
instead of fetching a second copy. It lives here, in the asset ledger, and is owned by Unity Unicorn. Other pets reach
it through a grant (read only), never through their own copy (TOOLS-AND-TRICKS.md, "Shared tools and grants").
AI Dashund's pet program, AI Doghouse, lists every row so the pets learn which tools exist and why.

A download row is a ledger row with these extra fields. Rows: [download-ledger-20261018.json](download-ledger-20261018.json).

| Field | What it holds | Public? |
|---|---|---|
| `kind` | `model`, `texture`, `sky`, `audio`, `program`, `plugin`, `mcp-server`, `package`, `script`, `service`, `reference-data` | Yes |
| `source` | Where it came from (official page or registry) | Yes |
| `size_mb`, `files_sha256` | How big, and hashes so a later copy can be checked | Yes |
| `downloaded_at`, `downloaded_by` | Denver date, and which assistant and model fetched it | Yes |
| `approved_by` | Who said yes, and when (downloads are asked for one by one) | Yes |
| `why`, `where_used` | What it was fetched for, and which worlds, avatars or pets use it | Yes |
| `owner_pet` | The one pet program that owns the copy; others get grants | Yes |
| `listed_in` | Where it is taught (AI Doghouse) | Yes |
| `stored` | Where the files sit on the PC | **No: on the PC only** |

Rules:
1. **Ask first.** No download without Astra's yes for that file, with its name, source and size.
2. **One copy.** Check this ledger before downloading. If it is already here, ask for a grant instead.
3. **Third-party tools go through quarantine.** A program, plugin or MCP server from someone else follows the Shed's
   rules (a checker reviews it and the farm's L4 approves) before it runs.
4. **Dates come from records.** `downloaded_at` comes from the chat, ACTIVITY.md or a receipt, never a file date.
