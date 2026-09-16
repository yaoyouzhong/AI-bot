using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class AdditionalQuotaApi
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
    internal sealed record BaiduCredentials(string AccessKey, string SecretKey, string PackageId);
    internal static async Task<DomesticProviderQuotaSnapshot> FetchAsync(string provider, string credential, HttpClient? client = null)
    {
        using var request = CreateRequest(provider, credential, DateTimeOffset.UtcNow);
        using var response = await (client ?? Client).SendAsync(request);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Quota request failed.", null, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return Parse(provider, json.RootElement, provider == "baidu" ? JsonSerializer.Deserialize<BaiduCredentials>(credential)!.PackageId : null);
    }
    internal static HttpRequestMessage CreateRequest(string provider, string credential, DateTimeOffset now)
    {
        if (provider == "stepfun") {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.stepfun.com/v1/accounts");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.Trim());
            return request;
        }
        if (provider != "baidu") throw new ArgumentException("Unsupported provider.");
        var c = JsonSerializer.Deserialize<BaiduCredentials>(credential) ?? throw new FormatException();
        if (string.IsNullOrWhiteSpace(c.AccessKey) || string.IsNullOrWhiteSpace(c.SecretKey) || string.IsNullOrWhiteSpace(c.PackageId)) throw new FormatException();
        var date = now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var prefix = $"bce-auth-v1/{c.AccessKey}/{date}/1800";
        var canonical = "POST\n/v2/charge\nAction=DescribePackageResource\nhost:qianfan.baidubce.com\nx-bce-date:" + Uri.EscapeDataString(date);
        var signingKey = Hmac(c.SecretKey, prefix);
        var r = new HttpRequestMessage(HttpMethod.Post, "https://qianfan.baidubce.com/v2/charge?Action=DescribePackageResource");
        r.Content = new StringContent(JsonSerializer.Serialize(new { packageId = c.PackageId }), Encoding.UTF8, "application/json");
        r.Headers.Host = "qianfan.baidubce.com";
        r.Headers.Add("x-bce-date", date);
        r.Headers.TryAddWithoutValidation("Authorization", prefix + "/host;x-bce-date/" + Hmac(signingKey, canonical));
        return r;
    }
    internal static string Hmac(string key, string value) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static double Number(JsonElement e) => (e.ValueKind is JsonValueKind.Number or JsonValueKind.String) && double.TryParse(e.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : throw new JsonException("Invalid number.");
    internal static DomesticProviderQuotaSnapshot Parse(string provider, JsonElement root, string? packageId = null)
    {
        if (provider == "stepfun") {
            if (root.GetProperty("object").GetString() != "account" || root.GetProperty("type").GetString() is not ("prepaid" or "postpaid")) throw new JsonException("Not an account response.");
            return new(provider, "API balance (not Step Plan)", null, null, null, null, Number(root.GetProperty("balance")), null, "CNY", DateTimeOffset.UtcNow, false);
        }
        if (provider != "baidu" || root.TryGetProperty("code", out _)) throw new JsonException("Not a quota response.");
        var i = root.GetProperty("result").GetProperty("instance");
        if (i.GetProperty("packageId").GetString() != packageId) throw new JsonException("Package mismatch.");
        var total = Number(i.GetProperty("specification"));
        var used = Number(i.GetProperty("used"));
        var status = i.GetProperty("status").GetString();
        var model = i.GetProperty("serviceName").GetString();
        if (total <= 0 || used < 0 || used > total || string.IsNullOrWhiteSpace(model) || model.Length > 100 || status is not ("Pending" or "Active" or "Exhausted" or "Expired")) throw new JsonException("Invalid package.");
        var expiry = DateTimeOffset.Parse(i.GetProperty("expiredTime").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        var pct = status is "Expired" or "Exhausted" || expiry <= DateTimeOffset.UtcNow ? 100 : used / total * 100;
        return new(provider, model, null, null, null, null, null, null, null, DateTimeOffset.UtcNow, false) { PlanPercent = pct, PlanResetsAt = expiry };
    }
}
