namespace AIBotBridge;

internal static class MirrorSelfTest
{
    internal static string Run(string outputPath)
    {
        var now = DateTimeOffset.Now;
        foreach(var alias in new[]{"pro"," PRO ","Pro"})
            if(PlanDisplay.Normalize(alias)!="PRO")throw new InvalidOperationException("PRO alias lost its legacy badge.");
        if(PlanDisplay.Normalize("pro_lite")!="PRO LITE"||PlanDisplay.Normalize("claude-max-5x")!="MAX 5X")throw new InvalidOperationException("Legacy plan aliases changed.");
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
                domestic with { Provider = "alibaba", Plan = "Token Plan", PrimaryPercent=null,WeeklyPercent=null,PlanPercent = 25,PlanResetsAt=now.AddDays(20) },
                domestic,
                domestic with { Provider = "minimax", Plan = "Coding Plan", WeeklyPercent = 35 },
                domestic with { Provider = "deepseek", Plan = "API PAYG", PrimaryPercent = null,
                    WeeklyPercent = null, Balance = 28.5, UsedCost = 9.75, Currency = "CNY" }),
            SystemMetrics = new SystemMetricsSnapshot(31.4, 72.8, 238_900, 4_821_100, now,
                Enumerable.Range(0, 224).Select(i => new NetworkSample((long)(180000 + 140000 * Math.Sin(i / 12.0)), (long)(3000000 + 2400000 * Math.Sin(i / 23.0)))).ToArray()),
            Music = new MusicSnapshot("夜空中最亮的星", "逃跑计划", "世界", true, 95, 260, now)
        };

        var coverPixels = new byte[PetAnimation.FrameBytes];
        foreach(var role in new[]{"claude","codex"})
        {
            var header=$"#define {role.ToUpperInvariant()}_LOGO_W 40\n#define {role.ToUpperInvariant()}_LOGO_H 40\nconst uint16_t {role}_logo_0[1600] PROGMEM = {{"+string.Join(",",Enumerable.Repeat("0x00F8",1600))+"};";
            var pixels=LocalPageLogos.ParseHeader(header,role);
            if(pixels.Length!=3200||pixels.Where((value,index)=>value!=(index%2==0?0:248)).Any())throw new InvalidOperationException("Logo RGB565 red byte order changed.");
            var kind=role=="claude"?BinaryResourceKind.ClaudeLogo:BinaryResourceKind.CodexLogo;
            var chunks=BinaryResourceProtocol.CreateChunks(kind,pixels,123).Select(c=>BinaryResourceProtocol.DecodeWire(c.WireBytes)).ToArray();
            if(chunks.Any(c=>c.Kind!=kind||c.TotalLength!=3200)||!chunks.SelectMany(c=>c.Payload).SequenceEqual(pixels))throw new InvalidOperationException("Logo resource roundtrip failed.");
            bool rejected=false;
            try{LocalPageLogos.ParseHeader(header.Replace("_LOGO_W 40","_LOGO_W 39"),role);}catch(InvalidDataException){rejected=true;}
            if(!rejected)throw new InvalidOperationException("Incorrect logo dimensions accepted.");
        }
        if(UsagePageRenderer.CreditBounds(1)!=new RectangleF(153,29,68,18)||UsagePageRenderer.CreditBounds(2)!=new RectangleF(153,19.5f,68,37)||UsagePageRenderer.CreditBounds(5)!=new RectangleF(153,15,68,94))throw new InvalidOperationException("Legacy credit badge bounds changed.");
        var creditSample=status.Quotas!.Codex! with {ResetCreditsAvailable=5,ResetCreditExpiresAt=[now.AddDays(1).ToUnixTimeSeconds(),now.AddDays(1).ToUnixTimeSeconds()]};
        var creditRows=UsagePageRenderer.SingleCreditRows(status,creditSample);
        if(creditRows.Length!=2||creditRows.Any(r=>r.Count!=1)||creditRows[0].Date!=creditRows[1].Date)throw new InvalidOperationException("Single quota must retain duplicate per-credit rows without inventing missing details.");
        if(UsagePageRenderer.SingleCreditRows(status,creditSample with {ResetCreditExpiresAt=[]}) is not [{Count:5,Date:""}])throw new InvalidOperationException("Unknown expiry aggregate changed.");
        Console.WriteLine("LOCAL_LOGO_AND_SINGLE_QUOTA_SELF_TEST_OK");
        using(var reference=new Bitmap(240,240))
        {
            reference.SetResolution(96,96);
            using(var g=Graphics.FromImage(reference)) UsagePageRenderer.Draw(g,status,"dual");
            foreach(var dpi in new[]{120,144,168,192})
            {
                using var candidate=new Bitmap(240,240);candidate.SetResolution(dpi,dpi);
                using(var g=Graphics.FromImage(candidate)) UsagePageRenderer.Draw(g,status,"dual");
                for(int y=0;y<240;y++)for(int x=0;x<240;x++)
                    if(candidate.GetPixel(x,y)!=reference.GetPixel(x,y))throw new InvalidOperationException($"Dual layout changes at {dpi} DPI.");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
            reference.Save(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outputPath))!,"dual-dpi-verified.png"));
        }
        Console.WriteLine("DUAL_LAYOUT_DPI_OK 96/120/144/168/192");
        using(var cases=new Bitmap(720,240))
        using(var g=Graphics.FromImage(cases))
        {
            var activity=status with {Codex=status.Codex with {TokensToday=uint.MaxValue},Claude=status.Claude with {TokensToday=uint.MaxValue}};
            using var activityPage=MirrorForm.RenderSnapshot(activity,"activity");
            g.DrawImageUnscaled(activityPage,0,0);
            foreach(int value in new[]{100,9}) {
                using var page=MirrorForm.RenderSnapshot(status with {SystemMetrics=status.SystemMetrics! with {CpuPercent=value,MemoryPercent=value}},"system");
                g.DrawImageUnscaled(page,value==100?240:480,0);
            }
            cases.Save(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outputPath))!,"activity-system-bounds.png"));
            for(int x=0;x<240;x++)for(int y=237;y<240;y++)
                if(activityPage.GetPixel(x,y).ToArgb()!=Color.Black.ToArgb())throw new InvalidOperationException("Activity footer touches bottom edge.");
        }
        foreach(var mode in new[]{"codex","dual"})
        {
            var sample=status with {Codex=new("idle",0),Quotas=new(status.Quotas!.Claude,status.Quotas.Codex! with {Plan="pro"})};
            using var lower=MirrorForm.RenderSnapshot(sample,mode);
            using var upper=MirrorForm.RenderSnapshot(sample with {Quotas=new(sample.Quotas.Claude,sample.Quotas.Codex! with {Plan="PRO"})},mode);
            int gold=0;
            for(int y=0;y<240;y++)for(int x=0;x<240;x++) {
                if(lower.GetPixel(x,y)!=upper.GetPixel(x,y))throw new InvalidOperationException("Lowercase PRO changes badge pixels.");
                if(lower.GetPixel(x,y).ToArgb()==Color.FromArgb(255,159,10).ToArgb())gold++;
            }
            if(gold==0)throw new InvalidOperationException("PRO badge is not legacy gold.");
        }
        for (int i = 1; i < coverPixels.Length; i += 2) coverPixels[i] = 0xF8;
        status = status with { Music = status.Music! with { CoverRgb565 = coverPixels } };
        using (var musicPage = MirrorForm.RenderSnapshot(status, "music"))
            if (musicPage.GetPixel(80, 50).ToArgb() != Color.Red.ToArgb())
                throw new InvalidOperationException("Music mirror did not render the captured cover.");
        using (var emptyMusic = MirrorForm.RenderSnapshot(status with { Music = null }, "music"))
            if (emptyMusic.GetPixel(60, 20).ToArgb() != Color.FromArgb(64,64,64).ToArgb())
                throw new InvalidOperationException("Missing music must retain the legacy No Art placeholder.");
        var alert = status with { Codex = new ToolState("working",0,NeedsInput:true), CapturedAt = DateTimeOffset.FromUnixTimeMilliseconds(800) };
        using (var on = MirrorForm.RenderSnapshot(alert,"codex"))
        using (var off = MirrorForm.RenderSnapshot(alert with { CapturedAt = DateTimeOffset.FromUnixTimeMilliseconds(1200) },"codex"))
        {
            if(on.GetPixel(5,120).ToArgb()!=Color.FromArgb(255,59,48).ToArgb() || off.GetPixel(5,120).ToArgb()==Color.FromArgb(255,59,48).ToArgb())
                throw new InvalidOperationException("Input alert must alternate red and restored quota border every 400 ms.");
        }
        var selected = "weather";
        using (var popup = new MirrorForm(()=>status,()=>selected,mode=>selected=mode))
        {
            if(popup.FormBorderStyle!=FormBorderStyle.None || !popup.TopMost || popup.ShowInTaskbar)
                throw new InvalidOperationException("Mirror must remain a tray popup.");
            var buttons=popup.Controls.OfType<RadioButton>().ToArray();
            if(buttons.Length!=8 || popup.Controls.OfType<TrackBar>().Single().Maximum!=100)
                throw new InvalidOperationException("Legacy mirror modes or brightness control missing.");
            buttons.Single(b=>b.Tag as string=="stocks").Checked=true;
            if(selected!="stocks") throw new InvalidOperationException("Mirror segmented mode callback failed.");
            popup.Location=new Point(-30000,-30000);popup.Show();Application.DoEvents();
            using var bitmap=new Bitmap(popup.Width,popup.Height);
            popup.DrawToBitmap(bitmap,new Rectangle(Point.Empty,popup.Size));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            bitmap.Save(Path.Combine(Path.GetDirectoryName(outputPath)!,"mirror-popup-self-test.png"));
            popup.Hide();
        }
        Console.WriteLine("MIRROR_INTERACTION_SELF_TEST_OK popup/modes/brightness/no-art/400ms-red-alert");
        if(SystemPageRenderer.Scale(0)!=10240 || SystemPageRenderer.Scale(70000)!=80000)
            throw new InvalidOperationException("System graph must preserve the legacy floor and 8/7 headroom.");
        if (System.Text.Json.JsonSerializer.Serialize(status, JsonDefaults.Options).Contains("coverRgb565", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Music pixels leaked into status JSON.");

        var stress = status with
        {
            Stocks = status.Stocks! with { Quotes = Enumerable.Range(0, 20).Select(i => new StockQuote("sh000001", "000001", new string('股', 100), "1234567890.12", "+100.00%", 1)).ToArray() },
            Music = status.Music! with { Title = new string('音', 200), Artist = new string('人', 200) },
            DisplayPolicy = new("auto", true, 15, DisplayModes.Pages.Select(p => p.Mode).ToArray())
        };
        var wire = DeviceStatusFrame.Create(stress);
        if(wire["data"]?["domesticQuotas"]?["alibaba"]?["planPercent"]?.GetValue<double>()!=25||wire["data"]?["domesticQuotas"]?["alibaba"]?["weeklyPercent"] is not null)throw new InvalidOperationException("Device frame conflated plan and weekly quota.");
        if (wire["data"]?["stocks"]?["quotes"]?.AsArray().Count != 20 || wire["data"]?["quotas"]?["codex"]?["resetCreditsAvailable"]?.GetValue<int>() != 2)
            throw new InvalidOperationException("Compact wire frame dropped rendered data.");
        Console.WriteLine("DEVICE_FRAME_SELF_TEST_OK bytes=" + System.Text.Encoding.UTF8.GetByteCount("@AIBOT " + wire.ToJsonString(JsonDefaults.Options)));

        var modes = DisplayModes.Pages.Select(page => page.Mode).Append("screensaver").Append("activity").ToArray();
        using var montage = new Bitmap(720, ((modes.Length + 2) / 3) * 240);
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
        using(var weatherCases=new Bitmap(720,240))
        using(var weatherGraphics=Graphics.FromImage(weatherCases)) {
            var epoch=new DateTimeOffset(2026,9,9,23,59,58,TimeSpan.Zero).ToUnixTimeSeconds();
            var sample=status with {EpochUtc=epoch,Weather=status.Weather! with {City="南京 雨花台",AirQualityLabel="优",UtcOffsetSeconds=28800,Animation="pet"}};
            if(WeatherMirrorRenderer.LocalTime(sample).Day!=10||WeatherMirrorRenderer.LocalTime(sample).Hour!=7)throw new InvalidOperationException("Weather city timezone was ignored at midnight.");
            using var baseline=MirrorForm.RenderSnapshot(sample,"weather");
            using var off=MirrorForm.RenderSnapshot(sample with {Weather=sample.Weather! with {Animation="off"}},"weather");
            int yellow=0;
            for(int y=0;y<240;y++)for(int x=0;x<240;x++) {
                if(baseline.GetPixel(x,y)!=off.GetPixel(x,y))throw new InvalidOperationException("Device animation leaked into desktop legacy layout.");
                if(x>=101&&x<185&&y>=54&&y<110&&baseline.GetPixel(x,y).ToArgb()==Color.FromArgb(255,204,0).ToArgb())yellow++;
            }
            if(yellow<20)throw new InvalidOperationException("Weather minutes lost legacy yellow typography.");
            weatherGraphics.DrawImageUnscaled(baseline,0,0);
            using var longCity=MirrorForm.RenderSnapshot(sample with {Weather=sample.Weather! with {City="呼和浩特 新城区",AirQualityLabel="轻度",Condition="小雨",Temperature=-12.5}},"weather");
            weatherGraphics.DrawImageUnscaled(longCity,240,0);
            using var missing=MirrorForm.RenderSnapshot(sample with {Weather=null},"weather");weatherGraphics.DrawImageUnscaled(missing,480,0);
            weatherCases.Save(Path.Combine(Path.GetDirectoryName(outputPath)??".","weather-mirror-self-test.png"),System.Drawing.Imaging.ImageFormat.Png);
        }
        Console.WriteLine("WEATHER_MIRROR_SELF_TEST_OK timezone typography animation-isolation");
        using(var balances=new Bitmap(960,240))
        using(var balanceGraphics=Graphics.FromImage(balances)) {
            for(int index=0;index<4;index++) {
                var values=new[]{0.0,28.5,123456.78,99999999.99};
                var sample=status with {DomesticQuotas=status.DomesticQuotas! with {DeepSeek=status.DomesticQuotas!.DeepSeek! with {Balance=values[index]}}};
                using var page=MirrorForm.RenderSnapshot(sample,"domestic_deepseek");
                // The content must stay inside its 200px lane, away from the ring.
                for(int y=62;y<168;y++)for(int x=14;x<20;x++)if(page.GetPixel(x,y).ToArgb()!=Color.Black.ToArgb()||page.GetPixel(239-x,y).ToArgb()!=Color.Black.ToArgb())throw new InvalidOperationException("Balance content escaped its lane.");
                balanceGraphics.DrawImageUnscaled(page,index*240,0);
            }
            balances.Save(Path.Combine(Path.GetDirectoryName(outputPath)??".","domestic-balance-self-test.png"),System.Drawing.Imaging.ImageFormat.Png);
        }
        using(var overviewCases=new Bitmap(960,240))
        using(var overviewGraphics=Graphics.FromImage(overviewCases))
        {
            for(int index=0;index<4;index++) {
                var codex=status.Quotas!.Codex! with {PrimaryPercent=index==0?18.5:null,WeeklyPercent=index==2?null:index==3?100:80,Plan=index==1?"ENTERPRISE":"PLUS"};
                var sample=status with {Quotas=new(provider,codex)};
                using var page=MirrorForm.RenderSnapshot(sample,"dual");
                var track=page.GetPixel(210,82);
                if(track.ToArgb()!=Color.FromArgb(42,42,42).ToArgb())throw new InvalidOperationException("Dual quota track background differs from legacy.");
                int barY=index==0?215:193;
                var expected=index==2?Color.FromArgb(42,42,42):index==3?Color.Red:Color.FromArgb(255,204,0);
                if(page.GetPixel(30,barY).ToArgb()!=expected.ToArgb())throw new InvalidOperationException("Dual quota collapsed/unknown/threshold rendering failed.");
                if(index>0&&page.GetPixel(30,215).ToArgb()!=Color.Black.ToArgb())throw new InvalidOperationException("Collapsed Codex left a second quota bar.");
                overviewGraphics.DrawImageUnscaled(page,index*240,0);
            }
            overviewCases.Save(Path.Combine(Path.GetDirectoryName(outputPath)??".","dual-layout-self-test.png"),System.Drawing.Imaging.ImageFormat.Png);
        }
        Console.WriteLine("DUAL_LAYOUT_SELF_TEST_OK full collapsed unknown exhausted");
        montage.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        using var screenSaver = MirrorForm.RenderSnapshot(status, "screensaver");
        screenSaver.Save(Path.Combine(Path.GetDirectoryName(outputPath) ?? ".", "screensaver-self-test.png"),
            System.Drawing.Imaging.ImageFormat.Png);
        for (long tick = 0; tick < 1000; tick++)
        {
            var position = ScreenSaverRenderer.Position(tick * 5);
            if (position.X < 6 || position.X + ScreenSaverRenderer.ClockWidth > 234 ||
                position.Y < 12 || position.Y + 112 > 228)
                throw new InvalidOperationException("Desktop screensaver clips the legacy 12px bottom margin.");
        }
        // The desktop legacy renderer has no PC OFF lane; firmware keeps its separate safe area.
        if(ScreenSaverRenderer.Position(104*5).Y!=116)throw new InvalidOperationException("Desktop screensaver lost the legacy vertical travel.");
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
