using System.Net;

namespace AIBotBridge;

internal static class StockFallbackSelfTest
{
    internal static async Task RunAsync()
    {
        await VerifyRealtimeAndPartialAsync();
        const string mainland="var hq_str_sh000001=\"Index,100,100,105\";";
        var parsed=StockService.ParseSina(mainland+"\nvar hq_str_rt_hk00700=\"Tencent,HK Name,0,0,0,0,600,-2,-0.33\";\nvar hq_str_gb_aapl=\"Apple,200,1.25,0,2.5\";",
            ["usAAPL","sh000001","hk00700"]);
        Require(parsed.Count==3&&parsed[0].Symbol=="usAAPL"&&parsed[1].ChangePercent=="+5.00%"&&parsed[2].Trend==-1,"Sina market mapping/order/calculation");
        Require(StockService.ParseSina("var hq_str_sh000001=\"bad,0,0,NaN\";",["sh000001"]).Count==0,"Reject invalid numbers");
        using var handler=new FakeQuotes(mainland);
        using var http=new HttpClient(handler);
        var stocks=new StockService(BridgeSettings.CreatePublicSelfTestSettings(),false,http);
        await stocks.RefreshAsync(CancellationToken.None);
        Require(stocks.Snapshot?.Quotes.Single().Price=="105.00"&&stocks.Snapshot.Stale==false&&handler.Calls==2,"Primary HTTP failure falls back");
        var good=stocks.Snapshot;
        handler.FailAll=true;
        await stocks.RefreshAsync(CancellationToken.None);
        Require(stocks.Snapshot?.Stale==true&&stocks.Snapshot.UpdatedAt==good!.UpdatedAt&&stocks.Snapshot.Quotes.SequenceEqual(good.Quotes),"Both failures retain successful cache");
        handler.FailAll=false;handler.EmptyPrimary=true;
        await stocks.RefreshAsync(CancellationToken.None);
        Require(stocks.Snapshot?.Stale==false&&handler.Calls==6,"Empty primary falls back and recovers");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        await stocks.RefreshAsync(cancelled.Token);
        Require(stocks.Snapshot?.Stale==false,"Shutdown cancellation must not erase cache");
        Console.WriteLine("STOCK_FALLBACK_SELF_TEST_OK markets/order/http-error/empty/retain/recover/cancel");
    }
    private static string Tencent(string symbol, string price)
    {
        var fields = Enumerable.Repeat("", 33).ToArray();
        fields[1] = symbol; fields[3] = price; fields[31] = "-0.88"; fields[32] = "-1.85";
        return $"v_{symbol}=\"{string.Join('~', fields)}\";";
    }
    private static async Task VerifyRealtimeAndPartialAsync()
    {
        Require(StockService.ParseTencent(Tencent("r_hk00001", "0.155"), ["hk00001"]).Single().Price == "0.155",
            "Preserve third decimal in Hong Kong quotes");
        Require(StockService.ParseTencent(Tencent("r_hkHSI", "24850.70"), ["hkHSI"]).Single().Price == "24850.70",
            "Do not round Hong Kong index to whole points");
        var settings = BridgeSettings.CreatePublicSelfTestSettings();
        var directory = Path.Combine(Path.GetTempPath(), "aibot-stock-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Require(settings.SaveEditable(new Dictionary<string,string> { ["stock_symbols"] = "sh000001,hk02015,hkHSI" }, out _, directory), "Test settings");
            using var handler = new RealtimeQuotes();
            using var http = new HttpClient(handler);
            var stocks = new StockService(settings, false, http);
            await stocks.RefreshAsync(CancellationToken.None);
            Require(stocks.Snapshot is { Stale: false } s && s.Quotes.Count == 3 &&
                s.Quotes[1].Symbol == "hk02015" && s.Quotes[1].Price == "46.68" && s.Quotes[2].Code == "HSI",
                "Realtime prefixes map back to configured identities and order");
            handler.Partial = true;
            await stocks.RefreshAsync(CancellationToken.None);
            Require(stocks.Snapshot is { Stale: false } partial && partial.Quotes[1].Price == "46.70", "Missing HK quote falls back independently");
            handler.FailFallback = true;
            await stocks.RefreshAsync(CancellationToken.None);
            Require(stocks.Snapshot is { Stale: true } cached && cached.Quotes.Count == 3 && cached.Quotes[1].Price == "46.70",
                "Partial refresh retains missing quote and marks snapshot stale");
            Console.WriteLine("STOCK_REALTIME_SELF_TEST_OK realtime-prefix/index/identity/partial-fallback/retain");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    private sealed class RealtimeQuotes : HttpMessageHandler
    {
        internal bool Partial, FailFallback;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            bool primary = request.RequestUri!.Host == "qt.gtimg.cn";
            string body;
            if (primary)
            {
                Require(request.RequestUri.PathAndQuery == "/q=sh000001,r_hk02015,r_hkHSI", "Primary must request realtime HK symbols");
                body = Tencent("sh000001", "3929.3") + "\n" + Tencent("r_hkHSI", "24850.7") +
                    (Partial ? "" : "\n" + Tencent("r_hk02015", "46.68"));
            }
            else
            {
                Require(request.RequestUri.AbsoluteUri.EndsWith("list=rt_hk02015", StringComparison.Ordinal), "Only missing symbol falls back to realtime Sina");
                body = "var hq_str_rt_hk02015=\"LI,LI,0,0,0,0,46.70,-0.86,-1.81\";";
            }
            return Task.FromResult(new HttpResponseMessage(!primary && FailFallback ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
                { Content = new StringContent(body) });
        }
    }
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private sealed class FakeQuotes(string response):HttpMessageHandler
    {
        internal int Calls;internal bool FailAll,EmptyPrimary;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();Calls++;
            bool primary=request.RequestUri!.Host=="qt.gtimg.cn";
            if(!primary)Require(request.Headers.Referrer?.Host=="finance.sina.com.cn","Sina referer");
            return Task.FromResult(new HttpResponseMessage(FailAll||primary&&!EmptyPrimary?HttpStatusCode.ServiceUnavailable:HttpStatusCode.OK)
                {Content=new StringContent(primary?"":response)});
        }
    }
}
