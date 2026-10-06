<#
pack-win.ps1 - build the Overmind Windows installer (dist/overmind-<version>-setup.exe).

Usage: build\windows\pack-win.ps1 -Version <x.y.z> -BundleVersion <int> [-CrewKit <path>] [-SkipWeb]

-CrewKit defaults to <repo>\src\OvermindCrewKit (the embedded crew clone); the flow is repo -> bundle -> home.

Publishes host + mcp as self-contained win-x64, stages the web static export and the crew kit
into dist\stage-win following build\dist-layout.env, then compiles build\windows\overmind.iss
with Inno Setup. Emits the setup exe plus a .sha256 sidecar next to it in dist\.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [int]$BundleVersion,

    [string]$CrewKit = '',

    [switch]$SkipWeb
)

$ErrorActionPreference = 'Stop'

function Fail {
    param([string]$Message)
    [Console]::Error.WriteLine("pack-win.ps1: $Message")
    exit 1
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Fail "-Version must look like x.y.z (got '$Version')"
}

if ($BundleVersion -lt 1) {
    Fail "-BundleVersion must be >= 1 (got $BundleVersion)"
}

$scriptDir = $PSScriptRoot
$buildDir = Split-Path -Parent $scriptDir
$repoRoot = Split-Path -Parent $buildDir
$webDir = Join-Path $repoRoot 'web'
$distDir = Join-Path $repoRoot 'dist'
$stageDir = Join-Path $distDir 'stage-win'
$issPath = Join-Path $scriptDir 'overmind.iss'
$iconPath = Join-Path $repoRoot 'src\Overmind.Host\Assets\overmind.ico'
$layoutPath = Join-Path $buildDir 'dist-layout.env'

if (-not $CrewKit) {
    $CrewKit = Join-Path $repoRoot 'src\OvermindCrewKit'
}

function Test-CommandOnPath {
    param([string]$Name)
    $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

if (-not (Test-CommandOnPath 'dotnet')) {
    Fail "dotnet is required but not on PATH"
}

if (-not (Test-CommandOnPath 'npm')) {
    Fail "npm is required but not on PATH"
}

if (-not (Test-Path (Join-Path $webDir 'node_modules'))) {
    Fail "web\node_modules is missing; run 'npm ci' in web/ first"
}

$isccCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
)

$iscc = $null
foreach ($candidate in $isccCandidates) {
    if ($candidate -and (Test-Path $candidate)) {
        $iscc = $candidate
        break
    }
}

if (-not $iscc) {
    $onPath = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($onPath) {
        $iscc = $onPath.Source
    }
}

if (-not $iscc) {
    Fail "ISCC.exe (Inno Setup 6) not found; install it with 'winget install --id JRSoftware.InnoSetup'"
}

if (-not (Test-Path $iconPath)) {
    Fail "src\Overmind.Host\Assets\overmind.ico not found"
}

if (-not (Test-Path $layoutPath)) {
    Fail "layout contract not found: $layoutPath"
}

function Read-LayoutContract {
    param([string]$Path)

    $contract = @{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*"?([^"#]*?)"?\s*$') {
            $contract[$matches[1]] = $matches[2].Trim()
        }
    }
    return $contract
}

$layout = Read-LayoutContract -Path $layoutPath
$crewItems = $layout.CREW_ITEMS -split '\s+' | Where-Object { $_ -ne '' }
$crewExcludesRaw = $layout.CREW_EXCLUDES
if ($null -eq $crewExcludesRaw) {
    $crewExcludesRaw = ''
}
$crewExcludes = $crewExcludesRaw -split '\s+' | Where-Object { $_ -ne '' }

if (-not (Test-Path $CrewKit)) {
    Fail "crew kit source not found: $CrewKit"
}

$kitDirty = & git -C $CrewKit status --porcelain 2>$null
if ($LASTEXITCODE -eq 0 -and $kitDirty) {
    Write-Warning "kit working tree has uncommitted changes; bundling as-is: $CrewKit"
}

foreach ($item in $crewItems) {
    $itemPath = Join-Path $CrewKit $item
    if (-not (Test-Path $itemPath)) {
        Fail "crew kit item not found: $itemPath"
    }
}

$skillCount = 0
foreach ($sub in @('.claude\skills', '.claude\agents')) {
    $subPath = Join-Path $CrewKit $sub
    if (Test-Path $subPath) {
        $skillCount += (Get-ChildItem -LiteralPath $subPath -Recurse -Filter 'SKILL.md' -ErrorAction SilentlyContinue | Measure-Object).Count
    }
}
if ($skillCount -lt 1) {
    Fail "no SKILL.md found under $CrewKit; refusing to ship an empty crew kit"
}

if (Test-Path $stageDir) {
    Remove-Item -LiteralPath $stageDir -Recurse -Force
}
New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
New-Item -ItemType Directory -Path $distDir -Force | Out-Null

$hostStageDir = Join-Path $stageDir $layout.DIR_HOST
$mcpStageDir = Join-Path $stageDir $layout.DIR_MCP
$webStageDir = Join-Path $stageDir $layout.DIR_WEB

