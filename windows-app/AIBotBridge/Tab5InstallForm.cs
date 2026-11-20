namespace AIBotBridge;

internal sealed class Tab5InstallForm : Form
{
    private const string WriteVerified = "写入校验通过，原固件已备份。";
    private readonly Tab5Service _service;
    private readonly bool _preview;
    private readonly bool _returnToAdd;
    private readonly ComboBox _ports = new() { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name", AccessibleName = "目标 TAB5 USB 端口", Margin = new(0, 0, 0, 12) };
    private readonly Label _heading = new() { AutoSize = true, Margin = new(0, 0, 0, 8), Font = SettingsWindow.TitleFont() };
    private readonly Label _instruction = new() { AutoSize = true, Margin = new(0, 0, 0, 12) };
    private readonly Label _usbStatus = new() { AutoSize = true, ForeColor = SettingsWindow.Muted, Margin = new(0, 0, 0, 8) };
    private readonly Label _status = new() { AutoSize = true, Visible = false, Margin = new(0, 2, 0, 4) };
    private readonly Label _backup = new() { AutoSize = true, Visible = false, ForeColor = SettingsWindow.Muted, Margin = new(0, 4, 0, 8) };
    private readonly Button _install = Button("安装 AI-bot", true), _check = Button("检查启动", true),
        _done = Button("进入 USB 配对", true), _cancel = Button("关闭");
    private readonly LinkLabel _local = new() { Text = "使用本地 ZIP…", AutoSize = true, Margin = new(0, 4, 16, 2) };
    private readonly LinkLabel _restore = new() { Text = "恢复备份…", AutoSize = true, Margin = new(0, 4, 8, 2) };
    private readonly FlowLayoutPanel _options;
    private readonly System.Windows.Forms.Timer _scan = new() { Interval = 1200 };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Height = 8, Visible = false, Style = ProgressBarStyle.Marquee };
    private CancellationTokenSource? _operation;
    private string? _mac, _version, _elfSha, _selectedIdentity;
    private bool _busy, _writing, _downloadFailed, _hasRecoveryBackup;
    private FlashUsbDevice[] _devices = [];
    private string _stageText = "";
    private int _lastPercent = -1;
    internal FlashUsbDevice? VerifiedDevice { get; private set; }

    internal Tab5InstallForm(Tab5Service service, bool preview = false, bool returnToAdd = false, bool previewRestart = false)
    {
        _service = service; _preview = preview; _returnToAdd = returnToAdd;
        if (returnToAdd) _check.Text = "验证并添加";
        if (!preview && returnToAdd) {
            try { _hasRecoveryBackup = Directory.EnumerateFiles(FirmwareFlasher.BackupDirectory, "tab5-*.bin.json").Any(); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        if (preview && previewRestart) { _mac = "preview"; _status.Text = WriteVerified; _status.Visible = true; }
        Text = "TAB5 首次安装"; Font = SettingsWindow.BodyFont();
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(516, 300); MinimumSize = new Size(465, 354); StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        BackColor = Color.White;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20, 18, 20, 16), ColumnCount = 1, RowCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);
        var content = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Margin = Padding.Empty };
        layout.Controls.Add(content, 0, 0); SettingsWindow.FitFlow(content);
        _options = Row(FlowDirection.LeftToRight, _local, _restore);
        content.Controls.AddRange([_heading, _instruction, _usbStatus, _ports, _status, _backup, _options]);
        layout.Controls.Add(_progress, 0, 1);
        layout.Controls.Add(Row(FlowDirection.RightToLeft, _install, _check, _done, _cancel), 0, 2);
        _ports.SelectionChangeCommitted += (_, _) => {
            _selectedIdentity = (_ports.SelectedItem as FlashUsbDevice)?.Identity;
            UpdateUsbStatus();
            UpdateButtons();
        };
        _install.Click += async (_, _) => await RunInstallAsync(null, null);
        _local.LinkClicked += async (_, _) => {
            using var pick = new OpenFileDialog { Title = "选择 TAB5 首次安装包", Filter = "TAB5 首次安装包 (*.zip)|*.zip" };
            if (pick.ShowDialog(this) == DialogResult.OK) await RunInstallAsync(pick.FileName, null);
        };
        _restore.LinkClicked += async (_, _) => {
            using var pick = new OpenFileDialog { Title = "选择本工具保存的原固件备份记录", Filter = "TAB5 备份记录 (*.bin.json)|*.bin.json", InitialDirectory = FirmwareFlasher.BackupDirectory };
            if (pick.ShowDialog(this) == DialogResult.OK) await RunInstallAsync(null, pick.FileName);
        };
        _check.Click += async (_, _) => await CheckBootAsync();
        _done.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        _cancel.Click += (_, _) => { if (_busy) _operation?.Cancel(); else Close(); };
        FormClosing += (_, e) => { if (_busy) { e.Cancel = true; if (!_writing) _operation?.Cancel(); } };
        _scan.Tick += (_, _) => RefreshDevices();
        Shown += (_, _) => { if (!_preview) _scan.Start(); };
        CancelButton = _cancel;
        RefreshDevices();
        SettingsWindow.FitScreen(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _scan.Dispose();
        base.Dispose(disposing);
    }

