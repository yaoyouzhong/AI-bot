namespace AIBotBridge;

internal static class MirrorSelfTest
{
    internal static string Run(string outputPath)
    {
        var now = DateTimeOffset.Now;
        var provider = new ProviderQuotaSnapshot("claude", "MAX", 34.5, now.AddHours(2),
            61.2, now.AddDays(3), null, Array.Empty<long>(), now, false);
        var domestic = new DomesticProviderQuotaSnapshot("kimi", "Ultra", 20, now.AddHours(3),
            42, now.AddDays(4), null, null, null, now, false);
        var status = SessionActivityReader.Capture() with
        {
            Codex = new ToolState("working", 2),
            Claude = new ToolState("idle", 120),
            Weather = new WeatherSnapshot("北京", "多云", 26, 31, 19, 58, 2, 13.2, 46,
                "Open-Meteo", now, false),
            Stocks = new StockSnapshot(new[]
            {
                new StockQuote("sh000001", "000001", "上证指数", "3821.44", "+0.85%", 1),
                new StockQuote("hk00700", "00700", "腾讯控股", "612.50", "-1.20%", -1),
                new StockQuote("usAAPL", "AAPL", "Apple", "238.10", "+0.14%", 1),
                new StockQuote("sz300750", "300750", "宁德时代", "321.20", "0.00%", 0)
            }, now, false),
            Quotas = new QuotaSnapshot(provider, provider with
            {
                Provider = "codex", Plan = "PLUS", PrimaryPercent = 18.5,
                WeeklyPercent = 42, ResetCreditsAvailable = 2,
                ResetCreditExpiresAt = [now.AddDays(3).ToUnixTimeSeconds(), now.AddDays(10).ToUnixTimeSeconds()]
            }),
            DomesticQuotas = new DomesticQuotaSnapshot(
                domestic with { Provider = "alibaba", Plan = "Token Plan", WeeklyPercent = 25 },
                domestic,
                domestic with { Provider = "minimax", Plan = "Coding Plan", WeeklyPercent = 35 },
                domestic with { Provider = "deepseek", Plan = "API PAYG", PrimaryPercent = null,
                    WeeklyPercent = null, Balance = 28.5, UsedCost = 9.75, Currency = "CNY" }),
            SystemMetrics = new SystemMetricsSnapshot(31.4, 72.8, 238_900, 4_821_100, now),
            Music = new MusicSnapshot("夜空中最亮的星", "逃跑计划", "世界", true, 95, 260, now)
        };

        var modes = new[] { "dual", "weather", "stocks", "quotas", "domestic", "system", "music", "pet", "screensaver" };
        using var montage = new Bitmap(720, 720);
        using (var graphics = Graphics.FromImage(montage))
        {
            graphics.Clear(Color.FromArgb(28, 28, 28));
            for (var index = 0; index < modes.Length; index++)
            {
                using var page = MirrorForm.RenderSnapshot(status, modes[index]);
                graphics.DrawImageUnscaled(page, (index % 3) * 240, (index / 3) * 240);
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        montage.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        using var creditPages = new Bitmap(720, 480);
        using (var g = Graphics.FromImage(creditPages))
        {
            var expirations = Enumerable.Range(0, 5).Select(index => now.AddDays(index + 1).ToUnixTimeSeconds()).ToArray();
            for (var page = 0; page < 3; page++)
            {
                var sample = status with
                {
                    EpochUtc = status.EpochUtc / 12 * 12 + page * 4,
                    Quotas = new QuotaSnapshot(provider, status.Quotas!.Codex! with
                    { ResetCreditsAvailable = 5, ResetCreditExpiresAt = expirations })
                };
                using var quota = MirrorForm.RenderSnapshot(sample, "quotas");
                using var pet = MirrorForm.RenderSnapshot(sample, "pet");
                g.DrawImageUnscaled(quota, page * 240, 0);
                g.DrawImageUnscaled(pet, page * 240, 240);
            }
        }
        creditPages.Save(Path.Combine(Path.GetDirectoryName(outputPath) ?? ".", "credit-pages-self-test.png"),
            System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine("MIRROR_SELF_TEST_OK " + outputPath);
        return outputPath;
    }
}
