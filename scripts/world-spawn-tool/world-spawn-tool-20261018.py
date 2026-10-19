#!/usr/bin/env python3
"""world-spawn-tool: spawn a new VRChat world for Astra (a Unity Unicorn tool; moved here from findastra/liminal-doors on 2026-10-18).

Claude Opus 5.5, 2026-10-18. Grew out of how Liminal Doors was started on 2026-10-18.

First things to do for every new world (Astra, 2026-10-18), in this order:
  1. a PUBLIC GitHub repo named the way Astra's repos are named (lowercase words joined by hyphens), and
  2. a link to it, with a cute one-line summary, in the Findastra profile README (section "VR & VRChat"), by pull request.
Then:
  3. the Unity project skeleton, copied from a reference world that already works (packages and the embedded VRChat
     SDK, project settings renamed for the new world, the agent queue, a README).

Preview by default: nothing changes until a flag asks for it.
  python world-spawn-tool-20261018.py --name "Liminal Doors" --summary "..." --reference <world project> --dest-root <folder>
    --check          first-steps status only (read-only GitHub checks)
    --create-repo    step 1: create the public repo
    --profile-pr     step 2: open the profile README pull request (never merges it)
    --apply          step 3: write the Unity project skeleton (refuses until steps 1 and 2 are done)
Needs Python 3.9+, git, and the GitHub CLI (gh) signed in for steps 1 and 2.
"""
import argparse
import base64
import datetime as dt
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import uuid
from pathlib import Path

TOOL_ID = "world-spawn-tool"
VERSION = "0.1.0-20261018"
HERE = Path(__file__).resolve().parent
TEMPLATE = HERE / "templates" / "agent-queue-20261018.cs.txt"
PROFILE_SECTION = "### VR & VRChat"
GITIGNORE_MUST = ["/Review/", "/Packages/com.vrchat.base/", "/Packages/com.vrchat.worlds/"]
PACKAGE_FILES = ["manifest.json", "packages-lock.json", "vpm-manifest.json"]
ROOT_FILES = [".gitignore", ".gitattributes", ".vsconfig"]


# ---------------------------------------------------------------- names

def derive_repo(name):
    """Astra's repo names: the title's words, lowercase, joined by hyphens ("Astra's Infinite Pole" -> astras-infinite-pole)."""
    words = re.findall(r"[a-z0-9]+", name.replace("'", "").replace("’", "").lower())
    return "-".join(words)


def valid_repo(repo):
    return re.fullmatch(r"[a-z0-9]+(?:-[a-z0-9]+)*", repo or "") is not None


def code_name(name):
    """C# / menu name: the title's words in PascalCase ("Liminal Doors" -> LiminalDoors)."""
    words = re.findall(r"[A-Za-z0-9]+", name.replace("'", "").replace("’", ""))
    code = "".join(w[:1].upper() + w[1:] for w in words)
    return code if code[:1].isalpha() else "World" + code


def today():
    return dt.datetime.now().astimezone().strftime("%Y%m%d")   # Astra's PC runs on America/Denver


# ---------------------------------------------------------------- step 2: the profile README line

def profile_line(owner, repo, summary, platform=None):
    summary = " ".join(summary.split()).strip()
    if not summary:
        raise ValueError("The profile needs a summary: one cute line about the world.")
    tail = " " + platform.strip() if platform else ""
    return "- [`%s`](https://github.com/%s/%s) -- %s%s" % (repo, owner, repo, summary, tail)


def insert_profile_line(readme, line, owner, repo, after=None):
    """Adds `line` at the end of the VR & VRChat list (or right after the `after` repo's line).
    Keeps the file's own line endings. Returns (new text, changed?)."""
    if "github.com/%s/%s)" % (owner, repo) in readme:
        return readme, False
    nl = "\r\n" if "\r\n" in readme else "\n"
    lines = readme.split(nl)
    try:
        start = next(i for i, l in enumerate(lines) if l.strip() == PROFILE_SECTION)
    except StopIteration:
        raise ValueError("The profile README has no '%s' section." % PROFILE_SECTION)
    items = []
    for i in range(start + 1, len(lines)):
        s = lines[i].strip()
        if s.startswith("#"):
            break
        if s.startswith("- "):
            items.append(i)
        elif s and items:
            break
    if not items:
        at = start + 1
    elif after:
        hit = [i for i in items if "/%s/%s)" % (owner, after) in lines[i]]
        at = (hit[0] if hit else items[-1]) + 1
    else:
        at = items[-1] + 1
    lines.insert(at, line)
    return nl.join(lines), True


