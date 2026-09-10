param([Parameter(Mandatory=$true)][string]$BridgeDll)
$ErrorActionPreference = 'Stop'
$BridgeDll = (Resolve-Path -LiteralPath $BridgeDll).Path
$dotnetExecutable = (Get-Command dotnet -ErrorAction Stop).Source
$testLog = Join-Path (Split-Path $BridgeDll) ('public-self-test-' + [Guid]::NewGuid().ToString('N') + '.log')
# Assign the desktop at process creation, before CLR/STA creates any windows.
# No SwitchDesktop call: the input desktop and resident bridge are untouched.
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
public static class AIBotHiddenTestProcess {
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    struct StartupInfo {
        public int Size; public string Reserved, Desktop, Title;
        public uint X,Y,Width,Height,CharsX,CharsY,Fill,Flags;
        public ushort Show,ReservedSize; public IntPtr ReservedData,Input,Output,Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct ProcessInfo {public IntPtr Process,Thread;public uint ProcessId,ThreadId;}
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern IntPtr CreateDesktopW(string name,IntPtr device,IntPtr mode,uint flags,uint access,IntPtr security);
    [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetHandleInformation(IntPtr handle,uint mask,uint flags);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern bool CreateProcessW(string app,StringBuilder command,IntPtr processAttrs,IntPtr threadAttrs,
        bool inherit,uint flags,IntPtr environment,string directory,ref StartupInfo startup,out ProcessInfo process);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetExitCodeProcess(IntPtr process,out uint code);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    public static int Run(string dotnet,string dll,string log) {
        string name="AIBot-self-test-"+Guid.NewGuid().ToString("N");
        IntPtr desktop=CreateDesktopW(name,IntPtr.Zero,IntPtr.Zero,0,0x10000000,IntPtr.Zero);
        if(desktop==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
        ProcessInfo process=new ProcessInfo();
        try {
            using(var output=new FileStream(log,FileMode.CreateNew,FileAccess.Write,FileShare.ReadWrite)) {
                IntPtr handle=output.SafeFileHandle.DangerousGetHandle();
                if(!SetHandleInformation(handle,1,1))throw new Win32Exception(Marshal.GetLastWin32Error());
                var startup=new StartupInfo {Size=Marshal.SizeOf(typeof(StartupInfo)),Desktop=name,
                    Flags=0x101,Show=0,Output=handle,Error=handle};
                var command=new StringBuilder("\""+dotnet+"\" \""+dll+"\" --self-test-public");
                if(!CreateProcessW(dotnet,command,IntPtr.Zero,IntPtr.Zero,true,0x08000000,IntPtr.Zero,
                    Environment.CurrentDirectory,ref startup,out process))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if(WaitForSingleObject(process.Process,0xffffffff)!=0)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                uint code;
                if(!GetExitCodeProcess(process.Process,out code))throw new Win32Exception(Marshal.GetLastWin32Error());
                return unchecked((int)code);
            }
        } finally {
            if(process.Thread!=IntPtr.Zero)CloseHandle(process.Thread);
            if(process.Process!=IntPtr.Zero)CloseHandle(process.Process);
            CloseDesktop(desktop);
        }
    }
}
'@
$testExitCode = [AIBotHiddenTestProcess]::Run($dotnetExecutable, $BridgeDll, $testLog)
Get-Content -LiteralPath $testLog -Encoding UTF8 | Write-Output
if ($testExitCode -ne 0) { throw "Public self-test failed: exit=$testExitCode log=$testLog" }
Write-Output 'PUBLIC_SELF_TEST_DESKTOP_OK separate invisible desktop; input desktop unchanged'
