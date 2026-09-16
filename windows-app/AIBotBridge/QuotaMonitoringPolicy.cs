namespace AIBotBridge;
internal static class QuotaMonitoringPolicy
{
    internal static HashSet<string> Monitored(DisplayPolicy policy, string domesticProvider, Func<string,bool> configured)
    {
        var selected=Selected(policy,domesticProvider);
        selected.RemoveWhere(p=>!configured(p));
        return selected;
    }
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
