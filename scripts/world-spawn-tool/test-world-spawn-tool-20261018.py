"""Tests for world-spawn-tool (no network, no Unity): python test-world-spawn-tool-20261018.py"""
import contextlib
import importlib.util
import io
import json
import shlex
import subprocess
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest import mock

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("wst", HERE / "world-spawn-tool-20261018.py")
wst = importlib.util.module_from_spec(spec)
spec.loader.exec_module(wst)


def no_network(url):
    raise AssertionError("the tests must not reach the network: " + url)


wst.http_get_json = no_network

PROFILE = ("<h2>Hi</h2>\r\n\r\n### VR & VRChat\r\n\r\n"
           "- [`astras-infinite-pole`](https://github.com/findastra/astras-infinite-pole) -- pole. PC VR / desktop\r\n"
           "- [`piranesi-house`](https://github.com/findastra/piranesi-house) -- house. PC VR / desktop\r\n"
           "- [`detour-into-vr`](https://github.com/findastra/detour-into-vr) -- rave\r\n\r\n"
           "### Discord\r\n\r\n- [`x`](https://github.com/findastra/x) -- x\r\n")

TEMPLATE_GUID = "9e59d35066069f1469c7165423087ff5"   # the same in every VCC World template, so a new world needs its own
SETTINGS = ("PlayerSettings:\r\n  productGUID: %s\r\n  AndroidProfiler: 0\r\n  companyName: DefaultCompany\r\n"
            "  productName: World\r\n  defaultCursor: {fileID: 0}\r\n" % TEMPLATE_GUID)
TEMPLATE_GITIGNORE = "# Unity\r\n.utmp/\r\n/[Ll]ibrary/\r\n/[Tt]emp/\r\n\r\n# Builds\r\n*.apk\r\n"


def make_template(root):
    """A small stand-in for VCC's World template, with the template's CRLF files."""
    t = root / "VRCTemplates" / "World"
    files = {
        "package.json": json.dumps({"name": "com.vrchat.template.world", "version": "1.5.0", "category": "ProjectTemplate"}),
        "README.md": "# vrc-template-World\n",
        ".gitignore": TEMPLATE_GITIGNORE,
        "Assets/.gitkeep": "",
        "Packages/.gitignore": "/*/\r\ncom.vrchat.*/\r\n!vpm-manifest.json\r\n!manifest.json\r\n",
        "Packages/manifest.json": json.dumps({"dependencies": {"com.unity.timeline": "1.7.5"}}, indent=2),
        "Packages/vpm-manifest.json": json.dumps({"dependencies": {"com.vrchat.base": {"version": "~3.10.x"},
                                                                    "com.vrchat.worlds": {"version": "~3.10.x"}}}),
        "ProjectSettings/ProjectVersion.txt": "m_EditorVersion: 2022.3.22f1\r\n",
        "ProjectSettings/ProjectSettings.asset": SETTINGS,
        "ProjectSettings/EditorBuildSettings.asset": "EditorBuildSettings:\r\n  m_Scenes: []\r\n",
        "ProjectSettings/TagManager.asset": "TagManager:\r\n  tags: []\r\n",
    }
    for rel, text in files.items():
        (t / rel).parent.mkdir(parents=True, exist_ok=True)
        (t / rel).write_bytes(text.encode("utf-8"))
    return t


def make_zip(path, package, extra=None):
    path.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(path, "w") as zf:
        zf.writestr("package.json", json.dumps(package))
        zf.writestr("Runtime/VRCSDK/Dependencies/VRChat/deep/file.txt", "sdk")
        for name, data in (extra or {}).items():
            zf.writestr(name, data)


