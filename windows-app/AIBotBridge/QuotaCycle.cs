using System.Globalization;
using System.Text.Json;

namespace AIBotBridge;

// Cycle metadata belongs to the same quota row as its percentage. A package's
// expiry, a remaining countdown or its first observation is not its duration.
internal static class QuotaCycle
{
    internal static JsonElement Field(JsonElement row,string name)
    {
        if(row.ValueKind==JsonValueKind.Object)
            foreach(var property in row.EnumerateObject())
                if(property.Name.Equals(name,StringComparison.OrdinalIgnoreCase))return property.Value;
        return default;
    }
    internal static long? ValidSeconds(double? seconds) => seconds is >0 and <=int.MaxValue &&
        double.IsFinite(seconds.Value) && seconds.Value==Math.Truncate(seconds.Value) ? (long)seconds.Value : null;
    internal static long? WindowSeconds(JsonElement window)
    {
        var duration=Field(window,"duration");var unit=Field(window,"unit");
        if(duration.ValueKind!=JsonValueKind.Number||!duration.TryGetDouble(out var count)||unit.ValueKind!=JsonValueKind.String)return null;
        var scale=unit.GetString()?.ToLowerInvariant() switch {
            "second" or "seconds"=>1,"minute" or "minutes"=>60,"hour" or "hours"=>3600,
            "day" or "days"=>86400,"week" or "weeks"=>604800,_=>0};
        return ValidSeconds(count*scale);
    }
    internal static DateTimeOffset? Timestamp(JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(),CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,out var date))return date;
        if(value.ValueKind==JsonValueKind.Number&&value.TryGetInt64(out var epoch)) {
            try {
                if(epoch is >1000000000000 and <253402300800000)return DateTimeOffset.FromUnixTimeMilliseconds(epoch);
                if(epoch is >1000000000 and <253402300800)return DateTimeOffset.FromUnixTimeSeconds(epoch);
            } catch(ArgumentOutOfRangeException) { }
        }
        return null;
    }
    internal static (DateTimeOffset? Reset,long? Seconds) Bounds(JsonElement row)
    {
        foreach(var (startKey,endKey) in new[]{("cycleStartAt","cycleEndAt"),("cycleStartTime","cycleEndTime"),("periodStartTime","periodEndTime"),
            ("startAt","endAt"),("startTime","endTime"),("effectiveTime","expiredTime")}) {
            var start=Timestamp(Field(row,startKey));var end=Timestamp(Field(row,endKey));
            if(start.HasValue&&end.HasValue && ValidSeconds((end.Value-start.Value).TotalSeconds) is long seconds)
                return (end,seconds);
        }
        return (null,null);
    }
    internal static long? MiniMaxWindow(JsonElement row,bool weekly)
    {
        var window=Field(Field(row,weekly?"current_weekly":"current_interval"),"window");
        if(window.ValueKind==JsonValueKind.Undefined)window=Field(Field(row,weekly?"weekly":"interval"),"window");
        return window.ValueKind==JsonValueKind.Undefined?(weekly?604800:18000):WindowSeconds(window);
    }
    internal static long? PersonalQwenSeconds(string? plan) =>
        plan?.Contains("Token Plan",StringComparison.OrdinalIgnoreCase)==true && plan.Contains("个人版") ? 30*86400 : null;
}
