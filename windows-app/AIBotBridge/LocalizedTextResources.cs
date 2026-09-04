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
                    _stocks = RenderLines(120, 400, names, 18, 20);
                    _stocksRevision++;
                }
            }

            var result = new List<ResourcePayload>(2);
            if (_weatherRevision > 0) result.Add(new(BinaryResourceKind.WeatherText, _weatherRevision, _weather));
            if (_stocksRevision > 0) result.Add(new(BinaryResourceKind.StockLabels, _stocksRevision, _stocks));
            return result;
        }
    }

    internal static byte[] RenderLines(int width, int height, IReadOnlyList<string> lines,
        int fontPixels, int rowHeight)
    {
        using var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Black);
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var font = new Font("Microsoft YaHei UI", fontPixels, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.White);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Near,
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
