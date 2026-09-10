using System.Text.Json;

namespace AIBotBridge;

internal static class LegacyStockLabelAudit
{
    internal static async Task RunAsync()
    {
        using var runtime=new BridgeRuntime(startRefresh:false);
        var quotes=runtime.Capture().Stocks?.Quotes ?? throw new InvalidOperationException("No current stock cache.");
        using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(5)};
        using var status=JsonDocument.Parse(await http.GetStringAsync("http://127.0.0.1:8765/stock"));
        var codes=status.RootElement.GetProperty("stocks").EnumerateArray().Select(q=>q.GetProperty("code").GetString()).ToArray();
        if(!codes.SequenceEqual(quotes.Select(q=>q.Code)))throw new InvalidOperationException("Watchlists differ; pixel comparison is not valid.");
        var expected=await http.GetByteArrayAsync("http://127.0.0.1:8765/stock/names.raw");
        var actual=LocalizedTextResources.RenderStockNames(quotes.Select(q=>q.Name).ToArray());
        if(expected.Length!=actual.Length)throw new InvalidOperationException("Stock label canvas sizes differ.");
        int differences=0;
        for(int i=0;i<actual.Length;i+=2)
            if(actual[i]!=expected[i+1] || actual[i+1]!=expected[i])differences++;
        Console.WriteLine($"LEGACY_STOCK_LABEL_AUDIT stocks={codes.Length} pixels={actual.Length/2} differingPixels={differences}; RGB565 endian normalized; legacy runtime unchanged");
        if(differences>0)throw new InvalidOperationException("Stock name pixels still differ from running legacy bridge.");
    }
}