# ---------------------------------------------------------------- GitHub (gh)

def gh(*args, check=True):
    p = subprocess.run(["gh", *args], capture_output=True, text=True, encoding="utf-8")
    # A signed-out gh must never read as "repo missing". The Microsoft Store Python runs sandboxed and may not see the
    # sign-in that gh has in a normal shell; GH_TOKEN (for example GH_TOKEN=$(gh auth token)) gets past that.
    if p.returncode != 0 and "gh auth login" in (p.stderr or ""):
        raise RuntimeError("gh is not signed in for this process. Run `gh auth login`, or set GH_TOKEN for this run.")
    if check and p.returncode != 0:
        raise RuntimeError("gh %s failed: %s" % (" ".join(args[:3]), (p.stderr or p.stdout).strip()))
    return p


def first_steps_status(owner, repo):
    """Read-only. Step 1: the repo exists and is public. Step 2: the profile README links it, or a PR adding it is open."""
    st = {"repo": "missing", "repo_url": None, "profile": "missing", "profile_pr": None}
    p = gh("repo", "view", "%s/%s" % (owner, repo), "--json", "visibility,url", check=False)
    if p.returncode == 0:
        info = json.loads(p.stdout)
        st["repo_url"] = info["url"]
        st["repo"] = "public" if info.get("visibility", "").upper() == "PUBLIC" else info.get("visibility", "?").lower()
    p = gh("api", "repos/%s/%s/contents/README.md" % (owner, owner), "--jq", ".content", check=False)
    if p.returncode == 0:
        text = base64.b64decode(p.stdout.strip()).decode("utf-8", "replace")
        if "github.com/%s/%s)" % (owner, repo) in text:
            st["profile"] = "linked"
    if st["profile"] != "linked":
        p = gh("pr", "list", "-R", "%s/%s" % (owner, owner), "--state", "open", "--json", "headRefName,url", check=False)
        if p.returncode == 0:
            for pr in json.loads(p.stdout or "[]"):
                if pr["headRefName"].startswith("claude/%s-link-" % repo) or pr["headRefName"].startswith("%s-link" % repo):
                    st["profile"], st["profile_pr"] = "pr-open", pr["url"]
    return st


def create_repo(owner, repo, summary):
    if first_steps_status(owner, repo)["repo"] != "missing":
        raise RuntimeError("%s/%s already exists; not creating it again." % (owner, repo))
    gh("repo", "create", "%s/%s" % (owner, repo), "--public", "--description", " ".join(summary.split()))
    return "https://github.com/%s/%s" % (owner, repo)


def profile_pr(owner, repo, name, line, after=None):
    branch = "claude/%s-link-%s" % (repo, today())
    with tempfile.TemporaryDirectory(prefix="world-spawn-profile-") as tmp:
        work = Path(tmp) / "profile"
        gh("repo", "clone", "%s/%s" % (owner, owner), str(work), "--", "-q", "--depth", "1")
        readme = work / "README.md"
        raw = readme.read_bytes().decode("utf-8")
        new, changed = insert_profile_line(raw, line, owner, repo, after)
        if not changed:
            return None
        readme.write_bytes(new.encode("utf-8"))
        git = lambda *a: subprocess.run(["git", "-C", str(work), *a], check=True, capture_output=True, text=True)
        git("checkout", "-q", "-b", branch)
        git("add", "README.md")
        git("commit", "-q", "-m", "Profile: link %s under VR & VRChat" % name, "-m",
            "Spawned with %s %s." % (TOOL_ID, VERSION))
        git("push", "-q", "-u", "origin", branch)
        body = ("Adds [%s/%s](https://github.com/%s/%s) to the **VR & VRChat** list:\n\n> %s\n\n"
                "One line added; nothing else changed. Opened by %s %s; merging is Astra's call."
                % (owner, repo, owner, repo, line.split(" -- ", 1)[-1], TOOL_ID, VERSION))
        p = gh("pr", "create", "-R", "%s/%s" % (owner, owner), "--base", "main", "--head", branch,
               "--title", "Profile: link %s under VR & VRChat" % name, "--body", body)
        return p.stdout.strip().splitlines()[-1]


