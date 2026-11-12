# Unity Stable keeps the masters of its three Unity skills

2026-11-12. Astra answered Luna's Log's skill-files deck "all recommended" (lunas-log card 011): each skill's master
lives in the pet that owns it, and blender-unity-assets belongs to Unity Stable. Until today these three lived only on
her PC, so the other assistant could not see them.

- `skills/face-transfer/SKILL.md` (from Claude's skills folder, unchanged; trick id `face-transfer-trick`, because
  `face-transfer` is already the L1 tool it uses).
- `skills/vrchat-unity-world/SKILL.md` (from Claude's skills folder, unchanged).
- `skills/blender-unity-assets/SKILL.md` (from Codex's skills folder; machine paths written as `%USERPROFILE%` and
  `%LOCALAPPDATA%`, and the collider pointer names the vrchat-unity-world skill instead of a path).
- `pet.json` L2 `tricks` and the page's L2 list name all three.

After this merges, Luna's Log's `skill-install-tool` installs them for Claude (`~/.claude/skills`) and Codex
(`~/.agents/skills`) from here; never copy them by hand.

Claude Opus 5.5 (`claude-opus-5-5`), 2026-11-12.
