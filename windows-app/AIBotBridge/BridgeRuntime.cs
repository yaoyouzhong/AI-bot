namespace AIBotBridge;

internal sealed class BridgeRuntime : IDisposable
{
    private readonly CancellationTokenSource _shutdown = new();
    private readonly WeatherService _weather;
    private readonly StockService _stocks;
    private readonly List<Task> _workers = new();

    internal BridgeRuntime(bool startRefresh = true)
    {
        var settings = BridgeSettings.Load();
        _weather = new WeatherService(settings);
        _stocks = new StockService(settings);
        if (startRefresh)
        {
            _workers.Add(Task.Run(() => _weather.RunAsync(_shutdown.Token)));
            _workers.Add(Task.Run(() => _stocks.RunAsync(_shutdown.Token)));
        }
    }

    internal StatusSnapshot Capture()
    {
        return SessionActivityReader.Capture() with
        {
            Weather = _weather.Snapshot,
            Stocks = _stocks.Snapshot
        };
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _shutdown.Dispose();
    }
}
