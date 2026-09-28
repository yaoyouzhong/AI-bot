namespace AIBotBridge;
internal static class QuotaMonitoringPolicy
{
    internal static HashSet<string> Monitored(DisplayPolicy policy, string domesticProvider, Func<string,bool> configured, bool tab5Paired=false)
    {
        var selected=Selected(policy,domesticProvider);
        // TAB5 keeps the user's chosen providers fresh even when the small screen
        // is temporarily fixed on another page. Authorization alone is not selection.
        if(tab5Paired) selected.UnionWith(Selected(policy with { SelectedMode="auto", CycleEnabled=true }, domesticProvider));
        selected.RemoveWhere(p=>!configured(p));
        return selected;
    }
    internal static string[] OrderWeb(IEnumerable<string> providers, bool tab5Paired) => tab5Paired
        // The overview always shows these two wallets. DeepSeek also has its independent API refresh.
        ? providers.OrderBy(p=>p=="zhipu"?0:p=="deepseek"?1:2).ToArray()
        : providers.ToArray();
    internal static HashSet<string> Selected(DisplayPolicy policy, string domesticProvider)
    {
        var modes = policy.SelectedMode == "auto" && policy.CycleEnabled ? policy.Pages : new[]{policy.SelectedMode};
        var providers = new HashSet<string>(StringComparer.Ordinal);
        foreach(var raw in modes) {
            var mode = DisplayModes.Normalize(raw);
            var provider = mode == "domestic" ? domesticProvider : mode.StartsWith("domestic_",StringComparison.Ordinal) ? mode[9..] : "";
            if(provider == "alibaba") provider = "qwen";
            if(MigratedDomestic.DomesticProviderCatalog.All.Any(p=>p.Id==provider && p.CaptureSupported)) providers.Add(provider);
        }
        return providers;
    }
}
