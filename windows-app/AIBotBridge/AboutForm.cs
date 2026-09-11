using System.Diagnostics;
using System.Reflection;

namespace AIBotBridge;

internal sealed class AboutForm : Form
{
    private const string RepositoryUrl = "https://github.com/yaoyouzhong/AI-bot";

    internal AboutForm()
    {
        Text = "关于 AI-bot";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(440, 235);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        var version = typeof(AboutForm).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0] ?? Application.ProductVersion.Split('+')[0];
        Controls.Add(new Label { Text = "AI-bot · AI 状态桌面时钟", AutoSize = true, Location = new Point(20, 20) });
        Controls.Add(new Label { Text = "版本：" + version, AutoSize = true, Location = new Point(20, 55) });
        Controls.Add(new Label { Text = "作者：yaoyouzhong & Codex", UseMnemonic = false, AutoSize = true, Location = new Point(20, 90) });
        var link = new LinkLabel { Text = RepositoryUrl, AutoSize = true, Location = new Point(20, 125) };
        link.LinkClicked += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(RepositoryUrl) { UseShellExecute = true }); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            { MessageBox.Show(this, "无法打开浏览器，请手动访问：\n" + RepositoryUrl, "GitHub", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        };
        Controls.Add(link);
        var close = new Button { Text = "关闭", DialogResult = DialogResult.OK, Size = new Size(90, 30), Location = new Point(330, 185) };
        Controls.Add(close);
        AcceptButton = close;
        CancelButton = close;
    }
}
