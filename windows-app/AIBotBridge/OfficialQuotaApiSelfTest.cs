using System.Net;
using System.Text.Json;

namespace AIBotBridge;

internal static class OfficialQuotaApiSelfTest
{
    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        internal Uri? Uri;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri=request.RequestUri;
            if(request.Method!=HttpMethod.Get || request.Headers.Authorization?.Scheme!="Bearer") throw new InvalidOperationException("API request contract.");
            return Task.FromResult(new HttpResponseMessage(status){Content=new StringContent(body)});
        }
    }
    internal static void Run() => RunAsync().GetAwaiter().GetResult();
    private static async Task RunAsync()
    {
        const string balance="""{"is_available":true,"balance_infos":[{"currency":"USD","total_balance":"8.00"},{"currency":"CNY","total_balance":"75.37","granted_balance":"1","topped_up_balance":"74.37"}]}""";
        using(var handler=new Handler(HttpStatusCode.OK,balance))
        using(var client=new HttpClient(handler)) {
            var value=await DeepSeekBalanceApi.FetchAsync("fixture-key",client:client);
            Require(value.Balance==75.37 && value.Currency=="CNY" && handler.Uri?.AbsoluteUri==DeepSeekBalanceApi.Endpoint,"Official total/currency route");
        }
        using(var doc=JsonDocument.Parse("""{"is_available":false,"balance_infos":[{"currency":"CNY","total_balance":"0.00"}]}"""))
            Require(DeepSeekBalanceApi.Parse(doc.RootElement).Balance==0,"Zero is a valid balance");
        foreach(var status in new[]{HttpStatusCode.Unauthorized,HttpStatusCode.Forbidden,HttpStatusCode.TooManyRequests,HttpStatusCode.InternalServerError,HttpStatusCode.Redirect})
        {
            using var handler=new Handler(status,"private-response-fixture");using var client=new HttpClient(handler);
            var result=await DeepSeekBalanceApi.FetchAsync("fixture-key",client:client);
            Require(!result.Success && !result.Error.Contains("private-response") && result.RateLimited==(status==HttpStatusCode.TooManyRequests),"HTTP failures must never replace cached data or reveal bodies");
        }
        foreach(var invalid in new[]{"{}","<html>login</html>","""{"balance_infos":[{"currency":"CNY","total_balance":"NaN"}]}""",
            """{"balance_infos":[{"currency":"CNY","balance":"75"}]}"""}) {
            using var handler=new Handler(HttpStatusCode.OK,invalid);using var client=new HttpClient(handler);
            Require(!(await DeepSeekBalanceApi.FetchAsync("fixture-key",client:client)).Success,"Invalid payload rejected");
        }
        const string kimi="""{"data":{"kind":"ok","summary":{"window":{"duration":1,"unit":"week"},"used":20,"limit":100},"limits":[{"window":{"duration":300,"unit":"minute"},"used":5,"limit":100}]}}""";
        using(var handler=new Handler(HttpStatusCode.OK,kimi))
        using(var client=new HttpClient(handler)) {
            var value=await KimiUsageApi.FetchAsync("fixture-local-token",58627,client);
            Require(value.WeeklyPercent==20 && value.PrimaryPercent==5 && value.Balance==null && handler.Uri?.Host=="127.0.0.1","Kimi subscription is not a wallet");
        }
        using(var doc=JsonDocument.Parse("""{"data":{"kind":"error","message":"private upstream detail"}}""")) {
            bool rejected=false;try { KimiUsageApi.Parse(doc.RootElement); } catch(JsonException) { rejected=true; }
            Require(rejected,"Kimi in-band errors rejected");
        }
        var health=new QuotaRefreshHealth();
        health.Fail("deepseek","first"); health.Fail("deepseek","updated");
        Require(health.TakeWarning()=="updated" && health.TakeWarning()==null,"Repeated failure reminder deduplicated");
        health.Recover("deepseek");Require(!health.Failed("deepseek"),"Recovery clears stale state");
        health.Fail("deepseek","second");Require(health.TakeWarning()=="second","New failure episode reminds again");
        health.Fail("kimi","old");health.Recover("kimi");health.Fail("kimi","new");
        Require(health.TakeWarning()=="new" && health.TakeWarning()==null,"Recovery before delivery does not duplicate warnings");
        foreach(var provider in new[]{"qwen","kimi","minimax","deepseek","zhipu"}) {
            var service=new MigratedDomestic.DomesticQuotaService();
            service.ReportWebFailure(provider,"timeout",explicitlyRequested:true);
            Require(service.RefreshHealth.Failed(provider),"All supported providers report browser failures");
        }
        var retained=new MigratedDomestic.DomesticQuotaService();
        retained.SetDeepSeek(77.77,"CNY",usedCost:22.23);
        retained.ReportWebFailure("deepseek","login expired");
        Require(retained.Snapshot.DeepSeekBalance==77.77 && retained.RefreshHealth.Failed("deepseek"),"Failure retains last balance");
        retained.SetDeepSeek(75.37,"CNY");
        Require(retained.Snapshot.DeepSeekBalance==75.37 && retained.Snapshot.DeepSeekUsedCost==null && !retained.RefreshHealth.Failed("deepseek"),"Recovery replaces balance without carrying old-account cost");
        Console.WriteLine("OFFICIAL_QUOTA_API_SELF_TEST_OK HTTP/auth/schema/currency/zero/Kimi-window/error/redaction/reminder-recovery; fake HTTP only");
    }
    private static void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
}
