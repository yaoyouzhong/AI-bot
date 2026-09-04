namespace AIBotBridge;

internal sealed class BridgeRuntime : IDisposable
{
    private readonly CancellationTokenSource _shutdown = new();
    private readonly WeatherService _weather;
    private readonly StockService _stocks;
    private readonly QuotaService _quotas;
    private readonly DomesticQuotaService _domesticQuotas;
    private readonly SystemMetricsService _systemMetrics;
    private readonly List<Task> _workers = new();

    internal BridgeRuntime(bool startRefresh = true)
    {
        var settings = BridgeSettings.Load();
        _weather = new WeatherService(settings);
        _stocks = new StockService(settings);
        _quotas = new QuotaService();
        _domesticQuotas = new DomesticQuotaService();
        _systemMetrics = new SystemMetricsService();
        if (startRefresh)
        {
            _workers.Add(Task.Run(() => _weather.RunAsync(_shutdown.Token)));
            _workers.Add(Task.Run(() => _stocks.RunAsync(_shutdown.Token)));
            _workers.Add(Task.Run(() => _quotas.RunAsync(_shutdown.Token)));
            _workers.Add(Task.Run(() => _domesticQuotas.RunAsync(_shutdown.Token)));
            _workers.Add(Task.Run(() => _systemMetrics.RunAsync(_shutdown.Token)));
        }
    }

    internal StatusSnapshot Capture()
    {
        return SessionActivityReader.Capture() with
        {
            Weather = _weather.Snapshot,
            Stocks = _stocks.Snapshot,
            Quotas = _quotas.Snapshot,
            DomesticQuotas = _domesticQuotas.Snapshot,
            SystemMetrics = _systemMetrics.Snapshot
        };
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _shutdown.Dispose();
    }
}
