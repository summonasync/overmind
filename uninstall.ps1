param(
    [switch]$DryRun,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$manifest = Get-Content (Join-Path $root 'manifest.json') -Raw | ConvertFrom-Json

$homePaths = @{
    kimi        = Join-Path $HOME '.kimi-code'
    claude      = Join-Path $HOME '.claude'
    gemini      = Join-Path $HOME '.gemini'
    antigravity = Join-Path $HOME '.gemini\antigravity'
    qwen        = Join-Path $HOME '.qwen'
}

$targets = @()
foreach ($entry in $manifest.entries) {
    $destHome = $homePaths[$entry.home]
    if (-not (Test-Path $destHome)) { continue }
    $name = Split-Path -Leaf $entry.source

    if ($entry.type -eq 'dir' -and $entry.mode -eq 'merge') {
        # Merge-mode deploys children individually, so removal is per child too — the shared
        # destination directory itself may hold third-party content we must not touch.
        # Limitation: only children that still exist in the repo are known to the manifest.
        $srcDir = Join-Path $root $entry.source
        if (Test-Path $srcDir) {
            foreach ($child in Get-ChildItem $srcDir -Force) {
                $targets += [pscustomobject]@{ Path = (Join-Path (Join-Path $destHome $name) $child.Name); Type = 'child'; Home = $destHome }
            }
        }
        continue
    }

    $targets += [pscustomobject]@{ Path = (Join-Path $destHome $name); Type = $entry.type; Home = $destHome }
}

$targets = @($targets | Sort-Object -Property Path -Unique)

if ($DryRun) {
    foreach ($t in $targets) { Write-Host "Would remove $($t.Path)" }
    Write-Host 'Dry run complete - no changes made.'
    exit 0
}

if (-not $Force) {
    Write-Host 'The following paths will be removed:'
    foreach ($t in $targets) { Write-Host "  $($t.Path)" }
    $answer = Read-Host 'Proceed? [y/N]'
    if ($answer -notmatch '^(y|yes)$') { Write-Host 'Aborted.'; exit 0 }
}

foreach ($t in $targets) {
    if (Test-Path $t.Path) {
        Remove-Item $t.Path -Recurse -Force
        Write-Host "Removed $($t.Path)"
    }
}

$parents = $targets | ForEach-Object { Split-Path -Parent $_.Path } | Sort-Object -Unique
foreach ($parent in $parents) {
    $isHomeRoot = ($targets.Home | Where-Object { $_ -eq $parent })
    if ($isHomeRoot) { continue }
    if ((Test-Path $parent) -and -not (Get-ChildItem $parent -Force)) {
        Remove-Item $parent -Force
        Write-Host "Removed empty dir $parent"
    }
}
exit 0
