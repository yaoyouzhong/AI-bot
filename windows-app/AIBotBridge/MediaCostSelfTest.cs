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
            bool ok=s.DeepSeekUsedCostFetchedAt>=started;
            if(ok||DateTime.UtcNow>=deadline){
                timer.Stop();
                Console.WriteLine(JsonSerializer.Serialize(new{webCaptured=ok,balance=s.DeepSeekBalance,usedCost=s.DeepSeekUsedCost,
                    currency=s.DeepSeekCurrency,usageUpdatedAt=s.DeepSeekUsedCostFetchedAt,
                    stages=WebQuotaDiagnostics.Snapshot().Where(x=>x.Provider=="deepseek")},JsonDefaults.Options));
                service.DisposeBrowser();context.ExitThread();
            }
        };
        timer.Start();Application.Run(context);
    }
    internal static void Run() {
        void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);}
        var now=DateTime.UtcNow;var s=new M.DomesticQuotaSnapshot();
        M.DomesticQuotaService.MergeDeepSeek(s,75.37,"CNY",null,null,22.23,"account-a",false,now);
        M.DomesticQuotaService.MergeDeepSeek(s,75.12,"CNY",null,null,null,"account-a",true,now.AddMinutes(2));
        Check(s.DeepSeekUsedCost==22.23&&s.DeepSeekUsedCostFetchedAt==now&&s.DeepSeekBalance==75.12,"balance refresh preserves web cost and age");
        M.DomesticQuotaService.MergeDeepSeek(s,75.12,"CNY",null,null,0,"account-a",false,now.AddMinutes(3));
        Check(s.DeepSeekUsedCost==0,"real zero accepted");
        M.DomesticQuotaService.MergeDeepSeek(s,50,"CNY",null,null,null,"account-b",true,now);
        Check(s.DeepSeekUsedCost is null,"changed key scope clears usage");
        M.DomesticQuotaService.MergeDeepSeek(s,50,"CNY",null,null,3,"account-b",false,now);
        M.DomesticQuotaService.MergeDeepSeek(s,6,"USD",null,null,null,"account-b",true,now);
        Check(s.DeepSeekUsedCost is null,"currency change clears usage");
        using var response=JsonDocument.Parse("""{"data":{"biz_data":{"normal_wallets":[{"balance":"99","currency":"USD"},{"balance":"75.37","currency":"CNY"}],"total_costs":[{"currency":"CNY","amount":"22.23"}]}}}""");
        Check(M.DomesticQuotaAuthForm.FindDeepSeekBalance(response.RootElement)?.Total==75.37,"web summary wallet");
        Check(M.DomesticQuotaAuthForm.FindDeepSeekUsedCost(response.RootElement,"CNY")==22.23,"web summary cost");
        Check(M.DomesticQuotaAuthForm.FindDeepSeekUsedCost(response.RootElement,"USD") is null,"no currency fallback");
        const string song="""{"name":"歌A","artists":[{"name":"歌手A"}],"album":{"name":"专辑A"},"duration":285827}""";
        Check(NeteaseLocalDuration.Parse(song,"歌A","歌手A","专辑A")==285.827,"exact recording duration");
        Check(NeteaseLocalDuration.Parse(song,"歌B","歌手A","专辑A") is null,"different title");
        Check(NeteaseLocalDuration.Parse(song,"歌A","歌手B","专辑A") is null,"different artist");
        Check(NeteaseLocalDuration.Parse(song,"歌A","歌手A","现场版") is null,"different album version");
        Check(NeteaseLocalDuration.Parse(song.Replace("285827","null"),"歌A","歌手A","专辑A") is null,"unknown duration");
        Check(NeteaseLocalDuration.Read("browser.exe","歌A","歌手A","专辑A") is null,"other player isolated");
        Console.WriteLine("MEDIA_COST_SELF_TEST_OK web/API merge, zero, key/currency change, wallet parsing, recording identity, missing data");
    }
}
