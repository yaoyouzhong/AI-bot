using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace AIBotBridge;

internal static class Tab5InputMethod
{
    internal static bool Ensure(Func<bool> ownsFocus,Func<bool> isDoubao,Func<bool> activate)=>
        ownsFocus()&&(isDoubao()||activate())&&ownsFocus()&&isDoubao();
    internal static void SelfTest() {
        int calls=0;bool focus=true,current=false;
        bool Activate(){calls++;current=true;return true;}
        if(Ensure(()=>false,()=>current,Activate)||calls!=0)throw new Exception("IME changed without focus");
        if(!Ensure(()=>focus,()=>current,Activate)||calls!=1)throw new Exception("IME not activated");
        if(!Ensure(()=>focus,()=>current,Activate)||calls!=1)throw new Exception("Active IME activated twice");
        current=false;
        if(Ensure(()=>focus,()=>current,()=>true))throw new Exception("Unconfirmed activation accepted");
        if(Ensure(()=>focus,()=>current,()=>{focus=false;current=true;return true;}))throw new Exception("IME focus loss accepted");
        Console.WriteLine("DOUBAO_ACTIVATION_GUARD_OK focus active_once verification focus_loss");
    }
    // Activation is confined to the calling UI thread. Never enable a profile
    // persistently or use FORPROCESS/FORSESSION flags.
    internal static bool ActivateDoubao(Func<bool> ownsFocus) {
        if(!ownsFocus())return false;
        if(IsDoubao(CurrentName()))return true;
        object? service=null;
        var previous=InputLanguage.CurrentInputLanguage;
        bool activated=false;
        try {
            var candidates=new HashSet<(Guid ClassId,ushort Language,Guid ProfileId)>();
            foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine}) {
                using var tips=hive.OpenSubKey(@"SOFTWARE\Microsoft\CTF\TIP");
                if(tips is null)continue;
                foreach(var cls in tips.GetSubKeyNames()) {
                    if(!Guid.TryParse(cls,out var classId))continue;
                    using var languages=tips.OpenSubKey(cls+@"\LanguageProfile");
                    if(languages is null)continue;
                    foreach(var lang in languages.GetSubKeyNames()) {
                        if(!ushort.TryParse(lang.StartsWith("0x")?lang[2..]:lang,System.Globalization.NumberStyles.HexNumber,null,out var language))continue;
                        using var profiles=languages.OpenSubKey(lang);
                        if(profiles is null)continue;
                        foreach(var id in profiles.GetSubKeyNames()) {
                            using var profile=profiles.OpenSubKey(id);
                            if(Guid.TryParse(id,out var profileId)&&profile?.GetValue("Description") is string name&&IsDoubao(name))
                                candidates.Add((classId,language,profileId));
                        }
                    }
                }
            }
            if(candidates.Count!=1||!ownsFocus())return false;
            var selected=candidates.Single();
            service=Activator.CreateInstance(Type.GetTypeFromCLSID(new("33c53a50-f456-4884-b049-85fd643ecfed"))!);
            var manager=(IProfileManager)service!;
            // Change only this thread's language when needed, then activate the
            // already enabled TIP. A pending activation alone is not success.
            if(previous.Culture.LCID!=selected.Language) {
                var language=InputLanguage.InstalledInputLanguages.Cast<InputLanguage>().FirstOrDefault(x=>x.Culture.LCID==selected.Language);
                if(language is null||!ownsFocus())return false;
                InputLanguage.CurrentInputLanguage=language;
            }
            if(!ownsFocus())return false;
            int result=manager.ActivateProfile(1,selected.Language,ref selected.ClassId,ref selected.ProfileId,IntPtr.Zero,0);
            var category=new Guid("34745c63-b2f0-4784-8b67-5e12c8701a31");
            activated=result==0&&ownsFocus()&&manager.GetActiveProfile(ref category,out var active)==0&&
                active.Type==1&&active.ClassId==selected.ClassId&&active.ProfileId==selected.ProfileId&&active.Language==selected.Language;
            return activated;
        }catch(Exception ex) when(ex is COMException or InvalidCastException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or System.ComponentModel.Win32Exception){return false;}
        finally {
            if(!activated&&InputLanguage.CurrentInputLanguage.Handle!=previous.Handle)InputLanguage.CurrentInputLanguage=previous;
            if(service is not null&&Marshal.IsComObject(service))Marshal.ReleaseComObject(service);
        }
    }
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
    // Layout and vtable order match Windows SDK msctf.h.
    [StructLayout(LayoutKind.Sequential)]
    private struct Profile {
        public uint Type;public ushort Language;public Guid ClassId,ProfileId,Category;
        public IntPtr Substitute;public uint Capabilities;public IntPtr Layout;public uint Flags;
    }
    [ComImport,Guid("71c6e74c-0f28-11d8-a82a-00065b84435c"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IProfileManager {
        [PreserveSig]int ActivateProfile(uint type,ushort language,ref Guid classId,ref Guid profileId,IntPtr layout,uint flags);
        void ReservedDeactivate();void ReservedGet();void ReservedEnum();
        void ReservedRelease();void ReservedRegister();void ReservedUnregister();
        [PreserveSig]int GetActiveProfile(ref Guid category,out Profile profile);
    }
}
