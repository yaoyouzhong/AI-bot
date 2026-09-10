using System.Globalization;
using System.Text.Json;

namespace AIBotBridge;

// Only the official finance overview response, never model token usage or a DOM number.
internal static class ZhipuBalance
{
    internal const string AuthorizationUrl = "https://bigmodel.cn/finance-center/finance/overview";
    internal static bool IsEndpoint(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort
        && uri.Host.Equals("bigmodel.cn", StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath == "/api/biz/account/query-customer-account-report";

    internal static DomesticProviderQuotaSnapshot Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("code", out var code)
            || code.ValueKind != JsonValueKind.Number || !code.TryGetInt32(out var status) || status != 200
            || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            throw new JsonException("GLM balance response was not successful.");
        // availableBalance is the spendable amount in CNY; balance may include frozen funds.
        var available = Number(data, "availableBalance")
            ?? throw new JsonException("GLM availableBalance is missing or invalid.");
        var used = Number(data, "totalSpendAmount");
        // The official finance UI truncates to two decimal places, rather than rounding up.
        // Keep the device/mirror consistent (e.g. 99.9993936 displays as 99.99, not 100.00).
        return new("zhipu", "API PAYG", null, null, null, null, DisplayMoney(available),
            used is double cost ? DisplayMoney(cost) : null, "CNY", DateTimeOffset.UtcNow, false);
    }

    private static double DisplayMoney(double value)
    {
        if (Math.Abs(value) >= (double)decimal.MaxValue) throw new JsonException("GLM amount is out of range.");
        return (double)decimal.Round((decimal)value, 2, MidpointRounding.ToZero);
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
