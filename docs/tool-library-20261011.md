# Unity tool library

The self-contained browser entry preserves the existing planning desk and adds a separate tool library. It has generic tool descriptions, command templates, private catalog imports, structural-audit findings and recorded execution history. It does not connect to Unity or execute commands.

## Using it

1. Open the app and choose **Import catalog or report**. Select a private catalog, a structural report, a usage receipt, or a previous tool backup. JSON arrays and JSONL usage logs are accepted.
2. Search tools, open **Command & source**, and copy the command. Replace placeholders and inspect project/output paths before running it locally. Existing Unity editor jobs should use their supported editor menu or queue; the page does not start another editor.
3. Import the completed tool report or usage receipt. Dates come from the imported execution record, never from opening this app or copying a command.
4. Use **Export tool backup** to keep catalog entries and run history outside Git.

Last recorded use is shown in America/Denver. A historical date without a verified time is labeled **date only**. Missing evidence stays **Unknown**. A failed execution is labeled **Failed attempt**. Imported timestamps are records supplied by the user/tool, not independently authenticated by the browser. File modification dates and catalog-generation dates do not establish use.

## Private catalog contract

```json
{
  "schema": "unity-unicorn.tool-catalog/v1",
  "tools": [{
    "id": "cloud-mesh-generator",
    "name": "Cloud mesh generator",
    "kind": "Generator",
    "description": "Build connected cloud meshes.",
    "project": {"name": "Example project", "path": "<private-project-path>"},
    "source_path": "<private-tool-path>",
    "command_template": "<command to review and run locally>",
    "requirements": ["Installed Blender and the intended project."],
    "limitations": ["Existing project-specific recipe; review its output paths."],
    "last_used": {
      "date": "2026-10-06",
      "status": "completed",
      "evidence_type": "dated-report",
      "evidence": "Execution report records that this tool ran on this date.",
      "evidence_path": "<private-report-path>"
    }
  }]
}
```

Omit `last_used` or set it to `null` when no execution evidence exists. For a known instant use `completed_at` with an ISO UTC timestamp ending in `Z` instead of `date`. A last-use record requires evidence text. The command token `{date}` expands to the owner's current `YYYYMMDD` date without changing execution history. Catalog imports update tools by `id` and do not remove other tools.

## Report and usage contracts

The masonry report schema is `unity-unicorn.structural-audit/v1` with `tool_id: "masonry-structural-audit"`, `project.name`, `completed_at` in UTC, `status: "completed"` or `"failed"`, `summary`, `metrics`, `limitations`, and a `findings` array. Each finding has `id`, `severity`, `title`, `evidence`, and `suggested_fix`. Evidence and suggested fixes may be strings or structured JSON. The browser renders them as text.

Usage records use `unity-unicorn.tool-usage/v1` with the same tool/project/completion/status fields. Optional `report_filename`, `report_sha256`, `evidence` and `evidence_path` describe the evidence. A report and its matching receipt are shown as one activity when tool, project and completion timestamp agree. Importing the same record again replaces its stored copy; it does not create a new execution date.

Backups use `unity-unicorn.tool-library-backup/v1`, `catalog` and `runs` arrays. The export timestamp is only the backup timestamp. Imports validate an entire JSON file before changing stored records. Files larger than 10 MB are refused.

## Storage and boundaries

Tool data uses `astra-unity-tool-library:1`. The existing planning desk remains at `astra-pet-desk:unity-unicorn:1`. Records are stored only on the browser origin/file location being used; there is no service, telemetry, token storage or network call. Moving to a different origin may require importing a backup. If browser storage fails, the page reports it and allows an export.

Private paths, project names, reports, run history and backups must remain outside the public source repository. Generic defaults contain none of the owner's private inventory. A tool's presence in the catalog does not prove it was run, and a completed geometric audit is not a Unity runtime check or a structural-engineering certification.
