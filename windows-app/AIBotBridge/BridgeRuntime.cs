namespace AIBotBridge;

internal sealed class BridgeRuntime : IDisposable
{
    private readonly CancellationTokenSource _shutdown = new();
    private readonly MigratedWeatherService _weather;
    private readonly StockService _stocks;
    private readonly QuotaService _quotas;
    private readonly MigratedDomesticBridge _domesticQuotas;
    private readonly SystemMetricsService _systemMetrics;
    private readonly NowPlayingService _music;
    private readonly LocalizedTextResources _localizedText = new();
    private readonly Dictionary<string,(CancellationTokenSource Stop,Task Run)> _workers = new();
    private HashSet<string> _sources=DeviceRegistryStore.SourceIds.ToHashSet();
    private readonly Func<string,CancellationToken,Task>? _collector;
    private readonly SemaphoreSlim _demandGate=new(1,1);
    internal string[] ActiveCollectors=>_workers.Keys.Order().ToArray();
    private DisplayPolicy? _displayPolicy;
    private readonly AutoFollowTracker _follow=new();
    internal void SetDisplayPolicy(DisplayPolicy policy) => _displayPolicy = policy;

    internal BridgeRuntime(bool startRefresh = true,Func<string,CancellationToken,Task>? collector=null)
    {
        _collector=collector;
        var settings = BridgeSettings.Load();
        _weather = new MigratedWeatherService();
        _stocks = new StockService(settings);
        _quotas = new QuotaService();
        _domesticQuotas = new MigratedDomesticBridge();
        _systemMetrics = new SystemMetricsService();
        _music = new NowPlayingService();
        if(startRefresh)StartCollectors(_sources);
    }
    private void StartCollectors(HashSet<string> sources) {
        foreach(var (name,run) in new (string,Func<CancellationToken,Task>)[]{("weather",_weather.RunAsync),("stocks",_stocks.RunAsync),("quotas",_quotas.RunAsync),("system",_systemMetrics.RunAsync),("music",_music.RunAsync)}) {
            if(!sources.Contains(name)||_workers.ContainsKey(name))continue;
            var stop=CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);_workers[name]=(stop,Task.Run(()=>_collector is null?run(stop.Token):_collector(name,stop.Token)));
        }
    }
    internal async Task ApplyDemandAsync(DeviceDataDemand demand) {
        await _demandGate.WaitAsync();try {
            foreach(string key in _workers.Keys.Where(k=>!demand.Sources.Contains(k)).ToArray()) {
                var worker=_workers[key];worker.Stop.Cancel();try{await worker.Run;}catch(OperationCanceledException) when(worker.Stop.IsCancellationRequested){}finally{worker.Stop.Dispose();_workers.Remove(key);}
            }
            _sources=new(demand.Sources);StartCollectors(_sources);
        }finally{_demandGate.Release();}
    }

    internal StatusSnapshot Capture()
    {
        var now=DateTimeOffset.Now;
        var activity=_sources.Contains("activity")?SessionActivityReader.Capture():new StatusSnapshot(1,now.ToString("HH:mm:ss"),now.ToUnixTimeSeconds(),(int)now.Offset.TotalSeconds,now,new ToolState("idle",null),new ToolState("idle",null));
        var snapshot = activity with
        {
            Weather = _weather.Snapshot,
            Stocks = _stocks.Snapshot,
            Quotas = _quotas.Snapshot,
            DomesticQuotas = _domesticQuotas.Snapshot,
            SystemMetrics = _systemMetrics.Snapshot,
            Music = _music.Snapshot,
            DisplayPolicy = _displayPolicy
        };
        return snapshot with {FollowApp=_follow.Update(snapshot,Environment.TickCount64)};
    }

    internal IReadOnlyList<ResourcePayload> Resources() => _music.Resources
        .Concat(_localizedText.Capture(_weather.Snapshot, _stocks.Snapshot)).Concat(_weather.Resources()).Concat(PetAnimationStore.Shared.AllResources()).Concat(LocalPageLogos.Resources()).ToArray();

    internal MigratedWeather.WeatherMonitor Weather => _weather.Monitor;
    internal SystemMetricsSnapshot? SystemMetrics => _systemMetrics.Snapshot;
    internal MigratedDomesticBridge Domestic => _domesticQuotas;
    internal void ReloadSettings() => _stocks.ReloadSettings(BridgeSettings.Load());
    internal Task RefreshAsync() { var tasks=new List<Task>();if(_sources.Contains("quotas")){_domesticQuotas.RefreshNow();tasks.Add(_quotas.RefreshAsync(_shutdown.Token));}if(_sources.Contains("weather"))tasks.Add(_weather.Monitor.Refresh());if(_sources.Contains("stocks"))tasks.Add(_stocks.RefreshAsync(_shutdown.Token));return Task.WhenAll(tasks); }

    internal void ApplyDomesticResponse(string provider, string json) =>
        _domesticQuotas.ApplyCapturedResponse(provider, json);

    public void Dispose()
    {
        _shutdown.Cancel();
        _domesticQuotas.Dispose();
        // Worker token sources are released after cancellation; cached data remains available.
        foreach(var worker in _workers.Values){worker.Stop.Cancel();_=worker.Run.ContinueWith(_=>worker.Stop.Dispose(),TaskScheduler.Default);}
        _workers.Clear();
        _shutdown.Dispose();
    }
}
