using System.Security.Principal;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AIBotBridge;

internal static class BridgeLifetime
{
    private static string SignalName => @"Local\AIBotBridge.Exit." + WindowsIdentity.GetCurrent().User!.Value;
    internal static EventWaitHandle Listen(){
        var signal=new EventWaitHandle(false,EventResetMode.AutoReset,SignalName);
        // A same-user ordinary launcher/uninstaller may request a graceful
        // exit from the elevated tray. This event carries no commands/data;
        // keep its existing user ACL and lower only the mandatory label.
        if(BridgeElevation.IsAdministrator){
            if(!ConvertStringSecurityDescriptorToSecurityDescriptor("S:(ML;;NW;;;ME)",1,out var descriptor,out _)){signal.Dispose();throw new Win32Exception();}
            try{if(!SetKernelObjectSecurity(signal.SafeWaitHandle.DangerousGetHandle(),0x10,descriptor)){signal.Dispose();throw new Win32Exception(Marshal.GetLastWin32Error());}}
            finally{LocalFree(descriptor);}
        }
        return signal;
    }
    internal static void RequestExit()
    {
        if(!EventWaitHandle.TryOpenExisting(SignalName,out var signal))
        {Console.WriteLine("BRIDGE_NOT_RUNNING");return;}
        using(signal)signal.Set();
        Console.WriteLine("BRIDGE_EXIT_REQUESTED");
    }
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string text,uint revision,out nint descriptor,out uint size);
    [DllImport("advapi32.dll",SetLastError=true)]private static extern bool SetKernelObjectSecurity(nint handle,uint information,nint descriptor);
    [DllImport("kernel32.dll")]private static extern nint LocalFree(nint memory);
}
