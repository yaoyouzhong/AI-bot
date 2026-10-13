using System.Net;
using System.Runtime.InteropServices;

namespace AIBotBridge;

internal sealed record WindowsProxyConfiguration(string? PacUrl,string? StaticProxy,bool StaticEnabled);
internal sealed record PacRoute(bool Resolved,Uri? Proxy,string Label,int Error=0);
internal static class WindowsPacRoute
{
    internal static WindowsProxyConfiguration Read() {
        if(!WinHttpGetIEProxyConfigForCurrentUser(out var config))return new(null,null,false);
        try {
            string? pac=Marshal.PtrToStringUni(config.Pac),proxy=Marshal.PtrToStringUni(config.Proxy);
            // Proxy returned by WinHTTP reflects the enabled static setting. A
            // saved but disabled ingress is diagnostic-only, never a PAC port.
            using var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
            return new(pac,proxy??key?.GetValue("ProxyServer") as string,proxy is {Length:>0});
        }finally{Free(config.Pac);Free(config.Proxy);Free(config.Bypass);}
    }
    internal static Uri? ParseProxy(string? text,string targetScheme="https") {
        if(string.IsNullOrWhiteSpace(text))return null;
        var parts=text.Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
        foreach(string part in parts.OrderByDescending(p=>p.StartsWith(targetScheme+"=",StringComparison.OrdinalIgnoreCase))) {
            string value=part.StartsWith("https=",StringComparison.OrdinalIgnoreCase)?part[6..]:part.StartsWith("http=",StringComparison.OrdinalIgnoreCase)?part[5..]:part;
            if(value.Contains('='))continue;
            if(!value.Contains("://"))value="http://"+value;
            if(Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.Scheme is "http" or "https" or "socks5"&&string.IsNullOrEmpty(uri.UserInfo))return uri;
        }
        return null;
    }
    internal static Uri? EnvironmentProxy(Uri target) {
        // .NET's current-process route is shown as diagnostic evidence only;
        // it is not proof of the environment inherited by the Codex process.
        var proxy=HttpClient.DefaultProxy;
        if(proxy.IsBypassed(target))return null;
        var uri=proxy.GetProxy(target);return uri!=target&&uri is not null&&string.IsNullOrEmpty(uri.UserInfo)?uri:null;
    }
    internal static PacRoute Resolve(string pac,Uri target) {
        IntPtr session=WinHttpOpen("AI-bot runtime monitor",1,null,null,0);
        if(session==IntPtr.Zero)return new(false,null,"无法读取选路",Marshal.GetLastWin32Error());
        try {
            WinHttpSetTimeouts(session,3000,3000,3000,3000);
            var options=new AutoProxyOptions {Flags=2,Url=pac};
            bool ok=WinHttpGetProxyForUrl(session,target.AbsoluteUri,ref options,out var info);int error=ok?0:Marshal.GetLastWin32Error();
            try {
                if(!ok)return new(false,null,"选路失败",error);
                if(info.AccessType==1)return new(true,null,"直连");
                var proxy=ParseProxy(Marshal.PtrToStringUni(info.Proxy));
                return proxy is null?new(false,null,"选路格式不支持"):new(true,proxy,"代理");
            }finally{Free(info.Proxy);Free(info.Bypass);}
        }finally{WinHttpCloseHandle(session);}
    }
    private static void Free(IntPtr pointer){if(pointer!=IntPtr.Zero)GlobalFree(pointer);}
    [StructLayout(LayoutKind.Sequential)]private struct IeProxyConfig {internal int AutoDetect;internal IntPtr Pac,Proxy,Bypass;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct AutoProxyOptions {
        internal uint Flags,DetectFlags;[MarshalAs(UnmanagedType.LPWStr)]internal string? Url;internal IntPtr Reserved;internal uint ReservedFlags;internal int AutoLogon;
    }
    [StructLayout(LayoutKind.Sequential)]private struct ProxyInfo {internal uint AccessType;internal IntPtr Proxy,Bypass;}
    [DllImport("winhttp.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool WinHttpGetIEProxyConfigForCurrentUser(out IeProxyConfig value);
    [DllImport("winhttp.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern IntPtr WinHttpOpen(string agent,uint access,string? proxy,string? bypass,uint flags);
    [DllImport("winhttp.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool WinHttpSetTimeouts(IntPtr session,int resolve,int connect,int send,int receive);
    [DllImport("winhttp.dll",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool WinHttpGetProxyForUrl(IntPtr session,string url,ref AutoProxyOptions options,out ProxyInfo info);
    [DllImport("winhttp.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool WinHttpCloseHandle(IntPtr session);
    [DllImport("kernel32.dll")]private static extern IntPtr GlobalFree(IntPtr pointer);
}
