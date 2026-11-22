namespace AIBotBridge;

internal sealed class DesktopUpgradeConfirmation : Form
{
    private readonly Icon _appIcon = AppIcon.Load();
    private readonly Bitmap _logo;

    internal DesktopUpgradeConfirmation(string currentVersion, string newVersion)
    {
        Text = "软件更新";
        Icon = _appIcon;
        Font = SettingsWindow.BodyFont();
        BackColor = Color.White;
        ForeColor = SettingsWindow.Ink;
        AutoScaleDimensions = new(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = MaximizeBox = ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new(SizeType.AutoSize));
        var body = new TableLayoutPanel {
            AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2,
            Padding = new(24, 22, 24, 20), Margin = Padding.Empty
        };
        _logo = _appIcon.ToBitmap();
        var mark = new PictureBox {
            Image = _logo, SizeMode = PictureBoxSizeMode.Zoom, Size = new(36, 36),
            Anchor = AnchorStyles.Left, Margin = new(0, 0, 14, 0)
        };
        var title = new Label {
            Text = "安装新版 AI-bot？", AutoSize = true,
            Font = new Font(Font.FontFamily, 14, FontStyle.Bold), Margin = Padding.Empty
        };
        var version = new Label {
            Text = $"{currentVersion}  →  {newVersion}", AutoSize = true,
            ForeColor = SettingsWindow.Muted, Margin = new(0, 3, 0, 0),
            AccessibleName = $"当前版本 {currentVersion}，新版本 {newVersion}"
        };
        body.Controls.Add(mark, 0, 0);
        body.SetRowSpan(mark, 2);
        body.Controls.Add(title, 1, 0);
        body.Controls.Add(version, 1, 1);
        var description = new Label {
            Text = "AI-bot 会短暂退出，安装完成后自动打开。",
            AutoSize = true, ForeColor = SettingsWindow.Ink, Margin = new(0, 18, 0, 0)
        };
        body.Controls.Add(description, 0, 2);
        body.SetColumnSpan(description, 2);
        layout.Controls.Add(body, 0, 0);

        var footer = new TableLayoutPanel {
            AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 3, BackColor = SettingsWindow.Surface,
            Padding = new(24, 12, 24, 12), Margin = Padding.Empty
        };
        footer.ColumnStyles.Add(new(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new(SizeType.AutoSize));
        footer.ColumnStyles.Add(new(SizeType.AutoSize));
        var later = new Button { Text = "稍后", DialogResult = DialogResult.Cancel, Margin = new(0, 0, 8, 0) };
        var install = new Button { Text = "安装并重启", DialogResult = DialogResult.OK, Margin = Padding.Empty };
        SettingsWindow.StyleButton(later);
        SettingsWindow.StyleButton(install, true);
        later.MinimumSize = new(74, 32);
        install.MinimumSize = new(100, 32);
        footer.Controls.Add(later, 1, 0);
        footer.Controls.Add(install, 2, 0);
        layout.Controls.Add(footer, 0, 1);
        Controls.Add(layout);

        AcceptButton = install;
        CancelButton = later;
        ActiveControl = later;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _logo?.Dispose(); _appIcon.Dispose(); }
        base.Dispose(disposing);
    }
}
