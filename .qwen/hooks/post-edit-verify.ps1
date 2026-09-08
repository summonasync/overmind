# Qwen Code PostToolUse hook — crew build/lint gate (see QWEN.md «Deterministic Enforcement»).
#
# Contract: hook event JSON on stdin. Exit 2 + stderr = blocking error, Qwen feeds stderr back
# to the model. Exit 0 = success, nothing injected.
#
# FAILS OPEN on purpose: no payload, unknown tool, no project anchor, missing toolchain,
# uninstalled dependencies — all exit 0 silently. A verification hook that wedges every edit
# is worse than no hook at all.

$ErrorActionPreference = 'Stop'

try { $raw = [Console]::In.ReadToEnd() } catch { exit 0 }
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }
try { $evt = $raw | ConvertFrom-Json } catch { exit 0 }

if ("$($evt.tool_name)" -notin @('edit', 'write_file', 'notebook_edit')) { exit 0 }

$edited = "$($evt.tool_input.file_path)"
if ([string]::IsNullOrWhiteSpace($edited)) { exit 0 }
if (-not (Test-Path -LiteralPath $edited -PathType Leaf)) { exit 0 }

function Find-Anchor {
    param([string]$From, [string[]]$Markers)
    try { $dir = Split-Path -Parent (Resolve-Path -LiteralPath $From).Path } catch { return $null }
    while ($dir) {
        foreach ($m in $Markers) {
            if (Get-ChildItem -LiteralPath $dir -Filter $m -File -Force -ErrorAction SilentlyContinue) { return $dir }
        }
        $parent = Split-Path -Parent $dir
        if (-not $parent -or $parent -eq $dir) { return $null }
        $dir = $parent
    }
    return $null
}

function Fail([string]$message) {
    [Console]::Error.WriteLine($message)
    exit 2
}

function Tail([string]$text, [int]$lines) {
    (($text -split "\r?\n") | Where-Object { $_ -match '\S' } | Select-Object -Last $lines) -join "`n"
}

$ext = [System.IO.Path]::GetExtension($edited).ToLowerInvariant()

if ($ext -eq '.cs') {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { exit 0 }
    $anchor = Find-Anchor -From $edited -Markers @('*.csproj', '*.sln')
    if (-not $anchor) { exit 0 }

    $out = & dotnet build $anchor --nologo 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { Fail "dotnet build FAILED in $anchor`n$(Tail $out 40)" }
    exit 0
}

if (@('.ts', '.tsx', '.js', '.jsx', '.css') -contains $ext) {
    if ($edited -match '[\\/]node_modules[\\/]') { exit 0 }
    if (-not (Get-Command npm -ErrorAction SilentlyContinue)) { exit 0 }
    $anchor = Find-Anchor -From $edited -Markers @('package.json')
    if (-not $anchor -or $anchor -match '[\\/]node_modules([\\/]|$)') { exit 0 }
    if (-not (Test-Path -LiteralPath (Join-Path $anchor 'node_modules') -PathType Container)) { exit 0 }

    $pkg = $null
    try { $pkg = Get-Content -LiteralPath (Join-Path $anchor 'package.json') -Raw | ConvertFrom-Json } catch { exit 0 }

    $report = ''
    $failed = $false

    $hasLint = $pkg -and $pkg.scripts -and (@($pkg.scripts.PSObject.Properties.Name) -contains 'lint')
    if ($hasLint) {
        Push-Location -LiteralPath $anchor
        try { $lintOut = & npm run lint 2>&1 | Out-String; $lintCode = $LASTEXITCODE } finally { Pop-Location }
        if ($lintCode -ne 0) { $failed = $true; $report += "npm run lint FAILED`n$(Tail $lintOut 30)`n" }
    }

    $tsConfig = Join-Path $anchor 'tsconfig.json'
    $tscBin = Join-Path $anchor 'node_modules\typescript'
    if ((Test-Path -LiteralPath $tsConfig -PathType Leaf) -and (Test-Path -LiteralPath $tscBin)) {
        Push-Location -LiteralPath $anchor
        try { $tscOut = & npx tsc --noEmit 2>&1 | Out-String; $tscCode = $LASTEXITCODE } finally { Pop-Location }
        if ($tscCode -ne 0) { $failed = $true; $report += "npx tsc --noEmit FAILED`n$(Tail $tscOut 30)`n" }
    }

    if ($failed) { Fail "frontend verification FAILED in $anchor`n$report" }
    exit 0
}

exit 0
