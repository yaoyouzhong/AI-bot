using System.Drawing.Drawing2D;

namespace AIBotBridge;

internal sealed class QuotaTrendForm : Form
{
    private readonly QuotaHistory _history;
    private readonly ComboBox _range = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    private readonly ComboBox _metric = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
    private readonly Label _updated = new() { AutoSize = true, Dock = DockStyle.Fill, ForeColor = Color.DimGray };
    private readonly Label _note = new() { AutoSize = true, Dock = DockStyle.Fill };
    private readonly TabControl _views = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _table = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White, BorderStyle = BorderStyle.None };
    private readonly TrendCanvas _chart = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 30000 };
    internal QuotaTrendForm(QuotaHistory? history = null)
    {
        _history = history ?? QuotaHistory.Shared;
        SuspendLayout();
        Text = "Codex 额度趋势";
        Font = new Font("Microsoft YaHei UI", 10, FontStyle.Regular, GraphicsUnit.Point);
        ClientSize = new Size(850, 620); MinimumSize = new Size(680, 520); BackColor = Color.White;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), RowCount = 4, ColumnCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 2; i++) layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.AutoSize));
        var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 16) };
        _range.Items.AddRange(["近 7 天", "近 30 天"]); _range.SelectedIndex = 0;
        _metric.Width = 190;
        _metric.Items.AddRange(["每日用量（周额度）", "五小时已用比例"]); _metric.SelectedIndex = 0;
        controls.Controls.AddRange([_metric, _range]);
        var chartPage = new TabPage("趋势图") { BackColor = Color.White, Padding = new Padding(8) };
        var tablePage = new TabPage("数据明细") { BackColor = Color.White, Padding = new Padding(8) };
        chartPage.Controls.Add(_chart); tablePage.Controls.Add(_table); _views.TabPages.AddRange([chartPage, tablePage]);
        layout.Controls.Add(controls, 0, 0); layout.Controls.Add(_updated, 0, 1); layout.Controls.Add(_views, 0, 2); layout.Controls.Add(_note, 0, 3); Controls.Add(layout);
        _updated.Margin = new Padding(0, 0, 0, 16);
        _note.Margin = new Padding(0, 12, 0, 0);
        _note.ForeColor = Color.DimGray;
        _table.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _table.MultiSelect = false;
        _table.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        _table.DefaultCellStyle.Padding = new Padding(6); _table.ColumnHeadersDefaultCellStyle.Padding = new Padding(6);
        _table.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 248, 250);
        _table.EnableHeadersVisualStyles = false; _table.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 241, 244);
        _table.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _range.SelectedIndexChanged += (_, _) => RefreshHistory(); _metric.SelectedIndexChanged += (_, _) => RefreshHistory();
        _timer.Tick += (_, _) => RefreshHistory(); Shown += (_, _) => { RefreshHistory(); _timer.Start(); };
        _chart.Selected += i => { if (i >= 0 && i < _table.Rows.Count) { _views.SelectedIndex = 1; _table.ClearSelection(); _table.Rows[i].Selected = true; _table.FirstDisplayedScrollingRowIndex = i; } };
        RefreshHistory();
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        ResumeLayout(true);
    }
    internal void SetTestView(int metric, int range, bool details) { _metric.SelectedIndex = metric; _range.SelectedIndex = range; _views.SelectedIndex = details ? 1 : 0; }
    internal string DetailText => _updated.Text;
    internal int DisplayedDays => _table.Rows.Count;
    private void RefreshHistory()
    {
        int days = _range.SelectedIndex == 0 ? 7 : 30;
        var rows = _history.Read(); var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, QuotaHistory.StatisticsZone);
        var today = DateOnly.FromDateTime(now.DateTime);
        var daily = QuotaHistory.Daily(rows, today, days, QuotaHistory.StatisticsZone);
        // Do not draw dates before recording began; gaps inside the recorded period stay unknown.
        if (rows.FirstOrDefault() is { } first)
            daily = daily.Where(x => x.Day >= DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(first.At, QuotaHistory.StatisticsZone).DateTime)).ToArray();
        var latest = rows.LastOrDefault();
        _updated.Text = latest is not null
            ? $"记录始于 {TimeZoneInfo.ConvertTime(rows[0].At, QuotaHistory.StatisticsZone):MM-dd HH:mm}    更新于 {TimeZoneInfo.ConvertTime(latest.At, QuotaHistory.StatisticsZone):MM-dd HH:mm}"
            : "等待首次读取额度，首条记录仅作基线。";
        var selected = _table.CurrentRow?.Index ?? 0;
        var scroll = _table.FirstDisplayedScrollingRowIndex;
        _table.Columns.Clear(); _table.Rows.Clear();
        if (_metric.SelectedIndex == 0)
        {
            var average = QuotaHistory.AverageRecorded(daily, today);
            _updated.Text += average.Value is double mean
                ? $"\n日均使用 {mean:0.##}%（{average.Days} 个完整统计日）"
                : "\n日均使用 --（暂无符合条件的历史日期）";
            foreach (var title in new[] { "日期", "用量（周额度 %）", "快照数", "说明" }) _table.Columns.Add(title, title);
            foreach (var day in daily) _table.Rows.Add(day.Day.ToString("MM-dd"), !day.Partial ? day.Growth?.ToString("0.##") ?? "--" : "--", day.Samples,
                string.Join("；", new[] {
                    day.Resets > 0 ? $"到期重置 {day.Resets} 次" : null,
                    day.UncertainResets > 0 ? $"疑似重置/校正 {day.UncertainResets} 次" : null,
                    day.Gaps > 0 ? "记录断档" : null,
                    day.Day == today ? "统计中" : day.Partial ? "数据不完整" : "完整统计日"
                }.Where(x => x is not null)));
            _table.Columns[3].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _table.Columns[3].FillWeight = 190;
            _chart.Set(daily.Select(x => x.Partial ? null : x.Growth).ToArray(), daily.Select(x => x.Day.ToString("MM-dd")).ToArray(), false);
        }
        else
        {
            foreach (var title in new[] { "采样时间", "已用（%）", "重置时间" }) _table.Columns.Add(title, title);
            var samples = rows.Where(x => x.At >= now.AddDays(-days) && x.FiveHour.HasValue).ToArray();
            // Show bounded hourly last samples; actual daily totals use every stored observation.
            var hourly = samples.GroupBy(x => x.At.ToUnixTimeSeconds() / 3600).Select(x => x.Last()).ToArray();
            foreach (var item in hourly) _table.Rows.Add(item.At.ToLocalTime().ToString("MM-dd HH:mm"), item.FiveHour!.Value.ToString("0.##"), item.FiveHourReset?.ToLocalTime().ToString("MM-dd HH:mm") ?? "--");
            _chart.Set(hourly.Select(x => x.FiveHour).ToArray(), hourly.Select(x => x.At.ToLocalTime().ToString("MM-dd HH:mm")).ToArray(), true);
        }
        foreach (DataGridViewColumn column in _table.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        if (_table.Rows.Count > 0) { _table.CurrentCell = _table.Rows[Math.Min(selected, _table.Rows.Count - 1)].Cells[0]; if (scroll >= 0) _table.FirstDisplayedScrollingRowIndex = Math.Min(scroll, _table.Rows.Count - 1); }
        _note.Text = _history.Error ?? (_metric.SelectedIndex == 0
            ? "北京时间 00:00–次日 00:00；按接口精度统计，断档或重置无法核实的日期留空。"
            : "每小时最后一个已用比例快照，非每日消耗。");
    }
    protected override void Dispose(bool disposing) { if (disposing) _timer.Dispose(); base.Dispose(disposing); }
    private sealed class TrendCanvas : Control
    {
        private double?[] _values = []; private string[] _labels = []; private bool _points;
        internal event Action<int>? Selected;
        internal TrendCanvas() { DoubleBuffered = true; BackColor = Color.White; }
        internal void Set(double?[] values, string[] labels, bool points) { _values = values; _labels = labels; _points = points; Invalidate(); }
        protected override void OnMouseClick(MouseEventArgs e) { float scale = DeviceDpi / 96f, x = e.X / scale, width = Width / scale; if (_values.Length > 0 && x >= 48 && x < width - 28) Selected?.Invoke(Math.Clamp((int)((x - 48) / Math.Max(1f, (width - 76f) / _values.Length)), 0, _values.Length - 1)); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            if (!_values.Any(x => x.HasValue)) { TextRenderer.DrawText(g, "暂无可比较历史 · 开始记录后自动生成", Font, ClientRectangle, Color.DimGray, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); return; }
            float scale = DeviceDpi / 96f; g.ScaleTransform(scale, scale);
            using var chartFont = new Font("Microsoft YaHei UI", 13, FontStyle.Regular, GraphicsUnit.Pixel);
            float left = 48, top = 28, h = Math.Max(20, Height / scale - (_points ? 92 : 70)), width = Math.Max(1, Width / scale - 76);
            g.DrawString(_points ? "已用比例（%）" : "已记录用量（周额度 %）", chartFont, Brushes.DimGray, left, 0);
            double max = _points ? 100 : Math.Max(10, Math.Ceiling(_values.Max(x => x ?? 0) / 10) * 10);
            using var grid = new Pen(Color.FromArgb(220, 228, 231)); using var fill = new SolidBrush(Color.FromArgb(28, 139, 147));
            for (int tick = 0; tick <= 4; tick++) { float y = top + h * tick / 4; g.DrawLine(grid, left, y, left + width, y); g.DrawString((max * (4 - tick) / 4).ToString("0.#"), chartFont, Brushes.DimGray, 0, y - 9); }
            float step = width / _values.Length;
            for (int i = 0; i < _values.Length; i++)
            {
                float x = left + step * i;
                if (_values[i] is double value) { float size = (float)(value / max * h); if (_points) g.FillEllipse(fill, x + step / 2 - 2, top + h - size - 2, 4, 4); else { float barWidth = Math.Clamp(step * .6f, 2, 44); g.FillRectangle(fill, x + (step - barWidth) / 2, top + h - Math.Max(1, size), barWidth, Math.Max(1, size)); } }
                else if (!_points) g.DrawString("--", chartFont, Brushes.Gray, x + step * .2f, top + h - 18);
                int labelEvery = Math.Max(1, (int)Math.Ceiling(_values.Length / Math.Max(1f, width / 78)));
                if (i % labelEvery == 0) {
                    var label = _points ? _labels[i].Replace(" ", "\n") : _labels[i];
                    using var format = new StringFormat { Alignment = StringAlignment.Center };
                    g.DrawString(label, chartFont, Brushes.DimGray, new RectangleF(x + step / 2 - 35, top + h + 8, 70, 44), format);
                }
            }
        }
    }
}
