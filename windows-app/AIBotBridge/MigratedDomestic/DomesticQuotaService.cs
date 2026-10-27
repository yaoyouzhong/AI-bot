#nullable disable
using Settings = AIBotBridge.MigratedWeather.Settings;
using CredentialStore = AIBotBridge.MigratedWeather.CredentialStore;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace AIBotBridge.MigratedDomestic;

sealed record DomesticProviderDefinition(
    string Id, string Name, string Product, string Url, bool CaptureSupported);

static class DomesticProviderCatalog
{
    public static readonly DomesticProviderDefinition[] All =
    {
        new("qwen", "阿里云百炼", "千问 Token Plan",
            "https://bailian.console.aliyun.com/cn-beijing?tab=plan#/efm/subscription/token-plan", true),
        new("kimi", "月之暗面", "Kimi Coding Plan", "https://www.kimi.com/code/console", true),
        new("xiaomi", "小米 MiMo", "MiMo Token Plan",
            "https://platform.xiaomimimo.com/token-plan", true),
        new("zhipu", "智谱 GLM", "开放平台可用余额（不是 Coding Plan 额度）", AIBotBridge.ZhipuBalance.AuthorizationUrl, true),
        new("volcengine", "火山方舟", "豆包大模型", "https://console.volcengine.com/ark", false),
        new("minimax", "MiniMax", "MiniMax Token Plan", "https://platform.minimaxi.com/console/usage", true),
        new("deepseek", "DeepSeek", "DeepSeek 开放平台", "https://platform.deepseek.com/usage", true),
        new("baidu", "百度智能云", "千帆模型资源包（指定量包）", "https://console.bce.baidu.com/qianfan/overview", true),
        new("tencent", "腾讯云", "混元大模型", "https://console.cloud.tencent.com/hunyuan", false),
        new("huawei", "华为云", "盘古 / ModelArts", "https://console.huaweicloud.com/modelarts/", false),
        new("iflytek", "讯飞开放平台", "讯飞星火", "https://console.xfyun.cn/", false),
        new("stepfun", "阶跃星辰", "开放平台余额（非 Step Plan）", "https://platform.stepfun.com/account-overview", true),
        new("baichuan", "百川智能", "Baichuan", "https://platform.baichuan-ai.com/", false),
        new("lingyi", "零一万物", "Yi", "https://platform.lingyiwanwu.com/", false),
    };
}

sealed class DomesticQuotaSnapshot
{
    public double? QwenPlanPct;
    public double? QwenWeeklyPct;
    public double? QwenFiveHourPct;
    public double? XiaomiPlanPct;
    public double? KimiWeeklyPct;
    public double? KimiFiveHourPct;
    public double? KimiPlanPct;
    public double? MiniMaxWeeklyPct;
    public double? MiniMaxFiveHourPct;
    public AIBotBridge.DomesticProviderQuotaSnapshot Zhipu;
    public AIBotBridge.DomesticProviderQuotaSnapshot StepFun;
    public AIBotBridge.DomesticProviderQuotaSnapshot Baidu;
    public string DeepSeekSource = "";
    public double? DeepSeekApiBalance;
    public string DeepSeekApiCurrency="", DeepSeekApiKeyScope="";
    public DateTime? DeepSeekApiFetchedAt;
    public double? DeepSeekWebBalance, DeepSeekWebUsedCost;
    public string DeepSeekWebCurrency = "";
    public DateTime? DeepSeekWebFetchedAt, DeepSeekWebUsedCostFetchedAt;
    public double? DeepSeekBalance;
    public double? DeepSeekGrantedBalance;
    public double? DeepSeekToppedUpBalance;
    public double? DeepSeekUsedCost;
    public string DeepSeekCurrency = "";
    public DateTime? DeepSeekUsedCostFetchedAt;
    public string DeepSeekUsageKeyScope = "";
    public DateTimeOffset? QwenPlanResetAt;
    public DateTimeOffset? QwenPlanExpiresAt;
    public DateTimeOffset? QwenWeeklyResetAt;
    public DateTimeOffset? QwenFiveHourResetAt;
    public DateTimeOffset? KimiWeeklyResetAt;
    public DateTimeOffset? KimiFiveHourResetAt;
    public DateTimeOffset? KimiPlanExpiresAt;
    public DateTimeOffset? MiniMaxWeeklyResetAt;
    public DateTimeOffset? MiniMaxFiveHourResetAt;
    public DateTimeOffset? MiniMaxPlanExpiresAt;
    public DateTimeOffset? KimiPlanResetAt;
    public DateTimeOffset? XiaomiPlanResetAt;
    public long? QwenPrimaryWindowSeconds, QwenWeeklyWindowSeconds, QwenPlanWindowSeconds;
    public long? KimiPrimaryWindowSeconds, KimiWeeklyWindowSeconds, KimiPlanWindowSeconds;
    public long? MiniMaxPrimaryWindowSeconds, MiniMaxWeeklyWindowSeconds, XiaomiPlanWindowSeconds;
    public string QwenMembership = "";
    public string KimiMembership = "";
    public string MiniMaxMembership = "";
    public DateTime? QwenFetchedAt;
    public DateTime? XiaomiFetchedAt;
    public DateTime? KimiFetchedAt;
    public DateTime? MiniMaxFetchedAt;
    public DateTime? DeepSeekFetchedAt;
}

/// Reads the same read-only quota responses as the vendors' own account pages.
/// Passwords never pass through the app: sign-in happens inside the vendor page
/// and WebView2 owns its isolated persistent browser profile.
sealed partial class DomesticQuotaService
{
    internal const string QwenSubscriptionName = "Token Plan 团队版";
    const string MiniMaxCredentialTarget = "AI-bot/MiniMaxTokenPlanKey";
    const string MiniMaxQuotaEndpoint = "https://www.minimaxi.com/v1/token_plan/remains";
    static readonly TimeSpan MinRefreshInterval = TimeSpan.FromSeconds(60);
    static readonly TimeSpan RateLimitBackoff = TimeSpan.FromSeconds(300);
    static readonly string CachePath = Path.Combine(
        AIBotBridge.AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AI-bot", "domestic-provider-cache.json");
    static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
    readonly object _lock = new();
    readonly Dictionary<string, DateTime> _lastRefreshAttempt = new();
    readonly Dictionary<string, DateTime> _backoffUntil = new();
    DomesticQuotaSnapshot _snapshot = Load();
    DomesticQuotaAuthForm _form;

    public DomesticQuotaSnapshot Snapshot
    {
        get
        {
            lock (_lock) { var value = new DomesticQuotaSnapshot
            {
                QwenPlanPct = _snapshot.QwenPlanPct,
                QwenWeeklyPct = _snapshot.QwenWeeklyPct,
                QwenFiveHourPct = _snapshot.QwenFiveHourPct,
                XiaomiPlanPct = _snapshot.XiaomiPlanPct,
                KimiWeeklyPct = _snapshot.KimiWeeklyPct,
                KimiFiveHourPct = _snapshot.KimiFiveHourPct,
                KimiPlanPct = _snapshot.KimiPlanPct,
                MiniMaxWeeklyPct = _snapshot.MiniMaxWeeklyPct,
                MiniMaxFiveHourPct = _snapshot.MiniMaxFiveHourPct,
                Zhipu = _snapshot.Zhipu,
                StepFun = _snapshot.StepFun,
                Baidu = _snapshot.Baidu,
                DeepSeekSource = _snapshot.DeepSeekSource,
                DeepSeekApiBalance = _snapshot.DeepSeekApiBalance,
                DeepSeekApiCurrency = _snapshot.DeepSeekApiCurrency,
                DeepSeekApiKeyScope = _snapshot.DeepSeekApiKeyScope,
                DeepSeekApiFetchedAt = _snapshot.DeepSeekApiFetchedAt,
                DeepSeekWebBalance = _snapshot.DeepSeekWebBalance,
                DeepSeekWebUsedCost = _snapshot.DeepSeekWebUsedCost,
                DeepSeekWebCurrency = _snapshot.DeepSeekWebCurrency,
                DeepSeekWebFetchedAt = _snapshot.DeepSeekWebFetchedAt,
                DeepSeekWebUsedCostFetchedAt = _snapshot.DeepSeekWebUsedCostFetchedAt,
                DeepSeekBalance = _snapshot.DeepSeekBalance,
                DeepSeekGrantedBalance = _snapshot.DeepSeekGrantedBalance,
                DeepSeekToppedUpBalance = _snapshot.DeepSeekToppedUpBalance,
                DeepSeekUsedCost = _snapshot.DeepSeekUsedCost,
                DeepSeekCurrency = _snapshot.DeepSeekCurrency,
                DeepSeekUsedCostFetchedAt = _snapshot.DeepSeekUsedCostFetchedAt,
                DeepSeekUsageKeyScope = _snapshot.DeepSeekUsageKeyScope,
                QwenPlanResetAt = _snapshot.QwenPlanResetAt,
                QwenPlanExpiresAt = _snapshot.QwenPlanExpiresAt,
                QwenWeeklyResetAt = _snapshot.QwenWeeklyResetAt,
                QwenFiveHourResetAt = _snapshot.QwenFiveHourResetAt,
                KimiWeeklyResetAt = _snapshot.KimiWeeklyResetAt,
                KimiFiveHourResetAt = _snapshot.KimiFiveHourResetAt,
                KimiPlanExpiresAt = _snapshot.KimiPlanExpiresAt,
                MiniMaxWeeklyResetAt = _snapshot.MiniMaxWeeklyResetAt,
                MiniMaxFiveHourResetAt = _snapshot.MiniMaxFiveHourResetAt,
                MiniMaxPlanExpiresAt = _snapshot.MiniMaxPlanExpiresAt,
                KimiPlanResetAt = _snapshot.KimiPlanResetAt,
                XiaomiPlanResetAt = _snapshot.XiaomiPlanResetAt,
                QwenPrimaryWindowSeconds = _snapshot.QwenPrimaryWindowSeconds,
                QwenWeeklyWindowSeconds = _snapshot.QwenWeeklyWindowSeconds,
                QwenPlanWindowSeconds = _snapshot.QwenPlanWindowSeconds,
                KimiPrimaryWindowSeconds = _snapshot.KimiPrimaryWindowSeconds,
                KimiWeeklyWindowSeconds = _snapshot.KimiWeeklyWindowSeconds,
                KimiPlanWindowSeconds = _snapshot.KimiPlanWindowSeconds,
                MiniMaxPrimaryWindowSeconds = _snapshot.MiniMaxPrimaryWindowSeconds,
                MiniMaxWeeklyWindowSeconds = _snapshot.MiniMaxWeeklyWindowSeconds,
                XiaomiPlanWindowSeconds = _snapshot.XiaomiPlanWindowSeconds,
                QwenMembership = _snapshot.QwenMembership,
                KimiMembership = _snapshot.KimiMembership,
                MiniMaxMembership = _snapshot.MiniMaxMembership,
                QwenFetchedAt = _snapshot.QwenFetchedAt,
                XiaomiFetchedAt = _snapshot.XiaomiFetchedAt,
                KimiFetchedAt = _snapshot.KimiFetchedAt,
                MiniMaxFetchedAt = _snapshot.MiniMaxFetchedAt,
                DeepSeekFetchedAt = _snapshot.DeepSeekFetchedAt,
            };
            SelectDeepSeekSource(value,DeepSeekKeyScope());
            return value; }
        }
    }

    public void DisposeBrowser() { _form?.Dispose(); }

    public void OpenAuthorization(string providerId = "qwen")
    {
        if (_form == null || _form.IsDisposed)
            _form = new DomesticQuotaAuthForm(this, initialProviderId: providerId);
        _form.ShowAuthorization(providerId);
    }

    public void Refresh(string providerId, bool force = false, bool webOnly = false)
    {
        var provider = DomesticProviderCatalog.All.FirstOrDefault(x => x.Id == providerId);
        if (provider?.CaptureSupported != true || providerId is "stepfun" or "baidu" && !HasOfficialApi(providerId)) return;
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            if (_backoffUntil.TryGetValue(providerId=="deepseek"&&webOnly?"deepseek-web":providerId, out var blockedUntil)
                && now < blockedUntil) return;
            if (!force && _lastRefreshAttempt.TryGetValue(providerId + (webOnly ? ":web" : ":api"), out var lastAttempt)
                && now - lastAttempt < MinRefreshInterval) return;
            _lastRefreshAttempt[providerId + (webOnly ? ":web" : ":api")] = now;
        }
        if (HasOfficialApi(providerId) && !webOnly)
        {
            _ = RefreshOfficialApi(providerId);
            return;
        }
        if (_form == null || _form.IsDisposed)
            _form = new DomesticQuotaAuthForm(this, initialProviderId: providerId);
        _form.RefreshInBackground(providerId);
    }

    internal bool HasMiniMaxApiKey => MiniMaxApiKey().Length > 0;

