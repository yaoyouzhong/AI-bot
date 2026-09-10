using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;

namespace AIBotBridge;

// Desktop mirror has its own legacy specification, separate from device graphics.
internal static class WeatherMirrorRenderer
{
    internal static void Draw(Graphics g,StatusSnapshot status,string? airLabel=null)
    {
        g.Clear(Color.Black);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
        if(status.Weather is not {} weather) {
            Label(g,"请在托盘设置天气城市",new(8,100,224,30),17,Color.LightGray);return;
        }
        using var typographic=(StringFormat)StringFormat.GenericTypographic.Clone();typographic.FormatFlags|=StringFormatFlags.NoWrap;
        float size=20;
        for(;size>10;size--) {
            using var candidate=new Font("Microsoft YaHei UI",size,FontStyle.Bold,GraphicsUnit.Pixel);
            if(Math.Ceiling(g.MeasureString(weather.City,candidate,int.MaxValue,typographic).Width)<=120)break;
        }
        using(var font=new Font("Microsoft YaHei UI",size,FontStyle.Bold,GraphicsUnit.Pixel)) {
            var measured=g.MeasureString(weather.City,font,int.MaxValue,typographic);
            g.DrawString(weather.City,font,Brushes.White,14,1+Math.Max(0,(26-measured.Height)/2),typographic);
        }
        int rangeY=size<17?33:34;
        At(g,$"L {Math.Round(weather.Low):0}C",20,rangeY,14,FontStyle.Bold,Color.Cyan);
        At(g,$"H {Math.Round(weather.High):0}C",81,rangeY,14,FontStyle.Bold,Color.Orange);
        airLabel??=weather.AirQualityLabel;
        if(!string.IsNullOrWhiteSpace(airLabel)&&airLabel!="--") {
            var color=MigratedWeather.WeatherMonitor.AirQualityColor(airLabel);
            var box=airLabel.Length>1?new RectangleF(137.5f,15,39,24):new RectangleF(144.5f,14.5f,25,25);
            float diameter=airLabel.Length>1?16:25;
            using var path=new GraphicsPath();
            foreach(var arc in new[]{(box.Left,box.Top,180),(box.Right-diameter,box.Top,270),(box.Right-diameter,box.Bottom-diameter,0),(box.Left,box.Bottom-diameter,90)})path.AddArc(arc.Item1,arc.Item2,diameter,diameter,arc.Item3,90);
            path.CloseFigure();using var fill=new SolidBrush(MigratedWeather.WeatherMonitor.BadgeBackground(color));using var border=new Pen(color,1.4f);
            g.FillPath(fill,path);g.DrawPath(border,path);Label(g,airLabel,new(136,12,42,30),airLabel.Length>1?12:18,color);
        }
        Label(g,weather.Condition,new(184,12,52,30),weather.Condition.Length>1?19:22,MigratedWeather.WeatherMonitor.ConditionColor(weather.Condition));
        var local=LocalTime(status);
        At(g,local.ToString("HH",CultureInfo.InvariantCulture),17,54,48,FontStyle.Bold,Color.White);
        At(g,local.ToString("mm",CultureInfo.InvariantCulture),101,54,48,FontStyle.Bold,Color.FromArgb(255,204,0));
        At(g,local.ToString("ss",CultureInfo.InvariantCulture),190,80,25,FontStyle.Regular,Color.White);
        using(var date=new Font("Microsoft YaHei UI",19,FontStyle.Bold,GraphicsUnit.Pixel))
            g.DrawString($"{local.Month}月{local.Day}日 周{"日一二三四五六"[(int)local.DayOfWeek]}",date,Brushes.White,14,117);
        At(g,$"TEMP   {Math.Round(weather.Temperature):0}C",14,161,24,FontStyle.Regular,Color.White);
        At(g,$"HUMID  {weather.Humidity}%",14,198,24,FontStyle.Regular,Color.White);
    }
    internal static DateTimeOffset LocalTime(StatusSnapshot status)=>DateTimeOffset.FromUnixTimeSeconds(status.EpochUtc).ToOffset(TimeSpan.FromSeconds(status.Weather?.UtcOffsetSeconds??status.UtcOffsetSeconds));
    private static void At(Graphics g,string text,int x,int y,int size,FontStyle style,Color color)
    {
        using var font=new Font("Consolas",size,style,GraphicsUnit.Pixel);using var brush=new SolidBrush(color);g.DrawString(text,font,brush,x,y);
    }
    private static void Label(Graphics g,string text,RectangleF bounds,int size,Color color)
    {
        using var font=new Font("Microsoft YaHei UI",size,FontStyle.Bold,GraphicsUnit.Pixel);using var brush=new SolidBrush(color);
        using var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};g.DrawString(text,font,brush,bounds,format);
    }
}
