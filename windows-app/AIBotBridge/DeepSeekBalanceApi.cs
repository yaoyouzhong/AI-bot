using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AIBotBridge;

internal sealed record DeepSeekBalanceResult(double? Balance, string Currency, string Error, bool RateLimited = false)
{
    internal bool Success => Balance.HasValue;
}

internal static class DeepSeekBalanceApi
{
    internal const string Endpoint = "https://api.deepseek.com/user/balance";
    // Never forward a bearer credential to a redirected host.
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(15) };

    internal static async Task<DeepSeekBalanceResult> FetchAsync(string key, string currency = "CNY", HttpClient? client = null)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await (client ?? Client).SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return new(null, currency, response.StatusCode switch {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "DeepSeek API Key 无效或无权限，请在模型额度设置中更新。",
                    HttpStatusCode.TooManyRequests => "DeepSeek 查询限流，5 分钟后重试。",
                    _ => $"DeepSeek 余额查询失败（HTTP {(int)response.StatusCode}），将自动重试。"
                }, response.StatusCode == HttpStatusCode.TooManyRequests);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return Parse(document.RootElement, currency);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or FormatException)
        {
            // Never expose request credentials, response bodies or exception fragments.
            return new(null, currency, ex is JsonException ? "DeepSeek 返回的余额数据无法识别，已保留上次结果。" : "DeepSeek 余额查询超时或网络异常，已保留上次结果。",
                false);
        }
    }

    internal static DeepSeekBalanceResult Parse(JsonElement root, string preferredCurrency = "CNY")
    {
        DeepSeekBalanceResult? first = null, preferred = null;
        var seen = new HashSet<string>();
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("balance_infos", out var infos) || infos.ValueKind != JsonValueKind.Array)
            throw new JsonException("Missing balance list.");
        foreach (var item in infos.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("currency", out var code) || code.ValueKind != JsonValueKind.String) continue;
            var currency = code.GetString();
            if (currency is not ("CNY" or "USD")) continue;
            if (!seen.Add(currency)) throw new JsonException("Duplicate currency.");
            if (!item.TryGetProperty("total_balance", out var value) || value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number) ||
                !double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) || !double.IsFinite(amount))
                throw new JsonException("Invalid balance.");
            var parsed = new DeepSeekBalanceResult(amount, currency, "");
            first ??= parsed;
            if (currency == preferredCurrency) preferred = parsed;
        }
        return preferred ?? first ?? throw new JsonException("No supported currency.");
    }
}
