using System.Text.Json;

namespace AIBotBridge;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SerialPublisher _serial;
    private readonly NotifyIcon _icon;
    private readonly System.Windows.Forms.Timer _timer;

    internal TrayApplicationContext()
    {
        var httpPort = int.TryParse(Environment.GetEnvironmentVariable("AIBOT_HTTP_PORT"), out var configuredPort)
            && configuredPort is > 0 and <= 65535
            ? configuredPort
            : 8765;
        var pairing = LanPairingFactory.Create(httpPort);
        _serial = new SerialPublisher(pairing);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Show status", null, (_, _) => ShowStatus());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "AI-bot starting",
            ContextMenuStrip = menu,
            Visible = true
        };

        _timer = new System.Windows.Forms.Timer { Interval = 2000 };
        _timer.Tick += (_, _) => RefreshTooltip();
        _timer.Start();

        var server = new LocalStatusServer(httpPort);
        _ = Task.Run(() => server.RunAsync(SessionActivityReader.Capture, _shutdown.Token));
        if (pairing is not null)
        {
            var lanServer = new LanStatusServer(pairing);
            _ = Task.Run(() => lanServer.RunAsync(SessionActivityReader.Capture, _shutdown.Token));
        }
        _ = Task.Run(() => _serial.RunAsync(SessionActivityReader.Capture, _shutdown.Token));
        RefreshTooltip();
    }

    private void RefreshTooltip()
    {
        var status = SessionActivityReader.Capture();
        var port = _serial.PortName ?? "USB waiting";
        _icon.Text = $"AI-bot | C:{status.Codex.State} A:{status.Claude.State} | {port}";
    }

    private static void ShowStatus()
    {
        var json = JsonSerializer.Serialize(
            SessionActivityReader.Capture(),
            new JsonSerializerOptions(JsonDefaults.Options) { WriteIndented = true });
        MessageBox.Show(json, "AI-bot status", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _timer.Dispose();
        _shutdown.Cancel();
        _icon.Visible = false;
        _icon.Dispose();
        _shutdown.Dispose();
        base.ExitThreadCore();
    }
}
