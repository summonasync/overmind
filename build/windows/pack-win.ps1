<#
pack-win.ps1 - build the Overmind Windows installer (dist/overmind-<version>-setup.exe).

Usage: build\windows\pack-win.ps1 -Version <x.y.z> -BundleVersion <int> [-CrewKit <path>] [-SkipWeb]
       build\windows\pack-win.ps1 -BundleVersion <int> -KitsOnly [-CrewKit <path>]

-CrewKit defaults to <repo>\src\OvermindCrewKit (the in-repo crew kit); the flow is repo -> bundle -> home.

Publishes host + mcp as self-contained win-x64, stages the web static export and the six bundled
kit folders cut from kit-membership.json into dist\stage-win following build\dist-layout.env, then
compiles build\windows\overmind.iss with Inno Setup. Emits the setup exe plus a .sha256 sidecar
next to it in dist\. -KitsOnly cuts the kit folders plus manifest.json into dist\kits and stops:
no publish, no web build, no Inno Setup — the kit-split verification leg on a machine without the
installer toolchain.
#>

param(
    [string]$Version = '',

    [Parameter(Mandatory = $true)]
    [int]$BundleVersion,

    [string]$CrewKit = '',

    [switch]$SkipWeb,

    [switch]$KitsOnly
)

$ErrorActionPreference = 'Stop'

function Fail {
    param([string]$Message)
    [Console]::Error.WriteLine("pack-win.ps1: $Message")
    exit 1
}

