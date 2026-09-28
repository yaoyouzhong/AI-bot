using System.Globalization;

namespace AIBotBridge;

internal sealed class SettingsForm : Form
{
    private readonly BridgeSettings _settings;
    private readonly bool _includeDeviceSettings;
    private readonly TextBox _city = new() { Width = 400 };
    private readonly TextBox _latitude = new() { Width = 150 };
    private readonly TextBox _longitude = new() { Width = 150 };
    private readonly TextBox _stocks = new() { Width = 400 };
    private readonly NumericUpDown _screenSaver = new() { Minimum = 0, Maximum = 1440, Width = 100 };
    private readonly TextBox _serialPort = new() { Width = 120, CharacterCasing = CharacterCasing.Upper };

    internal SettingsForm(BridgeSettings settings,bool includeDeviceSettings=true)
    {
        SuspendLayout();
        _settings = settings;_includeDeviceSettings=includeDeviceSettings;
        Text = "AI-bot 设置";
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Microsoft YaHei UI", 9F);
        MinimumSize = new Size(540, 400);
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(640, 450);

        _city.Text = settings.Get("weather_city");
        _latitude.Text = settings.Get("weather_latitude");
        _longitude.Text = settings.Get("weather_longitude");
        _stocks.Text = settings.Get("stock_symbols", "sh000001");
        if (int.TryParse(settings.Get("screensaver_timeout_minutes"), out var minutes))
            _screenSaver.Value = Math.Clamp(minutes, 0, 1440);
        _serialPort.Text = settings.Get("serial_port");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 2,
            RowCount = 7,
            AutoScroll = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(layout, 0, "天气城市", _city);
        AddRow(layout, 1, "纬度（可空）", _latitude);
        AddRow(layout, 2, "经度（可空）", _longitude);
        AddRow(layout, 3, "股票代码（逗号分隔）", _stocks);
        if(includeDeviceSettings)AddRow(layout, 4, "屏保等待（分钟）", _screenSaver);
        if(includeDeviceSettings)AddRow(layout, 5, "串口（空白为自动）", _serialPort);

        var note = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            ForeColor = Color.DimGray,
            Text = includeDeviceSettings?"股票最多 20 个；屏保 0 为关闭。串口更改后停用并启用设备生效。":"股票最多 20 个；账号与数据源由所有设备共享。"
        };
        layout.Controls.Add(note, 1, 6);
        note.Dock = DockStyle.Fill;
        layout.SizeChanged += (_, _) => note.MaximumSize = new Size(Math.Max(120,
            layout.ClientSize.Width - layout.Padding.Horizontal - (int)layout.ColumnStyles[0].Width - note.Margin.Horizontal), 0);

        var save = new Button { Text = "保存", AutoSize = true };
        var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => SaveAndClose();
        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        var root = new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        buttons.Dock=DockStyle.Fill;buttons.Padding=new Padding(18,8,18,12);
        SettingsWindow.StyleButton(save,true);SettingsWindow.StyleButton(cancel);
        root.Controls.Add(layout,0,0);root.Controls.Add(buttons,0,1);Controls.Add(root);
        AcceptButton = save;
        CancelButton = cancel;
        cancel.CausesValidation=false;
        cancel.Click+=(_,_)=>Close();
        SettingsWindow.FitScreen(this);
        ResumeLayout(true);
    }

    internal void FocusSection(string? section)
    {
        if (section == "stocks") { _stocks.Focus(); _stocks.SelectAll(); }
        if (section == "weather") { _city.Focus(); _city.SelectAll(); }
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(3, 8, 3, 8);
        layout.Controls.Add(control, 1, row);
    }

    private void SaveAndClose()
    {
        if (!TryCoordinate(_latitude.Text, -90, 90, out var latitude) ||
            !TryCoordinate(_longitude.Text, -180, 180, out var longitude) ||
            (latitude is null) != (longitude is null))
        {
            Warn("经纬度必须同时留空，或填写有效数字：纬度 -90~90、经度 -180~180。");
            return;
        }

        var rawSymbols = _stocks.Text.Replace('，', ',')
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var symbols = rawSymbols.Select(StockService.Normalize).ToArray();
        if (rawSymbols.Length > 20 || symbols.Any(string.IsNullOrEmpty))
        {
            Warn("股票代码最多 20 个，只支持 sh、sz、bj、hk、us 市场前缀；A 股可直接填写 6 位代码。");
            return;
        }

        var port = _serialPort.Text.Trim().ToUpperInvariant();
        if (port.Length > 0 && !(port.Length > 3 && port.StartsWith("COM", StringComparison.Ordinal) &&
                               port[3..].All(char.IsDigit)))
        {
            Warn("串口必须留空或填写 COM 后跟数字，例如 COM7。");
            return;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["weather_city"] = _city.Text.Trim(),
            ["weather_latitude"] = latitude?.ToString("0.######", CultureInfo.InvariantCulture) ?? string.Empty,
            ["weather_longitude"] = longitude?.ToString("0.######", CultureInfo.InvariantCulture) ?? string.Empty,
            ["stock_symbols"] = string.Join(',', symbols.Distinct(StringComparer.OrdinalIgnoreCase)),
            ["screensaver_timeout_minutes"] = decimal.ToInt32(_screenSaver.Value).ToString(CultureInfo.InvariantCulture),
            ["serial_port"] = port
        };
        if(!_includeDeviceSettings){values.Remove("screensaver_timeout_minutes");values.Remove("serial_port");}
        if (!_settings.SaveEditable(values, out var error))
        {
            Warn(error);
            return;
        }

        MessageBox.Show("设置已保存。", "AI-bot",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }

    private static bool TryCoordinate(string text, double minimum, double maximum, out double? value)
    {
        var candidate = text.Trim();
        if (candidate.Length == 0)
        {
            value = null;
            return true;
        }
        if (double.TryParse(candidate, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
            parsed >= minimum && parsed <= maximum)
        {
            value = parsed;
            return true;
        }
        value = null;
        return false;
    }

    private static void Warn(string message) => MessageBox.Show(message, "AI-bot 设置",
        MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
