param(
    [switch]$DryRun,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$manifest = Get-Content (Join-Path $root 'manifest.json') -Raw | ConvertFrom-Json

$homePaths = @{
    kimi           = Join-Path $HOME '.kimi-code'
    claude         = Join-Path $HOME '.claude'
    qwen           = Join-Path $HOME '.qwen'
    codex          = Join-Path $HOME '.codex'
    'codex-skills' = Join-Path $HOME '.agents'
}

$targets = @()
foreach ($entry in $manifest.entries) {
    $destHome = $homePaths[$entry.home]
    $gate = $destHome
    if ($entry.home -eq 'codex-skills') { $gate = Join-Path $HOME '.codex' }  # same runtime gate as install; ~/.agents may not exist yet
    if (-not (Test-Path $gate)) { continue }
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

    # '===OVERMIND' marks the 251 managed zone: a woven entry doc belongs to the app door,
    # not to this kit — uninstall never removes it.
    $target = Join-Path $destHome $name
    if ($entry.type -eq 'file' -and (Test-Path $target) -and (Select-String -Path $target -Pattern '===OVERMIND' -Quiet)) {
        Write-Host "Skipping ${name}: live entry doc carries an OVERMIND marker block"
        continue
    }

    $targets += [pscustomobject]@{ Path = $target; Type = $entry.type; Home = $destHome }
    # Crew skills installed before the .agents relocation live under the old home root
    # ($HOME/.codex/$name) — dead weight after the move, scrubbed when present.
    if ($entry.home -eq 'codex-skills') {
        $legacy = Join-Path (Join-Path $HOME '.codex') $name
        if (Test-Path $legacy) {
            $targets += [pscustomobject]@{ Path = $legacy; Type = 'dir'; Home = (Join-Path $HOME '.codex') }
        }
    }
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
