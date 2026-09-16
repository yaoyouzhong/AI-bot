using System.Text.Json;

namespace AIBotBridge;

internal sealed class MigratedDomesticBridge : IDisposable
{
    private readonly MigratedDomestic.DomesticQuotaService _service = new();
    private readonly DomesticQuotaSnapshot? _fallback = new DomesticQuotaService().Snapshot;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset _nextRefresh;
    private int _providerIndex;
    private DateTimeOffset _nextApiRefresh;
    private static readonly string[] Providers = ["qwen", "kimi", "minimax", "deepseek", "zhipu"];

    internal DomesticQuotaSnapshot Snapshot
    {
        get
        {
            var parsed = LegacyDisplayCache.ParseDomestic(JsonSerializer.SerializeToElement(_service.Snapshot,
                new JsonSerializerOptions { IncludeFields = true }));
            DomesticProviderQuotaSnapshot? Pick(DomesticProviderQuotaSnapshot? current, DomesticProviderQuotaSnapshot? fallback)
            {
                if (current is null || fallback is not null && fallback.UpdatedAt > current.UpdatedAt) current = fallback;
                if (current is null) return null;
                return current with { Stale = _service.RefreshHealth.Failed(current.Provider == "alibaba" ? "qwen" : current.Provider) || current.UpdatedAt < _startedAt || DateTimeOffset.UtcNow - current.UpdatedAt > TimeSpan.FromMinutes(6) };
            }
            return new(Pick(parsed.Alibaba, _fallback?.Alibaba), Pick(parsed.Kimi, _fallback?.Kimi),
                Pick(parsed.MiniMax, _fallback?.MiniMax), Pick(parsed.DeepSeek, _fallback?.DeepSeek),
                ZhipuBalance.Normalize(Pick(_service.Snapshot.Zhipu, _fallback?.Zhipu)));
        }
    }
    // Called by the tray's UI timer: WebView2 and its window remain on the STA.
    internal void RefreshNext()
    {
        if (DateTimeOffset.UtcNow >= _nextApiRefresh) {
            _nextApiRefresh = DateTimeOffset.UtcNow.AddMinutes(2);
            foreach (var provider in Providers.Where(_service.HasOfficialApi)) _service.Refresh(provider);
        }
        if (DateTimeOffset.UtcNow < _nextRefresh) return;
        _nextRefresh = DateTimeOffset.UtcNow.AddSeconds(65);
        for (int attempt=0;attempt<Providers.Length;attempt++) {
            var provider=Providers[_providerIndex++ % Providers.Length];
            if (!_service.HasOfficialApi(provider)) { _service.Refresh(provider); break; }
        }
    }
    internal void RefreshNow()
    {
        foreach (var provider in Providers.Where(_service.HasOfficialApi)) _service.Refresh(provider, force:true);
        var selected=BridgeSettings.Load().Get("domestic_provider","alibaba");
        if(selected=="alibaba")selected="qwen";
        if(Providers.Contains(selected) && !_service.HasOfficialApi(selected)) _service.Refresh(selected,force:true);
        _nextRefresh=DateTimeOffset.UtcNow.AddSeconds(65);
    }
    internal string? TakeRefreshWarning()
    {
        if(DateTimeOffset.UtcNow-_startedAt > TimeSpan.FromMinutes(6)) {
            var s=Snapshot;
            foreach(var q in new[]{s.Alibaba,s.Kimi,s.MiniMax,s.DeepSeek,s.Zhipu})
                if(q?.Stale==true) _service.RefreshHealth.Fail(q.Provider=="alibaba"?"qwen":q.Provider,
                    $"{q.Provider} 额度尚未更新，正在显示旧数据。请打开国产模型额度设置检查接口或重新登录。");
        }
        return _service.RefreshHealth.TakeWarning();
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
