namespace AIBotBridge;

internal static class SystemPageRenderer
{
    internal static void Draw(Graphics g, StatusSnapshot status)
    {
        g.Clear(Color.Black);
        g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var grey=new SolidBrush(Color.FromArgb(140,140,140));
        using var green=new SolidBrush(Color.FromArgb(0,217,51));
        using var yellow=new SolidBrush(Color.FromArgb(255,204,0));
        var m = status.SystemMetrics;
        Text(g, "DOWN", 14, 8, 8, grey); Text(g, "UP", 134, 8, 8, grey);
        Text(g, m is null ? "--" : Speed(m.DownloadBytesPerSecond) + "/s", 12, 19, 19, green);
        Text(g, m is null ? "--" : Speed(m.UploadBytesPerSecond) + "/s", 132, 19, 19, yellow);
        var history = m?.History?.TakeLast(224).ToArray() ?? [];
        double scale = Scale(history.Select(p => Math.Max(p.Upload, p.Download)).DefaultIfEmpty(0).Max());
        using var grid = new Pen(Color.FromArgb(41, 41, 41));
        for (int q = 1; q <= 3; q++) g.DrawLine(grid, 8, 60 + q * 32, 232, 60 + q * 32);
        using(var font=new Font("Consolas",8,FontStyle.Regular,GraphicsUnit.Pixel))
        using(var right=new StringFormat{Alignment=StringAlignment.Far})g.DrawString(Speed(scale),font,grey,new RectangleF(120,46,112,12),right);
        if (history.Length > 0)
        {
            double Value(int column, bool up)
            {
                int index = column - (224 - history.Length);
                return index < 0 ? 0 : up ? history[Math.Min(index, history.Length - 1)].Upload : history[Math.Min(index, history.Length - 1)].Download;
            }
            PointF[] Points(bool up) => Enumerable.Range(0, 224).Select(i => new PointF(8 + i,
                187 - (float)Math.Clamp((Value(Math.Max(0, i - 1), up) + Value(i, up) + Value(Math.Min(223, i + 1), up)) / 3 / scale, 0, 1) * 126)).ToArray();
            var down = Points(false);
            using var fill = new SolidBrush(Color.FromArgb(0, 84, 0));
            g.FillPolygon(fill, new[] { new PointF(8, 187) }.Concat(down).Append(new PointF(231, 187)).ToArray());
            using var downLine=new Pen(Color.FromArgb(0,217,51),3){LineJoin=System.Drawing.Drawing2D.LineJoin.Round};
            using var upLine=new Pen(Color.FromArgb(255,204,0),3){LineJoin=System.Drawing.Drawing2D.LineJoin.Round};
            g.DrawLines(downLine, down);g.DrawLines(upLine,Points(true));
        }
        using(var label=new Font("Consolas",7f*96/72,FontStyle.Regular,GraphicsUnit.Pixel))
        using(var value=new Font("Consolas",11.5f*96/72,FontStyle.Bold,GraphicsUnit.Pixel)) {
            g.DrawString("CPU",label,grey,28,196);g.DrawString("MEM",label,grey,130,196);
            g.DrawString(m is null?"--":$"{m.CpuPercent:0}%",value,Brushes.White,62,189);
            g.DrawString(m is null?"--":$"{m.MemoryPercent:0}%",value,Brushes.White,164,189);
        }
        using(var footer=new Font("Consolas",8,FontStyle.Regular,GraphicsUnit.Pixel))
        using(var center=new StringFormat{Alignment=StringAlignment.Center})g.DrawString("SYSTEM MONITOR",footer,grey,new RectangleF(0,212,240,12),center);
    }
    internal static string Speed(double bytes) => bytes >= 1_000_000 ? $"{bytes / 1_000_000:0.0}M" : bytes >= 1000 ? $"{bytes / 1000:0}K" : $"{bytes:0}B";
    internal static double Scale(double peak) => Math.Max(10240,peak*8/7);
    private static void Text(Graphics g, string text, int x, int y, int size, Brush brush)
    {
        using var font = new Font("Consolas", size, size > 10 ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        g.DrawString(text, font, brush, x, y);
    }
}
