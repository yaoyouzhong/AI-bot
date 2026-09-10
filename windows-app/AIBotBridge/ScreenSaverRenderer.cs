namespace AIBotBridge;

internal static class ScreenSaverRenderer
{
    internal const int ClockWidth = 204;
    internal const int ClockHeight = 76;
    private static readonly (RectangleF Bounds, string Digits)[] Segments =
    [
        (new(9, 0, 24, 9), "02356789"), (new(9, 67, 24, 9), "0235689"),
        (new(9, 33.5f, 24, 9), "2345689"), (new(0, 9, 9, 29), "045689"),
        (new(0, 38, 9, 29), "0268"), (new(33, 9, 9, 29), "01234789"),
        (new(33, 38, 9, 29), "013456789")
    ];

    internal static Point Position(long epoch)
    {
        static int Bounce(long tick, int speed, int range)
        {
            var phase = (int)((tick * speed) % (range * 2));
            return phase > range ? range * 2 - phase : phase;
        }
        return new Point(6 + Bounce(epoch / 5, 2, 24), 12 + Bounce(epoch / 5, 1, 104));
    }

    internal static void Draw(Graphics g, StatusSnapshot status)
    {
        var local = DateTimeOffset.FromUnixTimeSeconds(status.EpochUtc).AddSeconds(status.UtcOffsetSeconds);
        var origin = Position(status.EpochUtc);
        var digits = new[] { local.Hour / 10, local.Hour % 10, local.Minute / 10, local.Minute % 10 };
        var offsets = new[] { 0, 47, 115, 162 };
        for (var cell = 0; cell < 4; cell++)
            foreach (var segment in Segments)
                if (segment.Digits.Contains((char)('0' + digits[cell])))
                {
                    var rectangle = segment.Bounds;
                    rectangle.Offset(origin.X + offsets[cell], origin.Y);
                    g.FillRectangle(Brushes.Cyan, rectangle);
                }
        using var accent = new SolidBrush(Color.FromArgb(255,214,10));
        g.FillEllipse(accent, origin.X + 97, origin.Y + 21, 10, 10);
        g.FillEllipse(accent, origin.X + 97, origin.Y + 45, 10, 10);

        using var font = new Font("Microsoft YaHei UI", 19, FontStyle.Bold, GraphicsUnit.Pixel);
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        using var muted = new SolidBrush(Color.FromArgb(198, 203, 198));
        var date = local.ToString("MM-dd");
        var weekday = "周" + "日一二三四五六"[(int)local.DayOfWeek];
        var dateWidth = (int)Math.Ceiling(g.MeasureString(date, font, int.MaxValue, format).Width);
        var weekWidth = (int)Math.Ceiling(g.MeasureString(weekday, font, int.MaxValue, format).Width);
        var visibleCenter = origin.X + (ClockWidth + (digits[0] == 1 ? 33 : 0)) / 2f;
        var left = visibleCenter - (dateWidth + 8 + weekWidth) / 2f;
        g.DrawString(date, font, muted, left, origin.Y + 86, format);
        g.DrawString(weekday, font, accent, left + dateWidth + 8, origin.Y + 86, format);
    }
}