def make_repos(root):
    """VCC's package cache: two base versions, the worlds versions around them, a pre-release and a broken zip."""
    r = root / "Repos"
    base = lambda v: {"name": "com.vrchat.base", "version": v}
    worlds = lambda v, b: {"name": "com.vrchat.worlds", "version": v, "vpmDependencies": {"com.vrchat.base": b}}
    make_zip(r / "com.vrchat.base" / "com.vrchat.base-3.10.4.zip", base("3.10.4"))
    make_zip(r / "com.vrchat.base" / "com.vrchat.base-3.10.5.zip", base("3.10.5"))
    make_zip(r / "com.vrchat.worlds" / "com.vrchat.worlds-3.10.5.zip", worlds("3.10.5", "3.10.5"))
    make_zip(r / "com.vrchat.worlds" / "com.vrchat.worlds-3.10.6.zip", worlds("3.10.6", "3.10.6"))     # its base isn't cached
    make_zip(r / "com.vrchat.worlds" / "com.vrchat.worlds-3.10.7-beta.1.zip", worlds("3.10.7-beta.1", "3.10.7-beta.1"))
    make_zip(r / "com.vrchat.worlds" / "com.vrchat.worlds-4.0.0.zip", worlds("4.0.0", "4.0.0"))         # outside ~3.10.x
    (r / "com.vrchat.worlds" / "broken.zip").write_bytes(b"not a zip")
    return r


class Names(unittest.TestCase):
    def test_repo_names_follow_astras_rule(self):
        self.assertEqual(wst.derive_repo("Liminal Doors"), "liminal-doors")
        self.assertEqual(wst.derive_repo("Astra's Infinite Pole"), "astras-infinite-pole")
        self.assertEqual(wst.derive_repo("Astra’s Infinite Pole"), "astras-infinite-pole")   # curly apostrophe too
        self.assertEqual(wst.derive_repo("Mommy's World"), "mommys-world")
        self.assertTrue(wst.valid_repo("piranesi-house"))
        for bad in ("Liminal-Doors", "liminal_doors", "-x", "x-", "", "a--b"):
            self.assertFalse(wst.valid_repo(bad), bad)

    def test_code_name(self):
        self.assertEqual(wst.code_name("Liminal Doors"), "LiminalDoors")
        self.assertEqual(wst.code_name("Astra's Infinite Pole"), "AstrasInfinitePole")
        self.assertEqual(wst.code_name("Mommy's World"), "MommysWorld")
        self.assertEqual(wst.code_name("3 Rooms"), "World3Rooms")


class Profile(unittest.TestCase):
    line = wst.profile_line("findastra", "liminal-doors", "a world  of doors.", "PC VR / desktop")

    def test_line(self):
        self.assertEqual(self.line, "- [`liminal-doors`](https://github.com/findastra/liminal-doors) -- a world of doors. PC VR / desktop")
        with self.assertRaises(ValueError):
            wst.profile_line("findastra", "x", "   ")

    def test_inserts_at_end_of_section_keeping_crlf(self):
        new, changed = wst.insert_profile_line(PROFILE, self.line, "findastra", "liminal-doors")
        self.assertTrue(changed)
        rows = new.split("\r\n")
        i = rows.index(self.line)
        self.assertIn("detour-into-vr", rows[i - 1])
        self.assertEqual(rows[i + 1], "")
        self.assertNotIn("\n", new.replace("\r\n", ""))           # no bare LF added
        self.assertEqual(new.replace(self.line + "\r\n", ""), PROFILE)   # nothing else changed

    def test_after_and_idempotent(self):
        new, _ = wst.insert_profile_line(PROFILE, self.line, "findastra", "liminal-doors", after="piranesi-house")
        rows = new.split("\r\n")
        self.assertIn("piranesi-house", rows[rows.index(self.line) - 1])
        again, changed = wst.insert_profile_line(new, self.line, "findastra", "liminal-doors")
        self.assertFalse(changed)
        self.assertEqual(again, new)

    def test_missing_section(self):
        with self.assertRaises(ValueError):
            wst.insert_profile_line("# nothing here\n", self.line, "findastra", "liminal-doors")


