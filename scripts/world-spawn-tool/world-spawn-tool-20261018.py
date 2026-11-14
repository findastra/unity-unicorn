#!/usr/bin/env python3
"""world-spawn-tool: spawn a new VRChat world for Astra (a Unity Unicorn tool; moved here from findastra/liminal-doors on 2026-10-18).

Claude Opus 5.5, 2026-10-18. Grew out of how Liminal Doors was started on 2026-10-18. Step 3 starts from VCC's World
template since 0.2.0 (Claude Opus 5.5, 2026-10-19).

First things to do for every new world (Astra, 2026-10-18), in this order:
  1. a PUBLIC GitHub repo named the way Astra's repos are named (lowercase words joined by hyphens), and
  2. a link to it, with a cute one-line summary, in the Findastra profile README (section "VR & VRChat"), by pull request.
Then:
  3. the Unity project skeleton, fresh from VRChat Creator Companion's official World template plus the VRChat SDK zips
     in VCC's package cache (productName and a new productGUID for the world, empty build settings, the agent queue, a
     README). It never copies another world: new worlds start fresh (Astra, 2026-10-18).

Preview by default: nothing changes until a flag asks for it.
  python world-spawn-tool-20261018.py --name "Liminal Doors" --summary "..." --dest-root <folder>
    --check          first-steps status only (read-only GitHub checks)
    --create-repo    step 1: create the public repo
    --profile-pr     step 2: open the profile README pull request (never merges it)
    --apply          step 3: write the Unity project skeleton (refuses until steps 1 and 2 are done)
Needs Python 3.9+, git, and the GitHub CLI (gh). When gh can't see its sign-in from Python (Microsoft Store Python), the
read-only checks use GitHub's public API and the tool prints the exact gh command to run from the shell. It never asks
for a token.
"""
import argparse
import base64
import datetime as dt
import itertools
import json
import os
import re
import shlex
import shutil
import subprocess
import sys
import tempfile
import urllib.error
import urllib.request
import uuid
import zipfile
from pathlib import Path

TOOL_ID = "world-spawn-tool"
VERSION = "0.2.0-20261019"
HERE = Path(__file__).resolve().parent
AGENT_QUEUE_TEMPLATE = HERE / "templates" / "agent-queue-20261018.cs.txt"
PROFILE_SECTION = "### VR & VRChat"
GITIGNORE_MUST = ["/Review/", "/Packages/com.vrchat.base/", "/Packages/com.vrchat.worlds/"]
VCC_TEMPLATE_ID = "com.vrchat.template.world"
TEMPLATE_OWN_FILES = {"package.json", "README.md"}           # the template's own metadata, not part of a project
UNITY_FOLDERS = ("Assets", "Packages", "ProjectSettings", "Library")
WINDOWS_MAX_PATH = 260                                       # Unity can't import deeper files while long paths are off


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


# ---------------------------------------------------------------- GitHub: gh, or the public API when gh is signed out

class GhSignedOut(RuntimeError):
    """gh can't see its sign-in from this process."""


GH_SIGNED_OUT = (
    "gh can't see its sign-in from this Python. Microsoft Store Python gets its own private copy of %APPDATA%, so gh "
    "started from it can't find %APPDATA%\\GitHub CLI\\hosts.yml, the file that says who is signed in (the token itself "
    "is in the Windows keyring). The shell's gh is fine: run the gh commands below from Git Bash. git push from Python "
    "still works, because git has its own credential helper. Never ask Astra for a token.")


def shell_command(*args):
    """A command line to paste into Git Bash (or any POSIX shell)."""
    return " ".join(shlex.quote(str(a)) for a in args)


def gh(*args, check=True):
    p = subprocess.run(["gh", *args], capture_output=True, text=True, encoding="utf-8")
    # A signed-out gh must never read as "repo missing".
    err = p.stderr or ""
    if p.returncode != 0 and ("gh auth login" in err or "not logged in" in err):
        raise GhSignedOut("gh is not signed in for this process")
    if check and p.returncode != 0:
        raise RuntimeError("gh %s failed: %s" % (" ".join(args[:3]), (p.stderr or p.stdout).strip()))
    return p


