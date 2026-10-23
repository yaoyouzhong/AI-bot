@echo off
cd /d "%~dp0"
powershell.exe -NoLogo -NoProfile -Command "& ([scriptblock]::Create([IO.File]::ReadAllText((Join-Path (Get-Location) 'Merge-HD-Gallery.ps1')))) -Download"
if errorlevel 1 echo Download or merge failed. Run again to resume. All files are retained.
pause
