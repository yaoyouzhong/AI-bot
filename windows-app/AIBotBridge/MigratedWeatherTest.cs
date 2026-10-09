namespace AIBotBridge;

internal static class MigratedWeatherTest
{
    internal static async Task FreeAsync()
    {
        AppPaths.BeginPublicSelfTest();
        var monitor=new MigratedWeather.WeatherMonitor();
        // Public reference coordinates, no user location/credential reads or writes.
        var snapshot=await monitor.TestSettings("ignored.qweatherapi.com","unused-fixture","",true,32,119,"open-meteo");
        if(snapshot.Source!="open-meteo"||snapshot.UpdatedUtc<=0||snapshot.Daily.Length!=7||snapshot.Hourly.Length<24||snapshot.AirQualityStandard!="us-epa")
            throw new InvalidOperationException("Free weather route returned incomplete data or incorrect source.");
        var settings=BridgeSettings.Load();
        if(!settings.SaveEditable(new Dictionary<string,string>{["weather_provider"]="open-meteo"},out var error)||MigratedWeather.WeatherMonitor.Provider!="open-meteo")throw new Exception(error);
        Console.WriteLine($"FREE_WEATHER_OK source={snapshot.Source} hours={snapshot.Hourly.Length} days={snapshot.Daily.Length} AQI={snapshot.AirQualityStandard}; selected source saved in isolated profile");
    }
    internal static async Task LocateAsync(string output)
    {
        object result;
        try {
            var position=await MigratedWeather.WindowsLocation.LocateSilently();
            var snapshot=await new MigratedWeather.WeatherMonitor().TestQWeather(
                MigratedWeather.WeatherMonitor.QWeatherApiHost,"",MigratedWeather.WeatherMonitor.City,true,
                position.Latitude,position.Longitude);
            result=new{status="ok",city=snapshot.City};
        } catch(Exception ex) {result=new{status="failed",error=ex.GetType().Name,hresult=ex.HResult};}
        // Numeric/status diagnostics only. Never write coordinates, provider host or credentials.
        using var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write);
        await System.Text.Json.JsonSerializer.SerializeAsync(stream,result);
    }
    internal static async Task RunAsync()
    {
        WeatherForecastSelfTest.Run();
        var service = new MigratedWeatherService();
        var monitor = service.Monitor;
        // Explicit live read: no HTTP server, USB, credential writes or location prompt.
        var snapshot = await monitor.TestQWeather(MigratedWeather.WeatherMonitor.QWeatherApiHost, "",
            MigratedWeather.WeatherMonitor.City, MigratedWeather.WeatherMonitor.AutoLocation,
            MigratedWeather.WeatherMonitor.Latitude, MigratedWeather.WeatherMonitor.Longitude);
        if (snapshot.Source != "qweather" || snapshot.UpdatedUtc <= 0)
            throw new InvalidOperationException("QWeather data was not retrieved.");
        if(snapshot.Hourly.Length!=24||snapshot.Daily.Length!=7)throw new InvalidOperationException($"Incomplete forecasts: hours={snapshot.Hourly.Length}, days={snapshot.Daily.Length}");
        Console.WriteLine($"MIGRATED_QWEATHER_OK hourly={snapshot.Hourly.Length} daily={snapshot.Daily.Length} hour_temperatures={snapshot.Hourly.Count(h=>h.Temperature is not null)} hour_rain_probabilities={snapshot.Hourly.Count(h=>h.RainProbability is not null)} first_hour={snapshot.Hourly[0].Time} first_day={snapshot.Daily[0].Date} last_day={snapshot.Daily[^1].Date}");
    }
}
