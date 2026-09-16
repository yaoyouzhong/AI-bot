using CredentialStore = AIBotBridge.MigratedWeather.CredentialStore;
using Settings = AIBotBridge.MigratedWeather.Settings;

namespace AIBotBridge.MigratedDomestic;

sealed partial class DomesticQuotaService
{
    internal readonly QuotaRefreshHealth RefreshHealth = new();
    private readonly HashSet<string> _apiBusy = new();
    internal static bool SupportsOfficialApi(string provider) => provider is "deepseek" or "minimax" or "kimi";
    private static string Target(string provider) => provider switch {
        "deepseek" => "AI-bot/DeepSeekApiKey", "kimi" => "AI-bot/KimiLocalUsageToken",
        "minimax" => MiniMaxCredentialTarget, _ => throw new ArgumentOutOfRangeException(nameof(provider)) };
    private static string ApiKey(string provider) => provider == "minimax" ? MiniMaxApiKey() : CredentialStore.Read(Target(provider)).Trim();
    internal bool HasOfficialApi(string provider) => SupportsOfficialApi(provider) && ApiKey(provider).Length > 0;
    internal void SaveOfficialApiKey(string provider, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        CredentialStore.Write(Target(provider), key.Trim());
        lock (_lock) _backoffUntil.Remove(provider);
    }
    internal static int KimiPort => int.TryParse(Settings.Get("kimi_usage_port"), out var port) && port is >=1024 and <=65535 ? port : 58627;
    internal async Task<(bool Success, string Message)> RefreshOfficialApi(string provider)
    {
        lock (_lock) {
            if (_backoffUntil.TryGetValue(provider,out var until) && until > DateTime.UtcNow) return (false,"接口限流退避中，请稍后重试。");
            if (!_apiBusy.Add(provider)) return (false,"正在查询，请稍候。");
        }
        try
        {
            var key = ApiKey(provider);
            if (key.Length == 0) return (false,"尚未配置接口凭据，请在桥接 App 中保存并测试。");
            (bool Success, string Message) result;
            if (provider == "deepseek")
            {
                var response = await DeepSeekBalanceApi.FetchAsync(key, Snapshot.DeepSeekCurrency == "USD" ? "USD" : "CNY");
                if (response.RateLimited) BackOff(provider);
                if (key != ApiKey(provider)) return (false,"凭据已更换，请重新查询。");
                if (response.Success) SetDeepSeek(response.Balance!.Value, response.Currency);
                result = (response.Success, response.Success ? $"DeepSeek 官方 API 已更新：{response.Currency} {response.Balance:0.00}。" : response.Error);
            }
            else if (provider == "kimi")
            {
                var value = await KimiUsageApi.FetchAsync(key, KimiPort);
                if (key != ApiKey(provider)) return (false,"凭据已更换，请重新查询。");
                SetKimi(value.WeeklyPercent!.Value,value.PrimaryPercent,null,value.WeeklyResetsAt,value.PrimaryResetsAt);
                result = (true,"Kimi Code 官方本地服务已更新周额度和五小时额度。");
            }
            else result = await RefreshMiniMaxCore();
            if (result.Success) RefreshHealth.Recover(provider);
            else RefreshHealth.Fail(provider,result.Message);
            return result;
        }
        catch (Exception)
        {
            var message = provider == "kimi" ? "Kimi 官方本地接口暂不可用，请检查 kimi web 服务、端口、访问令牌及登录状态；已保留上次结果。"
                : "额度 API 查询失败，请检查网络和接口凭据；已保留上次结果。";
            RefreshHealth.Fail(provider,message);
            return (false,message);
        }
        finally { lock (_lock) _apiBusy.Remove(provider); }
    }
    internal void ReportWebFailure(string provider, string message, bool explicitlyRequested = false)
    {
        if (DomesticProviderCatalog.All.FirstOrDefault(x=>x.Id==provider)?.CaptureSupported!=true || HasOfficialApi(provider)) return;
        var s = Snapshot;
        bool configured = provider switch { "qwen" => s.QwenFetchedAt.HasValue, "kimi" => s.KimiFetchedAt.HasValue,
            "minimax" => s.MiniMaxFetchedAt.HasValue, "deepseek" => s.DeepSeekFetchedAt.HasValue, "zhipu" => s.Zhipu != null, _ => false };
        if (configured || explicitlyRequested) RefreshHealth.Fail(provider,message);
    }
}
