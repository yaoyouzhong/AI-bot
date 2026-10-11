using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AIBotBridge;

// Documented local Kimi Code server API. Not Moonshot's pay-as-you-go wallet.
internal static class KimiUsageApi
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
    internal static async Task<DomesticProviderQuotaSnapshot> FetchAsync(string token, int port, HttpClient? client = null)
    {
        if (port is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/api/v1/oauth/usage");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        using var response = await (client ?? Client).SendAsync(request);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Kimi local API failed.", null, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return Parse(doc.RootElement);
    }
    internal static DomesticProviderQuotaSnapshot Parse(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("kind", out var kind) || kind.GetString() != "ok")
            throw new JsonException("Kimi upstream error.");
        double? weekly = null, five = null,plan=null;
        DateTimeOffset? weeklyReset = null, fiveReset = null;
        DateTimeOffset? planReset=null;
        long? primarySeconds=null,weeklySeconds=null,planSeconds=null;
        if(data.TryGetProperty("quota",out var quota)&&quota.TryGetProperty("usages",out var usages)&&usages.ValueKind==JsonValueKind.Object) {
            void Named(string name,long? seconds,ref double? percent,ref DateTimeOffset? reset,ref long? cycle) {
                if(!usages.TryGetProperty(name,out var row))return;
                var ratio=QuotaCycle.Field(row,"usedRatio");
                if(ratio.ValueKind!=JsonValueKind.Number||!ratio.TryGetDouble(out var value)||!double.IsFinite(value)||value is <0 or >1)
                    throw new JsonException("Invalid Kimi usage ratio.");
                percent=value*100;reset=QuotaCycle.Timestamp(QuotaCycle.Field(row,"resetAt"));
                var bounds=QuotaCycle.Bounds(row);
                cycle=seconds??(reset is null||reset==bounds.Reset?bounds.Seconds:null);
                reset??=bounds.Reset;
            }
            Named("limit5h",18000,ref five,ref fiveReset,ref primarySeconds);
            Named("limit7d",604800,ref weekly,ref weeklyReset,ref weeklySeconds);
            Named("monthTotal",null,ref plan,ref planReset,ref planSeconds);
            if(plan is null)Named("monthCode",null,ref plan,ref planReset,ref planSeconds);
        }
        void Row(JsonElement row)
        {
            if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("window", out var window) || window.ValueKind != JsonValueKind.Object) return;
            var seconds=QuotaCycle.WindowSeconds(window);
            if(seconds!=604800 && seconds is not (>0 and <=86400))return;
            double used = row.GetProperty("used").GetDouble(), limit = row.GetProperty("limit").GetDouble();
            if (!double.IsFinite(used) || !double.IsFinite(limit) || used < 0 || limit <= 0) throw new JsonException("Invalid quota.");
            var percent = Math.Clamp(100*used/limit,0,100);
            DateTimeOffset? reset = row.TryGetProperty("reset_at", out var r) && r.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(r.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var stamp) ? stamp : null;
            if (seconds == 604800) { if (weekly.HasValue && (weekly != percent||weeklyReset!=reset)) throw new JsonException("Conflicting windows."); weekly=percent; weeklyReset=reset;weeklySeconds=seconds; }
            else { if (five.HasValue && (five != percent||primarySeconds!=seconds||fiveReset!=reset)) throw new JsonException("Conflicting windows."); five=percent; fiveReset=reset;primarySeconds=seconds; }
        }
        if (data.TryGetProperty("summary",out var summary)) Row(summary);
        if (data.TryGetProperty("limits",out var limits) && limits.ValueKind == JsonValueKind.Array)
            foreach(var row in limits.EnumerateArray()) Row(row);
        if (!weekly.HasValue&&!five.HasValue&&!plan.HasValue) throw new JsonException("Subscription window not available.");
        return new("kimi",null,five,fiveReset,weekly,weeklyReset,null,null,"",DateTimeOffset.UtcNow,false)
            {PrimaryWindowSeconds=primarySeconds,WeeklyWindowSeconds=weeklySeconds,PlanPercent=plan,PlanResetsAt=planReset,PlanWindowSeconds=planSeconds};
    }
}
