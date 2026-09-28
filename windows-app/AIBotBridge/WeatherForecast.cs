using System.Globalization;
using System.Text.Json;

namespace AIBotBridge;

internal sealed record WeatherHour(string Time,string Condition,double? Temperature,double? RainProbability);
internal sealed record WeatherDay(string Date,string Condition,double? Low,double? High,double? RainProbability,double? PrecipitationMm);
internal static class WeatherForecast
{
    internal static double? Number(JsonElement value) {
        double number;
        if(value.ValueKind==JsonValueKind.Number&&value.TryGetDouble(out number)||
            value.ValueKind==JsonValueKind.String&&double.TryParse(value.GetString(),NumberStyles.Float,CultureInfo.InvariantCulture,out number))
            return double.IsFinite(number)?number:null;
        return null;
    }
    private static double? Number(JsonElement value,string key)=>value.TryGetProperty(key,out var item)?Number(item):null;
    private static string Text(JsonElement value,string key)=>value.TryGetProperty(key,out var item)&&item.ValueKind==JsonValueKind.String?item.GetString()??"":"";
    private static double? Percent(double? value)=>value is >=0 and <=100?value:null;
    internal static WeatherHour[] QHours(JsonElement root)=>root.TryGetProperty("hourly",out var hours)&&hours.ValueKind==JsonValueKind.Array
        ?hours.EnumerateArray().Select(h=>new WeatherHour(Text(h,"fxTime"),Text(h,"text"),Number(h,"temp"),Percent(Number(h,"pop"))))
            .Where(h=>DateTimeOffset.TryParse(h.Time,CultureInfo.InvariantCulture,DateTimeStyles.None,out _)).OrderBy(h=>h.Time,StringComparer.Ordinal).Take(24).ToArray():[];
    internal static WeatherDay[] QDays(JsonElement root)=>root.TryGetProperty("daily",out var days)&&days.ValueKind==JsonValueKind.Array
        ?days.EnumerateArray().Select(d=>new WeatherDay(Text(d,"fxDate"),Text(d,"textDay"),Number(d,"tempMin"),Number(d,"tempMax"),null,Number(d,"precip")))
            .Where(d=>DateOnly.TryParseExact(d.Date,"yyyy-MM-dd",out _)).OrderBy(d=>d.Date,StringComparer.Ordinal).Take(7).ToArray():[];
    private static double? At(JsonElement array,string key,int i)=>array.TryGetProperty(key,out var values)&&values.ValueKind==JsonValueKind.Array&&i<values.GetArrayLength()?Number(values[i]):null;
    internal static WeatherHour[] MeteoHours(JsonElement root,DateTime localNow,Func<int,string> condition) {
        if(!root.TryGetProperty("hourly",out var hours)||!hours.TryGetProperty("time",out var times))return [];
        var result=new List<WeatherHour>();int i=0;
        foreach(var time in times.EnumerateArray()) {
            string stamp=time.GetString()??"";
            if(DateTime.TryParse(stamp,CultureInfo.InvariantCulture,DateTimeStyles.None,out var at)&&at>=localNow.AddHours(-1)&&result.Count<24) {
                var code=At(hours,"weather_code",i);
                result.Add(new(stamp,code is double c?condition((int)c):"--",At(hours,"temperature_2m",i),Percent(At(hours,"precipitation_probability",i))));
            }
            i++;
        }
        return result.ToArray();
    }
    internal static WeatherDay[] MeteoDays(JsonElement root,Func<int,string> condition) {
        if(!root.TryGetProperty("daily",out var days)||!days.TryGetProperty("time",out var times))return [];
        var result=new List<WeatherDay>();int i=0;
        foreach(var time in times.EnumerateArray()) {
            string date=time.GetString()??"";var code=At(days,"weather_code",i);
            if(DateOnly.TryParseExact(date,"yyyy-MM-dd",out _)&&result.Count<7)
                result.Add(new(date,code is double c?condition((int)c):"--",At(days,"temperature_2m_min",i),At(days,"temperature_2m_max",i),Percent(At(days,"precipitation_probability_max",i)),At(days,"precipitation_sum",i)));
            i++;
        }
        return result.ToArray();
    }
}
