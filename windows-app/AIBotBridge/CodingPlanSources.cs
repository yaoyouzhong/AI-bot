using System.Globalization;
using System.Text.Json;

namespace AIBotBridge;

// Read-only display contracts, independently implemented. See the dated handoff
// for official sources and the distinction between fixtures and subscribed accounts.
internal static class CodingPlanSources
{
    internal sealed record Product(string Id, string Label, string Url)
    {
        public override string ToString() => Label;
    }

    internal static Product[] Products(string provider) => provider switch
    {
        "zhipu" => [new("coding", "Coding Plan", "https://www.bigmodel.cn/coding-plan/personal/usage"), new("wallet", "API 账户余额", ZhipuBalance.AuthorizationUrl)],
        "qwen" => [new("coding", "Coding Plan", "https://bailian.console.aliyun.com/cn-beijing/subscription/coding-plan"), new("token", "Token Plan", "https://bailian.console.aliyun.com/cn-beijing/subscription/token-plan/personal")],
        "minimax" => [new("token", "Token Plan", "https://platform.minimax.cn/console/usage")],
        "xiaomi" => [new("token", "Token Plan", "https://platform.xiaomimimo.com/#/console/plan-manage")],
        "kimi" => [new("coding", "Coding Plan", "https://www.kimi.com/code/console")],
        "volcengine" => [new("coding", "Coding Plan", "https://console.volcengine.com/ark/region:ark+cn-beijing/codingPlan")],
        "tencent" => [new("coding", "Coding Plan", "https://console.cloud.tencent.com/tokenhub/codingplan"), new("token", "Token Plan", "https://console.cloud.tencent.com/tokenhub/tokenplan")],
        "baidu" => [new("token", "Token Plan", "https://console.bce.baidu.com/qianfan/resource/token-plan"), new("package", "指定模型资源包", "https://console.bce.baidu.com/qianfan/overview")],
        "stepfun" => [new("plan", "Coding Plan", "https://platform.stepfun.com/step-plan"), new("wallet", "API 账户余额", "https://platform.stepfun.com/account-overview")],
        "huawei" => [new("token", "Token Plan", "https://console.huaweicloud.com/maas/")],
        _ => []
    };

    internal static bool IsPlan(string product) => product is not ("wallet" or "package");
    internal static string? DisplayName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        if (name.Contains("Coding Plan", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Kimi Code", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Step Plan", StringComparison.OrdinalIgnoreCase)) return "Coding Plan";
        if (name.Contains("Token Plan", StringComparison.OrdinalIgnoreCase)) return "Token Plan";
        if (name.Equals("M Plan", StringComparison.OrdinalIgnoreCase)) return "Token Plan";
        return name;
    }
    internal static string Key(string provider, string product) => provider + ":" + product;
    internal static bool IsGlmEndpoint(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u)
        && u.Scheme == "https" && u.IsDefaultPort
        && u.Host is "open.bigmodel.cn" or "bigmodel.cn" or "www.bigmodel.cn"
        && u.AbsolutePath == "/api/monitor/usage/quota/limit";

    internal static DomesticProviderQuotaSnapshot ParseQwen(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Missing Coding Plan response.");
        if (root.TryGetProperty("success", out var success) && success.ValueKind != JsonValueKind.True)
            throw new JsonException("Coding Plan request failed.");
        var data = root.TryGetProperty("data", out var nested) ? nested : root;
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("Data", out nested)) data = nested;
        var rows = data.GetProperty("codingPlanInstanceInfos");
        JsonElement? active = null;
        foreach (var row in rows.EnumerateArray())
        {
            if (row.GetProperty("status").GetString() != "VALID") continue;
            if (active.HasValue) throw new JsonException("Ambiguous Coding Plan subscription.");
            active = row;
        }
        var selected = active ?? throw new JsonException("No active Coding Plan subscription.");
        var q = selected.GetProperty("codingPlanQuotaInfo");
        double Pct(string prefix)
        {
            var used = Number(q.GetProperty(prefix + "UsedQuota"));
            var total = Number(q.GetProperty(prefix + "TotalQuota"));
            if (total <= 0 || used < 0) throw new JsonException("Invalid Coding Plan quota.");
            return Math.Min(100, used / total * 100);
        }
        DateTimeOffset? Reset(string prefix) => q.TryGetProperty(prefix + "QuotaNextRefreshTime", out var time) ? Timestamp(time) : null;
        return new("alibaba", "Coding Plan", Pct("per5Hour"), Reset("per5Hour"), Pct("perWeek"), Reset("perWeek"), null, null, null, DateTimeOffset.UtcNow, false)
        {
            PlanPercent = Pct("perBillMonth"), PlanResetsAt = Reset("perBillMonth"),
            PrimaryWindowSeconds = 18000, WeeklyWindowSeconds = 604800
            // A subscription month is not a fixed thirty-day duration.
        };
    }

    internal static double Number(JsonElement value)
    {
        if (value.ValueKind is not (JsonValueKind.Number or JsonValueKind.String)
            || !double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n))
            throw new JsonException("Invalid plan number.");
        return n;
    }

    internal static DateTimeOffset? Timestamp(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var epoch))
        {
            try { return epoch > 1000000000000 ? DateTimeOffset.FromUnixTimeMilliseconds(epoch) : epoch > 1000000000 ? DateTimeOffset.FromUnixTimeSeconds(epoch) : null; }
            catch (ArgumentOutOfRangeException) { return null; }
        }
        // Chinese consoles display wall-clock timestamps in UTC+8. Never use the
        // computer's zone for a timestamp without an offset.
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"(?:Z|[+-]\d\d:\d\d)$")
            && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var offset)) return offset;
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            ? new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.FromHours(8)) : null;
    }
}
