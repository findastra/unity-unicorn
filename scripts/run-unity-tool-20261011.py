#!/usr/bin/env python3
"""Run one foreground tool and record its observed result in a private receipt.

Python 3.10+, standard library only. The command must wait for its real work to
finish. Never use a desktop launcher, a detached process, or a command that just
copies an old result. This wrapper measures the waited command's exit status;
it cannot verify a child process, a tool's conclusions, or engineering safety.
Use a dedicated synchronous verification script for Unity Editor workflows.
Blender must use background mode; Python work also requires a nonzero
--python-exit-code before its first Python command, so a traceback cannot be
mistaken for successful completion merely because Blender returned zero.

Example (replace the paths and tool id):
  python run-unity-tool-20261011.py --tool-id example-audit --project PATH \
    --usage-log private/example-usage-20261011.json --foreground -- \
    python path/to/foreground-check-20261011.py

Prepared with Codex (GPT-6), 2026-10-11. Public credit: Astra.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import time

SCHEMA = "unity-unicorn.tool-usage/v1"
# These launch shells, open files, copy artifacts, or commonly detach work.
# This is a misuse guard, not a sandbox; only run commands you trust.
DISALLOWED = {"cmd", "powershell", "pwsh", "sh", "bash", "zsh", "fish",
              "start", "open", "xdg-open", "explorer", "unity", "unity hub",
              "unityhub", "cp", "copy", "xcopy", "robocopy"}
DATED_NAME = re.compile(r"[a-z0-9]+(?:-[a-z0-9]+)*-\d{8}(?:-\d{6})?\.[a-z0-9]+$")


def utc_now():
    return datetime.now(timezone.utc).isoformat(timespec="microseconds").replace("+00:00", "Z")


def validate_name(path, extension=None):
    path = Path(path)
    if not DATED_NAME.fullmatch(path.name) or extension and path.suffix != extension:
        raise ValueError("Receipt/report names must be lowercase hyphenated and end -YYYYMMDD[-HHMMSS] before the extension; receipts require .json.")
    match = re.search(r"-(\d{8})(?:-(\d{6}))?\.[a-z0-9]+$", path.name)
    datetime.strptime(match[1]+(match[2] or ""), "%Y%m%d%H%M%S" if match[2] else "%Y%m%d")
    return path


def validate_command(command):
    if not command:
        raise ValueError("Provide a foreground command after --.")
    executable = Path(command[0]).name.lower()
    stem = Path(executable).stem if Path(executable).suffix.lower() == ".exe" else executable
    if stem in DISALLOWED or Path(executable).suffix.lower() in {".bat", ".cmd"}:
        raise ValueError("Shells, file-copy commands and desktop launchers are unsupported. Run a direct foreground checker; use a synchronous verification script for Unity.")
    if stem == "blender":
        if not any(arg in ("--background","-b") for arg in command[1:]):
            raise ValueError("Blender must run in background mode (--background or -b); opening its UI is not completed tool work.")
        python_positions = [i for i,arg in enumerate(command) if arg in ("--python","-P","--python-text","--python-expr","--python-console")]
        if "--python-console" in command:
            raise ValueError("Interactive Blender Python consoles are not foreground verification commands.")
        for python_position in python_positions:
            flags = [i for i,arg in enumerate(command[:python_position]) if arg == "--python-exit-code"]
            try:
                value = int(command[flags[-1]+1]) if flags else 0
            except (ValueError,IndexError):
                value = 0
            if not 1 <= value <= 255:
                raise ValueError("Every Blender Python command requires a preceding --python-exit-code value from 1 to 255; Blender can otherwise exit zero after a script exception.")
    if any("\0" in value for value in command):
        raise ValueError("Command arguments cannot contain NUL characters.")


def fingerprint(path):
    if not path.exists():
        return None
    if not path.is_file():
        raise ValueError("Report path is not a regular file.")
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024*1024), b""):
            digest.update(chunk)
    stat = path.stat()
    return {"sha256": digest.hexdigest(), "size": stat.st_size, "mtime_ns": stat.st_mtime_ns}


def collect_receipt(tool_id, project, command, report=None):
    """Wait synchronously; returning a receipt does not write or import history."""
    validate_command(command)
    started_at, started = utc_now(), time.monotonic()
    receipt = {"schema": SCHEMA, "tool_id": tool_id, "project": {"name": project.name},
        "executable": Path(command[0]).name,
        "started_at": started_at, "status": "failed", "exit_code": None,
        "evidence_scope": "Observed exit of the explicitly foreground command; does not validate detached children or the tool's conclusions."}
    try:
        before = fingerprint(report) if report else None
        process = subprocess.run(command, cwd=project, shell=False, check=False)
        receipt["exit_code"] = process.returncode
        if process.returncode == 0:
            receipt["status"] = "completed"
        else:
            receipt["failure_reason"] = "Foreground command returned a nonzero exit code."
        if report:
            after = fingerprint(report)
            if after and (before is None or after != before):
                receipt["report_filename"] = report.name
                receipt["report_sha256"] = after["sha256"]
                receipt["report_updated_during_run"] = True
            else:
                receipt["status"] = "failed"
                receipt["failure_reason"] = "Requested report was missing or unchanged after the command; no fresh report evidence was observed."
    except KeyboardInterrupt:
        receipt["exit_code"] = 130
        receipt["failure_reason"] = "Foreground command was interrupted; completion was not observed."
    except (OSError, ValueError) as error:
        receipt["failure_reason"] = f"Command or report verification failed: {type(error).__name__}: {error}"
    receipt["completed_at"] = utc_now()
    receipt["duration_seconds"] = round(time.monotonic()-started, 3)
    # Intentionally omit command arguments, environment and captured output:
    # these often contain tokens or other private data unrelated to usage dates.
    return receipt


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--tool-id", required=True)
    parser.add_argument("--project", required=True, type=Path)
    parser.add_argument("--usage-log", required=True, type=Path)
    parser.add_argument("--report", type=Path, help="Optional freshly generated report; unchanged or absent output makes this attempt fail")
    parser.add_argument("--foreground", required=True, action="store_true", help="Assert this command waits for its actual work to finish; never use a launcher or detached process")
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args(argv)
    command = args.command[1:] if args.command[:1] == ["--"] else args.command
    try:
        if not re.fullmatch(r"[a-z0-9]+(?:-[a-z0-9]+)*", args.tool_id) or len(args.tool_id) > 128:
            raise ValueError("Tool id must be a lowercase hyphenated identifier of at most 128 characters.")
        project = args.project.resolve(strict=True)
        if not project.is_dir(): raise ValueError("Project must be an existing directory.")
        receipt_path = validate_name(args.usage_log, ".json").resolve()
        report = validate_name(args.report).resolve() if args.report else None
        if report == receipt_path: raise ValueError("Report and receipt paths must differ.")
        validate_command(command)
    except (ValueError, OSError) as error:
        parser.error(str(error))
    receipt_path.parent.mkdir(parents=True, exist_ok=True)
    receipt = collect_receipt(args.tool_id, project, command, report)
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    print(json.dumps({"status": receipt["status"], "exit_code": receipt["exit_code"], "receipt": str(receipt_path)}))
    return 0 if receipt["status"] == "completed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