class Versions(unittest.TestCase):
    def test_vpm_ranges(self):
        yes = [("3.10.5", "~3.10.x"), ("3.10.0", "~3.10.x"), ("3.10.5", "3.10.x"), ("3.10.5", "3.10.5"),
               ("3.10.6", "~3.10.5"), ("3.11.0", "^3.10.0"), ("4.0.0", ">=3.10.0"), ("3.2.1", "3")]
        no = [("3.11.0", "~3.10.x"), ("3.10.4", "3.10.5"), ("3.10.6", "3.10.5"), ("3.10.4", "~3.10.5"),
              ("4.0.0", "^3.10.0"), ("3.10.7-beta.1", "~3.10.x"), ("junk", "~3.10.x")]
        for v, s in yes:
            self.assertTrue(wst.version_matches(v, s), (v, s))
        for v, s in no:
            self.assertFalse(wst.version_matches(v, s), (v, s))
        with self.assertRaises(ValueError):
            wst.version_matches("3.10.5", "3.10.5 || 3.11.0")


class Template(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.template, self.repos = make_template(self.root), make_repos(self.root)

    def tearDown(self):
        self.tmp.cleanup()

    def test_resolve_picks_the_newest_cached_set_that_fits(self):
        _, wanted = wst.read_template(self.template)
        sdk = wst.resolve_sdk(wanted, self.repos)
        self.assertEqual(list(sdk), ["com.vrchat.base", "com.vrchat.worlds"])        # the template's order
        # worlds 3.10.6 needs a base that isn't cached, the beta and 4.0.0 are out, so both land on 3.10.5
        self.assertEqual({n: p["version"] for n, p in sdk.items()}, {"com.vrchat.base": "3.10.5", "com.vrchat.worlds": "3.10.5"})

    def test_resolve_says_what_is_missing(self):
        _, wanted = wst.read_template(self.template)
        with self.assertRaisesRegex(ValueError, "Open VCC once"):
            wst.resolve_sdk(wanted, self.root / "empty-repos")
        for z in (self.repos / "com.vrchat.base").glob("*3.10.5.zip"):
            z.unlink()
        with self.assertRaises(ValueError) as e:
            wst.resolve_sdk(wanted, self.repos)
        for gap in ("com.vrchat.worlds 3.10.6 needs com.vrchat.base 3.10.6", "com.vrchat.worlds 3.10.5 needs com.vrchat.base 3.10.5"):
            self.assertIn(gap, str(e.exception))

    def test_refuses_what_is_not_vccs_world_template(self):
        with self.assertRaisesRegex(ValueError, "not at"):
            wst.read_template(self.root / "nowhere")
        (self.template / "package.json").write_text(json.dumps({"name": "com.vrchat.template.avatar"}))
        with self.assertRaisesRegex(ValueError, "not VCC's World template"):
            wst.read_template(self.template)

    def test_template_files_leave_out_the_templates_own_metadata(self):
        files = wst.template_files(self.template)
        self.assertNotIn("package.json", files)
        self.assertNotIn("README.md", files)
        for rel in (".gitignore", "Assets/.gitkeep", "Packages/.gitignore", "Packages/vpm-manifest.json", "ProjectSettings/TagManager.asset"):
            self.assertIn(rel, files)

    def test_rename_settings_keeps_crlf(self):
        out = wst.rename_settings(SETTINGS, "Liminal Doors")
        self.assertIn("  productName: Liminal Doors\r\n", out)
        self.assertNotIn(TEMPLATE_GUID, out)
        self.assertNotIn("\n", out.replace("\r\n", ""))
        self.assertEqual(len(out.splitlines()), len(SETTINGS.splitlines()))
        with self.assertRaises(ValueError):
            wst.rename_settings("nothing", "x")

    def test_merge_gitignore(self):
        text, changed = wst.merge_gitignore(None, TEMPLATE_GITIGNORE)
        self.assertTrue(changed)
        self.assertTrue(text.startswith(TEMPLATE_GITIGNORE))
        for must in wst.GITIGNORE_MUST:
            self.assertIn(must + "\r\n", text)
        self.assertNotIn("\n", text.replace("\r\n", ""))                     # the template's CRLF kept
        self.assertEqual(wst.merge_gitignore(text, TEMPLATE_GITIGNORE), (text, False))
        own = "/Review/\n.claude/worktrees/\n"
        text, changed = wst.merge_gitignore(own, TEMPLATE_GITIGNORE)
        lines = text.splitlines()
        self.assertTrue(changed and text.startswith(own))
        self.assertEqual(lines.count("/Review/"), 1)
        for rule in ("/[Ll]ibrary/", "*.apk", "/Packages/com.vrchat.worlds/"):
            self.assertIn(rule, lines)

    def test_unpack_refuses_unsafe_paths(self):
        bad = self.root / "bad.zip"
        with zipfile.ZipFile(bad, "w") as zf:
            zf.writestr("../escaped.txt", "x")
        with self.assertRaises(ValueError):
            wst.unpack(bad, self.root / "out")
        self.assertFalse((self.root / "escaped.txt").exists())

    def test_apply_builds_a_fresh_skeleton_from_the_template(self):
        dest = self.root / "worlds" / "liminal-doors"
        code, written, kept, rec = wst.apply_skeleton("Liminal Doors", "liminal-doors", "Doors.", self.template, self.repos, dest, date="20261019")
        self.assertEqual((code, kept), ("LiminalDoors", []))
        for n in ("com.vrchat.base", "com.vrchat.worlds"):
            self.assertEqual(json.loads((dest / "Packages" / n / "package.json").read_text())["version"], "3.10.5")
            self.assertTrue((dest / "Packages" / n / "Runtime" / "VRCSDK" / "Dependencies" / "VRChat" / "deep" / "file.txt").is_file())
        vpm = json.loads((dest / "Packages" / "vpm-manifest.json").read_text())
        self.assertEqual(vpm["dependencies"], {"com.vrchat.base": {"version": "3.10.5"}, "com.vrchat.worlds": {"version": "3.10.5"}})
        self.assertEqual(vpm["locked"]["com.vrchat.worlds"], {"version": "3.10.5", "dependencies": {"com.vrchat.base": "3.10.5"}})
        for same in ("Packages/.gitignore", "Packages/manifest.json", "ProjectSettings/TagManager.asset",
                     "ProjectSettings/ProjectVersion.txt", "Assets/.gitkeep"):
            self.assertEqual((dest / same).read_bytes(), (self.template / same).read_bytes(), same)
        settings = (dest / "ProjectSettings" / "ProjectSettings.asset").read_bytes().decode()
        self.assertIn("  productName: Liminal Doors\r\n", settings)
        self.assertRegex(settings, r"  productGUID: [0-9a-f]{32}\r\n")
        self.assertNotIn(TEMPLATE_GUID, settings)
        self.assertIn("m_Scenes: []", (dest / "ProjectSettings" / "EditorBuildSettings.asset").read_text())
        self.assertFalse((dest / "package.json").exists())                   # the template's own metadata stays behind
        readme = (dest / "README.md").read_text()
        self.assertTrue(readme.startswith("# Liminal Doors\n\nDoors.\n"))
        for words in ("World template 1.5.0", "com.vrchat.worlds 3.10.5", "nothing in it came from another world", "menu LiminalDoors/"):
            self.assertIn(words, readme)
        queue = (dest / "Assets" / "LiminalDoors" / "Editor" / "LiminalDoorsAgentQueue.cs").read_text()
        self.assertIn("public static class LiminalDoorsAgentQueue", queue)
        self.assertIn('StartsWith("LiminalDoors/")', queue)
        self.assertNotIn("{{", queue)
        self.assertEqual(json.loads((dest / "Assets" / "LiminalDoors" / "Editor" / "LiminalDoors.Editor.asmdef").read_text())["name"], "LiminalDoors.Editor")
        gi = (dest / ".gitignore").read_text().splitlines()
        for must in wst.GITIGNORE_MUST + ["/[Ll]ibrary/"]:
            self.assertIn(must, gi)
        self.assertEqual(gi.count("/Review/"), 1)
        self.assertIn("*.unity text eol=lf", (dest / ".gitattributes").read_text())
        usage = json.loads(rec.read_text())
        self.assertEqual((usage["schema"], usage["tool_id"], usage["tool_version"], usage["status"]),
                         ("unity-unicorn.tool-usage/v1", "world-spawn-tool", wst.VERSION, "completed"))
        self.assertTrue(usage["completed_at"].endswith("Z"))
        self.assertIn("World template 1.5.0", usage["evidence"])
        self.assertNotIn(str(self.root), json.dumps(usage))                  # no machine paths in the record
        self.assertNotIn(self.root.name, json.dumps(usage))
        self.assertTrue(str(rec.relative_to(dest)).startswith("Review"))
        with self.assertRaisesRegex(ValueError, "already holds a Unity project"):
            wst.apply_skeleton("Liminal Doors", "liminal-doors", "Doors.", self.template, self.repos, dest)

    def test_apply_into_a_fresh_clone_keeps_its_files(self):
        dest = self.root / "mommys-world"
        (dest / ".git").mkdir(parents=True)
        (dest / "README.md").write_bytes(b"# Mommy's World\n\nThe seed's README.\n")
        (dest / ".gitignore").write_bytes(b"/Review/\n.claude/worktrees/\n")
        code, written, kept, rec = wst.apply_skeleton("Mommy's World", "mommys-world", "Club.", self.template, self.repos, dest, date="20261019")
        self.assertEqual(code, "MommysWorld")
        self.assertEqual(kept, ["README.md"])
        self.assertEqual((dest / "README.md").read_bytes(), b"# Mommy's World\n\nThe seed's README.\n")
        gi = (dest / ".gitignore").read_text().splitlines()
        self.assertEqual(gi[:2], ["/Review/", ".claude/worktrees/"])
        self.assertEqual(gi.count("/Review/"), 1)
        self.assertIn("/[Ll]ibrary/", gi)
        self.assertIn(".gitignore (added the rules it lacked)", written)
        self.assertTrue((dest / "Packages" / "com.vrchat.worlds" / "package.json").is_file())
        self.assertIn("Kept what was already there: README.md", json.loads(rec.read_text())["evidence"])

    def test_nothing_is_written_when_the_template_is_wrong(self):
        dest = self.root / "out" / "x"
        with self.assertRaises(ValueError):
            wst.apply_skeleton("X", "x", "x", self.root / "nowhere", self.repos, dest)
        self.assertFalse(dest.exists())

    def test_preview_warns_about_long_paths(self):
        plan = wst.plan_skeleton(self.template, self.repos)
        rows = wst.describe_plan(plan, "Liminal Doors", self.root / "short", False)
        self.assertFalse(any("WARNING" in r for r in rows))
        self.assertTrue(any("com.vrchat.worlds 3.10.5" in r for r in rows))
        rows = wst.describe_plan(plan, "Liminal Doors", self.root / ("x" * 240), True)
        self.assertTrue(any("WARNING" in r and "long paths" in r for r in rows))
        self.assertTrue(any("is already there" in r for r in rows))


def signed_out_gh(calls):
    def run(cmd, **kw):
        calls.append(cmd)
        if cmd[0] != "gh":
            raise AssertionError("only gh is faked here: %r" % cmd)
        return subprocess.CompletedProcess(cmd, 1, "", "You are not logged into any GitHub hosts. To log in, run: gh auth login\n")
    return run


def public_api(repo_there=True, pr_open=True):
    def get(url):
        if url.endswith("/repos/findastra/mommys-world"):
            return {"visibility": "public", "html_url": "https://github.com/findastra/mommys-world"} if repo_there else None
        if url.endswith("/repos/findastra/findastra/contents/README.md"):
            import base64
            return {"content": base64.b64encode(PROFILE.encode()).decode()}
        if "/repos/findastra/findastra/pulls" in url:
            return [{"head": {"ref": "claude/mommys-world-link-20261019"}, "html_url": "https://github.com/findastra/findastra/pull/10"}] if pr_open else []
        raise AssertionError("unexpected URL " + url)
    return get


class GitHub(unittest.TestCase):
    def run_main(self, argv, **api):
        calls, out = [], io.StringIO()
        with mock.patch.object(wst.subprocess, "run", signed_out_gh(calls)), \
                mock.patch.object(wst, "http_get_json", public_api(**api)), contextlib.redirect_stdout(out):
            code = wst.main(argv)
        return code, out.getvalue(), calls

    def test_signed_out_gh_never_reads_as_missing(self):
        with mock.patch.object(wst.subprocess, "run", signed_out_gh([])):
            with self.assertRaises(wst.GhSignedOut):
                wst.gh("repo", "view", "findastra/mommys-world")

    def test_check_falls_back_to_the_public_api_and_prints_shell_commands(self):
        code, out, calls = self.run_main(["--name", "Mommy's World", "--check"])
        self.assertEqual(code, 0)
        self.assertIn("hosts.yml", out)
        self.assertIn("gh repo view findastra/mommys-world --json visibility,url", out)
        self.assertIn("FIRST STEP 1, public repo:     done (https://github.com/findastra/mommys-world)", out)
        self.assertIn("PR open, waiting for Astra (https://github.com/findastra/findastra/pull/10)", out)
        self.assertIn("checked through GitHub's public API", out)
        self.assertNotIn("GH_TOKEN", out)                                     # never a token workaround
        self.assertNotIn("gh auth token", out)

    def test_steps_1_and_2_print_the_gh_command_instead_of_failing(self):
        code, out, calls = self.run_main(["--name", "Mommy's World", "--summary", "Mommy's club, in VR.", "--create-repo"], repo_there=False)
        self.assertEqual(code, 0)
        cmd = next(l.strip() for l in out.splitlines() if l.strip().startswith("gh repo create"))
        self.assertEqual(shlex.split(cmd), ["gh", "repo", "create", "findastra/mommys-world", "--public", "--description", "Mommy's club, in VR."])
        self.assertFalse(any("create" in c for c in calls))                  # nothing was created from Python
        code, out, calls = self.run_main(["--name", "Mommy's World", "--summary", "Club.", "--profile-pr"])
        self.assertIn("step 2: already done (https://github.com/findastra/findastra/pull/10)", out)

    def test_apply_still_waits_for_the_first_steps(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            argv = ["--name", "Mommy's World", "--summary", "Club.", "--apply", "--dest-root", str(root / "worlds"),
                    "--template", str(make_template(root)), "--vcc-repos", str(make_repos(root))]
            code, out, _ = self.run_main(argv, pr_open=False)
            self.assertEqual(code, 2)
            self.assertIn("REFUSED", out)
            self.assertFalse((root / "worlds").exists())
            code, out, _ = self.run_main(argv)
            self.assertEqual(code, 0, out)
            self.assertTrue((root / "worlds" / "mommys-world" / "Assets" / "MommysWorld" / "Editor" / "MommysWorldAgentQueue.cs").is_file())

    def test_reference_is_gone(self):
        err = io.StringIO()
        with contextlib.redirect_stderr(err), self.assertRaises(SystemExit):
            wst.main(["--name", "Mommy's World", "--reference", "Astra-Infinite-Pole", "--skip-first-steps-check"])
        self.assertIn("start fresh from VCC's World template", err.getvalue())

    def test_shell_commands_survive_quotes_and_newlines(self):
        args = ["gh", "pr", "create", "--title", "Profile: link Mommy's World", "--body", "Adds `x`:\n\n> it's $HOME \"here\""]
        self.assertEqual(shlex.split(wst.shell_command(*args)), args)


if __name__ == "__main__":
    unittest.main(verbosity=2)
