using System.Text.Json;

namespace AIBotBridge;

internal sealed class MigratedDomesticBridge : IDisposable
{
    private readonly MigratedDomestic.DomesticQuotaService _service = new();
    private readonly DomesticQuotaSnapshot? _fallback = new DomesticQuotaService().Snapshot;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset _nextRefresh;
    private int _providerIndex;
    private static readonly string[] Providers = ["qwen", "kimi", "minimax", "deepseek", "zhipu"];

    internal DomesticQuotaSnapshot Snapshot
    {
        get
        {
            var parsed = LegacyDisplayCache.ParseDomestic(JsonSerializer.SerializeToElement(_service.Snapshot,
                new JsonSerializerOptions { IncludeFields = true }));
            DomesticProviderQuotaSnapshot? Pick(DomesticProviderQuotaSnapshot? current, DomesticProviderQuotaSnapshot? fallback)
            {
                if (current is null || fallback is not null && fallback.UpdatedAt > current.UpdatedAt) return fallback;
                return current with { Stale = current.UpdatedAt < _startedAt || DateTimeOffset.UtcNow - current.UpdatedAt > TimeSpan.FromMinutes(6) };
            }
            return new(Pick(parsed.Alibaba, _fallback?.Alibaba), Pick(parsed.Kimi, _fallback?.Kimi),
                Pick(parsed.MiniMax, _fallback?.MiniMax), Pick(parsed.DeepSeek, _fallback?.DeepSeek),
                ZhipuBalance.Normalize(Pick(_service.Snapshot.Zhipu, _fallback?.Zhipu)));
        }
    }
    // Called by the tray's UI timer: WebView2 and its window remain on the STA.
    internal void RefreshNext()
    {
        if (DateTimeOffset.UtcNow < _nextRefresh) return;
        _nextRefresh = DateTimeOffset.UtcNow.AddSeconds(65);
        _service.Refresh(Providers[_providerIndex++ % Providers.Length]);
    }
    internal void OpenAuthorization(string provider = "qwen") => _service.OpenAuthorization(provider);
    internal void ApplyCapturedResponse(string provider, string json)
    {
        var value = DomesticQuotaService.Parse(provider, json);
        switch (value.Provider)
        {
            case "alibaba": _service.SetQwen(value.PlanPercent!.Value, value.Plan, value.PlanResetsAt); break;
            case "kimi": _service.SetKimi(value.WeeklyPercent!.Value, value.PrimaryPercent, value.Plan, value.WeeklyResetsAt, value.PrimaryResetsAt); break;
            case "minimax": _service.SetMiniMax(value.WeeklyPercent!.Value, value.PrimaryPercent, value.Plan, value.WeeklyResetsAt, value.PrimaryResetsAt); break;
            case "zhipu": _service.SetZhipu(value); break;
            case "deepseek": _service.SetDeepSeek(value.Balance!.Value, value.Currency, usedCost: value.UsedCost); break;
        }
    }
    public void Dispose() => _service.DisposeBrowser();
}
