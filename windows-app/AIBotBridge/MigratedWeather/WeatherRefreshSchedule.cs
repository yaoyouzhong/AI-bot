namespace AIBotBridge.MigratedWeather;

// Keep the last successful query separate from saved settings. A settings window
// can update coordinates before the collector's next tick.
internal sealed class WeatherRefreshSchedule
{
    internal sealed record Query(bool Automatic, string City, string Provider, string Host,
        double Latitude, double Longitude);
    private Query? _last;
    private bool _locationAvailable;
    private DateTimeOffset _due;

    internal bool ShouldFetch(Query query, bool locationAvailable, bool force, DateTimeOffset now)
    {
        bool recovered = query.Automatic && locationAvailable && !_locationAvailable;
        // Remember denied/unavailable observations even when weather remains cached.
        _locationAvailable = locationAvailable;
        return force || _last != query || now >= _due || recovered;
    }

    internal void Succeeded(Query query, bool locationAvailable, DateTimeOffset now)
    {
        _last = query;
        _locationAvailable = locationAvailable;
        _due = now.AddMinutes(15);
    }

    internal void Failed() => _due = DateTimeOffset.MinValue;
}
