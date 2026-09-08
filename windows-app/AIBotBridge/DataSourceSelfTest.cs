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

        const string multipleCredits = """
            {"available_count":4,"credits":[
             {"status":"available","expires_at":"2026-09-22T20:00:00Z"},
             {"status":"used","expires_at":"2026-09-10T00:00:00Z"},
             {"status":"available","expires_at":"2026-09-21T00:00:00Z"},
             {"status":"available","expires_at":"2026-09-21T00:00:00Z"}]}
            """;
        var multiple = QuotaService.ParseCodex(codexJson, multipleCredits);
        var creditRows = ResetCreditDisplay.Rows(multiple, 8 * 3600);
        if (creditRows.Count != 4 || creditRows[0] != new ResetCreditDisplay.Row(1, "9/21") ||
            creditRows[1] != creditRows[0] || creditRows[2].Date != "9/23" ||
            creditRows[3] != new ResetCreditDisplay.Row(1, "--") ||
            ResetCreditDisplay.Rows(multiple with { ResetCreditsAvailable = 0, ResetCreditExpiresAt = [] }, 0)
                .Single() != new ResetCreditDisplay.Row(0, "--"))
            throw new InvalidOperationException("Reset credits lost duplicates, timezone, or unknown dates.");
        var missingDetails = multiple with { ResetCreditExpiresAt = [] };
        var retained = QuotaService.MergeCodex(missingDetails, multiple)!;
        if (!retained.Stale || retained.ResetCreditExpiresAt.Count != 3 ||
            QuotaService.MergeCodex(missingDetails with { ResetCreditsAvailable = 1 }, multiple)!.ResetCreditExpiresAt.Count != 0 ||
            QuotaService.MergeCodex(missingDetails with { ResetCreditsAvailable = 0 }, multiple)!.ResetCreditExpiresAt.Count != 0)
            throw new InvalidOperationException("Credit-detail fallback failed or retained dates after the count changed.");

        var alibaba = DomesticQuotaService.Parse("alibaba",
            """{"data":{"TotalValue":1000000,"TotalSurplusValue":750000,"SubscriptionName":"Token Plan 团队版","reset_at":"2026-10-01T00:00:00+08:00"}}""");
        if (alibaba.WeeklyPercent != 25 || alibaba.Plan != "Token Plan 团队版")
            throw new InvalidOperationException("Alibaba quota parser did not preserve plan usage.");

        var kimi = DomesticQuotaService.Parse("kimi",
            """{"membership":"Ultra","usages":[{"detail":{"limit":1000,"remaining":600,"reset_at":"2026-09-08T00:00:00+08:00"},"limits":[{"detail":{"limit":100,"remaining":90,"reset_at":"2026-09-04T20:00:00+08:00"}}]}]}""");
        if (kimi.Plan != "Ultra" || kimi.WeeklyPercent != 40 || kimi.PrimaryPercent != 10)
            throw new InvalidOperationException("Kimi quota parser did not preserve both windows.");

        var miniMaxBefore = DateTimeOffset.UtcNow;
        var miniMax = DomesticQuotaService.Parse("minimax",
            """{"data":{"plan_name":"Coding Plan","current_weekly_used_percent":35,"current_interval_remaining_percent":80,"weekly_remains_time":86400000,"remains_time":3600000}}""");
        if (miniMax.Plan != "Coding Plan" || miniMax.WeeklyPercent != 35 || miniMax.PrimaryPercent != 20 ||
            miniMax.PrimaryResetsAt is null || miniMax.WeeklyResetsAt is null ||
            Math.Abs((miniMax.PrimaryResetsAt.Value - miniMaxBefore).TotalSeconds - 3600) > 5 ||
            Math.Abs((miniMax.WeeklyResetsAt.Value - miniMaxBefore).TotalSeconds - 86400) > 5)
            throw new InvalidOperationException("MiniMax quota parser did not preserve both windows.");

        var deepSeek = DomesticQuotaService.Parse("deepseek",
            """{"balance_infos":[{"currency":"CNY","total_balance":"28.50"}],"total_costs":[{"currency":"CNY","amount":9.75}]}""");
        if (deepSeek.Balance != 28.5 || deepSeek.UsedCost != 9.75 || deepSeek.Currency != "CNY")
            throw new InvalidOperationException("DeepSeek quota parser did not preserve balance and cost.");

        if (SystemMetricsService.CalculateRate(1000, 2500, 0.5) != 3000 ||
            SystemMetricsService.CalculateRate(2500, 1000, 1) != 0)
            throw new InvalidOperationException("Network-rate calculation did not handle elapsed time or reset.");

        var rates = SystemMetricsService.AggregateNetworkRates(
            new Dictionary<string, SystemMetricsService.NetworkTotals>
            { ["kept"] = new(100, 200), ["removed"] = new(9000, 9000), ["reset"] = new(1000, 1000) },
            new Dictionary<string, SystemMetricsService.NetworkTotals>
            { ["kept"] = new(300, 600), ["new"] = new(800000, 900000), ["reset"] = new(10, 20) }, 2);
        if (rates != new SystemMetricsService.NetworkTotals(100, 200))
            throw new InvalidOperationException("Adapter changes fabricated traffic or lost the stable adapter delta.");

        if (!SerialPublisher.TryParsePong(
                "@AIBOT {\"version\":1,\"type\":\"pong\",\"device\":\"esp8266\",\"ip\":\"192.168.1.42\"}",
                out var deviceHost) || deviceHost != "192.168.1.42" ||
            !SerialPublisher.TryParsePong(
                "@AIBOT {\"version\":1,\"type\":\"pong\",\"device\":\"esp8266\"}", out var legacyHost) ||
            legacyHost is not null ||
            !SerialPublisher.TryParsePong(
                "@AIBOT {\"version\":1,\"type\":\"pong\",\"device\":\"esp8266\",\"ip\":\"8.8.8.8\"}", out var publicHost) ||
            publicHost is not null ||
            SerialPublisher.TryParsePong(
                "@AIBOT {\"version\":2,\"type\":\"pong\",\"device\":\"esp8266\",\"ip\":\"10.0.0.8\"}", out _) ||
            SerialPublisher.TryParsePong(
                "@AIBOT {\"version\":1,\"type\":\"pong\",\"device\":\"other\",\"ip\":\"10.0.0.8\"}", out _))
            throw new InvalidOperationException("Serial pong parsing did not enforce protocol and private-address rules.");

        var resource = Enumerable.Range(0, 2000).Select(index => (byte)(index * 31)).ToArray();
        var chunks = BinaryResourceProtocol.CreateChunks(BinaryResourceKind.PetAsset, resource, 0x12345678);
        var restored = chunks.SelectMany(chunk => BinaryResourceProtocol.DecodeWire(chunk.WireBytes).Payload).ToArray();
        if (!restored.SequenceEqual(resource) || chunks.Count != 3 ||
            BinaryResourceProtocol.Crc32("123456789"u8) != 0xCBF43926)
            throw new InvalidOperationException("COBS/CRC resource framing did not round-trip.");

        var textBitmap = NowPlayingService.RenderTextBitmap("中文 Song", "Artist 歌手");
        if (textBitmap.Length != 232 * 44 * 2 || textBitmap.All(value => value == 0))
            throw new InvalidOperationException("Music text bitmap did not render expected RGB565 pixels.");

        using var pixel = new Bitmap(1, 1);
        pixel.SetPixel(0, 0, Color.Red);
        var rgb565 = PetAssetImporter.EncodeRgb565(pixel);
        if (rgb565.Length != 2 || rgb565[0] != 0 || rgb565[1] != 0xF8)
            throw new InvalidOperationException("RGB565 pet encoding did not preserve channel order.");

        var weatherText = LocalizedTextResources.RenderLines(232, 24, ["北京  多云"], 20, 24);
        var stockText = LocalizedTextResources.RenderLines(120, 400, ["上证指数", "腾讯控股"], 18, 20);
        if (weatherText.Length != 232 * 24 * 2 || stockText.Length != 120 * 400 * 2 ||
            weatherText.All(value => value == 0) || stockText.All(value => value == 0))
            throw new InvalidOperationException("Localized weather/stock bitmaps were not rendered.");

        Console.WriteLine("DATA_SOURCE_SELF_TEST_OK");
    }
}
