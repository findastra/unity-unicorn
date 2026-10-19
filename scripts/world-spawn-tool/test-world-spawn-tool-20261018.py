"""Tests for world-spawn-tool (no network, no Unity): python test-world-spawn-tool-20261018.py"""
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("wst", HERE / "world-spawn-tool-20261018.py")
wst = importlib.util.module_from_spec(spec)
spec.loader.exec_module(wst)

PROFILE = ("<h2>Hi</h2>\r\n\r\n### VR & VRChat\r\n\r\n"
           "- [`astras-infinite-pole`](https://github.com/findastra/astras-infinite-pole) -- pole. PC VR / desktop\r\n"
           "- [`piranesi-house`](https://github.com/findastra/piranesi-house) -- house. PC VR / desktop\r\n"
           "- [`detour-into-vr`](https://github.com/findastra/detour-into-vr) -- rave\r\n\r\n"
           "### Discord\r\n\r\n- [`x`](https://github.com/findastra/x) -- x\r\n")

SETTINGS = "PlayerSettings:\n  productGUID: 9e59d35066069f1469c7165423087ff5\n  companyName: DefaultCompany\n  productName: Astra's Infinite Pole\n"


class Names(unittest.TestCase):
    def test_repo_names_follow_astras_rule(self):
        self.assertEqual(wst.derive_repo("Liminal Doors"), "liminal-doors")
        self.assertEqual(wst.derive_repo("Astra's Infinite Pole"), "astras-infinite-pole")
        self.assertEqual(wst.derive_repo("Astra’s Infinite Pole"), "astras-infinite-pole")   # curly apostrophe too
        self.assertTrue(wst.valid_repo("piranesi-house"))
        for bad in ("Liminal-Doors", "liminal_doors", "-x", "x-", "", "a--b"):
            self.assertFalse(wst.valid_repo(bad), bad)

    def test_code_name(self):
        self.assertEqual(wst.code_name("Liminal Doors"), "LiminalDoors")
        self.assertEqual(wst.code_name("Astra's Infinite Pole"), "AstrasInfinitePole")
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


class Skeleton(unittest.TestCase):
    def make_reference(self, root):
        ref = root / "Astra-Infinite-Pole"
        (ref / "ProjectSettings").mkdir(parents=True)
        (ref / "ProjectSettings" / "ProjectVersion.txt").write_text("m_EditorVersion: 2022.3.22f1\n")
        (ref / "ProjectSettings" / "ProjectSettings.asset").write_text(SETTINGS)
        (ref / "ProjectSettings" / "EditorBuildSettings.asset").write_text("m_Scenes:\n  - path: Assets/Old.unity\n")
        (ref / "Packages" / "com.vrchat.worlds").mkdir(parents=True)
        (ref / "Packages" / "com.vrchat.worlds" / "package.json").write_text("{}")
        (ref / "Packages" / "manifest.json").write_text("{}")
        (ref / ".gitignore").write_text("/[Ll]ibrary/\n/Review/\n")
        (ref / "Assets" / "Astra").mkdir(parents=True)
        (ref / "Assets" / "Astra" / "own-scene.unity").write_text("not copied")
        return ref

    def test_rename_settings(self):
        out = wst.rename_settings(SETTINGS, "Liminal Doors")
        self.assertIn("  productName: Liminal Doors\n", out)
        self.assertNotIn("9e59d35066069f1469c7165423087ff5", out)
        with self.assertRaises(ValueError):
            wst.rename_settings("nothing", "x")

    def test_apply_writes_a_clean_skeleton(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); ref = self.make_reference(root)
            dest = root / "out" / "liminal-doors"
            code, written, rec = wst.apply_skeleton("Liminal Doors", "liminal-doors", "Doors.", ref, dest, date="20261018")
            self.assertEqual(code, "LiminalDoors")
            self.assertTrue((dest / "Packages" / "com.vrchat.worlds" / "package.json").is_file())
            self.assertIn("productName: Liminal Doors", (dest / "ProjectSettings" / "ProjectSettings.asset").read_text())
            self.assertIn("m_Scenes: []", (dest / "ProjectSettings" / "EditorBuildSettings.asset").read_text())
            queue = (dest / "Assets" / "LiminalDoors" / "Editor" / "LiminalDoorsAgentQueue.cs").read_text()
            self.assertIn("public static class LiminalDoorsAgentQueue", queue)
            self.assertIn('StartsWith("LiminalDoors/")', queue)
            self.assertNotIn("{{", queue)
            self.assertEqual(json.loads((dest / "Assets" / "LiminalDoors" / "Editor" / "LiminalDoors.Editor.asmdef").read_text())["name"], "LiminalDoors.Editor")
            gi = (dest / ".gitignore").read_text().splitlines()
            for must in wst.GITIGNORE_MUST:
                self.assertIn(must, gi)
            self.assertEqual(gi.count("/Review/"), 1)
            self.assertFalse((dest / "Assets" / "Astra").exists())         # the reference's own assets are never copied
            usage = json.loads(rec.read_text())
            self.assertEqual((usage["schema"], usage["tool_id"], usage["status"]), ("unity-unicorn.tool-usage/v1", "world-spawn-tool", "completed"))
            self.assertTrue(usage["completed_at"].endswith("Z"))
            self.assertNotIn(str(root), json.dumps(usage))                 # no machine paths in the record
            self.assertTrue(str(rec.relative_to(dest)).startswith("Review"))
            with self.assertRaises(ValueError):
                wst.apply_skeleton("Liminal Doors", "liminal-doors", "Doors.", ref, dest)   # never into an existing folder

    def test_refuses_non_unity_reference(self):
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaises(ValueError):
                wst.apply_skeleton("X", "x", "x", Path(tmp), Path(tmp) / "out")


if __name__ == "__main__":
    unittest.main(verbosity=2)
