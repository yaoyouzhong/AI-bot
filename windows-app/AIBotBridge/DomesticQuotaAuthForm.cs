using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace AIBotBridge;

internal sealed class DomesticQuotaAuthForm : Form
{
    private sealed record Provider(string Id, string Label, Uri StartUri, string[] AllowedHosts);

    private static readonly Provider[] Providers =
    [
        new("alibaba", "阿里云百炼", new Uri("https://bailian.console.aliyun.com/cn-beijing?tab=plan#/efm/subscription/token-plan"), ["bailian.console.aliyun.com"]),
        new("kimi", "Kimi", new Uri("https://www.kimi.com/code/console"), ["www.kimi.com"]),
        new("minimax", "MiniMax", new Uri("https://platform.minimaxi.com/console/usage"), ["platform.minimaxi.com", "www.minimaxi.com"]),
        new("deepseek", "DeepSeek", new Uri("https://platform.deepseek.com/usage"), ["platform.deepseek.com"])
    ];

    private readonly BridgeRuntime _runtime;
    private readonly ComboBox _provider = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly Label _status = new() { AutoSize = true, Text = "正在初始化隔离浏览器…" };
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private CoreWebView2DevToolsProtocolEventReceiver? _responses;

    internal DomesticQuotaAuthForm(BridgeRuntime runtime)
    {
        _runtime = runtime;
        Text = "AI-bot 国产模型额度授权";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(980, 720);
        MinimumSize = new Size(760, 560);

        _provider.Items.AddRange(Providers.Cast<object>().ToArray());
        _provider.Format += (_, args) => args.Value = ((Provider)args.ListItem!).Label;
        _provider.SelectedIndex = 0;
        _provider.SelectedIndexChanged += (_, _) => NavigateSelected();
        var refresh = new Button { Text = "打开/刷新", AutoSize = true };
        refresh.Click += (_, _) => NavigateSelected();

        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(8),
            WrapContents = false
        };
        top.Controls.Add(_provider);
        top.Controls.Add(refresh);
        top.Controls.Add(_status);
        Controls.Add(_web);
        Controls.Add(top);
        Shown += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AI-bot", "quota-auth-profile");
            var environment = await CoreWebView2Environment.CreateAsync(null, profile);
            await _web.EnsureCoreWebView2Async(environment);
            _web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            await _web.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
            _responses = _web.CoreWebView2.GetDevToolsProtocolEventReceiver("Network.responseReceived");
            _responses.DevToolsProtocolEventReceived += ResponseReceived;
            _status.Text = "请登录当前厂商；只缓存可显示额度。";
            NavigateSelected();
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException)
        {
            _status.Text = "WebView2 初始化失败，请安装或修复 WebView2 Runtime。";
        }
    }

    private void NavigateSelected()
    {
        if (_web.CoreWebView2 is null || _provider.SelectedItem is not Provider provider) return;
        _status.Text = "正在打开 " + provider.Label + "…";
        _web.CoreWebView2.Navigate(provider.StartUri.AbsoluteUri);
    }

    private async void ResponseReceived(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs args)
    {
        try
        {
            if (_provider.SelectedItem is not Provider provider || _web.CoreWebView2 is null) return;
            using var envelope = JsonDocument.Parse(args.ParameterObjectAsJson);
            var root = envelope.RootElement;
            var response = root.GetProperty("response");
            var url = new Uri(response.GetProperty("url").GetString() ?? string.Empty);
            if (!provider.AllowedHosts.Contains(url.Host, StringComparer.OrdinalIgnoreCase)) return;
            var mimeType = response.GetProperty("mimeType").GetString() ?? string.Empty;
            if (!mimeType.Contains("json", StringComparison.OrdinalIgnoreCase)) return;
            var requestId = root.GetProperty("requestId").GetString();
            if (string.IsNullOrEmpty(requestId)) return;
            var bodyResult = await _web.CoreWebView2.CallDevToolsProtocolMethodAsync(
                "Network.getResponseBody", JsonSerializer.Serialize(new { requestId }));
            using var bodyDocument = JsonDocument.Parse(bodyResult);
            var body = bodyDocument.RootElement.GetProperty("body").GetString() ?? string.Empty;
            if (bodyDocument.RootElement.TryGetProperty("base64Encoded", out var encoded) && encoded.GetBoolean())
                body = Encoding.UTF8.GetString(Convert.FromBase64String(body));
            _runtime.ApplyDomesticResponse(provider.Id, body);
            if (!IsDisposed)
                BeginInvoke(() => _status.Text = provider.Label + " 额度已更新并缓存。" );
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or UriFormatException or COMException)
        {
            // Most JSON responses are unrelated to quota. Unknown data is ignored and never logged.
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _responses is not null)
            _responses.DevToolsProtocolEventReceived -= ResponseReceived;
        base.Dispose(disposing);
    }
}
