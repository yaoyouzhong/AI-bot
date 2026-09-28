using System.Text.Json;

namespace AIBotBridge;

internal sealed class MigratedDomesticBridge : IDisposable
{
    private readonly MigratedDomestic.DomesticQuotaService _service = new();
    private readonly DomesticQuotaSnapshot? _fallback = new DomesticQuotaService().Snapshot;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset _nextRefresh;
    private int _providerIndex;
    private DisplayPolicy? _monitorPolicy;
    private string _monitorSelection = "";
    private TimeSpan _staleAfter = TimeSpan.FromMinutes(6);
    private DateTimeOffset _nextApiRefresh;
    private static readonly string[] Providers = ["qwen", "kimi", "minimax", "deepseek", "zhipu", "stepfun", "baidu", "xiaomi"];
    internal Func<HashSet<string>>? DeviceProviders {get;set;}
    internal Func<bool> Tab5Paired { get; set; } = () => false;

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
                return current with { Stale = _service.RefreshHealth.Failed(current.Provider == "alibaba" ? "qwen" : current.Provider) || current.UpdatedAt < _startedAt || DateTimeOffset.UtcNow - current.UpdatedAt > _staleAfter };
            }
            return new(Pick(parsed.Alibaba, _fallback?.Alibaba), Pick(parsed.Kimi, _fallback?.Kimi),
                Pick(parsed.MiniMax, _fallback?.MiniMax), Pick(parsed.DeepSeek, _fallback?.DeepSeek),
                ZhipuBalance.Normalize(Pick(_service.Snapshot.Zhipu, _fallback?.Zhipu)),
                Pick(_service.Snapshot.StepFun, null), Pick(_service.Snapshot.Baidu, null),
                Pick(XiaomiQuota.Snapshot(_service.Snapshot.XiaomiPlanPct, _service.Snapshot.XiaomiFetchedAt), null));
        }
    }
    // Called by the tray's UI timer: WebView2 and its window remain on the STA.
    private HashSet<string> Monitored(DisplayPolicy policy)
    {
        if(DeviceProviders is not null)return DeviceProviders().Where(p=>_service.WasAuthorized(p)||_service.HasOfficialApi(p)).ToHashSet(StringComparer.Ordinal);
        return QuotaMonitoringPolicy.Monitored(policy,BridgeSettings.Load().Get("domestic_provider","alibaba"),
            p=>_service.WasAuthorized(p) || _service.HasOfficialApi(p),Tab5Paired());
    }
    private string[] WebProviders(HashSet<string> monitored) => QuotaMonitoringPolicy.OrderWeb(
        Providers.Where(p=>monitored.Contains(p) && !_service.UsesOnlyOfficialApi(p) && p is not ("stepfun" or "baidu")),Tab5Paired());
    internal void RefreshNext(DisplayPolicy policy)
    {
        _monitorPolicy=policy;
        var monitored=Monitored(policy);
        var selection=string.Join(",",monitored.Order());
        if(selection!=_monitorSelection) { _monitorSelection=selection; _nextRefresh=_nextApiRefresh=DateTimeOffset.MinValue; _providerIndex=0; }
        var web=WebProviders(monitored);
        _staleAfter=TimeSpan.FromSeconds(Math.Max(360,65*(web.Length+1)));
        // Allow a full background refresh round and its capture timeout before notifying.
        _service.RefreshHealth.WarningDelay=TimeSpan.FromSeconds(Math.Max(180,65*(web.Length+1)));
        if (DateTimeOffset.UtcNow >= _nextApiRefresh) {
            _nextApiRefresh = DateTimeOffset.UtcNow.AddMinutes(2);
            foreach (var provider in monitored.Where(_service.HasOfficialApi)) _service.Refresh(provider);
        }
        if (DateTimeOffset.UtcNow < _nextRefresh || web.Length==0) return;
        _nextRefresh = DateTimeOffset.UtcNow.AddSeconds(65);
        _service.Refresh(web[_providerIndex++ % web.Length],webOnly:true);
    }
    internal void RefreshNow()
    {
        var monitored=Monitored(_monitorPolicy ?? DisplayModes.Load(BridgeSettings.Load()));
        foreach (var provider in monitored.Where(_service.HasOfficialApi)) _service.Refresh(provider,force:true);
        var web=WebProviders(monitored).FirstOrDefault();
        if(web is not null) _service.Refresh(web,force:true,webOnly:true);
        _nextRefresh=DateTimeOffset.UtcNow.AddSeconds(65);
    }
    internal string? TakeRefreshWarning(DisplayPolicy policy)
    {
        var monitored=Monitored(policy);
        if(DateTimeOffset.UtcNow-_startedAt > _staleAfter) {
            var s=Snapshot;
            foreach(var q in new[]{s.Alibaba,s.Kimi,s.MiniMax,s.DeepSeek,s.Zhipu,s.StepFun,s.Baidu,s.Xiaomi}) {
                if(q is null) continue;
                var provider=q.Provider=="alibaba"?"qwen":q.Provider;
                if(monitored.Contains(provider) && q.Stale && !_service.RefreshHealth.Failed(provider)) _service.RefreshHealth.Fail(provider,
                    $"{MigratedDomestic.DomesticProviderCatalog.All.First(p=>p.Id==provider).Name} 额度暂未更新，正在自动重试并显示旧数据。可打开国产模型额度设置查看详情。");
            }
        }
        return _service.RefreshHealth.TakeWarning(monitored.Contains);
    }
    internal void OpenAuthorization(string provider = "qwen") => _service.OpenAuthorization(provider);
    internal void ApplyCapturedResponse(string provider, string json)
    {
        if(provider=="xiaomi") {
            using var doc=JsonDocument.Parse(json);
            _service.SetXiaomi(XiaomiQuota.Parse(doc.RootElement)); return;
        }
        var value = DomesticQuotaService.Parse(provider, json);
        switch (value.Provider)
        {
            case "alibaba": _service.SetQwen(value.PlanPercent!.Value, value.Plan, value.PlanResetsAt, value.PlanExpiresAt); break;
            case "kimi": _service.SetKimi(value.WeeklyPercent!.Value, value.PrimaryPercent, value.Plan, value.WeeklyResetsAt, value.PrimaryResetsAt, value.PlanExpiresAt); break;
            case "minimax": _service.SetMiniMax(value.WeeklyPercent!.Value, value.PrimaryPercent, value.Plan, value.WeeklyResetsAt, value.PrimaryResetsAt, value.PlanExpiresAt); break;
            case "zhipu": _service.SetZhipu(value); break;
            case "deepseek": _service.SetDeepSeek(value.Balance!.Value, value.Currency, usedCost: value.UsedCost); break;
        }
    }
    public void Dispose() => _service.DisposeBrowser();
}
