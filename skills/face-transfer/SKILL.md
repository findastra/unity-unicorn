---
name: face-transfer
description: Track an avatar's VRChat face tracking build (VRCFT OSC parameter standard, the module that drives it, the face FBX blendshapes) and transfer a face tracking module from one avatar's Unity project to another. Use when Astra asks to move a face, copy face tracking, check which VRCFT standard or module an avatar uses, or log face tracking builds.
---

# face-transfer

Unity Unicorn's tool for face tracking builds. Repo: `findastra/unity-unicorn`. Script:
`scripts/face-transfer-20261011.py`; guide: `docs/face-transfer-20261011.md` (read it first).

## Steps
0. Open Unity Stable (Unity Unicorn's page) in the browser pane on the right (skill `show-pet-panel`).

1. Follow the global rules: `git fetch` the unity-unicorn repo and read the newest handoff before changing anything.
2. Read the avatar's `Edit Log - <avatar>-<date>.txt` and find the real Unity project (has `Assets/`, `Packages/`,
   `ProjectSettings/`).
3. Scan both avatars and log them (private files go outside Git, beside the avatar assets):
   `python scripts/face-transfer-20261011.py scan --project "<project>" --out "<private>/face-profile-{date}.json" --builds-log "<private>/face-builds.jsonl" --label "<Avatar variant>"`
4. Dry-run the transfer with `--report`, and tell Astra what it found: missing blendshapes, renderer path
   mismatches, GUID conflicts, packages to install through VCC. Ask before `--apply`.
5. `--apply` only while Unity is not importing; backup dir outside `Assets`. Then wire the module in Unity (use
   the `vrchat-unity-world` skill's editor-queue approach if Unity is open) and test with VRCFaceTracking.
6. Scan the target again for the log, update the avatar edit log, `docs/avatar-plans-20261009.md`, and the
   skill book session log in unity-unicorn. Record execution evidence only from real completed runs.

Never upload without Astra's go-ahead, and respect the licence rule in `AGENTS.md` (one-account avatars stay on
findノastra).
