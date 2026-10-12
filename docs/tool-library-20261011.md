# Unity tool library

The self-contained browser entry preserves the existing planning desk and adds a separate tool library. It has generic tool descriptions, command templates, private catalog imports, structural-audit findings and recorded execution history. It does not connect to Unity or execute commands.

## Using it

1. Open the app and choose **Import catalog or report**. Select a private catalog, a structural report, a usage receipt, or a previous tool backup. JSON arrays and JSONL usage logs are accepted.
2. Search tools, open **Command & source**, and copy the command. Replace placeholders and inspect project/output paths before running it locally. Existing Unity editor jobs should use their supported editor menu or queue; the page does not start another editor.
3. Import the completed tool report or usage receipt. Dates come from the imported execution record, never from opening this app or copying a command.
4. Use **Export tool backup** to keep catalog entries and run history outside Git.

Last completed use is shown in America/Denver. A historical date without a verified time is labeled **date only**. Missing evidence stays **Unknown**. A newer failed execution appears separately as **Latest attempt** and never replaces the last completed use. Imported timestamps are records supplied by the user/tool, not independently authenticated by the browser. File modification dates and catalog-generation dates do not establish use.

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

Omit `last_used` or set it to `null` when no execution evidence exists. For a known instant use `completed_at` with an ISO UTC timestamp ending in `Z` instead of `date`. A last-use record requires evidence text and completed status (omitted status defaults to completed). Catalog last_used rejects failed records: import failed attempts as usage reports so older completed history remains available. The command token `{date}` expands to the owner's current `YYYYMMDD` date without changing execution history. Catalog imports update tools by `id` and do not remove other tools.

## Report and usage contracts

The masonry report schema is `unity-unicorn.structural-audit/v1` with `tool_id: "masonry-structural-audit"`, `project.name`, `completed_at` in UTC, `status: "completed"` or `"failed"`, `summary`, `metrics`, `limitations`, and a `findings` array. Each finding has `id`, `severity`, `title`, `evidence`, and `suggested_fix`. Evidence and suggested fixes may be strings or structured JSON. The browser renders them as text.

Usage records use `unity-unicorn.tool-usage/v1` with the same tool/project/completion/status fields. Optional `report_filename`, `report_sha256`, `evidence` and `evidence_path` describe the evidence. A report and its matching receipt are shown as one activity when tool, project and completion timestamp agree. Importing the same record again replaces its stored copy; it does not create a new execution date.

Backups use `unity-unicorn.tool-library-backup/v1`, `catalog` and `runs` arrays. The export timestamp is only the backup timestamp. Imports validate an entire JSON file before changing stored records. Files larger than 10 MB are refused.

## Storage and boundaries

Tool data uses `astra-unity-tool-library:1`. The existing planning desk remains at `astra-pet-desk:unity-unicorn:1`. Records are stored only on the browser origin/file location being used; there is no service, telemetry, token storage or network call. Moving to a different origin may require importing a backup. If browser storage fails, the page reports it and allows an export.

Private paths, project names, reports, run history and backups must remain outside the public source repository. Generic defaults contain none of the owner's private inventory. A tool's presence in the catalog does not prove it was run, and a completed geometric audit is not a Unity runtime check or a structural-engineering certification.

## Local inventory beside the desktop app

The page can read an optional, ignored file named unity-unicorn-local-tools-20261011.js in the same folder as the HTML. It contains one controlled assignment to globalThis.UnityUnicornLocalTools with a unity-unicorn.tool-library-backup/v1 object. The locally installed copy can therefore show a private catalog and dated reports immediately, even when a different browser has different storage. Missing sidecar files leave the generic catalog available.

This optional sidecar is trusted local code: generate it only from reviewed JSON, never from imported code or remote content. Never commit it or publish it as an asset. The source and Cage ignore matching local-tool files. Updates merge missing tools and newer completed evidence, preserve custom tools and commands; known built-in tools receive the current dated name and summary and retain existing run history. A malformed sidecar is rejected before it mutates stored records. The page does not monitor other tools or create completion records simply by loading the sidecar.

## Recording future runs

The current structural auditor supports the procedural PMS1/PMS2 mesh format, HouseLayout placement data and the supplied masonry profile. It is not a universal parser for arbitrary Unity scenes or FBX assets. Reuse its checks on compatible projects; extend the profile and add regression cases for different formats or architecture. The current assembled-opening checks cover four declared central junction segments, not every intersection in a world.

The structural auditor emits a report plus a usage receipt after completion. For another synchronous tool, scripts/run-unity-tool-20261011.py accepts --tool-id, --project, --usage-log, optional --report, and --foreground followed by -- and the command arguments. The foreground flag asserts that the command waits until the tool finishes. The wrapper rejects direct Unity launches, shells and known asynchronous launchers. Use the existing editor workflow and an explicit completion report for editor jobs.

A wrapper receipt records the process result, timing and optional output hash; it does not establish engineering correctness. A nonzero exit or missing/unchanged requested report records a failed attempt. Import that receipt to preserve it separately from completed use. Do not use a command that only starts work in the background. Private commands, local inventory and receipts stay outside public source.

For Blender Python jobs, place `--python-exit-code 1` before `--python` so script failures reach the wrapper. For Unity mesh refreshes, saving copied serialized data may leave stale native render buffers: synchronously reimport changed mesh assets before rendering inspection views, and compare the actual scene with the source checks.

## Dated definitions and open-arches audit

All 29 built-in tools have lowercase, hyphenated, dated definition files under `tools/`. Each card displays its exact definition filename and a short two-sentence summary below it. Definitions describe existing legacy scripts without renaming Unity asset identities. Natural-language searches also match hyphenated names. Last-use dates remain evidence-based; missing evidence displays Unknown.

`no-windows-audit-tool-20261011.py` checks glazing, bars, arched heads, low passages, retained window seats and neighboring architectural obstructions. It uses the same report/receipt contracts with tool ID `no-windows-audit-tool`. `--unity-probes` exports hash-bound scene probes for the separate editor refresh. `--git-ref` audits a previous version read-only. Sparse samples and declared channel-support contacts are disclosed; this does not provide fluid simulation or universal Unity scene parsing.

The matching scene-refresh entry records editor completion evidence; the foreground recorder is also listed. New tools follow `my-preferred-nomenclature` in the skill book. Definitions contain generic descriptions only; local paths, reports and actual use history stay private.
