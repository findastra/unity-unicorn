# Assistant rules for unity-unicorn

*A pet app by Astra.* Public credit is always Astra.

Browser interface prepared with Codex (GPT-6), 2026-10-08. Record the model and version when another assistant changes this repository.
Square pet frame (`v0.1.1-20261009`) fixed with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-09.
Skill book `docs/skills-20261009.md` started with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-09. After avatar or world sessions, add learned skills to its categories and append to its session log, without account details, blueprint IDs or machine paths.
Avatar tracker and plans (`docs/avatar-tracker-20261009.md`, `docs/avatar-plans-20261009.md`) started with Claude Opus 5.5 (`claude-opus-5-5`), 2026-10-09. Rules: every avatar asset folder keeps an `Edit Log - <avatar>-<date>.txt`, so create one from the template when it's missing. Refresh both files on each routine scan. When an avatar's licence may not allow uploads to more than one account, upload to findノastra only. Poppy's thumbnail is always `art/avatars/poppy-20261009.jpg` (the original is kept beside Poppy's assets); use it for every Poppy upload and tracker row. Fae's thumbnail is always `art/avatars/fae-20261009.jpg` (the original is kept beside Fae's assets).

## Scope

Working offline draft: purpose-specific records, editing, status changes, search, local browser storage, JSON and Markdown exports. External integrations are planned.

## Rules

- Keep the browser interface self-contained and dependency-free. Its dated entry is `unity-unicorn-20261008.html`.
- Treat user text as untrusted. Render it with textContent, validate links, and never insert record contents as HTML.
- Keep private records, health or finance entries, API secrets, credentials, environment files, logs and machine metadata outside Git. Browser exports are private user data.
- Label planned connections truthfully; do not imply an account, device, editor or service is connected without verification.
- Preserve the approved nine mood frames and the shared Cage character. Metadata is in `pet.json`; frame paths are in `sprite-20261008.json`.
- New descriptive files use lowercase names with the owner's America/Denver date immediately before the extension. Required contract files keep their standard names.
- Keep release tags immutable. Publish fixes as a new commit and tag; never force-push or retag an existing release.
- Update README limits and `docs/publications-20261008.md` when implementation or publication status changes.

## Validation

Check the actual browser script syntax and meaningful user flows after behavior changes. Keep data local, and do not launch scanners against the owner's files merely to test an interface.

Cage: https://github.com/findastra/astras-pet-apps. Repository topic: `pet-app`.
