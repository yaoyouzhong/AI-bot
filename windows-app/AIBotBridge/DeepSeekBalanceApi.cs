using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AIBotBridge;

internal sealed record DeepSeekBalanceResult(double? Balance, string Currency, string Error, bool RateLimited = false, string Category = "success", int? HttpStatus = null)
{
    internal bool Success => Balance.HasValue;
}

internal static class DeepSeekBalanceApi
{
    internal const string Endpoint = "https://api.deepseek.com/user/balance";
    // Never forward a bearer credential to a redirected host.
    private static readonly HttpClient Client = new(new SocketsHttpHandler {
        AllowAutoRedirect = false,
        // Refreshes are two minutes apart. Avoid reusing idle proxy/server connections across rounds.
        PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    })
        { Timeout = TimeSpan.FromSeconds(15) };

    internal static async Task<DeepSeekBalanceResult> FetchAsync(string key, string currency = "CNY", HttpClient? client = null)
    {
        var result = await FetchOnceAsync(key, currency, client);
        if (IsTransient(result)) {
            await Task.Delay(1000);
            result = await FetchOnceAsync(key, currency, client);
        }
        return result;
    }

    internal static bool IsTransient(DeepSeekBalanceResult result) => !result.Success &&
        (result.HttpStatus is 408 or 500 or 502 or 503 or 504 ||
         result.Category is "timeout" or "NameResolutionError" or "ConnectionError" or "ResponseEnded");

    private static async Task<DeepSeekBalanceResult> FetchOnceAsync(string key, string currency, HttpClient? client)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        int? status = null;
        string category = "unexpected";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await (client ?? Client).SendAsync(request);
            status = (int)response.StatusCode;
            category = response.IsSuccessStatusCode ? "schema" : status == 401 ? "authentication" : status == 403 ? "forbidden" : status == 429 ? "rate_limit" : "http";
            if (!response.IsSuccessStatusCode)
                return new(null, currency, response.StatusCode switch {
                    HttpStatusCode.Unauthorized => "DeepSeek 接口认证失败（HTTP 401），请在模型额度设置中检查 API Key。",
                    HttpStatusCode.Forbidden => "DeepSeek 接口拒绝访问（HTTP 403），请检查账户权限或网络访问限制。",
                    HttpStatusCode.TooManyRequests => "DeepSeek 查询限流，5 分钟后重试。",
                    _ => $"DeepSeek 余额查询失败（HTTP {(int)response.StatusCode}），将自动重试。"
                }, response.StatusCode == HttpStatusCode.TooManyRequests, category, status);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var parsed = Parse(document.RootElement, currency);
            category = "success";
            return parsed with { HttpStatus = status };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or FormatException)
        {
            category = ex is TaskCanceledException ? "timeout" : ex is HttpRequestException requestError ? requestError.HttpRequestError.ToString() : "schema";
            // Never expose request credentials, response bodies or exception fragments.
            return new(null, currency, category switch {
                "schema" => "DeepSeek 返回的余额数据无法识别，已保留上次结果。",
                "timeout" => "DeepSeek 余额查询超时，正在自动重试；已保留上次结果。",
                "NameResolutionError" => "DeepSeek 域名解析失败，正在自动重试；已保留上次结果。",
                "ConnectionError" => "DeepSeek 网络连接失败，正在自动重试；已保留上次结果。",
                "SecureConnectionError" => "DeepSeek 安全连接失败，请检查网络或代理；已保留上次结果。",
                _ => "DeepSeek 网络响应异常，正在自动重试；已保留上次结果。"
            },
                false, category, status);
        }
        finally { QuotaRequestDiagnostics.Record(category, status, started.ElapsedMilliseconds); }
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
