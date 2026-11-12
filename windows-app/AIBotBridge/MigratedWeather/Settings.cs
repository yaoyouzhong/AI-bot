namespace AIBotBridge.MigratedWeather;

// Adapter to new-repository settings; never writes AIClockBridge state.
internal static class Settings
{
    private static readonly object Sync = new();
    internal static string Get(string key) { lock (Sync) return BridgeSettings.Load().Get(key); }
    internal static void Set(string key, string value)
        => SetMany(new Dictionary<string, string> { [key] = value });

    internal static void SetMany(Dictionary<string, string> values)
    {
        lock (Sync)
            if (!BridgeSettings.Load().SaveEditable(values, out var error))
                throw new IOException(error);
    }
}
