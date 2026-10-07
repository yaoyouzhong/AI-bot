using System.Drawing.Imaging;
namespace AIBotBridge;

// Documentation only: no BridgeRuntime, serial worker, HTTP listener or provider refresh.
internal static class DocCapture
{
    [STAThread]
    private static void Main(string[] args)
    {
        string? designPet = null;
        PetAnimation? designAnimation = null;
        int designIndex = Array.IndexOf(args, "--design-pet");
        if (designIndex >= 0)
        {
            if (designIndex + 1 >= args.Length) throw new ArgumentException("--design-pet requires an approved GIF path.");
            designPet = Path.GetFullPath(args[designIndex + 1]);
            args = args.Take(designIndex).Concat(args.Skip(designIndex + 2)).ToArray();
        }
        bool release060 = args.Length == 2 && args[1] == "--release060";
        bool releaseUi = args.Length == 2 && args[1] == "--release-ui";
        bool screenSaverOnly = args.Length == 2 && args[1] == "--screensaver";
        bool quotaOnly = args.Length == 2 && args[1] == "--quota-api";
        bool codexCover = args.Length == 2 && args[1] == "--codex-pro-cover";
        if (args.Length is not (1 or 3) && !screenSaverOnly && !quotaOnly && !releaseUi && !codexCover && !release060) throw new ArgumentException("Supply an output directory, optionally --screensaver, --codex-pro-cover or Claude and Codex APET paths for approved quota screenshots.");
        AppPaths.BeginPublicSelfTest(); // Must precede any settings/cache/credential access.
        Application.SetHighDpiMode(Environment.GetEnvironmentVariable("AIBOT_DOC_NATIVE_DPI") == "1" ? HighDpiMode.PerMonitorV2 : HighDpiMode.DpiUnaware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        if(release060){ReleaseMedia.Run(output);return;}
        if (PetAnimationStore.Shared.AllResources().Count != 0)
            throw new InvalidOperationException("Documentation profile contains imported art.");
        if (args.Length == 3)
        {
            PetAnimationStore.Shared.Select("claude", PetAnimation.Decode(File.ReadAllBytes(args[1])));
            PetAnimationStore.Shared.Select("codex", PetAnimation.Decode(File.ReadAllBytes(args[2])));
        }
        if (designPet is not null)
        {
            if (!PetAnimation.TryImport(designPet, out var animation, out _, out var error))
                throw new InvalidDataException(error);
            designAnimation = animation;
            PetAnimationStore.Shared.Select("claude", animation!);
            PetAnimationStore.Shared.Select("codex", animation!);
            Console.WriteLine("DOC_DESIGN_PREVIEW selected artwork in isolated profile; shipped defaults unchanged");
        }
        var now = new DateTimeOffset(2026, 10, 7, 10, 24, 0, TimeSpan.FromHours(8));
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
        if (designPet is not null)
        {
            // Use the approved project character as fictional album artwork.
            using var original = new Bitmap(designPet);
            using var cover = new Bitmap(112, 112);
            using (var graphics = Graphics.FromImage(cover))
            {
                graphics.Clear(Color.FromArgb(31, 37, 42));
                graphics.DrawImage(original, 0, 0, 112, 112);
            }
            status = status with { Music = status.Music! with { CoverRgb565 = PetAssetImporter.EncodeRgb565(cover) } };
        }
        if (codexCover)
        {
            // Match the adjacent native TAB5 Codex quota preview, using synthetic data only.
            var codex = status.Quotas!.Codex! with { Plan="PRO", WeeklyPercent=88, ResetCreditsAvailable=4,
                ResetCreditExpiresAt=new[]{
                    new DateTimeOffset(2026,10,4,11,26,0,TimeSpan.FromHours(8)).ToUnixTimeSeconds(),
                    new DateTimeOffset(2026,10,5,13,38,0,TimeSpan.FromHours(8)).ToUnixTimeSeconds(),
                    new DateTimeOffset(2026,10,23,0,27,0,TimeSpan.FromHours(8)).ToUnixTimeSeconds(),
                    new DateTimeOffset(2026,11,4,11,26,0,TimeSpan.FromHours(8)).ToUnixTimeSeconds() } };
            var coverStatus = status with { Quotas=status.Quotas with { Codex=codex } };
            using var image = MirrorForm.RenderSnapshot(coverStatus, "codex");
            image.Save(Path.Combine(output,"codex-pro-cover.png"),ImageFormat.Png);
            SaveDesignFrames(coverStatus, "codex", "codex-pro-cover");
            Console.WriteLine("DOC_CODEX_COVER_OK native Codex page; synthetic PRO/weekly 88%; isolated profile");
            return;
        }
        if (releaseUi)
        {
            var store = new DeviceRegistryStore(Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData), "doc-devices.json"));
            store.Migrate(null, false, false);
            var tab = DeviceRegistryStore.Create(HardwareKind.Tab5, "M5Stack TAB5", "001122334455");
            var esp = DeviceRegistryStore.Create(HardwareKind.Esp8266, "ESP8266 小屏", null);
            store.Add(tab); store.Add(esp);
            using (var center = new DeviceCenterForm(store, d => new DeviceView(true, "在线（演示）", "演示连接", d.Kind == HardwareKind.Tab5 ? "0.2.145-ui" : "0.5.0", "数据已确认（演示）", "待机（演示）", d.Kind == HardwareKind.Tab5 ? "已连接（演示）" : null, "USB"), (_, _) => {}, (_, _) => Task.CompletedTask, () => {}, _ => {}))
            {
                center.ShowInTaskbar=false;center.StartPosition=FormStartPosition.Manual;center.Location=new(-32000,-32000);
                center.Show();Application.DoEvents();
                Save(center, "device-center");
                center.SelectDevice(esp.Id);Application.DoEvents();Save(center, "device-center-esp8266");
                center.ShowPage("accounts");Application.DoEvents();Save(center, "device-center-accounts");
                center.ShowPage("bridge-settings");Application.DoEvents();Save(center, "device-center-bridge");
            }
            Capture(new AddDeviceForm(new DeviceRegistry(1,true,false,false,[]),preview:true), "add-device");
            Capture(new FirmwareFlashForm(preview:true), "firmware-flasher");
            using var service = new Tab5Service();
            Capture(new Tab5InstallForm(service,preview:true), "tab5-first-install");
            using(var connection = new Tab5ConnectionForm(service)) {
                connection.ShowInTaskbar=false;connection.StartPosition=FormStartPosition.Manual;connection.Location=new(-32000,-32000);
                connection.Show();Application.DoEvents();Save(connection,"tab5-connection");
                var tabs=connection.Controls.OfType<TableLayoutPanel>().SelectMany(p=>p.Controls.OfType<TabControl>()).Single();
                tabs.SelectedIndex=1;Application.DoEvents();Save(connection,"tab5-usb-pairing");
                tabs.SelectedIndex=2;Application.DoEvents();Save(connection,"tab5-upgrade");
            }
            Console.WriteLine("DOC_RELEASE_UI_OK current native Forms; isolated synthetic data; no device write");
            return;
        }
        if (quotaOnly)
        {
            foreach (var provider in new[]{"deepseek","minimax","kimi","qwen","zhipu","stepfun","baidu","xiaomi"})
                Capture(new MigratedDomestic.DomesticQuotaAuthForm(new MigratedDomestic.DomesticQuotaService(),initialProviderId:provider,initializeBrowser:false), "api-settings-"+provider);
            using var fresh=MirrorForm.RenderSnapshot(status,"domestic_deepseek");
            fresh.Save(Path.Combine(output,"deepseek-fresh.png"));
            using var stale=MirrorForm.RenderSnapshot(status with {DomesticQuotas=status.DomesticQuotas! with {DeepSeek=status.DomesticQuotas.DeepSeek! with {Stale=true}}},"domestic_deepseek");
            stale.Save(Path.Combine(output,"deepseek-stale.png"));
            Console.WriteLine("DOC_QUOTA_API_CAPTURE_OK isolated profile; no credentials or network");
            return;
        }
        if (screenSaverOnly)
        {
            using var image = MirrorForm.RenderSnapshot(status, "screensaver");
            image.Save(Path.Combine(output, "screensaver.png"), ImageFormat.Png);
            Console.WriteLine("DOC_SCREENSAVER_CAPTURE_OK isolated profile; synthetic date");
            return;
        }
        if (args.Length == 3)
        {
            foreach (var mode in new[] { "claude", "codex" })
            {
                using var image = MirrorForm.RenderSnapshot(status, mode);
                image.Save(Path.Combine(output, mode+".png"), ImageFormat.Png);
            }
            Console.WriteLine("DOC_QUOTA_CAPTURE_OK selected pets; synthetic values; isolated profile; no device access");
            return;
        }
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
        Capture(new CycleSettingsForm(),"device-control");
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
        SaveDesignFrames(status, "pet", "pet");
        SaveDesignFrames(status with { Codex=new("working",0,NeedsInput:true), CapturedAt=DateTimeOffset.FromUnixTimeMilliseconds(800) }, "codex", "needs-input");
        SaveDesignFrames(status with { Codex=new("idle",0,CompletionActive:true) }, "codex", "completed");
        Console.WriteLine("DOC_CAPTURE_OK isolated profile; synthetic values; no USB, HTTP, online data or private artwork");
        void SaveDesignFrames(StatusSnapshot frameStatus, string mode, string name)
        {
            if (designAnimation is null) return;
            for (int i = 0; i < designAnimation.Frames.Length; i++)
            {
                var still = new PetAnimation([400], [designAnimation.Frames[i]], designAnimation.Width, designAnimation.Height);
                PetAnimationStore.Shared.Select("claude", still);
                PetAnimationStore.Shared.Select("codex", still);
                using var image = MirrorForm.RenderSnapshot(frameStatus, mode);
                image.Save(Path.Combine(output, $"{name}-frame-{i:D2}.png"), ImageFormat.Png);
            }
            PetAnimationStore.Shared.Select("claude", designAnimation);
            PetAnimationStore.Shared.Select("codex", designAnimation);
        }
        void Save(Form form, string name) {
            using var bitmap = new Bitmap(form.Width,form.Height);
            form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));
            bitmap.Save(Path.Combine(output,name+".png"));
        }
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
