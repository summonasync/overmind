param([switch]$DryRun)

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
$homeExists = @{}
foreach ($key in $homePaths.Keys) {
    $gate = $homePaths[$key]
    if ($key -eq 'codex-skills') { $gate = Join-Path $HOME '.codex' }  # runtime marker gates the relocated skills root
    $homeExists[$key] = Test-Path $gate
    if (-not $homeExists[$key]) {
        Write-Host "Home $gate not found, skipping its entries."
        continue
    }
    if ($key -eq 'codex-skills' -and -not (Test-Path $homePaths[$key])) {
        if ($DryRun) { Write-Host "Would mkdir -p $($homePaths[$key])" }
        else { New-Item -ItemType Directory -Path $homePaths[$key] -Force | Out-Null }
    }
}

function Invoke-Mirror([string]$Src, [string]$Dst, [bool]$Dry, [bool]$Summary = $true) {
    $flags = @('/MIR', '/NFL', '/NDL', '/NJH')
    if (-not $Summary) { $flags += '/NJS' }   # merge mode mirrors dozens of children; per-child summaries drown the plan
    if ($Dry) { robocopy $Src $Dst @flags /L }
    else { robocopy $Src $Dst @flags }
    if ($LASTEXITCODE -ge 8) {
        Write-Host "robocopy failed (exit $LASTEXITCODE) for $Src -> $Dst"
        exit $LASTEXITCODE
    }
}

function Copy-FileEntry([string]$Src, [string]$DstDir, [bool]$Dry) {
    if ($Dry) { Write-Host "Would copy $Src -> $DstDir" }
    else {
        if (-not (Test-Path $DstDir)) { New-Item -ItemType Directory -Path $DstDir -Force | Out-Null }
        Copy-Item $Src $DstDir -Force
    }
}

foreach ($entry in $manifest.entries) {
    if (-not $homeExists[$entry.home]) { continue }
    $destHome = $homePaths[$entry.home]
    $src = Join-Path $root $entry.source
    $name = Split-Path -Leaf $entry.source

    if ($entry.type -eq 'dir') {
        $dst = Join-Path $destHome $name
        if ($entry.mode -eq 'merge') {
            # Mirror each child separately: third-party content living next to the crew's own
            # entries in $dst (e.g. non-crew skills in ~/.qwen/skills) survives the install.
            foreach ($child in Get-ChildItem $src -Force) {
                if ($child.PSIsContainer) {
                    $childDst = Join-Path $dst $child.Name
                    if ($DryRun) { Write-Host "Would mirror $($child.FullName) -> $childDst" }
                    Invoke-Mirror $child.FullName $childDst $DryRun $false
                }
                else { Copy-FileEntry $child.FullName $dst $DryRun }
            }
        }
        else { Invoke-Mirror $src $dst $DryRun }
    }
    else {
        # '===OVERMIND' marks the 251 global-deploy managed zone: a live entry doc carrying it
        # is owned by the app door — a whole-file copy here would erase its marker blocks.
        $dest = Join-Path $destHome $name
        if ((Test-Path $dest) -and (Select-String -Path $dest -Pattern '===OVERMIND' -Quiet)) {
            Write-Host "Skipping ${name}: live entry doc carries an OVERMIND marker block"
        }
        else { Copy-FileEntry $src $destHome $DryRun }
    }
}

if ($DryRun) { Write-Host 'Dry run complete - no changes made.' }
exit 0
