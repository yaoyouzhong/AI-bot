namespace AIBotBridge;

internal sealed class MigratedWeatherService
{
    private readonly MigratedWeather.WeatherMonitor _monitor = new();
    internal MigratedWeatherService() => _monitor.LoadCache();
    internal MigratedWeather.WeatherMonitor Monitor => _monitor;
    internal WeatherSnapshot? Snapshot
    {
        get
        {
            var value = _monitor.Current;
            if (value.UpdatedUtc == 0) return null;
            return new(value.City, value.Condition, value.Temperature, value.High, value.Low,
                value.Humidity, -1, value.Pm25 < 0 ? null : value.Pm25, value.AirQualityIndex is double aqi?(int)Math.Round(aqi):null, value.Source,
                DateTimeOffset.FromUnixTimeSeconds(value.UpdatedUtc), value.Stale,
                value.AirQuality, MigratedWeather.WeatherMonitor.Animation,
                _monitor.HeaderCenterX, _monitor.DateCenterX, _monitor.RangeY, value.Icon, value.UtcOffsetS)
                { PressureHpa=value.PressureHpa,PressureKind=value.PressureKind,AirQualityStandard=value.AirQualityStandard,
                    LocationStatus=_monitor.LocationStatus,Hourly=value.Hourly,Daily=value.Daily,
                    HourlyStale=value.Stale||value.HourlyStale,DailyStale=value.Stale||value.DailyStale };
        }
    }
    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        await _monitor.Refresh();
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(cancellationToken)) await _monitor.Refresh(false);
    }
    internal IReadOnlyList<ResourcePayload> Resources()
    {
        if (Snapshot is null) return [];
        _ = _monitor.ToJson(); // Refresh the date bitmap independently of weather fetch time.
        return [new(BinaryResourceKind.WeatherHeader, _monitor.TextRev, _monitor.HeaderBitmap),
            new(BinaryResourceKind.WeatherDate, _monitor.TextRev, _monitor.DateBitmap),
            new(BinaryResourceKind.WeatherAir, _monitor.TextRev, _monitor.AirBitmap)];
    }
}
