namespace AIBotBridge;

internal sealed class DeviceControlForm : Form
{
    private readonly SerialPublisher _serial;
    private readonly Action<string> _selectMode;
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly TrackBar _brightness = new() { Minimum = 0, Maximum = 100, TickFrequency = 10, Value = 100, Width = 260 };
    private readonly Label _brightnessValue = new() { AutoSize = true, Text = "100%" };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.DimGray };

    internal DeviceControlForm(SerialPublisher serial, Action<string> selectMode, string selectedMode)
    {
        SuspendLayout();
        _serial = serial;
        _selectMode = selectMode;
        Text = "AI-bot 设备控制";
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Microsoft YaHei UI", 9F);
        MinimumSize = new Size(400, 350);
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(440, 380);

        _mode.Items.Add(new ModeItem("智能跟随 / 轮播", "auto"));
        foreach (var page in DisplayModes.Pages) _mode.Items.Add(new ModeItem(page.Label, page.Mode));
        _mode.Items.Add(new ModeItem("屏保", "screensaver"));
        _mode.SelectedIndex = Math.Max(0, _mode.Items.Cast<ModeItem>().ToList().FindIndex(item => item.Value == selectedMode));
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
        layout.AutoScroll = true;
        Controls.Add(layout);
        SettingsWindow.FitFlow(layout);
        brightnessRow.AutoSize = false;
        brightnessRow.Height = 52;
        brightnessRow.SizeChanged += (_, _) => _brightness.Width = Math.Max(120,
            brightnessRow.ClientSize.Width - _brightnessValue.PreferredWidth - 20);
        foreach (var button in new[] { applyMode, applyBrightness }) SettingsWindow.StyleButton(button);
        SettingsWindow.FitScreen(this);
        ResumeLayout(true);
    }

    private void ApplyMode()
    {
        var item = (ModeItem)_mode.SelectedItem!;
        // Use the same persistent policy as the tray, otherwise the next status
        // heartbeat would undo this control command.
        _selectMode(item.Value);
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
