using Microsoft.Win32;

namespace AIBotBridge;

// Only the explicit menu click writes this application's own HKCU value.
internal static class StartupRegistration
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AI-bot";
    internal static bool IsEnabled
    {
        get
        {
            try { using var key = Registry.CurrentUser.OpenSubKey(KeyPath); return key?.GetValue(ValueName) is string value && value == Command; }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { return false; }
        }
    }
    private static string Command => "\"" + Path.Combine(AppContext.BaseDirectory, "AIBotBridge.exe") + "\"";
    internal static void SetEnabled(bool enabled)
    {
        if (enabled && !File.Exists(Path.Combine(AppContext.BaseDirectory, "AIBotBridge.exe")))
            throw new IOException("找不到 AIBotBridge.exe，请先完成本地构建或安装。");
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, true);
        if (enabled) key.SetValue(ValueName, Command, RegistryValueKind.String);
        else key.DeleteValue(ValueName, false);
    }
}
