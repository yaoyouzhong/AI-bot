using System.Text.Json;

namespace AIBotBridge;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _flashActions = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly EventWaitHandle _exitSignal = BridgeLifetime.Listen();
    private readonly BridgeSettings _settings;
    private readonly BridgeRuntime _runtime;
    private readonly SerialPublisher _serial;
    private Tab5Service? _tab5=>_services.Tab5;
    private readonly DeviceRegistryStore _devices;
    private readonly DeviceServiceManager _services;
    private DeviceCenterForm? _center;
    private QuotaTrendForm? _trend;
    private BridgeStatusForm? _statusForm;
    private UpdateCenterForm? _updatesForm;
    private readonly UpdateService _updates=new();
    private DateTimeOffset _nextUpdateCheck=DateTimeOffset.UtcNow.AddSeconds(30),_nextUpdateNotice;
    private bool _checkingUpdates,_updateBalloon;
    private readonly BridgeNotifications _notifications=new();
    private bool _notificationAttention;
    private long _attentionSequence;
    private readonly Dictionary<string,List<Form>> _deviceWindows=new();
    private bool _changingDevices;
    private bool _exiting;
    private DeviceDataDemand _demand=new([],[]);
    private Tab5ConnectionForm? _tab5Form;
    private string? _tab5Error;
    private readonly NotifyIcon _icon;
    private readonly System.Windows.Forms.Timer _timer;
    private int _screenSaverMinutes;
    private string _selectedMode = "auto";
    private bool _automaticScreenSaver;
    private bool _lastAiWorking;
    private bool _lastMusicPlaying;
    private DateTimeOffset? _temporaryWakeUntil;
    private MirrorForm? _mirror;
    private PetGalleryForm? _petGallery;
    private SettingsForm? _settingsForm;
    private MigratedWeather.WeatherSettingsForm? _weatherSettings;
    private bool _deviceOperationBusy;
    private bool _refreshBusy;
    private bool _startupBusy;
    private DateTimeOffset _nextQuotaWarning;
    private long _lastCompletionSequence;
    private bool _codexWasForeground;
    private bool _lastAttention;
    private long _lastWakeCompletion;
    private long _cycleStartedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    internal void OpenTab5Connection() {var tab=_devices.Snapshot.Devices.SingleOrDefault(d=>d.Kind==HardwareKind.Tab5&&d.Enabled);if(tab is null)ShowDevices();else HandleDeviceAction(tab.Id,"tab5");}
    internal void OfferTab5Ota(string path) {
        if(_tab5 is null||!_tab5.HasPairedDevice)throw new InvalidOperationException("请先配对 TAB5。");
        _tab5.OfferOta(path);
        Console.WriteLine("TAB5_OTA_OFFER_READY device_confirmation_required");
    }


    internal TrayApplicationContext()
    {
        _settings = BridgeSettings.Load();
        _devices = new DeviceRegistryStore();
        if(!_devices.Snapshot.MigrationComplete){var pair=new Tab5PairingStore().Current;var appData=AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData);_devices.Migrate(pair?.DeviceId,!string.IsNullOrWhiteSpace(_settings.Get("serial_port"))||!string.IsNullOrWhiteSpace(_settings.Get("device_host")),File.Exists(Path.Combine(appData,"AI-bot","codex-quota-history.json"))||File.Exists(Path.Combine(appData,"AI-bot","usage-cache.json"))||!string.IsNullOrWhiteSpace(_settings.Get("domestic_provider")));}
        _runtime = new BridgeRuntime(startRefresh:false);
        _selectedMode = DisplayModes.Load(_settings).SelectedMode;
        PublishDisplayPolicy();
        _screenSaverMinutes = int.TryParse(_settings.Get("screensaver_timeout_minutes"), out var timeout)
            && timeout is > 0 and <= 1440 ? timeout : 0;
        var httpPort = int.TryParse(Environment.GetEnvironmentVariable("AIBOT_HTTP_PORT"), out var configuredPort)
            && configuredPort is > 0 and <= 65535
            ? configuredPort
            : 8765;
        _services=new DeviceServiceManager(_runtime,httpPort,_shutdown.Token);
        _runtime.MusicArtworkChanged+=()=>_tab5?.Publish(_runtime.Capture());
        _serial=_services.Serial;
        _runtime.Domestic.Tab5Paired=()=>_devices.Snapshot.Devices.Any(d=>d.Enabled&&d.Kind==HardwareKind.Tab5);
        _runtime.Domestic.DeviceProviders=()=>new(_demand.Providers);
        var menu=DeviceCenterMenu.Build(_devices.Snapshot,HandleDeviceAction,HandleMenuAction);

        _icon = new NotifyIcon
        {
            Icon = AppIcon.Load(),
            Text = _devices.Snapshot.Devices.Any(d=>d.Kind==HardwareKind.Esp8266&&d.Enabled)?"AI-bot｜单击预览小屏，右键管理设备":"AI-bot｜单击打开设备中心",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.MouseUp += (_, e) => {
            if(e.Button!=MouseButtons.Left)return;
            var esp=_devices.Snapshot.Devices.SingleOrDefault(d=>d.Kind==HardwareKind.Esp8266&&d.Enabled);
            if(esp is null)ShowDevices();else if(_mirror?.Visible==true)_mirror.Hide();else HandleDeviceAction(esp.Id,"mirror");
        };
        _icon.BalloonTipClicked+=(_,_)=>{if(_updateBalloon){_updateBalloon=false;ShowUpdates();}};

        _timer = new System.Windows.Forms.Timer { Interval = 2000 };
        _timer.Tick += (_, _) =>
        {
            if(_exitSignal.WaitOne(0)){ExitThread();return;}
            while (_flashActions.TryDequeue(out var action)) action();
            var status = _runtime.Capture();
            _tab5?.Publish(status);
            _services.ObserveConnections(_devices.Snapshot);
            PollUpdates();
            var codexForeground = ForegroundObserver.CodexVisible();
            if (codexForeground && !_codexWasForeground && status.Codex.CompletionActive)
                SessionActivityReader.Signals.Acknowledge();
            _codexWasForeground = codexForeground;
            if (status.Codex.CompletionSequence > _lastCompletionSequence)
            {
                _lastCompletionSequence = status.Codex.CompletionSequence;
                Notify("completion",_lastCompletionSequence.ToString(),"任务完成","Codex 任务已完成，可在电脑或 TAB5 查看回复。",ToolTipIcon.Info);
            }
            bool needsInput=status.Codex.NeedsInput||status.Claude.NeedsInput||status.DomesticActivity?.NeedsInput==true;
            if(needsInput&&!_notificationAttention)Notify("attention",(++_attentionSequence).ToString(),"任务等待操作","请在电脑上检查需要确认的操作。",ToolTipIcon.Info);
            _notificationAttention=needsInput;
            if(_services.EspEnabled)UpdateAutomaticScreenSaver(status);
            PublishDisplayPolicy();
            var quotaPolicy=DisplayModes.Load(BridgeSettings.Load(),_selectedMode);
            _runtime.Domestic.RefreshNext(quotaPolicy);
            if (DateTimeOffset.UtcNow >= _nextQuotaWarning && _runtime.Domestic.TakeRefreshWarning(quotaPolicy) is string warning) {
                _nextQuotaWarning=DateTimeOffset.UtcNow.AddSeconds(30);
                Notify("quota",warning,"模型额度更新失败",warning,ToolTipIcon.Warning);
            }
        };
        _timer.Start();

        _ = Task.Run(() => FlashUsbLease.ServeAsync(_serial.PauseForFlashAsync,
            _serial.ResumeAfterFlash, token =>
            {
                var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _flashActions.Enqueue(() => { RestartCycle(); done.TrySetResult(); });
                return done.Task.WaitAsync(token);
            }, _shutdown.Token));
        var server = new LocalStatusServer(httpPort, _serial.ReadDeviceInfo, () => _tab5?.DiagnosticSummary ?? _tab5Error ?? "TAB5 未启用",_runtime.AcceptBrowserArtworkAsync,()=>_tab5?.CrashDiagnostic??"[]",()=>_runtime.MusicDiagnostic);
        _ = Task.Run(() => server.RunAsync(_runtime.Capture, _shutdown.Token));
        try{_services.ApplyAsync(_devices.Snapshot).GetAwaiter().GetResult();UpdateDemandAsync().GetAwaiter().GetResult();}
        catch(Exception ex){_tab5Error=ex.Message;}
        _flashActions.Enqueue(()=> {
            try{if(_tab5Error is not null)throw new InvalidOperationException(_tab5Error);}
            catch(Exception ex){_tab5Error=ex.Message;MessageBox.Show(ex.Message,"设备服务未启动");ShowDevices();}
            if(_devices.Snapshot.Devices.Length==0||_devices.Snapshot.LegacyDecisionPending)ShowDevices();
        });
    }

    private Task UpdateDemandAsync() {
        _demand=DeviceDataDemand.From(_devices.Snapshot,DisplayModes.Load(BridgeSettings.Load(),_selectedMode),BridgeSettings.Load().Get("domestic_provider","qwen"));
        return _runtime.ApplyDemandAsync(_demand);
    }
    private void ShowDevices(string page="devices") {
        if(_center is null||_center.IsDisposed)_center=new DeviceCenterForm(_devices,_services.View,HandleDeviceAction,ChangeDeviceAsync,AddDevice,HandleMenuAction,
            key=>key=="startup"?StartupRegistration.IsEnabled:_devices.Snapshot.DesktopQuotaHistory,ChangeStartupAsync);
        _center.Reload();_center.ShowPage(page);SettingsWindow.Present(_center);
    }
    private void ShowCommonSettings(bool bridge) {
        ShowDevices(bridge?"bridge-settings":"accounts");
    }
    private void RebuildMenu() {var old=_icon.ContextMenuStrip;_icon.ContextMenuStrip=DeviceCenterMenu.Build(_devices.Snapshot,HandleDeviceAction,HandleMenuAction);_icon.Text=_devices.Snapshot.Devices.Any(d=>d.Kind==HardwareKind.Esp8266&&d.Enabled)?"AI-bot｜单击预览小屏，右键管理设备":"AI-bot｜单击打开设备中心";old?.Dispose();_center?.Reload();}
    private async void AddDevice() {
        if(_changingDevices){MessageBox.Show(SettingsWindow.DialogOwner(_center),"正在更新设备，请稍候再试。","添加设备");return;}
        _changingDevices=true;
        try{
        using var dialog=new AddDeviceForm(_devices.Snapshot);
        if(dialog.ShowDialog(SettingsWindow.DialogOwner(_center))!=DialogResult.OK||dialog.Added is not {} device)return;
        try{_devices.Add(device);await _services.ApplyAsync(_devices.Snapshot);await UpdateDemandAsync();RebuildMenu();}
        catch(Exception ex){_devices.Remove(device.Id);await _services.ApplyAsync(_devices.Snapshot);MessageBox.Show(ex.Message,"设备添加未完成");RebuildMenu();}
        }catch(Exception ex){MessageBox.Show(ex.Message,"添加设备未完成");}finally{_changingDevices=false;}
    }
    private async Task ChangeDeviceAsync(RegisteredDevice device,bool remove) {
        if(_changingDevices||_deviceOperationBusy||_services.Busy)throw new InvalidOperationException("设备正在处理操作，请结束录音、升级或安装后再试。");
        _changingDevices=true;var previous=_devices.Snapshot;
        try {
            CloseDeviceWindows(device.Id);
            var candidate=previous with {Devices=remove?previous.Devices.Where(d=>d.Id!=device.Id).ToArray():previous.Devices.Select(d=>d.Id==device.Id?d with{Enabled=!d.Enabled}:d).ToArray()};
            await _services.ApplyAsync(candidate);
            try{if(remove)_devices.Remove(device.Id);else _devices.Update(candidate.Devices.Single(d=>d.Id==device.Id));}
            catch{await _services.ApplyAsync(previous);throw;}
            await UpdateDemandAsync();RebuildMenu();
        }finally{_changingDevices=false;}
    }
    private void CloseDeviceWindows(string id) {if(_deviceWindows.TryGetValue(id,out var forms)){foreach(var f in forms.ToArray())if(!f.IsDisposed){f.Close();if(!f.IsDisposed&&f.Visible)throw new InvalidOperationException("设备窗口正在处理操作，请完成后再试。");f.Dispose();}_deviceWindows.Remove(id);}}
    private T DeviceWindow<T>(RegisteredDevice d,T form,string title) where T:Form {
        form.Text=$"{d.Name} · {title}";
        if(!_deviceWindows.TryGetValue(d.Id,out var forms))_deviceWindows[d.Id]=forms=[];forms.Add(form);form.FormClosed+=(_,_)=>forms.Remove(form);return form;
    }
    private async void HandleDeviceAction(string id,string action) {
        try {
            if(_changingDevices)throw new InvalidOperationException("正在更新设备，请稍候。");
            if(action=="open-device"){
                if(!_devices.Snapshot.Devices.Any(d=>d.Id==id))throw new InvalidOperationException("设备已移除。");
                ShowDevices();_center!.SelectDevice(id);return;
            }
            if(action=="dismiss-legacy"){_devices.DismissLegacy();RebuildMenu();return;}
            if(action=="migrate-legacy") {
                if(MessageBox.Show("沿用原 ESP8266 小屏配置？旧协议采用兼容绑定，连接后核实设备信息。","导入原小屏",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
                _devices.Add(DeviceRegistryStore.Create(HardwareKind.Esp8266,"ESP8266 小屏",null));await _services.ApplyAsync(_devices.Snapshot);await UpdateDemandAsync();RebuildMenu();return;
            }
            if(action is "renamed" or "data-changed"){
                if(action=="renamed"){
                    await _services.ApplyAsync(_devices.Snapshot);
                    var renamed=_devices.Snapshot.Devices.Single(d=>d.Id==id);
                    if(_deviceWindows.TryGetValue(id,out var windows))foreach(var window in windows.Where(f=>!f.IsDisposed)){int suffix=window.Text.IndexOf(" · ",StringComparison.Ordinal);if(suffix>=0)window.Text=renamed.Name+window.Text[suffix..];}
                }
                await UpdateDemandAsync();RebuildMenu();return;
            }
            var current=_devices.Snapshot.Devices.SingleOrDefault(d=>d.Id==id)??throw new InvalidOperationException("设备已移除。");
            var d=DeviceCapabilities.Require(_devices.Snapshot,id,action,_services.View(current).Online);
            if(action.StartsWith("mode:")){SelectDisplayMode(action[5..]);await UpdateDemandAsync();return;}
            if(action.StartsWith("animation:")){MigratedWeather.WeatherMonitor.Animation=action[10..];return;}
            bool Mode(string mode){try{DeviceCapabilities.Require(_devices.Snapshot,id,"mode:"+mode,true);bool applied=SelectDisplayMode(mode);if(applied)_=UpdateDemandAsync();return applied;}catch(Exception ex){MessageBox.Show(ex.Message);return false;}}
            switch(action) {
                case "tab5-display":
                    if(_tab5 is null)throw new InvalidOperationException("TAB5 连接服务未启动。");
                    using(var form=DeviceWindow(d,new Tab5DisplaySettingsForm(_tab5.DisplaySettingsAsync),"显示设置"))form.ShowDialog(SettingsWindow.DialogOwner(_center));break;
                case "task-pins":
                    using(var form=DeviceWindow(d,new Tab5TaskPinsForm(),"常用任务"))form.ShowDialog(SettingsWindow.DialogOwner(_center));break;
                case "data":
                    using(var form=DeviceWindow(d,new DeviceDataForm(_devices,d.Id),"数据设置"))if(form.ShowDialog(SettingsWindow.DialogOwner(_center))==DialogResult.OK){await UpdateDemandAsync();RebuildMenu();}break;
                case "upgrade-tab5":
                    if(_tab5?.Busy==true)throw new InvalidOperationException("设备正在处理操作，请结束后再打开升级入口。");
                    if(_tab5Form is not null&&!_tab5Form.IsDisposed)_tab5Form.Close();
                    goto case "tab5";
                case "tab5":
                    if(_tab5 is null)throw new InvalidOperationException(_services.Error??"设备连接服务未启动，请停用后重新启用。");
                    if(_tab5Form is null||_tab5Form.IsDisposed)_tab5Form=DeviceWindow(d,new Tab5ConnectionForm(_tab5,showUpgrade:action=="upgrade-tab5"),"连接与固件升级");SettingsWindow.Present(_tab5Form);break;
                case "voice": if(_tab5 is null)throw new InvalidOperationException("设备未连接到桥接服务。");_tab5.ShowVoiceSettings(SettingsWindow.DialogOwner(_center)!);break;
                case "birthday-settings": using(var form=DeviceWindow(d,new BirthdaySettingsForm(),"日历与生日"))form.ShowDialog(SettingsWindow.DialogOwner(_center));break;
                case "legacy-settings":
                    using(var form=DeviceWindow(d,new LegacyDeviceSettingsForm(_serial,d.ConnectionMode,()=>_services.View(_devices.Snapshot.Devices.Single(x=>x.Id==d.Id))),"连接设置"))if(form.ShowDialog(SettingsWindow.DialogOwner(_center))==DialogResult.OK){
                        var previous=_devices.Snapshot;
                        var updated=previous.Devices.Single(x=>x.Id==d.Id) with {ConnectionMode=form.SelectedMode};
                        var candidate=previous with {Devices=previous.Devices.Select(x=>x.Id==d.Id?updated:x).ToArray()};
                        await _services.ApplyAsync(candidate);
                        try{_devices.Update(updated);}catch{await _services.ApplyAsync(previous);throw;}
                        _runtime.ReloadSettings();var value=BridgeSettings.Load().Get("screensaver_timeout_minutes");
                        _screenSaverMinutes=int.TryParse(value,out int minutes)&&minutes is >0 and <=1440?minutes:0;
                        RebuildMenu();
                    }break;
                case "cycle":
                    using(var form=DeviceWindow(d,new CycleSettingsForm(_serial,policy=>{
                        _selectedMode=policy.SelectedMode;_automaticScreenSaver=false;_temporaryWakeUntil=null;
                        _cycleStartedAt=DateTimeOffset.UtcNow.ToUnixTimeSeconds();PublishDisplayPolicy();_serial.SendDisplayMode(policy.SelectedMode);_=UpdateDemandAsync();
                    }),"显示设置"))form.ShowDialog(SettingsWindow.DialogOwner(_center));break;
                case "mirror":
                    if(_mirror is null||_mirror.IsDisposed)_mirror=DeviceWindow(d,new MirrorForm(_runtime.Capture,()=>_selectedMode,Mode,_serial.SendBrightness,()=>Task.Run(_serial.ReadDeviceInfo)),"设备镜像");_mirror.ShowAtTray();break;
                case "appearance": ShowAppearance(d);break;
                case "pet-gallery":
                    if(_petGallery is null||_petGallery.IsDisposed)_petGallery=DeviceWindow(d,new PetGalleryForm(mode=>Mode(mode)),"桌宠素材");SettingsWindow.Present(_petGallery);break;
                case "flash": using(var form=DeviceWindow(d,new FirmwareFlashForm(preferredPort:_serial.PortName,bridgeRunning:true,expectedUsbIdentity:d.UsbIdentity),"固件升级"))form.ShowDialog(SettingsWindow.DialogOwner(_center));break;
                case "info": await ManageDeviceAsync(false);break;
                case "reset": await ManageDeviceAsync(true);break;
                case "fallback": await TestFallbackAsync();break;
            }
        }catch(Exception ex){MessageBox.Show(ex.Message,"设备操作未完成");}
    }

    private void ShowAppearance(RegisteredDevice device) {
        using var form=DeviceWindow(device,new DeviceAppearanceForm(MigratedWeather.WeatherMonitor.Animation,
            value=>MigratedWeather.WeatherMonitor.Animation=value,owner=>ImportPet(owner),
            owner=>PetAnimationStore.Shared.RestoreDefault(owner),owner=>{
                using var gallery=DeviceWindow(device,new PetGalleryForm(mode=>SelectDisplayMode(mode)),"桌宠素材");gallery.ShowDialog(owner);
            }),"外观设置");form.ShowDialog(SettingsWindow.DialogOwner(_center));
    }

    private async void HandleMenuAction(string action)
    {
        try {
            switch(action) {
                case "devices": ShowDevices();break;
                case "add-device": AddDevice();break;
                case "bridge-settings": ShowCommonSettings(true);break;
                case "accounts": ShowCommonSettings(false);break;
                case "refresh":
                    if(_refreshBusy)return;_refreshBusy=true;
                    try{await _runtime.RefreshAsync();_mirror?.Invalidate();}finally{_refreshBusy=false;}break;
                case "startup": await ChangeStartupAsync(!StartupRegistration.IsEnabled);RebuildMenu();break;
                case "quota-trend": if(_trend is null||_trend.IsDisposed)_trend=new QuotaTrendForm();SettingsWindow.Present(_trend);break;
                case "settings": ShowSettings();break;
                case "stocks-settings": ShowSettings("stocks");break;
                case "weather-settings": ShowWeatherSettings();break;
                case "authorize": ShowDomesticAuth();break;
                case "status": ShowStatus();break;
                case "updates": ShowUpdates();break;
                case "notifications": using(var form=new NotificationSettingsForm(_notifications))form.ShowDialog(SettingsWindow.DialogOwner(_center));break;
                case "configuration-backup": using(var form=new ConfigurationBackupForm(()=>{
                    _runtime.ReloadSettings();_selectedMode=DisplayModes.Load(BridgeSettings.Load()).SelectedMode;_automaticScreenSaver=false;
                    _cycleStartedAt=DateTimeOffset.UtcNow.ToUnixTimeSeconds();PublishDisplayPolicy();if(_services.EspEnabled)_serial.SendDisplayMode(_selectedMode);_=UpdateDemandAsync();RebuildMenu();
                }))form.ShowDialog(SettingsWindow.DialogOwner(_center));break;
                case "about": using(var dialog=new AboutForm())dialog.ShowDialog();break;
                case "exit": ExitThread();break;
                default: ShowDevices();break;
            }
        }catch(Exception ex){MessageBox.Show(ex.Message,"桥接操作未完成",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }

    private async Task ChangeStartupAsync(bool enabled)
    {
        if(_startupBusy)throw new InvalidOperationException("开机启动设置正在保存，请稍候。");
        _startupBusy=true;
        try{await Task.Run(()=>StartupRegistration.SetEnabled(enabled));}
        finally{_startupBusy=false;}
    }

    private void Notify(string kind,string key,string title,string message,ToolTipIcon icon) {
        var decision=_notifications.Evaluate(kind,key,title,DateTimeOffset.Now);
        if(decision.Show){_updateBalloon=false;_icon.ShowBalloonTip(10000,title,message,icon);}
        if(decision.Sound)_=Task.Run(CompletionChime.Play);
    }
    private bool SelectDisplayMode(string mode)
    {
        if (!_services.EspEnabled||!DisplayModes.IsValid(mode)) return false;
        if (!DisplayModes.TrySelect(mode,out var policy,out var error))
        { MessageBox.Show(error, "显示模式"); return false; }
        _selectedMode = mode;
        _automaticScreenSaver = false;
        _temporaryWakeUntil = null;
        if(mode=="auto")_cycleStartedAt=policy.CycleStartedAt;
        PublishDisplayPolicy();
        if (!_serial.SendDisplayMode(mode))
        {
            MessageBox.Show("显示选择已保存，将通过可用连接应用；USB 当前未连接或正在回退测试。", "AI-bot",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        return true;
    }

    private void PublishDisplayPolicy()
    {
        var selected = _automaticScreenSaver
            ? _temporaryWakeUntil.HasValue ? DisplayModes.Resolve(_runtime.Capture(),"auto") : "screensaver"
            : _selectedMode;
        _runtime.SetDisplayPolicy(DisplayModes.Load(BridgeSettings.Load(), selected) with { CycleStartedAt = _cycleStartedAt });
    }

    private void RestartCycle()
    {
        if(!_services.EspEnabled)return;
        _selectedMode = "auto";
        _automaticScreenSaver = false;
        _temporaryWakeUntil = null;
        _cycleStartedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        PublishDisplayPolicy();
        _serial.SendDisplayMode("auto");
    }

    private void UpdateAutomaticScreenSaver(StatusSnapshot status)
    {
        var timeout = BridgeSettings.Load().Get("screensaver_timeout_minutes");
        _screenSaverMinutes = int.TryParse(timeout, out var minutes) && minutes is > 0 and <= 1440 ? minutes : 0;
        var aiWorking = status.Codex.State == "working" || status.Claude.State == "working" || status.DomesticActivity?.State == "working";
        var attention = status.Codex.NeedsInput || status.Claude.NeedsInput || status.DomesticActivity?.NeedsInput == true;
        var newAlert = attention && !_lastAttention || status.Codex.CompletionActive && status.Codex.CompletionSequence > _lastWakeCompletion;
        _lastAttention = attention; _lastWakeCompletion = status.Codex.CompletionSequence;
        var musicPlaying = status.Music?.Playing == true;
        if (_screenSaverMinutes == 0 || _selectedMode == "screensaver")
        {
            _automaticScreenSaver = false;
            _temporaryWakeUntil = null;
            _lastAiWorking = aiWorking;
            _lastMusicPlaying = musicPlaying;
            return;
        }
        var idle = SystemIdleTime.Read();
        if (!_automaticScreenSaver && idle >= TimeSpan.FromMinutes(_screenSaverMinutes))
        {
            _automaticScreenSaver = true;
            _serial.SendDisplayMode("screensaver");
            _temporaryWakeUntil = null;
        }
        else if (_automaticScreenSaver && idle < TimeSpan.FromSeconds(3))
        {
            _automaticScreenSaver = false;
            _temporaryWakeUntil = null;
            _serial.SendDisplayMode(_selectedMode);
        }
        else if (_automaticScreenSaver &&
                 (newAlert || (musicPlaying && !_lastMusicPlaying) || (aiWorking && !_lastAiWorking)))
        {
            var wakeMode = DisplayModes.Resolve(status,"auto");
            _temporaryWakeUntil = DateTimeOffset.UtcNow.AddSeconds(12);
            _serial.SendDisplayMode(wakeMode);
        }
        else if (_automaticScreenSaver && _temporaryWakeUntil <= DateTimeOffset.UtcNow)
        {
            _temporaryWakeUntil = null;
            _serial.SendDisplayMode("screensaver");
        }
        _lastAiWorking = aiWorking;
        _lastMusicPlaying = musicPlaying;
    }

    private void ShowStatus()
    {
        if(_statusForm is null||_statusForm.IsDisposed)
            _statusForm=new BridgeStatusForm(()=>BridgeStatusView.Capture(_devices.Snapshot,_services.View,_runtime.Capture(),_demand,_services.Error,_services.Health),ReconnectDeviceAsync,path=>_services.ExportDiagnostics(path,_devices.Snapshot));
        SettingsWindow.Present(_statusForm);
    }
    private UpdateDevice[] UpdateDevices()=>[new("bridge","电脑端 AI-bot",Application.ProductVersion.Split('+')[0],"运行中",true,"bridge","打开安装程序"),.._devices.Snapshot.Devices.Select(d=>{
        return UpdateDevice.From(d,_services.View(d));
    })];
    private async Task InstallUpdateAsync(UpdateDevice target,PreparedUpdate prepared) {
        prepared.VerifyUnchanged();
        if(_changingDevices||_deviceOperationBusy||_services.Busy)throw new InvalidOperationException("请先结束正在进行的设备操作。");
        var current=UpdateDevices().SingleOrDefault(d=>d.Id==target.Id);
        if(!UpdateService.SameTarget(target,current,prepared.Release))
            throw new InvalidOperationException("设备或版本已改变，请重新检查更新。");
        if(target.Component=="bridge") {
            if(MessageBox.Show(_updatesForm,"安装程序已校验。现在打开安装程序更新电脑端？","更新电脑端",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(prepared.File){UseShellExecute=true});return;
        }
        var device=_devices.Snapshot.Devices.Single(d=>d.Id==target.Id);
        if(target.Component=="tab5") {
            if(_tab5 is null||!_tab5.HasPairedDevice||prepared.Tab5 is null)throw new InvalidOperationException("请先连接并配对 TAB5。");
            if(_tab5Form is not null&&!_tab5Form.IsDisposed){_tab5Form.Close();if(!_tab5Form.IsDisposed&&_tab5Form.Visible)throw new InvalidOperationException("请先结束 TAB5 窗口中的操作。");}
            _tab5.OfferOta(prepared.Tab5);
            _tab5Form=DeviceWindow(device,new Tab5ConnectionForm(_tab5,loadNetworks:false,showUpgrade:true),"连接与固件升级");SettingsWindow.Present(_tab5Form);
        } else {
            if(UpdateService.IsLegacyEsp(target)&&MessageBox.Show(_updatesForm,
                $"小屏固件未上报版本，无法判断 {prepared.Release.Version} 是否比当前版本更新，也可能是同版本或更旧版本。\n\n继续后将打开 USB 刷机工具，先完整备份，再由你点击开始刷机。是否继续？",
                "手动准备小屏升级",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)throw new OperationCanceledException();
            if(!UpdateService.SameTarget(target,UpdateDevices().SingleOrDefault(d=>d.Id==target.Id),prepared.Release))
                throw new InvalidOperationException("设备或版本已改变，请重新检查更新。");
            _deviceOperationBusy=true;
            try{using var form=DeviceWindow(device,new FirmwareFlashForm(preferredPort:_serial.PortName,preparedFirmware:prepared.File,bridgeRunning:true,expectedUsbIdentity:device.UsbIdentity),"固件升级");form.ShowDialog(_updatesForm);}
            finally{_deviceOperationBusy=false;}
        }
        await Task.CompletedTask;
    }
    private async void PollUpdates() {
        if(_checkingUpdates||_exiting||DateTimeOffset.UtcNow<_nextUpdateNotice)return;
        _nextUpdateNotice=DateTimeOffset.UtcNow.AddSeconds(30);
        var preference=UpdateReminder.Read();if(!preference.Automatic)return;
        _checkingUpdates=true;
        try {
            if(DateTimeOffset.UtcNow>=_nextUpdateCheck) {
                _nextUpdateCheck=DateTimeOffset.UtcNow.AddHours(1);
                await _updates.CheckAsync(_shutdown.Token);
                _nextUpdateCheck=DateTimeOffset.UtcNow.AddHours(24);
            }
            if(_exiting||_notifications.Options.IsQuiet(DateTime.Now))return;
            preference=UpdateReminder.Read();if(!preference.Automatic)return;
            var devices=UpdateDevices();var keys=UpdateReminder.Pending(devices,_updates.Available,preference);if(keys.Length==0)return;
            UpdateReminder.Remember(preference,keys);
            string names=string.Join("、",devices.Where(d=>keys.Contains(d.Id+":"+(_updates.Available.GetValueOrDefault(d.Component)?.Version??""))).Select(d=>d.Name));
            _updateBalloon=true;_icon.ShowBalloonTip(10000,"AI-bot 有可用更新",names+" · 点击查看更新说明并下载。",ToolTipIcon.Info);
        }catch(Exception ex) when(ex is HttpRequestException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException or OperationCanceledException or KeyNotFoundException){
            // Query errors remain visible in the update center; retry in one hour.
        }finally{_checkingUpdates=false;}
    }
    private void ShowUpdates() {
        if(_updatesForm is null||_updatesForm.IsDisposed)_updatesForm=new UpdateCenterForm(UpdateDevices,InstallUpdateAsync,_updates);
        SettingsWindow.Present(_updatesForm);
    }
    private async Task ReconnectDeviceAsync(string id) {
        if(_changingDevices||_deviceOperationBusy)throw new InvalidOperationException("设备操作尚未结束，请稍后重试。");
        _changingDevices=true;
        try {
            await _services.ReconnectAsync(_devices.Snapshot,id);
            if(_deviceWindows.TryGetValue(id,out var forms))foreach(var form in forms.ToArray())form.Close();
            _tab5?.Publish(_runtime.Capture());RebuildMenu();
        }finally{_changingDevices=false;}
    }

    private async Task ManageDeviceAsync(bool reset)
    {
        if (_deviceOperationBusy) return;
        if (reset && MessageBox.Show("将清除设备 Wi-Fi 配置并重启，之后需要重新配网。是否继续？",
                "重置设备 Wi-Fi", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.OK) return;
        _deviceOperationBusy = true;
        try
        {
            var result = await Task.Run(() =>
            {
                if (reset) { _serial.ResetDeviceWiFi(); return "设备已确认重置，即将重启。"; }
                return JsonSerializer.Serialize(_serial.ReadDeviceInfo(),
                    new JsonSerializerOptions(JsonDefaults.Options) { WriteIndented = true });
            });
            if (!_shutdown.IsCancellationRequested) MessageBox.Show(result, "AI-bot USB 设备管理");
        }
        catch (Exception ex)
        {
            if (!_shutdown.IsCancellationRequested) MessageBox.Show(
                "USB 操作未确认：" + ex.Message + (reset ? "\n设备可能已执行，请先观察设备；不会自动重试。" : ""), "AI-bot");
        }
        finally { _deviceOperationBusy = false; }
    }

    private async Task TestFallbackAsync()
    {
        if(_devices.Snapshot.Devices.SingleOrDefault(d=>d.Kind==HardwareKind.Esp8266)?.ConnectionMode!=EspConnectionMode.Auto){
            MessageBox.Show("请先在“连接设置”中选择“自动”，再检查 USB 与 Wi-Fi 自动切换。","连接诊断");return;
        }
        if (_deviceOperationBusy) return;
        if (MessageBox.Show("保持 USB 插着。测试将暂停 USB 常规发送约 12 秒，LAN 服务继续运行，" +
                "随后自动恢复。网络隔离时预期回退不通过。是否开始？", "Wi-Fi 回退测试",
                MessageBoxButtons.OKCancel) != DialogResult.OK) return;
        _deviceOperationBusy = true;
        try
        {
            await Task.Run(() => WifiFallbackTest.RunAsync(_serial, _shutdown.Token));
            if (!_shutdown.IsCancellationRequested) MessageBox.Show("Wi-Fi 回退与 USB 恢复均通过。", "AI-bot");
        }
        catch (Exception ex)
        {
            if (!_shutdown.IsCancellationRequested) MessageBox.Show("测试未通过：" + ex.Message, "AI-bot");
        }
        finally { _serial.ResumeTransmission(); _deviceOperationBusy = false; }
    }

    private void ShowSettings(string? section = null)
    {
        if(section=="weather"){ShowWeatherSettings();return;}
        if (_settingsForm is null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm(includeDeviceSettings:false,quotes:_runtime.Capture().Stocks?.Quotes);
            _settingsForm.FormClosed += (_,_)=>_runtime.ReloadSettings();
        }
        SettingsWindow.Present(_settingsForm);
        _settingsForm.FocusSection(section);
    }

    internal void OpenZhipuAuthorization()
    {
        _runtime.Domestic.OpenAuthorization("zhipu");
    }

    private void ShowDomesticAuth()
    {
        var provider = BridgeSettings.Load().Get("domestic_provider", "qwen");
        _runtime.Domestic.OpenAuthorization(provider == "alibaba" ? "qwen" : provider);
    }

    private void ShowWeatherSettings()
    {
        if (_weatherSettings is null || _weatherSettings.IsDisposed)
            _weatherSettings = new MigratedWeather.WeatherSettingsForm(_runtime.Weather);
        SettingsWindow.Present(_weatherSettings);
    }

    private void ImportPet(string? owner = null)
    {
        using var dialog = new OpenFileDialog
        {
            Title = $"{owner ?? "通用桌宠"}：选择有明确许可说明的桌宠图片",
            Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        if (!PetAnimation.TryImport(dialog.FileName, out var animation, out var licenseFile, out var error))
        {
            MessageBox.Show(error, "AI-bot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            if (owner is null) PetAnimationStore.Shared.Save(animation!);
            else PetAnimationStore.Shared.Select(owner, animation!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show("桌宠资源保存失败：" + ex.Message, "AI-bot",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        SelectDisplayMode(owner ?? "pet");
        MessageBox.Show($"桌宠已保存（{animation!.Frames.Length} 帧），镜像立即使用；设备将在 USB 连接后同步。许可说明：{Path.GetFileName(licenseFile)}", "AI-bot",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    protected override async void ExitThreadCore()
    {
        if(_exiting)return;_exiting=true;
        _timer.Stop();
        _timer.Dispose();
        _exitSignal.Dispose();
        _serial.NotifyHostGoingAway();
        _mirror?.Close();
        _petGallery?.Close();
        _settingsForm?.Close();
        _weatherSettings?.Close();
        _tab5Form?.Close();
        _shutdown.Cancel();
        _updates.Dispose();
        await _services.StopAsync();
        _center?.Close();_trend?.Close();_statusForm?.Close();_updatesForm?.Close();
        _runtime.Dispose();
        _icon.Visible = false;
        _icon.Icon?.Dispose();
        _icon.Dispose();
        _shutdown.Dispose();
        base.ExitThreadCore();
    }
}
