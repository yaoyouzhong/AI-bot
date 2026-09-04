namespace AIBotBridge;

internal sealed class DeviceControlForm : Form
{
    private readonly SerialPublisher _serial;
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly TrackBar _brightness = new() { Minimum = 0, Maximum = 100, TickFrequency = 10, Value = 100, Width = 260 };
    private readonly Label _brightnessValue = new() { AutoSize = true, Text = "100%" };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.DimGray };

    internal DeviceControlForm(SerialPublisher serial)
    {
        _serial = serial;
        Text = "AI-bot 设备控制";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(340, 250);

        _mode.Items.AddRange(new object[]
        {
            new ModeItem("自动轮播", "auto"), new ModeItem("Claude + Codex", "dual"),
            new ModeItem("天气", "weather"), new ModeItem("股票", "stocks"),
            new ModeItem("账户额度", "quotas"), new ModeItem("国产额度", "domestic"),
            new ModeItem("系统监控", "system"), new ModeItem("音乐", "music"),
            new ModeItem("桌宠", "pet"), new ModeItem("屏保", "screensaver")
        });
        _mode.SelectedIndex = 0;
        _brightness.Scroll += (_, _) => _brightnessValue.Text = _brightness.Value + "%";

        var applyMode = new Button { Text = "应用模式", AutoSize = true };
        applyMode.Click += (_, _) => ApplyMode();
        var applyBrightness = new Button { Text = "应用亮度", AutoSize = true };
        applyBrightness.Click += (_, _) => ApplyBrightness();

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(22, 18, 22, 18)
        };
        layout.Controls.Add(new Label { Text = "显示模式", AutoSize = true });
        layout.Controls.Add(_mode);
        layout.Controls.Add(applyMode);
        layout.Controls.Add(new Label { Text = "屏幕亮度", AutoSize = true, Margin = new Padding(3, 14, 3, 0) });
        var brightnessRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        brightnessRow.Controls.Add(_brightness);
        brightnessRow.Controls.Add(_brightnessValue);
        layout.Controls.Add(brightnessRow);
        layout.Controls.Add(applyBrightness);
        layout.Controls.Add(_status);
        Controls.Add(layout);
    }

    private void ApplyMode()
    {
        var item = (ModeItem)_mode.SelectedItem!;
        SetStatus(_serial.SendDisplayMode(item.Value), "显示模式已发送。", "设备尚未通过 USB 连接。");
    }

    private void ApplyBrightness() =>
        SetStatus(_serial.SendBrightness(_brightness.Value), "亮度已发送并由设备保存。", "设备尚未通过 USB 连接。");

    private void SetStatus(bool success, string successText, string errorText)
    {
        _status.ForeColor = success ? Color.ForestGreen : Color.Firebrick;
        _status.Text = success ? successText : errorText;
    }

    private sealed record ModeItem(string Label, string Value)
    {
        public override string ToString() => Label;
    }
}
