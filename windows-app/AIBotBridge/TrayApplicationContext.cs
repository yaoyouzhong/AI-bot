using System.Text.Json;

namespace AIBotBridge;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly CancellationTokenSource _shutdown = new();
    private readonly BridgeRuntime _runtime;
    private readonly SerialPublisher _serial;
    private readonly NotifyIcon _icon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly int _screenSaverMinutes;
    private string _selectedMode = "auto";
    private bool _automaticScreenSaver;

    internal TrayApplicationContext()
    {
        _runtime = new BridgeRuntime();
        var settings = BridgeSettings.Load();
        _screenSaverMinutes = int.TryParse(settings.Get("screensaver_timeout_minutes"), out var timeout)
            && timeout is > 0 and <= 1440 ? timeout : 0;
        var httpPort = int.TryParse(Environment.GetEnvironmentVariable("AIBOT_HTTP_PORT"), out var configuredPort)
            && configuredPort is > 0 and <= 65535
            ? configuredPort
            : 8765;
        var pairing = LanPairingFactory.Create(httpPort);
        _serial = new SerialPublisher(pairing);

        var menu = new ContextMenuStrip();
        menu.Items.Add("查看状态", null, (_, _) => ShowStatus());
        var displayMenu = new ToolStripMenuItem("显示模式");
        AddDisplayMode(displayMenu, "自动轮播", "auto");
        AddDisplayMode(displayMenu, "Claude + Codex", "dual");
        AddDisplayMode(displayMenu, "天气", "weather");
        AddDisplayMode(displayMenu, "股票", "stocks");
        AddDisplayMode(displayMenu, "账户额度", "quotas");
        AddDisplayMode(displayMenu, "国产额度", "domestic");
        AddDisplayMode(displayMenu, "系统监控", "system");
        AddDisplayMode(displayMenu, "屏保", "screensaver");
        menu.Items.Add(displayMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitThread());

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "AI-bot starting",
            ContextMenuStrip = menu,
            Visible = true
        };

        _timer = new System.Windows.Forms.Timer { Interval = 2000 };
        _timer.Tick += (_, _) =>
        {
            RefreshTooltip();
            UpdateAutomaticScreenSaver();
        };
        _timer.Start();

        var server = new LocalStatusServer(httpPort);
        _ = Task.Run(() => server.RunAsync(_runtime.Capture, _shutdown.Token));
        if (pairing is not null)
        {
            var lanServer = new LanStatusServer(pairing);
            _ = Task.Run(() => lanServer.RunAsync(_runtime.Capture, _shutdown.Token));
        }
        _ = Task.Run(() => _serial.RunAsync(_runtime.Capture, _shutdown.Token));
        RefreshTooltip();
    }

    private void AddDisplayMode(ToolStripMenuItem parent, string label, string mode)
    {
        parent.DropDownItems.Add(label, null, (_, _) =>
        {
            _selectedMode = mode;
            _automaticScreenSaver = false;
            if (!_serial.SendDisplayMode(mode))
                MessageBox.Show("设备尚未通过 USB 连接。", "AI-bot", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
        });
    }

    private void UpdateAutomaticScreenSaver()
    {
        if (_screenSaverMinutes == 0 || _selectedMode == "screensaver") return;
        var idle = SystemIdleTime.Read();
        if (!_automaticScreenSaver && idle >= TimeSpan.FromMinutes(_screenSaverMinutes))
        {
            _automaticScreenSaver = _serial.SendDisplayMode("screensaver");
        }
        else if (_automaticScreenSaver && idle < TimeSpan.FromSeconds(3))
        {
            if (_serial.SendDisplayMode(_selectedMode))
                _automaticScreenSaver = false;
        }
    }

    private void RefreshTooltip()
    {
        var status = _runtime.Capture();
        var port = _serial.PortName ?? "USB waiting";
        _icon.Text = $"AI-bot | C:{status.Codex.State} A:{status.Claude.State} | {port}";
    }

    private void ShowStatus()
    {
        var json = JsonSerializer.Serialize(
            _runtime.Capture(),
            new JsonSerializerOptions(JsonDefaults.Options) { WriteIndented = true });
        MessageBox.Show(json, "AI-bot status", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _timer.Dispose();
        _serial.NotifyHostGoingAway();
        _shutdown.Cancel();
        _runtime.Dispose();
        _icon.Visible = false;
        _icon.Dispose();
        _shutdown.Dispose();
        base.ExitThreadCore();
    }
}