    internal void SaveMiniMaxApiKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        CredentialStore.Write(MiniMaxCredentialTarget, key.Trim());
    }

    internal Task<(bool Success, string Message)> RefreshMiniMaxWithApiKey() => RefreshOfficialApi("minimax");

    internal async Task<(bool Success, string Message)> RefreshMiniMaxCore()
    {
        var key = MiniMaxApiKey();
        if (string.IsNullOrWhiteSpace(key))
            return (false, "尚未保存 MiniMax Subscription Key / API Key。");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, MiniMaxQuotaEndpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", key.Trim());
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await Http.SendAsync(request);
            if ((int)response.StatusCode == 429)
            {
                BackOff("minimax");
                return (false, "MiniMax 额度接口限流，5 分钟后自动重试。");
            }
            if (!response.IsSuccessStatusCode) return (false, $"MiniMax 额度查询失败（HTTP {(int)response.StatusCode}），请检查 Token Plan Key 或稍后重试。");
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            if(doc.RootElement.TryGetProperty("base_resp",out var apiStatus) && apiStatus.ValueKind==JsonValueKind.Object &&
                apiStatus.TryGetProperty("status_code",out var apiCode) && apiCode.TryGetInt32(out var errorCode) && errorCode!=0)
                return (false,"MiniMax 未接受 Token Plan Key 或当前请求，请检查凭据后重试。");
            var usage = DomesticQuotaAuthForm.FindMiniMaxUsage(doc.RootElement);
            if (!usage.HasValue)
                return (false, response.IsSuccessStatusCode
                    ? "MiniMax 响应中没有可识别的额度字段。"
                    : $"MiniMax 额度接口返回 HTTP {(int)response.StatusCode}。");
            SetMiniMax(usage.Value.WeeklyPct, usage.Value.FiveHourPct,
                usage.Value.Membership, usage.Value.WeeklyResetAt, usage.Value.FiveHourResetAt,
                primaryWindowSeconds:usage.Value.PrimaryWindowSeconds,weeklyWindowSeconds:usage.Value.WeeklyWindowSeconds);
            return (true, "已通过 MiniMax API 取得准确用量。");
        }
        catch (Exception)
        {
            return (false, "MiniMax API 网络异常或数据无法识别，已保留上次结果。");
        }
    }

    static string MiniMaxApiKey()
    {
        var saved = CredentialStore.Read(MiniMaxCredentialTarget).Trim();
        if (saved.Length > 0) return saved;
        foreach (var name in new[]
                 {
                     "MINIMAX_SUBSCRIPTION_KEY", "MINIMAX_TOKEN_PLAN_KEY",
                     "MINIMAX_API_KEY"
                 })
        {
            var value = AIBotBridge.AppPaths.GetProviderEnvironmentVariable(name)?.Trim() ?? "";
            if (value.Length > 0) return value;
        }
        return "";
    }

    internal void BackOff(string providerId)
    {
        lock (_lock)
        {
            var next = DateTime.UtcNow + RateLimitBackoff;
            if (!_backoffUntil.TryGetValue(providerId, out var current) || current < next)
                _backoffUntil[providerId] = next;
        }
    }

    internal void SetQwen(double pct, string membership = null, DateTimeOffset? resetAt = null, DateTimeOffset? planExpiresAt = null,
        long? windowSeconds = null)
    {
        RefreshHealth.Recover("qwen");
        lock (_lock)
        {
            _snapshot.QwenPlanPct = Clamp(pct);
            if (!string.IsNullOrWhiteSpace(membership))
                _snapshot.QwenMembership = membership.Trim();
            _snapshot.QwenPlanResetAt = resetAt;
            _snapshot.QwenPlanWindowSeconds = windowSeconds ?? QuotaCycle.PersonalQwenSeconds(membership);
            if (planExpiresAt.HasValue) _snapshot.QwenPlanExpiresAt = planExpiresAt;
            _snapshot.QwenFetchedAt = DateTime.UtcNow;
            Save();
        }
    }

    internal void SetQwenPageMetadata(string resetText, string membership, string expiryText = "")
    {
        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(membership))
            {
                var incoming = membership.Trim();
                var genericTokenPlan = incoming.Equals("Token Plan", StringComparison.OrdinalIgnoreCase);
                if (!genericTokenPlan || string.IsNullOrWhiteSpace(_snapshot.QwenMembership))
                    _snapshot.QwenMembership = incoming;
            }
            if (ParseResetAt(resetText) is DateTimeOffset resetAt)
                _snapshot.QwenPlanResetAt = resetAt;
            _snapshot.QwenPlanWindowSeconds = QuotaCycle.PersonalQwenSeconds(_snapshot.QwenMembership);
            _snapshot.QwenPlanExpiresAt = ParseResetAt(expiryText);
            Save();
        }
    }

    internal void SetAdditional(AIBotBridge.DomesticProviderQuotaSnapshot value)
    {
        lock (_lock) {
            if (value.Provider == "stepfun") _snapshot.StepFun = value;
            else if (value.Provider == "baidu") _snapshot.Baidu = value;
            else throw new ArgumentException("Unsupported provider.");
            Save();
        }
        RefreshHealth.Recover(value.Provider);
    }

    internal void SetXiaomi(double pct) => SetXiaomi(XiaomiQuota.Snapshot(pct,DateTime.UtcNow)
        ?? throw new JsonException("Invalid MiMo usage."));
    internal void SetXiaomi(AIBotBridge.DomesticProviderQuotaSnapshot value)
    {
        var pct=value.PlanPercent??double.NaN;
        if (!double.IsFinite(pct) || pct < 0 || pct > 100) throw new JsonException("Invalid MiMo usage.");
        RefreshHealth.Recover("xiaomi");
        lock (_lock) { _snapshot.XiaomiPlanPct = Clamp(pct); _snapshot.XiaomiFetchedAt = DateTime.UtcNow;
            _snapshot.XiaomiPlanResetAt=value.PlanResetsAt;_snapshot.XiaomiPlanWindowSeconds=value.PlanWindowSeconds;Save(); }
    }

    internal void SetKimi(double weeklyPct, double? fiveHourPct, string membership = null,
        DateTimeOffset? weeklyResetAt = null, DateTimeOffset? fiveHourResetAt = null, DateTimeOffset? planExpiresAt = null)
        => SetKimi(new AIBotBridge.DomesticProviderQuotaSnapshot("kimi",membership,fiveHourPct,fiveHourResetAt,weeklyPct,weeklyResetAt,
            null,null,null,DateTimeOffset.UtcNow,false) {PrimaryWindowSeconds=fiveHourPct.HasValue?18000:null,
                WeeklyWindowSeconds=604800,PlanExpiresAt=planExpiresAt});
    internal void SetKimi(AIBotBridge.DomesticProviderQuotaSnapshot value)
    {
        RefreshHealth.Recover("kimi");
        lock (_lock)
        {
            _snapshot.KimiWeeklyPct = value.WeeklyPercent is double weekly?Clamp(weekly):null;
            _snapshot.KimiFiveHourPct = value.PrimaryPercent is double primary?Clamp(primary):null;
            _snapshot.KimiWeeklyResetAt=value.WeeklyResetsAt;_snapshot.KimiFiveHourResetAt=value.PrimaryResetsAt;
            _snapshot.KimiPrimaryWindowSeconds=value.PrimaryWindowSeconds;_snapshot.KimiWeeklyWindowSeconds=value.WeeklyWindowSeconds;
            _snapshot.KimiPlanPct=value.PlanPercent;_snapshot.KimiPlanResetAt=value.PlanResetsAt;_snapshot.KimiPlanWindowSeconds=value.PlanWindowSeconds;
            if (value.PlanExpiresAt.HasValue) _snapshot.KimiPlanExpiresAt = value.PlanExpiresAt;
            if (!string.IsNullOrWhiteSpace(value.Plan))
            {
                _snapshot.KimiMembership = value.Plan.Trim();
                Settings.Set("kimi_membership", _snapshot.KimiMembership);
            }
            _snapshot.KimiFetchedAt = DateTime.UtcNow;
            Save();
        }
    }

    internal void SetMiniMax(double weeklyPct, double? fiveHourPct, string membership = null,
        DateTimeOffset? weeklyResetAt = null, DateTimeOffset? fiveHourResetAt = null, DateTimeOffset? planExpiresAt = null,
        long? primaryWindowSeconds = 18000,long? weeklyWindowSeconds = 604800)
    {
        RefreshHealth.Recover("minimax");
        lock (_lock)
        {
            _snapshot.MiniMaxWeeklyPct = Clamp(weeklyPct);
            _snapshot.MiniMaxFiveHourPct = fiveHourPct.HasValue ? Clamp(fiveHourPct.Value) : null;
            _snapshot.MiniMaxWeeklyResetAt = weeklyResetAt;
            _snapshot.MiniMaxFiveHourResetAt = fiveHourResetAt;
            _snapshot.MiniMaxPrimaryWindowSeconds = fiveHourPct.HasValue?primaryWindowSeconds:null;
            _snapshot.MiniMaxWeeklyWindowSeconds = weeklyWindowSeconds;
            if (planExpiresAt.HasValue) _snapshot.MiniMaxPlanExpiresAt = planExpiresAt;
            if (!string.IsNullOrWhiteSpace(membership))
                _snapshot.MiniMaxMembership = membership.Trim();
            _snapshot.MiniMaxFetchedAt = DateTime.UtcNow;
            Save();
        }
    }

    internal void SetZhipu(AIBotBridge.DomesticProviderQuotaSnapshot value)
    {
        RefreshHealth.Recover("zhipu");
        lock (_lock) { _snapshot.Zhipu = value; Save(); }
    }

    internal void SetDeepSeek(double balance, string currency, double? granted = null,
        double? toppedUp = null, double? usedCost = null, bool fromApi = false)
    {
        RefreshHealth.Recover(fromApi?"deepseek":"deepseek-web");
        if(!fromApi&&!HasOfficialApi("deepseek"))RefreshHealth.Recover("deepseek");
        lock (_lock)
        {
            MergeDeepSeek(_snapshot, balance, currency, granted, toppedUp, usedCost,
                DeepSeekKeyScope(), fromApi, DateTime.UtcNow);
            Save();
        }
    }

    internal static void MergeDeepSeek(DomesticQuotaSnapshot s, double balance, string currency,
        double? granted, double? toppedUp, double? usedCost, string keyScope, bool fromApi, DateTime now)
    {
        currency=string.IsNullOrWhiteSpace(currency)?"CNY":currency.Trim().ToUpperInvariant();
        if(!double.IsFinite(balance)||currency is not ("CNY" or "USD"))return;
        if(fromApi) {
            s.DeepSeekApiBalance=balance;s.DeepSeekApiCurrency=currency;s.DeepSeekApiKeyScope=keyScope;s.DeepSeekApiFetchedAt=now;
        }
        if(!fromApi) {
            s.DeepSeekWebBalance=balance;s.DeepSeekWebCurrency=currency;s.DeepSeekWebFetchedAt=now;
            // Both values must originate in this response, never a previous web login.
            s.DeepSeekWebUsedCost=usedCost is >=0 && double.IsFinite(usedCost.Value)?usedCost:null;
            s.DeepSeekWebUsedCostFetchedAt=s.DeepSeekWebUsedCost.HasValue?now:null;
            if(keyScope!="web")return;
        }
        s.DeepSeekSource=fromApi?"api":"web";
        s.DeepSeekUsageKeyScope=fromApi?keyScope:"web";
        s.DeepSeekUsedCost=fromApi?null:s.DeepSeekWebUsedCost;
        s.DeepSeekUsedCostFetchedAt=fromApi?null:s.DeepSeekWebUsedCostFetchedAt;
        s.DeepSeekBalance=balance;s.DeepSeekCurrency=currency;s.DeepSeekFetchedAt=now;
        s.DeepSeekGrantedBalance=fromApi?null:granted;s.DeepSeekToppedUpBalance=fromApi?null:toppedUp;
    }

    internal static void SelectDeepSeekSource(DomesticQuotaSnapshot s,string keyScope)
    {
        if(keyScope!="web") {
            bool match=s.DeepSeekApiKeyScope==keyScope;
            s.DeepSeekBalance=match?s.DeepSeekApiBalance:null;s.DeepSeekCurrency=match?s.DeepSeekApiCurrency:"";
            s.DeepSeekFetchedAt=match?s.DeepSeekApiFetchedAt:null;s.DeepSeekSource="api";
            s.DeepSeekUsedCost=null;s.DeepSeekUsedCostFetchedAt=null;
            // The account window also must not attribute a previous key's data to a new key.
            if(!match){s.DeepSeekApiBalance=null;s.DeepSeekApiFetchedAt=null;}
        } else if(s.DeepSeekWebFetchedAt.HasValue) {
            s.DeepSeekBalance=s.DeepSeekWebBalance;s.DeepSeekCurrency=s.DeepSeekWebCurrency;s.DeepSeekFetchedAt=s.DeepSeekWebFetchedAt;
            s.DeepSeekSource="web";s.DeepSeekUsedCost=s.DeepSeekWebUsedCost;s.DeepSeekUsedCostFetchedAt=s.DeepSeekWebUsedCostFetchedAt;
        } else {s.DeepSeekUsedCost=null;s.DeepSeekUsedCostFetchedAt=null;}
    }

    internal bool SetDeepSeekWeb(double balance, string currency, double? granted, double? toppedUp, double? cost)
    {
        lock(_lock) {
            MergeDeepSeek(_snapshot,balance,currency,granted,toppedUp,cost,DeepSeekKeyScope(),false,DateTime.UtcNow);
            Save();
        }
        RefreshHealth.Recover("deepseek-web");
        if(!HasOfficialApi("deepseek"))RefreshHealth.Recover("deepseek");
        return true;
    }

    internal void SetKimiMembership(string membership)
    {
        if (string.IsNullOrWhiteSpace(membership)) return;
        lock (_lock)
        {
            _snapshot.KimiMembership = membership.Trim();
            Settings.Set("kimi_membership", _snapshot.KimiMembership);
            Save();
        }
    }

    internal void SetKimiPageMetadata(string membership, string weeklyResetText,
        string fiveHourResetText)
    {
        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(membership)
                && !membership.StartsWith("LEVEL_", StringComparison.OrdinalIgnoreCase))
            {
                _snapshot.KimiMembership = membership.Trim();
                Settings.Set("kimi_membership", _snapshot.KimiMembership);
            }
            if (ParseResetAt(weeklyResetText) is DateTimeOffset weeklyResetAt
                && (!_snapshot.KimiWeeklyResetAt.HasValue
                    || _snapshot.KimiWeeklyResetAt <= DateTimeOffset.Now))
                _snapshot.KimiWeeklyResetAt = weeklyResetAt;
            if (ParseResetAt(fiveHourResetText) is DateTimeOffset fiveHourResetAt
                && (!_snapshot.KimiFiveHourResetAt.HasValue
                    || _snapshot.KimiFiveHourResetAt <= DateTimeOffset.Now))
                _snapshot.KimiFiveHourResetAt = fiveHourResetAt;
            Save();
        }
    }

    internal static DateTimeOffset? ParseResetAt(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out var parsedOffset))
            return parsedOffset;
        foreach (var format in new[]
                 {
                     "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy/MM/dd HH:mm:ss",
                     "yyyy.MM.dd HH:mm:ss", "yyyy.MM.dd HH:mm"
                 })
        {
            if (!DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces, out var local)) continue;
            local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
        }

        double minutes = 0;
        var matched = false;
        foreach (Match match in Regex.Matches(text,
                     @"(?<value>\d+(?:\.\d+)?)\s*(?<unit>天|小时|时|分钟|分|days?|d|hours?|hrs?|h|minutes?|mins?|m)",
                     RegexOptions.IgnoreCase))
        {
            if (!double.TryParse(match.Groups["value"].Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var value)) continue;
            var unit = match.Groups["unit"].Value.ToLowerInvariant();
            minutes += unit.StartsWith("天") || unit.StartsWith("day") || unit == "d" ? value * 1440
                : unit.StartsWith("小时") || unit == "时" || unit.StartsWith("hour") || unit.StartsWith("hr")
                    || unit == "h"
                    ? value * 60 : value;
            matched = true;
        }
        return matched && minutes > 0 ? DateTimeOffset.Now.AddMinutes(minutes) : null;
    }

    static double Clamp(double value) => Math.Clamp(value, 0, 100);

    static DomesticQuotaSnapshot Load()
    {
        DomesticQuotaSnapshot snapshot;
        try {
            var json=File.ReadAllText(File.Exists(CachePath) ? CachePath : Path.Combine(
                AIBotBridge.AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData),"AIClockBridge","domestic-quota-cache.json"));
            snapshot=JsonSerializer.Deserialize<DomesticQuotaSnapshot>(json,new JsonSerializerOptions {IncludeFields=true})??new();
            using var document=JsonDocument.Parse(json);
            var cycles=AIBotBridge.LegacyDisplayCache.ParseDomestic(document.RootElement);
            snapshot.QwenPrimaryWindowSeconds=cycles.Alibaba?.PrimaryWindowSeconds;
            snapshot.QwenWeeklyWindowSeconds=cycles.Alibaba?.WeeklyWindowSeconds;
            snapshot.QwenPlanWindowSeconds=cycles.Alibaba?.PlanWindowSeconds;
            snapshot.KimiPrimaryWindowSeconds=cycles.Kimi?.PrimaryWindowSeconds;
            snapshot.KimiWeeklyWindowSeconds=cycles.Kimi?.WeeklyWindowSeconds;
            snapshot.KimiPlanWindowSeconds=cycles.Kimi?.PlanWindowSeconds;
            snapshot.MiniMaxPrimaryWindowSeconds=cycles.MiniMax?.PrimaryWindowSeconds;
            snapshot.MiniMaxWeeklyWindowSeconds=cycles.MiniMax?.WeeklyWindowSeconds;
        }
        catch { snapshot = new(); }
        if (snapshot.KimiMembership.StartsWith("LEVEL_", StringComparison.OrdinalIgnoreCase))
            snapshot.KimiMembership = "";
        if (string.IsNullOrWhiteSpace(snapshot.KimiMembership))
        {
            var savedMembership = Settings.Get("kimi_membership");
            if (!savedMembership.StartsWith("LEVEL_", StringComparison.OrdinalIgnoreCase))
                snapshot.KimiMembership = savedMembership;
        }
        return snapshot;
    }

    void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            File.WriteAllText(CachePath, JsonSerializer.Serialize(_snapshot,
                new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
        }
        catch { }
    }
}

