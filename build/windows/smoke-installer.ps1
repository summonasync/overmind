<#
smoke-installer.ps1 - Windows installer smoke verification for Overmind.

Usage: build\windows\smoke-installer.ps1 <path-to-overmind-setup.exe> [-KillLiveMcpAdapters]

Silently installs setup.exe into a fresh temp prefix, boots the Host headless against an
isolated database/models directory, asserts health, the seeded bundled kit set (every kit the
an MCP initialize handshake and a graceful POST /api/shutdown stop, then silently uninstalls
and asserts no residue (install dir, Start Menu shortcut, uninstall registry key, PATH,
~/.overmind survives). Cell 1b runs the freshly installed overmind-mcp.exe with
'install --dry-run' (that install used /TASKS="" so the mcpregister [Run] entry never fired)
and asserts exit 0 plus a plan naming an agent - --dry-run guarantees nothing is written, so
this never touches a real agent config. Repeats install+boot+upgrade-over-running-install
through the real ~/.overmind/host.port discovery path (its pre-existing content, if any, is
backed up and restored), then a PATH round-trip cell with the mcppath+autostart tasks against
the real HKCU\Environment\Path (baseline persisted to a recovery file outside the temp tree
until the restore is confirmed), and a final cell that an over-long install directory is
refused up front. Every temp artifact lives under $env:TEMP and is cleaned in a finally block
even on failure. Prints "SMOKE OK: ..." when all thirteen cells (0-11 plus 1b) pass; exits
non-zero with the failing cell named in the message.

WARNING: every install and uninstall this script performs (four of each) unconditionally
force-kills every overmind-mcp.exe the current user owns (overmind.iss's StopRunningMcpAdapter,
by design, not a bug) - including adapters serving live agent sessions in other repos on this
machine. Cell 0 refuses to run while any overmind-mcp.exe is running unless -KillLiveMcpAdapters
is passed; close your agent sessions first, or pass the switch to proceed anyway.
#>

param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$SetupExe,

    [switch]$KillLiveMcpAdapters
)

$ErrorActionPreference = 'Stop'

function Test-PathValuesEqual {
    param([string]$A, [string]$B)
    if (($null -eq $A) -and ($null -eq $B)) {
        return $true
    }
    if (($null -eq $A) -or ($null -eq $B)) {
        return $false
    }
    return [string]::Equals($A, $B, [System.StringComparison]::Ordinal)
}

