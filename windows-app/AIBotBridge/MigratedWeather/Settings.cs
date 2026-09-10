namespace AIBotBridge.MigratedWeather;

// Adapter to new-repository settings; never writes AIClockBridge state.
internal static class Settings
{
    private static readonly object Sync = new();
    internal static string Get(string key) { lock (Sync) return BridgeSettings.Load().Get(key); }
    internal static void Set(string key, string value)
    {
        lock (Sync)
            if (!BridgeSettings.Load().SaveEditable(new Dictionary<string, string> { [key] = value }, out var error))
                throw new IOException(error);
    }
}
