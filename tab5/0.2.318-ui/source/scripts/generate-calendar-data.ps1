#Requires -PSEdition Core
# Reproducible compact lunar-year data from the current .NET calendar API.
# No downloaded source code or personal dates are used.
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$calendar=[System.Globalization.ChineseLunisolarCalendar]::new()
$lines=[System.Collections.Generic.List[string]]::new()
$lines.Add('// Generated with System.Globalization.ChineseLunisolarCalendar; see scripts/generate-calendar-data.ps1.')
$lines.Add('static const struct {int start; unsigned short months; unsigned char leap;} cal_years[] = {')
for($year=1901;$year -le 2100;$year++) {
    $start=$calendar.ToDateTime($year,1,1,0,0,0,0)
    $bits=0
    for($month=1;$month -le $calendar.GetMonthsInYear($year);$month++) {
        if($calendar.GetDaysInMonth($year,$month) -eq 30){$bits=$bits -bor (1 -shl ($month-1))}
    }
    $serial=[int](($start-[datetime]::new(1,1,1)).TotalDays)
    $lines.Add(('    {{{0},0x{1:X4},{2}}}, // {3}' -f $serial,$bits,$calendar.GetLeapMonth($year),$year))
}
$lines.Add('};')
[System.IO.File]::WriteAllLines((Join-Path $project 'firmware/main/calendar_data.h'),$lines,[Text.UTF8Encoding]::new($false))
