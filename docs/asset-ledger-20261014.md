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

## Credits page (generated per world)

```markdown
# Credits: <world name>
Made by Astra. Built with Unity and the VRChat SDK.

| Asset | Creator | License |
|---|---|---|
| <name> | <creator> | <license, linked> |

<attribution text required by each license>
```
