using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AIBotBridge;

internal sealed class FirmwareFlashForm : Form
{
    private readonly Label _deviceLabel = new() { Text = "正在识别小屏…", AutoSize = true, AccessibleName = "小屏连接状态", Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 12, 0) };
    private readonly Label _deviceDetails = new() { AutoSize = true, Dock = DockStyle.Top };
    private readonly FlashDeviceSelection _selection;
    private IReadOnlyList<FlashUsbDevice> _devices = [];
    private readonly System.Windows.Forms.Timer _deviceTimer = new() { Interval = 750 };
    private bool _scanning;
    private readonly Button _refresh = new() { Text = "刷新", AutoSize = true, Anchor = AnchorStyles.Right };
    private readonly TextBox _firmware = new() { ReadOnly = true, Dock = DockStyle.Fill, AccessibleName = "固件文件" };
    private readonly Button _browse = new() { Text = "浏览…", AutoSize = true };
    private readonly Button _start = new() { Text = "开始刷机", AutoSize = true, Enabled = false };
    private readonly Button _backup = new() { Text = "只备份设备", AutoSize = true };
    private readonly Button _cancel = new() { Text = "取消", AutoSize = true, Enabled = false };
    private readonly Label _status = new() { Text = "", AutoSize = true, Dock = DockStyle.Fill, ForeColor = Color.DimGray };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Height = 6, Margin = new Padding(0, 0, 0, 8) };
    private readonly TextBox _log = new() { Multiline = true, ReadOnly = true, WordWrap = false, ScrollBars = ScrollBars.Both, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(246, 248, 250), ForeColor = Color.FromArgb(86, 99, 111), Dock = DockStyle.Fill, Font = new Font("Microsoft YaHei UI", 8.5f), AccessibleName = "刷机详细记录" };
    private string _firmwarePath = "";
    private readonly bool _preview;
    private readonly bool _bridgeRunning;
    private readonly Panel _details = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly LinkLabel _more = new() { Text = "更多选项 ▾", AutoSize = true, Anchor = AnchorStyles.Right, LinkBehavior = LinkBehavior.NeverUnderline, LinkColor = Color.FromArgb(86, 99, 111) };
    private bool _busy;
    private bool _writing;
    private string _stageText = "";
    private long _lastProgressUpdate;
    private int _lastProgressPercent = -1;
    private CancellationTokenSource? _cancelSource;
    private bool _acceptance;
    private string? _evidencePath;
    internal bool LastSucceeded { get; private set; }

    // Explicit hardware acceptance entry: uses this window's normal discovery,
    // button handler, bridge handoff and pipeline. No alternate flashing path.
    internal void AcceptCurrentFirmware(string firmware, string evidencePath)
    {
        _acceptance = true; _evidencePath = evidencePath;
        _firmwarePath = Path.GetFullPath(firmware); _firmware.Text = Path.GetFileName(firmware);
        Shown += async (_, _) => {
            while (_scanning) await Task.Delay(50);
            await RefreshDevicesAsync();
            if (_selection.Selected is null) { File.WriteAllText(evidencePath, "FAILED: no identified screen"); Environment.ExitCode = 1; Close(); return; }
            UpdateButtons(); _start.PerformClick();
        };
    }

    internal FirmwareFlashForm(bool preview = false, string? preferredPort = null, string? preparedFirmware = null, bool bridgeRunning = false, string? expectedUsbIdentity = null, bool startAutomatically = false)
    {
        SuspendLayout();_preview = preview;_bridgeRunning=bridgeRunning;
        _selection = new FlashDeviceSelection(preferredPort,expectedUsbIdentity);
        Text = "AI-bot · 小屏刷机";
        using (var icon = typeof(FirmwareFlashForm).Assembly.GetManifestResourceStream("AIBotBridge.Assets.flash-icon.ico"))
            if (icon is not null) Icon = new Icon(icon);
        Font = SettingsWindow.BodyFont();
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(560, 300); MinimumSize = new Size(500, 290);
        BackColor = Color.White;
        ForeColor = Color.FromArgb(32, 44, 55);
        StartPosition = FormStartPosition.CenterScreen;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24), ColumnCount = 1, RowCount = 7 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 6; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(new Label { Text = "小屏刷机", Font = SettingsWindow.TitleFont(), AutoSize = true, Margin = new Padding(0, 0, 0, 16) });
        var portRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 3, BackColor = SettingsWindow.Surface, Padding = new Padding(12, 8, 12, 8), Margin = new Padding(0, 0, 0, 18) };
        portRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        portRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        portRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        portRow.Controls.Add(new Label { Text = "设备", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.DimGray, Margin = new Padding(0, 0, 16, 0) });
        portRow.Controls.Add(_deviceLabel); portRow.Controls.Add(_refresh); root.Controls.Add(portRow);
        var fileGroup = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, Margin = new Padding(0, 0, 0, 18) };
        fileGroup.Controls.Add(new Label { Text = "固件", AutoSize = true, ForeColor = Color.FromArgb(86, 99, 111), Margin = new Padding(0, 0, 0, 6) });
        var fileRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _firmware.BackColor = Color.White; _firmware.BorderStyle = BorderStyle.FixedSingle; _firmware.Margin = new Padding(0, 5, 10, 0);
        _browse.Text = "选择文件";
        fileRow.Controls.Add(_firmware); fileRow.Controls.Add(_browse); fileGroup.Controls.Add(fileRow); root.Controls.Add(fileGroup);
        _firmware.PlaceholderText = "选择固件";
        if(preparedFirmware is not null){_firmwarePath=Path.GetFullPath(preparedFirmware);_firmware.Text=Path.GetFileName(preparedFirmware);_status.Text="更新已下载并校验。连接小屏后点击开始刷机；先备份，再写入。";}
        var actionRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        _start.ForeColor = Color.White; _start.FlatStyle = FlatStyle.Flat; _start.FlatAppearance.BorderSize = 0;
        _start.Padding = new Padding(16, 6, 16, 6); _start.Margin = new Padding(0, 0, 8, 0);
        actions.Controls.Add(_start); actions.Controls.Add(_cancel); actionRow.Controls.Add(actions); actionRow.Controls.Add(_more); root.Controls.Add(actionRow);
        foreach(var button in new[]{_refresh,_browse,_start,_backup,_cancel})SettingsWindow.StyleButton(button,button==_start);
        SettingsWindow.FitScreen(this);
        _status.Margin = new Padding(0, 14, 0, 8); root.Controls.Add(_status);
        _progress.Visible = false; root.Controls.Add(_progress);
        var detailLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        detailLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var extra = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 12) };
        var backups = new Button { Text = "查看备份", AutoSize = true };
        foreach (var button in new[] { _refresh, _browse, _cancel, _backup, backups }) {
            button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = Color.FromArgb(216, 224, 230);
            button.BackColor = Color.White; button.ForeColor = ForeColor; SettingsWindow.StyleButton(button);
            button.Margin = new Padding(0, 0, 8, 0);
        }
        _refresh.Margin = _browse.Margin = Padding.Empty;
        _deviceDetails.ForeColor = Color.FromArgb(102, 114, 124); _deviceDetails.Margin = Padding.Empty;
        _details.Margin = new Padding(0, 10, 0, 0);
        backups.Click += (_, _) => { Directory.CreateDirectory(FirmwareFlasher.BackupDirectory); Process.Start(new ProcessStartInfo(FirmwareFlasher.BackupDirectory) { UseShellExecute = true }); };
        extra.Controls.Add(_backup); extra.Controls.Add(backups); detailLayout.Controls.Add(_deviceDetails); detailLayout.Controls.Add(extra); detailLayout.Controls.Add(_log);
        _details.Controls.Add(detailLayout); root.Controls.Add(_details); Controls.Add(root);ResumeLayout(true);
        _more.LinkClicked += (_, _) => { _details.Visible = !_details.Visible; _more.Text = _details.Visible ? "收起选项 ▴" : "更多选项 ▾"; ResizeForDetails(); };
        _refresh.Click += async (_, _) => await RefreshDevicesAsync();
        _deviceTimer.Tick += async (_, _) => await RefreshDevicesAsync();
        FormClosed += (_, _) => _deviceTimer.Dispose();
        _browse.Click += (_, _) => {
            using var dialog = new OpenFileDialog { Title = "选择 AI-bot 固件材料 ZIP 或 firmware.bin", Filter = "AI-bot 固件|*.zip;*.bin" };
            if (dialog.ShowDialog(this) == DialogResult.OK) { _firmwarePath = dialog.FileName; _firmware.Text = Path.GetFileName(dialog.FileName); UpdateButtons(); }
        };
        _start.Click += async (_, _) => await RunAsync(false);
        _backup.Click += async (_, _) => await RunAsync(true);
        _cancel.Click += (_, _) => _cancelSource?.Cancel();
        FormClosing += (_, e) => { if (_busy) { e.Cancel = true; _status.Text = _writing ? "正在写入或校验，请保持连接，完成后再关闭。" : "请先取消并等待操作结束，再关闭窗口。"; } };
        if (preview) { _deviceLabel.Text = "USB 设备已连接"; _deviceLabel.ForeColor = Color.FromArgb(18, 116, 94); _firmware.Text = "firmware.bin"; UpdateButtons(); }
        else Shown += async (_, _) => {
            await RefreshDevicesAsync();
            if (IsDisposed) return;
            _deviceTimer.Start();
            if (startAutomatically && preparedFirmware is not null && expectedUsbIdentity is not null &&
                _selection.Selected is { } device && device.Identity == expectedUsbIdentity && device.Port == preferredPort)
                await RunAsync(false);
        };
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ResizeForDetails();
    }

    internal void ShowCompletedPreview()
    {
        if (!_preview) throw new InvalidOperationException("Preview only");
        _details.Visible = true; _more.Text = "收起选项 ▴";
        _firmware.Text = ""; _start.Enabled = false; _start.BackColor = Color.FromArgb(232, 237, 240);
        _deviceDetails.Text = "COM7 · USB-SERIAL CH340";
        _status.Text = "备份完成"; _status.ForeColor = Color.FromArgb(18, 116, 94);
        _progress.Visible = true; _progress.Value = 100;
        _log.Text = "正在备份（4 MB）…\r\n95%\r\n100%\r\n备份校验通过\r\n已恢复设备连接";
        ResizeForDetails();
    }

    private void ResizeForDetails()
    {
        var scale = DeviceDpi / 96f;
        var area = Screen.FromControl(this).WorkingArea;
        ClientSize = new Size(Math.Min((int)(560 * scale), area.Width - 80), Math.Min((int)((_details.Visible ? 480 : 300) * scale), area.Height - 100));
    }

    private async Task RefreshDevicesAsync()
    {
        if (_preview || _busy || _scanning || IsDisposed) return;
        _scanning = true;
        try
        {
            var devices = await Task.Run(FlashDeviceDiscovery.Read);
            if (IsDisposed || _busy) return;
            _devices = devices; _selection.Update(devices); ShowSelection();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            if (!IsDisposed && !_busy) { _selection.ScanFailed(); ShowSelection(); _deviceDetails.Text = "设备扫描：" + ex.Message; }
        }
        finally { _scanning = false; }
    }

    private void ShowSelection()
    {
        _deviceLabel.Text = _selection.Message;
        _deviceLabel.ForeColor = _selection.Selected is null ? Color.DimGray : Color.FromArgb(18, 116, 94);
        _deviceDetails.Text = _selection.Selected is { } device ? $"{device.Port} · {device.Name}" : "未选择设备";
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        _refresh.Enabled = _browse.Enabled = !_busy;
        _backup.Enabled = !_busy && (_preview || _selection.Selected is not null);
        _start.Enabled = _backup.Enabled && (_preview || File.Exists(_firmwarePath));
        _start.BackColor = _start.Enabled ? SettingsWindow.Accent : Color.FromArgb(232, 237, 240);
        _start.ForeColor = _start.Enabled ? Color.White : Color.FromArgb(132, 143, 151);
        _start.Text = _busy ? "处理中…" : "开始刷机";
        _cancel.Enabled = _busy && !_writing; _cancel.Visible = _busy;
    }

    private void Log(string line)
    {
        if (IsDisposed) return;
        var stage = _stageText;
        BeginInvoke(() => {
            var percent = Regex.Match(line, @"(\d+)\s*%");
            if (_busy && stage == _stageText && percent.Success) {
                var value = Math.Clamp(int.Parse(percent.Groups[1].Value), 0, 100);
                var now = Stopwatch.GetTimestamp();
                var show = value != _lastProgressPercent &&
                    (_lastProgressPercent < 0 || value == 100 || Stopwatch.GetElapsedTime(_lastProgressUpdate, now).TotalSeconds >= 1);
                // Suppress only repetitive progress records, never diagnostic lines.
                if (!show && Regex.IsMatch(line, @"^\s*\d+\s*\(\d+\s*%\)\s*$")) return;
                if (show) {
                _lastProgressPercent = value; _lastProgressUpdate = now;
                _progress.Style = ProgressBarStyle.Continuous;
                _progress.Value = value;
                _status.Text = _stageText.TrimEnd('…', '。') + $" · {_progress.Value}%";
                }
            }
            if (_log.TextLength > 120_000) _log.Clear();
            _log.AppendText(line + Environment.NewLine);
        });
    }

    private async Task RunAsync(bool backupOnly)
    {
        if (_preview || _busy || _selection.Selected is not { } selected) return;
        _busy = true; _writing = false; _log.Clear(); _progress.Visible = true;
        LastSucceeded = false;
        using var cancellation = new CancellationTokenSource(); _cancelSource = cancellation;
        UpdateButtons();
        void Stage(string text) { _stageText = text; _lastProgressPercent = -1; _status.Text = text; _status.ForeColor = Color.DimGray; _progress.Value = 0; _progress.Style = ProgressBarStyle.Marquee; Log(text); }
        try
        {
            var (backup, resumed) = await FirmwareFlasher.RunUsbAsync(selected, backupOnly ? null : _firmwarePath, _bridgeRunning, _acceptance,
                Stage, (exe, args, token) => FirmwareFlasher.RunProcessAsync(exe, args, Log, token),
                () => { _writing = true; UpdateButtons(); }, Log, cancellation.Token);
            _status.Text = backupOnly ? "备份完成" : resumed ? "刷机完成，小屏正在重新连接" : "刷机完成，请启动桥接连接小屏";
            _status.ForeColor = Color.FromArgb(18, 116, 94);
            Log("备份保存到：" + backup);
            _progress.Style = ProgressBarStyle.Continuous; _progress.Value = 100;
            LastSucceeded = true;
        }
        catch (OperationCanceledException) { _status.Text = cancellation.IsCancellationRequested ? "已取消，未写入新固件。重新开始会重新完整备份。" : "下载工具超时，未写入新固件。请检查网络后重试。"; }
        catch (Exception ex) { _status.Text = "操作未完成：" + ex.Message; Log(ex.Message); }
        finally
        {
            _busy = false; _writing = false; _cancelSource = null;
            if (_progress.Style == ProgressBarStyle.Marquee) { _progress.Style = ProgressBarStyle.Continuous; _progress.Value = 0; }
            UpdateButtons();
            if (_acceptance && _evidencePath is not null)
            {
                // Let queued tool-output callbacks reach the log before recording it.
                await Task.Delay(150);
                File.WriteAllText(_evidencePath, (LastSucceeded ? "FORM_ACCEPTANCE_OK" : "FORM_ACCEPTANCE_FAILED") + Environment.NewLine + _status.Text + Environment.NewLine + _log.Text);
                using var bitmap = new Bitmap(Width, Height);
                DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size)); bitmap.Save(_evidencePath + ".png");
                Environment.ExitCode = LastSucceeded ? 0 : 1;
                Close();
            }
        }
    }
}
