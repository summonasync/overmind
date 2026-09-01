param([switch]$DryRun)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$manifest = Get-Content (Join-Path $root 'manifest.json') -Raw | ConvertFrom-Json

$homePaths = @{
    kimi        = Join-Path $HOME '.kimi-code'
    claude      = Join-Path $HOME '.claude'
    gemini      = Join-Path $HOME '.gemini'
    antigravity = Join-Path $HOME '.gemini\antigravity'
}
$homeExists = @{}
foreach ($key in $homePaths.Keys) {
    $homeExists[$key] = (Test-Path $homePaths[$key]) -or ($key -eq 'antigravity' -and (Test-Path (Join-Path $HOME '.gemini')))
    if (-not $homeExists[$key]) {
        Write-Host "Home $($homePaths[$key]) not found, skipping its entries."
    }
}

foreach ($entry in $manifest.entries) {
    if (-not $homeExists[$entry.home]) { continue }
    $destHome = $homePaths[$entry.home]
    $src = Join-Path $root $entry.source
    $name = Split-Path -Leaf $entry.source

    if ($entry.type -eq 'dir') {
        $dst = Join-Path $destHome $name
        if ($DryRun) { robocopy $src $dst /MIR /L /NFL /NDL /NJH }
        else { robocopy $src $dst /MIR /NFL /NDL /NJH }
        if ($LASTEXITCODE -ge 8) {
            Write-Host "robocopy failed (exit $LASTEXITCODE) for entry '$($entry.source)' -> $dst"
            exit $LASTEXITCODE
        }
    }
    else {
        if ($DryRun) { Write-Host "Would copy $src -> $destHome" }
        else { Copy-Item $src $destHome -Force }
    }
}

if ($DryRun) { Write-Host 'Dry run complete - no changes made.' }
exit 0