if (-not $SkipWeb) {
    Write-Host "==> building web export"
    Push-Location $webDir
    try {
        npm run build
        if ($LASTEXITCODE -ne 0) {
            Fail "npm run build failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

$webOutIndex = Join-Path $webDir 'out\index.html'
if (-not (Test-Path $webOutIndex)) {
    Fail "web build did not produce web\out\index.html"
}

Write-Host "==> publishing host for win-x64"
dotnet publish (Join-Path $repoRoot 'src\Overmind.Host\Overmind.Host.csproj') `
    -c Release -r win-x64 --self-contained "-p:Version=$Version" -o $hostStageDir
if ($LASTEXITCODE -ne 0) {
    Fail "dotnet publish (host) failed with exit code $LASTEXITCODE"
}

Write-Host "==> publishing mcp adapter for win-x64"
dotnet publish (Join-Path $repoRoot 'src\Overmind.Mcp\Overmind.Mcp.csproj') `
    -c Release -r win-x64 --self-contained "-p:Version=$Version" -o $mcpStageDir
if ($LASTEXITCODE -ne 0) {
    Fail "dotnet publish (mcp) failed with exit code $LASTEXITCODE"
}

Write-Host "==> staging web export"
New-Item -ItemType Directory -Path $webStageDir -Force | Out-Null
Copy-Item -Path (Join-Path $webDir 'out\*') -Destination $webStageDir -Recurse -Force

Write-Host "==> staging crew kit"
$bundledDir = Join-Path $hostStageDir $layout.DIR_BUNDLED
$kitDir = Join-Path $bundledDir $layout.CREW_KIT_NAME
New-Item -ItemType Directory -Path $kitDir -Force | Out-Null
foreach ($item in $crewItems) {
    Copy-Item -Path (Join-Path $CrewKit $item) -Destination $kitDir -Recurse -Force
}
foreach ($exclude in $crewExcludes) {
    $excludePath = Join-Path $kitDir ($exclude -replace '/', '\')
    if (Test-Path $excludePath) {
        Remove-Item -LiteralPath $excludePath -Recurse -Force
    }
}

$manifestPath = Join-Path $bundledDir 'manifest.json'
$manifestJson = '{"bundleVersion":' + $BundleVersion + ',"kits":["' + $layout.CREW_KIT_NAME + '"]}'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($manifestPath, $manifestJson, $utf8NoBom)

Write-Host "==> measuring staged payload path lengths"
$maxRelPathLength = 0
$maxRelPath = ''
foreach ($file in Get-ChildItem -LiteralPath $stageDir -Recurse -File) {
    $relPath = $file.FullName.Substring($stageDir.Length + 1)
    if ($relPath.Length -gt $maxRelPathLength) {
        $maxRelPathLength = $relPath.Length
        $maxRelPath = $relPath
    }
}

$maxPathBudget = 259

$defaultRootTemplate = 'C:\Users\' + ('x' * 20) + '\AppData\Local\Programs\Overmind'
$defaultRootLength = $defaultRootTemplate.Length

$worstCaseLength = $defaultRootLength + 1 + $maxRelPathLength
if ($worstCaseLength -gt $maxPathBudget) {
    $overflow = $worstCaseLength - $maxPathBudget
    Fail "staged payload does not fit at the default install location: '$maxRelPath' ($maxRelPathLength chars) plus a $defaultRootLength-char worst-case default root (20-char username, the Windows SAM account name limit) exceeds the $maxPathBudget-char path budget by $overflow char(s); shorten or exclude it via CREW_EXCLUDES in build\dist-layout.env"
}

Write-Host "==> compiling installer"
& $iscc "/DAppVersion=$Version" "/DPayloadDir=$stageDir" "/DOutputDir=$distDir" "/DMaxPayloadPath=$maxRelPathLength" $issPath
if ($LASTEXITCODE -ne 0) {
    Fail "ISCC.exe failed with exit code $LASTEXITCODE"
}

$setupPath = Join-Path $distDir "overmind-$Version-setup.exe"
if (-not (Test-Path $setupPath)) {
    Fail "expected artifact not found: $setupPath"
}

$hash = (Get-FileHash -LiteralPath $setupPath -Algorithm SHA256).Hash.ToLowerInvariant()
$setupName = Split-Path -Leaf $setupPath
$sidecarPath = "$setupPath.sha256"
$sidecarLine = "$hash  $setupName`n"
$asciiNoBom = New-Object System.Text.ASCIIEncoding
[System.IO.File]::WriteAllText($sidecarPath, $sidecarLine, $asciiNoBom)

$sizeBytes = (Get-Item -LiteralPath $setupPath).Length
$sizeMb = [Math]::Round($sizeBytes / 1MB, 2)
Write-Host "==> packed $setupName ($sizeMb MB)"
Write-Host "==> artifact: $setupPath"
Write-Host "==> longest staged payload path: '$maxRelPath' ($maxRelPathLength chars); worst-case default-root margin: $($maxPathBudget - $worstCaseLength) char(s)"
