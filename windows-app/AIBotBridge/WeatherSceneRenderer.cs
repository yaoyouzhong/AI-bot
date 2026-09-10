using System.Drawing.Text;

namespace AIBotBridge;

// Independent layout reconstruction from the legacy device geometry.
// No legacy bitmap, logo or animation is bundled by this renderer.
internal static class WeatherSceneRenderer
{
    internal static void Draw(Graphics g, StatusSnapshot status, string? airLabel = null)
        => WeatherMirrorRenderer.Draw(g,status,airLabel);

    internal static void DrawDevicePreview(Graphics g, StatusSnapshot status, string? airLabel = null)
    {
        g.Clear(Color.Black);
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        if (status.Weather is not { } weather)
        {
            Text(g, "请在托盘设置天气城市", new(8, 100, 224, 30), 17, Color.LightGray);
            return;
        }
        var local = DateTimeOffset.FromUnixTimeSeconds(status.EpochUtc).ToOffset(TimeSpan.FromSeconds(weather.UtcOffsetSeconds ?? status.UtcOffsetSeconds));
        airLabel ??= weather.AirQualityLabel;
        Text(g, weather.City, new(14, 0, 120, 28), 20, Color.White, fit: true);
        Text(g, $"L {weather.Low:0}C", new(14, 30, 64, 22), 14, Color.Cyan);
        Text(g, $"H {weather.High:0}C", new(78, 30, 58, 22), 14, Color.Orange);
        Text(g, weather.Condition, new(184, 9, 54, 32), 19, Color.Orange, fit: true);
        // A textual rating must come from its provider, not an unrelated AQI scale.
        if (!string.IsNullOrEmpty(airLabel))
        {
            var airColor = MigratedWeather.WeatherMonitor.AirQualityColor(airLabel);
            using var border = new Pen(airColor);
            if (airLabel.Length == 1) g.DrawEllipse(border, 145, 14, 25, 25);
            else {
                var bounds=new Rectangle(138,15,39,24);
                using var background=new SolidBrush(Color.FromArgb(airColor.R/8,airColor.G/8,airColor.B/8));
                g.FillRoundedRectangle(background,bounds,8);
                using var path=new System.Drawing.Drawing2D.GraphicsPath();
                path.AddArc(bounds.Left,bounds.Top,16,16,180,90);path.AddArc(bounds.Right-16,bounds.Top,16,16,270,90);
                path.AddArc(bounds.Right-16,bounds.Bottom-16,16,16,0,90);path.AddArc(bounds.Left,bounds.Bottom-16,16,16,90,90);path.CloseFigure();g.DrawPath(border,path);
            }
            Text(g, airLabel, new(140, 12, 42, 28), airLabel.Length > 1 ? 12 : 18, airColor, fit: true);
        }
        else if (weather.AirQualityIndex is int aqi)
            Text(g, $"AQI\n{aqi}", new(138, 3, 44, 44), 12, Color.LightGray);

        var digits = new[] { local.Hour / 10, local.Hour % 10, local.Minute / 10, local.Minute % 10 };
        var left = new[] { 16, 50, 94, 128 };
        for (var i = 0; i < 4; i++) Digit(g, digits[i], new(left[i], 57, 30, 48), i < 2 ? Color.White : Color.Orange);
        Digit(g, local.Second / 10, new(174, 75, 16, 28), Color.LightGray);
        Digit(g, local.Second % 10, new(194, 75, 16, 28), Color.LightGray);
        Text(g, $"{local.Month}月{local.Day}日 周{"日一二三四五六"[(int)local.DayOfWeek]}",
            new(14, 118, 210, 30), 19, Color.White);
        Metric(g, "TEMP", $"{weather.Temperature:0}C", 160,
            Math.Clamp((weather.Temperature + 10) / 40, 0, 1), Color.Red);
        Metric(g, "HUMID", $"{weather.Humidity}%", 197,
            Math.Clamp(weather.Humidity / 100.0, 0, 1), Color.LimeGreen);
        new WeatherAnimations(g).Draw(weather.AnimationIcon ?? IconForWmo(weather.WeatherCode), weather.Animation, (int)(status.EpochUtc * 4 % 12));
    }

    internal static int IconForWmo(int code) => code switch
    {
        0 => 0, 1 or 2 => 1, 3 => 2, 45 or 48 => 3,
        >= 51 and <= 67 or >= 80 and <= 82 => 4,
        >= 71 and <= 77 or 85 or 86 => 5, >= 95 and <= 99 => 6, _ => 2
    };

    private static void Metric(Graphics g, string label, string value, int y, double fill, Color color)
    {
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, 14, y + 9, 9, 9);
        if (label == "TEMP") g.FillRectangle(brush, 17, y, 3, 12);
        else g.FillPolygon(brush, new Point[] { new(14, y + 12), new(23, y + 12), new(18, y) });
        Text(g, label, new(30, y, 61, 13), 9, Color.LightGray);
        g.FillRectangle(Brushes.DimGray, 30, y + 18, 60, 5);
        g.FillRectangle(brush, 30, y + 18, (int)Math.Round(60 * fill), 5);
        Text(g, value, new(94, y, 54, 29), 22, Color.White, fit: true);
    }

    private static void Text(Graphics g, string text, Rectangle bounds, float pixels, Color color, bool fit = false)
    {
        using var format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        if (!text.Contains('\n')) format.FormatFlags = StringFormatFlags.NoWrap;
        if (fit)
            while (pixels > 10)
            {
                using var measure = new Font("Microsoft YaHei UI", pixels, FontStyle.Bold, GraphicsUnit.Pixel);
                if (g.MeasureString(text, measure).Width <= bounds.Width) break;
                pixels--;
            }
        using var font = new Font("Microsoft YaHei UI", pixels, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        g.DrawString(text, font, brush, bounds, format);
    }

    private static void Digit(Graphics g, int digit, Rectangle bounds, Color color)
    {
        var w = bounds.Width; var h = bounds.Height; var t = Math.Max(2, w / 7);
        var half = h / 2;
        (Rectangle Rectangle, string Digits)[] segments =
        [
            (new(t, 0, w - 2*t, t), "02356789"), (new(t, half - t/2, w - 2*t, t), "2345689"),
            (new(t, h-t, w - 2*t, t), "0235689"), (new(0, t, t, half-t), "045689"),
            (new(w-t, t, t, half-t), "01234789"), (new(0, half, t, half-t), "0268"),
            (new(w-t, half, t, half-t), "013456789")
        ];
        using var brush = new SolidBrush(color);
        foreach (var segment in segments)
            if (segment.Digits.Contains((char)('0' + digit)))
            {
                var rect = segment.Rectangle;
                rect.Offset(bounds.Location);
                g.FillRectangle(brush, rect);
            }
    }
}
