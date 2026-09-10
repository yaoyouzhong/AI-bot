using System.Drawing.Text;

namespace AIBotBridge;

internal sealed class LocalizedTextResources
{
    private readonly object _sync = new();
    private string _weatherKey = string.Empty;
    private string _stocksKey = string.Empty;
    private int _weatherRevision;
    private int _stocksRevision;
    private byte[] _weather = Array.Empty<byte>();
    private byte[] _stocks = Array.Empty<byte>();

    internal IReadOnlyList<ResourcePayload> Capture(WeatherSnapshot? weather, StockSnapshot? stocks)
    {
        lock (_sync)
        {
            if (weather is not null)
            {
                var key = weather.City + "\n" + weather.Condition;
                if (key != _weatherKey)
                {
                    _weatherKey = key;
                    _weather = RenderLines(232, 24, [weather.City + "  " + weather.Condition], 20, 24);
                    _weatherRevision++;
                }
            }
            if (stocks is not null)
            {
                var names = stocks.Quotes.Take(20).Select(quote => quote.Name).ToArray();
                var key = string.Join('\n', names);
                if (key != _stocksKey)
                {
                    _stocksKey = key;
                    _stocks = RenderStockNames(names);
                    _stocksRevision++;
                }
            }

            var result = new List<ResourcePayload>(2);
            if (_weatherRevision > 0) result.Add(new(BinaryResourceKind.WeatherText, _weatherRevision, _weather));
            if (_stocksRevision > 0) result.Add(new(BinaryResourceKind.StockLabels, _stocksRevision, _stocks));
            return result;
        }
    }

    internal static byte[] RenderStockNames(IReadOnlyList<string> names)
    {
        var pixels=new byte[156*400*2];
        for(int index=0;index<Math.Min(20,names.Count);index++)
        {
            using var row=new Bitmap(156,20);
            using var g=Graphics.FromImage(row);
            g.Clear(Color.Black);
            g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
            // Legacy device cache uses 21px glyphs (9pt at the original 175% desktop DPI).
            // Physical display resources must not shrink when rendered by a DPI-unaware CLI.
            using var font=new Font("Microsoft YaHei UI",21f,FontStyle.Regular,GraphicsUnit.Pixel);
            TextRenderer.DrawText(g,names[index],font,new Rectangle(0,0,156,20),Color.FromArgb(210,210,210),Color.Black,
                TextFormatFlags.Right|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);
            PetAssetImporter.EncodeRgb565(row).CopyTo(pixels,index*156*20*2);
        }
        return pixels;
    }

    internal static byte[] RenderLines(int width, int height, IReadOnlyList<string> lines,
        int fontPixels, int rowHeight, StringAlignment alignment = StringAlignment.Near, FontStyle style = FontStyle.Bold)
    {
        using var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Black);
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var font = new Font("Microsoft YaHei UI", fontPixels, style, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.White);
        using var format = new StringFormat
        {
            Alignment = alignment,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        for (var index = 0; index < lines.Count && index * rowHeight < height; index++)
            graphics.DrawString(lines[index], font, brush,
                new RectangleF(0, index * rowHeight, width, rowHeight), format);
        return PetAssetImporter.EncodeRgb565(bitmap);
    }
}
