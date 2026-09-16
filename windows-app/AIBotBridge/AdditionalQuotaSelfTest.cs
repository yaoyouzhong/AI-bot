using System.Net;
using System.Text.Json;

namespace AIBotBridge;
internal static class AdditionalQuotaSelfTest
{
    private sealed class Handler(HttpStatusCode code, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) });
    }
    internal static void Run() => RunAsync().GetAwaiter().GetResult();
    private static async Task RunAsync()
    {
        Require(AdditionalQuotaApi.Hmac("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "bce-auth-v1/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/2015-04-27T08:23:49Z/1800") == "1d5ce5f464064cbee060330d973218821825ac6952368a482a592e6615aef479", "Official BCE signing-key vector");
        var credentials = JsonSerializer.Serialize(new AdditionalQuotaApi.BaiduCredentials("fixture-ak", "fixture-sk", "fixture-package"));
        using(var req = AdditionalQuotaApi.CreateRequest("baidu", credentials, DateTimeOffset.Parse("2026-09-16T00:00:00Z"))) {
            Require(req.Method == HttpMethod.Post && req.RequestUri!.Host == "qianfan.baidubce.com" && req.RequestUri.Query == "?Action=DescribePackageResource", "Model-only API route");
            Require(req.Headers.GetValues("Authorization").Single().Contains("/host;x-bce-date/") && (await req.Content!.ReadAsStringAsync()).Contains("fixture-package"), "Signed resource package selection");
        }
        const string package = """{"result":{"instance":{"packageId":"fixture-package","serviceName":"ernie-4.0-8k","specification":"1000","used":250,"status":"Active","expiredTime":"2035-01-01T00:00:00Z"}}}""";
        using var pkgDoc = JsonDocument.Parse(package);
        var quota = AdditionalQuotaApi.Parse("baidu",pkgDoc.RootElement,"fixture-package");
        Require(quota.PlanPercent == 25 && quota.Balance == null && quota.Plan == "ernie-4.0-8k", "Package usage never cloud wallet");
        foreach (var bad in new[]{"{}", "{\"cashBalance\":75}", package.Replace("fixture-package","wrong-package"), package.Replace("1000","0"), package.Replace("250","1001"), package.Replace("250","-1")}) {
            try { using var d=JsonDocument.Parse(bad); AdditionalQuotaApi.Parse("baidu",d.RootElement,"fixture-package"); throw new InvalidOperationException("Bad package accepted"); }
            catch (Exception e) when (e is JsonException or KeyNotFoundException) { }
        }
        using(var expired=JsonDocument.Parse(package.Replace("Active","Expired")))
            Require(AdditionalQuotaApi.Parse("baidu",expired.RootElement,"fixture-package").PlanPercent==100,"Expired package has no usable remainder");
        foreach (var balance in new[]{"0", "75.37", "-1.25"}) {
            using var d=JsonDocument.Parse("{\"object\":\"account\",\"type\":\"prepaid\",\"balance\":"+balance+",\"total_cash_balance\":999}");
            var s=AdditionalQuotaApi.Parse("stepfun",d.RootElement);
            Require(s.Balance==double.Parse(balance,System.Globalization.CultureInfo.InvariantCulture) && s.PlanPercent==null,"StepFun current balance, not cumulative top-ups or Step Plan");
        }
        foreach(var code in new[]{HttpStatusCode.Unauthorized,HttpStatusCode.Forbidden,HttpStatusCode.TooManyRequests,HttpStatusCode.Redirect,HttpStatusCode.InternalServerError}) {
            using var client=new HttpClient(new Handler(code,"sensitive-response-fixture"));
            try { await AdditionalQuotaApi.FetchAsync("stepfun","fixture-key",client); throw new InvalidOperationException("HTTP error accepted"); }
            catch(HttpRequestException e) { Require(e.StatusCode==code && !e.Message.Contains("sensitive"),"HTTP error is redacted and classified"); }
        }
        using(var client=new HttpClient(new Handler(HttpStatusCode.OK,package)))
            Require((await AdditionalQuotaApi.FetchAsync("baidu",credentials,client)).PlanPercent==25,"HTTP-to-parser package path");
        var service = new MigratedDomestic.DomesticQuotaService(); service.SetAdditional(quota);
        Require(new MigratedDomestic.DomesticQuotaService().Snapshot.Baidu?.PlanPercent==25,"Persist/reload package without credentials");
        var snapshot=new DomesticQuotaSnapshot(null,null,null,null,Baidu:quota);
        var roundtrip=JsonSerializer.Deserialize<DomesticQuotaSnapshot>(JsonSerializer.Serialize(snapshot,JsonDefaults.Options),JsonDefaults.Options);
        Require(roundtrip?.Baidu==quota && DisplayModes.IsValid("domestic_baidu") && DisplayModes.IsValid("domestic_stepfun"),"Cross-platform optional fields and mode registration");
        Console.WriteLine("ADDITIONAL_QUOTA_SELF_TEST_OK official-signing-vector/model-package/balance/auth/schema/cache; simulated HTTP only");
    }
    private static void Require(bool ok,string what) { if(!ok) throw new InvalidOperationException(what); }
}
