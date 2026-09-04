namespace AIBotBridge;

internal static class DataSourceSelfTest
{
    internal static void Run()
    {
        const string forecast = """
            {"current":{"temperature_2m":23.5,"relative_humidity_2m":62,"weather_code":3},
             "daily":{"temperature_2m_max":[28.0],"temperature_2m_min":[18.0]}}
            """;
        const string air = """{"current":{"us_aqi":41,"pm2_5":12.4}}""";
        var weather = WeatherService.Parse(forecast, air, "测试城市");
        if (weather.Condition != "阴" || weather.Humidity != 62 || weather.Pm25 != 12.4)
            throw new InvalidOperationException("Weather parser did not preserve the provider fields.");

        var fields = Enumerable.Repeat(string.Empty, 33).ToArray();
        fields[1] = "Example";
        fields[2] = "000001";
        fields[3] = "10.50";
        fields[31] = "0.20";
        fields[32] = "1.94";
        var quotes = StockService.ParseTencent(
            $"v_sh000001=\"{string.Join('~', fields)}\";", ["sh000001"]);
        if (quotes.Count != 1 || quotes[0].Trend != 1 || quotes[0].ChangePercent != "+1.94%")
            throw new InvalidOperationException("Stock parser did not preserve order or trend semantics.");

        Console.WriteLine("DATA_SOURCE_SELF_TEST_OK");
    }
}