def http_get_json(url):
    """GET from GitHub's public REST API, signed out (60 requests an hour; public repos only). None on 404."""
    req = urllib.request.Request(url, headers={"Accept": "application/vnd.github+json", "User-Agent": TOOL_ID})
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            return json.loads(r.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        if e.code == 404:
            return None
        raise RuntimeError("GitHub's public API answered %s for %s" % (e.code, url))
    except urllib.error.URLError as e:
        raise RuntimeError("GitHub's public API can't be reached: %s" % e.reason)


def gh_facts(owner, repo):
    facts = {"repo": None, "readme": None, "prs": []}
    p = gh("repo", "view", "%s/%s" % (owner, repo), "--json", "visibility,url", check=False)
    if p.returncode == 0:
        info = json.loads(p.stdout)
        facts["repo"] = {"visibility": info.get("visibility", "?"), "url": info["url"]}
    p = gh("api", "repos/%s/%s/contents/README.md" % (owner, owner), "--jq", ".content", check=False)
    if p.returncode == 0:
        facts["readme"] = base64.b64decode(p.stdout.strip()).decode("utf-8", "replace")
    p = gh("pr", "list", "-R", "%s/%s" % (owner, owner), "--state", "open", "--json", "headRefName,url", check=False)
    if p.returncode == 0:
        facts["prs"] = [(pr["headRefName"], pr["url"]) for pr in json.loads(p.stdout or "[]")]
    return facts


def public_facts(owner, repo):
    facts = {"repo": None, "readme": None, "prs": []}
    api = "https://api.github.com/repos/"
    info = http_get_json(api + "%s/%s" % (owner, repo))
    if info:
        facts["repo"] = {"visibility": info.get("visibility") or ("private" if info.get("private") else "public"), "url": info["html_url"]}
    readme = http_get_json(api + "%s/%s/contents/README.md" % (owner, owner))
    if readme and readme.get("content"):
        facts["readme"] = base64.b64decode(readme["content"]).decode("utf-8", "replace")
    pulls = http_get_json(api + "%s/%s/pulls?state=open&per_page=100" % (owner, owner)) or []
    facts["prs"] = [(pr["head"]["ref"], pr["html_url"]) for pr in pulls]
    return facts


def first_steps_status(owner, repo, public=False):
    """Read-only. Step 1: the repo exists and is public. Step 2: the profile README links it, or a PR adding it is open.
    public=True reads GitHub's public API instead of gh; it can't see private repos, so a private one reads as missing."""
    facts = public_facts(owner, repo) if public else gh_facts(owner, repo)
    st = {"repo": "missing", "repo_url": None, "profile": "missing", "profile_pr": None,
          "source": "GitHub's public API" if public else "gh"}
    if facts["repo"]:
        st["repo_url"] = facts["repo"]["url"]
        vis = facts["repo"]["visibility"].lower()
        st["repo"] = "public" if vis == "public" else vis
    if facts["readme"] and "github.com/%s/%s)" % (owner, repo) in facts["readme"]:
        st["profile"] = "linked"
    else:
        for head, url in facts["prs"]:
            if head.startswith("claude/%s-link-" % repo) or head.startswith("%s-link" % repo):
                st["profile"], st["profile_pr"] = "pr-open", url
    return st


def check_commands(owner, repo):
    """The read-only checks, for the shell."""
    return [shell_command("gh", "repo", "view", "%s/%s" % (owner, repo), "--json", "visibility,url"),
            shell_command("gh", "api", "repos/%s/%s/contents/README.md" % (owner, owner), "-H", "Accept: application/vnd.github.raw")
            + " | " + shell_command("grep", "-F", "github.com/%s/%s)" % (owner, repo)),
            shell_command("gh", "pr", "list", "-R", "%s/%s" % (owner, owner), "--state", "open", "--search", repo)]


def create_repo_args(owner, repo, summary):
    return ("gh", "repo", "create", "%s/%s" % (owner, repo), "--public", "--description", " ".join(summary.split()))


def create_repo(owner, repo, summary):
    if first_steps_status(owner, repo)["repo"] != "missing":
        raise RuntimeError("%s/%s already exists; not creating it again." % (owner, repo))
    gh(*create_repo_args(owner, repo, summary)[1:])
    return "https://github.com/%s/%s" % (owner, repo)


def profile_pr(owner, repo, name, line, after=None):
    """Python does the README and git (git push works from any Python); gh opens the pull request.
    Returns ("opened", url), ("run", gh command for the shell) when gh is signed out here, or (None, None) when the
    profile already links the repo."""
    branch = "claude/%s-link-%s" % (repo, today())
    with tempfile.TemporaryDirectory(prefix="world-spawn-profile-") as tmp:
        work = Path(tmp) / "profile"
        subprocess.run(["git", "clone", "-q", "--depth", "1", "https://github.com/%s/%s.git" % (owner, owner), str(work)],
                       check=True, capture_output=True, text=True)
        readme = work / "README.md"
        raw = readme.read_bytes().decode("utf-8")
        new, changed = insert_profile_line(raw, line, owner, repo, after)
        if not changed:
            return None, None
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
    args = ("pr", "create", "-R", "%s/%s" % (owner, owner), "--base", "main", "--head", branch,
            "--title", "Profile: link %s under VR & VRChat" % name, "--body", body)
    try:
        p = gh(*args)
    except (GhSignedOut, FileNotFoundError):
        return "run", shell_command("gh", *args)
    return "opened", p.stdout.strip().splitlines()[-1]


# ---------------------------------------------------------------- step 3: the Unity project skeleton, from VCC's World template

def vcc_home():
    return Path(os.environ.get("LOCALAPPDATA") or Path.home() / "AppData" / "Local") / "VRChatCreatorCompanion"


def default_template():
    return vcc_home() / "VRCTemplates" / "World"


def default_repos():
    return vcc_home() / "Repos"


def parse_version(v):
    """'3.10.5' -> (3, 10, 5). None for anything else, pre-releases included (VCC hides those by default)."""
    m = re.fullmatch(r"(\d+)\.(\d+)\.(\d+)", str(v or "").strip())
    return tuple(int(x) for x in m.groups()) if m else None


def version_matches(version, spec):
    """The VPM ranges VCC's templates and the SDK use: '~3.10.x', '3.10.x', '~3.10.5', '^3.10.0', '>=3.10.0', '3.10.5'.
    A bare full version means exactly that version: the VRChat SDK packages ship in lockstep."""
    v = parse_version(version)
    m = re.fullmatch(r"(~|\^|>=)?(\d+)(?:\.(\d+|[x*]))?(?:\.(\d+|[x*]))?", str(spec or "").strip())
    if not m:
        raise ValueError("Unsupported VPM version range %r." % spec)
    if v is None:
        return False
    fixed = []
    for part in m.group(2, 3, 4):
        if part is None or part in ("x", "*"):
            break
        fixed.append(int(part))
    low = tuple(fixed) + (0,) * (3 - len(fixed))
    op = m.group(1)
    if op == ">=":
        return v >= low
    if op == "^":
        return v >= low and v[0] == low[0]
    if op == "~":
        return v >= low and v[:min(2, len(fixed))] == low[:min(2, len(fixed))]
    return v[:len(fixed)] == tuple(fixed)


def unity_version(project):
    m = re.search(r"m_EditorVersion: (\S+)", (Path(project) / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8"))
    return m.group(1) if m else "unknown"


def read_template(template):
    """Checks that `template` is VCC's World template. Returns (its package.json, the SDK ranges its vpm-manifest asks for)."""
    template = Path(template)
    need = ["package.json", "Packages/manifest.json", "Packages/vpm-manifest.json",
            "ProjectSettings/ProjectVersion.txt", "ProjectSettings/ProjectSettings.asset"]
    missing = [n for n in need if not (template / n).is_file()]
    if missing:
        raise ValueError("VCC's World template is not at %s (missing %s). Install VRChat Creator Companion, or pass --template."
                         % (template, ", ".join(missing)))
    info = json.loads((template / "package.json").read_text(encoding="utf-8-sig"))
    if info.get("name") != VCC_TEMPLATE_ID:
        raise ValueError("%s is not VCC's World template (package.json name %r, not %s)." % (template, info.get("name"), VCC_TEMPLATE_ID))
    deps = json.loads((template / "Packages" / "vpm-manifest.json").read_text(encoding="utf-8-sig")).get("dependencies", {})
    wanted = {n: (d.get("version") if isinstance(d, dict) else d) for n, d in deps.items()}
    if "com.vrchat.worlds" not in wanted:
        raise ValueError("The template's vpm-manifest.json doesn't ask for com.vrchat.worlds.")
    return info, wanted


def template_files(template):
    """The template's files that become the project: all of them except its own package.json and README.md."""
    template = Path(template)
    rels = (p.relative_to(template).as_posix() for p in template.rglob("*") if p.is_file())
    return sorted(r for r in rels if r not in TEMPLATE_OWN_FILES)


def cached_packages(repos, name):
    """{version: (zip, vpmDependencies)} for package `name` in VCC's Repos cache, read from each zip's own package.json."""
    found = {}
    folder = Path(repos) / name
    for z in sorted(folder.glob("*.zip")) if folder.is_dir() else []:
        try:
            with zipfile.ZipFile(z) as zf:
                info = json.loads(zf.read("package.json").decode("utf-8-sig"))
        except (zipfile.BadZipFile, KeyError, ValueError, OSError):
            continue
        if info.get("name") == name and parse_version(info.get("version")):
            found[info["version"]] = (z, info.get("vpmDependencies") or {})
    return found


def resolve_sdk(wanted, repos):
    """One cached zip per package the template asks for: the newest set where every version matches the template's range
    and every package's vpmDependencies. Returns {name: {"version", "zip", "vpm"}} in the template's order."""
    cache = {n: cached_packages(repos, n) for n in wanted}
    options = []
    for n, spec in wanted.items():
        ok = sorted((v for v in cache[n] if version_matches(v, spec)), key=parse_version, reverse=True)
        if not ok:
            have = ", ".join(sorted(cache[n], key=parse_version)) or "none"
            raise ValueError("VCC's package cache has no %s matching %s (cached: %s). Open VCC once so it downloads the SDK."
                             % (n, spec, have))
        options.append([(n, v) for v in ok])
    unmet = []
    for combo in itertools.product(*options):
        pick = dict(combo)
        gaps = ["%s %s needs %s %s" % (n, v, d, s) for n, v in combo for d, s in cache[n][v][1].items()
                if d not in pick or not version_matches(pick[d], s)]
        if not gaps:
            return {n: {"version": v, "zip": cache[n][v][0], "vpm": cache[n][v][1]} for n, v in combo}
        unmet += [g for g in gaps if g not in unmet]
    raise ValueError("No set of cached SDK packages fits together (%s). Open VCC once so it downloads the matching ones."
                     % "; ".join(unmet))


def plan_skeleton(template, repos):
    """What --apply builds, read only: the template's version and files, its Unity version, and the SDK zips it unpacks."""
    info, wanted = read_template(template)
    return {"template_version": info.get("version", "?"), "unity": unity_version(template),
            "files": template_files(template), "sdk": resolve_sdk(wanted, repos)}


def sdk_text(plan):
    return ", ".join("%s %s" % (n, p["version"]) for n, p in plan["sdk"].items())


def longest_sdk_path(dest, plan):
    longest = 0
    for n, p in plan["sdk"].items():
        with zipfile.ZipFile(p["zip"]) as zf:
            for m in zf.namelist():
                longest = max(longest, len(os.path.abspath(os.path.join(str(dest), "Packages", n, *m.rstrip("/").split("/")))))
    return longest


def check_dest(dest):
    """The project goes in a new folder, or in a folder with no Unity project yet (a fresh clone of the world's repo).
    Returns True when the folder is already there."""
    dest = Path(dest)
    if not dest.exists():
        return False
    if not dest.is_dir():
        raise ValueError("Destination is a file: %s" % dest)
    there = [f for f in UNITY_FOLDERS if (dest / f).exists()]
    if there:
        raise ValueError("Destination already holds a Unity project (%s); the tool never writes into one: %s" % (", ".join(there), dest))
    return True


def rename_settings(text, product_name):
    text, n1 = re.subn(r"(?m)^(  productName: )[^\r\n]*", lambda m: m.group(1) + product_name, text)
    text, n2 = re.subn(r"(?m)^(  productGUID: )[0-9a-f]{32}(?=\r?$)", lambda m: m.group(1) + uuid.uuid4().hex, text)
    if n1 != 1 or n2 != 1:
        raise ValueError("ProjectSettings.asset: expected one productName and one productGUID (found %d, %d)." % (n1, n2))
    return text


def merge_gitignore(own, template_text):
    """The project's .gitignore: the folder's own (or else VCC's), plus any of VCC's rules and GITIGNORE_MUST it lacks.
    Keeps the file's line endings. Returns (text, changed?)."""
    text = template_text if own is None else own
    nl = "\r\n" if "\r\n" in text else "\n"
    have = {l.strip() for l in text.splitlines()}
    vcc = []
    for l in template_text.splitlines():
        if l.strip() and not l.lstrip().startswith("#") and l.strip() not in have and l.strip() not in vcc:
            vcc.append(l.strip())
    must = [l for l in GITIGNORE_MUST if l not in have]
    blocks = []
    if vcc:
        blocks.append(["# world-spawn-tool: VCC World template rules"] + vcc)
    if must:
        blocks.append(["# world-spawn-tool: review output and the embedded VRChat SDK stay local"] + must)
    if not blocks:
        return text, False
    if text and not text.endswith(nl):
        text += nl
    lead = nl if text.strip() and not text.endswith(nl + nl) else ""
    return text + lead + (nl + nl).join(nl.join(b) for b in blocks) + nl, True


def vpm_manifest(plan):
    """vpm-manifest.json the way VCC writes it once a project is made: exact versions, and the lock."""
    sdk = plan["sdk"]
    return {"dependencies": {n: {"version": p["version"]} for n, p in sdk.items()},
            "locked": {n: {"version": p["version"], "dependencies": dict(p["vpm"])} for n, p in sdk.items()}}


def unpack(zip_path, target):
    """Unpacks one SDK zip into Packages/<name>/. The SDK's folders are deep, so on Windows this writes through \\\\?\\ paths."""
    with zipfile.ZipFile(zip_path) as zf:
        for m in zf.namelist():
            parts = m.replace("\\", "/").split("/")
            if m.startswith(("/", "\\")) or ".." in parts or ":" in parts[0]:
                raise ValueError("Unsafe path in %s: %r" % (Path(zip_path).name, m))
        zf.extractall(long_path(target))


def long_path(p):
    p = os.path.abspath(str(p))
    return "\\\\?\\" + p if os.name == "nt" and not p.startswith("\\\\?\\") else p


EMPTY_BUILD_SETTINGS = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1045 &1
EditorBuildSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Scenes: []
  m_configObjects: {}
"""

GITATTRIBUTES = """# world-spawn-tool: Unity's text files stay LF; pictures, sound and models are binary
*.unity text eol=lf
*.prefab text eol=lf
*.mat text eol=lf
*.asset text eol=lf
*.meta text eol=lf
*.anim text eol=lf
*.controller text eol=lf
*.cs text eol=lf
*.asmdef text eol=lf
*.shader text eol=lf
*.md text eol=lf
*.png binary
*.jpg binary
*.tga binary
*.psd binary
*.exr binary
*.hdr binary
*.fbx binary
*.wav binary
*.ogg binary
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


def apply_skeleton(name, repo, summary, template, repos, dest, date=None):
    """Builds the project in `dest` from VCC's World template and the cached SDK. Never copies another world, never
    writes into a Unity project, and never overwrites a file that is already there (a fresh clone keeps its README;
    its .gitignore only gains the rules it lacks). Returns (code name, written, kept, usage record)."""
    date = date or today()
    template, dest = Path(template), Path(dest)
    plan = plan_skeleton(template, repos)                   # checks everything before the first write
    check_dest(dest)
    code = code_name(name)
    written, kept = [], []

    def put(rel, data):
        target = dest / rel
        if target.exists():
            kept.append(rel)
            return False
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data if isinstance(data, bytes) else data.encode("utf-8"))
        return True

    for rel in plan["files"]:
        if rel == ".gitignore":
            continue                                        # merged below
        data = (template / rel).read_bytes()
        if rel == "ProjectSettings/ProjectSettings.asset":
            data = rename_settings(data.decode("utf-8"), name).encode("utf-8")
        elif rel == "ProjectSettings/EditorBuildSettings.asset":
            data = EMPTY_BUILD_SETTINGS
        elif rel == "Packages/vpm-manifest.json":
            data = json.dumps(vpm_manifest(plan), indent=2) + "\n"
        put(rel, data)
    settings = sum(1 for r in plan["files"] if r.startswith("ProjectSettings/"))
    written.append("ProjectSettings/ (%d files from VCC's World template %s; productName %s, a new productGUID, empty build settings)"
                   % (settings, plan["template_version"], name))
    written += [r for r in plan["files"] if not r.startswith("ProjectSettings/") and r != ".gitignore" and r not in kept]
    if "Packages/vpm-manifest.json" in written:
        written[written.index("Packages/vpm-manifest.json")] = "Packages/vpm-manifest.json (locked to %s)" % sdk_text(plan)
    for n, p in plan["sdk"].items():
        unpack(p["zip"], dest / "Packages" / n)
        written.append("Packages/%s/ (%s from VCC's package cache; not committed)" % (n, p["version"]))

    gi = dest / ".gitignore"
    own = gi.read_bytes().decode("utf-8") if gi.exists() else None
    template_gi = (template / ".gitignore").read_bytes().decode("utf-8") if (template / ".gitignore").is_file() else ""
    text, changed = merge_gitignore(own, template_gi)
    if own is None or changed:
        gi.write_bytes(text.encode("utf-8"))
        written.append(".gitignore" if own is None else ".gitignore (added the rules it lacked)")
    else:
        kept.append(".gitignore")
    if put(".gitattributes", GITATTRIBUTES):
        written.append(".gitattributes")

    editor = "Assets/%s/Editor/" % code
    queue = AGENT_QUEUE_TEMPLATE.read_text(encoding="utf-8").replace("{{CLASS}}", code + "AgentQueue").replace("{{MENU}}", code).replace("{{DATE}}", date)
    if "{{" in queue:
        raise ValueError("Agent queue template has an unfilled placeholder.")
    for rel, data in ((editor + code + "AgentQueue.cs", queue), (editor + code + ".Editor.asmdef", ASMDEF % code)):
        if put(rel, data):
            written.append(rel)

    readme = ("# %s\n\n%s\n\n## Working in it\n\n"
              "- Unity %s, started fresh from VRChat Creator Companion's World template %s, with the VRChat SDK (%s) "
              "unpacked from VCC's package cache into `Packages/` (not committed). Spawned with %s %s on %s; nothing "
              "in it came from another world.\n"
              "- Agents drive an open editor through `Review/agent-queue/*.cmd` (`status`, `refresh`, `save`, `menu %s/...`). "
              "Never relaunch or batchmode Unity while the project is open.\n"
              "- A piece from another world comes in only when Astra names it.\n"
              "- Uploads, releases and `main` change only with Astra's OK.\n"
              % (name, " ".join(summary.split()), plan["unity"], plan["template_version"], sdk_text(plan), TOOL_ID, VERSION, date, code))
    if put("README.md", readme):
        written.append("README.md")

    # Unity Unicorn usage record: private run history, so it goes under Review/ (ignored), never into Git.
    usage = {"schema": "unity-unicorn.tool-usage/v1", "tool_id": TOOL_ID, "tool_version": VERSION,
             "project": {"name": name}, "completed_at": dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
             "status": "completed", "summary": "Spawned the Unity project skeleton for %s from VCC's World template %s (%d items)."
             % (name, plan["template_version"], len(written)),
             "evidence": "Written by %s from VCC's World template %s and the cached VRChat SDK (%s): %s%s"
             % (TOOL_ID, plan["template_version"], sdk_text(plan), "; ".join(written),
                (". Kept what was already there: " + ", ".join(kept)) if kept else "")}
    rec = dest / "Review" / "world-spawn" / ("%s-usage-%s.json" % (TOOL_ID, date))
    rec.parent.mkdir(parents=True, exist_ok=True)
    rec.write_text(json.dumps(usage, indent=2) + "\n", encoding="utf-8", newline="\n")
    return code, written, kept, rec


def describe_plan(plan, name, dest, existed):
    code = code_name(name)
    copied = [f for f in plan["files"] if not f.startswith("ProjectSettings/")]
    settings = len(plan["files"]) - len(copied)
    rows = ["step 3 preview: VCC World template %s (Unity %s) -> %s" % (plan["template_version"], plan["unity"], dest),
            "   copy     %s, ProjectSettings/ (%d files)" % (", ".join(copied), settings),
            "   unpack   %s from VCC's package cache into Packages/ (not committed)" % sdk_text(plan),
            "   set      productName %s, a new productGUID, empty build settings, vpm-manifest.json locked to the SDK above" % name,
            "   write    .gitattributes, Assets/%s/Editor/%sAgentQueue.cs, %s.Editor.asmdef, README.md, Review/world-spawn/ usage record"
            % (code, code, code)]
    if existed:
        rows.append("   note     %s is already there: files in it are kept; its .gitignore only gains the rules it lacks" % dest)
    longest = longest_sdk_path(dest, plan)
    if longest >= WINDOWS_MAX_PATH:
        rows.append("   WARNING  the deepest SDK file would be %d characters; Unity can't import paths of %d or more while "
                    "Windows long paths are off. Use a shorter --dest-root." % (longest, WINDOWS_MAX_PATH))
    return rows


# ---------------------------------------------------------------- command line

def main(argv=None):
    ap = argparse.ArgumentParser(description="Spawn a new VRChat world: public repo, profile link, Unity project skeleton.")
    ap.add_argument("--name", required=True, help='World title in Title Case, e.g. "Liminal Doors"')
    ap.add_argument("--repo", help="GitHub repo name (default: derived from --name, lowercase words joined by hyphens)")
    ap.add_argument("--owner", default="findastra")
    ap.add_argument("--summary", help="One cute line about the world, for the repo description and the profile README")
    ap.add_argument("--platform", help='Optional tail for the profile line, e.g. "PC VR / desktop"')
    ap.add_argument("--profile-after", help="Put the profile line right after this repo's line (default: end of the list)")
    ap.add_argument("--dest-root", help="Folder that holds project folders; the project goes in <dest-root>/<repo>")
    ap.add_argument("--template", help="VCC's World template (default: %%LOCALAPPDATA%%\\VRChatCreatorCompanion\\VRCTemplates\\World)")
    ap.add_argument("--vcc-repos", help="VCC's package cache with the SDK zips (default: %%LOCALAPPDATA%%\\VRChatCreatorCompanion\\Repos)")
    ap.add_argument("--reference", help=argparse.SUPPRESS)
    ap.add_argument("--check", action="store_true", help="first-steps status only")
    ap.add_argument("--create-repo", action="store_true", help="step 1: create the public GitHub repo")
    ap.add_argument("--profile-pr", action="store_true", help="step 2: open the profile README pull request")
    ap.add_argument("--apply", action="store_true", help="step 3: write the Unity project skeleton")
    ap.add_argument("--skip-first-steps-check", action="store_true", help="let --apply run without GitHub (offline); say so in the handoff")
    a = ap.parse_args(argv)
    if a.reference:
        ap.error("--reference is gone (0.2.0): new worlds start fresh from VCC's World template, never from another world. "
                 "Bring in only the pieces Astra names, after the skeleton is made.")

    repo = a.repo or derive_repo(a.name)
    if not valid_repo(repo):
        ap.error("repo name must be lowercase words joined by hyphens: %r" % repo)
    if a.repo and a.repo != derive_repo(a.name):
        print("note: --repo %s differs from the name-derived %s; names come from GitHub, so make sure it is the real one." % (a.repo, derive_repo(a.name)))
    print("%s %s: %s -> %s/%s (code name %s)" % (TOOL_ID, VERSION, a.name, a.owner, repo, code_name(a.name)))

    line = profile_line(a.owner, repo, a.summary, a.platform) if a.summary else None
    st, gh_here = None, True
    if not a.skip_first_steps_check:
        try:
            st = first_steps_status(a.owner, repo)
        except (FileNotFoundError, GhSignedOut) as e:
            gh_here = False
            print(GH_SIGNED_OUT if isinstance(e, GhSignedOut) else "gh (GitHub CLI) is not installed here.")
            print("Read-only checks from the shell:")
            for c in check_commands(a.owner, repo):
                print("  " + c)
            try:
                st = first_steps_status(a.owner, repo, public=True)
            except RuntimeError as e2:
                print("first steps cannot be checked: %s" % e2)
        except RuntimeError as e:
            print("first steps cannot be checked: %s" % e)
    if st:
        print("FIRST STEP 1, public repo:     " + {"public": "done (%s)" % st["repo_url"],
              "missing": "TO DO (--create-repo)" + (" (or private: the public API can't see private repos)" if st["source"] != "gh" else "")
              }.get(st["repo"], "NOT PUBLIC (%s), fix on GitHub" % st["repo"]))
        print("FIRST STEP 2, profile link:    " + {"linked": "done (in the profile README)", "pr-open": "PR open, waiting for Astra (%s)" % st["profile_pr"], "missing": "TO DO (--profile-pr)"}[st["profile"]])
        if st["source"] != "gh":
            print("(checked through %s)" % st["source"])
    if line:
        print("profile line: " + line)
    if a.check:
        return 0

    if a.create_repo:
        if not a.summary: ap.error("--create-repo needs --summary (it becomes the repo description)")
        run_it = "step 1: run this from the shell (gh can't sign in from this Python):\n  " + shell_command(*create_repo_args(a.owner, repo, a.summary))
        if st and st["repo"] != "missing":
            print("step 1: %s/%s is already there; not creating it again." % (a.owner, repo))
        elif not gh_here:
            print(run_it)
        else:
            try:
                print("step 1: created " + create_repo(a.owner, repo, a.summary))
                st = first_steps_status(a.owner, repo)
            except (GhSignedOut, FileNotFoundError):
                print(run_it)
    if a.profile_pr:
        if not line: ap.error("--profile-pr needs --summary")
        if st and st["profile"] != "missing":
            print("step 2: already done (%s); not opening another pull request." % (st["profile_pr"] or "the profile links %s" % repo))
        else:
            how, out = profile_pr(a.owner, repo, a.name, line, a.profile_after)
            if how == "opened":
                print("step 2: opened " + out)
                st = first_steps_status(a.owner, repo)
            elif how == "run":
                print("step 2: pushed the branch; open the pull request from the shell (gh can't sign in from this Python):\n  " + out)
            else:
                print("step 2: the profile already links %s; nothing to do" % repo)

    if not a.dest_root:
        if a.apply: ap.error("--apply needs --dest-root")
        print("step 3 (Unity skeleton): give --dest-root to preview it.")
        return 0
    template = Path(a.template) if a.template else default_template()
    repos = Path(a.vcc_repos) if a.vcc_repos else default_repos()
    dest = Path(os.path.abspath(a.dest_root)) / repo        # not resolve(): keep the Astra's PetOS junction as given
    try:
        plan = plan_skeleton(template, repos)
        existed = check_dest(dest)
    except ValueError as e:
        print("step 3 can't start: %s" % e)
        return 2
    if not a.apply:
        for row in describe_plan(plan, a.name, dest, existed):
            print(row)
        print("Nothing written. Add --apply to write it.")
        return 0
    if not a.skip_first_steps_check and not (st and st["repo"] == "public" and st["profile"] in ("linked", "pr-open")):
        print("REFUSED: do the first steps first (public repo, then the profile link PR), or pass --skip-first-steps-check.")
        return 2
    if not a.summary: ap.error("--apply needs --summary (it starts the README)")
    code, written, kept, rec = apply_skeleton(a.name, repo, a.summary, template, repos, dest)
    print("step 3: wrote %s (%d items)%s. Usage record: %s" % (dest, len(written), "; kept " + ", ".join(kept) if kept else "", rec))
    print("next: open the project in Unity once from the Hub or VCC (a normal window, so the SDK sign-in stays); then "
          + ("commit on claude/<topic>-<date> and push." if existed else
             "git init, commit on claude/<topic>-<date>, add origin https://github.com/%s/%s.git and push." % (a.owner, repo))
          + " Pieces from another world come in only when Astra names them.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
