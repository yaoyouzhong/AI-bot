#Requires -PSEdition Core
param([string]$Csv)
$ErrorActionPreference='Stop'
Add-Type -TypeDefinition @'
using System;
using System.Globalization;
using System.IO;
public static class CalendarReferenceCheck {
    public static int Run(string path) {
        var cal=new ChineseLunisolarCalendar();int count=0;
        foreach(var row in File.ReadLines(path)) {
            var s=Array.ConvertAll(row.Split(','),int.Parse);
            var date=new DateTime(s[0],s[1],s[2]);
            int year=cal.GetYear(date),month=cal.GetMonth(date),leap=cal.GetLeapMonth(year);
            bool isLeap=leap!=0&&month==leap;
            if(leap!=0&&month>=leap)month--;
            if(s[3]!=year||s[4]!=month||s[5]!=cal.GetDayOfMonth(date)||s[6]!=(isLeap?1:0))
                throw new Exception("Lunar reference mismatch: "+date.ToString("yyyy-MM-dd"));
            count++;
        }
        return count;
    }
}
'@
$count=[CalendarReferenceCheck]::Run((Resolve-Path -LiteralPath $Csv))
Write-Output "CALENDAR_DOTNET_REFERENCE_OK $count dates compared to ChineseLunisolarCalendar"
