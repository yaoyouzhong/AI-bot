using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AIBotBridge;
internal static class WindowsSessionLock
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct Level1 {
        public uint SessionId;public int SessionState,SessionFlags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=33)]public string Station;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=21)]public string User;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=18)]public string Domain;
        public long Logon,Connect,Disconnect,LastInput,Current;
        public uint IncomingBytes,OutgoingBytes,IncomingFrames,OutgoingFrames,IncomingCompressed,OutgoingCompressed;
    }
    [StructLayout(LayoutKind.Sequential)]private struct InfoEx {public uint Level;public Level1 Data;}
    [DllImport("wtsapi32.dll",EntryPoint="WTSQuerySessionInformationW",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool Query(IntPtr server,int session,int infoClass,out IntPtr buffer,out int bytes);
    [DllImport("wtsapi32.dll")]private static extern void WTSFreeMemory(IntPtr buffer);
    internal static bool? Decode(int flags)=>flags switch{0=>true,1=>false,_=>null};
    internal static bool? Read() {
        using var process=Process.GetCurrentProcess();
        if(!Query(IntPtr.Zero,process.SessionId,25,out var buffer,out int bytes))return null;
        try {
            if(bytes<Marshal.SizeOf<InfoEx>())return null;
            var info=Marshal.PtrToStructure<InfoEx>(buffer);
            return info.Level==1&&info.Data.SessionId==process.SessionId?Decode(info.Data.SessionFlags):null;
        }finally{WTSFreeMemory(buffer);}
    }
}
