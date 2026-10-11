using System.Diagnostics;
using System.Reflection;

namespace AIBotBridge;

internal sealed class AboutForm : Form
{
    private const string RepositoryUrl = "https://github.com/yaoyouzhong/AI-bot";

    internal AboutForm()
    {
        SuspendLayout();
        Text = "关于 AI-bot";Font=SettingsWindow.BodyFont();
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(440, 280);MinimumSize=new(420,310);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        var version = typeof(AboutForm).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0] ?? Application.ProductVersion.Split('+')[0];
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(24),ColumnCount=1,RowCount=6};root.ColumnStyles.Add(new(SizeType.Percent,100));
        for(int i=0;i<6;i++)root.RowStyles.Add(new(i==5?SizeType.Percent:SizeType.AutoSize,i==5?100:0));Controls.Add(root);
        root.Controls.Add(SettingsWindow.Title("关于 AI-bot"),0,0);
        root.Controls.Add(new Label { Text = "AI 状态桌面时钟", AutoSize = true,Margin=new(0,0,0,12) },0,1);
        root.Controls.Add(new Label { Text = "版本：" + version, AutoSize = true,Margin=new(0,0,0,8) },0,2);
        root.Controls.Add(new Label { Text = "作者：yaoyouzhong & Codex", UseMnemonic = false, AutoSize = true,Margin=new(0,0,0,8) },0,3);
        var link = new LinkLabel { Text = "GitHub 项目主页", AutoSize = true,Margin=Padding.Empty };
        link.LinkClicked += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(RepositoryUrl) { UseShellExecute = true }); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            { MessageBox.Show(this, "无法打开浏览器，请手动访问：\n" + RepositoryUrl, "GitHub", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        };
        root.Controls.Add(link,0,4);
        var close = new Button { Text = "关闭", DialogResult = DialogResult.OK,Anchor=AnchorStyles.Bottom|AnchorStyles.Right,Margin=new(0,12,0,0) };
        SettingsWindow.StyleButton(close);root.Controls.Add(close,0,5);
        AcceptButton = close;
        CancelButton = close;
        close.CausesValidation=false;
        close.Click+=(_,_)=>Close();
        SettingsWindow.FitScreen(this);
        ResumeLayout(true);
    }
}
