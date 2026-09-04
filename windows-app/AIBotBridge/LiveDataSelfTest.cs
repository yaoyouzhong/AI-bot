namespace AIBotBridge;

internal static class LiveDataSelfTest
{
    internal static async Task RunAsync()
    {
        var settings = BridgeSettings.CreatePublicSelfTestSettings();
        var weather = new WeatherService(settings, persistCache: false);
        var stocks = new StockService(settings, persistCache: false);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await Task.WhenAll(
            weather.RefreshAsync(timeout.Token),
            stocks.RefreshAsync(timeout.Token));

        var weatherSnapshot = weather.Snapshot;
        var stockSnapshot = stocks.Snapshot;
        if (weatherSnapshot is null || weatherSnapshot.Stale)
            throw new InvalidOperationException("Live weather did not produce a fresh snapshot.");
        if (stockSnapshot is null || stockSnapshot.Stale || stockSnapshot.Quotes.Count == 0)
            throw new InvalidOperationException("Live stocks did not produce a fresh snapshot.");

        Console.WriteLine($"WEATHER_LIVE_OK source={weatherSnapshot.Source}");
        Console.WriteLine($"STOCK_LIVE_OK count={stockSnapshot.Quotes.Count}");
    }
}
