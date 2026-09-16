<#
install.ps1 - register (or remove) the Overmind user autostart shortcut on Windows.

Usage: powershell -ExecutionPolicy Bypass -File install.ps1 [-Unregister]

Run from inside the unpacked Overmind folder. Creates an Overmind.lnk in the user's Startup
folder pointing at run.cmd. Writes nothing outside the Startup folder.
#>

param(
    [switch]$Unregister
)

$ErrorActionPreference = 'Stop'

$appDir = $PSScriptRoot
$runCmd = Join-Path $appDir 'run.cmd'
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Startup')) 'Overmind.lnk'

if (-not (Test-Path $runCmd)) {
    Write-Error "run.cmd not found next to install.ps1; run this from the unpacked folder"
}

if ($Unregister) {
    if (Test-Path $shortcutPath) {
        Remove-Item $shortcutPath
        Write-Host "Removed $shortcutPath"
    }
    else {
        Write-Host 'No Overmind autostart shortcut found.'
    }
    exit 0
}

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $runCmd
$shortcut.WorkingDirectory = $appDir
$shortcut.Description = 'Overmind host'
$shortcut.Save()

Write-Host "Registered $shortcutPath"
Write-Host 'Overmind starts at login; UI: http://127.0.0.1:4761'
Write-Host 'The live port is written to %USERPROFILE%\.overmind\host.port once the host is up.'
