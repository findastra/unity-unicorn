# Unity Unicorn: open the Unity editor with the newest project made with its version (Unity Stable's Unity light).
#
# Unity.exe started with no project only starts Unity Hub, hidden in the tray, and the editor never opens (Unity Stable
# button audit, 2026-11-12). This opens the newest project in Unity's own recent list that was made with this editor
# version, the one at the top of Unity Hub's list. Inference Iguana's Benchmark Rock runs it when Unity's red light is
# pressed (program-watch-parts-20261113.json); it can also be run by hand.
#
# It prints one line saying what it did and exits 0 when Unity was started, 1 when it was not.
#   -DryRun   only says which project it would open; starts nothing (the self-test uses it)
#   -Recent   a list of project folders to use instead of Unity's own recent list (the self-test uses it)

param(
    [string]$Version = '2022.3.22f1',
    [switch]$DryRun,
    [string[]]$Recent
)

$ErrorActionPreference = 'Stop'
$name = "Unity $Version"

function Get-UnityRecent {
    # RecentlyUsedProjectPaths-<n>_h<hash> under Unity's editor key, newest first: each a path as UTF-8 bytes ending in
    # a zero.
    $found = @{}
    try {
        $key = Get-Item -LiteralPath 'HKCU:\Software\Unity Technologies\Unity Editor 5.x' -ErrorAction Stop
        foreach ($value in $key.GetValueNames()) {
            if ($value -notmatch '^RecentlyUsedProjectPaths-(\d+)(_h\d+)?$') { continue }
            $n = [int]$Matches[1]
            $data = $key.GetValue($value)
            if ($data -is [byte[]]) { $path = [Text.Encoding]::UTF8.GetString($data).TrimEnd([char]0) } else { $path = [string]$data }
            if ($path) { $found[$n] = $path }
        }
    } catch { }
    return @($found.Keys | Sort-Object | ForEach-Object { $found[$_] })
}

function Find-Newest([string[]]$list, [string]$version) {
    foreach ($p in $list) {
        try {
            $path = $p.Replace('/', '\')
            $stamp = Join-Path $path 'ProjectSettings\ProjectVersion.txt'
            if ((Test-Path -LiteralPath (Join-Path $path 'Assets') -PathType Container) -and (Test-Path -LiteralPath $stamp -PathType Leaf) -and
                ((Get-Content -LiteralPath $stamp -Raw) -match [regex]::Escape("m_EditorVersion: $version"))) { return $path }
        } catch { }
    }
    return $null
}

if (-not $PSBoundParameters.ContainsKey('Recent')) { $Recent = Get-UnityRecent }
$project = Find-Newest $Recent $Version
if (-not $project) {
    Write-Output "${name}: no project made with Unity $Version was found in Unity's recent list. Open one from Unity Hub."
    exit 1
}
$exe = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$Version\Editor\Unity.exe"
if ($DryRun) { Write-Output "$name would open $(Split-Path $project -Leaf) ($project) with $exe."; exit 0 }
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { Write-Output "$name is not installed at $exe."; exit 1 }
Start-Process -FilePath $exe -ArgumentList @('-projectPath', "`"$project`"")
# A project takes a minute or two to load; the light turns green once the editor's window is up.
Write-Output "$name is opening $(Split-Path $project -Leaf), the newest project made with it. Its light turns green when the editor's window is up (a minute or two)."
exit 0
