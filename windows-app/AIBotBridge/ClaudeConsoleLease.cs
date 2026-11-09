using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AIBotBridge;

// Discovery and sending run on different threads/processes. Serializing the
// attach/detach interval prevents our own read-only probe becoming an apparent
// unrelated console owner. This lock grants no access to the terminal.
internal sealed class ClaudeConsoleLease:IDisposable
{
    private readonly Mutex _mutex;
    private ClaudeConsoleLease(Mutex mutex)=>_mutex=mutex;
    internal static ClaudeConsoleLease Acquire(string scope,int milliseconds=5000){
        if(scope.Length!=64||!scope.All(Uri.IsHexDigit))throw new IOException("Native console lock identity unavailable");
        var mutex=new Mutex(false,@"Local\AIBot.ClaudeConsole."+BridgeElevation.Owner+"."+scope);
        try{
            // Ordinary and elevated instances of the same user must share the
            // lock. Keep its user ACL; permit medium-integrity synchronization.
            if(BridgeElevation.IsAdministrator){
                if(!ConvertStringSecurityDescriptorToSecurityDescriptor("S:(ML;;NW;;;ME)",1,out var descriptor,out _))throw new Win32Exception();
                try{if(!SetKernelObjectSecurity(mutex.SafeWaitHandle.DangerousGetHandle(),0x10,descriptor))throw new Win32Exception(Marshal.GetLastWin32Error());}
                finally{LocalFree(descriptor);}
            }
            try{if(!mutex.WaitOne(milliseconds))throw new IOException("Native console probe busy");}
            catch(AbandonedMutexException){ /* The previous worker has detached by exiting. */ }
            return new(mutex);
        }catch{mutex.Dispose();throw;}
    }
    public void Dispose(){try{_mutex.ReleaseMutex();}finally{_mutex.Dispose();}}
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string text,uint revision,out nint descriptor,out uint size);
    [DllImport("advapi32.dll",SetLastError=true)]private static extern bool SetKernelObjectSecurity(nint handle,uint information,nint descriptor);
    [DllImport("kernel32.dll")]private static extern nint LocalFree(nint memory);
}
