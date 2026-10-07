using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace AIBotBridge;

// A packaged launcher can virtualize file access even when KnownFolder and
// GetCurrentPackageFullName report an ordinary desktop process. Inspect handles.
internal static class AppProfileGuard
{
    private static readonly Lazy<bool> Validated = new(() => { Validate(); return true; });
    internal static void EnsureDesktopProfile() => _ = Validated.Value;
    internal const string Recovery = "数据目录被启动环境重定向，已停止启动以保护配对和额度历史。请从 Windows 开始菜单或资源管理器启动 AI-bot；无需重新配对。";

    internal static bool IsPackageData(string path) {
        string normalized=path.Replace('/','\\');
        return normalized.Contains("\\Packages\\",StringComparison.OrdinalIgnoreCase) &&
            new[]{"\\LocalCache\\","\\LocalState\\","\\RoamingState\\"}.Any(part=>normalized.Contains(part,StringComparison.OrdinalIgnoreCase));
    }
    internal static void Validate() {
        foreach(var folder in new[]{Environment.SpecialFolder.ApplicationData,Environment.SpecialFolder.LocalApplicationData}) {
            string root=Environment.GetFolderPath(folder);
            if(string.IsNullOrWhiteSpace(root)||IsPackageData(root))throw new IOException(Recovery);
            string? actualRoot=PhysicalPath(root);
            if(actualRoot is null)throw new IOException("无法核验数据目录，已停止启动。");
            if(IsPackageData(actualRoot))throw new IOException(Recovery);
            // No creation, reads of file contents, migration or credential changes.
            foreach(string path in new[]{Path.Combine(root,"AI-bot"),Path.Combine(root,"AI-bot","settings.json"),
                Path.Combine(root,"AI-bot","tab5-pairing.dat"),Path.Combine(root,"AI-bot","codex-quota-history.json")}) {
                string? actual=PhysicalPath(path);
                if(actual is not null&&IsPackageData(actual))throw new IOException(Recovery);
            }
        }
    }
    internal static string? PhysicalPath(string path) {
        using var handle=CreateFile(path,0,7,0,3,0x02000000,0);
        if(handle.IsInvalid) {
            int error=Marshal.GetLastWin32Error();
            if(error is 2 or 3)return null;
            throw new IOException("无法核验数据目录，已停止启动。",new Win32Exception(error));
        }
        var buffer=new StringBuilder(512);
        uint length=GetFinalPathNameByHandle(handle,buffer,(uint)buffer.Capacity,0);
        if(length>=buffer.Capacity) {
            buffer.EnsureCapacity(checked((int)length+1));
            length=GetFinalPathNameByHandle(handle,buffer,(uint)buffer.Capacity,0);
        }
        if(length==0||length>=buffer.Capacity)throw new IOException("无法核验实际数据目录，已停止启动。",new Win32Exception(Marshal.GetLastWin32Error()));
        return buffer.ToString();
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern SafeFileHandle CreateFile(string path,uint access,uint share,nint security,uint disposition,uint flags,nint template);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle,StringBuilder path,uint length,uint flags);
}
