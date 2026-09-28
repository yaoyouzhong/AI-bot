using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace AIBotBridge;

internal static class Tab5InputMethod
{
    internal static bool IsDoubao(string name)=>name.Contains("豆包",StringComparison.OrdinalIgnoreCase)||name.Contains("Doubao",StringComparison.OrdinalIgnoreCase);
    internal static string CurrentName() {
        object? service=null;
        try {
            service=Activator.CreateInstance(Type.GetTypeFromCLSID(new("33c53a50-f456-4884-b049-85fd643ecfed"))!);
            var manager=(IProfileManager)service!;
            var category=new Guid("34745c63-b2f0-4784-8b67-5e12c8701a31");
            if(manager.GetActiveProfile(ref category,out var profile)==0&&profile.Type==1) {
                string path=$@"SOFTWARE\Microsoft\CTF\TIP\{profile.ClassId:B}\LanguageProfile\0x{profile.Language:x8}\{profile.ProfileId:B}";
                using var user=Registry.CurrentUser.OpenSubKey(path);
                using var machine=Registry.LocalMachine.OpenSubKey(path);
                if((user?.GetValue("Description")??machine?.GetValue("Description")) is string name&&!string.IsNullOrWhiteSpace(name))return name;
            }
        }catch(Exception ex) when(ex is COMException or InvalidCastException or UnauthorizedAccessException or System.Security.SecurityException) { }
        finally{if(service is not null&&Marshal.IsComObject(service))Marshal.ReleaseComObject(service);}
        return InputLanguage.CurrentInputLanguage.LayoutName;
    }
    // Layout and vtable order match Windows SDK msctf.h. Only GetActiveProfile
    // is invoked; the preceding slots are reserved, never called.
    [StructLayout(LayoutKind.Sequential)]
    private struct Profile {
        public uint Type;public ushort Language;public Guid ClassId,ProfileId,Category;
        public IntPtr Substitute;public uint Capabilities;public IntPtr Layout;public uint Flags;
    }
    [ComImport,Guid("71c6e74c-0f28-11d8-a82a-00065b84435c"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IProfileManager {
        void ReservedActivate();void ReservedDeactivate();void ReservedGet();void ReservedEnum();
        void ReservedRelease();void ReservedRegister();void ReservedUnregister();
        [PreserveSig]int GetActiveProfile(ref Guid category,out Profile profile);
    }
}
