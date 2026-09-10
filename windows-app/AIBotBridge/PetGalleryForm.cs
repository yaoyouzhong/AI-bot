namespace AIBotBridge;

internal sealed class PetGalleryForm : Form
{
    private readonly TextBox _search = new() { PlaceholderText = "搜索桌宠名称…", Dock = DockStyle.Fill };
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly PictureBox _preview = new() { BackColor = Color.Black, SizeMode = PictureBoxSizeMode.CenterImage, Dock = DockStyle.Fill };
    private readonly ComboBox _owner = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _motion = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Button _apply = new() { Text = "应用并同步", Dock = DockStyle.Fill, Enabled = false };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 40 };
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _selection;
    private GalleryPet[] _pets = [];
    private GalleryPet? _sheetPet;
    private GallerySheet? _sheet;
    private PetAnimation? _animation;
    private readonly Action<string> _selectPage;
    private long _started;
    private int _generation;
    private bool _resourcesDisposed;

    internal PetGalleryForm(Action<string> selectPage, bool load = true)
    {
        _selectPage = selectPage;
        Text = "更换桌宠动画（petdex.dev）";
        Font = new Font("Microsoft YaHei UI", 9);
        ClientSize = new Size(480,620); MinimumSize = new Size(440,540);
        StartPosition = FormStartPosition.CenterScreen;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 6 };
        foreach (var style in new[] { new RowStyle(SizeType.Absolute,34), new RowStyle(SizeType.Percent,100), new RowStyle(SizeType.Absolute,150), new RowStyle(SizeType.Absolute,38), new RowStyle(SizeType.Absolute,40), new RowStyle(SizeType.Absolute,64) }) layout.RowStyles.Add(style);
        layout.Controls.Add(_search,0,0); layout.Controls.Add(_list,0,1); layout.Controls.Add(_preview,0,2);
        var controls = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        controls.ColumnStyles.Add(new(SizeType.Percent,30)); controls.ColumnStyles.Add(new(SizeType.Percent,40)); controls.ColumnStyles.Add(new(SizeType.Percent,30));
        controls.Controls.Add(_owner,0,0); controls.Controls.Add(_motion,1,0); controls.Controls.Add(_apply,2,0);
        layout.Controls.Add(controls,0,3); layout.Controls.Add(_status,0,4);
        layout.Controls.Add(new Label { Text = "图库素材仅用于本机；不随开源包分发。设备需在 USB 连接后同步。", Dock = DockStyle.Fill, ForeColor = SystemColors.GrayText },0,5);
        Controls.Add(layout);
        _owner.Items.AddRange(["Claude", "Codex"]); _owner.SelectedIndex = 0;
        _motion.DropDownWidth = 240;
        _motion.Format += (_, e) => { if (e.ListItem is GalleryMotion m) e.Value = m.Label.Split(' ')[0]; };
        _motion.FormattingEnabled = true;
        _motion.Items.AddRange(PetGalleryService.Motions.Cast<object>().ToArray()); _motion.SelectedIndex = 7;
        _search.TextChanged += (_,_) => Filter();
        _list.SelectedIndexChanged += async (_,_) => await SelectAsync();
        _owner.SelectedIndexChanged += async (_,_) => await SelectAsync();
        _motion.SelectedIndexChanged += async (_,_) => await SelectAsync();
        _apply.Click += (_,_) => Apply();
        _timer.Tick += (_,_) => PaintFrame(); _timer.Start();
        if (load) Shown += async (_,_) => await LoadAsync();
    }
    private string SelectedOwner => _owner.SelectedIndex == 0 ? "claude" : "codex";
    private async Task LoadAsync()
    {
        _status.Text = "正在加载图库…";
        try
        {
            var result = await PetGalleryService.Load(_lifetime.Token);
            if (IsDisposed) return;
            _pets = result.Pets; Filter();
            _status.Text = $"共 {_pets.Length} 个桌宠" + (result.Cached ? "（离线列表缓存）" : "");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { if (!IsDisposed && !_lifetime.IsCancellationRequested) _status.Text = "列表加载失败：" + ex.Message; }
    }
    private void Filter()
    {
        _list.BeginUpdate(); _list.Items.Clear();
        _list.Items.AddRange(_pets.Where(p => p.Name.Contains(_search.Text.Trim(), StringComparison.OrdinalIgnoreCase) || p.Slug.Contains(_search.Text.Trim(), StringComparison.OrdinalIgnoreCase)).Cast<object>().ToArray());
        _list.EndUpdate();
    }
    private async Task SelectAsync()
    {
        int generation = ++_generation;
        _selection?.Cancel(); _selection?.Dispose();
        _selection = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var token = _selection.Token;
        _apply.Enabled = false; _animation = null;
        var previous = _preview.Image; _preview.Image = null; previous?.Dispose();
        if (_list.SelectedItem is not GalleryPet pet || _motion.SelectedItem is not GalleryMotion motion) return;
        var owner = SelectedOwner;
        _status.Text = "正在读取动画…";
        try
        {
            var sheet = _sheetPet == pet && _sheet is not null ? _sheet : await PetGalleryService.LoadSheet(pet, token);
            if (IsDisposed || generation != _generation) return;
            _sheet = sheet; _sheetPet = pet;
            _animation = PetGalleryService.Animate(sheet, motion, owner);
            _started = Environment.TickCount64;
            _apply.Enabled = true; PaintFrame();
            _status.Text = $"{pet.Name} · {motion.Label} · {owner}";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { if (!IsDisposed && generation == _generation && !token.IsCancellationRequested) _status.Text = "预览失败：" + ex.Message; }
    }
    private void PaintFrame()
    {
        if (_animation is null) return;
        var old = _preview.Image; _preview.Image = _animation.BitmapAt(Environment.TickCount64 - _started); old?.Dispose();
    }
    private void Apply()
    {
        if (_animation is null || !_apply.Enabled) return;
        try
        {
            PetAnimationStore.Shared.Select(SelectedOwner, _animation);
            _selectPage(SelectedOwner);
            _status.Text = $"{SelectedOwner} 已保存；设备将在 USB 连接后同步（尚未确认设备显示）。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { _status.Text = "保存失败：" + ex.Message; }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_resourcesDisposed)
        {
            _resourcesDisposed = true;
            _lifetime.Cancel(); _selection?.Cancel(); _selection?.Dispose();
            _lifetime.Dispose(); _timer.Dispose(); _preview.Image?.Dispose(); _preview.Image = null;
        }
        base.Dispose(disposing);
    }
}
