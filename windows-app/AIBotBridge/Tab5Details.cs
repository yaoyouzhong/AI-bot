namespace AIBotBridge;

internal static class Tab5Details
{
    internal static bool CodexHasFiveHour(string? plan) => PlanDisplay.Normalize(plan)=="PLUS";
    internal sealed record Quota(string Id,string Name,string? Plan,double? Primary,double? Weekly,double? PlanPercent,
        double? Balance,double? Cost,string? Currency,string PrimaryReset,string WeeklyReset,string PlanReset,
        int? Credits,bool Stale,long TokensToday)
    {
        public string[] CreditExpiries { get; init; } = [];
        public string BillingMode { get; init; } = "subscription";
        public string PlanExpiry { get; init; } = "--";
    }
    internal static string BillingMode(string id,DomesticProviderQuotaSnapshot? q)
    {
        if(q?.PrimaryPercent is not null || q?.WeeklyPercent is not null || q?.PlanPercent is not null ||
            (q?.Plan?.Contains("Coding",StringComparison.OrdinalIgnoreCase)??false)) return "subscription";
        return id=="deepseek" || q?.Balance is not null || q?.UsedCost is not null ||
            (q?.Plan?.Contains("PAYG",StringComparison.OrdinalIgnoreCase)??false) ? "payg" : "subscription";
    }
    internal static Quota[] Quotas(StatusSnapshot s)
    {
        string Date(DateTimeOffset? time)=>time?.ToOffset(TimeSpan.FromSeconds(s.UtcOffsetSeconds)).ToString("MM-dd HH:mm")??"--";
        var result=new List<Quota>();
        void Main(string id,string name,ProviderQuotaSnapshot? q,long tokens)=>result.Add(new(id,name,PlanDisplay.Normalize(q?.Plan??""),id!="codex"||CodexHasFiveHour(q?.Plan)?q?.PrimaryPercent:null,q?.WeeklyPercent,null,null,null,null,
            Date(id!="codex"||CodexHasFiveHour(q?.Plan)?q?.PrimaryResetsAt:null),Date(q?.WeeklyResetsAt),"--",q?.ResetCreditsAvailable,q?.Stale??false,tokens)
            {CreditExpiries=(q?.ResetCreditExpiresAt??[]).Order().Select(epoch=>
                DateTimeOffset.FromUnixTimeSeconds(epoch).ToOffset(TimeSpan.FromSeconds(s.UtcOffsetSeconds)).ToString("yyyy-MM-dd HH:mm")).ToArray()});
        Main("claude","Claude",s.Quotas?.Claude,s.Claude.TokensToday);Main("codex","Codex",s.Quotas?.Codex,s.Codex.TokensToday);
        void Domestic(string id,string name,DomesticProviderQuotaSnapshot? q) =>result.Add(new(id,name,q?.Plan,q?.PrimaryPercent,q?.WeeklyPercent,q?.PlanPercent,q?.Balance,q?.UsedCost,q?.Currency,
            Date(q?.PrimaryResetsAt),Date(q?.WeeklyResetsAt),Date(q?.PlanResetsAt),null,q?.Stale??false,s.DomesticActivity?.Providers.GetValueOrDefault(id)?.TokensToday??0)
            {BillingMode=BillingMode(id,q),PlanExpiry=q?.PlanExpiresAt?.ToOffset(TimeSpan.FromSeconds(s.UtcOffsetSeconds)).ToString("yyyy-MM-dd")??"--"});
        Domestic("alibaba","阿里",s.DomesticQuotas?.Alibaba);Domestic("kimi","Kimi",s.DomesticQuotas?.Kimi);
        Domestic("minimax","MiniMax",s.DomesticQuotas?.MiniMax);Domestic("deepseek","DeepSeek",s.DomesticQuotas?.DeepSeek);
        Domestic("zhipu","智谱",s.DomesticQuotas?.Zhipu);Domestic("stepfun","阶跃",s.DomesticQuotas?.StepFun);
        Domestic("baidu","百度",s.DomesticQuotas?.Baidu);Domestic("xiaomi","小米",s.DomesticQuotas?.Xiaomi);
        return result.ToArray();
    }
}
