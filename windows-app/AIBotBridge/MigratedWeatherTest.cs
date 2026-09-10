namespace AIBotBridge;

internal static class MigratedWeatherTest
{
    internal static async Task RunAsync()
    {
        var service = new MigratedWeatherService();
        var monitor = service.Monitor;
        // Explicit live read: no HTTP server, USB, credential writes or location prompt.
        var snapshot = await monitor.TestQWeather(MigratedWeather.WeatherMonitor.QWeatherApiHost, "",
            MigratedWeather.WeatherMonitor.City, MigratedWeather.WeatherMonitor.AutoLocation,
            MigratedWeather.WeatherMonitor.Latitude, MigratedWeather.WeatherMonitor.Longitude);
        if (snapshot.Source != "qweather" || snapshot.UpdatedUtc <= 0)
            throw new InvalidOperationException("QWeather data was not retrieved.");
        Console.WriteLine($"MIGRATED_QWEATHER_OK city={snapshot.City} temperature={snapshot.Temperature} air={snapshot.AirQuality}");
    }
}
