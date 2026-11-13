@echo off
cd /d "%~dp0"
powershell.exe -NoLogo -NoProfile -Command "& ([scriptblock]::Create([IO.File]::ReadAllText((Join-Path (Get-Location) 'Merge-HD-Gallery.ps1')))) -Download -ReleaseBaseUrl 'https://github.com/yaoyouzhong/AI-bot/releases/download/gallery-2026.11.12.1/'"
pause
