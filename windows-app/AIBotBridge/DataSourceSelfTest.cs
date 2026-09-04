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

        const string claudeJson = """
            {"plan_type":"max","five_hour":{"utilization":32.5,"resets_at":"2026-09-04T12:00:00Z"},
             "seven_day":{"utilization":61.25,"resets_at":"2026-09-08T00:00:00Z"}}
            """;
        var claude = QuotaService.ParseClaude(claudeJson);
        if (claude.Plan != "max" || claude.PrimaryPercent != 32.5 || claude.WeeklyPercent != 61.25 ||
            claude.PrimaryResetsAt?.ToUnixTimeSeconds() != 1788523200)
            throw new InvalidOperationException("Claude quota parser did not preserve windows or reset time.");

        const string codexJson = """
            {"plan_type":"plus","rate_limit":{"primary_window":{"limit_window_seconds":18000,
             "used_percent":18.5,"reset_at":1788526800},"secondary_window":{"limit_window_seconds":604800,
             "used_percent":42,"reset_at":1788998400}},"rate_limit_reset_credits":{"available_count":1}}
            """;
        const string creditJson = """
            {"available_count":2,"credits":[{"status":"used","expires_at":"2026-09-10T00:00:00Z"},
             {"status":"available","expires_at":"2026-09-09T00:00:00Z"}]}
            """;
        var codex = QuotaService.ParseCodex(codexJson, creditJson);
        if (codex.Plan != "plus" || codex.PrimaryPercent != 18.5 || codex.WeeklyPercent != 42 ||
            codex.ResetCreditsAvailable != 2 || codex.ResetCreditExpiresAt.Count != 1)
            throw new InvalidOperationException("Codex quota parser did not preserve windows or reset credits.");

        Console.WriteLine("DATA_SOURCE_SELF_TEST_OK");
    }
}
