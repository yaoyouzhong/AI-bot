using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if(args.Length==1&&args[0]=="--self-test-tab5-host-binding"){AppPaths.BeginPublicSelfTest();Tab5HostBindingSelfTest.Run();return;}
        if(args.Length==2&&args[0]=="--self-test-tightvnc-component"){AppPaths.BeginPublicSelfTest();ApplicationConfiguration.Initialize();try{TightVncSelfTest.Run(Path.GetFullPath(args[1]));}catch(Exception ex){Console.Error.WriteLine("TIGHTVNC_COMPONENT_SELF_TEST_FAILED: "+ex);Environment.ExitCode=1;}return;}
        if(args.Length==2&&args[0]=="--test-tightvnc-sandbox"){AppPaths.BeginPublicSelfTest();Environment.ExitCode=0;TightVncSelfTest.SandboxAsync(Path.GetFullPath(args[1])).GetAwaiter().GetResult();return;}
        if(args.Length==2&&args[0]=="--diagnose-tightvnc-sandbox"){AppPaths.BeginPublicSelfTest();TightVncSelfTest.DiagnoseSandboxAsync(Path.GetFullPath(args[1])).GetAwaiter().GetResult();return;}
        if(args.Length==2&&args[0]=="--test-tightvnc-sandbox-success"){AppPaths.BeginPublicSelfTest();TightVncSelfTest.SandboxAsync(Path.GetFullPath(args[1]),testRollback:false).GetAwaiter().GetResult();return;}
        if(args.Length==2&&args[0]=="--clean-tightvnc-sandbox"){AppPaths.BeginPublicSelfTest();TightVncSelfTest.CleanSandboxAsync(Path.GetFullPath(args[1])).GetAwaiter().GetResult();return;}
        if(args.Length==2&&args[0]=="--provision-tightvnc-sandbox"){ApplicationConfiguration.Initialize();try{TightVncSelfTest.SandboxAsync(Path.GetFullPath(args[1]),testRollback:false,keepInstalled:true).GetAwaiter().GetResult();}catch(Exception ex){Console.Error.WriteLine("TIGHTVNC_SANDBOX_PROVISION_FAILED: "+ex);Environment.ExitCode=1;}return;}
        if(args.Length==1&&args[0]=="--check-wifi-keyboard"){AppProfileGuard.EnsureDesktopProfile();TightVncSelfTest.CheckDesktopAsync().GetAwaiter().GetResult();return;}

        if(args.Length==4&&args[0]=="--tightvnc-worker"){Environment.ExitCode=TightVncElevation.WorkerAsync(args[1],args[2],args[3]).GetAwaiter().GetResult();return;}
        if(args.Length==1&&args[0]=="--self-test-runtime-diagnostics"){AppPaths.BeginPublicSelfTest();Tab5Service.RuntimeDiagnosticsSelfTestAsync().GetAwaiter().GetResult();return;}
        if(args.Length==1&&args[0]=="--self-test-wireless-boot"){Tab5Service.WirelessBootSelfTestAsync().GetAwaiter().GetResult();return;}
        if(args.Length==1&&args[0]=="--tightvnc-inspect"){Console.WriteLine(JsonSerializer.Serialize(TightVncMachine.Inspect(TightVncPackage.Sid)));return;}
        if(args.Length==1&&args[0]=="--tightvnc-uninstall-prompt"){AppProfileGuard.EnsureDesktopProfile();ApplicationConfiguration.Initialize();Environment.ExitCode=TightVncMaintenance.UninstallPrompt();return;}
        if(args.Length==1&&args[0]=="--configure-wifi-keyboard"){AppProfileGuard.EnsureDesktopProfile();ApplicationConfiguration.Initialize();WifiKeyboardSettings.Configure();return;}
        if(args.Length==2&&args[0]=="--self-test-wifi-unlock"){AppPaths.BeginPublicSelfTest();ApplicationConfiguration.Initialize();WifiUnlockSelfTest.RunAsync(Path.GetFullPath(args[1])).GetAwaiter().GetResult();return;}
        if(args.Length==2&&args[0]=="--self-test-tab5-pin"){AppPaths.BeginPublicSelfTest();ApplicationConfiguration.Initialize();Tab5PinSelfTest.Run(Path.GetFullPath(args[1]));return;}
        if(args.Length==2&&args[0]=="--self-test-computer-control"){AppPaths.BeginPublicSelfTest();ApplicationConfiguration.Initialize();ComputerControlSelfTest.Run(Path.GetFullPath(args[1]));return;}

        if(args.Length==2&&args[0]=="--preview-notification-sounds"){AppPaths.BeginPublicSelfTest();CompletionChime.ExportPreviews(Path.GetFullPath(args[1]));return;}
        if(args.Length==2&&args[0]=="--self-test-agenda"){AppPaths.BeginPublicSelfTest();AgendaSelfTest.Run(Path.GetFullPath(args[1]));return;}
        if(args.Length==2&&args[0]=="--self-test-agenda-ui") {
            AppPaths.BeginPublicSelfTest();if(!Application.SetHighDpiMode(HighDpiMode.DpiUnaware))throw new InvalidOperationException("DPI test process must start before any windows.");
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);AgendaSelfTest.LayoutSimulations(AgendaStore.Shared,Path.GetFullPath(args[1]));return;
        }
        if((args.Length==1&&args[0]=="--tab5-ota-info")||(args.Length==3&&(args[0]=="--tab5-start-ota"||args[0]=="--tab5-verify-ota"))) {
            try {
                var result=Tab5OtaControl.CallAsync(args.Length==1?"info":args[0]=="--tab5-verify-ota"?"verify":"start",args.Length==3?args[1]:null,args.Length==3?args[2]:null).GetAwaiter().GetResult();
                Console.WriteLine(JsonSerializer.Serialize(result,JsonDefaults.Options));if(!result.GetProperty("ok").GetBoolean())Environment.ExitCode=1;
            }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or TimeoutException or OperationCanceledException or JsonException){Console.Error.WriteLine("TAB5_LOCAL_CONTROL_FAILED: "+ex.GetType().Name);Environment.ExitCode=1;}
            return;
        }
        if(args.Length==1&&args[0]=="--self-test-esp-usb-recovery"){EspUsbRecoverySelfTest.Run();return;}
        if(args.Length==1&&args[0]=="--self-test-serial-disconnect"){AppPaths.BeginPublicSelfTest();SerialDisconnectSelfTest.RunAsync().GetAwaiter().GetResult();return;}
        if(args.Length==1&&args[0]=="--test-esp-usb-recovery"){EspUsbRecoverySelfTest.RunHardwareAsync().GetAwaiter().GetResult();return;}
        if(args.Length==3&&args[0]=="--self-test-hd-import-ui"){AppPaths.BeginPublicSelfTest();ApplicationConfiguration.Initialize();GalleryHdPackSelfTest.RunUi(Path.GetFullPath(args[1]),Path.GetFullPath(args[2]));return;}
        if(args.Length==3&&args[0]=="--self-test-hd-import"){AppPaths.BeginPublicSelfTest();Task.Run(()=>GalleryHdPackSelfTest.Run(Path.GetFullPath(args[1]),Path.GetFullPath(args[2]))).GetAwaiter().GetResult();return;}
        if(args.Length==4&&args[0]=="--self-test-desktop-art"){AppPaths.BeginPublicSelfTest();Task.Run(()=>GalleryDesktopImagesSelfTest.Run(Path.GetFullPath(args[1]),Path.GetFullPath(args[2]),Path.GetFullPath(args[3]))).GetAwaiter().GetResult();return;}
        if(args.Length is 2 or 3 or 4&&args[0]=="--self-test-gallery-browser"){AppPaths.BeginPublicSelfTest();ApplicationConfiguration.Initialize();GalleryBrowserSelfTest.Run(Path.GetFullPath(args[1]),args.Length>=3?Path.GetFullPath(args[2]):null,args.Length==4?Path.GetFullPath(args[3]):null);return;}
        if(args.Length is 3 or 5&&args[0]=="--runtime-health-guard"){if(args.Length==5&&args[3]!="--self-test")throw new ArgumentException("Invalid guard arguments");RuntimeHealthGuard.Run(int.Parse(args[1]),args[2],args.Length==5?Path.GetFullPath(args[4]):null);return;}
        if(args.Length==3&&args[0]=="--self-test-runtime-health-guard-parent"){AppPaths.BeginPublicSelfTest();RuntimeHealthGuardSelfTest.ParentAsync(Path.GetFullPath(args[1]),args[2]).GetAwaiter().GetResult();return;}
        if(args.Length==1&&args[0]=="--self-test-serial-runtime"){
            using var port=new System.IO.Ports.SerialPort();
            Console.WriteLine("SERIAL_RUNTIME_OK ports="+System.IO.Ports.SerialPort.GetPortNames().Length);return;
        }
        if(args.Length==2&&args[0]=="--self-test-runtime-health"){AppPaths.BeginPublicSelfTest();RuntimeHealthSelfTest.RunAsync(Path.GetFullPath(args[1])).GetAwaiter().GetResult();return;}
        if(args.Length==2&&args[0]=="--runtime-health-once"){RuntimeHealthSelfTest.LiveAsync(Path.GetFullPath(args[1])).GetAwaiter().GetResult();return;}
        if(args.Length==3&&args[0]=="--runtime-health-watch"){RuntimeHealthSelfTest.WatchAsync(Path.GetFullPath(args[1]),Math.Clamp(int.Parse(args[2]),10,300)).GetAwaiter().GetResult();return;}
        if(args.Length==3&&args[0]=="--self-test-runtime-health-ui"){AppPaths.BeginPublicSelfTest();ApplicationConfiguration.Initialize();RuntimeHealthSelfTest.PreviewForm(Path.GetFullPath(args[1]),Path.GetFullPath(args[2]));return;}
        if(args.Length==1&&args[0]=="--self-test-weather-forecast"){AppPaths.BeginPublicSelfTest();WeatherForecastSelfTest.Run();return;}
        if(args.Length==1&&args[0]=="--self-test-codex-read-sync"){AppPaths.BeginPublicSelfTest();Tab5CodexReadStateSelfTest.Run();return;}
        if(args.Length==1&&args[0]=="--self-test-codex-tasks"){AppPaths.BeginPublicSelfTest();Tab5CodexSelfTest.RunAsync().GetAwaiter().GetResult();return;}
        if(args.Length==1&&args[0]=="--self-test-codex-read-state-local"){
            var state=Tab5CodexReadState.ReadLocal();Console.WriteLine(state is null?"CODEX_LOCAL_READ_STATE_UNAVAILABLE":"CODEX_LOCAL_READ_STATE_OK unread="+state.Unread.Count);return;
        }
        if(args.Length==2&&args[0]=="--self-test-device-center-style"){AppPaths.BeginPublicSelfTest();ApplicationConfiguration.Initialize();DeviceCenterStyleSelfTest.Run(Path.GetFullPath(args[1]));return;}
        if(args.Length==3&&args[0]=="--self-test-device-center-style"){AppPaths.BeginPublicSelfTest();ApplicationConfiguration.Initialize();DeviceCenterStyleSelfTest.Run(Path.GetFullPath(args[1]),args[2]);return;}
        if(args.Length==1&&args[0]=="--self-test-gallery-updates"){AppPaths.BeginPublicSelfTest();GalleryUpdateSelfTest.RunAsync().GetAwaiter().GetResult();return;}
        if(args.Length==1&&args[0]=="--self-test-gallery-pack-guides"){AppPaths.BeginPublicSelfTest();GalleryPackSelfTest.Run();return;}
        if(args.Length==3&&args[0]=="--self-test-gallery-guide-packs"){AppPaths.BeginPublicSelfTest();GalleryPackSelfTest.RunGuidePackages(args[1],args[2]);return;}
        if(args.Length==3&&args[0]=="--self-test-gallery-annual-pack"){AppPaths.BeginPublicSelfTest();GalleryPackSelfTest.RunAnnualPack(Path.GetFullPath(args[1]),Path.GetFullPath(args[2]));return;}
        if(args.Length==2&&args[0]=="--self-test-gallery-update-ui"){AppPaths.BeginPublicSelfTest();ApplicationConfiguration.Initialize();GalleryUpdateUiSelfTest.Run(Path.GetFullPath(args[1]));return;}
        if(args.Length==2&&args[0]=="--self-test-gallery-update-live"){AppPaths.BeginPublicSelfTest();ApplicationConfiguration.Initialize();GalleryUpdateUiSelfTest.RunLive(Path.GetFullPath(args[1]));return;}
        if(args.Length==4&&args[0]=="--self-test-gallery-update-live-resume"){AppPaths.BeginPublicSelfTest();ApplicationConfiguration.Initialize();GalleryUpdateUiSelfTest.RunLive(Path.GetFullPath(args[1]),Path.GetFullPath(args[2]),Path.GetFullPath(args[3]));return;}
        if(args.Length==1&&args[0]=="--self-test-media-players"){AppPaths.BeginPublicSelfTest();MediaPlayerSelfTest.RunAsync().GetAwaiter().GetResult();MusicLifecycleSelfTest.Run();return;}
        if(args.Length==2&&args[0]=="--test-media-live"){MediaPlayerSelfTest.LiveAsync(Path.GetFullPath(args[1])).GetAwaiter().GetResult();return;}
        if(args.Length==2&&args[0]=="--self-test-bridge-ui"){AppPaths.BeginPublicSelfTest();ApplicationConfiguration.Initialize();BridgeUiSelfTest.Run(Path.GetFullPath(args[1]));return;}
        if(args.Length==1&&args[0]=="--self-test-netease-music"){AppPaths.BeginPublicSelfTest();NeteaseMusicSelfTest.Run();MusicLifecycleSelfTest.Run();return;}
        if(args.Length==1&&args[0]=="--self-test-weather-free"){MigratedWeatherTest.FreeAsync().GetAwaiter().GetResult();return;}
        if(args.Length==2&&args[0]=="--self-test-netease-live"){NeteaseMusicSelfTest.LiveAsync(Path.GetFullPath(args[1])).GetAwaiter().GetResult();return;}
        if(args.Length==1&&args[0]=="--self-test-startup-read"){AppPaths.BeginPublicSelfTest();ApplicationConfiguration.Initialize();StartupRegistrationSelfTest.Run();return;}
        if(args.Length==2&&args[0]=="--self-test-stock-settings"){AppPaths.BeginPublicSelfTest();ApplicationConfiguration.Initialize();StockSettingsSelfTest.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--self-test-stock-search-live"){AppPaths.BeginPublicSelfTest();StockSettingsSelfTest.LiveAsync(args[1]).GetAwaiter().GetResult();return;}
        if(args.Length==1&&args[0]=="--self-test-esp-updates"){AppPaths.BeginPublicSelfTest();EspUpdateSelfTest.Run();return;}
        if(args.Length==3&&args[0]=="--test-esp-update-readonly"){AppPaths.BeginPublicSelfTest();EspUpdateSelfTest.ReadHardwareAsync(args[1],args[2]).GetAwaiter().GetResult();return;}
        if(args.Length==4&&args[0]=="--self-test-gallery-packs"){AppPaths.BeginPublicSelfTest();ApplicationConfiguration.Initialize();GalleryPackSelfTest.RunPackages(args[1],args[2],args[3]);return;}
        if(args.Length==1&&args[0]=="--self-test-profile-guard"){AppProfileGuardSelfTest.Run();return;}
        if(args.Length==1&&args[0]=="--self-test-unified-updates"){AppPaths.BeginPublicSelfTest();UpdateSelfTest.RunAsync().GetAwaiter().GetResult();return;}
        if(args.Length==2&&args[0]=="--self-test-update-ui"){AppPaths.BeginPublicSelfTest();ApplicationConfiguration.Initialize();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);UpdateUiSelfTest.Run(args[1]);return;}
        if(args.Length==2&&args[0]=="--self-test-update-live"){AppPaths.BeginPublicSelfTest();UpdateLiveSelfTest.RunAsync(args[1]).GetAwaiter().GetResult();return;}
        if(args.Length==0||!args[0].StartsWith("--self-test-",StringComparison.Ordinal)) {
            try { AppProfileGuard.EnsureDesktopProfile(); }
            catch(IOException ex) {
                Environment.ExitCode=2;
                if(args.Length==0)MessageBox.Show(ex.Message,"AI-bot 数据目录检查",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                else { try { Console.Error.WriteLine(ex.Message); } catch(IOException) { } }
                return;
            }
        }
        if(args.Length==3&&args[0]=="--self-test-tab5-ota-compression"){AppPaths.BeginPublicSelfTest();Tab5OtaCompressionSelfTest.RunAsync(args[1],args[2]).GetAwaiter().GetResult();return;}
        if(args.Length==1&&args[0]=="--self-test-browser-artwork") {AppPaths.BeginPublicSelfTest();BrowserMusicArtworkSelfTest.RunAsync().GetAwaiter().GetResult();return;}
        if(args.Length==1&&args[0]=="--self-test-tab5-crashes") {AppPaths.BeginPublicSelfTest();Tab5CrashDiagnosticsSelfTest.RunAsync().GetAwaiter().GetResult();return;}
        if(args.Length==3&&args[0]=="--test-tab5-usb-binary"){Tab5UsbReadHardwareTest.RunAsync(args[1],args[2]).GetAwaiter().GetResult();return;}
        if(args.Length==1&&args[0]=="--self-test-user-preferences"){UserPreferencesSelfTest.Run();return;}
        if(args.Length==1&&args[0]=="--self-test-device-recovery"){DeviceRecoverySelfTest.RunAsync().GetAwaiter().GetResult();return;}
        if(args.Length==2&&args[0]=="--self-test-tab5-integrated-transfer"){AppPaths.BeginPublicSelfTest();Tab5IntegratedTransferSelfTest.Run(args[1]);return;}
        if(args.Length==1&&args[0]=="--self-test-tab5-remote-ota"){Tab5RemoteOtaSelfTest.RunAsync().GetAwaiter().GetResult();return;}
        if(args.Length==1&&args[0]=="--self-test-tab5-display-settings"){Tab5DisplaySettingsSelfTest.RunAsync().GetAwaiter().GetResult();return;}
        if(args.Length==1&&args[0]=="--self-test-upgrade"){UpgradeSelfTest.Run();return;}
        if(args.Length==2&&args[0]=="--self-test-development-ui"){ApplicationConfiguration.Initialize();DevelopmentUiSelfTest.Run(args[1]);return;}
        if(args.Length==1&&args[0]=="--self-test-display-settings"){ApplicationConfiguration.Initialize();DisplaySettingsSelfTest.Run();return;}
        if(args.Length==2&&args[0]=="--test-esp-display"){ApplicationConfiguration.Initialize();DisplaySettingsSelfTest.RunHardware(int.Parse(args[1]));return;}
        if(args.Length==2&&args[0]=="--test-esp-transports") {
            ApplicationConfiguration.Initialize();
            try{EspTransportHardwareTest.RunAsync(int.Parse(args[1])).GetAwaiter().GetResult();}
            catch(Exception ex){Console.Error.WriteLine("ESP_TRANSPORTS_FAILED: "+ex.Message);Environment.ExitCode=1;}
            return;
        }
        if(args.Length==2&&args[0]=="--self-test-device-center"){ApplicationConfiguration.Initialize();DeviceCenterSelfTest.Run(Path.GetFullPath(args[1]));return;}
        if(args.Length==1&&args[0]=="--self-test-device-registry"){DeviceRegistrySelfTest.Run();return;}
        if(args.Length==2&&args[0]=="--self-test-tab5-closeout"){Tab5CloseoutSelfTest.Run(args[1]);return;}
        if(args.Length==1&&args[0]=="--self-test-tab5-quick-console"){AppPaths.BeginPublicSelfTest();Tab5QuickConsoleSelfTest.RunAsync().GetAwaiter().GetResult();return;}
        if(args.Length==1&&args[0]=="--self-test-art-guides"){AppPaths.BeginPublicSelfTest();Tab5ArtGuides.SelfTest();return;}
        if(args.Length==1&&args[0]=="--self-test-tab5-gallery"){AppPaths.BeginPublicSelfTest();Tab5GallerySelfTest.Run();return;}
        if(args.Length==1&&args[0]=="--self-test-tab5-gallery-transports"){AppPaths.BeginPublicSelfTest();Tab5GalleryTransportSelfTest.RunAsync().GetAwaiter().GetResult();return;}
        if(args.Length==1&&args[0]=="--self-test-tab5-rpc"){AppPaths.BeginPublicSelfTest();Tab5RpcSelfTest.RunAsync().GetAwaiter().GetResult();return;}
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
        if(args.Length==1&&args[0]=="--self-test-tab5-voice-core"){AppPaths.BeginPublicSelfTest();Tab5VoiceSelfTest.RunAsync().GetAwaiter().GetResult();return;}
        if(args.Length==1&&args[0]=="--self-test-tab5-voice"){AppPaths.BeginPublicSelfTest();ApplicationConfiguration.Initialize();Tab5VoiceSelfTest.RunUi();Tab5VoiceSelfTest.RunAsync().GetAwaiter().GetResult();return;}
        if(args.Length==3&&args[0]=="--tab5-voice-check"){ApplicationConfiguration.Initialize();Tab5VoiceCheck.Run(args[1],args[2]);return;}
        if(args.Length==4&&args[0]=="--tab5-voice-check"){ApplicationConfiguration.Initialize();Tab5VoiceCheck.Run(args[1],args[2],args[3]);return;}
        if(args.Length==2&&args[0]=="--preview-tab5-voice-settings"){ApplicationConfiguration.Initialize();Tab5VoiceSelfTest.PreviewSettings(args[1]);return;}
        if(args.Length==1&&args[0]=="--tab5-voice-devices"){
            Console.WriteLine(JsonSerializer.Serialize(new{inputs=Tab5VoiceAudio.Devices(NAudio.CoreAudioApi.DataFlow.Capture).Select(d=>new{d.Name,dji=Tab5VoiceAudio.IsDji(d.Name)}),cable=Tab5VoiceAudio.Devices(NAudio.CoreAudioApi.DataFlow.Render).Any(d=>Tab5VoiceAudio.IsCable(d.Name))},JsonDefaults.Options));return;
        }
        if(args.Length==2 && args[0]=="--self-test-tab5-telemetry") {AppPaths.BeginPublicSelfTest();Tab5TelemetrySelfTest.Run(args[1]);return;}
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
        if(args.Length is 2 or 3&&args[0]=="--tab5-usb-metrics-diagnostic") {
            if(args.Length==3) {
                if(!int.TryParse(args[2],out int interval)||interval is <25 or >250)throw new ArgumentException("Diagnostic interval must be 25..250 ms");
                Tab5Service.UsbMetricsTraceIntervalMs=interval;
            }
            Tab5Service.UsbMetricsTracePath=Path.GetFullPath(args[1]);
            RunTray(showTab5:true);return;
        }
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
            Console.WriteLine("请在设备中心验证并添加 TAB5；旧配对入口已转到设备中心。");
            RunTray(showTab5:true);return;
        }
        if (args.Length == 1 && args[0] == "--self-test-tab5")
        { AppPaths.BeginPublicSelfTest(); Tab5SelfTest.RunAsync().GetAwaiter().GetResult(); return; }
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
        if (args.Length == 1 && args[0] == "--self-test-quota-history")
        {
            AppPaths.BeginPublicSelfTest();
            ApplicationConfiguration.Initialize();
            try { QuotaHistorySelfTest.Run(); }
            catch(Exception ex) { Console.Error.WriteLine("QUOTA_HISTORY_SELF_TEST_FAILED: "+ex); Environment.ExitCode=1; }
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
        if (args.Length == 1 && args[0] == "--self-test-flash-tool")
        {
            AppPaths.BeginPublicSelfTest();
            FirmwareFlashSelfTest.CheckBundledToolAsync().GetAwaiter().GetResult(); return;
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
            if(!new DeviceRegistryStore().Snapshot.Devices.Any(d=>d.Enabled&&d.Kind==HardwareKind.Esp8266)){Console.Error.WriteLine("请先在设备中心添加并启用 ESP8266 小屏。");Environment.ExitCode=1;return;}
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
            try { PublicSelfTest.Run(); }
            catch(Exception ex) { Console.Error.WriteLine("PUBLIC_SELF_TEST_FAILED: "+ex); Environment.ExitCode=1; }
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
        if(args.Length==1&&args[0]=="--devices"){RunTray(showDevices:true);return;}
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

        if(args.Length==2&&args[0]=="--self-test-unlock-setup"){AppPaths.BeginPublicSelfTest();ComputerUnlockSetupSelfTest.RunAsync(args[1]).GetAwaiter().GetResult();return;}
        if(args.Length==1&&args[0]=="--complete-unlock-setup") {
            var state=new ComputerUnlockSetup().EnsureAsync(message=>Console.WriteLine(message),CancellationToken.None).GetAwaiter().GetResult();
            Environment.ExitCode=state.Ready?0:1;
            if(!state.Ready)MessageBox.Show(state.Message+"\n请在一键解锁页面点击保存重试。","解锁配置未完成",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            return;
        }
        if(args.Length==2&&args[0]=="--configure-login-helper"&&args[1] is "install" or "uninstall") {
            Environment.ExitCode=WindowsLoginMethod.ConfigureAsync(args[1]=="install").GetAwaiter().GetResult()?0:1;return;
        }
        if(args.Length==2&&args[0]=="--set-login-method"&&args[1] is "manual" or "pin" or "password") {
            var pair=new Tab5PairingStore().Current;var store=new Tab5PinStore();
            if(WindowsSessionLock.Read()!=false||pair is null||!store.Has(pair)||(args[1]!="manual"&&!WindowsLoginMethod.Installed)){Environment.ExitCode=1;return;}
            byte[] before=store.Read(pair)!;byte[] after=[];
            try {
                store.SetMethod(pair,args[1]=="pin"?AIBot.LoginMethod.LoginMethodKind.Pin:args[1]=="password"?AIBot.LoginMethod.LoginMethodKind.Password:AIBot.LoginMethod.LoginMethodKind.Manual);
                after=store.Read(pair)??[];
                if(!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(before,after))throw new IOException("Credential preservation check failed");
                Console.WriteLine("LOGIN_METHOD_SAVED_CREDENTIAL_PRESERVED");
            }finally{System.Security.Cryptography.CryptographicOperations.ZeroMemory(before);System.Security.Cryptography.CryptographicOperations.ZeroMemory(after);}
            return;
        }
        if(args.Length==1&&args[0]=="--check-login-helper"){Console.WriteLine(WindowsLoginMethod.Installed?"LOGIN_HELPER_INSTALLED":"LOGIN_HELPER_UNAVAILABLE");Environment.ExitCode=WindowsLoginMethod.Installed?0:1;return;}
        if (args.Contains("--status-once", StringComparer.OrdinalIgnoreCase))
        {
            // A desktop launcher may provide redirected output without a console.
            // Write UTF-8 to that stream without changing the console code page.
            using var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false), leaveOpen: true);
            using var runtime = new BridgeRuntime(startRefresh: false);
            SessionActivityReader.WaitForInitialScanAsync().GetAwaiter().GetResult();
            output.WriteLine(JsonSerializer.Serialize(
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

    private static void RunTray(bool authorizeZhipu = false, bool showTab5 = false, string? otaPath = null,bool showDevices=false)
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
            using var pointerVisibility=new PointerVisibility();
            var context = new TrayApplicationContext();
            if(otaPath is not null)context.OfferTab5Ota(otaPath);
            if (authorizeZhipu) context.OpenZhipuAuthorization();
            if (showTab5) context.OpenTab5Connection();
            if (showDevices) context.OpenDevices();
            Application.Run(context);
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException or FormatException or JsonException) {
            MessageBox.Show("设备配置未加载，原文件保持不变。请先备份 AI-bot 配置目录，检查 devices.json / devices.json.bak 和配对资料。\n"+ex.Message,"桥接启动未完成",MessageBoxButtons.OK,MessageBoxIcon.Error);Environment.ExitCode=1;
        }
        finally { instance.ReleaseMutex(); }
    }
}
