# grimoire-kit installer — merge-safe: kit files land in kit-owned subpaths, foreign
# content in the destination homes (skills, hooks, settings.json entries) survives.
param(
    [string]$BaseHome,
    [ValidateSet('claude', 'qwen', 'all')]
    [string]$Runtime = 'all',
    [switch]$Uninstall,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$manifestPath = Join-Path $root 'manifest.json'
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$mergeTool = Join-Path $root 'tools/settings_merge.py'
$marker = 'hooks/grimoire-kit/'

if (-not $BaseHome) { $BaseHome = $env:HOME }
if (-not $BaseHome) { $BaseHome = $env:USERPROFILE }
if (-not $BaseHome) { Write-Host 'No HOME resolved; pass -BaseHome DIR'; exit 1 }

if (-not (Get-Command python3 -ErrorAction SilentlyContinue)) {
    Write-Host 'python3 is required (settings.json hook merge)'
    exit 1
}

$realHome = $env:HOME
if (-not $realHome) { $realHome = $env:USERPROFILE }
$sandbox = ($BaseHome -ne $realHome)

$homePaths = @{
    claude = Join-Path $BaseHome '.claude'
    qwen   = Join-Path $BaseHome '.qwen'
}

function Test-HomeActive([string]$RuntimeName) {
    $hp = $homePaths[$RuntimeName]
    if ($sandbox) {
        if (-not $DryRun) { New-Item -ItemType Directory -Path $hp -Force | Out-Null }
        return $true
    }
    return (Test-Path $hp)
}

function Copy-MirrorDir([string]$Src, [string]$Dst) {
    if ($DryRun) { Write-Host "Would mirror $Src -> $Dst"; return }
    if (Test-Path $Dst) { Remove-Item $Dst -Recurse -Force }
    New-Item -ItemType Directory -Path $Dst -Force | Out-Null
    Copy-Item (Join-Path $Src '*') $Dst -Force
}

function Copy-FileEntry([string]$Src, [string]$DstDir) {
    if ($DryRun) { Write-Host "Would copy $Src -> $DstDir"; return }
    if (-not (Test-Path $DstDir)) { New-Item -ItemType Directory -Path $DstDir -Force | Out-Null }
    Copy-Item $Src $DstDir -Force
}

function Remove-KitPath([string]$Path) {
    if ($DryRun) { if (Test-Path $Path) { Write-Host "Would remove $Path" }; return }
    if (Test-Path $Path) { Remove-Item $Path -Recurse -Force; Write-Host "Removed $Path" }
}

foreach ($entry in $manifest.entries) {
    if ($Runtime -ne 'all' -and $Runtime -ne $entry.home) { continue }
    if (-not (Test-HomeActive $entry.home)) {
        Write-Host "Home $($homePaths[$entry.home]) not found, skipping its entries."
        continue
    }
    $hp = $homePaths[$entry.home]
    $src = Join-Path $root $entry.source
    $dest = Join-Path $hp ($entry.dest -replace '/', [System.IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path $src)) { Write-Host "Missing kit source $src"; exit 1 }

    if ($Uninstall) {
        if ($entry.mode -eq 'merge') {
            foreach ($child in Get-ChildItem $src -Force) {
                Remove-KitPath (Join-Path $dest $child.Name)
            }
        }
        else { Remove-KitPath $dest }
        continue
    }

    if ($entry.type -eq 'dir') {
        if ($entry.mode -eq 'merge') {
            foreach ($child in Get-ChildItem $src -Force) {
                if ($child.PSIsContainer) { Copy-MirrorDir $child.FullName (Join-Path $dest $child.Name) }
                else { Copy-FileEntry $child.FullName $dest }
            }
        }
        else { Copy-MirrorDir $src $dest }
    }
    else { Copy-FileEntry $src (Split-Path -Parent $dest) }
}

foreach ($rt in @('claude', 'qwen')) {
    if ($Runtime -ne 'all' -and $Runtime -ne $rt) { continue }
    if (-not (Test-HomeActive $rt)) {
        Write-Host "Home $($homePaths[$rt]) not found, skipping its hook wiring."
        continue
    }
    $settings = Join-Path $homePaths[$rt] $manifest.hooks.$rt.settings
    if ($DryRun) {
        $verb = if ($Uninstall) { "Would remove grimoire-kit hook entries from $settings" }
                else { "Would merge hook entry into $settings [$($manifest.hooks.$rt.event)]" }
        Write-Host $verb
        continue
    }
    if ($Uninstall) {
        & python3 $mergeTool unmerge $settings $marker
    }
    else {
        $entryJson = (& python3 $mergeTool entry $root $rt) -join ''
        & python3 $mergeTool merge $settings $manifest.hooks.$rt.event $entryJson $marker
    }
    if ($LASTEXITCODE -ne 0) { Write-Host "settings merge failed for $settings (exit $LASTEXITCODE)"; exit $LASTEXITCODE }
}

if ($Uninstall -and -not $DryRun) {
    foreach ($entry in $manifest.entries) {
        if ($Runtime -ne 'all' -and $Runtime -ne $entry.home) { continue }
        if (-not (Test-HomeActive $entry.home)) { continue }
        $dest = Join-Path $homePaths[$entry.home] ($entry.dest -replace '/', [System.IO.Path]::DirectorySeparatorChar)
        $dir = if ($entry.type -eq 'dir') { $dest } else { Split-Path -Parent $dest }
        while ($dir -and $dir -ne $BaseHome -and (Test-Path $dir) -and -not (Get-ChildItem $dir -Force)) {
            Remove-Item $dir -Force
            Write-Host "Removed empty dir $dir"
            $dir = Split-Path -Parent $dir
        }
    }
}

if ($DryRun) { Write-Host 'Dry run complete - no changes made.' }
if (-not $Uninstall) { Write-Host "grimoire-kit install complete (runtime: $Runtime)." }
exit 0