    private static Button Button(string text, bool primary = false) { var b = new Button { Text = text }; SettingsWindow.StyleButton(b, primary); return b; }
    private static FlowLayoutPanel Row(FlowDirection direction, params Control[] controls) {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = direction, Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 0) };
        foreach (var control in controls) {
            if (control is Button) control.Margin = new Padding(8, 0, 0, 0);
            row.Controls.Add(control);
        }
        return row;
    }

    private void RefreshDevices(bool reselection = false)
    {
        if (_busy || _ports.DroppedDown) return;
        try {
            var devices = _preview ? [new FlashUsbDevice("COM7", @"USB\VID_303A&PID_1001\PREVIEW", "TAB5 USB (COM7)")] :
                _service.UsbDevices().Where(d => d.Identity.Contains("VID_303A", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (reselection || !_devices.SequenceEqual(devices)) {
                _devices = devices;
                _ports.DataSource = devices;
                int selected = Array.FindIndex(devices, d => d.Identity.Equals(_selectedIdentity, StringComparison.OrdinalIgnoreCase));
                _ports.SelectedIndex = selected >= 0 ? selected : (_selectedIdentity is null && devices.Length == 1 ? 0 : -1);
            }
            UpdateUsbStatus();
        } catch (Exception ex) {
            _devices = [];
            _ports.DataSource = null;
            _usbStatus.Text = "USB 检测失败，正在重试。 " + ex.Message;
        }
        UpdateButtons();
    }

    private void UpdateUsbStatus()
    {
        _usbStatus.Text = _ports.SelectedItem is FlashUsbDevice device ? $"已检测到 USB 设备（{device.Port}）" :
            _devices.Length == 0 ? "正在检测 USB 设备…" : "请选择这台 TAB5 的 USB 端口。";
    }

    private void UpdateButtons() {
        bool device = _ports.SelectedItem is FlashUsbDevice;
        bool restart = _mac is not null;
        bool complete = VerifiedDevice is not null;
        _heading.Text = complete ? "启动检查通过" : restart ? "重启 TAB5" : _busy ? "安装 TAB5" : "连接 TAB5";
        _instruction.Visible = !_busy && !complete;
        _instruction.Text = restart ? "短按 RESET。\n等屏幕显示 AI-bot。" : "接好 USB-C 数据线。\n长按 RESET，绿灯快闪时松开。";
        _usbStatus.Visible = !_busy && !complete;
        _ports.Visible = !_busy && !complete && (_devices.Length > 1 || _devices.Length == 1 && !device);
        _install.Visible = !restart && !complete;
        _install.Enabled = !_busy && device;
        bool showLocal = !_busy && !restart && (!_returnToAdd || _downloadFailed);
        bool showRestore = !_busy && (!_returnToAdd || !restart && _hasRecoveryBackup);
        _options.Visible = showLocal || showRestore;
        _local.Visible = showLocal;
        _local.Enabled = !_preview && device;
        _restore.Visible = showRestore;
        _restore.Enabled = !_preview && !_busy && device;
        _check.Visible = restart && !complete;
        _check.Enabled = !_busy && device && restart;
        _done.Visible = !_returnToAdd && complete;
        _done.Enabled = !_busy && VerifiedDevice is not null;
        AcceptButton = complete ? _done : restart ? _check : _install;
        _cancel.Enabled = !_writing; _cancel.Text = _busy ? "取消准备" : "关闭"; _progress.Visible = _busy;
    }
    private void Stage(string text) {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => Stage(text)); return; }
        _stageText = text; _lastPercent = -1; _status.Text = text;
        _status.Visible = true;
        _progress.Style = ProgressBarStyle.Marquee;
    }
    private async Task<string> RunToolAsync(string executable, string[] args, CancellationToken token) {
        var tail = new Queue<string>();
        void Report(string line) {
            lock (tail) { if (tail.Count >= 4) tail.Dequeue(); tail.Enqueue(line.Length > 200 ? line[..200] : line); }
            var match = System.Text.RegularExpressions.Regex.Match(line, @"(\d{1,3})\s*%");
            if (!match.Success) return;
            int value = Math.Clamp(int.Parse(match.Groups[1].Value), 0, 100);
            string stage = _stageText;
            BeginInvoke(() => {
                if (IsDisposed || stage != _stageText || value == _lastPercent) return;
                _lastPercent = value; _status.Text = stage + " " + value + "%";
                _progress.Style = ProgressBarStyle.Continuous; _progress.Value = value;
            });
        }
        try { return await FirmwareFlasher.RunProcessAsync(executable, args, Report, token); }
        catch (IOException ex) { lock (tail) throw new IOException("刷机工具未完成：" + string.Join("\n", tail) + "\n" + ex.Message, ex); }
    }
    private async Task RunInstallAsync(string? packagePath, string? backupRecord) {
        if (_busy || _preview || _ports.SelectedItem is not FlashUsbDevice device) return;
        bool restore = backupRecord is not null;
        bool download = packagePath is null && !restore;
        string directory = Path.Combine(FirmwareFlasher.CacheDirectory, "tab5-install-" + Guid.NewGuid().ToString("N"));
        Tab5Installer? installer = null;
        _busy = true; VerifiedDevice = null; _mac = _version = _elfSha = null; _operation = new(); UpdateButtons();
        try {
            if (download) {
                Stage("正在下载官方首刷包…");
                Directory.CreateDirectory(directory);
                packagePath = Path.Combine(directory, UpdateService.Tab5FirstInstallAsset);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_operation.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(90));
                try {
                    using var updater = new UpdateService();
                    await updater.DownloadTab5FirstInstallAsync(packagePath, timeout.Token);
                    _downloadFailed = false;
                } catch (Exception ex) when (ex is not OperationCanceledException || !_operation.IsCancellationRequested) {
                    _downloadFailed = true;
                    Stage("下载失败，可以重试或使用本地首刷 ZIP。未写入设备。");
                    return;
                }
            }
            Stage("正在校验安装材料…");
            Tab5InstallPackage? package = restore ? null : await Task.Run(() => Tab5InstallPackage.Load(packagePath!, directory), _operation.Token);
            if (restore) await Task.Run(() => Tab5Installer.ReadBackup(backupRecord!), _operation.Token);
            _operation.Token.ThrowIfCancellationRequested();
            string action = restore ? "恢复原固件备份" : $"安装 AI-bot TAB5 {package!.Manifest.Version}";
            if (MessageBox.Show(this, $"{action}（{device.Port}）\n\n电脑会先备份，再替换 TAB5 的固件和设置。写入时请保持连接。\n\n请确认这台设备是你的 TAB5。", "确认 TAB5 写入", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.OK) {
                Stage("已取消，未写入设备。"); return;
            }
            using var tool = await FirmwareFlasher.PrepareToolAsync(Stage, _operation.Token);
            installer = new Tab5Installer((args, token) => RunToolAsync(tool.Executable, args, token), Stage);
            using var lease = await FlashUsbLease.AcquireAsync(_operation.Token);
            try {
                await _service.WithInstallUsbAsync(device, async () => {
                    void Writing() { _writing = true; UpdateButtons(); }
                    if (restore) await installer.RestoreAsync(device.Port, backupRecord!, directory, FirmwareFlasher.BackupDirectory, Writing, _operation.Token);
                    else await installer.InstallAsync(device.Port, package!, FirmwareFlasher.BackupDirectory, Writing, _operation.Token);
                }, _operation.Token);
                if (!restore) {
                    _mac = installer.DeviceMac; _version = package!.Manifest.Version;
                    _selectedIdentity = null;
                    byte[] app = File.ReadAllBytes(Path.Combine(directory, "application.bin"));
                    _elfSha = Convert.ToHexString(app.AsSpan(176, 32)).ToLowerInvariant();
                }
                Stage(restore ? "备份恢复并校验通过。请短按 RESET，检查原系统是否正常启动。" : WriteVerified);
            } finally { await lease.ReleaseAsync(false); }
        } catch (OperationCanceledException) { Stage(_writing ? "写入流程已结束，但桥接恢复未确认。请检查启动或用备份恢复。" : "已取消准备，未开始写入。重新操作前请再次进入下载模式。"); }
        catch (Exception ex) { Stage(($"未完成：{ex.Message}\n") + (_writing ? "请保持连接，重新进入下载模式后使用「恢复备份」。" : "本次未开始写入。")); }
        finally {
            if (installer?.BackupPath is string path) {
                _hasRecoveryBackup |= installer.BackupVerified;
                _backup.Text = installer.BackupVerified
                    ? "备份已保存到本机：" + path
                    : "备份未完成或校验失败。本次未开始写入，请重新备份后再试。";
                _backup.Visible = _mac is null || !_returnToAdd;
            }
            _writing = _busy = false; _operation?.Dispose(); _operation = null;
            try { FirmwareFlasher.CleanupTemporaryDirectory(directory, "tab5-install-"); } catch (IOException) { }
            RefreshDevices(reselection: _mac is not null);
        }
    }
    private async Task CheckBootAsync() {
        if (_busy || _preview || _mac is null || _ports.SelectedItem is not FlashUsbDevice device) return;
        _busy = true; VerifiedDevice = null; _operation = new(); UpdateButtons();
        try {
            Stage("正在核对设备、固件指纹和界面心跳…");
            using var lease = await FlashUsbLease.AcquireAsync(_operation.Token);
            try { await _service.CheckInstalledUsbAsync(device, _mac, _version!, _elfSha!, _operation.Token); }
            finally { await lease.ReleaseAsync(false); }
            VerifiedDevice = device; Stage(_returnToAdd?"启动检查通过。":"启动检查通过，请进入 USB 配对。");
        } catch (OperationCanceledException) { Stage("启动检查已取消，尚未确认设备运行。"); }
        catch (Exception ex) { Stage("启动未确认：" + ex.Message); }
        finally { _busy = false; _operation?.Dispose(); _operation = null; UpdateButtons(); }
        if(_returnToAdd&&VerifiedDevice is not null){DialogResult=DialogResult.OK;Close();}
    }
}
