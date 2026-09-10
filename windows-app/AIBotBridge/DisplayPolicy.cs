namespace AIBotBridge;

internal sealed record DisplayPolicy(string SelectedMode, bool CycleEnabled, int IntervalSeconds, string[] Pages, long CycleStartedAt = 0);

internal static class DisplayModes
{
    internal static readonly (string Label, string Mode)[] Pages =
    [("Claude", "claude"), ("Codex", "codex"), ("Claude + Codex 额度", "dual"),
     ("阿里云", "domestic_alibaba"), ("Kimi", "domestic_kimi"), ("MiniMax", "domestic_minimax"),
     ("DeepSeek", "domestic_deepseek"), ("智谱 GLM", "domestic_zhipu"), ("系统监控", "system"), ("音乐播放", "music"),
     ("股票行情", "stocks"), ("天气时钟", "weather"), ("桌宠", "pet")];
    internal static bool IsValid(string mode) => mode is "auto" or "screensaver" or "quotas" or "domestic" or "activity" || Pages.Any(page => page.Mode == mode);
    internal static string Normalize(string mode) => mode switch
    {
        "stock" => "stocks", "net" => "system", "domestic:alibaba" or "domestic:qwen" => "domestic_alibaba",
        "domestic:kimi" => "domestic_kimi", "domestic:minimax" => "domestic_minimax",
        "domestic:deepseek" => "domestic_deepseek", "domestic:zhipu" or "glm" => "domestic_zhipu", _ => mode
    };
    internal static DisplayPolicy Load(BridgeSettings settings, string? selected = null)
    {
        var pages = settings.GetList("display_cycle_pages", "codex,claude,weather,stock")
            .Select(Normalize).Where(mode => Pages.Any(page => page.Mode == mode)).Distinct().ToArray();
        if (pages.Length == 0) pages = ["codex", "claude", "weather", "stocks"];
        var interval = int.TryParse(settings.Get("display_cycle_interval_seconds"), out var value) &&
            value is 10 or 15 or 30 or 60 ? value : 15;
        var mode = selected ?? settings.Get("display_mode", "auto");
        return new(IsValid(mode) ? mode : "auto", settings.Get("display_cycle_enabled", "1") == "1", interval, pages);
    }
    internal static string Resolve(StatusSnapshot status, string selected)
    {
        if (selected == "screensaver") return selected;
        if (status.DomesticActivity?.NeedsInput==true) return DomesticPage(status.DomesticActivity.ActiveProvider);
        if (status.Claude.NeedsInput && status.Codex.NeedsInput && status.FollowApp is "claude" or "codex") return status.FollowApp;
        if (status.Codex.NeedsInput) return "codex";
        if (status.Claude.NeedsInput) return "claude";
        if (status.Codex.CompletionActive) return "codex";
        if (selected != "auto") return selected;
        var policy = status.DisplayPolicy;
        if (policy?.CycleEnabled == true && policy.Pages.Length > 0)
            return policy.Pages[(int)(Math.Max(0, status.EpochUtc - policy.CycleStartedAt) / Math.Max(1, policy.IntervalSeconds) % policy.Pages.Length)];
        if (status.Music?.Playing == true) return "music";
        if (status.DomesticActivity?.State == "working") return DomesticPage(status.DomesticActivity.ActiveProvider);
        if(status.FollowApp is "claude" or "codex")return status.FollowApp;
        bool codexWorking=status.Codex.State=="working",claudeWorking=status.Claude.State=="working";
        if(codexWorking!=claudeWorking)return codexWorking?"codex":"claude";
        int interval=codexWorking?2:6;
        return Math.Max(0,status.EpochUtc-(policy?.CycleStartedAt??0))/interval%2==0?"claude":"codex";
    }
    internal static string DomesticPage(string provider) => provider is "alibaba" or "kimi" or "minimax" or "deepseek" or "zhipu" ? "domestic_"+provider : "activity";
}
