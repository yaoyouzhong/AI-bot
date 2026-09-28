namespace AIBotBridge;

internal sealed record DeviceDataDemand(HashSet<string> Sources,HashSet<string> Providers)
{
    internal static DeviceDataDemand From(DeviceRegistry registry,DisplayPolicy legacy,string domesticProvider) {
        var sources=registry.Devices.Where(d=>d.Enabled).SelectMany(d=>d.Sources).ToHashSet(StringComparer.Ordinal);
        var providers=registry.Devices.Where(d=>d.Enabled&&d.Sources.Contains("quotas")).SelectMany(d=>d.Providers).ToHashSet(StringComparer.Ordinal);
        if(registry.Devices.Any(d=>d.Enabled&&d.Kind==HardwareKind.Esp8266&&d.Sources.Contains("quotas")))providers.UnionWith(QuotaMonitoringPolicy.Selected(legacy,domesticProvider));
        if(registry.DesktopQuotaHistory){sources.Add("quotas");providers.UnionWith(QuotaMonitoringPolicy.Selected(legacy with {SelectedMode="auto",CycleEnabled=true},domesticProvider));}
        return new(sources,providers);
    }
}
