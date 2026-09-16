using System.Text.Json;
namespace AIBotBridge;
internal static class XiaomiQuotaSelfTest
{
    internal static void Run()
    {
        const string good="""{"code":0,"data":{"totalCredits":"1000","usedCredits":250,"remainingCredits":750}}""";
        using(var d=JsonDocument.Parse(good)) Require(XiaomiQuota.Parse(d.RootElement)==25,"Credit utilization");
        foreach(var pair in new[]{("{\"usedPercent\":0}",0d),("{\"usagePercent\":100}",100d),("{\"usageRate\":0.25}",25d)}) {
            using var d=JsonDocument.Parse(pair.Item1);Require(XiaomiQuota.Parse(d.RootElement)==pair.Item2,"Percent scale and zero");
        }
        foreach(var bad in new[]{"{}","{\"balance\":50}","{\"ratio\":0.2}","{\"data\":[{\"usedPercent\":25}]}","{\"limits\":{\"usedPercent\":25}}",good.Replace("\"code\":0","\"code\":401"),good.Replace("750","700"),good.Replace("250","-1"),good.Replace("1000","0"),"{\"usedPercent\":101}","{\"usedPercent\":\"NaN\"}"}) {
            try { using var d=JsonDocument.Parse(bad);XiaomiQuota.Parse(d.RootElement);throw new InvalidOperationException("Ambiguous MiMo data accepted"); }
            catch(JsonException) { }
        }
        Require(XiaomiQuota.IsEndpoint("https://platform.xiaomimimo.com/api/v1/tokenPlan/usage?x=1"),"Exact console route");
        foreach(var url in new[]{"http://platform.xiaomimimo.com/api/v1/tokenPlan/usage","https://platform.xiaomimimo.com.evil.test/api/v1/tokenPlan/usage","https://evil.test/api/v1/tokenPlan/usage","https://platform.xiaomimimo.com/api/v1/balance","https://platform.xiaomimimo.com:8443/api/v1/tokenPlan/usage"}) Require(!XiaomiQuota.IsEndpoint(url),"Reject unrelated origins and wallets");
        var service=new MigratedDomestic.DomesticQuotaService();
        service.SetXiaomi(25);service.ReportWebFailure("xiaomi","login expired");
        Require(service.Snapshot.XiaomiPlanPct==25 && service.RefreshHealth.Failed("xiaomi"),"Retain cache on failure");
        service.SetXiaomi(30);Require(!service.RefreshHealth.Failed("xiaomi"),"Recover health on valid usage");
        try { service.SetXiaomi(double.NaN);throw new InvalidOperationException("Invalid setter accepted"); } catch(JsonException) { }
        Require(new MigratedDomestic.DomesticQuotaService().Snapshot.XiaomiPlanPct==30,"Cache survives reload");
        using(var bridge=new MigratedDomesticBridge()) {
            bridge.ApplyCapturedResponse("xiaomi",good);
            var q=bridge.Snapshot.Xiaomi;
            Require(q?.PlanPercent==25 && q.PrimaryPercent==null && q.WeeklyPercent==null && q.Balance==null,"Plan does not become weekly quota or wallet");
        }
        var cycle=new DisplayPolicy("auto",true,15,new[]{"domestic_xiaomi","domestic_deepseek","weather"});
        Require(QuotaMonitoringPolicy.Monitored(cycle,"alibaba",p=>p=="xiaomi").SetEquals(new[]{"xiaomi"}),"Cycle requires configured provider");
        Require(QuotaMonitoringPolicy.Monitored(cycle,"alibaba",_=>false).Count==0,"Never configured means no monitoring");
        Require(QuotaMonitoringPolicy.Selected(cycle with {SelectedMode="domestic_deepseek"},"alibaba").SetEquals(new[]{"deepseek"}),"Fixed page overrides saved cycle selection");
        Require(QuotaMonitoringPolicy.Selected(cycle with {SelectedMode="weather"},"alibaba").Count==0,"Non-domestic fixed page is quiet");
        Require(QuotaMonitoringPolicy.Selected(cycle with {CycleEnabled=false},"alibaba").Count==0,"Disabled cycle is quiet");
        Require(QuotaMonitoringPolicy.Selected(cycle with {SelectedMode="domestic"},"alibaba").SetEquals(new[]{"qwen"}),"Generic domestic and alias");
        var health=new QuotaRefreshHealth();health.Fail("deepseek","hidden");health.Fail("xiaomi","selected");
        Require(health.TakeWarning(p=>p=="xiaomi")=="selected" && health.TakeWarning(p=>p=="xiaomi")==null,"Unselected failure cannot block or leak warnings");
        Require(health.TakeWarning(p=>p=="deepseek")=="hidden" && health.TakeWarning()==null,"Selected later warns once");
        health.Fail("kimi","old");health.Recover("kimi");Require(health.TakeWarning()==null,"Recovered hidden failure is discarded");
        Require(DisplayModes.DomesticPage("xiaomi")=="domestic_xiaomi","Display routing");
        Console.WriteLine("XIAOMI_QUOTA_SELF_TEST_OK fixture-parser/origin/cache/bridge/display/selected-and-configured-reminders; no live account");
    }
    private static void Require(bool ok,string message) {if(!ok)throw new InvalidOperationException(message);}
}
