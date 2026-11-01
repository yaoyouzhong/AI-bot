using System.Globalization;
using System.Text.Json;

namespace AIBotBridge;

// Only the official finance overview response, never model token usage or a DOM number.
internal static class ZhipuBalance
{
    internal const string AuthorizationUrl = "https://bigmodel.cn/finance-center/finance/overview";
    internal const string OverviewPath = "/api/biz/account/finance-overview";
    internal static bool IsEndpoint(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort
        && uri.Host.Equals("bigmodel.cn", StringComparison.OrdinalIgnoreCase)
        && (uri.AbsolutePath == "/api/biz/account/query-customer-account-report"
            || uri.AbsolutePath == OverviewPath);

    internal static DomesticProviderQuotaSnapshot Parse(JsonElement root, bool financeOverview = false)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("code", out var code)
            || code.ValueKind != JsonValueKind.Number || !code.TryGetInt32(out var status) || status != 200
            || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            throw new JsonException("GLM balance response was not successful.");
        // Bind the schema to the captured endpoint. Neither gross balance nor accountList
        // entries are substitutes for spendable funds after frozen amounts are deducted.
        var field = financeOverview ? "availableTotalBalance" : "availableBalance";
        var available = Number(data, field)
            ?? throw new JsonException($"GLM {field} is missing or invalid.");
        // The new overview does not provide cumulative spend; never invent it from balances.
        var used = financeOverview ? null : Number(data, "totalSpendAmount");
        // Current official UI uses Big.toFixed(2, 1) (half up); legacy UI truncates.
        return new("zhipu", "API PAYG", null, null, null, null, DisplayMoney(available,
            financeOverview ? MidpointRounding.AwayFromZero : MidpointRounding.ToZero),
            used is double cost ? DisplayMoney(cost) : null, "CNY", DateTimeOffset.UtcNow, false);
    }

    private static double DisplayMoney(double value, MidpointRounding rounding = MidpointRounding.ToZero)
    {
        if (Math.Abs(value) >= (double)decimal.MaxValue) throw new JsonException("GLM amount is out of range.");
        return (double)decimal.Round((decimal)value, 2, rounding);
    }

    internal static DomesticProviderQuotaSnapshot? Normalize(DomesticProviderQuotaSnapshot? value) => value is null ? null
        : value with { Balance = value.Balance is double balance ? DisplayMoney(balance) : null,
            UsedCost = value.UsedCost is double cost ? DisplayMoney(cost) : null };

    private static double? Number(JsonElement data, string name)
    {
        if (!data.TryGetProperty(name, out var value)) return null;
        double number;
        bool valid = value.ValueKind == JsonValueKind.Number ? value.TryGetDouble(out number)
            : double.TryParse(value.ValueKind == JsonValueKind.String ? value.GetString() : null,
                NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        return valid && double.IsFinite(number) ? number : null;
    }
}
