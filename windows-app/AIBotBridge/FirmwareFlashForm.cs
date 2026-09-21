using System.Diagnostics;
using System.Security.Principal;
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

    internal FirmwareFlashForm(bool preview = false, string? preferredPort = null)
    {
        _preview = preview;
        _selection = new FlashDeviceSelection(preferredPort);
        Text = "AI-bot · 小屏刷机";
        using (var icon = typeof(FirmwareFlashForm).Assembly.GetManifestResourceStream("AIBotBridge.Assets.flash-icon.ico"))
            if (icon is not null) Icon = new Icon(icon);
        Font = new Font("Microsoft YaHei UI", 9);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(560, 245); MinimumSize = new Size(500, 250);
        BackColor = Color.White;
        ForeColor = Color.FromArgb(32, 44, 55);
        StartPosition = FormStartPosition.CenterScreen;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(22), ColumnCount = 1, RowCount = 7 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 6; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(new Label { Text = "小屏刷机", Font = new Font(Font.FontFamily, 14, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 16) });
        var portRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 3, BackColor = Color.FromArgb(240, 248, 247), Padding = new Padding(12, 8, 12, 8), Margin = new Padding(0, 0, 0, 18) };
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
        var actionRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        _start.ForeColor = Color.White; _start.FlatStyle = FlatStyle.Flat; _start.FlatAppearance.BorderSize = 0;
        _start.Padding = new Padding(16, 6, 16, 6); _start.Margin = new Padding(0, 0, 8, 0);
        actions.Controls.Add(_start); actions.Controls.Add(_cancel); actionRow.Controls.Add(actions); actionRow.Controls.Add(_more); root.Controls.Add(actionRow);
        _status.Margin = new Padding(0, 14, 0, 8); root.Controls.Add(_status);
        _progress.Visible = false; root.Controls.Add(_progress);
        var detailLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        detailLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var extra = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 12) };
        var backups = new Button { Text = "查看备份", AutoSize = true };
        foreach (var button in new[] { _refresh, _browse, _cancel, _backup, backups }) {
            button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = Color.FromArgb(216, 224, 230);
            button.BackColor = Color.White; button.ForeColor = ForeColor; button.Padding = new Padding(8, 3, 8, 3);
            button.Margin = new Padding(0, 0, 8, 0);
        }
        _refresh.Margin = _browse.Margin = Padding.Empty;
        _deviceDetails.ForeColor = Color.FromArgb(102, 114, 124); _deviceDetails.Margin = Padding.Empty;
        _details.Margin = new Padding(0, 10, 0, 0);
        backups.Click += (_, _) => { Directory.CreateDirectory(FirmwareFlasher.BackupDirectory); Process.Start(new ProcessStartInfo(FirmwareFlasher.BackupDirectory) { UseShellExecute = true }); };
        extra.Controls.Add(_backup); extra.Controls.Add(backups); detailLayout.Controls.Add(_deviceDetails); detailLayout.Controls.Add(extra); detailLayout.Controls.Add(_log);
        _details.Controls.Add(detailLayout); root.Controls.Add(_details); Controls.Add(root);
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
        else Shown += async (_, _) => { await RefreshDevicesAsync(); _deviceTimer.Start(); };
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
        ClientSize = new Size(Math.Min((int)(560 * scale), area.Width - 80), Math.Min((int)((_details.Visible ? 440 : 245) * scale), area.Height - 100));
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
        _start.BackColor = _start.Enabled ? Color.FromArgb(8, 127, 131) : Color.FromArgb(232, 237, 240);
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
        var port = selected.Port;
        _busy = true; _writing = false; _log.Clear(); _progress.Visible = true;
        LastSucceeded = false;
        using var cancellation = new CancellationTokenSource(); _cancelSource = cancellation;
        UpdateButtons();
        using var bridge = new Mutex(false, @"Local\AIBotBridge.Instance." + WindowsIdentity.GetCurrent().User!.Value);
        var ownsBridge = false; var restart = false; var touchedDevice = false;
        BridgeResumeTarget? resumeTarget = null;
        var work = Path.Combine(FirmwareFlasher.CacheDirectory, "firmware-" + Guid.NewGuid().ToString("N"));
        void Stage(string text) { _stageText = text; _lastProgressPercent = -1; _status.Text = text; _status.ForeColor = Color.DimGray; _progress.Value = 0; _progress.Style = ProgressBarStyle.Marquee; Log(text); }
        try
        {
            var firmware = backupOnly ? "" : FirmwareFlasher.PrepareFirmware(_firmwarePath, work);
            using var tool = await FirmwareFlasher.PrepareToolAsync(Stage, cancellation.Token);
            bool Claim() { try { return bridge.WaitOne(0); } catch (AbandonedMutexException) { return true; } }
            ownsBridge = Claim();
            if (!ownsBridge)
            {
                Stage("正在连接设备…");
                resumeTarget = BridgeResumeTarget.Capture();
                restart = true; BridgeLifetime.RequestExit();
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (!ownsBridge && DateTime.UtcNow < deadline) { await Task.Delay(200, cancellation.Token); ownsBridge = Claim(); }
                if (!ownsBridge) throw new IOException("桥接尚未退出，未进行刷机。请退出桥接后重试。");
            }
            var flasher = new FirmwareFlasher(async (args, token) => {
                FlashDeviceSelection.RequireSame(selected, await Task.Run(FlashDeviceDiscovery.Read, token));
                if (args.Contains("--port")) touchedDevice = true;
                return await FirmwareFlasher.RunProcessAsync(tool.Executable, args, Log, token);
            }, Stage);
            var backup = await flasher.ExecuteAsync(port, firmware, FirmwareFlasher.BackupDirectory, backupOnly,
                () => { _writing = true; UpdateButtons(); }, cancellation.Token, _acceptance);
            if (!backupOnly && !BridgeSettings.Load().SaveEditable(new Dictionary<string, string> {
                ["display_mode"] = "auto", ["display_cycle_enabled"] = "1"
            }, out var error)) throw new IOException("固件已写入并校验，但恢复自动轮播失败：" + error);
            _status.Text = backupOnly ? "备份完成" : resumeTarget is not null ? "刷机完成，小屏正在重新连接" : "刷机完成，请启动桥接连接小屏";
            _status.ForeColor = Color.FromArgb(18, 116, 94);
            Log("备份保存到：" + backup);
            _progress.Style = ProgressBarStyle.Continuous; _progress.Value = 100;
            LastSucceeded = true;
        }
        catch (OperationCanceledException) { _status.Text = cancellation.IsCancellationRequested ? "已取消，未写入新固件。重新开始会重新完整备份。" : "下载工具超时，未写入新固件。请检查网络后重试。"; }
        catch (Exception ex) { _status.Text = "操作未完成：" + ex.Message; Log(ex.Message); }
        finally
        {
            if (ownsBridge && touchedDevice && !_writing && !LastSucceeded)
            {
                // A killed read process cannot run esptool's normal RTS reset.
                // Reset only after read/check failures, never after a partial write.
                try {
                    await Task.Run(() => {
                        FlashDeviceSelection.RequireSame(selected, FlashDeviceDiscovery.Read());
                        using var serial = new System.IO.Ports.SerialPort(port, 460800) { DtrEnable = false, RtsEnable = false };
                        serial.Open(); serial.RtsEnable = true; Thread.Sleep(100); serial.RtsEnable = false;
                    });
                    Log("已复位小屏，正在恢复连接。");
                } catch (Exception ex) { Log("自动复位未完成，请拔下 USB 后重新插入：" + ex.Message); }
            }
            if (ownsBridge) bridge.ReleaseMutex();
            if (restart && resumeTarget is not null) { try { resumeTarget.Restore(); } catch (Exception ex) {
                LastSucceeded = false; _status.Text = "桥接未能恢复，请手动启动原桥接程序"; Log("请手动启动桥接：" + ex.Message);
            } }
            _busy = false; _writing = false; _cancelSource = null;
            if (_progress.Style == ProgressBarStyle.Marquee) { _progress.Style = ProgressBarStyle.Continuous; _progress.Value = 0; }
            UpdateButtons();
            // Only this operation's copied firmware is temporary; never delete backups.
            try { FirmwareFlasher.CleanupTemporaryDirectory(work, "firmware-"); } catch (IOException ex) { Log("临时文件清理未完成：" + ex.Message); }
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
