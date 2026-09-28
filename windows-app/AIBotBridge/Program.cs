using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if(args.Length==2&&args[0]=="--self-test-tab5-closeout"){Tab5CloseoutSelfTest.Run(args[1]);return;}
        if(args.Length==1&&args[0]=="--self-test-tab5-ble-voice"){Tab5BleVoiceSelfTest.RunAsync().GetAwaiter().GetResult();return;}
        if(args.Length==2&&args[0]=="--self-test-tab5-ble-voice"){Tab5BleVoiceSelfTest.RunAsync(args[1]).GetAwaiter().GetResult();return;}
        if(args.Length==2&&args[0]=="--self-test-tab5-holidays"){Tab5HolidaySelfTest.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--self-test-tab5-install"){Tab5InstallSelfTest.RunAsync(args[1]).GetAwaiter().GetResult();return;}
        if(args.Length==2&&args[0]=="--capture-tab5-install"){ApplicationConfiguration.Initialize();Tab5InstallSelfTest.Capture(args[1]);return;}
        if(args.Length==2&&args[0]=="--self-test-tab5-install-tool"){Tab5InstallSelfTest.CheckToolAsync(args[1]).GetAwaiter().GetResult();return;}
        if(args.Length==1&&args[0]=="--tab5-wifi-list-once") {
            using var service=new Tab5Service();var networks=service.ReadWifiNetworksAsync(CancellationToken.None).GetAwaiter().GetResult();
            Console.WriteLine(JsonSerializer.Serialize(new{networks},JsonDefaults.Options));return;
        }
        if(args.Length==1&&args[0]=="--tab5-overview-once") {
            var catalog=Tab5CodexCatalog.Recent();var desktop=new Tab5CodexDesktop();
            var live=new Tab5LiveActivity((id,ct)=>desktop.ReadAsync(id,ct));
            live.RefreshAsync(catalog.Take(16).ToArray(),CancellationToken.None).GetAwaiter().GetResult();
            var states=live.Fresh().ToDictionary(s=>s.Id);var selected=Tab5CodexTasks.SelectOverview(catalog,states);
            Console.WriteLine(JsonSerializer.Serialize(new {taskId=selected?.Id,state=selected is null?"none":states.GetValueOrDefault(selected.Id)?.State??"unknown",liveStates=states.Count},JsonDefaults.Options));return;
        }
        if(args.Length==1&&args[0]=="--self-test-tab5-ble"){Tab5BleTransferSelfTest.RunAsync().GetAwaiter().GetResult();return;}
        if(args.Length==2&&args[0]=="--self-test-hidden-settings") {ApplicationConfiguration.Initialize();SettingsLayoutSelfTest.CheckHiddenLaunch(args[1]);return;}
        if(args.Length==2&&args[0]=="--self-test-settings-layout") {ApplicationConfiguration.Initialize();SettingsLayoutSelfTest.Run(args[1]);return;}
        if(args.Length==1&&args[0]=="--self-test-tab5-voice"){ApplicationConfiguration.Initialize();Tab5VoiceSelfTest.RunUi();Tab5VoiceSelfTest.RunAsync().GetAwaiter().GetResult();return;}
        if(args.Length==3&&args[0]=="--tab5-voice-check"){ApplicationConfiguration.Initialize();Tab5VoiceCheck.Run(args[1],args[2]);return;}
        if(args.Length==4&&args[0]=="--tab5-voice-check"){ApplicationConfiguration.Initialize();Tab5VoiceCheck.Run(args[1],args[2],args[3]);return;}
        if(args.Length==2&&args[0]=="--preview-tab5-voice-settings"){ApplicationConfiguration.Initialize();Tab5VoiceSelfTest.PreviewSettings(args[1]);return;}
        if(args.Length==1&&args[0]=="--tab5-voice-devices"){
            Console.WriteLine(JsonSerializer.Serialize(new{inputs=Tab5VoiceAudio.Devices(NAudio.CoreAudioApi.DataFlow.Capture).Select(d=>new{d.Name,dji=Tab5VoiceAudio.IsDji(d.Name)}),cable=Tab5VoiceAudio.Devices(NAudio.CoreAudioApi.DataFlow.Render).Any(d=>Tab5VoiceAudio.IsCable(d.Name))},JsonDefaults.Options));return;
        }
        if(args.Length==1 && args[0]=="--verify-deepseek-web") {MediaCostSelfTest.VerifyWeb();return;}
        if(args.Length==1 && args[0]=="--self-test-media-cost") {MediaCostSelfTest.Run();return;}
        if(args.Length==4 && args[0]=="--netease-duration-once") {
            var value=NeteaseLocalDuration.Read("cloudmusic.exe",args[1],args[2],args[3]);
            Console.WriteLine("NETEASE_LOCAL_DURATION_SECONDS="+(value?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"unknown"));return;
        }
        if (args.Length == 2 && args[0] == "--tab5-offer-ota")
        {
            try {
                var path=Path.GetFullPath(args[1]);
                _=Tab5OtaPackage.Load(path);
                RunTray(showTab5:true,otaPath:path);
            } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) {
                Console.Error.WriteLine("TAB5_OTA_OFFER_FAILED: "+ex.Message);Environment.ExitCode=1;
            }
            return;
        }
        if (args.Length == 1 && args[0] == "--tab5-connect") { RunTray(showTab5: true); return; }
        if (args.Length == 1 && args[0] == "--tab5-mic-diagnostic")
        {
            Environment.SetEnvironmentVariable("AIBOT_TAB5_USB_PAUSED", "1");
            Environment.SetEnvironmentVariable("AIBOT_TAB5_MIC_DIAGNOSTIC", "1");
            RunTray(showTab5: true);
            return;
        }
        if (args.Length == 2 && args[0] == "--capture-tab5")
        {
            ApplicationConfiguration.Initialize();
            using var form = new Tab5ConnectionForm(new Tab5Service());
            form.Show(); Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(Path.GetFullPath(args[1]), System.Drawing.Imaging.ImageFormat.Png);
            return;
        }
        if (args.Length == 2 && args[0] == "--tab5-pair")
        {
            var device = FlashDeviceDiscovery.Read().Single(d => d.Port.Equals(args[1], StringComparison.OrdinalIgnoreCase)
                && d.Identity.Contains("VID_303A", StringComparison.OrdinalIgnoreCase));
            new Tab5Service().PairUsbAsync(device, CancellationToken.None).GetAwaiter().GetResult();
            Console.WriteLine("TAB5_USB_PAIRED " + device.Port); return;
        }
        if (args.Length == 1 && args[0] == "--self-test-tab5")
        { Tab5SelfTest.RunAsync().GetAwaiter().GetResult(); return; }
        if (args.Length == 1 && args[0] == "--quota-history-once")
        {
            var rows = QuotaHistory.Shared.Read();
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, QuotaHistory.StatisticsZone).DateTime);
            Console.WriteLine(JsonSerializer.Serialize(new {
                timezone="Asia/Shanghai", unit="weekly_percentage_points", pollSeconds=60,
                samples=rows.Length, lastSample=rows.LastOrDefault()?.At, storageError=QuotaHistory.Shared.Error,
                days=QuotaHistory.Daily(rows,today,7,QuotaHistory.StatisticsZone)
            },JsonDefaults.Options));
            return;
        }
        if (args.Length == 2 && args[0] == "--capture-flasher-complete")
        {
            ApplicationConfiguration.Initialize(); FirmwareFlashSelfTest.Capture(args[1], completedPreview: true); return;
        }
        if (args.Length == 2 && args[0] == "--self-test-flash-progress-child")
        {
            Console.Write("4096 (25 %)\b\b\b"); Console.Out.Flush();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!File.Exists(args[1]) && DateTime.UtcNow < deadline) Thread.Sleep(20);
            Environment.ExitCode = File.Exists(args[1]) ? 0 : 1; return;
        }
        if (args.Length == 1 && args[0] == "--check-flash-tool")
        {
            using var tool = FirmwareFlasher.PrepareToolAsync(_ => { }, CancellationToken.None).GetAwaiter().GetResult();
            var version = FirmwareFlasher.RunProcessAsync(tool.Executable, ["version"], _ => { }, CancellationToken.None).GetAwaiter().GetResult();
            Console.WriteLine("BUNDLED_FLASH_TOOL_OK " + version.Trim()); return;
        }
        if (args.Length == 4 && args[0] == "--accept-flasher-current-firmware")
        {
            ApplicationConfiguration.Initialize();
            using var form = new FirmwareFlashForm(preferredPort: args[1]);
            form.AcceptCurrentFirmware(args[2], Path.GetFullPath(args[3]));
            Application.Run(form); return;
        }
        if (args.Length == 1 && args[0] == "--diagnose-flash-devices")
        {
            foreach (var device in FlashDeviceDiscovery.Read()) Console.WriteLine($"FLASH_USB_DEVICE port={device.Port} name={device.Name}");
            return;
        }
        if (args.Length == 2 && args[0] == "--test-flasher-backup")
        {
            FirmwareFlashSelfTest.RunDeviceBackupAsync(args[1]).GetAwaiter().GetResult(); return;
        }
        if (args.Length == 1 && args[0] == "--self-test-flasher")
        {
            FirmwareFlashSelfTest.RunAsync().GetAwaiter().GetResult(); return;
        }
        if (args.Length == 2 && args[0] == "--capture-flasher")
        {
            ApplicationConfiguration.Initialize(); FirmwareFlashSelfTest.Capture(args[1]); return;
        }
        if (args.Length == 2 && args[0] == "--capture-flasher-device")
        {
            ApplicationConfiguration.Initialize(); FirmwareFlashSelfTest.Capture(args[1], liveDevice: true); return;
        }
        if (args.Length is 1 or 2 && args[0] == "--flash")
        {
            using var instance = new Mutex(false, @"Local\AIBotBridge.Flasher." + System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value);
            bool acquired;
            try { acquired = instance.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) { MessageBox.Show("刷机窗口已打开，请先完成当前操作。", "AI-bot"); return; }
            try { ApplicationConfiguration.Initialize(); Application.Run(new FirmwareFlashForm(preferredPort: args.Length == 2 ? args[1] : null)); }
            finally { instance.ReleaseMutex(); }
            return;
        }
        if (args.Length == 1 && args[0] == "--test-stock-display-device")
        {
            StockDisplayDeviceTest.RunAsync().GetAwaiter().GetResult();
            return;
        }
        if (args.Length == 1 && args[0] == "--self-test-stock-display")
        {
            StockFallbackSelfTest.RunAsync().GetAwaiter().GetResult();
            DisplayCommandQueueSelfTest.RunAsync().GetAwaiter().GetResult();
            return;
        }
        if (args.Length == 1 && args[0] is "--enable-startup" or "--disable-startup")
        {
            try
            {
                StartupRegistration.SetEnabled(args[0] == "--enable-startup");
                Console.WriteLine("STARTUP_UPDATED " + StartupRegistration.TaskName);
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or
                UnauthorizedAccessException or System.Security.SecurityException or IOException)
            { Console.Error.WriteLine("STARTUP_FAILED: " + ex.Message); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 1 && args[0] == "--diagnose-pets")
        {
            Console.WriteLine("PET_CACHE_DIRECTORY=" + Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AI-bot"));
            foreach (var owner in new[] { "claude", "codex" })
            {
                var pet = PetAnimationStore.Shared.Selection(owner);
                Console.WriteLine($"PET_CACHE {owner} loaded={pet is not null} width={pet?.Width} height={pet?.Height} frames={pet?.Frames.Length}");
            }
            return;
        }
        if (args.Length == 1 && args[0] == "--self-test-public")
        {
            AppPaths.BeginPublicSelfTest();
            ApplicationConfiguration.Initialize();
            PublicSelfTest.Run();
            return;
        }
        if(args.Length==1&&args[0]=="--restore-cycle-default") {
            if(!BridgeSettings.Load().SaveEditable(new Dictionary<string,string> {
                ["display_mode"]="auto",["display_cycle_enabled"]="1"
            },out var error)){Console.Error.WriteLine(error);Environment.ExitCode=1;return;}
            Console.WriteLine("CYCLE_DEFAULT_RESTORED pages/order/interval preserved; restart bridge to apply");return;
        }
        if(args.Length==1&&args[0]=="--self-test-cycle-layout") { ApplicationConfiguration.Initialize(); CycleSettingsForm.VerifyLayout(); return; }
        if(args.Length==1&&args[0]=="--preview-cycle-settings") { ApplicationConfiguration.Initialize(); Application.Run(new CycleSettingsForm()); return; }
        if(args.Contains("--self-test-zhipu")) { ApplicationConfiguration.Initialize(); ZhipuSelfTest.Run(); return; }
        if(args.Contains("--test-zhipu-device")) { ZhipuSelfTest.RunDeviceAsync().GetAwaiter().GetResult(); return; }
        if(args.Length==1&&args[0]=="--authorize-zhipu") {
            RunTray(authorizeZhipu: true); return;
        }
        if(args.Contains("--test-activity-refresh")){try{ActivityRefreshDeviceTest.RunAsync().GetAwaiter().GetResult();}catch(Exception ex){Console.Error.WriteLine("ACTIVITY_REFRESH_FAILED: "+ex.Message);Environment.ExitCode=1;}return;}
        if(args.Contains("--test-bridge-grace")){try{BridgeGraceDeviceTest.RunAsync().GetAwaiter().GetResult();}catch(Exception ex){Console.Error.WriteLine("BRIDGE_GRACE_FAILED: "+ex.Message);Environment.ExitCode=1;}return;}
        if(args.Length==1&&args[0]=="--diagnose-deepseek"){QuotaRequestDiagnostics.ProbeAsync().GetAwaiter().GetResult();return;}
        if(args.Length==1&&args[0]=="--exit"){BridgeLifetime.RequestExit();return;}
        if(args.Contains("--test-system-refresh")){try{SystemRefreshDeviceTest.RunAsync().GetAwaiter().GetResult();}catch(Exception ex){Console.Error.WriteLine("SYSTEM_REFRESH_DEVICE_FAILED: "+ex.Message);Environment.ExitCode=1;}return;}
        if(args.Length==2&&args[0]=="--audit-device-test-cache") {DeviceAcceptanceTest.AuditTemporaryResources(Path.GetFullPath(args[1]));return;}
        if(args.Length==2&&args[0]=="--audit-legacy-device-assets") {LegacyDeviceAssetImport.Run(Path.GetFullPath(args[1]),false);return;}
        if(args.Length==2&&args[0]=="--import-legacy-device-assets") {ApplicationConfiguration.Initialize();LegacyDeviceAssetImport.Run(Path.GetFullPath(args[1]));return;}
        if(args.Length==3&&args[0]=="--audit-legacy-scenes") {ApplicationConfiguration.Initialize();LegacySceneAudit.Run(args[1],args[2]);return;}
        if(args.Length==1&&args[0]=="--audit-legacy-stock-labels") {ApplicationConfiguration.Initialize();LegacyStockLabelAudit.RunAsync().GetAwaiter().GetResult();return;}
        if(args.Length==2&&args[0]=="--audit-local-legacy-pets") {LegacyPetImport.Audit(Path.GetFullPath(args[1]));return;}
        if(args.Length==2&&args[0]=="--import-local-legacy-logos") {LocalPageLogos.Import(Path.GetFullPath(args[1]));return;}
        if (args.Contains("--self-test-migration"))
        {
            MigrationRegressionSelfTest.RunAsync().GetAwaiter().GetResult();
            return;
        }
        if (args.Contains("--self-test-pet-gallery") || args.Contains("--test-pet-gallery-live"))
        {
            ApplicationConfiguration.Initialize();
            PetGallerySelfTest.Run(args.Contains("--test-pet-gallery-live")).GetAwaiter().GetResult();
            return;
        }
        if (args.Contains("--self-test-pet-selection"))
        {
            PetSelectionSelfTest.Run();
            return;
        }
        if (args.Length == 2 && args[0] == "--import-local-legacy-pets")
        {
            LegacyPetImport.Run(Path.GetFullPath(args[1]));
            return;
        }
        if (args.Contains("--self-test-activity-cache")) { ActivityCacheSelfTest.RunAsync().GetAwaiter().GetResult(); return; }
        if (args.Contains("--self-test-codex-lifecycle"))
        {
            CodexLifecycleSelfTest.Run();
            return;
        }
        if (args.Contains("--self-test-migrated-domestic"))
        {
            ApplicationConfiguration.Initialize();
            MigratedDomesticSelfTest.Run();
            return;
        }
        if (args.Contains("--self-test-pet-animation"))
        {
            PetAnimationSelfTest.Run();
            return;
        }
        if (args.Contains("--self-test-display-policy"))
        {
            DisplayPolicySelfTest.Run();
            return;
        }
        if(args.Length==1&&args[0]=="--weather-settings") {
            ApplicationConfiguration.Initialize();
            var weather=new MigratedWeather.WeatherMonitor();weather.LoadCache();
            using var form=new MigratedWeather.WeatherSettingsForm(weather);
            form.Shown+=(_,_)=>SettingsWindow.Present(form);
            Application.Run(form);return;
        }
        if(args.Length==2&&args[0]=="--diagnose-weather-location") {
            MigratedWeatherTest.LocateAsync(Path.GetFullPath(args[1])).GetAwaiter().GetResult();return;
        }
        if (args.Contains("--test-migrated-weather"))
        {
            MigratedWeatherTest.RunAsync().GetAwaiter().GetResult();
            return;
        }
        if (args.Contains("--preview-weather-migration"))
        {
            ApplicationConfiguration.Initialize();
            WeatherMigrationPreview.RunAsync().GetAwaiter().GetResult();
            return;
        }
        if (args.Contains("--self-test-tray"))
        {
            ApplicationConfiguration.Initialize();
            TrayMenuSelfTest.Run();
            return;
        }
        if (args.Contains("--test-live-device-data"))
        {
            try { LiveDeviceDataTest.RunAsync().GetAwaiter().GetResult(); }
            catch (Exception ex)
            {
                Console.Error.WriteLine("LIVE_DEVICE_DATA_FAILED: " + ex.Message);
                Environment.ExitCode = 1;
            }
            return;
        }
        if (args.Contains("--test-device-pages"))
        {
            try { DeviceAcceptanceTest.RunAsync().GetAwaiter().GetResult(); }
            catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException)
            {
                Console.Error.WriteLine("DEVICE_ACCEPTANCE_FAILED: " + ex.Message);
                Environment.ExitCode = 1;
            }
            return;
        }
        if (args.Contains("--test-wifi-fallback") || args.Contains("--test-usb-management") ||
            args.Contains("--test-wifi-stability-5m"))
        {
            Console.OutputEncoding = Encoding.UTF8;
            try { WifiFallbackTest.RunHardwareAsync(
                args.Contains("--test-wifi-fallback") || args.Contains("--test-wifi-stability-5m"),
                args.Contains("--test-wifi-stability-5m")).GetAwaiter().GetResult(); }
            catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or
                                       System.Net.Sockets.SocketException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine("HARDWARE_TEST_FAILED: " + ex.Message);
                Environment.ExitCode = 1;
            }
            return;
        }
        if (args.Contains("--test-wifi-discovery"))
        {
            Console.OutputEncoding = Encoding.UTF8;
            try { LanDiscoveryDeviceTest.RunAsync().GetAwaiter().GetResult(); }
            catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or
                                       System.Net.Sockets.SocketException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine("LAN_DISCOVERY_DEVICE_FAILED: " + ex.Message);
                Environment.ExitCode = 1;
            }
            return;
        }
        if (args.Contains("--self-test-usb-management"))
        {
            UsbManagementSelfTest.RunAsync().GetAwaiter().GetResult();
            return;
        }
        if (args.Contains("--self-test-settings", StringComparer.OrdinalIgnoreCase))
        {
            Console.OutputEncoding = Encoding.UTF8;
            ApplicationConfiguration.Initialize();
            var output = Path.Combine(Environment.CurrentDirectory, "artifacts", "settings-self-test.png");
            SettingsSelfTest.Run(output);
            return;
        }

        if (args.Contains("--self-test-mirror", StringComparer.OrdinalIgnoreCase))
        {
            ApplicationConfiguration.Initialize();
            Console.OutputEncoding = Encoding.UTF8;
            var output = Path.Combine(Environment.CurrentDirectory, "artifacts", "mirror-self-test.png");
            MirrorSelfTest.Run(output);
            return;
        }

        if(args.Contains("--self-test-music-artwork")) {MusicLifecycleSelfTest.Run();return;}
        if (args.Contains("--self-test-music", StringComparer.OrdinalIgnoreCase))
        {
            Console.OutputEncoding = Encoding.UTF8;
            var music = new NowPlayingService();
            music.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
            var snapshot = music.Snapshot;
            Console.WriteLine($"MUSIC_SELF_TEST_OK session={(string.IsNullOrEmpty(snapshot?.Title) ? "none" : "present")} " +
                              $"playing={snapshot?.Playing ?? false} duration={snapshot?.DurationSeconds ?? 0:0} legacyBytes={snapshot?.CoverRgb565?.Length??0} tab5Bytes={snapshot?.Tab5CoverRgb565?.Length??0} sourcePixels={music.ArtworkSourceSize}");
            return;
        }

        if (args.Contains("--self-test-system", StringComparer.OrdinalIgnoreCase))
        {
            Console.OutputEncoding = Encoding.UTF8;
            var metrics = new SystemMetricsService().CaptureForSelfTestAsync().GetAwaiter().GetResult();
            Console.WriteLine($"SYSTEM_SELF_TEST_OK cpu={metrics.CpuPercent:0.0}% memory={metrics.MemoryPercent:0.0}% " +
                              $"up={metrics.UploadBytesPerSecond} down={metrics.DownloadBytesPerSecond}");
            return;
        }

        if (args.Contains("--self-test-live-data", StringComparer.OrdinalIgnoreCase))
        {
            Console.OutputEncoding = Encoding.UTF8;
            LiveDataSelfTest.RunAsync().GetAwaiter().GetResult();
            return;
        }

        if (args.Contains("--self-test-data", StringComparer.OrdinalIgnoreCase))
        {
            Console.OutputEncoding = Encoding.UTF8;
            DataSourceSelfTest.Run();
            return;
        }

        if (args.Contains("--self-test-lan", StringComparer.OrdinalIgnoreCase))
        {
            Console.OutputEncoding = Encoding.UTF8;
            LanBindingSelfTest.Run();
            LanBindingSelfTest.RunRebindingAsync().GetAwaiter().GetResult();
            LanDiscoverySelfTest.Run();
            LanDiscoverySelfTest.RunAddressChangeAsync().GetAwaiter().GetResult();
            LanServerSelfTest.RunAsync().GetAwaiter().GetResult();
            return;
        }

        if (args.Contains("--status-once", StringComparer.OrdinalIgnoreCase))
        {
            Console.OutputEncoding = Encoding.UTF8;
            using var runtime = new BridgeRuntime(startRefresh: false);
            SessionActivityReader.WaitForInitialScanAsync().GetAwaiter().GetResult();
            Console.WriteLine(JsonSerializer.Serialize(
                runtime.Capture(), JsonDefaults.Options));
            return;
        }

        // A misspelled/offline-preview flag must never start another bridge and
        // contend with the users working legacy installation for USB/HTTP.
        if (args.Length != 0)
        {
            Console.Error.WriteLine("Unknown command; no bridge started.");
            Environment.ExitCode = 2;
            return;
        }
        RunTray();
    }

    private static void RunTray(bool authorizeZhipu = false, bool showTab5 = false, string? otaPath = null)
    {
        using var instance = new Mutex(false, @"Local\AIBotBridge.Instance." +
            System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value);
        bool acquired;
        try { acquired = instance.WaitOne(0); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired)
        {
            Console.WriteLine("BRIDGE_ALREADY_RUNNING");
            if(otaPath is not null){Console.Error.WriteLine("TAB5_OTA_OFFER_FAILED: 请先退出正在运行的桥接，再提供固件。");Environment.ExitCode=1;}
            if (authorizeZhipu)
                MessageBox.Show("桥接已在运行，请从托盘菜单打开智谱授权。", "AI-bot");
            return;
        }
        try
        {
            ApplicationConfiguration.Initialize();
            var context = new TrayApplicationContext();
            if(otaPath is not null)context.OfferTab5Ota(otaPath);
            if (authorizeZhipu) context.OpenZhipuAuthorization();
            if (showTab5) context.OpenTab5Connection();
            Application.Run(context);
        }
        finally { instance.ReleaseMutex(); }
    }
}
