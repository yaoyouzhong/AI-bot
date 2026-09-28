using System.Globalization;
using System.Text.Json;

namespace AIBotBridge;

internal sealed record Tab5HolidayRange(int Start,int End,string Name);
internal sealed record Tab5HolidayDay(DateTime Date,string Name,bool Off);
internal sealed record Tab5HolidayYear(int Year,Tab5HolidayRange[] Holidays,int[] Workdays,string[] Sources,Tab5HolidayDay[]? PreviousDecember=null);

// Public holiday-cn data cites the State Council notice. It is a maintained
// dataset, not an official API. Never infer statutory days from lunar dates.
internal static class Tab5HolidayStore
{
    private static readonly object Gate=new();
    private static readonly Dictionary<int,Tab5HolidayYear> Years=[];
    private static DateTimeOffset _nextCheck;
    private static bool _refreshing;
    internal static string Status {get;private set;}="等待检查";
    private static string Folder=>Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AI-bot","calendar");
    internal static Tab5HolidayYear? Get(int year) {lock(Gate)return Years.GetValueOrDefault(year);}
    internal static Tab5HolidayYear? ApplyNextYear(Tab5HolidayYear? current) =>current is null?null:Merge(current,Get(current.Year+1));
    internal static Tab5HolidayYear Merge(Tab5HolidayYear current,Tab5HolidayYear? next) {
        if(next?.Year!=current.Year+1||next.PreviousDecember is not {Length:>0} changes)return current;
        var days=new SortedDictionary<DateTime,(string Name,bool Off)>();
        foreach(var h in current.Holidays)
            for(var d=new DateTime(current.Year,h.Start/100,h.Start%100);d<=new DateTime(current.Year,h.End/100,h.End%100);d=d.AddDays(1))days[d]=(h.Name,true);
        foreach(int md in current.Workdays)days[new(current.Year,md/100,md%100)]=("调休",false);
        foreach(var d in changes)days[d.Date]=(d.Name,d.Off);
        return Build(current.Year,days,current.Sources.Concat(next.Sources).Distinct().ToArray(),current.PreviousDecember);
    }
    internal static void Schedule(int year)
    {
        if(year is <2000 or >2099)return;
        lock(Gate) {
            if(_refreshing||DateTimeOffset.UtcNow<_nextCheck)return;
            _refreshing=true;
        }
        _=Task.Run(async ()=> {
            bool failed=false;
            try {
                using var client=new HttpClient {Timeout=TimeSpan.FromSeconds(15),MaxResponseContentBufferSize=65536};
                for(int y=year-1;y<=year+1;y++) {
                    string path=Path.Combine(Folder,y+".json");
                    try {
                        if(Get(y) is null&&File.Exists(path)&&new FileInfo(path).Length<=65536) {
                            var cached=Parse(File.ReadAllText(path),y);lock(Gate)Years[y]=cached;
                        }
                    }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException) { }
                    try {
                        using var response=await client.GetAsync($"https://raw.githubusercontent.com/NateScarlet/holiday-cn/master/{y}.json");
                        if(response.StatusCode==System.Net.HttpStatusCode.NotFound)continue;
                        response.EnsureSuccessStatusCode();
                        string json=await response.Content.ReadAsStringAsync();
                        if(Unpublished(json,y))continue;
                        var parsed=SaveDownloaded(Folder,y,json);
                        lock(Gate)Years[y]=parsed;
                    }catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException) {failed=true;}
                }
            } finally {
                lock(Gate) {
                    Status=failed?"更新失败，保留已有安排":"已检查，未发布年份等待公告";
                    _nextCheck=DateTimeOffset.UtcNow.AddHours(failed?1:24);_refreshing=false;
                }
            }
        });
    }
    internal static Tab5HolidayYear SaveDownloaded(string folder,int year,string json) {
        var parsed=Parse(json,year); // Validate fully before touching last-good cache.
        Directory.CreateDirectory(folder);string path=Path.Combine(folder,year+".json"),temp=path+".tmp";
        File.WriteAllText(temp,json);File.Move(temp,path,true);return parsed;
    }
    internal static bool Unpublished(string json,int year) {
        using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
        return root.ValueKind==JsonValueKind.Object&&root.TryGetProperty("year",out var y)&&y.TryGetInt32(out var actual)&&actual==year&&
            root.TryGetProperty("papers",out var p)&&p.ValueKind==JsonValueKind.Array&&p.GetArrayLength()==0&&
            root.TryGetProperty("days",out var d)&&d.ValueKind==JsonValueKind.Array&&d.GetArrayLength()==0;
    }
    internal static Tab5HolidayYear Parse(string json,int year)
    {
        try{return ParseDocument(json,year);}
        catch(Exception ex) when(ex is KeyNotFoundException or FormatException or InvalidOperationException or JsonException) {
            throw new ArgumentException("年度节假日数据格式无效",ex);
        }
    }
    private static Tab5HolidayYear ParseDocument(string json,int year)
    {
        if(json.Length>65536||year is <2000 or >2100)throw new ArgumentException("节假日数据大小或年份无效");
        using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
        if(root.GetProperty("year").GetInt32()!=year)throw new ArgumentException("节假日年份不匹配");
        var sources=root.GetProperty("papers").EnumerateArray().Select(p=>p.GetString()??"").ToArray();
        if(sources.Length is <1 or >8||sources.Any(s=>!Uri.TryCreate(s,UriKind.Absolute,out var u)||u.Scheme!="https"||u.Host!="www.gov.cn"))
            throw new ArgumentException("缺少国务院公告来源");
        var days=new SortedDictionary<DateTime,(string Name,bool Off)>();
        foreach(var day in root.GetProperty("days").EnumerateArray()) {
            string name=day.GetProperty("name").GetString()??"";
            if(name.Length is <1 or >16||name.Any(char.IsControl)||
                !DateTime.TryParseExact(day.GetProperty("date").GetString(),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)||
                (date.Year!=year&&!(date.Year==year-1&&date.Month==12))||
                !days.TryAdd(date,(name,day.GetProperty("isOffDay").GetBoolean())))throw new ArgumentException("节假日日期重复或无效");
        }
        // Reject empty/partial/unpublished years, including plausible-looking forecasts.
        string[] names=["元旦","春节","清明","劳动","端午","中秋","国庆"];
        if(days.Count is <20 or >80||names.Any(n=>!days.Any(d=>d.Value.Off&&d.Value.Name.Contains(n))))throw new ArgumentException("年度安排不完整");
        var previous=days.Where(d=>d.Key.Year==year-1).Select(d=>new Tab5HolidayDay(d.Key,d.Value.Name,d.Value.Off)).ToArray();
        return Build(year,new(days.Where(d=>d.Key.Year==year).ToDictionary()),sources,previous);
    }
    private static Tab5HolidayYear Build(int year,SortedDictionary<DateTime,(string Name,bool Off)> days,string[] sources,Tab5HolidayDay[]? previousDays) {
        var ranges=new List<Tab5HolidayRange>();DateTime previous=DateTime.MinValue;
        foreach(var (date,value) in days.Where(d=>d.Value.Off)) {
            int md=date.Month*100+date.Day;
            if(ranges.Count>0&&date==previous.AddDays(1)&&ranges[^1].Name==value.Name)ranges[^1]=ranges[^1] with {End=md};
            else ranges.Add(new(md,md,value.Name));
            previous=date;
        }
        if(ranges.Count>16)throw new ArgumentException("节假日区间过多");
        return new(year,ranges.ToArray(),days.Where(d=>!d.Value.Off).Select(d=>d.Key.Month*100+d.Key.Day).ToArray(),sources,previousDays);
    }
}