# ---------------------------------------------------------------- step 3: the Unity project skeleton

def rename_settings(text, product_name):
    text, n1 = re.subn(r"(?m)^(  productName: ).*$", lambda m: m.group(1) + product_name, text)
    text, n2 = re.subn(r"(?m)^(  productGUID: )[0-9a-f]{32}\s*$", lambda m: m.group(1) + uuid.uuid4().hex, text)
    if n1 != 1 or n2 != 1:
        raise ValueError("ProjectSettings.asset: expected one productName and one productGUID (found %d, %d)." % (n1, n2))
    return text


EMPTY_BUILD_SETTINGS = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1045 &1
EditorBuildSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Scenes: []
  m_configObjects: {}
"""

ASMDEF = """{
    "name": "%s.Editor",
    "rootNamespace": "",
    "references": [],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
"""


def long_path(p):
    """The VRChat SDK's folders are deep; on Windows, copy through \\\\?\\ paths so a long destination doesn't hit MAX_PATH."""
    p = os.path.abspath(str(p))
    return "\\\\?\\" + p if os.name == "nt" and not p.startswith("\\\\?\\") else p


def copy_tree(src, dst):
    shutil.copytree(long_path(src), long_path(dst))


def unity_version(reference):
    m = re.search(r"m_EditorVersion: (\S+)", (reference / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8"))
    return m.group(1) if m else "unknown"


def plan_skeleton(reference, dest):
    """What --apply would copy and write, as (kind, relative path) rows."""
    rows = []
    for f in PACKAGE_FILES:
        if (reference / "Packages" / f).is_file():
            rows.append(("copy", "Packages/" + f))
    for d in sorted((reference / "Packages").glob("com.vrchat.*")):
        if d.is_dir():
            rows.append(("copy dir", "Packages/" + d.name))
    rows.append(("copy dir", "ProjectSettings (productName, new productGUID, empty build settings)"))
    for f in ROOT_FILES:
        if (reference / f).is_file():
            rows.append(("copy", f))
    return rows


def apply_skeleton(name, repo, summary, reference, dest, date=None):
    date = date or today()
    reference, dest = Path(reference), Path(dest)
    for need in ("ProjectSettings/ProjectVersion.txt", "ProjectSettings/ProjectSettings.asset", "Packages/manifest.json"):
        if not (reference / need).is_file():
            raise ValueError("Reference is not a Unity project (missing %s): %s" % (need, reference))
    if dest.exists():
        raise ValueError("Destination already exists; the tool never writes into an existing folder: %s" % dest)
    code = code_name(name)
    written = []
    (dest / "Packages").mkdir(parents=True)
    for f in PACKAGE_FILES:
        if (reference / "Packages" / f).is_file():
            shutil.copy2(reference / "Packages" / f, dest / "Packages" / f); written.append("Packages/" + f)
    for d in sorted((reference / "Packages").glob("com.vrchat.*")):
        if d.is_dir():
            copy_tree(d, dest / "Packages" / d.name); written.append("Packages/" + d.name + "/")
    copy_tree(reference / "ProjectSettings", dest / "ProjectSettings"); written.append("ProjectSettings/")
    ps = dest / "ProjectSettings" / "ProjectSettings.asset"
    ps.write_bytes(rename_settings(ps.read_bytes().decode("utf-8"), name).encode("utf-8"))
    (dest / "ProjectSettings" / "EditorBuildSettings.asset").write_text(EMPTY_BUILD_SETTINGS, encoding="utf-8", newline="\n")
    for f in ROOT_FILES:
        if (reference / f).is_file():
            shutil.copy2(reference / f, dest / f); written.append(f)
    gi = dest / ".gitignore"
    have = gi.read_text(encoding="utf-8").splitlines() if gi.exists() else []
    missing = [l for l in GITIGNORE_MUST if l not in have]
    if missing:
        with gi.open("a", encoding="utf-8", newline="\n") as fh:
            fh.write(("\n" if have and have[-1] else "") + "# world-spawn-tool: review output and the embedded VRChat SDK stay local\n" + "\n".join(missing) + "\n")
        if ".gitignore" not in written: written.append(".gitignore")

    editor = dest / "Assets" / code / "Editor"
    editor.mkdir(parents=True)
    queue = TEMPLATE.read_text(encoding="utf-8").replace("{{CLASS}}", code + "AgentQueue").replace("{{MENU}}", code).replace("{{DATE}}", date)
    if "{{" in queue:
        raise ValueError("Agent queue template has an unfilled placeholder.")
    (editor / (code + "AgentQueue.cs")).write_text(queue, encoding="utf-8", newline="\n"); written.append("Assets/%s/Editor/%sAgentQueue.cs" % (code, code))
    (editor / (code + ".Editor.asmdef")).write_text(ASMDEF % code, encoding="utf-8", newline="\n"); written.append("Assets/%s/Editor/%s.Editor.asmdef" % (code, code))

    readme = ("# %s\n\n%s\n\n## Working in it\n\n"
              "- Unity %s with the VRChat Worlds SDK embedded in `Packages/` (not committed), project settings from the "
              "reference world `%s`. Spawned with %s %s on %s.\n"
              "- Agents drive an open editor through `Review/agent-queue/*.cmd` (`status`, `refresh`, `save`, `menu %s/...`). "
              "Never relaunch or batchmode Unity while the project is open.\n"
              "- Uploads, releases and `main` change only with Astra's OK.\n"
              % (name, " ".join(summary.split()), unity_version(reference), reference.name, TOOL_ID, VERSION, date, code))
    (dest / "README.md").write_text(readme, encoding="utf-8", newline="\n"); written.append("README.md")

    # Unity Unicorn usage record: private run history, so it goes under Review/ (ignored), never into Git.
    usage = {"schema": "unity-unicorn.tool-usage/v1", "tool_id": TOOL_ID, "tool_version": VERSION,
             "project": {"name": name}, "completed_at": dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
             "status": "completed", "summary": "Spawned the Unity project skeleton for %s (%d items)." % (name, len(written)),
             "evidence": "Written by %s after the skeleton was copied: %s" % (TOOL_ID, ", ".join(written))}
    rec = dest / "Review" / "world-spawn" / ("%s-usage-%s.json" % (TOOL_ID, date))
    rec.parent.mkdir(parents=True)
    rec.write_text(json.dumps(usage, indent=2) + "\n", encoding="utf-8", newline="\n")
    return code, written, rec


# ---------------------------------------------------------------- command line

def main(argv=None):
    ap = argparse.ArgumentParser(description="Spawn a new VRChat world: public repo, profile link, Unity project skeleton.")
    ap.add_argument("--name", required=True, help='World title in Title Case, e.g. "Liminal Doors"')
    ap.add_argument("--repo", help="GitHub repo name (default: derived from --name, lowercase words joined by hyphens)")
    ap.add_argument("--owner", default="findastra")
    ap.add_argument("--summary", help="One cute line about the world, for the repo description and the profile README")
    ap.add_argument("--platform", help='Optional tail for the profile line, e.g. "PC VR / desktop"')
    ap.add_argument("--profile-after", help="Put the profile line right after this repo's line (default: end of the list)")
    ap.add_argument("--reference", help="A working Unity world project to copy packages and settings from (read only)")
    ap.add_argument("--dest-root", help="Folder that holds project folders; the project goes in <dest-root>/<repo>")
    ap.add_argument("--check", action="store_true", help="first-steps status only")
    ap.add_argument("--create-repo", action="store_true", help="step 1: create the public GitHub repo")
    ap.add_argument("--profile-pr", action="store_true", help="step 2: open the profile README pull request")
    ap.add_argument("--apply", action="store_true", help="step 3: write the Unity project skeleton")
    ap.add_argument("--skip-first-steps-check", action="store_true", help="let --apply run without GitHub (offline); say so in the handoff")
    a = ap.parse_args(argv)

    repo = a.repo or derive_repo(a.name)
    if not valid_repo(repo):
        ap.error("repo name must be lowercase words joined by hyphens: %r" % repo)
    if a.repo and a.repo != derive_repo(a.name):
        print("note: --repo %s differs from the name-derived %s; names come from GitHub, so make sure it is the real one." % (a.repo, derive_repo(a.name)))
    print("%s %s: %s -> %s/%s (code name %s)" % (TOOL_ID, VERSION, a.name, a.owner, repo, code_name(a.name)))

    line = profile_line(a.owner, repo, a.summary, a.platform) if a.summary else None
    st = None
    if not a.skip_first_steps_check:
        try:
            st = first_steps_status(a.owner, repo)
        except FileNotFoundError:
            print("gh (GitHub CLI) is not installed; first steps cannot be checked.")
        except RuntimeError as e:
            print("first steps cannot be checked: %s" % e)
    if st:
        print("FIRST STEP 1, public repo:     " + {"public": "done (%s)" % st["repo_url"], "missing": "TO DO (--create-repo)"}.get(st["repo"], "NOT PUBLIC (%s), fix on GitHub" % st["repo"]))
        print("FIRST STEP 2, profile link:    " + {"linked": "done (in the profile README)", "pr-open": "PR open, waiting for Astra (%s)" % st["profile_pr"], "missing": "TO DO (--profile-pr)"}[st["profile"]])
    if line:
        print("profile line: " + line)
    if a.check:
        return 0

    if a.create_repo:
        if not a.summary: ap.error("--create-repo needs --summary (it becomes the repo description)")
        print("step 1: created " + create_repo(a.owner, repo, a.summary))
        st = first_steps_status(a.owner, repo)
    if a.profile_pr:
        if not line: ap.error("--profile-pr needs --summary")
        url = profile_pr(a.owner, repo, a.name, line, a.profile_after)
        print("step 2: " + ("opened " + url if url else "the profile already links %s; nothing to do" % repo))
        st = first_steps_status(a.owner, repo)

    if not (a.reference and a.dest_root):
        if a.apply: ap.error("--apply needs --reference and --dest-root")
        print("step 3 (Unity skeleton): give --reference and --dest-root to preview it.")
        return 0
    reference, dest = Path(a.reference).resolve(), Path(a.dest_root).resolve() / repo
    if not a.apply:
        print("step 3 preview: %s -> %s (Unity %s)" % (reference.name, dest, unity_version(reference) if (reference / "ProjectSettings").is_dir() else "?"))
        for kind, rel in plan_skeleton(reference, dest):
            print("   %-8s %s" % (kind, rel))
        print("   write    Assets/%s/Editor/%sAgentQueue.cs, %s.Editor.asmdef, README.md, Review/world-spawn/ usage record" % ((code_name(a.name),) * 3))
        print("Nothing written. Add --apply to write it.")
        return 0
    if not a.skip_first_steps_check and not (st and st["repo"] == "public" and st["profile"] in ("linked", "pr-open")):
        print("REFUSED: do the first steps first (public repo, then the profile link PR), or pass --skip-first-steps-check.")
        return 2
    if not a.summary: ap.error("--apply needs --summary (it starts the README)")
    code, written, rec = apply_skeleton(a.name, repo, a.summary, reference, dest)
    print("step 3: wrote %s (%d items). Usage record: %s" % (dest, len(written), rec))
    print("next: copy reused assets by GUID (Tools/infinite-pole-export), open the project in Unity once, "
          "git init, commit on claude/<topic>-<date>, add origin https://github.com/%s/%s.git and push." % (a.owner, repo))
    return 0


if __name__ == "__main__":
    sys.exit(main())
