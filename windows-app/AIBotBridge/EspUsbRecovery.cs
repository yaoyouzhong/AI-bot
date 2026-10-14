namespace AIBotBridge;

internal static class EspUsbRecovery
{
    internal static bool ValidId(string? value)=>value is {Length:20}&&value.StartsWith("esp8266-",StringComparison.Ordinal)&&value[8..].All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f')&&value!="esp8266-000000000000"&&value!="esp8266-ffffffffffff";
    internal static bool Matches(string? expected,string? actual)=>ValidId(expected)&&string.Equals(expected,actual,StringComparison.Ordinal);

    // A changed USB path is only a probe candidate. No config/data is sent
    // until its USB pong matches the ID learned on the trusted device path.
    internal static IReadOnlyList<FlashUsbDevice> Candidates(string? identity,string? deviceId,IReadOnlyList<FlashUsbDevice> devices,string? reservedPort) {
        if(identity is null||!ValidId(deviceId))return [];
        string[] parts=identity.Split('\\');if(parts.Length!=3)return [];
        return devices.Where(d=>d.Identity.StartsWith(parts[0]+"\\"+parts[1]+"\\",StringComparison.OrdinalIgnoreCase)&&
            !d.Identity.Equals(identity,StringComparison.OrdinalIgnoreCase)&&
            !d.Identity.Contains("VID_303A",StringComparison.OrdinalIgnoreCase)&&!d.Port.Equals(reservedPort,StringComparison.OrdinalIgnoreCase)).ToArray();
    }
}
