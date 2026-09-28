namespace AIBotBridge;

internal sealed class Tab5InstallForm : Form
{
    private readonly Tab5Service _service;
    private readonly bool _preview;
    private readonly ComboBox _ports = new() { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name" };
    private readonly Label _package = new() { AutoSize = true, Text = "尚未选择首次安装 ZIP 包" };
    private readonly Label _status = new() { AutoSize = true, Dock = DockStyle.Fill, Text = "连接 TAB5 并进入下载模式，然后刷新设备。" };
    private readonly Label _backup = new() { AutoSize = true, Text = "原固件备份会保留在本机，不上传。" };
    private readonly CheckBox _confirm = new() { AutoSize = true, Text = "确认是 TAB5，允许替换原固件及设置。" };
    private readonly Button _refresh = Button("刷新设备"), _choose = Button("选择安装包…"), _install = Button("备份并安装", true),
        _restore = Button("恢复原固件…"), _check = Button("检查启动"), _done = Button("进入 USB 配对"), _cancel = Button("关闭");
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Height = 8, Visible = false, Style = ProgressBarStyle.Marquee };
    private CancellationTokenSource? _operation;
    private string? _zip, _mac, _version, _elfSha;
    private bool _busy, _writing, _verified;
    private string _stageText = "";
    private int _lastPercent = -1;

