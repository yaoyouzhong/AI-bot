using AIBot.LoginMethod;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;

namespace AIBotBridge;

internal static class WindowsLoginMethod
{
    internal static bool Installed {get{using var identity=WindowsIdentity.GetCurrent();string? owner=identity.User?.Value;return owner is not null&&HelperContract.Manifest(owner) is not null;}}
    internal static async Task<bool> ConfigureAsync(bool install)
    {
        if (WindowsSessionLock.Read()!=false) return false;
        string path=Path.Combine(AppContext.BaseDirectory,"LoginHelper","AIBotLoginHelper.exe");
        var start=new ProcessStartInfo(path){UseShellExecute=true,Verb="runas",WorkingDirectory=Path.GetDirectoryName(path)!,WindowStyle=ProcessWindowStyle.Hidden};
        start.ArgumentList.Add(install?"--install":"--uninstall");start.ArgumentList.Add(Environment.ProcessId.ToString());
        using var process=Process.Start(start)??throw new IOException("辅助组件未能启动。");
        await process.WaitForExitAsync();return process.ExitCode==0&&Installed==install;
    }

    internal static bool Prepare(LoginMethodKind method)
    {
        if(method==LoginMethodKind.Manual)return true;
        if(method is not (LoginMethodKind.Pin or LoginMethodKind.Password)||WindowsSessionLock.Read()!=true||!Installed)return false;
        string nonce=Guid.NewGuid().ToString("N"),target=HelperContract.Target(method);
        int session=Process.GetCurrentProcess().SessionId;long issued=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        try {
            dynamic service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;
            service.Connect();dynamic folder=service.GetFolder("\\");dynamic task=folder.GetTask(HelperContract.TaskName);
            dynamic definition=task.Definition;
            if((string)definition.Principal.UserId is not ("SYSTEM" or "S-1-5-18")||definition.Principal.LogonType!=5||definition.Actions.Count!=1||
                !string.Equals((string)definition.Actions.Item(1).Path,Path.Combine(HelperContract.InstallPath,"AIBotLoginHelper.exe"),StringComparison.OrdinalIgnoreCase)||
                (string)definition.Actions.Item(1).Arguments!=HelperContract.Arguments)return false;
            task.Run(new string[]{nonce,session.ToString(),target,issued.ToString(System.Globalization.CultureInfo.InvariantCulture)});
            var timer=Stopwatch.StartNew();
            string result=Path.Combine(HelperContract.InstallPath,"result.json");
            while(timer.ElapsedMilliseconds<4700&&HelperContract.Fresh(issued,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())) {
                if(WindowsSessionLock.Read()!=true)return false;
                if(File.Exists(result)) {
                    var info=new FileInfo(result);
                    if(info.Length<=2048&&HelperContract.Trusted(info)) {
                        var reply=JsonSerializer.Deserialize<HelperReply>(File.ReadAllText(result));
                        if(reply?.Nonce==nonce)return HelperContract.Accept(reply,nonce,session,target,issued,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    }
                }
                Thread.Sleep(40);
            }
            return false;
        } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or COMException or JsonException or System.ComponentModel.Win32Exception or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) {return false;}
    }
}