sealed class DomesticQuotaAuthForm : Form
{
    const int GwlExStyle = -20;
    const long WsExTransparent = 0x00000020L;
    const long WsExNoActivate = 0x08000000L;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    static extern nint SetWindowLongPtr(nint window, int index, nint value);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetWindowPos(nint window,nint after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")]
    static extern bool ShowWindow(nint window,int command);

    static readonly TimeSpan LoginRetention = TimeSpan.FromDays(30);

    readonly DomesticQuotaService _service;
    readonly bool _hideOnUserClose;
    WebView2 _web = CreateWebView();
    readonly Panel _webHost = new() { Dock = DockStyle.Fill };
    readonly Dictionary<string, (string ProviderId, string Endpoint, int Generation)> _quotaResponses = new();
    readonly Dictionary<string, Panel> _providerCards = new();
    CoreWebView2DevToolsProtocolEventReceiver _responseReceiver;
    CoreWebView2DevToolsProtocolEventReceiver _finishedReceiver;
    CoreWebView2 _activeCore;
    Task _webInitialization;
    DomesticProviderDefinition _activeProvider;
    string _initialProviderId;
    int _navigationGeneration;
    int _webRecoveryRequest;
    int _webRecoveryCount;
    bool _capturedForNavigation;
    bool _everShown;
    bool _backgroundRefresh;
    bool _webRecoveryPending;
    readonly ComboBox _zoom = new() { DropDownStyle=ComboBoxStyle.DropDownList, Width=92, FormattingEnabled=true, AccessibleName="网页缩放" };
    readonly Button _refreshWeb = new() {Text="刷新网页",AutoSize=true,FlatStyle=FlatStyle.Flat,Margin=new Padding(8,0,0,0),Visible=false};
    readonly Label _deepSeekSources = new() { AutoSize=true, Dock=DockStyle.Fill, Margin=new Padding(0,8,0,0), Visible=false };
    static readonly int[] ZoomLevels = [60,75,90,100,125];
    static string ZoomKey(string provider)=>"model_web_zoom_"+provider;
    void ApplyZoom(bool save=false) {
        if(_activeProvider is null)return;
        int percent=_zoom.SelectedItem is int selected?selected:100;
        _web.ZoomFactor=percent/100.0;
        if(save)Settings.Set(ZoomKey(_activeProvider.Id),percent.ToString(CultureInfo.InvariantCulture));
    }
    void UpdateDeepSeekSources() {
        _deepSeekSources.Visible=_activeProvider?.Id=="deepseek";
        _refreshWeb.Visible=_deepSeekSources.Visible&&_service.HasOfficialApi("deepseek");
        if(!_deepSeekSources.Visible)return;
        var s=_service.Snapshot;
        string Value(double? n)=>n.HasValue?n.Value.ToString("0.00",CultureInfo.InvariantCulture):"待获取";
        string Time(DateTime? time)=>time?.ToLocalTime().ToString("MM-dd HH:mm:ss")??"尚未刷新";
        string api=s.DeepSeekApiFetchedAt.HasValue||_service.HasOfficialApi("deepseek")?$"API 总余额：{s.DeepSeekApiCurrency} {Value(s.DeepSeekApiBalance)} · {Time(s.DeepSeekApiFetchedAt)}" : "API：未配置";
        _deepSeekSources.Text=api+(_service.RefreshHealth.Failed("deepseek")?"（刷新失败，保留旧值）":"")+"\n"+
            $"网页余额：{s.DeepSeekWebCurrency} {Value(s.DeepSeekWebBalance)}；网页消费：{Value(s.DeepSeekWebUsedCost)} · {Time(s.DeepSeekWebFetchedAt)}"+
            (_service.RefreshHealth.Failed("deepseek-web")?"（刷新失败，保留旧值）":"")+(_service.HasOfficialApi("deepseek")?"\n设备使用 API 总余额；网页消费独立显示。":"\n设备使用网页来源；API 与网页分别保存。");
    }
    readonly Label _providerTitle = new()
    {
        AutoSize = true, Font = new Font("Microsoft YaHei UI", 14, FontStyle.Bold),
        ForeColor = Color.FromArgb(31, 41, 55), Location = new Point(20, 14),
    };
    readonly Label _providerState = new()
    {
        AutoSize = true, Font = new Font("Microsoft YaHei UI", 9), Location = new Point(21, 45),
    };
    readonly Label _status = new()
    {
        Dock = DockStyle.Bottom, Height = 60, TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(14, 0, 0, 0), BackColor = Color.FromArgb(248, 250, 252),
        ForeColor = Color.FromArgb(71, 85, 105),
        Text = "选择厂商，配置接口或网页登录。",
    };
    readonly TableLayoutPanel _miniMaxKeyPanel = new()
    {
        Dock = DockStyle.Top, Height = 52, Padding = new Padding(20, 8, 20, 8),
        BackColor = Color.FromArgb(248, 250, 252), Visible = false,
    };
    readonly TextBox _miniMaxApiKey = new()
    {
        UseSystemPasswordChar = true,
        PlaceholderText = "MiniMax Subscription Key / API Key，留空则只测试已保存 Key",
    };
    readonly Label _baiduSecretLabel = new() { Text="Secret Access Key（SK）", AutoSize=true };
    readonly Label _baiduPackageLabel = new() { Text="千帆模型资源包 ID（packageId）", AutoSize=true };
    readonly TextBox _baiduSecret = new() { UseSystemPasswordChar = true, PlaceholderText = "Secret Access Key（SK）", Dock = DockStyle.Fill };
    readonly TextBox _baiduPackage = new() { PlaceholderText = "千帆模型资源包 ID（packageId）", Dock = DockStyle.Fill };
    readonly NumericUpDown _kimiPort = new() { Minimum=1024, Maximum=65535, Value=DomesticQuotaService.KimiPort, Width=90, Location=new Point(160,45), AccessibleName="Kimi 本地服务端口" };
    readonly Label _kimiPortLabel = new() { Text="Kimi 本地服务端口", AutoSize=true, Location=new Point(20,48) };
    readonly Button _miniMaxSaveKey = new()
    {
        Text = "保存并测试", Width = 108, Height = 32,
        FlatStyle = FlatStyle.Flat, BackColor = Color.White,
        ForeColor = Color.FromArgb(51, 65, 85),
    };

    public DomesticQuotaAuthForm(DomesticQuotaService service, bool hideOnUserClose = true,
                                 string initialProviderId = "qwen", bool initializeBrowser = true)
    {
        _service = service;
        _hideOnUserClose = hideOnUserClose;
        _initialProviderId = initialProviderId;
        Text = "国产模型额度设置";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Normal;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Microsoft YaHei UI", 9);
        Width = 1060;
        Height = 720;
        _interactiveMinimumSize = new Size(900, 620);
        BackColor = Color.White;

        var navigation = new Panel
        {
            Dock = DockStyle.Left, Width = 270, Padding = new Padding(14, 16, 10, 12),
            BackColor = Color.FromArgb(245, 247, 250),
        };
        var navigationTitle = new Label
        {
            Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 14), Text = "模型厂商",
            Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
        };
        var providerList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Padding = new Padding(0, 4, 0, 8),
        };
        navigation.Controls.Add(providerList);
        navigation.Controls.Add(navigationTitle);

        foreach (var provider in DomesticProviderCatalog.All) providerList.Controls.Add(BuildProviderCard(provider));
        // Measure with the active font after DPI changes, including background-to-interactive transitions.
        void LayoutProviderList()
        {
            navigation.Width = Math.Max(210, navigationTitle.Font.Height * 12);
            var width = Math.Max(160, providerList.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - providerList.Padding.Horizontal - 4);
            foreach (Control card in providerList.Controls)
            {
                card.MinimumSize = new Size(width, 0);
                card.MaximumSize = new Size(width, 0);
                card.Width = width;
            }
        }
        providerList.SizeChanged += (_, _) => LayoutProviderList();
        DpiChanged += (_, _) => BeginInvoke(LayoutProviderList);
        Shown += (_, _) => LayoutProviderList();