    internal Tab5InstallForm(Tab5Service service, bool preview = false)
    {
        _service = service; _preview = preview;
        Text = "TAB5 首次安装"; Font = new Font("Microsoft YaHei UI", 9F);
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(700, 690); MinimumSize = new Size(600, 550); StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.White;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);
        var content = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        layout.Controls.Add(content, 0, 0); SettingsWindow.FitFlow(content);
        content.SizeChanged += (_, _) => _confirm.MaximumSize = new Size(Math.Max(100, content.Width - SystemInformation.VerticalScrollBarWidth - 12), 0);
        void Note(string text, bool heading = false) {
            content.Controls.Add(new Label { Text = text, AutoSize = true, Margin = new Padding(0, 4, 0, 10),
                Font = heading ? new Font(Font, FontStyle.Bold) : Font });
        }
        Note("1 连接并进入下载模式", true);
        Note("使用 USB 数据线连接 TAB5 的 USB-C 接口。长按 RESET 约 2 秒，绿灯快速闪烁后松开，再刷新设备。请勿选择其他 ESP32 开发板。");
        content.Controls.Add(_ports); content.Controls.Add(Row(_refresh));
        Note("2 选择完整安装包", true);
        content.Controls.Add(_package); content.Controls.Add(Row(_choose));
        Note("适用于出厂系统。已安装 AI-bot 的设备请用“固件升级”中的无线升级；首次安装包与 OTA 的 .bin 文件不同。");
        content.Controls.Add(_confirm); content.Controls.Add(Row(_install, _restore));
        Note("3 重启、检查并配对", true);
        Note("写入校验通过后，短按 RESET，等待屏幕启动。USB 端口可能变化，请刷新并选择新端口，再检查启动。随后进行 USB 配对和 Wi-Fi 设置。");
        _status.Margin = new Padding(0, 8, 0, 8); layout.Controls.Add(_status, 0, 1);
        _backup.Margin = new Padding(0, 4, 0, 8); _backup.ForeColor = Color.FromArgb(80, 95, 110); content.Controls.Add(_backup);
        layout.Controls.Add(_progress, 0, 2); layout.Controls.Add(Row(_check, _done, _cancel), 0, 3);
        _refresh.Click += (_, _) => RefreshDevices();
        _choose.Click += (_, _) => {
            using var pick = new OpenFileDialog { Title = "选择 TAB5 首次安装包", Filter = "TAB5 首次安装包 (*.zip)|*.zip" };
            if (pick.ShowDialog(this) == DialogResult.OK) { _zip = pick.FileName; _package.Text = Path.GetFileName(_zip); UpdateButtons(); }
        };
        _confirm.CheckedChanged += (_, _) => UpdateButtons(); _ports.SelectedIndexChanged += (_, _) => UpdateButtons();
        _install.Click += async (_, _) => await RunInstallAsync(null);
        _restore.Click += async (_, _) => {
            using var pick = new OpenFileDialog { Title = "选择本工具保存的原固件备份记录", Filter = "TAB5 备份记录 (*.bin.json)|*.bin.json", InitialDirectory = FirmwareFlasher.BackupDirectory };
            if (pick.ShowDialog(this) == DialogResult.OK) await RunInstallAsync(pick.FileName);
        };
        _check.Click += async (_, _) => await CheckBootAsync();
        _done.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        _cancel.Click += (_, _) => { if (_busy) _operation?.Cancel(); else Close(); };
        FormClosing += (_, e) => { if (_busy) { e.Cancel = true; if (!_writing) _operation?.Cancel(); } };
        RefreshDevices(); SettingsWindow.FitScreen(this);
    }
    private static Button Button(string text, bool primary = false) { var b = new Button { Text = text }; SettingsWindow.StyleButton(b, primary); return b; }
    private static FlowLayoutPanel Row(params Control[] controls) {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Fill, Margin = new Padding(0, 5, 0, 7) };
        foreach (var c in controls) { c.Margin = new Padding(0, 0, 8, 2); row.Controls.Add(c); } return row;
    }
    private void RefreshDevices() {
        try {
            var selected = (_ports.SelectedItem as FlashUsbDevice)?.Identity;
            _ports.DataSource = _preview ? new[] { new FlashUsbDevice("COM7", "preview", "TAB5 USB 下载端口 (COM7)") } :
                FlashDeviceDiscovery.Read().Where(d => d.Identity.Contains("VID_303A", StringComparison.OrdinalIgnoreCase)).ToArray();
            _ports.SelectedIndex = -1;
            for (int i = 0; i < _ports.Items.Count; i++) if ((_ports.Items[i] as FlashUsbDevice)?.Identity == selected) _ports.SelectedIndex = i;
            if (_ports.Items.Count == 0) _status.Text = "未发现设备。请确认使用数据线并进入下载模式，然后刷新。";
        } catch (Exception ex) { _status.Text = ex.Message; }
        UpdateButtons();
    }
    private void UpdateButtons() {
        bool device = _ports.SelectedItem is FlashUsbDevice;
        _ports.Enabled = _refresh.Enabled = _choose.Enabled = _confirm.Enabled = !_busy;
        _install.Enabled = !_preview && !_busy && device && _zip is not null && _confirm.Checked;
        _restore.Enabled = !_preview && !_busy && device;
        _check.Enabled = !_preview && !_busy && device && _mac is not null;
        _done.Enabled = !_busy && _verified;
        _cancel.Enabled = !_writing; _cancel.Text = _busy ? "取消准备" : "关闭"; _progress.Visible = _busy;
    }
    private void Stage(string text) {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => Stage(text)); return; }
        _stageText = text; _lastPercent = -1; _status.Text = text;
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
    private async Task RunInstallAsync(string? backupRecord) {
        if (_busy || _preview || _ports.SelectedItem is not FlashUsbDevice device) return;
        bool restore = backupRecord is not null;
        string directory = Path.Combine(FirmwareFlasher.CacheDirectory, "tab5-install-" + Guid.NewGuid().ToString("N"));
        Tab5Installer? installer = null;
        _busy = true; _verified = false; _mac = _version = _elfSha = null; _operation = new(); UpdateButtons();
        try {
            Stage("正在校验安装材料…");
            Tab5InstallPackage? package = restore ? null : await Task.Run(() => Tab5InstallPackage.Load(_zip!, directory), _operation.Token);
            if (restore) await Task.Run(() => Tab5Installer.ReadBackup(backupRecord!), _operation.Token);
            _operation.Token.ThrowIfCancellationRequested();
            string action = restore ? "恢复原固件备份" : $"安装 AI-bot TAB5 {package!.Manifest.Version}";
            if (MessageBox.Show(this, $"将在 {device.Name} 上{action}。\n\n先备份并核验整片闪存，再替换全部 P4 固件及设置。写入期间不能取消，请保持供电和连接。\n\n确认实物为 TAB5 后继续。", "确认 TAB5 写入", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) {
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
                    byte[] app = File.ReadAllBytes(Path.Combine(directory, "application.bin"));
                    _elfSha = Convert.ToHexString(app.AsSpan(176, 32)).ToLowerInvariant();
                }
                Stage(restore ? "备份恢复并校验通过。请短按 RESET，检查原系统是否正常启动。" : "写入校验通过。请短按 RESET，刷新并选择启动后的 USB 端口，再点“检查启动”。");
            } finally { await lease.ReleaseAsync(false); }
        } catch (OperationCanceledException) { Stage(_writing ? "写入流程已结束，但桥接恢复未确认。请检查启动或用备份恢复。" : "已取消准备，未开始写入。重新操作前请再次进入下载模式。"); }
        catch (Exception ex) { Stage(($"未完成：{ex.Message}\n") + (_writing ? "请保持连接，重新进入下载模式后，可用“恢复原固件”恢复下方备份。" : "本次未开始写入。")); }
        finally {
            if (installer?.BackupPath is string path) _backup.Text = installer.BackupVerified
                ? "本次备份：" + path + "\n恢复请选择同目录的 .bin.json；请勿上传备份，其中可能含设备设置。"
                : "备份未完成或未通过校验，本次未开始写入；未完成的备份不用于恢复。";
            _writing = _busy = false; _operation?.Dispose(); _operation = null;
            try { FirmwareFlasher.CleanupTemporaryDirectory(directory, "tab5-install-"); } catch (IOException) { }
            UpdateButtons();
        }
    }
    private async Task CheckBootAsync() {
        if (_busy || _mac is null || _ports.SelectedItem is not FlashUsbDevice device) return;
        _busy = true; _verified = false; _operation = new(); UpdateButtons();
        try {
            Stage("正在核对设备、固件指纹和界面心跳…");
            using var lease = await FlashUsbLease.AcquireAsync(_operation.Token);
            try { await _service.CheckInstalledUsbAsync(device, _mac, _version!, _elfSha!, _operation.Token); }
            finally { await lease.ReleaseAsync(false); }
            _verified = true; Stage("启动检查通过。请进入 USB 配对，然后在 Wi-Fi 页保存网络并确认连接状态。");
        } catch (OperationCanceledException) { Stage("启动检查已取消，尚未确认设备运行。"); }
        catch (Exception ex) { Stage("启动未确认：" + ex.Message); }
        finally { _busy = false; _operation?.Dispose(); _operation = null; UpdateButtons(); }
    }
}
