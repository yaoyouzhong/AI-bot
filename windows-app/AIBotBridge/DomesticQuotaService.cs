using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AIBotBridge;

internal sealed class DomesticQuotaService
{
    private const string MiniMaxEndpoint = "https://www.minimaxi.com/v1/token_plan/remains";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly object _sync = new();
    private DomesticQuotaSnapshot? _snapshot;

    internal DomesticQuotaService()
    {
        var cached = SnapshotCache.Load<DomesticQuotaSnapshot>("domestic-quota-cache.json");
        var legacy = LegacyDisplayCache.Domestic();
        cached = new(cached?.Alibaba ?? legacy?.Alibaba, cached?.Kimi ?? legacy?.Kimi,
            cached?.MiniMax ?? legacy?.MiniMax, cached?.DeepSeek ?? legacy?.DeepSeek, cached?.Zhipu);
        if (cached is not null) _snapshot = MarkStale(cached);
    }

    internal DomesticQuotaSnapshot? Snapshot
    {
        get { lock (_sync) return _snapshot; }
    }

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        await RefreshMiniMaxAsync(cancellationToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
        while (await timer.WaitForNextTickAsync(cancellationToken))
            await RefreshMiniMaxAsync(cancellationToken);
    }

    internal async Task<bool> RefreshMiniMaxAsync(CancellationToken cancellationToken)
    {
        var key = ReadMiniMaxKey();
        if (key is null) return false;
        using var request = new HttpRequestMessage(HttpMethod.Get, MiniMaxEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.Accept.ParseAdd("application/json");
        try
        {
            using var response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return false;
            var parsed = Parse("minimax", await response.Content.ReadAsStringAsync(cancellationToken));
            SaveProvider(parsed);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return false;
        }
    }

    internal void ApplyCapturedResponse(string provider, string json) => SaveProvider(Parse(provider, json));

    internal static DomesticProviderQuotaSnapshot Parse(string provider, string json)
    {
        using var document = JsonDocument.Parse(json);
        return provider.ToLowerInvariant() switch
        {
            "alibaba" or "qwen" => ParseAlibaba(document.RootElement),
            "kimi" => ParseKimi(document.RootElement),
            "minimax" => ParseMiniMax(document.RootElement),
            "deepseek" => ParseDeepSeek(document.RootElement),
            "zhipu" or "glm" => ZhipuBalance.Parse(document.RootElement),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), "Unsupported domestic provider.")
        };
    }

    private void SaveProvider(DomesticProviderQuotaSnapshot value)
    {
        lock (_sync)
        {
            var current = _snapshot ?? new DomesticQuotaSnapshot(null, null, null, null);
            _snapshot = value.Provider switch
            {
                "alibaba" => current with { Alibaba = value },
                "kimi" => current with { Kimi = value },
                "minimax" => current with { MiniMax = value },
                "deepseek" => current with { DeepSeek = value },
                "zhipu" => current with { Zhipu = value },
                _ => current
            };
            SnapshotCache.Save("domestic-quota-cache.json", _snapshot);
        }
    }

    private static DomesticProviderQuotaSnapshot ParseAlibaba(JsonElement root)
    {
        var match = Find(root, value =>
        {
            var total = Number(value, "TotalValue");
            var remaining = Number(value, "TotalSurplusValue") ?? Number(value, "SurplusValue");
            return total is > 0 && remaining.HasValue
                ? Math.Clamp(100 * (total.Value - remaining.Value) / total.Value, 0, 100) : null;
        });
        if (!match.HasValue) throw new JsonException("Alibaba quota fields are missing.");
        return Windowed("alibaba", TextRecursive(root, "SubscriptionName") ?? "Token Plan",
            null, null, null, null, root) with {PlanPercent=Clamp(match),PlanResetsAt=DateRecursive(root)};
    }

    private static DomesticProviderQuotaSnapshot ParseKimi(JsonElement root)
    {
        double? weekly = null, primary = null;
        DateTimeOffset? weeklyReset = null, primaryReset = null;
        Visit(root, value =>
        {
            if (weekly.HasValue || value.ValueKind != JsonValueKind.Object ||
                !Property(value, "usages", out var usages) || usages.ValueKind != JsonValueKind.Array) return;
            foreach (var usage in usages.EnumerateArray())
            {
                if (!Property(usage, "detail", out var detail)) continue;
                weekly = UsedPercent(detail);
                weeklyReset = DateRecursive(detail) ?? DateRecursive(usage);
                if (Property(usage, "limits", out var limits) && limits.ValueKind == JsonValueKind.Array)
                {
                    foreach (var limit in limits.EnumerateArray())
                    {
                        if (!Property(limit, "detail", out var rateDetail)) continue;
                        primary = UsedPercent(rateDetail);
                        primaryReset = DateRecursive(rateDetail) ?? DateRecursive(limit);
                        if (primary.HasValue) break;
                    }
                }
                if (weekly.HasValue) break;
            }
        });
        if (!weekly.HasValue) throw new JsonException("Kimi quota fields are missing.");
        var plan = TextRecursive(root, "membership") ?? TextRecursive(root, "plan_name") ?? "Kimi Coding Plan";
        return Windowed("kimi", plan, primary, primaryReset, weekly, weeklyReset, root);
    }

    private static DomesticProviderQuotaSnapshot ParseMiniMax(JsonElement root)
    {
        DomesticProviderQuotaSnapshot? result = null;
        Visit(root, value =>
        {
            if (result is not null || value.ValueKind != JsonValueKind.Object) return;
            var weekly = Percent(value, "current_weekly") ?? Percent(value, "weekly");
            if (!weekly.HasValue) return;
            var primary = Percent(value, "current_interval") ?? Percent(value, "interval") ??
                          Percent(value, "five_hour");
            var plan = Text(value, "combo_name") ?? Text(value, "plan_name") ?? "Token Plan";
            result = Windowed("minimax", plan, primary,
                RemainingMilliseconds(value, "remains_time") ?? RemainingMilliseconds(value, "interval_remains_time"),
                weekly, RemainingMilliseconds(value, "weekly_remains_time"), value);
        });
        return result ?? throw new JsonException("MiniMax quota fields are missing.");
    }

    private static DomesticProviderQuotaSnapshot ParseDeepSeek(JsonElement root)
    {
        double? balance = null, used = null;
        string currency = "CNY";
        Visit(root, value =>
        {
            if (value.ValueKind != JsonValueKind.Object) return;
            if (!balance.HasValue)
            {
                var candidate = Number(value, "total_balance") ?? Number(value, "available_balance") ??
                                Number(value, "balance");
                if (candidate is >= 0)
                {
                    balance = candidate;
                    currency = Text(value, "currency")?.ToUpperInvariant() is "USD" ? "USD" : "CNY";
                }
            }
        });
        Visit(root, value =>
        {
            if (used.HasValue || value.ValueKind != JsonValueKind.Object ||
                !Property(value, "total_costs", out var costs) || costs.ValueKind != JsonValueKind.Array) return;
            foreach (var cost in costs.EnumerateArray())
            {
                var amount = Number(cost, "amount");
                var itemCurrency = Text(cost, "currency");
                if (amount.HasValue && (itemCurrency is null || itemCurrency.Equals(currency, StringComparison.OrdinalIgnoreCase)))
                {
                    used = amount;
                    break;
                }
            }
        });
        if (!balance.HasValue) throw new JsonException("DeepSeek balance fields are missing.");
        return new DomesticProviderQuotaSnapshot("deepseek", "API PAYG", null, null, null, null,
            balance, used, currency, DateTimeOffset.UtcNow, false);
    }

    private static DomesticProviderQuotaSnapshot Windowed(string provider, string? plan,
        double? primary, DateTimeOffset? primaryReset, double? weekly, DateTimeOffset? weeklyReset,
        JsonElement _) => new(provider, plan, Clamp(primary), primaryReset, Clamp(weekly), weeklyReset,
            null, null, null, DateTimeOffset.UtcNow, false);

    private static double? Clamp(double? value) => value.HasValue ? Math.Clamp(value.Value, 0, 100) : null;

    private static double? UsedPercent(JsonElement value)
    {
        var remaining = Number(value, "remaining");
        var limit = Number(value, "limit");
        return remaining.HasValue && limit is > 0 ? 100 * (limit.Value - remaining.Value) / limit.Value : null;
    }

    private static double? Percent(JsonElement value, string prefix)
    {
        foreach (var suffix in new[] { "_used_percent", "_usage_percent", "_usedPercent" })
            if (Number(value, prefix + suffix) is double used) return used;
        foreach (var suffix in new[] { "_remaining_percent", "_remains_percent", "_remainingPercent" })
            if (Number(value, prefix + suffix) is double remaining) return 100 - remaining;
        var total = Number(value, prefix + "_total") ?? Number(value, prefix + "_total_count");
        var remainingCount = Number(value, prefix + "_remaining") ?? Number(value, prefix + "_remains_count");
        return total is > 0 && remainingCount.HasValue ? 100 * (total.Value - remainingCount.Value) / total.Value : null;
    }

    private static DateTimeOffset? RemainingMilliseconds(JsonElement value, string name)
    {
        var milliseconds = Number(value, name);
        return milliseconds is >= 0 && milliseconds <= 8 * 24 * 60 * 60 * 1000
            ? DateTimeOffset.UtcNow.AddMilliseconds(milliseconds.Value) : null;
    }

    private static DateTimeOffset? DateRecursive(JsonElement root)
    {
        DateTimeOffset? result = null;
        Visit(root, value =>
        {
            if (result.HasValue || value.ValueKind != JsonValueKind.Object) return;
            foreach (var property in value.EnumerateObject())
            {
                var name = property.Name.ToLowerInvariant();
                if (!(name.Contains("reset") || name.Contains("expire") || name.Contains("end_time"))) continue;
                if (property.Value.ValueKind == JsonValueKind.String &&
                    DateTimeOffset.TryParse(property.Value.GetString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal, out var parsed)) result = parsed;
                else if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt64(out var epoch) &&
                         epoch is > 1700000000 and < 4102444800) result = DateTimeOffset.FromUnixTimeSeconds(epoch);
            }
        });
        return result;
    }

    private static double? Find(JsonElement root, Func<JsonElement, double?> selector)
    {
        double? result = null;
        Visit(root, value => result ??= selector(value));
        return result;
    }

    private static string? TextRecursive(JsonElement root, string name)
    {
        string? result = null;
        Visit(root, value => result ??= Text(value, name));
        return result;
    }

    private static void Visit(JsonElement value, Action<JsonElement> visitor)
    {
        visitor(value);
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var property in value.EnumerateObject()) Visit(property.Value, visitor);
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var child in value.EnumerateArray()) Visit(child, visitor);
    }

    private static bool Property(JsonElement value, string name, out JsonElement result)
    {
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var property in value.EnumerateObject())
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    result = property.Value;
                    return true;
                }
        result = default;
        return false;
    }

    private static string? Text(JsonElement value, string name) =>
        Property(value, name, out var result) && result.ValueKind == JsonValueKind.String
            ? result.GetString()?.Trim() : null;

    private static double? Number(JsonElement value, string name)
    {
        if (!Property(value, name, out var result)) return null;
        if (result.ValueKind == JsonValueKind.Number && result.TryGetDouble(out var number)) return number;
        return result.ValueKind == JsonValueKind.String &&
               double.TryParse(result.GetString(), NumberStyles.Float | NumberStyles.AllowThousands,
                   CultureInfo.InvariantCulture, out number) ? number : null;
    }

    private static string? ReadMiniMaxKey()
    {
        foreach (var name in new[] { "MINIMAX_SUBSCRIPTION_KEY", "MINIMAX_TOKEN_PLAN_KEY", "MINIMAX_API_KEY" })
        {
            var value = AIBotBridge.AppPaths.GetProviderEnvironmentVariable(name)?.Trim();
            if (!string.IsNullOrEmpty(value)) return value;
        }
        return null;
    }

    private static DomesticQuotaSnapshot MarkStale(DomesticQuotaSnapshot value) => new(
        Stale(value.Alibaba), Stale(value.Kimi), Stale(value.MiniMax), Stale(value.DeepSeek), Stale(value.Zhipu));

    private static DomesticProviderQuotaSnapshot? Stale(DomesticProviderQuotaSnapshot? value) =>
        value is null ? null : value with { Stale = true };
}
