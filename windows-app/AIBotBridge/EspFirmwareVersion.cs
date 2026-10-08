namespace AIBotBridge;

internal static class EspFirmwareVersion
{
    internal const string Legacy = "版本未知（旧固件未上报）";
    internal const string Invalid = "固件版本上报无效";
    internal static string Read(string? value) => value is null ? Legacy :
        value.Length is > 0 and <= 32 && value.Split('.').Length == 3 &&
        value.All(c => char.IsAsciiDigit(c) || c == '.') && Version.TryParse(value, out var version) &&
        version.ToString() == value ? value : Invalid;
}