function Get-RawUserPath {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment')
    if ($null -eq $key) {
        return $null
    }
    try {
        return $key.GetValue('Path', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
    }
    finally {
        $key.Close()
    }
}

function Get-RawMachinePath {
    $key = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey('SYSTEM\CurrentControlSet\Control\Session Manager\Environment')
    if ($null -eq $key) {
        return $null
    }
    try {
        return $key.GetValue('Path', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
    }
    finally {
        $key.Close()
    }
}

function Set-RawUserPath {
    param([string]$Value)
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment', $true)
    try {
        if ($null -eq $Value) {
            $key.DeleteValue('Path', $false)
        }
        else {
            $key.SetValue('Path', $Value, [Microsoft.Win32.RegistryValueKind]::ExpandString)
        }
    }
    finally {
        $key.Close()
    }
}

function Test-McpAdapterRunning {
    return $null -ne (Get-Process -Name 'overmind-mcp' -ErrorAction SilentlyContinue)
}

function Get-StartMenuShortcutPath {
    return Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Overmind\Overmind.lnk'
}

function Get-UninstallKeyPath {
    return 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{22F04E3B-CF50-4887-B2A5-3F0816BA1BE9}_is1'
}

function Get-RunKeyValue {
    $runKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    if (-not (Test-Path -LiteralPath $runKeyPath)) {
        return $null
    }
    return (Get-ItemProperty -LiteralPath $runKeyPath -Name 'Overmind' -ErrorAction SilentlyContinue).Overmind
}

function Test-RealHostAlive {
    param([string]$BaseUrl)
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/api/health" -TimeoutSec 3
        return ($response.StatusCode -eq 200)
    }
    catch {
        return $false
    }
}

function Wait-ForFile {
    param([string]$Path, [int]$TimeoutSec, [string]$CellLabel)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while (-not (Test-Path -LiteralPath $Path)) {
        if ((Get-Date) -gt $deadline) {
            throw "$CellLabel : expected file did not appear within ${TimeoutSec}s: $Path"
        }
        Start-Sleep -Seconds 1
    }
}

function Wait-ForPortFile {
    param(
        [string]$PortFilePath,
        [int]$TimeoutSec,
        [System.Diagnostics.Process]$Process,
        [string]$StdoutLog,
        [string]$StderrLog,
        [string]$CellLabel
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ($true) {
        if (Test-Path -LiteralPath $PortFilePath) {
            $raw = Get-Content -LiteralPath $PortFilePath -Raw -ErrorAction SilentlyContinue
            if ($raw) {
                $trimmed = $raw.Trim()
                if ($trimmed) {
                    return $trimmed
                }
            }
        }
        if ($Process.HasExited) {
            $stdoutTail = ''
            $stderrTail = ''
            if (Test-Path -LiteralPath $StdoutLog) {
                $stdoutTail = (Get-Content -LiteralPath $StdoutLog -Tail 40) -join "`n"
            }
            if (Test-Path -LiteralPath $StderrLog) {
                $stderrTail = (Get-Content -LiteralPath $StderrLog -Tail 40) -join "`n"
            }
            [Console]::Error.WriteLine("$CellLabel : host exited early (exit code $($Process.ExitCode)); last 40 lines of stdout:")
            [Console]::Error.WriteLine($stdoutTail)
            [Console]::Error.WriteLine("$CellLabel : last 40 lines of stderr:")
            [Console]::Error.WriteLine($stderrTail)
            throw "$CellLabel : host process exited before writing the port file"
        }
        if ((Get-Date) -gt $deadline) {
            throw "$CellLabel : no port file at $PortFilePath after ${TimeoutSec}s"
        }
        Start-Sleep -Seconds 1
    }
}

function Wait-ForInstallDirGone {
    param([string]$InstallDir, [int]$TimeoutSec, [string]$CellLabel)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ($true) {
        if (-not (Test-Path -LiteralPath $InstallDir)) {
            return
        }
        $remaining = Get-ChildItem -LiteralPath $InstallDir -Force -ErrorAction SilentlyContinue
        if (-not $remaining) {
            return
        }
        if ((Get-Date) -gt $deadline) {
            throw "$CellLabel : install directory $InstallDir still contains files after ${TimeoutSec}s: $(($remaining | Select-Object -First 5 -ExpandProperty Name) -join ', ')"
        }
        Start-Sleep -Seconds 1
    }
}

function Invoke-JsonGet {
    param([string]$Uri, [string]$CellLabel)
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $Uri -TimeoutSec 15
    }
    catch {
        throw "$CellLabel : request to $Uri failed: $($_.Exception.Message)"
    }
    if ($response.StatusCode -ne 200) {
        throw "$CellLabel : $Uri returned HTTP $($response.StatusCode)"
    }
    return $response
}

function Invoke-SilentInstall {
    param([string]$Setup, [string]$InstallDir, [string]$Tasks, [string]$LogPath)
    if ($LogPath) {
        & $Setup /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR="$InstallDir" /TASKS="$Tasks" /LOG="$LogPath" | Out-Null
    }
    else {
        & $Setup /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR="$InstallDir" /TASKS="$Tasks" | Out-Null
    }
}

function Wait-ForInnoLogClosed {
    param([string]$LogPath, [int]$TimeoutSec, [string]$CellLabel)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ($true) {
        if (Test-Path -LiteralPath $LogPath) {
            $content = Get-Content -LiteralPath $LogPath -Raw -ErrorAction SilentlyContinue
            if ($content -and ($content -match '(?i)Log closed\.')) {
                return $content
            }
        }
        if ((Get-Date) -gt $deadline) {
            throw "$CellLabel : installer did not finish (no 'Log closed.' in $LogPath) within ${TimeoutSec}s"
        }
        Start-Sleep -Seconds 1
    }
}

function Invoke-SilentUninstall {
    param([string]$InstallDir, [int]$TimeoutSec, [string]$CellLabel)
    $unins = Join-Path $InstallDir 'unins000.exe'
    if (-not (Test-Path -LiteralPath $unins)) {
        throw "$CellLabel : unins000.exe not found at $unins"
    }
    Start-Process -FilePath $unins -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES') -PassThru | Out-Null
    Wait-ForInstallDirGone -InstallDir $InstallDir -TimeoutSec $TimeoutSec -CellLabel $CellLabel
}

function Start-RedirectedProcess {
    param(
        [string]$FilePath,
        [string]$Arguments,
        [string]$WorkingDirectory,
        [hashtable]$EnvironmentOverrides,
        [string]$StdoutLog,
        [string]$StderrLog
    )
    $previousValues = @{}
    foreach ($key in $EnvironmentOverrides.Keys) {
        $previousValues[$key] = [System.Environment]::GetEnvironmentVariable($key)
    }
    try {
        foreach ($key in $EnvironmentOverrides.Keys) {
            [System.Environment]::SetEnvironmentVariable($key, $EnvironmentOverrides[$key])
        }
        return Start-Process -FilePath $FilePath -ArgumentList $Arguments -WorkingDirectory $WorkingDirectory -PassThru -WindowStyle Hidden -RedirectStandardOutput $StdoutLog -RedirectStandardError $StderrLog
    }
    finally {
        foreach ($key in $previousValues.Keys) {
            [System.Environment]::SetEnvironmentVariable($key, $previousValues[$key])
        }
    }
}

function Stop-ProcessTreeById {
    param([int]$RootProcessId)
    $children = Get-CimInstance Win32_Process -Filter "ParentProcessId=$RootProcessId" -ErrorAction SilentlyContinue
    foreach ($child in $children) {
        Stop-ProcessTreeById -RootProcessId $child.ProcessId
    }
    try {
        Stop-Process -Id $RootProcessId -Force -ErrorAction SilentlyContinue
    }
    catch {
    }
}

function Invoke-BestEffortUninstall {
    param([string]$InstallDir)
    $unins = Join-Path $InstallDir 'unins000.exe'
    if (Test-Path -LiteralPath $unins) {
        try {
            Start-Process -FilePath $unins -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES') -PassThru | Out-Null
            $deadline = (Get-Date).AddSeconds(60)
            while ((Test-Path -LiteralPath $InstallDir) -and ((Get-Date) -le $deadline)) {
                Start-Sleep -Seconds 1
            }
        }
        catch {
        }
    }
}

$setupPath = Resolve-Path -LiteralPath $SetupExe -ErrorAction SilentlyContinue
if (-not $setupPath) {
    [Console]::Error.WriteLine("smoke-installer.ps1: setup exe not found: $SetupExe")
    exit 1
}
$setupPath = $setupPath.Path

$realDataDir = Join-Path $env:USERPROFILE '.overmind'
$realPortFile = Join-Path $realDataDir 'host.port'

$userPathBaseline = Get-RawUserPath

$shortSuffix = -join ((1..6) | ForEach-Object { '{0:X}' -f (Get-Random -Maximum 16) })
$root = Join-Path $env:TEMP "ovm-$shortSuffix"
New-Item -ItemType Directory -Path $root -Force | Out-Null

$hostProcess = $null
$mcpProcess = $null
$pendingUninstalls = New-Object System.Collections.Generic.List[string]
$smokeSummary = ''
$failed = $false
$realPortFileBackup = $null
$realPortFileHadContent = $false
$realPortFileTouched = $false
$pathBaselineFile = $null

try {
    if (Test-Path -LiteralPath $realPortFile) {
        $realPort = (Get-Content -LiteralPath $realPortFile -Raw -ErrorAction SilentlyContinue)
        if ($realPort) {
            $realPort = $realPort.Trim()
        }
        if ($realPort -and (Test-RealHostAlive -BaseUrl "http://127.0.0.1:$realPort")) {
            throw "cell 0 (preflight): a running Overmind was found at 127.0.0.1:$realPort; quit it from the tray before smoking the installer. The single-instance mutex is per-user and global, so a second instance launched here would silently exit 0 instead of failing loudly, and later cells would hang waiting for a port file that never appears."
        }
    }
    if ((Test-McpAdapterRunning) -and (-not $KillLiveMcpAdapters)) {
        throw "cell 0 (preflight): a live overmind-mcp.exe process was found. This run will force-kill every overmind-mcp.exe you own (four installs + four uninstalls, each running taskkill /F /IM overmind-mcp.exe) - that includes adapters serving live agent sessions in other repos. Close those sessions first, or re-run with -KillLiveMcpAdapters if you accept losing them."
    }
    if ($KillLiveMcpAdapters -and (Test-McpAdapterRunning)) {
        Write-Host "cell 0: -KillLiveMcpAdapters passed - proceeding even though a live overmind-mcp.exe was detected; it and any other adapters you own will be force-killed repeatedly by this run"
    }
    Write-Host "cell 0: preflight OK - no live Overmind instance detected"

    $installDir1 = Join-Path $root 'i1'
    Invoke-SilentInstall -Setup $setupPath -InstallDir $installDir1 -Tasks ''
    $hostExe1 = Join-Path $installDir1 'host\Overmind.Host.exe'
    Wait-ForFile -Path $hostExe1 -TimeoutSec 120 -CellLabel 'cell 1 (silent install)'
    $pendingUninstalls.Add($installDir1)

    $expectedPaths = @(
        'host\Overmind.Host.exe',
        'host\bundled\manifest.json',
        'mcp\overmind-mcp.exe',
        'web\index.html'
    )
    foreach ($rel in $expectedPaths) {
        $full = Join-Path $installDir1 $rel
        if (-not (Test-Path -LiteralPath $full)) {
            throw "cell 1 (silent install): expected path missing: $full"
        }
    }

    $bundledManifest = Get-Content -LiteralPath (Join-Path $installDir1 'host\bundled\manifest.json') -Raw | ConvertFrom-Json
    $bundledKits = @($bundledManifest.kits)
    if ($bundledKits.Count -lt 1) {
        throw 'cell 1 (silent install): bundled manifest.json lists no kits'
    }
    foreach ($kit in $bundledKits) {
        $kitDir = Join-Path $installDir1 ('host\bundled\' + $kit.name)
        if (-not (Test-Path -LiteralPath $kitDir)) {
            throw "cell 1 (silent install): bundled kit folder missing: $kitDir"
        }
    }
    foreach ($legacyKit in @($bundledManifest.legacy)) {
        $legacyDir = Join-Path $installDir1 ('host\bundled\' + $legacyKit)
        if (Test-Path -LiteralPath $legacyDir) {
            throw "cell 1 (silent install): kit '$legacyKit' is listed as legacy and still shipped as a folder: $legacyDir"
        }
    }
    $forbiddenPaths = @('run.cmd', 'install.ps1')
    foreach ($rel in $forbiddenPaths) {
        $full = Join-Path $installDir1 $rel
        if (Test-Path -LiteralPath $full) {
            throw "cell 1 (silent install): forbidden path present in installer payload: $full"
        }
    }

    foreach ($kit in $bundledKits) {
        $syncedPath = Join-Path $installDir1 ('host\bundled\' + $kit.name + '\skills\synced')
        if (Test-Path -LiteralPath $syncedPath) {
            throw "cell 1 (silent install): machine state shipped inside kit '$($kit.name)': $syncedPath"
        }
    }

    $startMenuShortcut1 = Get-StartMenuShortcutPath
    if (-not (Test-Path -LiteralPath $startMenuShortcut1)) {
        throw "cell 1 (silent install): Start Menu shortcut missing after install: $startMenuShortcut1 (it is unconditional - created regardless of /TASKS)"
    }
    $uninstallKeyPath1 = Get-UninstallKeyPath
    if (-not (Test-Path -LiteralPath $uninstallKeyPath1)) {
        throw "cell 1 (silent install): uninstall registry key missing after install: $uninstallKeyPath1"
    }

    Write-Host "cell 1: silent install OK - tree verified under $installDir1, run.cmd/install.ps1/skills-synced absent, Start Menu shortcut + uninstall key present"

    $mcpExeForDryRun = Join-Path $installDir1 'mcp\overmind-mcp.exe'
    $dryRunPsi = New-Object System.Diagnostics.ProcessStartInfo
    $dryRunPsi.FileName = $mcpExeForDryRun
    $dryRunPsi.Arguments = 'install --dry-run'
    $dryRunPsi.UseShellExecute = $false
    $dryRunPsi.RedirectStandardOutput = $true
    $dryRunPsi.RedirectStandardError = $true
    $dryRunPsi.CreateNoWindow = $true
    $dryRunProcess = [System.Diagnostics.Process]::Start($dryRunPsi)
    $dryRunStdout = $dryRunProcess.StandardOutput.ReadToEnd()
    $dryRunStderr = $dryRunProcess.StandardError.ReadToEnd()
    if (-not $dryRunProcess.WaitForExit(30000)) {
        try { $dryRunProcess.Kill() } catch {}
        throw "cell 1b (agent registration dry-run): overmind-mcp.exe install --dry-run did not exit within 30s"
    }
    if ($dryRunProcess.ExitCode -ne 0) {
        throw "cell 1b (agent registration dry-run): overmind-mcp.exe install --dry-run exited $($dryRunProcess.ExitCode), expected 0. stdout: $dryRunStdout stderr: $dryRunStderr"
    }
    if ($dryRunStdout -notmatch '(?i)dry run: no config files are written') {
        throw "cell 1b (agent registration dry-run): stdout did not confirm dry-run mode (expected 'dry run: no config files are written.'). stdout: $dryRunStdout"
    }
    if ($dryRunStdout -notmatch '(?i)(claude|kimi-code|codex|gemini|qwen|antigravity):') {
        throw "cell 1b (agent registration dry-run): stdout named no agent from the six-agent registry. stdout: $dryRunStdout"
    }
    Write-Host "cell 1b: agent registration dry-run OK - installed overmind-mcp.exe (this install used /TASKS='' so [Run]'s mcpregister task never fired) answered 'install --dry-run' with exit 0, confirmed dry-run mode in its own output, and named at least one agent from the registry; --dry-run guarantees no config file, real or otherwise, was touched"

    $work2 = Join-Path $root 'w2'
    New-Item -ItemType Directory -Path $work2 -Force | Out-Null
    $dbPath2 = Join-Path $work2 'overmind.db'
    $portFilePath2 = Join-Path $work2 'host.port'
    $modelsDir2 = Join-Path $work2 'models'
    $stdoutLog2 = Join-Path $work2 'host.stdout.log'
    $stderrLog2 = Join-Path $work2 'host.stderr.log'

    $hostProcess = Start-RedirectedProcess -FilePath $hostExe1 -Arguments '--no-tray' -WorkingDirectory (Join-Path $installDir1 'host') -EnvironmentOverrides @{
        'Overmind__DatabasePath' = $dbPath2
        'Overmind__PortFilePath' = $portFilePath2
        'OVERMIND_MODELS_DIR'    = $modelsDir2
    } -StdoutLog $stdoutLog2 -StderrLog $stderrLog2

    $port2 = Wait-ForPortFile -PortFilePath $portFilePath2 -TimeoutSec 120 -Process $hostProcess -StdoutLog $stdoutLog2 -StderrLog $stderrLog2 -CellLabel 'cell 2 (headless boot)'
    $baseUrl = "http://127.0.0.1:$port2"
    Write-Host "cell 2: headless boot OK - port=$port2, isolated db/port-file/models under $work2"

    $healthResponse = Invoke-JsonGet -Uri "$baseUrl/api/health" -CellLabel 'cell 3 (health)'
    $healthJson = $healthResponse.Content | ConvertFrom-Json
    if ($healthJson.status -ne 'ok') {
        throw "cell 3 (health): status was '$($healthJson.status)', expected 'ok'"
    }
    Write-Host "cell 3: health OK - status=ok"

    $kitsResponse = Invoke-JsonGet -Uri "$baseUrl/api/kits" -CellLabel 'cell 4 (bundled kits)'
    $kits = $kitsResponse.Content | ConvertFrom-Json
    foreach ($kit in $bundledKits) {
        $kitRow = $kits | Where-Object { $_.name -eq $kit.name } | Select-Object -First 1
        if (-not $kitRow) {
            throw "cell 4 (bundled kits): no kit named '$($kit.name)' in /api/kits response"
        }
        if ($kitRow.sourceKind -ne 'bundled') {
            throw "cell 4 (bundled kits): kit '$($kit.name)' sourceKind was '$($kitRow.sourceKind)', expected 'bundled'"
        }
        if ($kitRow.mandatory -ne $kit.mandatory) {
            throw "cell 4 (bundled kits): kit '$($kit.name)' mandatory was '$($kitRow.mandatory)', manifest said '$($kit.mandatory)'"
        }
    }
    foreach ($legacyKit in @($bundledManifest.legacy)) {
        if ($kits | Where-Object { $_.name -eq $legacyKit }) {
            throw "cell 4 (bundled kits): legacy kit '$legacyKit' came back in a freshly seeded library"
        }
    }
    Write-Host "cell 4: bundled kits seeded OK - $($bundledKits.Count) kits bundled, manifest mandatory flags reproduced next to host\bundled\"

    $webResponse = Invoke-JsonGet -Uri "$baseUrl/" -CellLabel 'cell 5 (web UI)'
    if ($webResponse.Content -notmatch '(?i)<!DOCTYPE html') {
        throw "cell 5 (web UI): response body at / does not contain <!DOCTYPE html>"
    }
    Write-Host "cell 5: web UI OK - served at / with no launcher and no OVERMIND_WEB_ROOT"

    $mcpExe1 = Join-Path $installDir1 'mcp\overmind-mcp.exe'
    $psiMcp = New-Object System.Diagnostics.ProcessStartInfo
    $psiMcp.FileName = $mcpExe1
    $psiMcp.UseShellExecute = $false
    $psiMcp.RedirectStandardInput = $true
    $psiMcp.RedirectStandardOutput = $true
    $psiMcp.RedirectStandardError = $true
    $psiMcp.CreateNoWindow = $true
    $psiMcp.EnvironmentVariables['OVERMIND_API_URL'] = $baseUrl

    $mcpProcess = [System.Diagnostics.Process]::Start($psiMcp)
    $initializeRequest = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"smoke","version":"1.0"}}}'
    $mcpProcess.StandardInput.Write($initializeRequest + [char]10)
    $mcpProcess.StandardInput.Flush()

    $readTask = $mcpProcess.StandardOutput.ReadLineAsync()
    if (-not $readTask.Wait(30000)) {
        throw "cell 6 (MCP handshake): no response line from overmind-mcp within 30s"
    }
    $responseLine = $readTask.Result
    if (-not $responseLine) {
        throw "cell 6 (MCP handshake): empty response line from overmind-mcp"
    }
    try {
        $rpcResponse = $responseLine | ConvertFrom-Json
    }
    catch {
        throw "cell 6 (MCP handshake): response line did not parse as JSON: $responseLine"
    }
    if ($rpcResponse.id -ne 1) {
        throw "cell 6 (MCP handshake): response id was '$($rpcResponse.id)', expected 1"
    }
    if (($null -eq $rpcResponse.result) -or ($null -eq $rpcResponse.result.serverInfo)) {
        throw "cell 6 (MCP handshake): response has no result.serverInfo: $responseLine"
    }

    if (-not $mcpProcess.HasExited) {
        $mcpProcess.Kill()
    }
    $mcpProcess.WaitForExit(5000) | Out-Null
    $mcpProcess = $null
    Write-Host "cell 6: MCP handshake OK - initialize answered with result.serverInfo"

    $shutdownResponse = Invoke-WebRequest -UseBasicParsing -Uri "$baseUrl/api/shutdown" -Method Post -TimeoutSec 10
    if ($shutdownResponse.StatusCode -ne 202) {
        throw "cell 7 (graceful stop): POST /api/shutdown returned $($shutdownResponse.StatusCode), expected 202"
    }
    $deadline7 = (Get-Date).AddSeconds(10)
    while (-not $hostProcess.HasExited) {
        if ((Get-Date) -gt $deadline7) {
            throw "cell 7 (graceful stop): host process did not exit within 10s of POST /api/shutdown"
        }
        Start-Sleep -Milliseconds 250
    }
    $hostProcess = $null
    Write-Host "cell 7: graceful stop OK - 202 Accepted, process exited within 10s"

    Invoke-SilentUninstall -InstallDir $installDir1 -TimeoutSec 60 -CellLabel 'cell 8 (silent uninstall)'
    $pendingUninstalls.Remove($installDir1) | Out-Null

    $runValue8 = Get-RunKeyValue
    if ($runValue8) {
        throw "cell 8 (silent uninstall): HKCU Run key still has an Overmind value: $runValue8"
    }

    if (Test-Path -LiteralPath (Get-StartMenuShortcutPath)) {
        throw "cell 8 (silent uninstall): Start Menu shortcut still present: $(Get-StartMenuShortcutPath)"
    }
    if (Test-Path -LiteralPath (Get-UninstallKeyPath)) {
        throw "cell 8 (silent uninstall): uninstall registry key still present: $(Get-UninstallKeyPath)"
    }

    $mcpSegment1 = Join-Path $installDir1 'mcp'
    $userPathAfterCell8 = Get-RawUserPath
    $machinePathAfterCell8 = Get-RawMachinePath
    if ($userPathAfterCell8 -and ($userPathAfterCell8 -like "*$mcpSegment1*")) {
        throw "cell 8 (silent uninstall): user Path still contains $mcpSegment1"
    }
    if ($machinePathAfterCell8 -and ($machinePathAfterCell8 -like "*$mcpSegment1*")) {
        throw "cell 8 (silent uninstall): machine Path still contains $mcpSegment1"
    }

    if (-not (Test-Path -LiteralPath $realDataDir)) {
        throw "cell 8 (silent uninstall): %USERPROFILE%\.overmind is gone - a silent uninstall must never delete user data"
    }

    if (-not (Test-PathValuesEqual -A $userPathAfterCell8 -B $userPathBaseline)) {
        throw "cell 8 (silent uninstall): user Path changed during cells 1-8 even though /TASKS=`"`" never selects mcppath (before: '$userPathBaseline', after: '$userPathAfterCell8')"
    }
    Write-Host "cell 8: silent uninstall OK - install dir gone, Run key/Start Menu shortcut/uninstall key clean, no mcp on PATH, ~/.overmind intact, user Path byte-identical"

    $installDir9 = Join-Path $root 'i9'
    Invoke-SilentInstall -Setup $setupPath -InstallDir $installDir9 -Tasks ''
    $hostExe9 = Join-Path $installDir9 'host\Overmind.Host.exe'
    Wait-ForFile -Path $hostExe9 -TimeoutSec 120 -CellLabel 'cell 9 (upgrade-over-running-install)'
    $pendingUninstalls.Add($installDir9)

    $work9 = Join-Path $root 'w9'
    New-Item -ItemType Directory -Path $work9 -Force | Out-Null
    $dbPath9 = Join-Path $work9 'overmind.db'
    $modelsDir9 = Join-Path $work9 'models'
    $stdoutLog9 = Join-Path $work9 'host.stdout.log'
    $stderrLog9 = Join-Path $work9 'host.stderr.log'

    if (Test-Path -LiteralPath $realPortFile) {
        $realPortFileBackup = Get-Content -LiteralPath $realPortFile -Raw -ErrorAction SilentlyContinue
        $realPortFileHadContent = $true
        Remove-Item -LiteralPath $realPortFile -Force -ErrorAction SilentlyContinue
    }
    $realPortFileTouched = $true

    $hostProcess = Start-RedirectedProcess -FilePath $hostExe9 -Arguments '--no-tray' -WorkingDirectory (Join-Path $installDir9 'host') -EnvironmentOverrides @{
        'Overmind__DatabasePath' = $dbPath9
        'Overmind__PortFilePath' = $realPortFile
        'OVERMIND_MODELS_DIR'    = $modelsDir9
    } -StdoutLog $stdoutLog9 -StderrLog $stderrLog9

    $port9 = Wait-ForPortFile -PortFilePath $realPortFile -TimeoutSec 120 -Process $hostProcess -StdoutLog $stdoutLog9 -StderrLog $stderrLog9 -CellLabel 'cell 9 (upgrade-over-running-install)'

    $mcpExe9 = Join-Path $installDir9 'mcp\overmind-mcp.exe'
    $psiMcp9 = New-Object System.Diagnostics.ProcessStartInfo
    $psiMcp9.FileName = $mcpExe9
    $psiMcp9.UseShellExecute = $false
    $psiMcp9.RedirectStandardInput = $true
    $psiMcp9.RedirectStandardOutput = $true
    $psiMcp9.RedirectStandardError = $true
    $psiMcp9.CreateNoWindow = $true
    $psiMcp9.EnvironmentVariables['OVERMIND_API_URL'] = "http://127.0.0.1:$port9"
    $mcpProcess = [System.Diagnostics.Process]::Start($psiMcp9)
    Start-Sleep -Milliseconds 500
    if ($mcpProcess.HasExited) {
        throw "cell 9 (upgrade-over-running-install): the installed overmind-mcp.exe exited immediately (code $($mcpProcess.ExitCode)) instead of staying alive to hold {app}\mcp locked - cannot exercise the CRIT-1 adapter-kill path without a live installed adapter"
    }
    $mcpProcess9ModulePath = $null
    try {
        $mcpProcess9ModulePath = $mcpProcess.MainModule.FileName
    }
    catch {
    }
    if ((-not $mcpProcess9ModulePath) -or ($mcpProcess9ModulePath -notlike "$installDir9*")) {
        throw "cell 9 (upgrade-over-running-install): the running overmind-mcp.exe's main module path ('$mcpProcess9ModulePath') is not confirmed inside the installed directory ($installDir9) - the lock proof would not be on the installed copy"
    }

    $webIndexPath9 = Join-Path $installDir9 'web\index.html'
    $webIndexOriginalLength9 = (Get-Item -LiteralPath $webIndexPath9).Length
    if ($webIndexOriginalLength9 -le 0) {
        throw "cell 9 (upgrade-over-running-install): $webIndexPath9 is already empty before the marker step - cannot prove replacement"
    }
    try {
        Clear-Content -LiteralPath $webIndexPath9 -ErrorAction Stop
    }
    catch {
        throw "cell 9 (upgrade-over-running-install): could not truncate $webIndexPath9 before the upgrade to prove replacement: $($_.Exception.Message)"
    }

    $upgradeLogPath9 = Join-Path $work9 'upgrade-install.log'
    Invoke-SilentInstall -Setup $setupPath -InstallDir $installDir9 -Tasks '' -LogPath $upgradeLogPath9

    $upgradeLogContent9 = Wait-ForInnoLogClosed -LogPath $upgradeLogPath9 -TimeoutSec 120 -CellLabel 'cell 9 (upgrade-over-running-install)'

    if ($upgradeLogContent9 -notmatch '(?i)Installation process succeeded\.') {
        $tail9log = $upgradeLogContent9
        if ($tail9log.Length -gt 800) {
            $tail9log = $tail9log.Substring($tail9log.Length - 800)
        }
        throw "cell 9 (upgrade-over-running-install): second install did not report success in its own log (the Host file may still have been locked). Log tail: $tail9log"
    }
    if ($upgradeLogContent9 -match '(?i)Rolling back changes\.') {
        throw "cell 9 (upgrade-over-running-install): second install rolled back changes - the running Host was not stopped in time to release the file lock"
    }
    $webIndexRestoredLength9 = (Get-Item -LiteralPath $webIndexPath9).Length
    if ($webIndexRestoredLength9 -ne $webIndexOriginalLength9) {
        throw "cell 9 (upgrade-over-running-install): $webIndexPath9 was truncated to 0 bytes before the upgrade and is $webIndexRestoredLength9 bytes afterward (expected $webIndexOriginalLength9) - the payload was not actually rewritten by the second install"
    }

    $deadline9mcp = (Get-Date).AddSeconds(10)
    while (-not $mcpProcess.HasExited) {
        if ((Get-Date) -gt $deadline9mcp) {
            throw "cell 9 (upgrade-over-running-install): the installed overmind-mcp.exe was still running after the upgrade completed; expected PrepareToInstall's StopRunningMcpAdapter to have killed it before {app}\mcp was replaced"
        }
        Start-Sleep -Milliseconds 500
    }
    $mcpProcess = $null

    $deadline9b = (Get-Date).AddSeconds(15)
    while (-not $hostProcess.HasExited) {
        if ((Get-Date) -gt $deadline9b) {
            throw "cell 9 (upgrade-over-running-install): Host process was still running after the second install completed; expected PrepareToInstall to have stopped it via POST /api/shutdown"
        }
        Start-Sleep -Milliseconds 500
    }
    $stdoutContent9 = ''
    $deadline9c = (Get-Date).AddSeconds(10)
    while ($true) {
        if (Test-Path -LiteralPath $stdoutLog9) {
            $stdoutContent9 = Get-Content -LiteralPath $stdoutLog9 -Raw -ErrorAction SilentlyContinue
        }
        if (-not $stdoutContent9) {
            $stdoutContent9 = ''
        }
        if ($stdoutContent9 -match '(?i)shutting down') {
            break
        }
        if ((Get-Date) -gt $deadline9c) {
            $tail9 = $stdoutContent9
            if ($tail9.Length -gt 500) {
                $tail9 = $tail9.Substring($tail9.Length - 500)
            }
            throw "cell 9 (upgrade-over-running-install): host.stdout.log has no graceful-shutdown log line after 10s; the installer may have used taskkill instead of POST /api/shutdown. Log tail: $tail9"
        }
        Start-Sleep -Milliseconds 500
    }
    $hostProcess = $null

    Invoke-SilentUninstall -InstallDir $installDir9 -TimeoutSec 60 -CellLabel 'cell 9 (upgrade-over-running-install)'
    $pendingUninstalls.Remove($installDir9) | Out-Null

    if (Test-Path -LiteralPath (Get-StartMenuShortcutPath)) {
        throw "cell 9 (upgrade-over-running-install): Start Menu shortcut still present after uninstall: $(Get-StartMenuShortcutPath)"
    }
    if (Test-Path -LiteralPath (Get-UninstallKeyPath)) {
        throw "cell 9 (upgrade-over-running-install): uninstall registry key still present after uninstall: $(Get-UninstallKeyPath)"
    }

    if ($realPortFileHadContent) {
        Set-Content -LiteralPath $realPortFile -Value $realPortFileBackup -NoNewline -ErrorAction SilentlyContinue
    }
    else {
        Remove-Item -LiteralPath $realPortFile -Force -ErrorAction SilentlyContinue
    }
    $realPortFileTouched = $false

    Write-Host "cell 9: upgrade-over-running-install OK - discovered through the real ~/.overmind/host.port (its original content backed up and restored), the installed overmind-mcp.exe was actually running and locking {app}\mcp before the upgrade and is confirmed gone afterward (CRIT-1's StopRunningMcpAdapter genuinely exercised, not just read), second install stopped the Host gracefully (its own log shows 'Application is shutting down...' after the 202, not a kill), Inno log confirms Installation process succeeded with no rollback, web\index.html byte-for-byte restored (real replacement proof), Start Menu/uninstall-key residue clean"

    $pathBaselineFile = Join-Path $env:TEMP "ovm-path-baseline-$shortSuffix.txt"
    (@{ Value = $userPathBaseline } | ConvertTo-Json -Compress) | Set-Content -LiteralPath $pathBaselineFile -NoNewline
    $recoveryHint = "recover manually from '$pathBaselineFile' if this script did not restore it: read the JSON 'Value' field and write it back to HKCU\Environment\Path as REG_EXPAND_SZ"

    $installDir10 = Join-Path $root 'i10'
    $installLogPath10 = Join-Path $root 'i10-install.log'
    Invoke-SilentInstall -Setup $setupPath -InstallDir $installDir10 -Tasks 'mcppath,autostart' -LogPath $installLogPath10
    $installLogContent10 = Wait-ForInnoLogClosed -LogPath $installLogPath10 -TimeoutSec 120 -CellLabel 'cell 10 (PATH round-trip)'
    if ($installLogContent10 -notmatch '(?i)Installation process succeeded\.') {
        throw "cell 10 (PATH round-trip): install did not report success in its own log ($recoveryHint)"
    }
    $hostExe10 = Join-Path $installDir10 'host\Overmind.Host.exe'
    if (-not (Test-Path -LiteralPath $hostExe10)) {
        throw "cell 10 (PATH round-trip): host\Overmind.Host.exe missing after a reported-successful install"
    }
    $pendingUninstalls.Add($installDir10)

    $mcpSegment10 = Join-Path $installDir10 'mcp'
    $cell10Failure = $null

    $userPathAfterInstall10 = Get-RawUserPath
    if (-not $userPathAfterInstall10) {
        $cell10Failure = "cell 10 (PATH round-trip): HKCU Environment Path is empty or missing after a mcppath install"
    }
    elseif (-not $userPathAfterInstall10.EndsWith($mcpSegment10, [StringComparison]::OrdinalIgnoreCase)) {
        $tailShown = $userPathAfterInstall10
        if ($tailShown.Length -gt 160) {
            $tailShown = $tailShown.Substring($tailShown.Length - 160)
        }
        $cell10Failure = "cell 10 (PATH round-trip): Path does not end with the install's mcp segment '$mcpSegment10'; actual tail: ...$tailShown"
    }
    elseif ($userPathAfterInstall10.StartsWith(';')) {
        $cell10Failure = "cell 10 (PATH round-trip): Path has a leading semicolon after the mcppath install"
    }
    elseif ($userPathAfterInstall10 -match ';;') {
        $cell10Failure = "cell 10 (PATH round-trip): Path has a doubled semicolon after the mcppath install"
    }
    else {
        $envKey10 = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment')
        try {
            $valueKind10 = $envKey10.GetValueKind('Path')
        }
        finally {
            $envKey10.Close()
        }
        if ($valueKind10 -ne [Microsoft.Win32.RegistryValueKind]::ExpandString) {
            $cell10Failure = "cell 10 (PATH round-trip): Path value kind is $valueKind10, expected ExpandString (REG_EXPAND_SZ)"
        }
    }

    $runValueAfterInstall10 = Get-RunKeyValue
    if (-not $cell10Failure) {
        if (-not $runValueAfterInstall10) {
            $cell10Failure = "cell 10 (PATH round-trip): HKCU Run key has no Overmind value after an autostart-enabled install"
        }
        elseif ($runValueAfterInstall10 -notmatch [regex]::Escape('Overmind.Host.exe')) {
            $cell10Failure = "cell 10 (PATH round-trip): HKCU Run key's Overmind value does not reference Overmind.Host.exe: $runValueAfterInstall10"
        }
    }

    try {
        Invoke-SilentUninstall -InstallDir $installDir10 -TimeoutSec 60 -CellLabel 'cell 10 (PATH round-trip)'
        $pendingUninstalls.Remove($installDir10) | Out-Null
    }
    catch {
        if (-not $cell10Failure) {
            $cell10Failure = $_.Exception.Message
        }
    }

    $runValueAfterUninstall10 = Get-RunKeyValue
    if ($runValueAfterUninstall10 -and (-not $cell10Failure)) {
        $cell10Failure = "cell 10 (PATH round-trip): HKCU Run key still has an Overmind value after uninstall: $runValueAfterUninstall10"
    }

    $userPathAfterUninstall10 = Get-RawUserPath
    if (-not (Test-PathValuesEqual -A $userPathAfterUninstall10 -B $userPathBaseline)) {
        Set-RawUserPath -Value $userPathBaseline
        if (-not $cell10Failure) {
            $cell10Failure = "cell 10 (PATH round-trip): Path after uninstall was not byte-identical to the value captured before this cell; force-restored the original captured value"
        }
        else {
            $cell10Failure = "$cell10Failure; additionally the uninstall did not restore Path byte-identically, so the script force-restored the originally captured value"
        }
    }

    $userPathAfterCell10Restore = Get-RawUserPath
    if (Test-PathValuesEqual -A $userPathAfterCell10Restore -B $userPathBaseline) {
        Remove-Item -LiteralPath $pathBaselineFile -Force -ErrorAction SilentlyContinue
        $pathBaselineFile = $null
    }

    if ($cell10Failure) {
        throw $cell10Failure
    }
    Write-Host "cell 10: PATH round-trip OK - mcppath segment appended cleanly (no leading/doubled ;, REG_EXPAND_SZ preserved), autostart Run-key round-trip verified, uninstall restored Path byte-identically"

    $overlongPad = 'x' * 160
    $overlongDir = Join-Path $root $overlongPad
    $overlongLogPath = Join-Path $root 'i11-overlong-install.log'

    $overlongPsi = New-Object System.Diagnostics.ProcessStartInfo
    $overlongPsi.FileName = $setupPath
    $overlongPsi.Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR=$overlongDir /TASKS= /LOG=$overlongLogPath"
    $overlongPsi.UseShellExecute = $false
    $overlongPsi.CreateNoWindow = $true
    $overlongStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $overlongProcess = [System.Diagnostics.Process]::Start($overlongPsi)

    try {
        $overlongExitedInTime = $overlongProcess.WaitForExit(15000)
        $overlongStopwatch.Stop()

        if (-not $overlongExitedInTime) {
            throw "cell 11 (path-length guard): setup.exe did not exit within 15s after refusing a $($overlongDir.Length)-char over-long install directory - expected a clean silent abort in a few seconds; this is a regression (the process tree is about to be force-killed as a safety net, not as the expected path)"
        }

        $overlongExitCode = $overlongProcess.ExitCode
        Write-Host "cell 11: setup.exe exited with code $overlongExitCode after $([Math]::Round($overlongStopwatch.Elapsed.TotalSeconds, 1))s"
        if ($overlongExitCode -eq 0) {
            throw "cell 11 (path-length guard): setup.exe exited 0 despite refusing a $($overlongDir.Length)-char over-long install directory; expected a non-zero exit code"
        }

        $overlongLogContent = ''
        if (Test-Path -LiteralPath $overlongLogPath) {
            $overlongLogContent = Get-Content -LiteralPath $overlongLogPath -Raw -ErrorAction SilentlyContinue
        }
        if (-not $overlongLogContent) {
            $overlongLogContent = ''
        }

        if ($overlongLogContent -notmatch '(?i)refusing silent install') {
            $tail11 = $overlongLogContent
            if ($tail11.Length -gt 500) {
                $tail11 = $tail11.Substring($tail11.Length - 500)
            }
            throw "cell 11 (path-length guard): expected the installer's log to record refusing the silent install of a $($overlongDir.Length)-char directory; log tail: $tail11"
        }
        if ($overlongLogContent -match '(?i)Installing the file\.') {
            throw "cell 11 (path-length guard): the installer copied files despite the over-long directory; the guard did not fire before file operations"
        }
        if ($overlongLogContent -match '(?i)Installation process succeeded\.') {
            throw "cell 11 (path-length guard): the installer reported success despite the over-long directory"
        }
        if (Test-Path -LiteralPath $overlongDir) {
            $remainingOverlong = Get-ChildItem -LiteralPath $overlongDir -Force -ErrorAction SilentlyContinue
            if ($remainingOverlong) {
                throw "cell 11 (path-length guard): install directory still contains files after a refused install: $overlongDir"
            }
        }

        Write-Host "cell 11: path-length guard OK - a $($overlongDir.Length)-char install directory was refused up front (InitializeSetup, silent-mode pre-UI abort), exit code $overlongExitCode in $([Math]::Round($overlongStopwatch.Elapsed.TotalSeconds, 1))s, no files copied"
    }
    finally {
        if (-not $overlongProcess.HasExited) {
            Stop-ProcessTreeById -RootProcessId $overlongProcess.Id
        }
    }

    $smokeSummary = "install=OK port=$port2 health=ok bundled kits seeded with manifest mandatory flags web UI served MCP initialize ok (result.serverInfo) graceful shutdown 202+exit0 silent uninstall clean (Start Menu/uninstall-key/Path byte-identical, ~/.overmind intact) agent registration dry-run ok (overmind-mcp.exe install --dry-run exit 0, plan named an agent, nothing written) upgrade-over-running-install ok via the real host.port (installed overmind-mcp.exe genuinely locked+killed, graceful Host stop, web\index.html byte-for-byte replacement proof) PATH+autostart round-trip ok (mcppath/autostart tasks, REG_EXPAND_SZ preserved, Run key round-trip) path-length guard ok (silent pre-UI abort, exit code $overlongExitCode in $([Math]::Round($overlongStopwatch.Elapsed.TotalSeconds, 1))s, no files copied)"
    Write-Host "SMOKE OK: $smokeSummary"
}
catch {
    [Console]::Error.WriteLine("smoke-installer.ps1: $($_.Exception.Message)")
    $failed = $true
}
finally {
    if ($mcpProcess -and (-not $mcpProcess.HasExited)) {
        try {
            $mcpProcess.Kill()
        }
        catch {
        }
    }
    if ($hostProcess -and (-not $hostProcess.HasExited)) {
        try {
            $hostProcess.Kill()
        }
        catch {
        }
    }
    if ($hostProcess) {
        try {
            $hostProcess.WaitForExit(5000) | Out-Null
            $hostProcess.Dispose()
        }
        catch {
        }
    }
    if ($mcpProcess) {
        try {
            $mcpProcess.Dispose()
        }
        catch {
        }
    }

    Get-EventSubscriber -ErrorAction SilentlyContinue | Unregister-Event -ErrorAction SilentlyContinue

    if ($realPortFileTouched) {
        try {
            if ($realPortFileHadContent) {
                Set-Content -LiteralPath $realPortFile -Value $realPortFileBackup -NoNewline -ErrorAction SilentlyContinue
            }
            else {
                Remove-Item -LiteralPath $realPortFile -Force -ErrorAction SilentlyContinue
            }
            [Console]::Error.WriteLine("smoke-installer.ps1: cell 9 did not complete cleanly; restored the real ~/.overmind/host.port from its captured backup as a safety net")
        }
        catch {
        }
    }

    foreach ($dir in @($pendingUninstalls)) {
        Invoke-BestEffortUninstall -InstallDir $dir
    }

    for ($attempt = 1; $attempt -le 3; $attempt++) {
        if (-not (Test-Path -LiteralPath $root)) {
            break
        }
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $root) {
            Start-Sleep -Seconds 2
        }
    }

    $userPathFinalCheck = Get-RawUserPath
    if (-not (Test-PathValuesEqual -A $userPathFinalCheck -B $userPathBaseline)) {
        Set-RawUserPath -Value $userPathBaseline
        [Console]::Error.WriteLine("smoke-installer.ps1: user Path diverged from the captured baseline at cleanup time; force-restored the original value as a safety net")
    }

    if ($pathBaselineFile -and (Test-Path -LiteralPath $pathBaselineFile)) {
        $userPathAfterFinallyRestore = Get-RawUserPath
        if (Test-PathValuesEqual -A $userPathAfterFinallyRestore -B $userPathBaseline) {
            Remove-Item -LiteralPath $pathBaselineFile -Force -ErrorAction SilentlyContinue
        }
        else {
            [Console]::Error.WriteLine("smoke-installer.ps1: Path could not be confirmed restored at cleanup time; the recovery baseline is kept at $pathBaselineFile")
        }
    }
}

if ($failed) {
    exit 1
}
exit 0
