namespace AIBotBridge;

internal sealed class MirrorForm : Form
{
    private readonly Func<StatusSnapshot> _capture;
    private readonly Func<string> _mode;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 500 };

    internal MirrorForm(Func<StatusSnapshot> capture, Func<string> mode)
    {
        _capture = capture;
        _mode = mode;
        Text = "AI-bot 240×240 镜像";
        ClientSize = new Size(480, 480);
        MinimumSize = new Size(280, 310);
        BackColor = Color.Black;
        DoubleBuffered = true;
        _timer.Tick += (_, _) => Invalidate();
        _timer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var logical = RenderSnapshot(_capture(), EffectiveMode());
        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        var side = Math.Min(ClientSize.Width, ClientSize.Height);
        var left = (ClientSize.Width - side) / 2;
        var top = (ClientSize.Height - side) / 2;
        e.Graphics.DrawImage(logical, new Rectangle(left, top, side, side));
    }

    internal static Bitmap RenderSnapshot(StatusSnapshot status, string mode)
    {
        var bitmap = new Bitmap(240, 240);
        using var graphics = Graphics.FromImage(bitmap);
        Draw(graphics, status, mode);
        return bitmap;
    }

    private string EffectiveMode()
    {
        var selected = _mode();
        if (selected != "auto") return selected;
        var status = _capture();
        if (status.Music?.Playing == true) return "music";
        var pages = new List<string> { "dual", "pet" };
        if (status.Weather is not null) pages.Add("weather");
        if (status.Stocks?.Quotes.Count > 0) pages.Add("stocks");
        if (status.Quotas is not null) pages.Add("quotas");
        if (status.DomesticQuotas is not null) pages.Add("domestic");
        if (status.SystemMetrics is not null) pages.Add("system");
        return pages[(int)((status.EpochUtc / 15) % pages.Count)];
    }

    private static void Draw(Graphics graphics, StatusSnapshot status, string mode)
    {
        graphics.Clear(Color.Black);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        switch (mode)
        {
            case "weather": DrawWeather(graphics, status); break;
            case "stocks": DrawStocks(graphics, status); break;
            case "quotas": DrawQuotas(graphics, status); break;
            case "domestic": DrawDomestic(graphics, status); break;
            case "system": DrawSystem(graphics, status); break;
            case "music": DrawMusic(graphics, status); break;
            case "pet": DrawPet(graphics, status); break;
            case "screensaver": DrawScreenSaver(graphics, status); break;
            default: DrawDashboard(graphics, status); break;
        }
    }

    private static void DrawDashboard(Graphics g, StatusSnapshot s)
    {
        Center(g, "AI-bot", 18, 24, Color.Cyan, true);
        Center(g, s.Time, 68, 30, Color.White, true);
        g.DrawLine(Pens.DimGray, 20, 124, 220, 124);
        Row(g, "CODEX", s.Codex.State.ToUpperInvariant(), 146, StateColor(s.Codex.State));
        Row(g, "CLAUDE", s.Claude.State.ToUpperInvariant(), 181, StateColor(s.Claude.State));
        if (s.Quotas?.Codex?.ResetCreditsAvailable is > 0)
            DrawRight(g, $"R*{s.Quotas.Codex.ResetCreditsAvailable}", 222, 214, 16,
                s.Quotas.Codex.Stale ? Color.Orange : Color.LimeGreen, true);
    }

    private static void DrawWeather(Graphics g, StatusSnapshot s)
    {
        Center(g, "WEATHER", 6, 14, Color.Cyan, true);
        if (s.Weather is not { } w) { Center(g, "等待数据", 105, 16, Color.Gray); return; }
        Center(g, $"{w.City}  {w.Condition}", 35, 18, Color.White, true);
        Center(g, $"{w.Temperature:0}°C", 70, 38, Color.Orange, true);
        Center(g, $"{w.Low:0} / {w.High:0}", 119, 16, Color.LightGray);
        Row(g, "湿度", $"{w.Humidity}%", 157, Color.White);
        Row(g, "PM2.5", w.Pm25?.ToString("0.0") ?? "--", 190, Color.White);
        DrawText(g, "OPEN-METEO", 6, 225, 8, Color.DimGray);
    }

    private static void DrawStocks(Graphics g, StatusSnapshot s)
    {
        Center(g, "STOCKS", 5, 14, Color.Cyan, true);
        var quotes = s.Stocks?.Quotes.Take(4).ToArray() ?? [];
        if (quotes.Length == 0) { Center(g, "等待数据", 105, 16, Color.Gray); return; }
        for (var index = 0; index < quotes.Length; index++)
        {
            var q = quotes[index];
            var y = 35 + index * 49;
            DrawText(g, string.IsNullOrEmpty(q.Name) ? q.Code : q.Name, 10, y, 13, Color.LightGray, true);
            DrawText(g, q.Price, 10, y + 21, 14, Color.White, true);
            DrawRight(g, q.ChangePercent, 230, y + 21, 14,
                q.Trend > 0 ? Color.Red : q.Trend < 0 ? Color.LimeGreen : Color.LightGray, true);
        }
    }

    private static void DrawQuotas(Graphics g, StatusSnapshot s)
    {
        Center(g, "ACCOUNT QUOTAS", 5, 14, Color.White, true);
        Provider(g, "CLAUDE", s.Quotas?.Claude, 32);
        Provider(g, "CODEX", s.Quotas?.Codex, 112);
        DrawResetCredits(g, s, 196);
    }

    private static void DrawResetCredits(Graphics g, StatusSnapshot s, int y)
    {
        var rows = ResetCreditDisplay.Rows(s.Quotas?.Codex, s.UtcOffsetSeconds);
        if (rows.Count == 0) return;
        var pages = (rows.Count + 1) / 2;
        var page = (int)((s.EpochUtc / 4) % pages);
        var color = s.Quotas?.Codex?.Stale == true ? Color.Orange : Color.LimeGreen;
        for (var index = page * 2; index < rows.Count && index < page * 2 + 2; index++)
        {
            var top = y + index % 2 * 18;
            DrawText(g, $"R*{rows[index].Count}", 16, top, 14, color, true);
            DrawRight(g, rows[index].Date, 180, top, 12, color);
        }
        if (pages > 1) DrawRight(g, $"{page + 1}/{pages}", 228, y + 5, 9, Color.LightGray);
    }

    private static void Provider(Graphics g, string name, ProviderQuotaSnapshot? value, int y)
    {
        DrawText(g, name, 12, y, 14, Color.Cyan, true);
        DrawRight(g, value?.Stale == true ? "STALE" : value?.Plan ?? "", 228, y, 12, Color.Gray);
        Row(g, "5H", Percent(value?.PrimaryPercent), y + 24, Color.White);
        Row(g, "7D", Percent(value?.WeeklyPercent), y + 47, Color.White);
    }

    private static void DrawDomestic(Graphics g, StatusSnapshot s)
    {
        Center(g, "DOMESTIC QUOTAS", 5, 14, Color.White, true);
        DomesticRow(g, "QWEN", s.DomesticQuotas?.Alibaba, 34);
        DomesticRow(g, "KIMI", s.DomesticQuotas?.Kimi, 80);
        DomesticRow(g, "MINIMAX", s.DomesticQuotas?.MiniMax, 126);
        DomesticRow(g, "DEEPSEEK", s.DomesticQuotas?.DeepSeek, 172);
    }

    private static void DomesticRow(Graphics g, string name, DomesticProviderQuotaSnapshot? value, int y)
    {
        DrawText(g, name, 10, y, 13, value?.Stale == true ? Color.Orange : Color.Cyan, true);
        var display = value?.WeeklyPercent is double weekly ? $"{weekly:0.0}% WK"
            : value?.PrimaryPercent is double primary ? $"{primary:0.0}% 5H" : "--";
        if (value?.Balance is double balance)
        {
            using var numberFont = new Font("Microsoft YaHei UI", 22, FontStyle.Bold, GraphicsUnit.Pixel);
            using var currencyFont = new Font("Microsoft YaHei UI", 11, FontStyle.Regular, GraphicsUnit.Pixel);
            var number = balance.ToString("0.00");
            var width = g.MeasureString(number, numberFont).Width;
            DrawRight(g, number, 230, y, 22, Color.White, true);
            var baselineOffset = FontAscent(numberFont) - FontAscent(currencyFont);
            DrawRight(g, value.Currency ?? "", 226 - width, y + baselineOffset, 11, Color.White);
        }
        else DrawRight(g, display, 230, y, 13, Color.White, true);
        DrawText(g, value?.Plan ?? "", 10, y + 28, 9, Color.Gray);
    }

    private static void DrawSystem(Graphics g, StatusSnapshot s)
    {
        Center(g, "SYSTEM", 7, 16, Color.Cyan, true);
        if (s.SystemMetrics is not { } m) { Center(g, "等待数据", 105, 16, Color.Gray); return; }
        Row(g, "CPU", $"{m.CpuPercent:0.0}%", 47, Color.LimeGreen);
        Row(g, "MEMORY", $"{m.MemoryPercent:0.0}%", 87, Color.Orange);
        g.DrawLine(Pens.DimGray, 18, 125, 222, 125);
        Row(g, "UPLOAD", Rate(m.UploadBytesPerSecond), 145, Color.Yellow);
        Row(g, "DOWNLOAD", Rate(m.DownloadBytesPerSecond), 185, Color.Cyan);
    }

    private static void DrawMusic(Graphics g, StatusSnapshot s)
    {
        var m = s.Music;
        Center(g, m?.Playing == true ? "NOW PLAYING" : "MUSIC", 5, 14,
            m?.Playing == true ? Color.LimeGreen : Color.Gray, true);
        if (string.IsNullOrEmpty(m?.Title)) { Center(g, "无活动媒体", 105, 16, Color.Gray); return; }
        using var cover = new SolidBrush(Color.FromArgb(25, 45, 55));
        g.FillRoundedRectangle(cover, new RectangleF(64, 28, 112, 106), 8);
        Center(g, "♪", 63, 48, Color.Cyan, true);
        Center(g, m!.Title, 143, 16, Color.White, true);
        Center(g, m.Artist, 166, 12, Color.LightGray);
        var ratio = m.DurationSeconds > 0 ? Math.Clamp(m.ElapsedSeconds / m.DurationSeconds, 0, 1) : 0;
        g.DrawRectangle(Pens.DimGray, 18, 193, 204, 7);
        using var progress = new SolidBrush(Color.Cyan);
        g.FillRectangle(progress, 20, 195, (float)(200 * ratio), 4);
    }

    private static void DrawPet(Graphics g, StatusSnapshot s)
    {
        var working = s.Codex.State == "working" || s.Claude.State == "working";
        Center(g, "BYTE SPROUT", 12, 14, working ? Color.LimeGreen : Color.Cyan, true);
        var owner = s.Codex.State == "working" && s.Claude.State == "working" ? "CODEX + CLAUDE"
            : s.Codex.State == "working" ? "CODEX" : s.Claude.State == "working" ? "CLAUDE" : "READY";
        Center(g, owner, 32, 9, Color.LightGray);
        using var shell = new SolidBrush(working ? Color.Cyan : Color.DimGray);
        g.FillRectangle(shell, 99, 68, 42, 34);
        g.FillRectangle(shell, 103, 108, 34, 42);
        using var face = new SolidBrush(Color.Black);
        g.FillRectangle(face, 105, 75, 30, 18);
        Center(g, working ? "WORKING" : "IDLE", 180, 16, working ? Color.LimeGreen : Color.Yellow, true);
        DrawResetCredits(g, s, 200);
    }

    private static void DrawScreenSaver(Graphics g, StatusSnapshot s) =>
        Center(g, s.Time[..5], 82, 34, Color.LimeGreen, true);

    private static void Row(Graphics g, string left, string right, int y, Color rightColor)
    {
        DrawText(g, left, 18, y, 14, Color.LightGray);
        DrawRight(g, right, 222, y, 14, rightColor, true);
    }

    private static void Center(Graphics g, string value, float y, float size, Color color, bool bold = false)
    {
        using var font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(value, font, brush, new RectangleF(4, y, 232, size + 8), format);
    }

    private static void DrawText(Graphics g, string value, float x, float y, float size, Color color, bool bold = false)
    {
        using var font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        g.DrawString(value, font, brush, x, y);
    }

    private static void DrawRight(Graphics g, string value, float x, float y, float size, Color color, bool bold = false)
    {
        using var font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = StringAlignment.Far };
        g.DrawString(value, font, brush, new PointF(x, y), format);
    }

    private static float FontAscent(Font font) => font.Size * font.FontFamily.GetCellAscent(font.Style) /
        font.FontFamily.GetEmHeight(font.Style);

    private static Color StateColor(string state) => state == "working" ? Color.LimeGreen
        : state == "idle" ? Color.Yellow : Color.DimGray;
    private static string Percent(double? value) => value.HasValue ? $"{value:0.0}%" : "--";
    private static string Rate(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1048576.0:0.0} MB/s"
        : bytes >= 1024 ? $"{bytes / 1024.0:0.0} KB/s" : $"{bytes} B/s";
}

internal static class GraphicsExtensions
{
    internal static void FillRoundedRectangle(this Graphics graphics, Brush brush, RectangleF bounds, float radius)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }
}
