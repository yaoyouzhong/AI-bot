using System.Globalization;
using System.Text.RegularExpressions;

namespace AIBotBridge;

internal static class Tab5SolarTerms
{
    private static readonly string[] Names=["小寒","大寒","立春","雨水","驚蟄","春分","清明","穀雨","立夏","小滿","芒種","夏至","小暑","大暑","立秋","處暑","白露","秋分","寒露","霜降","立冬","小雪","大雪","冬至"];
    private static readonly object Gate=new();
    private static readonly Dictionary<int,int[]> Cached=[];
    private static bool _busy;private static DateTimeOffset _next;
    internal static int[]? Get(int year){lock(Gate)return Cached.GetValueOrDefault(year)?.ToArray();}
    internal static void Schedule(int year) {
        if(year is <1902 or >2096)return;
        lock(Gate){if(_busy||DateTimeOffset.UtcNow<_next)return;_busy=true;}
        _=Task.Run(async()=> {
            bool failed=false;
            try {
                string folder=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AI-bot","calendar");
                using var client=new HttpClient {Timeout=TimeSpan.FromSeconds(15),MaxResponseContentBufferSize=131072};
                // Restore all cached years before waiting on any network request.
                for(int y=year-1;y<=year+3;y++) {
                    string path=Path.Combine(folder,$"terms-{y}.txt");
                    try {
                        if(Get(y) is null&&File.Exists(path)&&new FileInfo(path).Length<=131072) {
                            var values=Parse(File.ReadAllText(path),y);lock(Gate)Cached[y]=values;
                        }
                    }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException){ }
                }
                for(int y=year-1;y<=year+3;y++) {
                    try {
                        string table=await client.GetStringAsync($"https://www.hko.gov.hk/tc/gts/time/calendar/text/files/T{y}c.txt");
                        Save(folder,y,table);
                    }catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or ArgumentException){failed=true;}
                }
            }finally{lock(Gate){_next=DateTimeOffset.UtcNow.AddHours(failed?1:24);_busy=false;}}
        });
    }
    internal static int[] Save(string folder,int year,string table) {
        var dates=Parse(table,year);Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,$"terms-{year}.txt"),temp=path+".tmp";
        File.WriteAllText(temp,table);File.Move(temp,path,true);
        lock(Gate)Cached[year]=dates;
        return dates.ToArray();
    }
    internal static int[] Parse(string table,int year) {
        if(table.Length>131072||year is <1901 or >2099)throw new ArgumentException("节气年份或数据大小无效");
        int[] dates=new int[24];
        foreach(string line in table.Split('\n')) {
            var fields=Regex.Split(line.Trim(),@"\s+");
            if(fields.Length<4)continue;
            int term=Array.IndexOf(Names,fields[^1]);if(term<0)continue;
            if(dates[term]!=0||!DateTime.TryParseExact(fields[0],"yyyy年M月d日",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)||date.Year!=year||date.Month!=term/2+1)
                throw new ArgumentException("节气日期重复或无效");
            dates[term]=date.Month*100+date.Day;
        }
        if(dates.Any(d=>d==0)||!dates.SequenceEqual(dates.Order()))throw new ArgumentException("缺少完整的二十四节气");
        return dates;
    }
}
