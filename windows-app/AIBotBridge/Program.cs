using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
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
        if (args.Contains("--test-wifi-fallback") || args.Contains("--test-usb-management"))
        {
            Console.OutputEncoding = Encoding.UTF8;
            try { WifiFallbackTest.RunHardwareAsync(args.Contains("--test-wifi-fallback")).GetAwaiter().GetResult(); }
            catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or
                                       System.Net.Sockets.SocketException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine("HARDWARE_TEST_FAILED: " + ex.Message);
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

        if (args.Contains("--self-test-music", StringComparer.OrdinalIgnoreCase))
        {
            Console.OutputEncoding = Encoding.UTF8;
            var music = new NowPlayingService();
            music.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
            var snapshot = music.Snapshot;
            Console.WriteLine($"MUSIC_SELF_TEST_OK session={(string.IsNullOrEmpty(snapshot?.Title) ? "none" : "present")} " +
                              $"playing={snapshot?.Playing ?? false} duration={snapshot?.DurationSeconds ?? 0:0}");
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
            LanServerSelfTest.RunAsync().GetAwaiter().GetResult();
            return;
        }

        if (args.Contains("--status-once", StringComparer.OrdinalIgnoreCase))
        {
            Console.OutputEncoding = Encoding.UTF8;
            using var runtime = new BridgeRuntime(startRefresh: false);
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

    private static void RunTray(bool authorizeZhipu = false)
    {
        using var instance = new Mutex(false, @"Local\AIBotBridge.Instance." +
            System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value);
        bool acquired;
        try { acquired = instance.WaitOne(0); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired)
        {
            Console.WriteLine("BRIDGE_ALREADY_RUNNING");
            if (authorizeZhipu)
                MessageBox.Show("桥接已在运行，请从托盘菜单打开智谱授权。", "AI-bot");
            return;
        }
        try
        {
            ApplicationConfiguration.Initialize();
            var context = new TrayApplicationContext();
            if (authorizeZhipu) context.OpenZhipuAuthorization();
            Application.Run(context);
        }
        finally { instance.ReleaseMutex(); }
    }
}
