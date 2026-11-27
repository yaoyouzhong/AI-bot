using System.Net;
using System.Text.Json;

namespace AIBotBridge;

internal static class CodingPlanSelfTest
{
    internal const string Glm = """{"success":true,"code":200,"data":{"limits":[{"type":"TIME_LIMIT","percentage":99},{"type":"TOKENS_LIMIT","unit":3,"number":5,"percentage":12.5,"nextResetTime":1795824000000},{"type":"TOKENS_LIMIT","unit":6,"number":1,"percentage":25,"nextResetTime":1796083200000}]}}""";
    internal const string Qwen = """{"success":true,"data":{"codingPlanInstanceInfos":[{"status":"VALID","codingPlanQuotaInfo":{"per5HourUsedQuota":0,"per5HourTotalQuota":6000,"per5HourQuotaNextRefreshTime":"2026-11-27 18:00:00","perWeekUsedQuota":9000,"perWeekTotalQuota":45000,"perWeekQuotaNextRefreshTime":"2026-11-30 00:00:00","perBillMonthUsedQuota":22500,"perBillMonthTotalQuota":90000,"perBillMonthQuotaNextRefreshTime":"2026-12-03 00:00:00"}}]}}""";

    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Check(request.Method == HttpMethod.Get && request.RequestUri?.AbsoluteUri == "https://open.bigmodel.cn/api/monitor/usage/quota/limit", "Read-only official GLM route");
            Check(request.Headers.GetValues("Authorization").Single() == "fixture-plan-key", "GLM raw subscription credential, without Bearer prefix");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    internal static void Run()
    {
        using (var client = new HttpClient(new Handler(HttpStatusCode.OK, Glm)))
        {
            var q = QuotaSourceApi.FetchAsync("zhipu", "fixture-plan-key", client).GetAwaiter().GetResult();
            Check(q.PrimaryPercent == 12.5 && q.WeeklyPercent == 25 && q.Balance is null && q.PrimaryWindowSeconds == 18000, "GLM subscription excludes MCP and wallet");
        }
        foreach (var code in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests, HttpStatusCode.Redirect, HttpStatusCode.InternalServerError })
        {
            using var client = new HttpClient(new Handler(code, "private upstream body"));
            try { QuotaSourceApi.FetchAsync("zhipu", "fixture-plan-key", client).GetAwaiter().GetResult(); throw new InvalidOperationException("HTTP error accepted"); }
            catch (HttpRequestException e) { Check(e.StatusCode == code && !e.Message.Contains("private"), "HTTP failure classification and redaction"); }
        }
        var service = new MigratedDomestic.DomesticQuotaService();
        service.SelectProduct("qwen", "coding"); service.SelectProduct("zhipu", "coding");
        Check(service.ApplyPlanResponse("qwen", "coding", Qwen) && service.ApplyPlanResponse("zhipu", "coding", Glm), "Capture-to-cache integration");
        var qwen = service.Plan("qwen")!;
        Check(qwen.PrimaryPercent == 0 && qwen.WeeklyPercent == 20 && qwen.PlanPercent == 25, "Three independent Qwen quota windows including zero");
        Check(qwen.PrimaryWindowSeconds == 18000 && qwen.WeeklyWindowSeconds == 604800 && qwen.PlanWindowSeconds is null
            && qwen.PlanResetsAt == new DateTimeOffset(2026, 12, 3, 0, 0, 0, TimeSpan.FromHours(8)), "Qwen reset zone and unknown calendar-month duration");
        foreach (var bad in new[] { "{}", "<html>login</html>", Qwen.Replace("true", "false"), Qwen.Replace("VALID", "EXPIRED"), Qwen.Replace("6000", "0"), Qwen.Replace("22500", "-1") })
        {
            try { service.ApplyPlanResponse("qwen", "coding", bad); throw new InvalidOperationException("Bad quota accepted"); }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException && e.Message != "Bad quota accepted") { }
            Check(service.Plan("qwen") == qwen, "Failed capture retains last-good plan and timestamp");
        }
        service.SelectProduct("zhipu", "wallet");
        Check(!service.SupportsSelectedApi("zhipu") && service.Plan("zhipu") is null && !service.ApplyPlanResponse("zhipu", "coding", Glm), "Late plan response cannot overwrite a wallet selection");
        service.SetZhipu(new("zhipu", "API PAYG", null, null, null, null, 42, null, "CNY", DateTimeOffset.UtcNow, false));
        service.SelectProduct("zhipu", "coding");
        Check(service.Plan("zhipu")?.WeeklyPercent == 25 && service.Snapshot.Zhipu?.Balance == 42, "Separate retained wallet and subscription");
        Check(new MigratedDomestic.DomesticQuotaService().Plan("qwen") == qwen, "Subscription cache reload");
        using (var bridge = new MigratedDomesticBridge())
            Check(bridge.Snapshot.Alibaba?.PlanPercent == 25 && bridge.Snapshot.Zhipu?.WeeklyPercent == 25 && bridge.Snapshot.Zhipu?.Balance is null, "Selected subscription reaches bridge status using existing quota fields");

        foreach (var provider in new[] { "volcengine", "tencent", "baidu", "stepfun", "huawei" })
        {
            var product = CodingPlanSources.Products(provider)[0];
            using var rows = JsonDocument.Parse("""[{"label":"每5小时","percent":0},{"label":"每周","percent":20},{"label":"套餐额度","percent":50}]""");
            var plan = CodingPlanPage.Parse(provider, product.Id, rows.RootElement);
            service.SelectProduct(provider, product.Id); service.SetPlan(provider, product.Id, plan);
            Check(new MigratedDomestic.DomesticQuotaService().Plan(provider)?.PlanPercent == 50 && plan.Balance is null && plan.PlanWindowSeconds is null, "Labelled page progress persists without wallet or invented month");
            Check(!CodingPlanPage.Allows(provider, product.Id, "https://example.com/plan") && !CodingPlanPage.Allows(provider, product.Id, product.Url.Replace("https://", "http://")), "Trusted console scope");
        }
        foreach (var bad in new[] { "[]", "[{\"label\":\"每周\",\"percent\":101}]", "[{\"label\":\"云余额\",\"percent\":20}]", "[{\"label\":\"每周 本月\",\"percent\":20}]", "[{\"label\":\"每周\",\"percent\":20},{\"label\":\"每周\",\"percent\":30}]" })
        {
            try { using var d = JsonDocument.Parse(bad); CodingPlanPage.Parse("tencent", "coding", d.RootElement); throw new InvalidOperationException("Bad page accepted"); }
            catch (JsonException) { }
        }
        foreach (var host in new[] { "www.minimax.cn", "platform.minimax.cn", "www.minimaxi.com" })
            Check(MiniMaxQuota.IsEndpoint("https://" + host + "/v1/token_plan/remains"), "MiniMax current and legacy exact official domains");
        Check(!MiniMaxQuota.IsEndpoint("https://minimax.evil.test/v1/token_plan/remains") && !MiniMaxQuota.IsEndpoint("https://www.minimax.cn/usage"), "MiniMax unrelated responses excluded");
        Check(CodingPlanSources.Products("deepseek").Length == 0, "No invented DeepSeek subscription");
        foreach (var name in new[] { "GLM Coding Plan", "Kimi Coding Plan", "百炼 Coding Plan", "Kimi Code", "Step Plan" })
            Check(CodingPlanSources.DisplayName(name) == "Coding Plan" && DomesticDisplayText.Membership("zhipu", name, false) == "Coding Plan", "Display labels omit repeated provider/model names");
        foreach (var name in new[] { "MiMo Token Plan", "MiniMax Token Plan", "百炼 Token Plan", "M Plan" })
            Check(CodingPlanSources.DisplayName(name) == "Token Plan", "Token product display labels omit provider names");
        foreach (var provider in new[] { "qwen", "kimi", "zhipu", "xiaomi", "minimax", "volcengine", "tencent", "baidu", "stepfun", "huawei" })
            Check(CodingPlanSources.Products(provider).Where(p => CodingPlanSources.IsPlan(p.Id)).All(p => p.Label is "Coding Plan" or "Token Plan"), "Dropdown labels use only product types");
        Console.WriteLine("CODING_PLAN_SELF_TEST_OK GLM HTTP/auth/errors; Qwen 3 windows; product isolation; cache/status; five console adapters; exact hosts; simulated only");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
