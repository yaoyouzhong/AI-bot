using System.Globalization;
using System.Text.Json;

namespace AIBotBridge;

internal sealed record Birthday(string Name,int Month,int Day,bool Lunar,bool Leap,int RemindDays);
internal static class BirthdayStore
{
    private static readonly object Gate=new();
    private static Birthday[]? _saved;
    private static string FilePath=>Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AI-bot","birthdays.json");
    internal static Birthday[] Load()
    {
        lock(Gate) {
            if(_saved is not null)return _saved.ToArray();
            if(!File.Exists(FilePath))return [];
            var values=JsonSerializer.Deserialize<Birthday[]>(File.ReadAllText(FilePath))??[];
            Validate(values);_saved=values;return values.ToArray();
        }
    }
    internal static void Validate(Birthday[] values)
    {
        if(values.Length>32)throw new InvalidOperationException("最多保存 32 个生日。");
        foreach(var b in values) {
            if(string.IsNullOrWhiteSpace(b.Name)||b.Name.Length>16||b.Name.Any(char.IsControl))throw new InvalidOperationException("姓名须为 1–16 个字符。");
            if(b.Month is <1 or >12||b.Day<1||b.Day>(b.Lunar?30:DateTime.DaysInMonth(2000,b.Month)))throw new InvalidOperationException($"{b.Name} 的生日日期无效。");
            if(b.RemindDays is <0 or >30)throw new InvalidOperationException("提前提醒范围为 0–30 天。");
            if(b.Leap&&!b.Lunar)throw new InvalidOperationException("闰月仅适用于农历。");
        }
    }
    internal static void Save(Birthday[] values)
    {
        Validate(values);
        lock(Gate) {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string temp=FilePath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {File.WriteAllText(temp,JsonSerializer.Serialize(values));File.Move(temp,FilePath,true);_saved=values.ToArray();}
            finally {if(File.Exists(temp))File.Delete(temp);}
        }
    }
    internal static DateTime Occurrence(Birthday b,int year)
    {
        if(!b.Lunar)return new DateTime(year,b.Month,Math.Min(b.Day,DateTime.DaysInMonth(year,b.Month)));
        var cal=new ChineseLunisolarCalendar();int leap=cal.GetLeapMonth(year),slot=b.Month;
        if(leap!=0&&b.Month>=leap)slot++;
        if(b.Leap&&leap==b.Month+1)slot=leap;
        return cal.ToDateTime(year,slot,Math.Min(b.Day,cal.GetDaysInMonth(year,slot)),0,0,0,0);
    }
    internal static object[] Snapshot(DateTime today)
    {
        Birthday[] entries;
        try {entries=Load();} catch(IOException){return [];} catch(JsonException){return [];} catch(InvalidOperationException){return [];}
        return entries.Select(b=>new {name=b.Name,month=b.Month,day=b.Day,lunar=b.Lunar,leap=b.Leap,remind=b.RemindDays}).Cast<object>().ToArray();
    }
    internal static void SelfTest()
    {
        var solar=new Birthday("示例",2,29,false,false,7);
        if(Occurrence(solar,2026)!=new DateTime(2026,2,28)||Occurrence(solar,2028)!=new DateTime(2028,2,29))throw new InvalidOperationException("Birthday leap-day failure");
        if(Occurrence(new("示例",1,1,true,false,0),2026)!=new DateTime(2026,2,17))throw new InvalidOperationException("Lunar birthday failure");
        var leap=new Birthday("示例",6,1,true,true,3);
        if(Occurrence(leap,2025)!=new DateTime(2025,7,25)||Occurrence(leap,2026)!=Occurrence(leap with {Leap=false},2026))throw new InvalidOperationException("Lunar leap-month failure");
        bool rejected=false;try{Validate([solar with {Month=13}]);}catch(InvalidOperationException){rejected=true;}
        if(!rejected)throw new InvalidOperationException("Invalid birthday accepted");
    }
}
