<#
.SYNOPSIS
  Install (or update) the Udon kits in one VRChat world project, and optionally run the kit tests first.
  Claude Opus 5.5 (claude-opus-5-5), 2026-10-26. Part of Unity Unicorn's udon-kit-mover tool.

.DESCRIPTION
  Copies scripts/udon-kits/Assets/UdonKits (the UdonSharp wrappers and their .meta files, with fixed GUIDs) into
  <World>/Assets/UdonKits. Idempotent: a file whose contents already match is left alone, so running it twice changes
  nothing. It never deletes or touches anything outside Assets/UdonKits. Close Unity first, or let it reimport after.

  Without -Apply it only says what it would copy (a dry run).

.PARAMETER World
  The world's Unity project folder (the one holding Assets and Packages).

.PARAMETER Apply
  Copy for real. Without it, nothing is written.

.PARAMETER RunTests
  Run the kit tests before copying (needs the .NET 8 SDK: winget install Microsoft.DotNet.SDK.8). Stops on a failure.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\udon-kits\Tools\install-udon-kits-20261026.ps1 -World "$env:USERPROFILE\Documents\Astra's PetOS\liminal-doors" -RunTests
  powershell -ExecutionPolicy Bypass -File scripts\udon-kits\Tools\install-udon-kits-20261026.ps1 -World "$env:USERPROFILE\Documents\Astra's PetOS\liminal-doors" -Apply
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)][string]$World,
  [switch]$Apply,
  [switch]$RunTests
)

$ErrorActionPreference = 'Stop'
$kits = Split-Path -Parent $PSScriptRoot
$source = Join-Path $kits 'Assets\UdonKits'

if (-not (Test-Path (Join-Path $World 'Assets')) -or -not (Test-Path (Join-Path $World 'Packages'))) {
  throw "Not a Unity project (no Assets and Packages folders): $World"
}
$manifestPath = Join-Path $World 'Packages\vpm-manifest.json'
if (Test-Path $manifestPath) {
  $vpm = Get-Content -Raw $manifestPath | ConvertFrom-Json
  if (-not $vpm.dependencies.'com.vrchat.worlds') { Write-Warning 'This project has no com.vrchat.worlds package: the kits need the VRChat Worlds SDK (UdonSharp is part of it).' }
}

if ($RunTests) {
  if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'dotnet is not on PATH. Install the .NET 8 SDK (winget install Microsoft.DotNet.SDK.8), or leave out -RunTests.' }
  & dotnet run --project (Join-Path $kits 'Tests') -c Release
  if ($LASTEXITCODE -ne 0) { throw "The kit tests failed (exit $LASTEXITCODE). Nothing was copied." }
}

$target = Join-Path $World 'Assets\UdonKits'
$copied = 0; $same = 0
$sourceFull = (Resolve-Path $source).Path
# pairs of (from, to): the folder's own .meta sits beside the folder, then every file inside it
$pairs = @(, @((Join-Path $kits 'Assets\UdonKits.meta'), (Join-Path $World 'Assets\UdonKits.meta')))
foreach ($f in Get-ChildItem -Recurse -File $source) {
  $pairs += , @($f.FullName, (Join-Path $target $f.FullName.Substring($sourceFull.Length).TrimStart('\', '/')))
}
foreach ($p in $pairs) {
  $from = $p[0]; $dest = $p[1]
  if ((Test-Path $dest) -and ((Get-FileHash $dest).Hash -eq (Get-FileHash $from).Hash)) { $same++; continue }
  if ($Apply) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest) | Out-Null
    Copy-Item -Force $from $dest
    Write-Host "copied   $dest"
  } else {
    Write-Host "would copy $dest"
  }
  $copied++
}

if ($Apply) { Write-Host "Done: $copied copied, $same already up to date, in $target" }
else { Write-Host "Dry run: $copied to copy, $same already up to date. Add -Apply to copy." }
