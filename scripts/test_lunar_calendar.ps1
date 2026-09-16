$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot
$testPath = Join-Path $repoPath 'artifacts/lunar-native-test'
New-Item -ItemType Directory -Force -Path $testPath | Out-Null
$calendar = [Globalization.ChineseLunisolarCalendar]::new()
$writer = [IO.StreamWriter]::new((Join-Path $testPath 'expected.csv'),$false,[Text.UTF8Encoding]::new($false))
try {
    for ($date=$calendar.MinSupportedDateTime.Date; $date -le $calendar.MaxSupportedDateTime.Date; $date=$date.AddDays(1)) {
        $year=$calendar.GetYear($date); $month=$calendar.GetMonth($date); $leap=$calendar.GetLeapMonth($year)
        $isLeap=[int]($leap -ne 0 -and $month -eq $leap)
        if ($leap -ne 0 -and $month -ge $leap) { $month-- }
        $writer.WriteLine(('{0},{1},{2},{3},{4},{5}' -f $date.Year,$date.Month,$date.Day,$month,$calendar.GetDayOfMonth($date),$isLeap))
    }
} finally { $writer.Dispose() }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$visualStudio = & $vswhere -latest -products '*' -property installationPath
if (!$visualStudio) { throw 'Visual C++ Build Tools required for host firmware calendar validation' }
$vcvars = Join-Path $visualStudio 'VC/Auxiliary/Build/vcvars64.bat'
$source = Join-Path $PSScriptRoot 'test_lunar_calendar.cpp'
Push-Location $testPath
try {
    & cmd /d /c "`"$vcvars`" >nul && cl /nologo /EHsc /std:c++14 /utf-8 `"$source`" /Fe:lunar-test.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Native lunar test build failed' }
    & (Join-Path $testPath 'lunar-test.exe') (Join-Path $testPath 'expected.csv')
    if ($LASTEXITCODE -ne 0) { throw 'Native lunar validation failed' }
} finally { Pop-Location }
