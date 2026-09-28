using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5InstallBootCheck
{
    internal static long Hello(JsonElement root, string mac, string version, long previous)
    {
        string? Text(string name) => root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
        long Number(string name) => root.TryGetProperty(name, out var p) && p.TryGetInt64(out var v) ? v : -1;
        long uptime = Number("uptimeMs"), age = Number("uiAgeMs");
        if (Text("type") != "tab5_hello" || Number("version") != 1 || Text("deviceId") != mac.Replace(":", "") || Text("firmware") != version)
            throw new IOException("启动设备身份或固件版本不符，请确认选择的是刚安装的 TAB5。");
        if (uptime <= previous || age < 0 || age > 2000 || Number("flushErrors") != 0 || Number("flushCount") <= 0)
            throw new IOException("设备界面尚未稳定运行，请稍后重新检查；持续失败时可恢复备份。");
        return uptime;
    }
    internal static void Diagnostic(JsonElement root, string version, string elfSha)
    {
        bool Text(string name, string expected) => root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String && p.GetString() == expected;
        bool Number(string name, int expected) => root.TryGetProperty(name, out var p) && p.TryGetInt32(out var v) && v == expected;
        if (!Text("type", "tab5_ota_diagnostic") || !Text("firmware", version) || !Text("partition", "ota_0") ||
            !Number("address", 0x20000) || !Text("elfSha256", elfSha))
            throw new IOException("启动分区或固件指纹不符合本次安装包。");
    }
}
