@echo off
cd /d "%~dp0"
powershell.exe -NoLogo -NoProfile -Command "& ([scriptblock]::Create([IO.File]::ReadAllText((Join-Path (Get-Location) 'Merge-HD-Gallery.ps1'))))"
if errorlevel 1 echo Merge failed. All files have been preserved.
pause
