using System.Net;

namespace AIBotBridge;

internal static class StockFallbackSelfTest
{
    internal static async Task RunAsync()
    {
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
