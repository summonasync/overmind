@echo off
rem run.cmd - launch the Overmind host from an unpacked distribution folder.
setlocal
set "OVERMIND_WEB_ROOT=%~dp0web"
"%~dp0host\Overmind.Host.exe" %*
