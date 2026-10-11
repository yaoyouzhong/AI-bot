using System.Text.Json;

namespace AIBotBridge;

internal static class DomesticQuotaCycleSelfTest
{
    private sealed class LocalUsageHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            if(request.RequestUri?.Host!="127.0.0.1"||request.RequestUri.AbsolutePath!="/api/v1/oauth/usage"||
                request.Headers.Authorization?.Scheme!="Bearer")throw new InvalidOperationException("Kimi read-only local request contract changed");
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new StringContent(body)});
        }
    }
    internal static void Run()
    {
        static void Check(bool ok,string message) {if(!ok)throw new InvalidOperationException(message);}
        static DomesticProviderQuotaSnapshot Local(string json) {using var doc=JsonDocument.Parse(json);return KimiUsageApi.Parse(doc.RootElement);}
        const string currentKimi="""{"data":{"kind":"ok","quota":{"usages":{"limit5h":{"usedRatio":0.2,"resetAt":"2026-10-10T08:00:00Z"},"limit7d":{"usedRatio":0.4,"resetAt":"2026-10-16T00:00:00Z"},"monthTotal":{"usedRatio":0.6,"resetAt":"2026-11-01T00:00:00Z","startAt":"2026-10-01T00:00:00Z","endAt":"2026-11-01T00:00:00Z"}},"extraUsage":{"balanceCents":999}}}}""";
        var kimi=Local(currentKimi);
        using(var client=new HttpClient(new LocalUsageHandler(currentKimi)))
            Check(KimiUsageApi.FetchAsync("fixture-token",58627,client).GetAwaiter().GetResult().WeeklyWindowSeconds==604800,"Read-only HTTP collection retains cycle metadata");
        Check(kimi.PrimaryPercent==20&&kimi.WeeklyPercent==40&&kimi.PlanPercent==60&&kimi.Balance is null,"Named Kimi quota windows stay independent of the wallet");
        Check(kimi.PrimaryWindowSeconds==18000&&kimi.WeeklyWindowSeconds==604800&&kimi.PlanWindowSeconds==31*86400,"Kimi named short/week cycles and actual 31-day bounds");
        var monthlyUnknown=Local(currentKimi.Replace(",\"startAt\":\"2026-10-01T00:00:00Z\",\"endAt\":\"2026-11-01T00:00:00Z\"",""));
        Check(monthlyUnknown.PlanWindowSeconds is null&&monthlyUnknown.PlanResetsAt.HasValue,"Monthly reset alone does not invent a duration");
        var custom=Local("""{"data":{"kind":"ok","summary":{"window":{"duration":1,"unit":"week"},"used":40,"limit":100},"limits":[{"window":{"duration":420,"unit":"minute"},"used":20,"limit":100,"reset_at":"2026-10-10T08:00:00Z"}]}}""");
        Check(custom.PrimaryWindowSeconds==25200&&custom.WeeklyWindowSeconds==604800,"Kimi explicit seven-hour duration is not replaced by five hours");
        Check(Local("""{"data":{"kind":"ok","quota":{"usages":{"monthCode":{"usedRatio":0.2,"resetAt":"2026-11-01T00:00:00Z"}}}}}""").PlanPercent==20,"Kimi monthly code-only allowance is kept when the total allowance is absent");
        foreach(var duration in new[]{"0","-1","0.0001","2147483648"}) {
            using var bad=JsonDocument.Parse("{\"duration\":"+duration+",\"unit\":\"second\"}");
            Check(QuotaCycle.WindowSeconds(bad.RootElement) is null,"Invalid cycle seconds rejected");
        }
        const string miniJson="""{"data":{"plan_name":"Token Plan","current_weekly_used_percent":35,"current_interval_remaining_percent":80,"weekly_remains_time":86400000,"remains_time":3600000}}""";
        var mini=DomesticQuotaService.Parse("minimax",miniJson);
        Check(mini.PrimaryWindowSeconds==18000&&mini.WeeklyWindowSeconds==604800&&Math.Abs((mini.PrimaryResetsAt!.Value-DateTimeOffset.UtcNow).TotalSeconds-3600)<2,"MiniMax actual fixed cycles are distinct from remaining milliseconds");
        const string aliJson="""{"SubscriptionName":"Token Plan 团队版","data":{"TotalValue":100,"TotalSurplusValue":60,"StartTime":"2026-02-01T00:00:00Z","EndTime":"2026-03-01T00:00:00Z"}}""";
        var ali=DomesticQuotaService.Parse("alibaba",aliJson);
        Check(ali.PlanWindowSeconds==28*86400&&ali.PlanResetsAt==DateTimeOffset.Parse("2026-03-01T00:00:00Z"),"Qwen actual February bounds are not a guessed 30-day month");
        var personal=DomesticQuotaService.Parse("alibaba","""{"SubscriptionName":"Token Plan 个人版","data":{"TotalValue":100,"TotalSurplusValue":60,"NextResetTime":"2026-11-01T00:00:00Z"}}""");
        Check(personal.PlanWindowSeconds==30*86400,"Qwen explicit personal plan retains its documented 30-day cycle");
        var unspecified=DomesticQuotaService.Parse("alibaba","""{"data":{"TotalValue":100,"TotalSurplusValue":60,"NextResetTime":"2026-11-01T00:00:00Z"}}""");
        Check(unspecified.PlanWindowSeconds is null,"Generic Qwen plan does not inherit personal or team assumptions");
        using(var glmDoc=JsonDocument.Parse("""{"success":true,"data":{"limits":[{"type":"TOKENS_LIMIT","unit":3,"number":7,"percentage":25,"nextResetTime":1791619200000},{"type":"TOKENS_LIMIT","unit":6,"number":1,"percentage":40}]}}""")) {
            var glm=QuotaSourceApi.Parse("zhipu",glmDoc.RootElement);
            Check(glm.PrimaryWindowSeconds==25200&&glm.WeeklyWindowSeconds==604800,"GLM retains explicit API units and quantities");
        }
        const string xiaomiJson="""{"data":{"totalCredits":100,"usedCredits":25,"cycleStartAt":"2026-10-01T00:00:00Z","cycleEndAt":"2026-11-01T00:00:00Z"}}""";
        using(var doc=JsonDocument.Parse(xiaomiJson)) {
            var xiaomi=XiaomiQuota.ParseSnapshot(doc.RootElement);
            Check(xiaomi.PlanWindowSeconds==31*86400&&xiaomi.PlanResetsAt.HasValue,"MiMo retains explicit package bounds");
        }
        using(var doc=JsonDocument.Parse("""{"data":{"usedPercent":25,"expireTime":"2026-11-01T00:00:00Z"}}"""))
            Check(XiaomiQuota.ParseSnapshot(doc.RootElement).PlanWindowSeconds is null,"MiMo expiry alone does not invent a cycle");
        using(var doc=JsonDocument.Parse("""{"result":{"instance":{"packageId":"p","specification":100,"used":25,"status":"Active","serviceName":"model","effectiveTime":"2026-10-01T00:00:00Z","expiredTime":"2026-11-01T00:00:00Z"}}}"""))
            Check(AdditionalQuotaApi.Parse("baidu",doc.RootElement,"p").PlanWindowSeconds==31*86400,"Baidu resource package uses effective-to-expiry bounds");
        using(var old=JsonDocument.Parse("""{"KimiFiveHourPct":20,"KimiWeeklyPct":40,"MiniMaxFiveHourPct":30,"MiniMaxWeeklyPct":50,"QwenPlanPct":60}""")) {
            var legacy=LegacyDisplayCache.ParseDomestic(old.RootElement);
            Check(legacy.Kimi!.PrimaryWindowSeconds==18000&&legacy.MiniMax!.WeeklyWindowSeconds==604800&&legacy.Alibaba!.PlanWindowSeconds is null,"Named legacy windows recover durations without first-observation guesses");
        }
        var service=new MigratedDomestic.DomesticQuotaService();service.SetKimi(kimi);
        var cached=new MigratedDomestic.DomesticQuotaService().Snapshot;
        var restored=LegacyDisplayCache.ParseDomestic(JsonSerializer.SerializeToElement(cached,new JsonSerializerOptions{IncludeFields=true})).Kimi!;
        Check(restored.PrimaryWindowSeconds==18000&&restored.WeeklyWindowSeconds==604800&&restored.PlanWindowSeconds==31*86400&&restored.PlanPercent==60,"Kimi collection, cache reload and adaptation retain all cycle metadata");
        service.SetKimi(custom);
        var adapted=LegacyDisplayCache.ParseDomestic(JsonSerializer.SerializeToElement(service.Snapshot,new JsonSerializerOptions{IncludeFields=true})).Kimi!;
        Check(adapted.PrimaryWindowSeconds==25200&&adapted.PlanPercent is null&&adapted.PlanWindowSeconds is null,"Changing windows clears the previous package metadata");
        using(var bridge=new MigratedDomesticBridge()) {
            bridge.ApplyCapturedResponse("alibaba",aliJson);bridge.ApplyCapturedResponse("minimax",miniJson);bridge.ApplyCapturedResponse("xiaomi",xiaomiJson);
            var mapped=Tab5Details.Quotas(new StatusSnapshot(1,"12:00",1791604800,28800,DateTimeOffset.UtcNow,new("idle",0),new("idle",0),DomesticQuotas:bridge.Snapshot));
            Check(mapped[2].PlanWindowSeconds==28*86400&&mapped[4].PrimaryWindowSeconds==18000&&mapped[4].WeeklyWindowSeconds==604800&&mapped[9].PlanWindowSeconds==31*86400,"Capture-to-TAB5 wire preserves domestic durations and reset instants");
            try {bridge.ApplyCapturedResponse("minimax","{}");throw new InvalidOperationException("Invalid response accepted");}catch(JsonException) { }
            Check(bridge.Snapshot.MiniMax!.WeeklyWindowSeconds==604800,"Failed collection preserves the successful cycle");
            bridge.ApplyCapturedResponse("minimax",miniJson.Replace("\"plan_name\":\"Token Plan\"","\"plan_name\":\"Token Plan\",\"current_interval\":{\"window\":{\"duration\":7,\"unit\":\"hour\"}}"));
            Check(bridge.Snapshot.MiniMax!.PrimaryWindowSeconds==25200,"Explicit MiniMax duration overrides the standard named interval");
            bridge.ApplyCapturedResponse("minimax",miniJson.Replace("\"plan_name\":\"Token Plan\"","\"plan_name\":\"Token Plan\",\"current_interval\":{\"window\":{\"duration\":0,\"unit\":\"hour\"}}"));
            Check(bridge.Snapshot.MiniMax!.PrimaryWindowSeconds is null,"Invalid explicit duration stays unknown through snapshot adaptation");
            Check(new MigratedDomestic.DomesticQuotaService().Snapshot.MiniMaxPrimaryWindowSeconds is null,"Cache reload does not reinterpret an explicit unknown duration as five hours");
        }
        Console.WriteLine("DOMESTIC_QUOTA_CYCLE_OK named and explicit API periods, 28/31-day bounds, no unknown-month guessing, legacy/cache/capture/TAB5 integration; fixture data");
    }
}
