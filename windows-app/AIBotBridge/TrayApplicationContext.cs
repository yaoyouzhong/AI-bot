using System.Text.Json;

namespace AIBotBridge;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly CancellationTokenSource _shutdown = new();
    private readonly EventWaitHandle _exitSignal = BridgeLifetime.Listen();
    private readonly BridgeSettings _settings;
    private readonly BridgeRuntime _runtime;
    private readonly SerialPublisher _serial;
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
    private DeviceControlForm? _deviceControl;
    private SettingsForm? _settingsForm;
    private MigratedWeather.WeatherSettingsForm? _weatherSettings;
    private bool _deviceOperationBusy;
    private bool _refreshBusy;
    private long _lastCompletionSequence;
    private bool _codexWasForeground;
    private bool _lastAttention;
    private long _lastWakeCompletion;
    private long _cycleStartedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    internal TrayApplicationContext()
    {
        _settings = BridgeSettings.Load();
        _runtime = new BridgeRuntime();
        _selectedMode = DisplayModes.Load(_settings).SelectedMode;
        PublishDisplayPolicy();
        _screenSaverMinutes = int.TryParse(_settings.Get("screensaver_timeout_minutes"), out var timeout)
            && timeout is > 0 and <= 1440 ? timeout : 0;
        var httpPort = int.TryParse(Environment.GetEnvironmentVariable("AIBOT_HTTP_PORT"), out var configuredPort)
            && configuredPort is > 0 and <= 65535
            ? configuredPort
            : 8765;
        var pairing = LanPairingFactory.Create(httpPort);
        _serial = new SerialPublisher(pairing, _settings.Get("serial_port"));

        var menu = TrayMenu.Build(HandleMenuAction, SelectDisplayMode, () => _selectedMode,
            () => _serial.PortName is { } port ? $"已连接：{port}（USB）" : "等待 USB 设备（自动连接）", () => _runtime.Capture().Quotas);

        _icon = new NotifyIcon
        {
            Icon = AppIcon.Load(),
            Text = "AI-bot｜单击打开镜像，右键打开菜单",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.MouseUp += (_, e) => { if (e.Button == MouseButtons.Left) ToggleMirror(); };

        _timer = new System.Windows.Forms.Timer { Interval = 2000 };
        _timer.Tick += (_, _) =>
        {
            if(_exitSignal.WaitOne(0)){ExitThread();return;}
            var status = _runtime.Capture();
            var codexForeground = ForegroundObserver.CodexVisible();
            if (codexForeground && !_codexWasForeground && status.Codex.CompletionActive)
                SessionActivityReader.Signals.Acknowledge();
            _codexWasForeground = codexForeground;
            if (status.Codex.CompletionSequence > _lastCompletionSequence)
            {
                _lastCompletionSequence = status.Codex.CompletionSequence;
                _ = Task.Run(CompletionChime.Play);
            }
            UpdateAutomaticScreenSaver(status);
            PublishDisplayPolicy();
            _runtime.Domestic.RefreshNext();
        };
        _timer.Start();

        var server = new LocalStatusServer(httpPort);
        _ = Task.Run(() => server.RunAsync(_runtime.Capture, _shutdown.Token));
        if (pairing is not null)
        {
            var lanServer = new LanStatusServer(pairing, _runtime.Resources);
            _ = Task.Run(() => lanServer.RunAsync(_runtime.Capture, _shutdown.Token));
        }
        _ = Task.Run(() => _serial.RunAsync(_runtime.Capture, _runtime.Resources, _shutdown.Token));
        _ = Task.Run(() => _serial.RunMetricsAsync(() => _runtime.SystemMetrics, _shutdown.Token));
    }

    private async void HandleMenuAction(string action)
    {
        if (action.StartsWith("cycle:", StringComparison.Ordinal))
        {
            var policy = DisplayModes.Load(BridgeSettings.Load());
            var pages = policy.Pages.ToList();
            var changes = new Dictionary<string, string>();
            var command = action[6..];
            if (command == "toggle") changes["display_cycle_enabled"] = policy.CycleEnabled ? "0" : "1";
            else if (command.StartsWith("interval:") && int.TryParse(command[9..], out var interval) && interval is 10 or 15 or 30 or 60)
                changes["display_cycle_interval_seconds"] = interval.ToString();
            else if (command.StartsWith("page:") && DisplayModes.Pages.Any(p => p.Mode == command[5..]))
            {
                var page = command[5..];
                if (!pages.Remove(page)) pages.Add(page);
                if (pages.Count == 0) { changes["display_cycle_enabled"] = "0"; pages = policy.Pages.ToList(); }
                changes["display_cycle_pages"] = string.Join(',', pages);
            }
            else return;
            changes["display_mode"] = "auto";
            if (!_settings.SaveEditable(changes, out var error)) { MessageBox.Show(error, "循环展示"); return; }
            RestartCycle();
            return;
        }
        if (action.StartsWith("pet:", StringComparison.Ordinal))
        {
            ImportPet(action.Split(':')[1]);
            return;
        }
        if (action.StartsWith("pet-reset:", StringComparison.Ordinal))
        {
            var owner = action.Split(':')[1];
            try
            {
                PetAnimationStore.Shared.RestoreDefault(owner);
                MessageBox.Show($"{owner} 默认动画已恢复到本机，设备将在 USB 连接后同步；其他角色未改变。", "AI-bot");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            { MessageBox.Show(ex.Message, "恢复默认动画失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            return;
        }
        if (action.StartsWith("screensaver:", StringComparison.Ordinal))
        {
            var minutes = int.Parse(action.Split(':')[1]);
            if (!SaveSetting("screensaver_timeout_minutes", minutes.ToString())) return;
            _screenSaverMinutes = minutes;
            if (minutes == 0 && _automaticScreenSaver)
            {
                _automaticScreenSaver = false;
                _temporaryWakeUntil = null;
                _serial.SendDisplayMode(_selectedMode);
            }
            PublishDisplayPolicy();
            return;
        }
        switch (action)
        {
            case "refresh":
                if (_refreshBusy) break;
                _refreshBusy = true;
                try { await _runtime.RefreshAsync(); _mirror?.Invalidate(); }
                catch (Exception ex) when (ex is IOException or HttpRequestException or OperationCanceledException or JsonException)
                { MessageBox.Show("刷新未完成，保留最近可用数据。", "AI-bot"); }
                finally { _refreshBusy = false; }
                break;
            case "startup":
                try { StartupRegistration.SetEnabled(!StartupRegistration.IsEnabled); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.Runtime.InteropServices.COMException)
                { MessageBox.Show(ex.Message, "开机启动设置失败"); }
                break;
            case "mirror": ToggleMirror(); break;
            case "quota-trend":
                if (_mirror is null || _mirror.IsDisposed)
                    _mirror = new MirrorForm(_runtime.Capture, () => _selectedMode, SelectDisplayMode, _serial.SendBrightness, () => Task.Run(_serial.ReadDeviceInfo));
                _mirror.ShowQuotaTrend(); break;
            case "settings": ShowSettings(); break;
            case "stocks-settings": ShowSettings("stocks"); break;
            case "weather-settings": ShowWeatherSettings(); break;
            case "device": ShowDeviceControl(); break;
            case "authorize": ShowDomesticAuth(); break;
            case "status": ShowStatus(); break;
            case "completion-ack": SessionActivityReader.Signals.Acknowledge(); break;
            case "cycle":
                using (var dialog = new CycleSettingsForm())
                    if (dialog.ShowDialog() == DialogResult.OK) RestartCycle();
                break;
            case "info": await ManageDeviceAsync(false); break;
            case "reset": await ManageDeviceAsync(true); break;
            case "fallback": await TestFallbackAsync(); break;
            case "pet": ImportPet(); break;
            case "pet-gallery":
                if (_petGallery is null || _petGallery.IsDisposed) _petGallery = new PetGalleryForm(SelectDisplayMode);
                _petGallery.Show(); _petGallery.Activate(); break;
            case "address": MessageBox.Show("本机状态接口：http://127.0.0.1:" +
                (Environment.GetEnvironmentVariable("AIBOT_HTTP_PORT") ?? "8765") +
                "/status\n局域网回退使用独立鉴权，以上本机地址不能供设备访问。", "桥接服务地址"); break;
            case "exit": ExitThread(); break;
        }
    }

    private void SelectDisplayMode(string mode)
    {
        if (!DisplayModes.IsValid(mode)) return;
        if (!_settings.SaveEditable(new Dictionary<string,string> { ["display_mode"] = mode, ["display_cycle_enabled"] = "0" }, out var error))
        { MessageBox.Show(error, "显示模式"); return; }
        _selectedMode = mode;
        _automaticScreenSaver = false;
        _temporaryWakeUntil = null;
        PublishDisplayPolicy();
        if (!_serial.SendDisplayMode(mode))
        {
            MessageBox.Show("显示选择已保存，将通过可用连接应用；USB 当前未连接或正在回退测试。", "AI-bot",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
    }

    private bool SaveSetting(string key, string value)
    {
        if (_settings.SaveEditable(new Dictionary<string, string> { [key] = value }, out var error)) return true;
        MessageBox.Show(error, "AI-bot 设置");
        return false;
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
        var json = JsonSerializer.Serialize(
            _runtime.Capture(),
            new JsonSerializerOptions(JsonDefaults.Options) { WriteIndented = true });
        var device = _serial.DeviceHost ?? "not discovered";
        MessageBox.Show($"Device LAN: {device}\n\n{json}", "AI-bot status",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    private void ToggleMirror()
    {
        if (_mirror is { IsDisposed: false, Visible: true }) _mirror.Hide();
        else ShowMirror();
    }

    private void ShowMirror()
    {
        PetAnimationStore.Shared.ReloadMissing();
        if (_mirror is null || _mirror.IsDisposed)
            _mirror = new MirrorForm(_runtime.Capture, () => _selectedMode,SelectDisplayMode,_serial.SendBrightness,()=>Task.Run(_serial.ReadDeviceInfo));
        _mirror.ShowAtTray();
    }

    private void ShowDeviceControl()
    {
        if (_deviceControl is null || _deviceControl.IsDisposed)
            _deviceControl = new DeviceControlForm(_serial, SelectDisplayMode, _selectedMode);
        _deviceControl.Show();
        _deviceControl.Activate();
    }

    private void ShowSettings(string? section = null)
    {
        if (_settingsForm is null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm(_settings);
            _settingsForm.FormClosed += (_,_)=>_runtime.ReloadSettings();
        }
        _settingsForm.Show();
        _settingsForm.Activate();
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
        _weatherSettings.Show();
        _weatherSettings.Activate();
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

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _timer.Dispose();
        _exitSignal.Dispose();
        _serial.NotifyHostGoingAway();
        _mirror?.Close();
        _petGallery?.Close();
        _settingsForm?.Close();
        _weatherSettings?.Close();
        _deviceControl?.Close();
        _shutdown.Cancel();
        _runtime.Dispose();
        _icon.Visible = false;
        _icon.Icon?.Dispose();
        _icon.Dispose();
        _shutdown.Dispose();
        base.ExitThreadCore();
    }
}