if (-not $KitsOnly) {
    if ($Version -notmatch '^\d+\.\d+\.\d+$') {
        Fail "-Version must look like x.y.z (got '$Version')"
    }
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

if (-not (Test-Path $CrewKit)) {
    Fail "crew kit source not found: $CrewKit"
}

$kitDirty = & git -C $CrewKit status --porcelain 2>$null
if ($LASTEXITCODE -eq 0 -and $kitDirty) {
    Write-Warning "kit working tree has uncommitted changes; bundling as-is: $CrewKit"
}

function Test-CommandOnPath {
    param([string]$Name)
    $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

if (-not $KitsOnly -and -not (Test-CommandOnPath 'dotnet')) {
    Fail "dotnet is required but not on PATH"
}

if (-not $KitsOnly -and -not (Test-CommandOnPath 'npm')) {
    Fail "npm is required but not on PATH"
}

if (-not $KitsOnly -and -not (Test-Path (Join-Path $webDir 'node_modules'))) {
    Fail "web\node_modules is missing; run 'npm ci' in web/ first"
}

$iscc = $null
if (-not $KitsOnly) {
    $isccCandidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )

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

function Read-LayoutList {
    param([hashtable]$Contract, [string]$Key, [bool]$Required = $true)

    $raw = $Contract[$Key]
    if (-not $raw) {
        if ($Required) {
            Fail "layout contract is missing $Key"
        }
        return @()
    }
    return @($raw -split '\s+' | Where-Object { $_ -ne '' })
}

# Each bundled kit folder carries the canonical side of its membership items: side-prefixed paths
# lose the prefix, side-unique prose lands at the kit root under its own name, and every other
# item is a mirror copy that materialization regenerates per platform. Excludes drop machine state.
function Test-KitExcluded {
    param([string]$Relative, [string[]]$Excludes)

    foreach ($exclude in $Excludes) {
        if (-not $exclude) {
            continue
        }
        if ($Relative -eq $exclude -or $Relative.StartsWith($exclude + '/', [StringComparison]::Ordinal)) {
            return $true
        }
    }
    return $false
}

function Get-KitItemDest {
    param(
        [string]$Item,
        [string[]]$Sides,
        [string[]]$CrossSideItems,
        [string[]]$Excludes
    )

    foreach ($side in $Sides) {
        $prefix = $side + '/'
        if ($Item.StartsWith($prefix, [StringComparison]::Ordinal)) {
            $relative = $Item.Substring($prefix.Length)
            if (Test-KitExcluded -Relative $relative -Excludes $Excludes) {
                return ''
            }
            return $relative
        }
    }
    foreach ($cross in $CrossSideItems) {
        if ($cross -and $Item -eq $cross) {
            # Strip the leading segment and keep the rest, matching pack.sh (${cross#*/}) and the
            # side branch above; Split-Path -Leaf would flatten a nested cross-side item and the
            # two pack emitters would disagree on the kit folder layout. Membership paths are
            # slash-delimited on both platforms.
            $slash = $cross.IndexOf('/')
            $relative = if ($slash -ge 0) { $cross.Substring($slash + 1) } else { $cross }
            if (Test-KitExcluded -Relative $relative -Excludes $Excludes) {
                return ''
            }
            return $relative
        }
    }
    return ''
}

$layout = Read-LayoutContract -Path $layoutPath
$requiredKeys = @('BUNDLED_KITS', 'KIT_SIDES', 'KIT_MEMBERSHIP_FILE', 'DIR_HOST', 'DIR_MCP', 'DIR_WEB', 'DIR_BUNDLED')
foreach ($key in $requiredKeys) {
    if (-not $layout[$key]) {
        Fail "layout contract is missing $key"
    }
}

$bundledKits = Read-LayoutList -Contract $layout -Key 'BUNDLED_KITS'
$mandatoryKits = Read-LayoutList -Contract $layout -Key 'MANDATORY_KITS' -Required $false
$legacyKits = Read-LayoutList -Contract $layout -Key 'LEGACY_KITS' -Required $false
$kitSides = Read-LayoutList -Contract $layout -Key 'KIT_SIDES'
$crossSideItems = Read-LayoutList -Contract $layout -Key 'KIT_CROSS_SIDE_ITEMS' -Required $false
$kitExcludes = Read-LayoutList -Contract $layout -Key 'KIT_EXCLUDES' -Required $false

foreach ($kit in $mandatoryKits) {
    if ($bundledKits -notcontains $kit) {
        Fail "MANDATORY_KITS names '$kit', which is not a bundled kit"
    }
}
foreach ($kit in $legacyKits) {
    if ($bundledKits -contains $kit) {
        Fail "LEGACY_KITS names '$kit', which is also shipped - drop it from one of the two lists"
    }
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$membershipPath = Join-Path $CrewKit $layout['KIT_MEMBERSHIP_FILE']
if (-not (Test-Path -LiteralPath $membershipPath)) {
    Fail "kit membership manifest not found: $membershipPath"
}
$membership = Get-Content -LiteralPath $membershipPath -Raw | ConvertFrom-Json
$declaredKits = @($membership.PSObject.Properties.Name)
foreach ($kit in $bundledKits) {
    if ($declaredKits -notcontains $kit) {
        Fail "kit membership manifest has no entry for bundled kit '$kit'"
    }
    if (@($membership.$kit).Count -lt 1) {
        Fail "kit membership manifest entry for '$kit' is empty"
    }
}
foreach ($kit in $declaredKits) {
    if ($bundledKits -notcontains $kit) {
        Fail "kit membership manifest declares '$kit', which BUNDLED_KITS does not ship"
    }
}

function Stage-BundledKits {
    param([string]$BundledDir)

    $totalSkills = 0
    $totalAgents = 0
    foreach ($kit in $bundledKits) {
        $kitDir = Join-Path $BundledDir $kit
        New-Item -ItemType Directory -Path $kitDir -Force | Out-Null
        $staged = 0
        $mirrored = 0
        $skills = 0
        $agents = 0
        $prose = 0
        foreach ($item in @($membership.$kit)) {
            $source = Join-Path $CrewKit ($item -replace '[\\/]', [IO.Path]::DirectorySeparatorChar)
            if (-not (Test-Path -LiteralPath $source)) {
                Fail "kit '$kit' item not found: $source"
            }
            $relative = Get-KitItemDest -Item $item -Sides $kitSides -CrossSideItems $crossSideItems -Excludes $kitExcludes
            if (-not $relative) {
                $mirrored++
                continue
            }
            $destination = Join-Path $kitDir ($relative -replace '[\\/]', [IO.Path]::DirectorySeparatorChar)
            if (Test-Path -LiteralPath $destination) {
                Fail "kit '${kit}': two membership items land on '$relative'"
            }
            $parent = Split-Path -Path $destination -Parent
            if ($parent -and -not (Test-Path -LiteralPath $parent)) {
                New-Item -ItemType Directory -Path $parent -Force | Out-Null
            }
            Copy-Item -LiteralPath $source -Destination $destination -Recurse
            $staged++
            if ($relative.StartsWith('skills/', [StringComparison]::Ordinal)) {
                $skills++
            }
            elseif ($relative.StartsWith('agents/', [StringComparison]::Ordinal)) {
                $agents++
            }
            elseif (-not $relative.Contains('/')) {
                $prose++
            }
        }

        $diskSkills = 0
        $diskAgents = 0
        $skillsDir = Join-Path $kitDir 'skills'
        $agentsDir = Join-Path $kitDir 'agents'
        if (Test-Path -LiteralPath $skillsDir) {
            $diskSkills = (Get-ChildItem -LiteralPath $skillsDir -Recurse -Filter 'SKILL.md' | Measure-Object).Count
        }
        if (Test-Path -LiteralPath $agentsDir) {
            $diskAgents = (Get-ChildItem -LiteralPath $agentsDir -File | Measure-Object).Count
        }
        $diskProse = (Get-ChildItem -LiteralPath $kitDir -File | Measure-Object).Count
        if ($diskSkills -ne $skills) {
            Fail "kit '$kit' staged $skills skills, disk holds $diskSkills"
        }
        if ($diskAgents -ne $agents) {
            Fail "kit '$kit' staged $agents agents, disk holds $diskAgents"
        }
        if ($diskProse -ne $prose) {
            Fail "kit '$kit' staged $prose root files, disk holds $diskProse"
        }

        Write-Host ("==> kit {0,-18} {1,3} items staged: {2,2} skills, {3,2} agents, {4,2} prose ({5} mirror copies skipped)" -f `
            $kit, $staged, $skills, $agents, $prose, $mirrored)
        $totalSkills += $skills
        $totalAgents += $agents
    }

    $sourceSkills = 0
    $sourceAgents = 0
    $claudeRoot = Join-Path $CrewKit '.claude'
    $sourceSkillsDir = Join-Path $claudeRoot 'skills'
    $sourceAgentsDir = Join-Path $claudeRoot 'agents'
    if (Test-Path -LiteralPath $sourceSkillsDir) {
        $sourceSkills = (Get-ChildItem -LiteralPath $sourceSkillsDir -Recurse -Filter 'SKILL.md' | Measure-Object).Count
    }
    if (Test-Path -LiteralPath $sourceAgentsDir) {
        $sourceAgents = (Get-ChildItem -LiteralPath $sourceAgentsDir -File | Measure-Object).Count
    }
    if ($sourceSkills -lt 1) {
        Fail "no SKILL.md found under $CrewKit; refusing to ship an empty bundle"
    }
    if ($totalSkills -ne $sourceSkills) {
        Fail "kit split lost skills: the bundled kits stage $totalSkills of $sourceSkills"
    }
    if ($totalAgents -ne $sourceAgents) {
        Fail "kit split lost agent definitions: the bundled kits stage $totalAgents of $sourceAgents"
    }

    $kitObjects = foreach ($kit in $bundledKits) {
        [ordered]@{ name = $kit; mandatory = ($mandatoryKits -contains $kit) }
    }
    $manifest = [ordered]@{
        bundleVersion = $BundleVersion
        kits          = @($kitObjects)
        legacy        = @($legacyKits)
    }
    $manifestJson = (ConvertTo-Json -InputObject $manifest -Depth 5 -Compress) + "`n"
    [System.IO.File]::WriteAllText((Join-Path $BundledDir 'manifest.json'), $manifestJson, $utf8NoBom)

    Write-Host ("==> bundled kits: {0} folders, {1} skills, {2} agents, legacy {3}" -f `
        $bundledKits.Count, $totalSkills, $totalAgents, ($legacyKits -join ' '))
}

if ($KitsOnly) {
    $kitsDir = Join-Path $distDir 'kits'
    if (Test-Path -LiteralPath $kitsDir) {
        Remove-Item -LiteralPath $kitsDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $kitsDir -Force | Out-Null
    Write-Host "==> cutting bundled kits into $kitsDir (no publish, no installer)"
    Stage-BundledKits -BundledDir $kitsDir
    Write-Host "==> done: kit staging only, nothing published"
    exit 0
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

Write-Host "==> cutting bundled kits"
Stage-BundledKits -BundledDir (Join-Path $hostStageDir $layout.DIR_BUNDLED)

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
    Fail "staged payload does not fit at the default install location: '$maxRelPath' ($maxRelPathLength chars) plus a $defaultRootLength-char worst-case default root (20-char username, the Windows SAM account name limit) exceeds the $maxPathBudget-char path budget by $overflow char(s); shorten or exclude it via KIT_EXCLUDES in build\dist-layout.env"
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
