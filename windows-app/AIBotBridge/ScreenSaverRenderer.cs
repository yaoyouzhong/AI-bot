namespace AIBotBridge;

internal static class ScreenSaverRenderer
{
    internal const int ClockWidth = 204;
    internal const int ClockHeight = 76;
    private static readonly (Rectangle Bounds, string Digits)[] Segments =
    [
        (new(9, 0, 24, 9), "02356789"), (new(9, 67, 24, 9), "0235689"),
        (new(9, 34, 24, 9), "2345689"), (new(0, 9, 9, 29), "045689"),
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
        return new Point(6 + Bounce(epoch / 5, 2, 24), 12 + Bounce(epoch / 5, 1, 90));
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
                    g.FillRoundedRectangle(Brushes.Cyan, rectangle, 3);
                }
        g.FillEllipse(Brushes.Yellow, origin.X + 97, origin.Y + 21, 10, 10);
        g.FillEllipse(Brushes.Yellow, origin.X + 97, origin.Y + 45, 10, 10);

        using var font = new Font("Microsoft YaHei UI", 22, FontStyle.Bold, GraphicsUnit.Pixel);
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        using var muted = new SolidBrush(Color.FromArgb(198, 195, 198));
        var date = local.ToString("MM-dd");
        var weekPrefix = "周";
        var weekDay = "日一二三四五六"[(int)local.DayOfWeek].ToString();
        var dateWidth = g.MeasureString(date, font, int.MaxValue, format).Width;
        var prefixWidth = g.MeasureString(weekPrefix, font, int.MaxValue, format).Width;
        var dayWidth = g.MeasureString(weekDay, font, int.MaxValue, format).Width;
        var visibleCenter = origin.X + (ClockWidth + (digits[0] == 1 ? 33 : 0)) / 2f;
        var left = visibleCenter - (dateWidth + 10 + prefixWidth + dayWidth) / 2;
        g.DrawString(date, font, muted, left, origin.Y + 86, format);
        g.DrawString(weekPrefix, font, muted, left + dateWidth + 10, origin.Y + 86, format);
        g.DrawString(weekDay, font, Brushes.Yellow, left + dateWidth + 10 + prefixWidth, origin.Y + 86, format);
    }
}
