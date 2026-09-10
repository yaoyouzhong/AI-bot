using System.Drawing.Imaging;
namespace AIBotBridge;

// Documentation only: no BridgeRuntime, serial worker, HTTP listener or provider refresh.
internal static class DocCapture
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Supply an output directory.");
        AppPaths.BeginPublicSelfTest(); // Must precede any settings/cache/credential access.
        Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        if (PetAnimationStore.Shared.AllResources().Count != 0)
            throw new InvalidOperationException("Documentation profile contains imported art.");
        var now = new DateTimeOffset(2026, 9, 10, 10, 24, 0, TimeSpan.FromHours(8));
        var quota = new ProviderQuotaSnapshot("claude", "MAX", 34, now.AddHours(2), 61,
            now.AddDays(3), null, [], now, false);
        var domestic = new DomesticProviderQuotaSnapshot("kimi", "Ultra", 20, now.AddHours(3),
            42, now.AddDays(4), null, null, null, now, false);
        var status = new StatusSnapshot(1, "10:24", now.ToUnixTimeSeconds(), 28800, now,
            new("working", 2, TokensToday: 12500), new("idle", 120, TokensToday: 8000),
            Weather: new("示例城市", "多云", 26, 31, 19, 58, 2, 13, 46, "Demo", now, false),
            Stocks: new(new[] { new StockQuote("sh000001", "000001", "示例指数", "3000.00", "+0.85%", 1),
                new StockQuote("hk00700", "00700", "示例港股", "300.00", "-1.20%", -1),
                new StockQuote("usAAPL", "AAPL", "示例美股", "200.00", "+0.14%", 1),
                new StockQuote("sz300750", "300750", "示例股票", "180.00", "0.00%", 0) }, now, false),
            Quotas: new(quota, quota with { Provider="codex", Plan="PLUS", PrimaryPercent=18, WeeklyPercent=42,
                ResetCreditsAvailable=2, ResetCreditExpiresAt=[now.AddDays(3).ToUnixTimeSeconds(),now.AddDays(10).ToUnixTimeSeconds()] }),
            DomesticQuotas: new(domestic with { Provider="alibaba", Plan="Token Plan", PrimaryPercent=null, WeeklyPercent=null, PlanPercent=25, PlanResetsAt=now.AddDays(20) },
                domestic, domestic with { Provider="minimax", Plan="Coding Plan" },
                domestic with { Provider="deepseek", Plan="API PAYG", PrimaryPercent=null, WeeklyPercent=null, Balance=28.5, Currency="CNY" },
                domestic with { Provider="zhipu", Plan="GLM", PrimaryPercent=null, WeeklyPercent=null, Balance=16.8, Currency="CNY" }),
            SystemMetrics: new(31.4,72.8,238900,4821100,now,Enumerable.Range(0,224).Select(i=>new NetworkSample((long)(180000+140000*Math.Sin(i/12.0)),(long)(3000000+2400000*Math.Sin(i/23.0)))).ToArray()),
            Music: new("桌面之光 · 示例曲目", "AI-bot 演示", "示例专辑", true, 95, 260, now));
        foreach (var mode in DisplayModes.Pages.Select(x=>x.Mode).Concat(new[]{"domestic","screensaver","activity"}).Distinct())
        {
            using var image = MirrorForm.RenderSnapshot(status, mode);
            image.Save(Path.Combine(output, mode+".png"), ImageFormat.Png);
        }
        using (var image = MirrorForm.RenderSnapshot(status with { Codex=new("working",0,NeedsInput:true), CapturedAt=DateTimeOffset.FromUnixTimeMilliseconds(800) }, "codex"))
            image.Save(Path.Combine(output,"needs-input.png"));
        using (var image = MirrorForm.RenderSnapshot(status with { Codex=new("idle",0,CompletionActive:true) }, "codex"))
            image.Save(Path.Combine(output,"completed.png"));
        Capture(new SettingsForm(BridgeSettings.CreatePublicSelfTestSettings()), "settings");
        Capture(new PetGalleryForm(_=>{},load:false), "pet-gallery-empty");
        var serial=new SerialPublisher(null); Capture(new DeviceControlForm(serial,_=>{},"auto"),"device-control");
        Capture(new MigratedWeather.WeatherSettingsForm(new MigratedWeather.WeatherMonitor()),"weather-settings");
        Capture(new MigratedDomestic.DomesticQuotaAuthForm(new MigratedDomestic.DomesticQuotaService(),initializeBrowser:false),"authorization-empty");
        Capture(new CycleSettingsForm(), "cycle-settings");
        Capture(new QuotaTrendForm(), "quota-trend-empty");
        Capture(new MirrorForm(()=>status,()=>"codex"), "mirror-window");
        using var menu = TrayMenu.Build(_=>{},_=>{},()=>"auto",()=>"USB 未连接（离线示例）",()=>status.Quotas);
        menu.Show(new Point(-32000,-32000)); Application.DoEvents();
        using (var bitmap = new Bitmap(menu.Width,menu.Height))
        { menu.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(Path.Combine(output,"tray-menu.png")); }
        menu.Close();
        Console.WriteLine("DOC_CAPTURE_OK isolated profile; synthetic values; no USB, HTTP, online data or private artwork");
        void Capture(Form form, string name)
        {
            using (form)
            {
                form.WindowState=FormWindowState.Normal;form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-32000,-32000);
                form.Show();Application.DoEvents();
                using var bitmap=new Bitmap(form.Width,form.Height);
                form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));form.Hide();bitmap.Save(Path.Combine(output,name+".png"));
            }
        }
    }
}
