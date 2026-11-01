using System.Globalization;
using System.Text.Json;

namespace AIBotBridge;

// Shared by live browser/API capture and the cached-response adapter.
internal static class MiniMaxQuota
{
    internal static DateTimeOffset? Reset(JsonElement row,bool weekly,Func<DateTimeOffset?> legacy)
    {
        var value=QuotaCycle.Field(row,weekly?"weekly_end_time":"end_time");
        if(value.ValueKind==JsonValueKind.Undefined)return legacy();
        // Official UI subtracts Date.now(). These are epoch milliseconds, never seconds
        // or remains_time countdowns. Invalid explicit values stay unknown.
        if(value.ValueKind is not (JsonValueKind.Number or JsonValueKind.String)
            || !long.TryParse(value.ToString(),NumberStyles.Integer,CultureInfo.InvariantCulture,out var epoch)
            || epoch is <=1000000000000 or >=253402300800000)return null;
        return DateTimeOffset.FromUnixTimeMilliseconds(epoch);
    }

    internal static double? ExpandedPercent(JsonElement row,string prefix)
    {
        var totalValue=QuotaCycle.Field(row,prefix+"_total_percent");
        if(totalValue.ValueKind==JsonValueKind.Undefined)return null;
        static double Read(JsonElement value) {
            if(value.ValueKind is not (JsonValueKind.Number or JsonValueKind.String)
                || !double.TryParse(value.ToString().Trim().TrimEnd('%'),NumberStyles.Float,CultureInfo.InvariantCulture,out var n)
                || !double.IsFinite(n))throw new JsonException("Invalid MiniMax expanded quota.");
            return n;
        }
        var total=Read(totalValue);var used=Read(QuotaCycle.Field(row,prefix+"_used_percent"));
        if(total<=0||used<0)throw new JsonException("Invalid MiniMax expanded quota.");
        // Mirrors the official progress rule; used=120,total=200 means 60%, not full.
        return Math.Min(1,used/total)*100;
    }
}
