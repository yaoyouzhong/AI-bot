using System.Globalization;
using System.Text.Json;

namespace AIBotBridge;

// Read-only, field-by-field adaptation of display caches. Never deserialize or
// migrate authentication files, arbitrary settings, errors, cookies or tokens.
internal static class LegacyDisplayCache
{
    internal static QuotaSnapshot? Usage() => Read("usage-cache.json", ParseUsage);
    internal static DomesticQuotaSnapshot? Domestic() => Read("domestic-quota-cache.json", ParseDomestic);
    private static T? Read<T>(string name, Func<JsonElement, T> parse) where T : class
    {
        try
        {
            var path = Path.Combine(AIBotBridge.AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AIClockBridge", name);
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > 1024 * 1024) return null;
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            return parse(json.RootElement);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentOutOfRangeException) { return null; }
    }
    internal static QuotaSnapshot ParseUsage(JsonElement root)
    {
        ProviderQuotaSnapshot? Provider(string name)
        {
            var value = Field(root, name);
            var primary = Number(value, "PrimaryPct"); var weekly = Number(value, "WeeklyPct");
            var credits = Number(value, "ResetCreditsAvailable");
            if (primary is null && weekly is null && credits is null) return null;
            var fetched = Date(value, "FetchedAt");
            DateTimeOffset? Reset(string key) => fetched is not null && Number(value, key) is double minutes && minutes is >= 0 and <= 525600 ? fetched.Value.AddMinutes(minutes) : null;
            var list = Field(value, "ResetCreditExpiresAtList");
            var dates = list.ValueKind == JsonValueKind.Array ? list.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) && n > 0 && n <= 7258118400).Select(v => v.GetInt64()).ToArray() : [];
            if (dates.Length == 0 && Number(value, "ResetCreditExpiresAt") is double earliest && earliest is > 0 and <= 7258118400) dates = [(long)earliest];
            return new(name.ToLowerInvariant(), Text(value, "Plan"), primary, Reset("PrimaryResetMin"), weekly, Reset("WeeklyResetMin"),
                credits is >= 0 and <= 10000 ? (int)credits : null, dates, fetched ?? DateTimeOffset.UnixEpoch, true);
        }
        return new(Provider("Claude"), Provider("Codex"));
    }
    internal static DomesticQuotaSnapshot ParseDomestic(JsonElement root)
    {
        DomesticProviderQuotaSnapshot? Provider(string id, string prefix)
        {
            var primary = Number(root, prefix + "FiveHourPct");
            var weekly = Number(root, prefix + "WeeklyPct");
            var plan = Number(root, prefix + "PlanPct");
            var balance = Number(root, prefix + "Balance");
            if (primary is null && weekly is null && plan is null && balance is null) return null;
            return new(id, Text(root, prefix + "Membership"), primary, Date(root, prefix + "FiveHourResetAt"), weekly,
                Date(root, prefix + "WeeklyResetAt"), balance,
                Number(root, prefix + "UsedCost"), Text(root, prefix + "Currency"), Date(root, prefix + "FetchedAt") ?? DateTimeOffset.UnixEpoch, true)
                { PlanPercent=plan, PlanResetsAt=Date(root,prefix+"PlanResetAt") };
        }
        return new(Provider("alibaba", "Qwen"), Provider("kimi", "Kimi"), Provider("minimax", "MiniMax"), Provider("deepseek", "DeepSeek"));
    }
    private static JsonElement Field(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var field) ? field : default;
    private static string? Text(JsonElement value, string key) => Field(value, key) is var field && field.ValueKind == JsonValueKind.String ? field.GetString() : null;
    private static double? Number(JsonElement value, string key) => Field(value, key) is var field && field.ValueKind == JsonValueKind.Number && field.TryGetDouble(out var n) && double.IsFinite(n) ? n : null;
    private static DateTimeOffset? Date(JsonElement value, string key) => DateTimeOffset.TryParse(Text(value, key), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : null;
}
