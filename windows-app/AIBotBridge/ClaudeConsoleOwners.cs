using System.Diagnostics;

namespace AIBotBridge;

// A foreground CLI shares its console with the shell waiting for it to exit.
// Accept that verified ancestor chain, never unrelated console readers.
internal static class ClaudeConsoleOwners
{
    // Only medium/high interactive terminals owned by this user. Elevation
    // permits access downwards; it does not grant access to other users,
    // sessions, low-integrity clients, SYSTEM or unrelated console readers.
    internal static bool CanAccess(int observer,int target)=>target is >=0x2000 and <=0x3000&&observer>=target&&observer<=0x3000;
    internal sealed record Owner(int Pid,int Parent,long Started,string Image,string Sid,int Session,int Integrity);

    internal static bool Matches(IReadOnlyCollection<uint> owners,int target,int worker,Func<int,Owner> read,Func<string,bool> shell){
        if(owners.Count is <2 or >6||owners.Distinct().Count()!=owners.Count||
            !owners.Contains((uint)target)||!owners.Contains((uint)worker)||target==worker)return false;
        var child=read(target);var observer=read(worker);
        if(child.Pid!=target||observer.Pid!=worker||child.Sid!=observer.Sid||
            child.Session!=observer.Session||!CanAccess(observer.Integrity,child.Integrity))return false;
        var remaining=owners.Where(p=>p!=target&&p!=worker).ToHashSet();
        while(remaining.Count>0){
            if(child.Parent<=0||!remaining.Remove((uint)child.Parent))return false;
            var parent=read(child.Parent);
            if(parent.Pid!=child.Parent||parent.Started>child.Started||parent.Sid!=child.Sid||
                parent.Session!=child.Session||parent.Integrity!=child.Integrity||!shell(parent.Image))return false;
            child=parent;
        }
        return true;
    }

    internal static bool Matches(uint[] owners,int target)=>Matches(owners,target,Environment.ProcessId,Read,TrustedShell);

    private static Owner Read(int pid){
        using var process=Process.GetProcessById(pid);
        // Keep the queried process handle alive for all fields; an exited or
        // reused parent must not become proof of a waiting shell.
        _=process.Handle;
        var value=new Owner(pid,ClaudeNativeCode.Parent(process),process.StartTime.ToUniversalTime().ToFileTimeUtc(),
            process.MainModule?.FileName??"",BridgeElevation.ProcessSid(process)??"",process.SessionId,ClaudeNativeConsole.Integrity((uint)pid));
        if(process.HasExited||value.Sid.Length==0)throw new IOException("Native console owners changed");
        return value;
    }

    internal static bool TrustedShell(string image){
        string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        foreach(string folder in new[]{"System32","SysWOW64"})
            foreach(string relative in new[]{"cmd.exe",@"WindowsPowerShell\v1.0\powershell.exe"})
                if(string.Equals(image,Path.Combine(windows,folder,relative),StringComparison.OrdinalIgnoreCase))return true;
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"PowerShell");
        string? directory=Path.GetDirectoryName(image);
        if(!string.Equals(Path.GetFileName(image),"pwsh.exe",StringComparison.OrdinalIgnoreCase)||directory is null||
            !string.Equals(Path.GetDirectoryName(directory),root,StringComparison.OrdinalIgnoreCase))return false;
        string version=Path.GetFileName(directory);
        if(version.EndsWith("-preview",StringComparison.OrdinalIgnoreCase))version=version[..^8];
        return version.Length>0&&version.All(char.IsAsciiDigit);
    }
}
