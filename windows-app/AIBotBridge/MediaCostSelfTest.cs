using System.Text.Json;
using M=AIBotBridge.MigratedDomestic;
namespace AIBotBridge;
internal static class MediaCostSelfTest
{
    internal static void VerifyWeb() {
        ApplicationConfiguration.Initialize();
        using var context=new ApplicationContext();
        var service=new M.DomesticQuotaService();
        using var timer=new System.Windows.Forms.Timer{Interval=500};
        var started=DateTime.UtcNow;var deadline=started.AddSeconds(80);bool requested=false;
        timer.Tick+=async (_,_)=>{
            if(!requested){
                requested=true;
                if(service.HasOfficialApi("deepseek"))await service.RefreshOfficialApi("deepseek");
                service.Refresh("deepseek",force:true,webOnly:true);
            }
            var s=service.Snapshot;
            bool ok=s.DeepSeekWebUsedCostFetchedAt>=started;
            if(ok||DateTime.UtcNow>=deadline){
                timer.Stop();
                Console.WriteLine(JsonSerializer.Serialize(new{webCaptured=ok,balance=s.DeepSeekWebBalance,usedCost=s.DeepSeekWebUsedCost,
                    currency=s.DeepSeekWebCurrency,usageUpdatedAt=s.DeepSeekWebUsedCostFetchedAt,
                    stages=WebQuotaDiagnostics.Snapshot().Where(x=>x.Provider=="deepseek")},JsonDefaults.Options));
                service.DisposeBrowser();context.ExitThread();
            }
        };
        timer.Start();Application.Run(context);
    }
    internal static void Run() {
        void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);}
        var now=DateTime.UtcNow;var s=new M.DomesticQuotaSnapshot();
        M.DomesticQuotaService.MergeDeepSeek(s,90,"CNY",null,null,null,"account-a",true,now);
        M.DomesticQuotaService.MergeDeepSeek(s,12,"CNY",null,null,5,"account-a",false,now.AddMinutes(1));
        Check(s.DeepSeekBalance==90&&s.DeepSeekUsedCost is null&&s.DeepSeekFetchedAt==now&&s.DeepSeekWebBalance==12&&s.DeepSeekWebUsedCost==5,"API A and web B remain independent");
        s=JsonSerializer.Deserialize<M.DomesticQuotaSnapshot>(JsonSerializer.Serialize(s,new JsonSerializerOptions{IncludeFields=true}),new JsonSerializerOptions{IncludeFields=true})!;
        M.DomesticQuotaService.SelectDeepSeekSource(s,"account-a");
        Check(s.DeepSeekBalance==90&&s.DeepSeekUsedCost is null&&s.DeepSeekWebUsedCost==5,"restart preserves source isolation");
        M.DomesticQuotaService.SelectDeepSeekSource(s,"account-b");
        Check(s.DeepSeekBalance is null&&s.DeepSeekWebBalance==12,"new key does not expose previous API account");
        M.DomesticQuotaService.MergeDeepSeek(s,12,"CNY",null,null,5,"web",false,now);
        Check(s.DeepSeekBalance==12&&s.DeepSeekUsedCost==5&&s.DeepSeekSource=="web","web-only same-response values");
        M.DomesticQuotaService.MergeDeepSeek(s,90,"CNY",null,null,null,"account-a",true,now);
        Check(s.DeepSeekUsedCost is null&&s.DeepSeekUsedCostFetchedAt is null&&s.DeepSeekWebUsedCost==5,"API cannot inherit web cost in reverse event order");
        using var response=JsonDocument.Parse("""{"data":{"biz_data":{"normal_wallets":[{"balance":"99","currency":"USD"},{"balance":"10.10","currency":"CNY"}],"bonus_wallets":[{"balance":"0.20","currency":"CNY"}],"total_costs":[{"currency":"CNY","amount":"5"}]}}}""");
        Check(M.DomesticQuotaAuthForm.FindDeepSeekBalance(response.RootElement)?.Total==10.30,"decimal web wallet sum");
        Check(M.DomesticQuotaAuthForm.FindDeepSeekUsedCost(response.RootElement,"CNY")==5,"web summary cost");
        Check(M.DomesticQuotaAuthForm.FindDeepSeekUsedCost(response.RootElement,"USD") is null,"no currency fallback");
        foreach(var (json,expected) in new (string,double?)[]{
            ("""{"normal_wallets":[],"bonus_wallets":[{"balance":"0.20","currency":"CNY"}]}""",0.2),
            ("""{"normal_wallets":[{"balance":"10.10","currency":"CNY"}],"bonus_wallets":[{"balance":"0E-16","currency":"CNY"}]}""",10.1),
            ("""{"normal_wallets":[{"balance":"10.10","currency":"CNY"}],"bonus_wallets":[{"balance":"7","currency":"USD"}]}""",10.1),
            ("""{"balance_infos":[{"total_balance":"10.30","currency":"CNY"}],"normal_wallets":[{"balance":"10.10","currency":"CNY"}],"bonus_wallets":[{"balance":"0.20","currency":"CNY"}]}""",10.3),
            ("""{"normal_wallets":[{"balance":"10.10","currency":"CNY"}]}""",null),
            ("""{"normal_wallets":[],"bonus_wallets":[{"balance":"NaN","currency":"CNY"}]}""",null),
            ("""{"normal_wallets":[],"bonus_wallets":[{"balance":"1","currency":"CNY"},{"balance":"2","currency":"CNY"}]}""",null),
            ("""{"normal_wallets":[],"bonus_wallets":[{"balance":"1","currency":"CNY"},{"balance":"1","currency":"CNY"}]}""",1)}) {
            using var doc=JsonDocument.Parse(json);Check(DeepSeekWallets.Parse(doc.RootElement)?.Total==expected,"wallet group validation and deduplication");
        }
        const string song="""{"name":"歌A","artists":[{"name":"歌手A"}],"album":{"name":"专辑A"},"duration":285827}""";
        Check(NeteaseLocalDuration.Parse(song,"歌A","歌手A","专辑A")==285.827,"exact recording duration");
        Check(NeteaseLocalDuration.Parse(song,"歌B","歌手A","专辑A") is null,"different title");
        Check(NeteaseLocalDuration.Parse(song,"歌A","歌手B","专辑A") is null,"different artist");
        Check(NeteaseLocalDuration.Parse(song,"歌A","歌手A","现场版") is null,"different album version");
        Check(NeteaseLocalDuration.Parse(song.Replace("285827","null"),"歌A","歌手A","专辑A") is null,"unknown duration");
        Check(NeteaseLocalDuration.Read("browser.exe","歌A","歌手A","专辑A") is null,"other player isolated");
        Console.WriteLine("MEDIA_COST_SELF_TEST_OK web/API isolation, decimal wallet sum, zero, key change, wallet parsing, recording identity, missing data");
    }
}
