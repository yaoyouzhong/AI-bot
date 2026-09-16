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
        double? weekly = null, five = null;
        DateTimeOffset? weeklyReset = null, fiveReset = null;
        void Row(JsonElement row)
        {
            if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("window", out var window) || window.ValueKind != JsonValueKind.Object) return;
            var duration = window.GetProperty("duration").GetDouble();
            var unit = window.GetProperty("unit").GetString();
            double hours = duration * (unit switch { "minute" => 1d/60, "hour" => 1, "day" => 24, "week" => 168, _ => 0 });
            if (hours != 168 && hours != 5) return;
            double used = row.GetProperty("used").GetDouble(), limit = row.GetProperty("limit").GetDouble();
            if (!double.IsFinite(used) || !double.IsFinite(limit) || used < 0 || limit <= 0) throw new JsonException("Invalid quota.");
            var percent = Math.Clamp(100*used/limit,0,100);
            DateTimeOffset? reset = row.TryGetProperty("reset_at", out var r) && r.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(r.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var stamp) ? stamp : null;
            if (hours == 168) { if (weekly.HasValue && weekly != percent) throw new JsonException("Conflicting windows."); weekly=percent; weeklyReset=reset; }
            else { if (five.HasValue && five != percent) throw new JsonException("Conflicting windows."); five=percent; fiveReset=reset; }
        }
        if (data.TryGetProperty("summary",out var summary)) Row(summary);
        if (data.TryGetProperty("limits",out var limits) && limits.ValueKind == JsonValueKind.Array)
            foreach(var row in limits.EnumerateArray()) Row(row);
        if (!weekly.HasValue) throw new JsonException("Weekly subscription window not available.");
        return new("kimi",null,five,fiveReset,weekly,weeklyReset,null,null,"",DateTimeOffset.UtcNow,false);
    }
}
