using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace AIBotBridge;

// Changes only on an explicit menu or command-line request.
internal static class StartupRegistration
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AI-bot";
    private static string UserSid => WindowsIdentity.GetCurrent().User!.Value;
    internal static string TaskName => "AI-bot-Logon-" + UserSid;
    private static string Executable => Path.Combine(AppContext.BaseDirectory, "AIBotBridge.exe");
    private static dynamic Connect()
    {
        dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", true)!)!;
        service.Connect();
        return service;
    }
    private static dynamic? FindTask(dynamic folder)
    {
        try { return folder.GetTask(TaskName); }
        catch (COMException ex) when (ex.HResult == unchecked((int)0x80070002)) { return null; }
    }
    internal static bool IsEnabled
    {
        get
        {
            try
            {
                dynamic service = Connect();
                dynamic task = FindTask(service.GetFolder(@"\"))!;
                if (task is not null && task!.Enabled && task!.Definition.Actions.Count == 1)
                {
                    dynamic action = task!.Definition.Actions.Item(1);
                    if (action.Type == 0 && string.Equals((string)action.Path, Executable,
                            StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty((string)action.Arguments))
                        return true;
                }
            }
            catch (Exception ex) when (ex is COMException or UnauthorizedAccessException) { }
            // Keep existing Run registrations controllable until explicitly migrated.
            try { using var key = Registry.CurrentUser.OpenSubKey(KeyPath); return key?.GetValue(ValueName) is string value && value == Command; }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { return false; }
        }
    }
    private static string Command => "\"" + Executable + "\"";
    internal static void SetEnabled(bool enabled)
    {
        if (enabled && !File.Exists(Executable))
            throw new IOException("找不到 AIBotBridge.exe，请先完成本地构建或安装。");
        dynamic service = Connect();
        dynamic folder = service.GetFolder(@"\");
        if (enabled)
        {
            dynamic definition = service.NewTask(0);
            definition.RegistrationInfo.Description = "AI-bot：当前用户登录后启动托盘桥接。";
            definition.Principal.UserId = UserSid;
            definition.Principal.LogonType = 3; // Interactive token, no password.
            definition.Principal.RunLevel = 0; // Least privilege.
            definition.Settings.Enabled = true;
            definition.Settings.DisallowStartIfOnBatteries = false;
            definition.Settings.StopIfGoingOnBatteries = false;
            definition.Settings.ExecutionTimeLimit = "PT0S";
            definition.Settings.MultipleInstances = 2; // IgnoreNew.
            definition.Settings.StartWhenAvailable = true;
            dynamic trigger = definition.Triggers.Create(9); // Current-user logon.
            trigger.UserId = UserSid;
            trigger.Enabled = true;
            trigger.Delay = "PT5S";
            dynamic action = definition.Actions.Create(0);
            action.Path = Executable;
            action.WorkingDirectory = AppContext.BaseDirectory;
            folder.RegisterTaskDefinition(TaskName, definition, 6, UserSid, null, 3, null);
        }
        else if (FindTask(folder) is not null) folder.DeleteTask(TaskName, 0);
        // Remove the queued Run entry only after task registration succeeds.
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, true);
        key?.DeleteValue(ValueName, false);
    }
}