        var content = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty, Padding = Padding.Empty };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 2,
            Padding = new Padding(20, 16, 20, 12), BackColor = Color.White,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _providerTitle.Font = new Font("Microsoft YaHei UI", 13, FontStyle.Bold);
        _providerTitle.Dock = DockStyle.Fill;
        _providerTitle.AutoSize = true;
        _providerTitle.MinimumSize = new Size(0, 36);
        _providerTitle.AutoEllipsis = false;
        _providerState.Dock = DockStyle.Fill;
        _providerState.AutoSize = true;
        _providerState.MinimumSize = new Size(0, 36);
        _providerState.Margin = new Padding(0, 6, 0, 0);
        var refresh = new Button
        {
            Text = "刷新额度", AutoSize = true, MinimumSize = new Size(108, 36),
            FlatStyle = FlatStyle.Flat, BackColor = Color.White,
            ForeColor = Color.FromArgb(51, 65, 85), Margin = new Padding(16, 0, 0, 0),
        };
        refresh.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        refresh.Click += async (_, _) => {
            if(_activeProvider is null)return;
            if(_service.HasOfficialApi(_activeProvider.Id)) {
                var result=await _service.RefreshOfficialApi(_activeProvider.Id);_status.Text=result.Message;UpdateDeepSeekSources();
            } else await ReloadWebView();
        };
        header.Controls.Add(_providerTitle, 0, 0);
        AIBotBridge.SettingsWindow.StyleButton(refresh);
        AIBotBridge.SettingsWindow.StyleButton(_miniMaxSaveKey,true);
        AIBotBridge.SettingsWindow.FitScreen(this);
        var actions=new FlowLayoutPanel { AutoSize=true, FlowDirection=FlowDirection.LeftToRight, WrapContents=false, Margin=Padding.Empty };
        _zoom.Items.AddRange(ZoomLevels.Cast<object>().ToArray());_zoom.SelectedItem=100;
        _zoom.Format+=(_,e)=>e.Value=e.Value+"%";
        _zoom.SelectionChangeCommitted+=(_,_)=>{try{ApplyZoom(true);}catch(IOException){_status.Text="网页缩放未能保存，请重试。";}};
        AIBotBridge.SettingsWindow.StyleButton(_refreshWeb);
        _refreshWeb.Click+=async (_,_)=>await ReloadWebView();
        actions.Controls.Add(_zoom);actions.Controls.Add(_refreshWeb);actions.Controls.Add(refresh);
        header.Controls.Add(actions, 1, 0);
        header.Controls.Add(_providerState, 0, 1);
        header.SetColumnSpan(_providerState, 2);
        header.Controls.Add(_deepSeekSources,0,2);header.SetColumnSpan(_deepSeekSources,2);
        _miniMaxKeyPanel.AutoSize = true;
        _miniMaxKeyPanel.ColumnCount = 1;
        _miniMaxKeyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var keyLayout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 8 };
        keyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        keyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var keyTitle = new Label { Text = "接口凭据", AutoSize = true, Margin = new Padding(0, 4, 0, 8), Font = new Font(Font, FontStyle.Bold) };
        var keyHint = new Label { Text = "凭据仅保存在本机 Windows 凭据管理器；留空可测试已保存的凭据。", AutoSize = true, Margin = new Padding(0, 8, 0, 8), ForeColor = Color.FromArgb(100, 116, 139) };
        _miniMaxApiKey.Dock = DockStyle.Fill;
        _miniMaxApiKey.Margin = new Padding(0, 4, 12, 0);
        _miniMaxSaveKey.AutoSize = true;
        AIBotBridge.SettingsWindow.StyleButton(_miniMaxSaveKey,true);
        _miniMaxSaveKey.Margin = new Padding(0);
        _miniMaxSaveKey.BackColor = Color.FromArgb(37, 99, 235);
        _miniMaxSaveKey.ForeColor = Color.White;
        _miniMaxSaveKey.FlatAppearance.BorderSize = 0;
        _miniMaxSaveKey.Click += async (_, _) => await SaveAndTestMiniMaxKey();
        var portRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0) };
        portRow.Controls.Add(_kimiPortLabel);
        portRow.Controls.Add(_kimiPort);
        keyLayout.Controls.Add(keyTitle, 0, 0);
        keyLayout.SetColumnSpan(keyTitle, 2);
        keyLayout.Controls.Add(_miniMaxApiKey, 0, 1);
        keyLayout.Controls.Add(_miniMaxSaveKey, 1, 1);
        keyLayout.Controls.Add(keyHint, 0, 2);
        keyLayout.SetColumnSpan(keyHint, 2);
        keyLayout.Controls.Add(portRow, 0, 3);
        keyLayout.SetColumnSpan(portRow, 2);
        keyLayout.Controls.Add(_baiduSecretLabel, 0, 4);
        keyLayout.SetColumnSpan(_baiduSecretLabel, 2);
        keyLayout.Controls.Add(_baiduSecret, 0, 5);
        keyLayout.SetColumnSpan(_baiduSecret, 2);
        keyLayout.Controls.Add(_baiduPackageLabel, 0, 6);
        keyLayout.SetColumnSpan(_baiduPackageLabel, 2);
        keyLayout.Controls.Add(_baiduPackage, 0, 7);
        keyLayout.SetColumnSpan(_baiduPackage, 2);
        _miniMaxKeyPanel.Controls.Add(keyLayout);
        _webHost.Controls.Add(_web);
        header.Margin = _miniMaxKeyPanel.Margin = _webHost.Margin = _status.Margin = Padding.Empty;
        content.Controls.Add(header, 0, 0);
        content.Controls.Add(_miniMaxKeyPanel, 0, 1);
        content.Controls.Add(_webHost, 0, 2);
        content.Controls.Add(_status, 0, 3);
        Controls.Add(content);
        Controls.Add(navigation);

        Shown += async (_, _) =>
        {
            if (!initializeBrowser) { ConfigureProviderDisplay(ProviderById(_initialProviderId)); return; } // No profile or navigation.
            _everShown = true;
            await SelectProvider(ProviderById(_initialProviderId));
        };
    }

    public void ShowAuthorization(string providerId)
    {
        _initialProviderId = providerId;
        _backgroundRefresh = false;
        Enabled = true;
        FormBorderStyle = FormBorderStyle.Sizable;
        _interactiveMinimumSize = new Size(900, 620);
        if (_providerTitle.Parent != null) _providerTitle.Parent.Visible = true;
        _status.Visible = true;
        ShowInTaskbar = true;
        Opacity = 1;
        var targetScreen = Screen.FromPoint(Cursor.Position);
        StartPosition = FormStartPosition.Manual;
        WindowState = FormWindowState.Normal;
        var area = targetScreen.WorkingArea;
        var scale = DeviceDpi / 96f;
        var width = Math.Min(area.Width-32, (int)(1060 * scale));
        var height = Math.Min(area.Height-32, (int)(720 * scale));
        _interactiveMinimumSize = new Size(Math.Min(area.Width-32, (int)(900 * scale)), Math.Min(area.Height-32, (int)(620 * scale)));
        Bounds = new Rectangle(area.Left + (area.Width-width)/2, area.Top + (area.Height-height)/2, width, height);
        TopMost = true;
        var wasEverShown = _everShown;
        if (!Visible) Show();
        SetBackgroundWindowStyle(false);
        WindowState = FormWindowState.Normal;
        if (wasEverShown) BeginInvoke(async () => await SelectProvider(ProviderById(providerId)));
        BringToFront();
        Activate();
        BeginInvoke(() => TopMost = false);
    }

    public void RefreshInBackground(string providerId)
    {
        if (Visible && !_backgroundRefresh) return; // never interrupt an interactive login
        _initialProviderId = providerId;
        _backgroundRefresh = true;
        // Apply before any handle/style/visibility changes, including a hidden
        // account window that was previously opened interactively.
        SetBackgroundWindowStyle(true);
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        if (_providerTitle.Parent != null) _providerTitle.Parent.Visible = false;
        _status.Visible = false;
        // Alibaba only creates its signed quota POST while WebView2 owns a
        // renderable viewport. A nearly invisible 16px viewport is enough;
        // click-through + no-activate styles keep it out of the user's way.
        Opacity = 0.01;
        StartPosition = FormStartPosition.Manual;
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Bounds = new Rectangle(area.Right - 16, area.Top, 16, 16);
        var wasEverShown = _everShown;
        if (!Visible) Show();
        SetBackgroundWindowStyle(true);
        if(WindowState!=FormWindowState.Normal)ShowWindow(Handle,4 /* SW_SHOWNOACTIVATE */);
        // Form.TopMost uses SetWindowPos WITHOUT SWP_NOACTIVATE, even when
        // assigned true repeatedly. It can steal TSF focus from a voice draft.
        // Keep the renderable viewport topmost without activating its window.
        SetWindowPos(Handle,new nint(-1),0,0,0,0,0x0013 /* NOMOVE | NOSIZE | NOACTIVATE */);
        if (wasEverShown) BeginInvoke(async () => await SelectProvider(ProviderById(providerId)));
    }

    static DomesticProviderDefinition ProviderById(string providerId) =>
        DomesticProviderCatalog.All.FirstOrDefault(x => x.Id == providerId)
        ?? DomesticProviderCatalog.All[0];

    static WebView2 CreateWebView() => new() { Dock = DockStyle.Fill };

    internal static void AlignmentUiSelfTest(string directory) {
        Directory.CreateDirectory(directory);
        var service=new DomesticQuotaService();service.SetDeepSeekWeb(12,"CNY",2,10,5);service.SetDeepSeek(90,"CNY",fromApi:true);
        foreach(float scale in new[]{1f,1.25f,1.75f}) {
            using var form=new DomesticQuotaAuthForm(service,hideOnUserClose:false,initialProviderId:"deepseek",initializeBrowser:false);
            form.StartPosition=FormStartPosition.Manual;form.Location=new Point(20,20);form.Size=new Size(1000,700);
            form.Show();Application.DoEvents();form.Scale(new SizeF(scale,scale));Application.DoEvents();
            form._zoom.SelectedItem=75;form.ApplyZoom(true);
            form.ConfigureProviderDisplay(ProviderById("zhipu"));form._zoom.SelectedItem=60;form.ApplyZoom(true);
            form.ConfigureProviderDisplay(ProviderById("deepseek"));
            if(form._web.ZoomFactor!=0.75||form._zoom.SelectedItem is not 75)throw new InvalidOperationException("Provider zoom not restored");
            var zoom=form._zoom.RectangleToScreen(form._zoom.ClientRectangle);var client=form.RectangleToScreen(form.ClientRectangle);
            if(!client.Contains(zoom)||!form._deepSeekSources.Visible)throw new InvalidOperationException("Zoom/source controls clipped");
            using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));
            image.Save(Path.Combine(directory,$"model-accounts-{scale:0.00}.png"));
            form.Close();
        }
        service.DisposeBrowser();
        Console.WriteLine("MODEL_ACCOUNT_UI_OK per-provider zoom save/restore and source separation at 100/125/175 percent; synthetic accounts; browser navigation excluded");
    }

    internal uint BrowserProcessIdForTest
    {
        get
        {
            try { return _activeCore?.BrowserProcessId ?? 0; }
            catch { return 0; }
        }
    }

    internal static void BackgroundFocusSelfTest() {
        using var editor=new Form{Text="Synthetic voice draft",ShowInTaskbar=false,Size=new Size(320,120)};
        using var input=new TextBox{Dock=DockStyle.Fill,Multiline=true};editor.Controls.Add(input);
        var service=new DomesticQuotaService();
        using var background=new DomesticQuotaAuthForm(service,hideOnUserClose:false,initialProviderId:"deepseek",initializeBrowser:false);
        int lost=0;input.LostFocus+=(_,_)=>lost++;
        editor.Show();editor.Activate();input.Select();Application.DoEvents();
        if(!input.Focused)throw new InvalidOperationException("Synthetic editor could not obtain thread focus");
        for(int refresh=0;refresh<30;refresh++) {
            int before=lost;
            background.RefreshInBackground("deepseek");Application.DoEvents();
            if(!input.Focused||lost!=before)
                throw new InvalidOperationException($"BACKGROUND_QUOTA_STOLE_FOCUS refresh={refresh}; lost={lost-before}; active={ActiveForm?.GetType().Name}");
        }
        // Return from an explicitly opened/minimized account window to background
        // polling; handle recreation must preserve the same no-activate boundary.
        foreach(bool minimized in new[]{false,true}) {
            background.ShowAuthorization("deepseek");Application.DoEvents();
            if(minimized)background.WindowState=FormWindowState.Minimized;
            background.Hide();editor.Activate();input.Select();Application.DoEvents();
            if(!input.Focused)throw new InvalidOperationException("Editor focus precondition failed after interactive account window");
            int before=lost;background.RefreshInBackground("deepseek");Application.DoEvents();
            if(!input.Focused||lost!=before||background.WindowState!=FormWindowState.Normal)
                throw new InvalidOperationException($"BACKGROUND_QUOTA_FOCUS_ROUNDTRIP_FAILED minimized={minimized}; focused={input.Focused}; lost={lost-before}; state={background.WindowState}; active={ActiveForm?.GetType().Name}");
        }
        background.Hide();service.DisposeBrowser();
        Console.WriteLine("BACKGROUND_QUOTA_FOCUS_OK initial/30 repeated/interactive/minimized refresh preserves edit focus; native windows, no browser/profile/credentials");
    }

    internal int WebViewRecoveryCountForTest => _webRecoveryCount;

    protected override bool ShowWithoutActivation => _backgroundRefresh;
    Size _interactiveMinimumSize;
    [StructLayout(LayoutKind.Sequential)]
    struct MinMaxInfo {
        public Point Reserved, MaximumSize, MaximumPosition, MinimumTrackSize, MaximumTrackSize;
    }
    protected override void WndProc(ref Message message) {
        base.WndProc(ref message);
        // Keep the interactive resize limit without toggling Form.MinimumSize:
        // its setter calls an activating SetWindowPos even on a hidden window.
        if(!_backgroundRefresh&&message.Msg==0x0024 /* WM_GETMINMAXINFO */&&message.LParam!=0) {
            var limits=Marshal.PtrToStructure<MinMaxInfo>(message.LParam);
            limits.MinimumTrackSize=new Point(Math.Max(limits.MinimumTrackSize.X,_interactiveMinimumSize.Width),
                Math.Max(limits.MinimumTrackSize.Y,_interactiveMinimumSize.Height));
            Marshal.StructureToPtr(limits,message.LParam,false);
        }
    }
    protected override CreateParams CreateParams {
        get {
            var value=base.CreateParams;
            if(_backgroundRefresh)value.ExStyle|=(int)(WsExTransparent|WsExNoActivate);
            return value;
        }
    }

    void SetBackgroundWindowStyle(bool background)
    {
        if (!IsHandleCreated) return;
        var style = GetWindowLongPtr(Handle, GwlExStyle).ToInt64();
        var flags = WsExTransparent | WsExNoActivate;
        style = background ? style | flags : style & ~flags;
        SetWindowLongPtr(Handle, GwlExStyle, new nint(style));
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_hideOnUserClose && e.CloseReason == CloseReason.UserClosing)
        {
            if (_activeProvider?.Id is "qwen" or "kimi" or "minimax" or "deepseek" or "zhipu" or "xiaomi")
                _ = PersistLoginCookies(_activeProvider.Url);
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }

    Panel BuildProviderCard(DomesticProviderDefinition provider)
    {
        var card = new TableLayoutPanel
        {
            Width = 240, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2, RowCount = 2, Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.White, Cursor = Cursors.Hand, Tag = provider.Id,
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var name = new Label
        {
            Text = provider.Name, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0),
            Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59), Cursor = Cursors.Hand,
        };
        var product = new Label
        {
            Text = provider.Product, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 0),
            Font = new Font("Microsoft YaHei UI", 8),
            ForeColor = Color.FromArgb(100, 116, 139), Cursor = Cursors.Hand,
        };
        var badge = new Label
        {
            Text = provider.Id is "stepfun" or "baidu" or "xiaomi" ? "待验证" : provider.CaptureSupported ? "可用" : "待接", AutoSize = true,
            Padding = new Padding(8, 2, 8, 2), Margin = new Padding(0), Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Font = new Font("Microsoft YaHei UI", 7.5f, FontStyle.Bold), Cursor = Cursors.Hand,
            ForeColor = provider.CaptureSupported ? Color.FromArgb(22, 101, 52) : Color.FromArgb(100, 116, 139),
            BackColor = provider.CaptureSupported ? Color.FromArgb(220, 252, 231) : Color.FromArgb(241, 245, 249),
        };
        async void Click(object sender, EventArgs e) => await SelectProvider(provider);
        card.Click += Click;
        name.Click += Click;
        product.Click += Click;
        badge.Click += Click;
        card.Controls.Add(name, 0, 0);
        card.Controls.Add(badge, 1, 0);
        card.Controls.Add(product, 0, 1);
        card.SetColumnSpan(product, 2);
        _providerCards[provider.Id] = card;
        return card;
    }

    void ConfigureProviderDisplay(DomesticProviderDefinition provider)
    {
        _activeProvider = provider;
        int zoom=int.TryParse(Settings.Get(ZoomKey(provider.Id)),out var saved)&&ZoomLevels.Contains(saved)?saved:100;
        _zoom.SelectedItem=zoom;ApplyZoom();UpdateDeepSeekSources();
        foreach (var (id, card) in _providerCards)
            card.BackColor = id == provider.Id ? Color.FromArgb(224, 242, 254) : Color.White;
        _providerTitle.Text = provider.Name;
        _miniMaxApiKey.Clear();
        _baiduSecret.Clear(); _baiduPackage.Clear();
        _baiduSecretLabel.Visible = _baiduPackageLabel.Visible = _baiduSecret.Visible = _baiduPackage.Visible = provider.Id == "baidu";
        _miniMaxKeyPanel.Visible = DomesticQuotaService.SupportsOfficialApi(provider.Id);

        _kimiPort.Visible = _kimiPortLabel.Visible = provider.Id == "kimi";
        _miniMaxApiKey.PlaceholderText = provider.Id == "baidu" ? "Access Key ID（AK）；三项均留空可测试已保存的配置" : provider.Id == "stepfun" ? "阶跃星辰 API Key（不是 Step Plan Key）" : provider.Id == "kimi" ? "Kimi Code 本地服务访问令牌（不是 Moonshot API Key）" : provider.Id == "deepseek" ? "DeepSeek API Key；留空测试已保存 Key" : "MiniMax Token Plan Key；留空测试已保存 Key";
        _providerState.Text = provider.Id == "xiaomi" ? "网页登录读取 Token Plan Credits 用量；真实套餐待验证。" : provider.Id == "baidu" ? "填写 AK、SK 和资源包 ID，读取资源包用量；真实账号待验证。"
            : provider.Id == "stepfun" ? "读取开放平台余额，不含 Step Plan；真实账号待验证。"
            : provider.Id == "minimax"
            ? "保存 Subscription Key 自动查询，或登录控制台。"
            : provider.Id == "deepseek"
            ? "API 总余额和网页余额、消费分别刷新。"
            : provider.Id == "kimi"
            ? "运行并登录 kimi web，填写令牌和端口；也可网页登录。"
            : provider.Id == "zhipu"
            ? "登录后打开财务总览，读取余额，不含 Coding Plan。"
            : provider.CaptureSupported
            ? "登录后打开订阅或用量页，自动读取额度。"
            : "可登录控制台，额度读取尚待适配。";
        _providerState.ForeColor = provider.CaptureSupported
            ? Color.FromArgb(22, 101, 52) : Color.FromArgb(180, 83, 9);
    }

    async Task SelectProvider(DomesticProviderDefinition provider)
    {
        ConfigureProviderDisplay(provider);
        try
        {
            await Navigate(provider);
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException or IOException)
        {
            _service.ReportWebFailure(provider.Id,$"{provider.Name} 网页初始化或导航失败，正在尝试恢复。",!_backgroundRefresh);
            if (QueueWebViewRecovery(provider))
                QuotaDiagnostics.Log($"quota WebView2 navigation rejected; recreating control: {ex.Message}");
        }
    }

    async Task SaveAndTestMiniMaxKey()
    {
        var provider = _activeProvider?.Id;
        if (!DomesticQuotaService.SupportsOfficialApi(provider)) return;
        _miniMaxSaveKey.Enabled = false;
        try
        {
            if(provider=="kimi") Settings.Set("kimi_usage_port",((int)_kimiPort.Value).ToString());
            if (provider == "baidu" && new[]{_miniMaxApiKey.Text,_baiduSecret.Text,_baiduPackage.Text}.Any(x=>!string.IsNullOrWhiteSpace(x))) {
                if (new[]{_miniMaxApiKey.Text,_baiduSecret.Text,_baiduPackage.Text}.Any(string.IsNullOrWhiteSpace)) { _status.Text="请完整填写 AK、SK 和模型资源包 ID，或全部留空测试已保存的配置。"; return; }
                _service.SaveOfficialApiKey(provider,JsonSerializer.Serialize(new AdditionalQuotaApi.BaiduCredentials(_miniMaxApiKey.Text.Trim(),_baiduSecret.Text.Trim(),_baiduPackage.Text.Trim())));
                _miniMaxApiKey.Clear(); _baiduSecret.Clear(); _baiduPackage.Clear();
            }
            else if (!string.IsNullOrWhiteSpace(_miniMaxApiKey.Text)) {
                _service.SaveOfficialApiKey(provider,_miniMaxApiKey.Text);
                _miniMaxApiKey.Clear();
            }
            if (!_service.HasOfficialApi(provider)) { _status.Text="请填写当前厂商的接口凭据。"; return; }
            ++_navigationGeneration; // Cancel stale DOM capture before changing the authoritative source.
            _status.Text="正在通过官方接口查询…";
            var result=await _service.RefreshOfficialApi(provider);
            if (_activeProvider?.Id==provider && !IsDisposed) { _capturedForNavigation=result.Success; _status.Text=result.Message; }
        }
        catch { if(!IsDisposed) _status.Text="接口凭据保存失败，请检查 Windows 凭据管理器。"; }
        finally { if(!IsDisposed) { _miniMaxSaveKey.Enabled=true;UpdateDeepSeekSources(); } }
    }

    async Task Navigate(DomesticProviderDefinition provider)
    {
        AIBotBridge.WebQuotaDiagnostics.Record(provider.Id, _backgroundRefresh ? "background-start" : "interactive-start");
        var generation = ++_navigationGeneration;
        _capturedForNavigation = false;
        if (_service.UsesOnlyOfficialApi(provider.Id)) {
            _web.Visible=false;
            _status.Text="正在通过官方接口查询…";
            var result=await _service.RefreshOfficialApi(provider.Id);
            if(generation==_navigationGeneration && !IsDisposed) { _capturedForNavigation=result.Success; _status.Text=result.Message; UpdateDeepSeekSources(); }
            return;
        }
        if (provider.Id is "stepfun" or "baidu") {
            _web.Visible=false;
            _status.Text="请在上方填写官方接口凭据并保存测试。此厂商尚未启用网页自动采集。";
            return;
        }
        _web.Visible=true;
        await EnsureWebView();
        _status.Text = provider.Id == "minimax" && _service.HasMiniMaxApiKey
            ? "MiniMax Key 已保存；后台刷新优先通过官方 API 查询额度。"
            : provider.Id == "zhipu"
            ? "请登录智谱 GLM 并进入财务总览；自动缓存可用余额和累计消费金额，不读取 Coding Plan 额度。"
            : provider.CaptureSupported
            ? $"请登录{provider.Name}；捕获到准确用量后会自动缓存百分比。"
            : $"{provider.Name}已提供统一登录入口；当前版本暂不读取其额度数字。";
        ApplyZoom();
        _web.CoreWebView2.Navigate(provider.Url);
        if (provider.CaptureSupported) _ = ShowPendingStatus(provider, generation);
    }

    async Task EnsureWebView()
    {
        if (_web.CoreWebView2 != null) return;
        var web = _web;
        var initialization = _webInitialization ??= InitializeWebView(web);
        try
        {
            await initialization;
        }
        finally
        {
            if (ReferenceEquals(_webInitialization, initialization) && initialization.IsCompleted)
                _webInitialization = null;
        }
    }

    async Task InitializeWebView(WebView2 web)
    {
        var profile = Path.Combine(
            AIBotBridge.AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AI-bot", "quota-auth-profile");
        var env = await CoreWebView2Environment.CreateAsync(null, profile);
        await web.EnsureCoreWebView2Async(env);
        if (!ReferenceEquals(web, _web) || web.IsDisposed) return;
        _activeCore = web.CoreWebView2;
        _activeCore.NavigationCompleted += WebViewNavigationCompleted;
        _activeCore.ProcessFailed += WebViewProcessFailed;
        _activeCore.Settings.IsPasswordAutosaveEnabled = false;
        _activeCore.Settings.AreDevToolsEnabled = false;
        await _activeCore.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
        _responseReceiver = _activeCore.GetDevToolsProtocolEventReceiver("Network.responseReceived");
        _finishedReceiver = _activeCore.GetDevToolsProtocolEventReceiver("Network.loadingFinished");
        _responseReceiver.DevToolsProtocolEventReceived += CdpResponseReceived;
        _finishedReceiver.DevToolsProtocolEventReceived += CdpLoadingFinished;
    }

    async void WebViewNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!ReferenceEquals(sender, _activeCore)) return;
        if (_activeProvider != null) {
            var loginPage = Uri.TryCreate(_activeCore.Source, UriKind.Absolute, out var location) &&
                (location.AbsolutePath.Contains("login", StringComparison.OrdinalIgnoreCase) || location.AbsolutePath.Contains("signin", StringComparison.OrdinalIgnoreCase));
            AIBotBridge.WebQuotaDiagnostics.Record(_activeProvider.Id, !e.IsSuccess ? "navigation-failed" : loginPage ? "login-page" : "navigation-complete");
        }
        if (_activeProvider != null && !_service.HasOfficialApi(_activeProvider.Id)) {
            if(!e.IsSuccess) _service.ReportWebFailure(_activeProvider.Id,$"{_activeProvider.Name} 网页加载失败，请检查网络或重新登录。",!_backgroundRefresh);
            else if(Uri.TryCreate(_activeCore.Source,UriKind.Absolute,out var login) &&
                (login.AbsolutePath.Contains("login",StringComparison.OrdinalIgnoreCase)||login.AbsolutePath.Contains("signin",StringComparison.OrdinalIgnoreCase)))
                _service.ReportWebFailure(_activeProvider.Id,$"{_activeProvider.Name} 网页登录尚未恢复，正在自动重试；若持续无法更新，请打开国产模型额度设置确认登录。",!_backgroundRefresh);
        }
        if (e.IsSuccess && _activeProvider?.Id is "qwen" or "kimi" or "minimax" or "deepseek" or "zhipu" or "xiaomi")
            await PersistLoginCookies(_activeProvider.Url);
        if (e.IsSuccess && _activeProvider?.Id == "kimi")
            _ = CaptureKimiPageMetadata(_navigationGeneration);
        if (e.IsSuccess && _activeProvider?.Id == "qwen")
            _ = CaptureQwenPageMetadata(_navigationGeneration);
    }

    void WebViewProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e)
    {
        if (!ReferenceEquals(sender, _activeCore)) return;
        QuotaDiagnostics.Log(
            $"quota WebView2 process failed: kind={e.ProcessFailedKind}, reason={e.Reason}, "
            + $"exit_code={e.ExitCode}, description={e.ProcessDescription}, "
            + $"source={e.FailureSourceModulePath}");
        if (_activeProvider != null) _service.ReportWebFailure(_activeProvider.Id,$"{_activeProvider.Name} 网页采集进程中断，正在尝试恢复；旧数据已保留。",!_backgroundRefresh);
        if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited)
        {
            QueueWebViewRecovery(_activeProvider ?? ProviderById(_initialProviderId));
            return;
        }
        if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessExited)
            BeginInvoke(async () => await ReloadWebView());
    }

    bool QueueWebViewRecovery(DomesticProviderDefinition provider)
    {
        if (_webRecoveryPending || IsDisposed || !IsHandleCreated) return false;
        _webRecoveryPending = true;
        var request = ++_webRecoveryRequest;
        ++_navigationGeneration;
        BeginInvoke(async () => await RecoverWebView(request, provider));
        return true;
    }

    async Task RecoverWebView(int request, DomesticProviderDefinition provider)
    {
        try
        {
            var oldWeb = _web;
            DetachWebViewEvents();
            lock (_quotaResponses) _quotaResponses.Clear();
            _webHost.Controls.Remove(oldWeb);
            oldWeb.Dispose();
            _web = CreateWebView();
            _webHost.Controls.Add(_web);
            _webInitialization = null;
            _webRecoveryPending = false;
            await Navigate(provider);
            if (request != _webRecoveryRequest) return;
            ++_webRecoveryCount;
            QuotaDiagnostics.Log(
                $"quota WebView2 recovered: provider={provider.Id}, browser_pid={BrowserProcessIdForTest}");
        }
        catch (Exception ex)
        {
            QuotaDiagnostics.Log($"quota WebView2 recovery failed: {ex}");
            if (!IsDisposed) _status.Text = $"授权浏览器恢复失败：{ex.Message}；下次刷新将重试。";
        }
        finally
        {
            if (request == _webRecoveryRequest) _webRecoveryPending = false;
        }
    }

    void DetachWebViewEvents()
    {
        try
        {
            if (_responseReceiver != null)
                _responseReceiver.DevToolsProtocolEventReceived -= CdpResponseReceived;
            if (_finishedReceiver != null)
                _finishedReceiver.DevToolsProtocolEventReceived -= CdpLoadingFinished;
            if (_activeCore != null)
            {
                _activeCore.NavigationCompleted -= WebViewNavigationCompleted;
                _activeCore.ProcessFailed -= WebViewProcessFailed;
            }
        }
        catch
        {
            // A failed browser process can reject COM event unsubscription.
        }
        _responseReceiver = null;
        _finishedReceiver = null;
        _activeCore = null;
    }

    async Task ReloadWebView()
    {
        try
        {
            if (_web.CoreWebView2 == null)
                await Navigate(_activeProvider ?? ProviderById(_initialProviderId));
            else
                _web.CoreWebView2.Reload();
        }
        catch (InvalidOperationException ex)
        {
            if (QueueWebViewRecovery(_activeProvider ?? ProviderById(_initialProviderId)))
                QuotaDiagnostics.Log($"quota WebView2 reload rejected; recreating control: {ex.Message}");
        }
    }

    async Task CaptureKimiPageMetadata(int generation)
    {
        const string script = """
            (() => {
              const cards = Array.from(document.querySelectorAll('.stats-card'));
              const card = cards.find(item => {
                const title = item.querySelector('.stats-card-title');
                const text = title ? title.textContent.trim() : '';
                return text === '我的权益' || text === 'My Benefits';
              });
              const value = card ? card.querySelector('.stats-card-value') : null;
              const metric = pattern => {
                const containers = Array.from(document.querySelectorAll(
                  '.stats-card, .combined-card-quota, .combined-card-item'));
                const container = containers.find(item => pattern.test(item.innerText || ''));
                const match = container?.innerText?.match(/(\d+(?:\.\d+)?)\s*%/);
                return match ? Number(match[1]) : null;
              };
              const entries = Array.from(document.querySelectorAll(
                  '.stats-card-reset-time, .combined-card-reset-time'))
                .map(item => ({
                  text: item.textContent.trim(),
                  context: (item.closest('.combined-card-quota, .stats-card') || item.parentElement)
                    ?.innerText || ''
                })).filter(item => item.text);
              const unique = entries.filter((item, index) =>
                entries.findIndex(other => other.text === item.text) === index);
              const weekly = unique.find(item => /本周用量|weekly\s+usage/i.test(item.context));
              const fiveHour = unique.find(item => /频限明细|rate\s+limit/i.test(item.context));
              return {
                membership: value ? value.textContent.trim() : '',
                weeklyPct: metric(/本周用量|weekly\s+usage/i),
                fiveHourPct: metric(/频限明细|rate\s+limit/i),
                weeklyResetText: weekly?.text || '',
                fiveHourResetText: fiveHour?.text || ''
              };
            })()
            """;
        double? previousWeeklyPct = null;
        double? previousFiveHourPct = null;
        var previousWeeklyReset = "";
        var previousFiveHourReset = "";
        var stableReadCount = 0;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (IsDisposed || generation != _navigationGeneration || _activeProvider?.Id != "kimi"
                || _web.CoreWebView2 == null) return;
            try
            {
                var result = await _web.CoreWebView2.ExecuteScriptAsync(script);
                using var doc = JsonDocument.Parse(result);
                var root = doc.RootElement;
                var membership = root.TryGetProperty("membership", out var membershipValue)
                    ? membershipValue.GetString()?.Trim() ?? "" : "";
                var weeklyReset = root.TryGetProperty("weeklyResetText", out var weeklyValue)
                    ? weeklyValue.GetString()?.Trim() ?? "" : "";
                var fiveHourReset = root.TryGetProperty("fiveHourResetText", out var fiveHourValue)
                    ? fiveHourValue.GetString()?.Trim() ?? "" : "";
                var weeklyPct = root.TryGetProperty("weeklyPct", out var weeklyPctValue)
                    && weeklyPctValue.ValueKind == JsonValueKind.Number
                    ? weeklyPctValue.GetDouble() : (double?)null;
                var fiveHourPct = root.TryGetProperty("fiveHourPct", out var fiveHourPctValue)
                    && fiveHourPctValue.ValueKind == JsonValueKind.Number
                    ? fiveHourPctValue.GetDouble() : (double?)null;
                if (weeklyPct.HasValue && fiveHourPct.HasValue
                    && weeklyReset.Length > 0 && fiveHourReset.Length > 0)
                {
                    stableReadCount = weeklyPct == previousWeeklyPct
                        && fiveHourPct == previousFiveHourPct
                        && weeklyReset == previousWeeklyReset
                        && fiveHourReset == previousFiveHourReset ? stableReadCount + 1 : 1;
                    previousWeeklyPct = weeklyPct;
                    previousFiveHourPct = fiveHourPct;
                    previousWeeklyReset = weeklyReset;
                    previousFiveHourReset = fiveHourReset;
                    if (stableReadCount >= 2)
                    {
                        _service.SetKimi(weeklyPct.Value, fiveHourPct, membership,
                            DomesticQuotaService.ParseResetAt(weeklyReset),
                            DomesticQuotaService.ParseResetAt(fiveHourReset));
                        _capturedForNavigation = true;
                        QuotaDiagnostics.Log(
                            $"[quota] Kimi DOM updated: Weekly {weeklyPct.Value:0.##}%, "
                            + $"5H {fiveHourPct.Value:0.##}%");
                        BeginInvoke(() => _status.Text =
                            $"已取得 Kimi 准确用量：Weekly {weeklyPct.Value:0.##}%，"
                            + $"5h {fiveHourPct.Value:0.##}%（已缓存）");
                        if (_backgroundRefresh) BeginInvoke(Hide);
                        return;
                    }
                }
            }
            catch
            {
                // The Vue page may still be replacing its initial DOM; retry below.
            }
            await Task.Delay(500);
        }
    }

    async Task CaptureQwenPageMetadata(int generation)
    {
        const string script = """
            (() => {
              const text = document.body ? document.body.innerText : '';
              const reset = text.match(/重置时间\s*([0-9]{4}[-\/.]\d{1,2}[-\/.]\d{1,2}\s+\d{1,2}:\d{2}(?::\d{2})?)/);
              const candidates = Array.from(document.querySelectorAll(
                '[class*="card"], [class*="panel"], [class*="quota"], [class*="usage"]'))
                .map(node => node.innerText || '')
                .filter(value => /用量消耗|usage\s+consumption/i.test(value) && /\d+(?:\.\d+)?\s*%/.test(value))
                .sort((a, b) => a.length - b.length);
              const marker = text.search(/用量消耗|usage\s+consumption/i);
              const fallback = marker >= 0 ? text.slice(marker, marker + 800) : '';
              const usage = (candidates[0] || fallback).match(/(\d+(?:\.\d+)?)\s*%/);
              let membership = '';
              if (/Coding\s*Plan/i.test(text)) membership = 'Coding Plan';
              else if (/Token\s*Plan/i.test(text) && /团队版/i.test(text)) membership = 'Token Plan 团队版';
              else if (/Token\s*Plan/i.test(text)) membership = 'Token Plan';
              const planCard = Array.from(document.querySelectorAll('[class*="card"], [class*="panel"], [class*="plan"]'))
                .map(node => node.innerText || '')
                .filter(value => /Coding\s*Plan/i.test(value) && /到期|有效期/.test(value))
                .sort((a, b) => a.length - b.length)[0] || '';
              const expiry = planCard.match(/(?:套餐到期|计划到期|到期日期|到期时间|有效期至)\s*([0-9]{4}[-\/.]\d{1,2}[-\/.]\d{1,2}(?:\s+\d{1,2}:\d{2}(?::\d{2})?)?)/);
              return {
                resetText: reset ? reset[1] : '',
                usagePctText: usage ? usage[1] : '',
                membership,
                expiryText: expiry ? expiry[1] : ''
              };
            })()
            """;
        for (var attempt = 0; attempt < 80; attempt++)
        {
            if (IsDisposed || generation != _navigationGeneration || _activeProvider?.Id != "qwen"
                || _web.CoreWebView2 == null || _capturedForNavigation) return;
            try
            {
                var result = await _web.CoreWebView2.ExecuteScriptAsync(script);
                using var doc = JsonDocument.Parse(result);
                var root = doc.RootElement;
                var reset = root.TryGetProperty("resetText", out var resetValue)
                    ? resetValue.GetString()?.Trim() ?? "" : "";
                var usagePctText = root.TryGetProperty("usagePctText", out var usagePctValue)
                    ? usagePctValue.GetString()?.Trim() ?? "" : "";
                var membership = root.TryGetProperty("membership", out var membershipValue)
                    ? membershipValue.GetString()?.Trim() ?? "" : "";
                var expiry = root.TryGetProperty("expiryText", out var expiryValue)
                    ? expiryValue.GetString()?.Trim() ?? "" : "";
                _service.SetQwenPageMetadata(reset, membership, expiry);
                if (double.TryParse(usagePctText, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var usagePct)
                    && usagePct is >= 0 and <= 100)
                {
                    _service.SetQwen(usagePct, membership, DomesticQuotaService.ParseResetAt(reset));
                    _capturedForNavigation = true;
                    if (_backgroundRefresh) BeginInvoke(Hide);
                    return;
                }
            }
            catch
            {
                // The console shell renders before the subscription card; retry below.
            }
            await Task.Delay(500);
        }
    }

    async Task ShowPendingStatus(DomesticProviderDefinition provider, int generation)
    {
        // Kimi's console now loads its Connect usage service after the main
        // shell. On slower links the old 12-second window hid the background
        // WebView before BillingService/GetUsage completed, leaving both
        // Weekly and 5H stuck on the last successful cache.
        await Task.Delay(TimeSpan.FromSeconds(provider.Id == "qwen" ? 45
            : provider.Id is "kimi" or "minimax" or "deepseek" or "zhipu" ? 60 : 12));
        if (IsDisposed || generation != _navigationGeneration || _activeProvider != provider
            || _capturedForNavigation) return;
        if(provider.Id=="deepseek" && _web.CoreWebView2!=null) {
            try {
                var health=await _web.CoreWebView2.ExecuteScriptAsync("JSON.stringify({login:!!document.querySelector('input[type=password],input[type=tel]'),textLength:(document.body?.innerText||'').length})");
                using var wrapper=JsonDocument.Parse(health);
                using var state=JsonDocument.Parse(wrapper.RootElement.GetString()??"{}");
                var login=state.RootElement.TryGetProperty("login",out var l)&&l.ValueKind==JsonValueKind.True;
                AIBotBridge.WebQuotaDiagnostics.Record("deepseek",login?"login-required":"no-usage-response");
            }catch{AIBotBridge.WebQuotaDiagnostics.Record("deepseek","page-state-unavailable");}
        }
        AIBotBridge.WebQuotaDiagnostics.Record(provider.Id, "capture-timeout");
        _service.ReportWebFailure(provider.Id,$"{provider.Name} 网页额度刷新超时，正在自动重试；已保留旧数据。可打开国产模型额度设置查看详情。",!_backgroundRefresh);
        var snapshot = _service.Snapshot;
        var fetchedAt = provider.Id switch
        {
            "qwen" => snapshot.QwenFetchedAt,
            "xiaomi" => snapshot.XiaomiFetchedAt,
            "kimi" => snapshot.KimiFetchedAt,
            "minimax" => snapshot.MiniMaxFetchedAt,
            "deepseek" => snapshot.DeepSeekFetchedAt,
            "zhipu" => snapshot.Zhipu?.UpdatedAt.UtcDateTime,
            _ => null,
        };
        var last = fetchedAt.HasValue
            ? $"最近成功：{fetchedAt.Value.ToLocalTime():M月d日 HH:mm}"
            : "尚无成功记录";
        BeginInvoke(() => _status.Text = $"尚未捕获到{provider.Product}额度响应；{last}。可刷新页面重试。 ");
        if (_backgroundRefresh) BeginInvoke(Hide);
    }

    void CdpResponseReceived(object sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
    {
        if(_activeProvider != null && _service.UsesOnlyOfficialApi(_activeProvider.Id)) return;
        try
        {
            using var doc = JsonDocument.Parse(e.ParameterObjectAsJson);
            var root = doc.RootElement;
            var requestId = root.GetProperty("requestId").GetString();
            var response = root.GetProperty("response");
            var uri = response.GetProperty("url").GetString() ?? "";
            var mime = response.TryGetProperty("mimeType", out var mimeValue)
                ? mimeValue.GetString() ?? "" : "";
            var resourceType = root.TryGetProperty("type", out var typeValue)
                ? typeValue.GetString() ?? "" : "";
            var statusCode = response.TryGetProperty("status", out var statusValue)
                && statusValue.ValueKind == JsonValueKind.Number
                ? statusValue.GetInt32() : 0;
            var jsonRequest = (resourceType == "XHR" || resourceType == "Fetch")
                && (mime.Contains("json", StringComparison.OrdinalIgnoreCase)
                    || uri.Contains(".json", StringComparison.OrdinalIgnoreCase));
            var isAlibaba = _activeProvider?.Id == "qwen" && jsonRequest
                && Uri.TryCreate(uri, UriKind.Absolute, out var alibabaUri)
                && alibabaUri.Host.Equals("bailian.console.aliyun.com", StringComparison.OrdinalIgnoreCase)
                && alibabaUri.AbsolutePath.Equals("/data/api.json", StringComparison.OrdinalIgnoreCase);
            var isXiaomi = _activeProvider?.Id == "xiaomi"
                && (resourceType == "XHR" || resourceType == "Fetch") && XiaomiQuota.IsEndpoint(uri);
            var isKimiUsage = _activeProvider?.Id == "kimi"
                && (resourceType == "XHR" || resourceType == "Fetch")
                && IsKimiUsageEndpoint(uri);
            var isKimi = _activeProvider?.Id == "kimi"
                && (jsonRequest || isKimiUsage)
                && uri.Contains("kimi", StringComparison.OrdinalIgnoreCase);
            var isMiniMax = _activeProvider?.Id == "minimax"
                && (jsonRequest || IsMiniMaxUsageEndpoint(uri))
                && IsMiniMaxUsageEndpoint(uri);
            var isDeepSeek = _activeProvider?.Id == "deepseek"
                && (resourceType == "XHR" || resourceType == "Fetch")
                && Uri.TryCreate(uri, UriKind.Absolute, out var deepSeekUri)
                && deepSeekUri.Host.Contains("deepseek", StringComparison.OrdinalIgnoreCase)
                && IsDeepSeekUsageEndpoint(uri);
            var isZhipu = _activeProvider?.Id == "zhipu"
                && (resourceType == "XHR" || resourceType == "Fetch")
                && AIBotBridge.ZhipuBalance.IsEndpoint(uri);
            if (isZhipu) AIBotBridge.WebQuotaDiagnostics.Record("zhipu", "balance-response", statusCode);
            if (isDeepSeek) AIBotBridge.WebQuotaDiagnostics.Record("deepseek", "wallet-response", statusCode);
            if ((statusCode == 401 || statusCode == 403) && (isAlibaba || isXiaomi || isKimiUsage || isMiniMax || isZhipu || isDeepSeek && IsDeepSeekUsageEndpoint(uri))) {
                _service.ReportWebFailure(_activeProvider.Id,$"{_activeProvider.Name} 余额请求未通过认证或访问被拒绝，正在自动重试；若持续失败，请检查登录状态和访问权限。旧数据已保留。",!_backgroundRefresh);
                return;
            }
            if (isXiaomi && statusCode != 200 && statusCode != 429) {
                _service.ReportWebFailure("xiaomi","小米套餐用量请求失败，已保留上次结果，请刷新或重新登录。",!_backgroundRefresh);
                return;
            }
            if (isZhipu && statusCode != 200 && statusCode != 429) return;
            if (statusCode == 429 && (isAlibaba || isXiaomi || isKimi || isMiniMax || isDeepSeek || isZhipu))
            {
                var providerId = isAlibaba ? "qwen" : isXiaomi ? "xiaomi"
                    : isKimi ? "kimi" : isMiniMax ? "minimax" : isZhipu ? "zhipu" : "deepseek";
                _service.BackOff(providerId=="deepseek"?"deepseek-web":providerId);
                _service.ReportWebFailure(providerId,"供应商额度接口限流，5 分钟后重试；正在显示旧数据。",!_backgroundRefresh);
                BeginInvoke(() => _status.Text = "供应商额度接口限流，5 分钟后自动重试；当前继续显示最近成功值。");
                BeginInvoke(UpdateDeepSeekSources);
                if (_backgroundRefresh) BeginInvoke(Hide);
                return;
            }
            if(isDeepSeek && statusCode!=200)return;
            if (requestId != null && (isAlibaba || isXiaomi || isKimi || isMiniMax || isDeepSeek || isZhipu))
            {
                var endpoint = Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
                    ? parsed.Host + parsed.AbsolutePath : uri.Split('?')[0];
                lock (_quotaResponses)
                    _quotaResponses[requestId] =
                        (isAlibaba ? "qwen" : isXiaomi ? "xiaomi"
                            : isKimi ? "kimi" : isMiniMax ? "minimax" : isZhipu ? "zhipu" : "deepseek", endpoint, _navigationGeneration);
                if (isKimiUsage)
                    QuotaDiagnostics.Log($"[quota] Kimi usage response detected: {endpoint}");
                if (isMiniMax)
                    QuotaDiagnostics.Log($"[quota] MiniMax usage response detected: {endpoint}");
                if (isDeepSeek)
                    QuotaDiagnostics.Log($"[quota] DeepSeek balance response detected: {endpoint}");
            }
        }
        catch
        {
            // Ignore unrelated or incomplete browser network events.
        }
    }

    async void CdpLoadingFinished(object sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
    {
        string requestId;
        try
        {
            using var finished = JsonDocument.Parse(e.ParameterObjectAsJson);
            requestId = finished.RootElement.GetProperty("requestId").GetString();
        }
        catch { return; }
        (string ProviderId, string Endpoint, int Generation) responseInfo;
        lock (_quotaResponses)
        {
            if (requestId == null || !_quotaResponses.Remove(requestId, out responseInfo)) return;
            if (_service.UsesOnlyOfficialApi(responseInfo.ProviderId) || responseInfo.Generation != _navigationGeneration) return;
        }
        try
        {
            var args = JsonSerializer.Serialize(new { requestId });
            var result = await _web.CoreWebView2.CallDevToolsProtocolMethodAsync(
                "Network.getResponseBody", args);
            using var envelope = JsonDocument.Parse(result);
            var body = envelope.RootElement.GetProperty("body").GetString() ?? "";
            if (envelope.RootElement.TryGetProperty("base64Encoded", out var encoded)
                && encoded.ValueKind == JsonValueKind.True)
                body = Encoding.UTF8.GetString(Convert.FromBase64String(body));

            if (_service.UsesOnlyOfficialApi(responseInfo.ProviderId) || responseInfo.Generation != _navigationGeneration) return;
            using var doc = JsonDocument.Parse(body);
            double weeklyPct;
            double? fiveHourPct = null;
            string membership = null;
            if (responseInfo.ProviderId == "xiaomi") {
                var value=XiaomiQuota.ParseSnapshot(doc.RootElement);
                weeklyPct = value.PlanPercent!.Value;
                _service.SetXiaomi(value);
            }
            else if (responseInfo.ProviderId == "kimi")
            {
                membership = FindKimiMembership(doc.RootElement);
                AIBotBridge.DomesticProviderQuotaSnapshot kimi;
                try {kimi=AIBotBridge.DomesticQuotaService.Parse("kimi",body);}
                catch(JsonException)
                {
                    _service.SetKimiMembership(membership);
                    return;
                }
                weeklyPct = kimi.WeeklyPercent??kimi.PlanPercent??kimi.PrimaryPercent??0;
                fiveHourPct = kimi.PrimaryPercent;
                _service.SetKimi(kimi with {Plan=membership});
                QuotaDiagnostics.Log(
                    $"[quota] Kimi updated: Weekly {weeklyPct:0.##}%, 5H "
                    + (fiveHourPct.HasValue ? $"{fiveHourPct.Value:0.##}%" : "--"));
            }
            else if (responseInfo.ProviderId == "minimax")
            {
                var miniMax = FindMiniMaxUsage(doc.RootElement);
                if (!miniMax.HasValue) return;
                weeklyPct = miniMax.Value.WeeklyPct;
                fiveHourPct = miniMax.Value.FiveHourPct;
                membership = miniMax.Value.Membership;
                _service.SetMiniMax(weeklyPct, fiveHourPct, membership,
                    miniMax.Value.WeeklyResetAt, miniMax.Value.FiveHourResetAt,
                    primaryWindowSeconds:miniMax.Value.PrimaryWindowSeconds,weeklyWindowSeconds:miniMax.Value.WeeklyWindowSeconds);
                QuotaDiagnostics.Log(
                    $"[quota] MiniMax updated: Weekly {weeklyPct:0.##}%, 5H "
                    + (fiveHourPct.HasValue ? $"{fiveHourPct.Value:0.##}%" : "--"));
            }
            else if (responseInfo.ProviderId == "zhipu")
            {
                var value = AIBotBridge.ZhipuBalance.Parse(doc.RootElement);
                _service.SetZhipu(value);
                AIBotBridge.WebQuotaDiagnostics.Record("zhipu", "balance-saved");
                weeklyPct = 0;
                membership = "CNY";
            }
            else if (responseInfo.ProviderId == "deepseek")
            {
                var deepSeek = FindDeepSeekBalance(doc.RootElement);
                if (!deepSeek.HasValue) {AIBotBridge.WebQuotaDiagnostics.Record("deepseek","wallet-fields-missing");return;}
                var usedCost = FindDeepSeekUsedCost(doc.RootElement, deepSeek.Value.Currency);
                if(!_service.SetDeepSeekWeb(deepSeek.Value.Total, deepSeek.Value.Currency,
                    deepSeek.Value.Granted, deepSeek.Value.ToppedUp, usedCost)) {
                    AIBotBridge.WebQuotaDiagnostics.Record("deepseek","wallet-mismatch");return;
                }
                AIBotBridge.WebQuotaDiagnostics.Record("deepseek",usedCost.HasValue?"usage-saved":"balance-saved");
                weeklyPct = 0;
                membership = deepSeek.Value.Currency;
                QuotaDiagnostics.Log(
                    $"[quota] DeepSeek updated: {deepSeek.Value.Currency} {deepSeek.Value.Total:0.00}");
            }
            else
            {
                if (responseInfo.ProviderId == "qwen")
                    membership = DomesticQuotaService.QwenSubscriptionName;
                var isQwen = responseInfo.ProviderId == "qwen";
                var qwen = isQwen
                    ? FindQwenUsage(doc.RootElement) : null;
                var pct = qwen?.Pct ?? FindUsage(doc.RootElement, isQwen);
                if (!pct.HasValue) return;
                weeklyPct = pct.Value;
                if (responseInfo.ProviderId == "qwen")
                    _service.SetQwen(pct.Value, membership, qwen?.ResetAt,windowSeconds:qwen?.WindowSeconds);
                else _service.SetXiaomi(pct.Value);
            }
            var responseProvider = ProviderById(responseInfo.ProviderId);
            var loginSaved = responseInfo.ProviderId is "qwen" or "kimi" or "minimax" or "deepseek" or "zhipu" or "xiaomi"
                && await PersistLoginCookies(responseProvider.Url);
            _capturedForNavigation = true;
            BeginInvoke(() => _status.Text =
                responseInfo.ProviderId == "kimi"
                    ? $"已取得 Kimi 准确用量：Weekly {weeklyPct:0.##}%"
                        + (fiveHourPct.HasValue ? $"，5h {fiveHourPct.Value:0.##}%" : "")
                        + "（已缓存）" + (loginSaved ? "；登录状态已持久保存" : "")
                    : responseInfo.ProviderId == "minimax"
                    ? $"已取得 MiniMax 准确用量：Weekly {weeklyPct:0.##}%"
                        + (fiveHourPct.HasValue ? $"，5h {fiveHourPct.Value:0.##}%" : "")
                        + "（已缓存）" + (loginSaved ? "；登录状态已持久保存" : "")
                    : responseInfo.ProviderId == "zhipu"
                    ? $"已取得智谱 GLM 可用余额：CNY {_service.Snapshot.Zhipu.Balance:0.00}（已缓存）"
                    : responseInfo.ProviderId == "deepseek"
                    ? $"已取得 DeepSeek 可用余额：{membership} "
                        + $"{_service.Snapshot.DeepSeekWebBalance:0.00}（网页来源，独立缓存）"
                        + (loginSaved ? "；登录状态已持久保存" : "")
                    : $"已取得{(responseInfo.ProviderId == "qwen" ? "阿里云" : "小米")}准确用量：{weeklyPct:F1}%（已缓存）"
                    + (responseInfo.ProviderId == "qwen" && !string.IsNullOrWhiteSpace(membership)
                        ? $"；订阅：{membership}" : "")
                    + (loginSaved ? "；登录状态已持久保存" : ""));
            BeginInvoke(UpdateDeepSeekSources);
            if (_backgroundRefresh) BeginInvoke(Hide);
        }
        catch (Exception ex)
        {
            AIBotBridge.WebQuotaDiagnostics.Record(responseInfo.ProviderId, "response-parse-failed");
            QuotaDiagnostics.Log(
                $"[quota] {responseInfo.ProviderId} response parse failed at {responseInfo.Endpoint}: {ex.Message}");
            _service.ReportWebFailure(responseInfo.ProviderId,"网页额度响应无法识别，已保留上次结果；请稍后刷新或重新登录。",!_backgroundRefresh);
            BeginInvoke(() => _status.Text = "网页额度响应无法识别，已保留上次结果；请稍后刷新或重新登录。");
        }
    }

    static bool IsKimiUsageEndpoint(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)) return false;
        return parsed.AbsolutePath.Contains("BillingService/GetUsage",
            StringComparison.OrdinalIgnoreCase);
    }

    static bool IsMiniMaxUsageEndpoint(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)) return false;
        var host = parsed.Host;
        if (!host.Contains("minimax", StringComparison.OrdinalIgnoreCase)
            && !host.Contains("minimaxi", StringComparison.OrdinalIgnoreCase))
            return false;
        var path = parsed.AbsolutePath;
        return path.Contains("token_plan", StringComparison.OrdinalIgnoreCase)
            || path.Contains("coding_plan", StringComparison.OrdinalIgnoreCase)
            || path.Contains("model_remains", StringComparison.OrdinalIgnoreCase)
            || path.Contains("usage", StringComparison.OrdinalIgnoreCase)
            || path.Contains("remains", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsDeepSeekUsageEndpoint(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
            || !parsed.Host.Equals("platform.deepseek.com", StringComparison.OrdinalIgnoreCase)) return false;
        var path = parsed.AbsolutePath;
        return path.EndsWith("/get_user_summary", StringComparison.OrdinalIgnoreCase)
            || path.Contains("balance", StringComparison.OrdinalIgnoreCase)
            || path.Contains("billing", StringComparison.OrdinalIgnoreCase)
            || path.Contains("usage", StringComparison.OrdinalIgnoreCase)
            || path.Contains("account", StringComparison.OrdinalIgnoreCase);
    }

    async Task<bool> PersistLoginCookies(string url)
    {
        if (_web.CoreWebView2 == null) return false;
        try
        {
            var manager = _web.CoreWebView2.CookieManager;
            var cookies = await manager.GetCookiesAsync(url);
            var expires = DateTime.UtcNow.Add(LoginRetention);
            var updated = false;
            foreach (var cookie in cookies)
            {
                if (!cookie.IsSession) continue;
                cookie.Expires = expires;
                manager.AddOrUpdateCookie(cookie);
                updated = true;
            }
            return updated || cookies.Any(cookie => !cookie.IsSession && cookie.Expires > DateTime.UtcNow);
        }
        catch
        {
            return false;
        }
    }

    internal readonly record struct MiniMaxUsage(double WeeklyPct, double? FiveHourPct,
        string Membership, DateTimeOffset? WeeklyResetAt, DateTimeOffset? FiveHourResetAt)
    {
        public long? PrimaryWindowSeconds {get;init;}=18000;
        public long? WeeklyWindowSeconds {get;init;}=604800;
    }
    internal readonly record struct DeepSeekBalance(double Total, double? Granted,
        double? ToppedUp, string Currency);
    readonly record struct KimiUsage(double WeeklyPct, double? FiveHourPct,
        DateTimeOffset? WeeklyResetAt, DateTimeOffset? FiveHourResetAt);
    readonly record struct QwenUsage(double Pct, DateTimeOffset? ResetAt, long? WindowSeconds);

    internal static DeepSeekBalance? FindDeepSeekBalance(JsonElement element)
    {
        var value=AIBotBridge.DeepSeekWallets.Parse(element);
        return value is null?null:new(value.Total,value.Granted,value.ToppedUp,value.Currency);
    }

    internal static double? FindDeepSeekUsedCost(JsonElement element, string currency)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var values = element.EnumerateObject().ToDictionary(p => p.Name, p => p.Value,
                StringComparer.OrdinalIgnoreCase);
            if (values.TryGetValue("total_costs", out var costs)
                && costs.ValueKind == JsonValueKind.Array)
            {
                double? first = null;
                foreach (var cost in costs.EnumerateArray())
                {
                    if (cost.ValueKind != JsonValueKind.Object) continue;
                    var costValues = cost.EnumerateObject().ToDictionary(p => p.Name, p => p.Value,
                        StringComparer.OrdinalIgnoreCase);
                    var amount = Number(costValues, "amount");
                    if (!amount.HasValue) continue;
                    first ??= amount;
                    var itemCurrency = costValues.TryGetValue("currency", out var currencyValue)
                        && currencyValue.ValueKind == JsonValueKind.String
                        ? currencyValue.GetString()?.Trim() ?? "" : "";
                    if (itemCurrency.Equals(currency, StringComparison.OrdinalIgnoreCase))
                        return amount;
                }
                /* A different currency is not a usable fallback. */
            }
            foreach (var child in values.Values)
                if (FindDeepSeekUsedCost(child, currency) is double found) return found;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                if (FindDeepSeekUsedCost(child, currency) is double found) return found;
        }
        return null;
    }

    static QwenUsage? FindQwenUsage(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var values = element.EnumerateObject().ToDictionary(p => p.Name, p => p.Value,
                StringComparer.OrdinalIgnoreCase);
            if (Number(values, "TotalValue") is double total && total > 0
                && (Number(values, "TotalSurplusValue") ?? Number(values, "SurplusValue")) is double remain)
            {
                DateTimeOffset? resetAt = null;
                foreach (var name in new[]
                         {
                             "ResetTime", "NextResetTime", "ResetAt", "NextResetAt",
                             "EndTime", "ExpireTime", "ExpirationTime", "ValidTo", "CycleEndTime"
                         })
                {
                    if (!values.TryGetValue(name, out var value)) continue;
                    resetAt = DateTimeValue(value);
                    if (resetAt.HasValue) break;
                }
                var bounds=QuotaCycle.Bounds(element);
                return new QwenUsage(100.0 * Math.Max(0, total - remain) / total, resetAt??bounds.Reset,
                    resetAt is null||resetAt==bounds.Reset?bounds.Seconds:null);
            }
            foreach (var child in values.Values)
                if (FindQwenUsage(child) is QwenUsage found) return found;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                if (FindQwenUsage(child) is QwenUsage found) return found;
        }
        return null;
    }

    static DateTimeOffset? DateTimeValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
            return DomesticQuotaService.ParseResetAt(value.GetString());
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var epoch)) return null;
        try
        {
            if (epoch > 10_000_000_000) return DateTimeOffset.FromUnixTimeMilliseconds(epoch);
            if (epoch > 1_000_000_000) return DateTimeOffset.FromUnixTimeSeconds(epoch);
        }
        catch (ArgumentOutOfRangeException) { }
        return null;
    }

    static DateTimeOffset? FindDirectResetAt(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in element.EnumerateObject())
        {
            var name = property.Name.ToLowerInvariant();
            if (!name.Contains("reset") && !name.Contains("refresh")
                && !name.Contains("expire") && !name.Contains("expiry")
                && !name.Contains("end_time") && !name.Contains("endtime")) continue;
            if (DateTimeValue(property.Value) is DateTimeOffset absolute)
                return absolute;
            if (RelativeResetAt(name, property.Value) is DateTimeOffset relative)
                return relative;
        }
        return null;
    }

    static DateTimeOffset? FindResetAt(JsonElement element)
    {
        if (FindDirectResetAt(element) is DateTimeOffset direct) return direct;
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array)) continue;
            if (FindResetAt(property.Value) is DateTimeOffset nested) return nested;
        }
        return null;
    }

    static DateTimeOffset? RelativeResetAt(string name, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
            return DomesticQuotaService.ParseResetAt(value.GetString());
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var amount)
            || amount <= 0) return null;
        double seconds;
        if (name.Contains("millisecond") || name.EndsWith("_ms")) seconds = amount / 1000.0;
        else if (name.Contains("minute") || name.EndsWith("_min")) seconds = amount * 60.0;
        else if (name.Contains("hour")) seconds = amount * 3600.0;
        else seconds = amount;
        return seconds <= 31 * 24 * 3600 ? DateTimeOffset.Now.AddSeconds(seconds) : null;
    }

    static string FindKimiMembership(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String) return "";
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String
                    && (property.Name.Contains("membership", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Contains("member_level", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Contains("plan_name", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Contains("tier", StringComparison.OrdinalIgnoreCase)))
                {
                    var value = property.Value.GetString()?.Trim() ?? "";
                    if (value.Length is > 0 and <= 32)
                    {
                        if (value.StartsWith("LEVEL_", StringComparison.OrdinalIgnoreCase)) continue;
                        return value;
                    }
                }
                var nested = FindKimiMembership(property.Value);
                if (nested.Length > 0) return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                var nested = FindKimiMembership(child);
                if (nested.Length > 0) return nested;
            }
        }
        return "";
    }

    static KimiUsage? FindKimiUsage(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var values = element.EnumerateObject().ToDictionary(p => p.Name, p => p.Value,
                StringComparer.OrdinalIgnoreCase);
            if (values.TryGetValue("usages", out var usages) && usages.ValueKind == JsonValueKind.Array)
            {
                foreach (var usage in usages.EnumerateArray())
                {
                    if (usage.ValueKind != JsonValueKind.Object) continue;
                    var usageValues = usage.EnumerateObject().ToDictionary(p => p.Name, p => p.Value,
                        StringComparer.OrdinalIgnoreCase);
                    if (!usageValues.TryGetValue("detail", out var detail)) continue;
                    var weeklyPct = PercentageFromRemaining(detail);
                    if (!weeklyPct.HasValue) continue;
                    var weeklyResetAt = FindResetAt(detail) ?? FindDirectResetAt(usage);
                    double? fiveHourPct = null;
                    DateTimeOffset? fiveHourResetAt = null;
                    if (usageValues.TryGetValue("limits", out var limits)
                        && limits.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var limit in limits.EnumerateArray())
                        {
                            if (limit.ValueKind != JsonValueKind.Object) continue;
                            var limitValues = limit.EnumerateObject().ToDictionary(p => p.Name, p => p.Value,
                                StringComparer.OrdinalIgnoreCase);
                            if (limitValues.TryGetValue("detail", out var rateDetail)
                                && PercentageFromRemaining(rateDetail) is double ratePct)
                            {
                                fiveHourPct = ratePct;
                                fiveHourResetAt = FindResetAt(rateDetail) ?? FindDirectResetAt(limit);
                                break;
                            }
                        }
                    }
                    return new KimiUsage(weeklyPct.Value, fiveHourPct,
                        weeklyResetAt, fiveHourResetAt);
                }
            }
            foreach (var child in values.Values)
                if (FindKimiUsage(child) is KimiUsage found) return found;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                if (FindKimiUsage(child) is KimiUsage found) return found;
        }
        return null;
    }

    internal static MiniMaxUsage? FindMiniMaxUsage(JsonElement element)
    {
        var candidates = new List<(MiniMaxUsage Usage, int Score)>();
        CollectMiniMaxUsage(element, candidates);
        return candidates.Count == 0
            ? null
            : candidates.OrderByDescending(item => item.Score).First().Usage;
    }

    static void CollectMiniMaxUsage(JsonElement element, List<(MiniMaxUsage Usage, int Score)> candidates)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var values = element.EnumerateObject().ToDictionary(p => p.Name, p => p.Value,
                StringComparer.OrdinalIgnoreCase);
            if (TryMiniMaxUsage(values) is (MiniMaxUsage Usage, int Score) candidate)
                candidates.Add(candidate);
            foreach (var child in values.Values)
                if (child.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    CollectMiniMaxUsage(child, candidates);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                CollectMiniMaxUsage(child, candidates);
        }
    }

    static (MiniMaxUsage Usage, int Score)? TryMiniMaxUsage(Dictionary<string, JsonElement> values)
    {
        var weeklyPct = MiniMaxPercent(values, "current_weekly")
            ?? MiniMaxPercent(values, "weekly");
        var fiveHourPct = MiniMaxPercent(values, "current_interval")
            ?? MiniMaxPercent(values, "interval")
            ?? MiniMaxPercent(values, "five_hour")
            ?? MiniMaxPercent(values, "fiveHour");
        if (!weeklyPct.HasValue) return null;

        var weeklyResetAt = MiniMaxResetAt(values, "weekly_remains_time")
            ?? MiniMaxResetAt(values, "current_weekly_remains_time")
            ?? FindDirectResetAt(ObjectFromValues(values, "weekly"));
        var fiveHourResetAt = MiniMaxResetAt(values, "remains_time")
            ?? MiniMaxResetAt(values, "current_interval_remains_time")
            ?? MiniMaxResetAt(values, "interval_remains_time");
        var membership = FindMiniMaxMembership(values);
        var score = 0;
        if (values.TryGetValue("model_name", out var modelValue)
            && modelValue.ValueKind == JsonValueKind.String)
        {
            var model = modelValue.GetString() ?? "";
            if (model.Equals("general", StringComparison.OrdinalIgnoreCase)) score += 30;
            if (!model.Contains("video", StringComparison.OrdinalIgnoreCase)
                && !model.Contains("hailuo", StringComparison.OrdinalIgnoreCase)) score += 15;
        }
        if (fiveHourPct.HasValue) score += 10;
        if (membership.Length > 0) score += 5;
        return (new MiniMaxUsage(weeklyPct.Value, fiveHourPct, membership,
            weeklyResetAt, fiveHourResetAt) {PrimaryWindowSeconds=QuotaCycle.MiniMaxWindow(JsonSerializer.SerializeToElement(values),false),
                WeeklyWindowSeconds=QuotaCycle.MiniMaxWindow(JsonSerializer.SerializeToElement(values),true)}, score);
    }

    static JsonElement ObjectFromValues(Dictionary<string, JsonElement> values, string name)
    {
        return values.TryGetValue(name, out var value) ? value : default;
    }

    static DateTimeOffset? MiniMaxResetAt(Dictionary<string, JsonElement> values, string name)
    {
        if (!values.TryGetValue(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()?.Trim() ?? "";
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture,
                    out var milliseconds))
                return MiniMaxRemainingMilliseconds(milliseconds);
            return DomesticQuotaService.ParseResetAt(text);
        }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var numeric))
            return MiniMaxRemainingMilliseconds(numeric);
        return null;
    }

    static DateTimeOffset? MiniMaxRemainingMilliseconds(double milliseconds)
    {
        // MiniMax's remains_time and weekly_remains_time are countdowns in
        // milliseconds. Treating them as seconds produced multi-day 5H resets;
        // larger weekly values then failed to parse and left stale cache dates.
        var maxWindow = TimeSpan.FromDays(8).TotalMilliseconds;
        return double.IsFinite(milliseconds) && milliseconds >= 0 && milliseconds <= maxWindow
            ? DateTimeOffset.Now.AddMilliseconds(milliseconds) : null;
    }

    static string FindMiniMaxMembership(Dictionary<string, JsonElement> values)
    {
        foreach (var name in new[]
                 {
                     "combo_name", "plan_name", "plan_title", "current_subscribe_title",
                     "subscribe_title", "subscription_name", "title"
                 })
        {
            if (!values.TryGetValue(name, out var value)
                || value.ValueKind != JsonValueKind.String) continue;
            var text = value.GetString()?.Trim() ?? "";
            if (text.Length is > 0 and <= 48) return text;
        }
        return "TOKEN PLAN";
    }

    static double? MiniMaxPercent(Dictionary<string, JsonElement> values, string prefix)
    {
        foreach (var name in new[]
                 {
                     $"{prefix}_used_percent", $"{prefix}_usage_percent",
                     $"{prefix}_usagePercent", $"{prefix}_usedPercent"
                 })
            if (PercentNumber(values, name) is double used) return used;
        foreach (var name in new[]
                 {
                     $"{prefix}_remaining_percent", $"{prefix}_remains_percent",
                     $"{prefix}_remainingPercent", $"{prefix}_remainsPercent"
                 })
            if (PercentNumber(values, name) is double remaining) return 100 - remaining;

        var total = Number(values, $"{prefix}_total_count")
            ?? Number(values, $"{prefix}_total");
        var usedCount = Number(values, $"{prefix}_usage_count")
            ?? Number(values, $"{prefix}_used_count")
            ?? Number(values, $"{prefix}_used");
        var remainingCount = Number(values, $"{prefix}_remains_count")
            ?? Number(values, $"{prefix}_remaining_count")
            ?? Number(values, $"{prefix}_remaining");
        if (total is > 0)
        {
            if (usedCount.HasValue) return 100 * usedCount.Value / total.Value;
            if (remainingCount.HasValue) return 100 * (total.Value - remainingCount.Value) / total.Value;
        }
        return null;
    }

    static double? PercentageFromRemaining(JsonElement detail)
    {
        if (detail.ValueKind != JsonValueKind.Object) return null;
        var values = detail.EnumerateObject().ToDictionary(p => p.Name, p => p.Value,
            StringComparer.OrdinalIgnoreCase);
        var remaining = Number(values, "remaining");
        var limit = Number(values, "limit");
        return remaining.HasValue && limit is > 0
            ? 100.0 * Math.Max(0, limit.Value - remaining.Value) / limit.Value : null;
    }

    static double? FindUsage(JsonElement element, bool alibaba)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var values = element.EnumerateObject().ToDictionary(p => p.Name, p => p.Value,
                StringComparer.OrdinalIgnoreCase);
            if (alibaba && Number(values, "TotalValue") is double total && total > 0
                && (Number(values, "TotalSurplusValue") ?? Number(values, "SurplusValue")) is double remain)
                return 100.0 * Math.Max(0, total - remain) / total;
            foreach (var name in new[] { "usagePercent", "usedPercent", "usageRate", "usedRate", "ratio" })
                if (Number(values, name) is double direct)
                    return name.EndsWith("Rate", StringComparison.OrdinalIgnoreCase) || name == "ratio"
                        ? direct * 100 : direct;
            var totalAny = Number(values, "totalCredits") ?? Number(values, "total") ?? Number(values, "quota");
            var used = Number(values, "usedCredits") ?? Number(values, "used");
            var remaining = Number(values, "remainingCredits") ?? Number(values, "remaining");
            if (totalAny is > 0)
            {
                if (used.HasValue) return 100 * used.Value / totalAny.Value;
                if (remaining.HasValue) return 100 * (totalAny.Value - remaining.Value) / totalAny.Value;
            }
            foreach (var child in values.Values)
                if (FindUsage(child, alibaba) is double found) return found;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                if (FindUsage(child, alibaba) is double found) return found;
        }
        return null;
    }

    static double? Number(Dictionary<string, JsonElement> values, string name)
    {
        if (!values.TryGetValue(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number) return value.GetDouble();
        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }

    static double? PercentNumber(Dictionary<string, JsonElement> values, string name)
    {
        if (!values.TryGetValue(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number) return value.GetDouble();
        if (value.ValueKind != JsonValueKind.String) return null;
        var text = (value.GetString() ?? "").Trim().TrimEnd('%');
        return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture, out var parsed)
            ? parsed : null;
    }
}


internal static class QuotaDiagnostics
{
    internal static long EventCount;
    // Browser/parse exceptions can contain response fragments. Count events only;
    // interactive failures remain visible in the authorization window.
    internal static void Log(string _) => Interlocked.Increment(ref EventCount);
}
