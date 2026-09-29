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
        long baseEpoch=status.EpochUtc;
        var brightnessSent=new List<int>();
        var previewStatus=status with {EpochUtc=baseEpoch,DisplayPolicy=new("weather",false,15,["weather","domestic_deepseek","stocks","codex"],baseEpoch)};
        int selections=0;bool allowSelection=true;
        using (var popup = new MirrorForm(()=>previewStatus,()=>selected,mode=>{if(!allowSelection)return false;selected=mode;selections++;previewStatus=previewStatus with {DisplayPolicy=previewStatus.DisplayPolicy! with {SelectedMode=mode,CycleEnabled=mode=="auto",CycleStartedAt=previewStatus.EpochUtc}};return true;},level=>{brightnessSent.Add(level);return true;},()=>Task.FromResult(new UsbDeviceInfo("AI-bot",1,"","auto",80,true,true,1,1,0))))
        {
            if(popup.FormBorderStyle!=FormBorderStyle.None || !popup.TopMost || popup.ShowInTaskbar)
                throw new InvalidOperationException("Mirror must remain a tray popup.");
            var pages=popup.Controls.OfType<Button>().Single(b=>b.Name=="preview-pages");
            var automatic=popup.Controls.OfType<Button>().Single(b=>b.Name=="preview-auto");
            var playback=popup.Controls.OfType<Label>().Single(l=>l.Name=="preview-playback");
            if(!popup.ShortcutPages.SequenceEqual(new[]{"weather","domestic_deepseek","stocks","codex"}))throw new InvalidOperationException("Preview choices lost cycle selection/order");
            popup.Location=new(-30000,-30000);popup.Show();Application.DoEvents();
            void CapturePreview(string name){Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);using var shot=new Bitmap(popup.Width,popup.Height);popup.DrawToBitmap(shot,new Rectangle(Point.Empty,popup.Size));shot.Save(Path.Combine(Path.GetDirectoryName(outputPath)!,name+".png"));}
            CapturePreview("preview-manual");
            var slider=popup.Controls.OfType<PreviewBrightness>().Single();
            if(slider.Value!=80||brightnessSent.Count!=0)throw new InvalidOperationException("Preview brightness read triggered a write");
            foreach(var (key,expected) in new[]{(Keys.Right,81),(Keys.Home,0),(Keys.End,100)}){
                typeof(PreviewBrightness).GetMethod("OnKeyDown",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(slider,[new KeyEventArgs(key)]);
                if(slider.Value!=expected||brightnessSent.Last()!=expected)throw new InvalidOperationException("Preview brightness keyboard control failed");
            }
            pages.PerformClick();Application.DoEvents();
            if(!popup.Visible||!pages.ContextMenuStrip!.Visible)throw new InvalidOperationException("Page menu hid preview");
            if(!pages.ContextMenuStrip.Items.Cast<ToolStripItem>().Select(i=>i.Tag as string).SequenceEqual(popup.ShortcutPages))throw new InvalidOperationException("Page menu order differs from cycle");
            pages.ContextMenuStrip.Items.OfType<ToolStripMenuItem>().Single(i=>i.Tag as string=="stocks").PerformClick();pages.ContextMenuStrip.Close();
            if(selected!="stocks"||selections!=1||popup.DisplayedMode!="stocks")throw new InvalidOperationException("Manual preview choice failed");
            allowSelection=false;automatic.PerformClick();
            if(automatic.Text!="未生效"||((PreviewButton)automatic).Active)throw new InvalidOperationException("Failed automatic action falsely showed success");
            allowSelection=true;
            automatic.PerformClick();popup.SyncModes();
            if(selected!="auto"||selections!=2||popup.DisplayedMode!="weather")throw new InvalidOperationException("Preview automatic action failed");
            if(automatic.Text!="已开始轮播"||!((PreviewButton)automatic).Active)throw new InvalidOperationException("Automatic action lacks visible confirmation");
            CapturePreview("preview-auto-started");
            automatic.PerformClick();
            if(selections!=3)throw new InvalidOperationException("Repeated automatic click was ignored");
            if(automatic.Text!="已重新开始")throw new InvalidOperationException("Repeated automatic click lacks feedback");
            CapturePreview("preview-auto-restarted");
            previewStatus=previewStatus with {EpochUtc=baseEpoch+14};popup.SyncModes();
            if(popup.DisplayedMode!="weather"||!playback.Text.Contains("1 秒后"))throw new InvalidOperationException("Preview cycle countdown wrong");
            previewStatus=previewStatus with {EpochUtc=baseEpoch+15};popup.SyncModes();
            if(popup.DisplayedMode!="domestic_deepseek"||!playback.Text.Contains("2/4")||pages.Text!="DeepSeek")throw new InvalidOperationException("Automatic preview did not advance to the next page");
            previewStatus=previewStatus with {Codex=previewStatus.Codex with {NeedsInput=true}};popup.SyncModes();
            if(!playback.Text.StartsWith("等待确认")||popup.DisplayedMode!="codex")throw new InvalidOperationException("Alert interruption hidden from user");
            previewStatus=previewStatus with {Codex=previewStatus.Codex with {NeedsInput=false},DisplayPolicy=new("auto",true,30,["codex","weather"],baseEpoch)};popup.SyncModes();
            if(!popup.ShortcutPages.SequenceEqual(new[]{"codex","weather"})||selections!=3)throw new InvalidOperationException("Live preview refresh changed mode or retained removed pages");
            popup.Controls.OfType<Button>().Single(b=>b.AccessibleName=="下一页").PerformClick();popup.SyncModes();
            if(selected!="weather")throw new InvalidOperationException("Next page skipped configured order");
            popup.Controls.OfType<Button>().Single(b=>b.AccessibleName=="上一页").PerformClick();popup.SyncModes();
            if(selected!="codex")throw new InvalidOperationException("Previous page skipped configured order");
            var compactSize=popup.Size;
            previewStatus=previewStatus with {DisplayPolicy=new("auto",true,15,DisplayModes.Pages.Select(p=>p.Mode).Reverse().ToArray(),baseEpoch)};popup.SyncModes();
            if(!popup.ShortcutPages.SequenceEqual(previewStatus.DisplayPolicy.Pages)||popup.Size!=compactSize||popup.Controls.OfType<RadioButton>().Any())throw new InvalidOperationException("Many preview pages enlarged or stacked controls");
            foreach(var control in popup.Controls.Cast<Control>())if(!popup.ClientRectangle.Contains(control.Bounds))throw new InvalidOperationException("Preview controls clipped");
            using var bitmap=new Bitmap(popup.Width,popup.Height);
            popup.DrawToBitmap(bitmap,new Rectangle(Point.Empty,popup.Size));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            bitmap.Save(Path.Combine(Path.GetDirectoryName(outputPath)!,"mirror-popup-self-test.png"));popup.Hide();
        }
        Console.WriteLine("MIRROR_INTERACTION_SELF_TEST_OK compact selector, repeated auto click, countdown, timed advance, alert reason, live page list");
        if(SystemPageRenderer.Scale(0)!=10240 || SystemPageRenderer.Scale(70000)!=80000)
            throw new InvalidOperationException("System graph must preserve the legacy floor and 8/7 headroom.");
        if (System.Text.Json.JsonSerializer.Serialize(status, JsonDefaults.Options).Contains("coverRgb565", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Music pixels leaked into status JSON.");

        var stress = status with
        {
            Weather = status.Weather! with {
                Hourly=Enumerable.Range(0,24).Select(i=>new WeatherHour(status.CapturedAt.AddHours(i).ToString("O"),"雷阵雨",28,90)).ToArray(),
                Daily=Enumerable.Range(0,7).Select(i=>new WeatherDay(status.CapturedAt.AddDays(i).ToString("yyyy-MM-dd"),"多云转雷阵雨",20,30,90,10)).ToArray(),
                HourlyStale=true,DailyStale=true
            },
            Stocks = status.Stocks! with { Quotes = Enumerable.Range(0, 20).Select(i => new StockQuote("sh000001", "000001", new string('股', 100), "1234567890.12", "+100.00%", 1)).ToArray() },
            Music = status.Music! with { Title = new string('音', 200), Artist = new string('人', 200) },
            DisplayPolicy = new("auto", true, 15, DisplayModes.Pages.Select(p => p.Mode).ToArray())
        };
        stress = stress with { DomesticQuotas = stress.DomesticQuotas! with {
            Xiaomi = XiaomiQuota.Snapshot(25, status.CapturedAt.UtcDateTime),
            StepFun = new("stepfun", "API balance (not Step Plan)", null, null, null, null, 75.37, null, "CNY", status.CapturedAt, false),
            Baidu = new("baidu", "ernie-4.0-8k", null, null, null, null, null, null, null, status.CapturedAt, false) { PlanPercent = 25, PlanResetsAt = status.CapturedAt.AddDays(30) }
        }};
        var wire = DeviceStatusFrame.Create(stress);
        if(wire["data"]?["weather"]?["hourly"] is not null||wire["data"]?["weather"]?["daily"] is not null||
            wire["data"]?["weather"]?["temperature"]?.GetValue<double>()!=stress.Weather!.Temperature)
            throw new InvalidOperationException("ESP weather heartbeat lost current conditions or retained unsupported forecasts.");
        var full=System.Text.Json.JsonSerializer.SerializeToNode(stress,JsonDefaults.Options)!;
        if(full["weather"]?["hourly"]?.AsArray().Count!=24||full["weather"]?["daily"]?.AsArray().Count!=7)
            throw new InvalidOperationException("Compact USB weather mutated the shared TAB5/LAN forecast.");
        if(wire["data"]?["domesticQuotas"]?["xiaomi"]?["planPercent"]?.GetValue<double>()!=25) throw new InvalidOperationException("MiMo wire data missing.");
        if(wire["data"]?["domesticQuotas"]?["stepFun"]?["balance"]?.GetValue<double>() != 75.37 || wire["data"]?["domesticQuotas"]?["baidu"]?["planPercent"]?.GetValue<double>() != 25)
            throw new InvalidOperationException("Additional provider fields lost in device frame.");
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
                position.Y < 12 || position.Y + ScreenSaverRenderer.GroupHeight > 216)
                throw new InvalidOperationException("Screensaver lunar row clips the safe area.");
        }
        // Reserve the same bottom lane as firmware for the lunar row and PC OFF.
        if(ScreenSaverRenderer.Position(66*5).Y!=78)throw new InvalidOperationException("Screensaver lunar layout travel changed.");
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
