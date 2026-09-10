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
    private readonly List<Task> _workers = new();
    private DisplayPolicy? _displayPolicy;
    private readonly AutoFollowTracker _follow=new();
    internal void SetDisplayPolicy(DisplayPolicy policy) => _displayPolicy = policy;

    internal BridgeRuntime(bool startRefresh = true)
    {
        var settings = BridgeSettings.Load();
        _weather = new MigratedWeatherService();
        _stocks = new StockService(settings);
        _quotas = new QuotaService();
        _domesticQuotas = new MigratedDomesticBridge();
        _systemMetrics = new SystemMetricsService();
        _music = new NowPlayingService();
        if (startRefresh)
        {
            _workers.Add(Task.Run(() => _weather.RunAsync(_shutdown.Token)));
            _workers.Add(Task.Run(() => _stocks.RunAsync(_shutdown.Token)));
            _workers.Add(Task.Run(() => _quotas.RunAsync(_shutdown.Token)));
            _workers.Add(Task.Run(() => _systemMetrics.RunAsync(_shutdown.Token)));
            _workers.Add(Task.Run(() => _music.RunAsync(_shutdown.Token)));
        }
    }

    internal StatusSnapshot Capture()
    {
        var snapshot = SessionActivityReader.Capture() with
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
    internal Task RefreshAsync() => Task.WhenAll(_weather.Monitor.Refresh(), _stocks.RefreshAsync(_shutdown.Token), _quotas.RefreshAsync(_shutdown.Token));

    internal void ApplyDomesticResponse(string provider, string json) =>
        _domesticQuotas.ApplyCapturedResponse(provider, json);

    public void Dispose()
    {
        _shutdown.Cancel();
        _domesticQuotas.Dispose();
        _shutdown.Dispose();
    }
}
