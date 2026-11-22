namespace AIBotBridge;

internal sealed class DesktopUpgradeConfirmation : Form
{
    internal DesktopUpgradeConfirmation(string currentVersion, string newVersion)
    {
        Text = "升级 AI-bot";
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

        var content = new TableLayoutPanel { AutoSize = true, Padding = new(20), ColumnCount = 1 };
        content.Controls.Add(new Label { Text = $"从 {currentVersion} 升级到 {newVersion}？",
            Font = SettingsWindow.SectionFont(), AutoSize = true });
        content.Controls.Add(new Label { Text = "确认后将下载安装包。安装时，AI-bot 会自动退出并重新打开。",
            MaximumSize = new(350, 0), AutoSize = true, Margin = new(3, 10, 3, 18) });
        var actions = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right, WrapContents = false };
        var later = new Button { Text = "稍后", DialogResult = DialogResult.Cancel };
        var upgrade = new Button { Text = "开始升级", DialogResult = DialogResult.OK };
        SettingsWindow.StyleButton(later);
        SettingsWindow.StyleButton(upgrade, true);
        actions.Controls.AddRange([later, upgrade]);
        content.Controls.Add(actions);
        Controls.Add(content);
        CancelButton = later;
        AcceptButton = upgrade;
        ActiveControl = later;
    }
}
