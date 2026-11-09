using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace AIBotBridge;

// The product itself is elevated. There is no separate privileged broker.
internal static class BridgeElevation
{
    internal static string Owner=>WindowsIdentity.GetCurrent().User?.Value??throw new IOException("bridge_owner_unavailable");
    internal static bool IsAdministrator {get{using var user=WindowsIdentity.GetCurrent();return new WindowsPrincipal(user).IsInRole(WindowsBuiltInRole.Administrator);}}
    internal static string? ProcessSid(Process process){
        if(!OpenProcessToken(process.Handle,8,out var token))throw new Win32Exception(Marshal.GetLastWin32Error());
        using(token)using(var identity=new WindowsIdentity(token.DangerousGetHandle()))return identity.User?.Value;
    }
    internal static bool EnsureTray(){
        AppProfileGuard.EnsureDesktopProfile();
        string[] arguments=Environment.GetCommandLineArgs().Skip(1).ToArray();
        if(arguments.Length>=2&&arguments[0]=="--elevated-tray")arguments=arguments.Skip(2).ToArray();
        if(IsAdministrator){
            string path=BridgeInstallation.Configure();
            if(string.Equals(path,Environment.ProcessPath,StringComparison.OrdinalIgnoreCase)){
                if(Environment.GetEnvironmentVariable("AIBOT_HTTP_PORT") is null&&BridgeInstallation.Read(path,Owner)?.HttpPort is {} port)
                    Environment.SetEnvironmentVariable("AIBOT_HTTP_PORT",port.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return true;
            }
            if(arguments.Length==0)StartupRegistration.StartInstalled(path);
            else Start(path,arguments,false);
            return false;
        }
        // Only the fixed, protected product action can be launched without a
        // new prompt. No task arguments, commands or caller-selected paths.
        if(arguments.Length==0&&StartupRegistration.TryStartInstalled())return false;
        var elevatedArgs=new List<string>{"--elevated-tray",Owner};elevatedArgs.AddRange(arguments);
        Start(Environment.ProcessPath??throw new IOException("bridge_executable_unavailable"),elevatedArgs,true);
        return false;
    }
    internal static int Start(string path,IEnumerable<string> arguments,bool elevate,bool wait=false){
        var info=new ProcessStartInfo(path){UseShellExecute=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(path)};
        if(elevate)info.Verb="runas";
        foreach(string argument in arguments)info.ArgumentList.Add(argument);
        using var child=Process.Start(info)??throw new IOException("bridge_start_failed");
        if(wait){child.WaitForExit();return child.ExitCode;}return 0;
    }
    [DllImport("advapi32.dll",SetLastError=true)]private static extern bool OpenProcessToken(IntPtr process,uint rights,out SafeAccessTokenHandle token);
}
