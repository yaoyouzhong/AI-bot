using System.Globalization;
using System.Text.Json;
namespace AIBotBridge;

// This is a console-session adapter, not a documented API-key endpoint.
internal static class XiaomiQuota
{
    internal static bool IsEndpoint(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u)
        && u.Scheme == "https" && u.IsDefaultPort && u.Host.Equals("platform.xiaomimimo.com", StringComparison.OrdinalIgnoreCase)
        && u.AbsolutePath == "/api/v1/tokenPlan/usage";

    internal static double Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Not a usage object.");
        if (root.TryGetProperty("code", out var code) && code.ToString() is not ("0" or "200")) throw new JsonException("Unsuccessful response.");
        if (root.TryGetProperty("success", out var success) && success.ValueKind != JsonValueKind.True) throw new JsonException("Unsuccessful response.");
        var data = root.TryGetProperty("data", out var d) ? d : root;
        if (data.ValueKind != JsonValueKind.Object) throw new JsonException("No active usage object.");
        // Deliberately do not search arbitrary nested objects, arrays, money or generic `ratio` fields.
        double? Read(string name) {
            if (!data.TryGetProperty(name, out var v)) return null;
            if (v.ValueKind is not (JsonValueKind.Number or JsonValueKind.String) || !double.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n)) throw new JsonException("Invalid usage number.");
            return n;
        }
        var candidates = new List<double>();
        foreach(var field in new[]{"usagePercent","usedPercent"}) if(Read(field) is double pct) candidates.Add(pct);
        foreach(var field in new[]{"usageRate","usedRate"}) if(Read(field) is double rate) candidates.Add(rate*100);
        var total = Read("totalCredits"); var used = Read("usedCredits"); var remaining = Read("remainingCredits");
        if (total.HasValue || used.HasValue || remaining.HasValue) {
            if (total is not > 0 || used is < 0 || remaining is < 0 || used > total || remaining > total) throw new JsonException("Invalid credit counters.");
            if(used.HasValue) candidates.Add(used.Value/total.Value*100);
            if(remaining.HasValue) candidates.Add((total.Value-remaining.Value)/total.Value*100);
        }
        if(candidates.Count==0 || candidates.Any(n=>n<0 || n>100 || !double.IsFinite(n)) || candidates.Any(n=>Math.Abs(n-candidates[0])>0.1)) throw new JsonException("Missing or ambiguous plan usage.");
        return candidates[0];
    }
    internal static DomesticProviderQuotaSnapshot? Snapshot(double? percent, DateTime? fetchedAt)
    {
        if(percent is not double p || !double.IsFinite(p) || p<0 || p>100 || fetchedAt is null) return null;
        return new("xiaomi","MiMo Token Plan",null,null,null,null,null,null,null,
            new DateTimeOffset(DateTime.SpecifyKind(fetchedAt.Value,DateTimeKind.Utc)),false) { PlanPercent=p };
    }
}
