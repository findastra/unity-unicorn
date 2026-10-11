"""Synthetic runner tests. Construct receipts in memory; never add usage history."""
import importlib.util
from datetime import datetime, timezone
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

SOURCE = Path(__file__).resolve().parents[1]/"scripts/run-unity-tool-20261011.py"
spec = importlib.util.spec_from_file_location("tool_runner", SOURCE)
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class RunnerTests(unittest.TestCase):
    def test_recorded_times_keep_real_microseconds(self):
        observed = [datetime(2026,10,11,19,38,20,123456,tzinfo=timezone.utc),
                    datetime(2026,10,11,19,38,20,123789,tzinfo=timezone.utc)]
        with patch.object(runner,"datetime") as clock:
            clock.now.side_effect = observed
            first,second = runner.utc_now(),runner.utc_now()
        self.assertEqual(first,"2026-10-11T19:38:20.123456Z")
        self.assertEqual(second,"2026-10-11T19:38:20.123789Z")
        self.assertNotEqual(first,second)

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="unicorn-runner-synthetic-")
        self.root = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def test_exact_argument_list_no_shell_and_no_sensitive_args_in_receipt(self):
        command = ["python", "a path/check-20261011.py", "literal;$(not-a-command)", "--token", "synthetic-secret"]
        with patch.object(runner.subprocess,"run",return_value=subprocess.CompletedProcess(command,0)) as call:
            receipt = runner.collect_receipt("synthetic-check", self.root, command)
        call.assert_called_once_with(command,cwd=self.root,shell=False,check=False)
        self.assertEqual(receipt["schema"],"unity-unicorn.tool-usage/v1")
        self.assertEqual(receipt["status"],"completed")
        self.assertEqual(receipt["exit_code"],0)
        self.assertTrue(receipt["started_at"].endswith("Z"))
        self.assertTrue(receipt["completed_at"].endswith("Z"))
        self.assertNotIn("synthetic-secret",str(receipt))

    def test_nonzero_launch_error_and_interrupt_are_failed_attempts(self):
        for response in [subprocess.CompletedProcess(["python"],7),FileNotFoundError("synthetic"),KeyboardInterrupt()]:
            kwargs = {"side_effect": response} if isinstance(response,BaseException) else {"return_value": response}
            with patch.object(runner.subprocess,"run",**kwargs):
                result = runner.collect_receipt("synthetic-check",self.root,["python"])
            self.assertEqual(result["status"],"failed")
            self.assertIn("failure_reason",result)

    def test_report_must_be_fresh_and_hash_matches(self):
        report = self.root/"synthetic-report-20261011.json"
        with patch.object(runner.subprocess,"run",return_value=subprocess.CompletedProcess(["python"],0)):
            self.assertEqual(runner.collect_receipt("test",self.root,["python"],report)["status"],"failed")
            report.write_text("old")
            self.assertEqual(runner.collect_receipt("test",self.root,["python"],report)["status"],"failed")
        def produce(*args,**kwargs):
            report.write_text("new output")
            return subprocess.CompletedProcess(["python"],0)
        with patch.object(runner.subprocess,"run",side_effect=produce):
            result = runner.collect_receipt("test",self.root,["python"],report)
        self.assertEqual(result["status"],"completed")
        self.assertEqual(result["report_sha256"],runner.fingerprint(report)["sha256"])
        self.assertTrue(result["report_updated_during_run"])

    def test_rejects_shells_launchers_copy_and_missing_command(self):
        for command in [[],["cmd.exe","/c","echo"],["pwsh"],["C:/editor/Unity.exe"],["check.cmd"],["copy"],["python","bad\0arg"]]:
            with self.subTest(command=command),self.assertRaises(ValueError): runner.validate_command(command)

    def test_blender_script_must_propagate_failure_before_execution(self):
        script = "check-20261011.py"
        for args in [["--python",script],["-b","--python",script],
                     ["-b","--python-exit-code","0","--python",script],
                     ["-b","--python",script,"--python-exit-code","1"],
                     ["-b","--python-exit-code","1","--python",script,"--python-exit-code","0","--python",script],
                     ["-b","--python-exit-code","no","--python-expr","raise Exception()"],
                     ["-b","--python-exit-code","1","--python-console"]]:
            with self.subTest(args=args),self.assertRaises(ValueError): runner.validate_command(["blender.exe",*args])
        runner.validate_command(["blender.exe","-b","--python-exit-code","1","--python",script])
        runner.validate_command(["blender","--background","--python-exit-code","2","-P",script])

    def test_filename_contract_and_cli_does_not_launch_on_invalid_input(self):
        self.assertEqual(runner.validate_name("tool-usage-20261011-142000.json",".json").name,"tool-usage-20261011-142000.json")
        for name in ["report.json","Report-20261011.json","bad-20261301.json","bad-20261011-290000.json"]:
            with self.assertRaises(ValueError): runner.validate_name(name,".json")
        base=["--tool-id","synthetic","--project",str(self.root),"--usage-log",str(self.root/"usage-20261011.json")]
        with patch.object(runner.subprocess,"run") as call:
            with self.assertRaises(SystemExit): runner.main(base+["--","python"])
            with self.assertRaises(SystemExit): runner.main(base+["--foreground","--","cmd.exe"])
            call.assert_not_called()
        self.assertFalse((self.root/"usage-20261011.json").exists())


if __name__ == "__main__":
    unittest.main()
