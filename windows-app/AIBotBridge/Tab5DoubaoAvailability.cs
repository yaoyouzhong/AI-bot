using System.Diagnostics;
using System.Security;
using Microsoft.Win32;

namespace AIBotBridge;

// Failure-path observation only. Never launch/install software, switch the
// default IME or alter registration. Incomplete evidence must not mean absent.
internal static class Tab5DoubaoAvailability
{
    internal const string SwitchInput="未能切换到豆包，请在电脑语音输入框切换后重试";
    internal static bool IsInputMethodProduct(string name)=>
        name.Contains("豆包",StringComparison.OrdinalIgnoreCase)&&name.Contains("输入法",StringComparison.Ordinal)||
        name.Replace(" ","").Contains("DoubaoIME",StringComparison.OrdinalIgnoreCase)||
        name.Replace(" ","").Contains("DoubaoInputMethod",StringComparison.OrdinalIgnoreCase);
    internal static string Message(bool? installed,bool? running)=>running==true
        ?"豆包输入法服务暂不可用，请在电脑重启豆包后重试"
        :installed==true&&running==false?"豆包输入法已安装但未运行，请在电脑启动后重试"
        :installed==false&&running==false?"电脑未检测到豆包输入法，请先安装并启动"
        :"无法确认豆包输入法状态，请在电脑检查安装与运行";
    internal static string UnavailableMessage() {var state=Observe();return Message(state.Installed,state.Running);}
    internal static (bool? Installed,bool? Running) Observe() {
        bool found=false,complete=true;
        foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})
        foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64}) {
            try {
                using var root=RegistryKey.OpenBaseKey(hive,view);
                using(var uninstall=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")) {
                    if(uninstall is not null)foreach(string name in uninstall.GetSubKeyNames()) {
                        using var entry=uninstall.OpenSubKey(name);
                        found|=IsInputMethodProduct(entry?.GetValue("DisplayName") as string??"");
                    }
                }
                using var tips=root.OpenSubKey(@"SOFTWARE\Microsoft\CTF\TIP");
                if(tips is not null)foreach(string tip in tips.GetSubKeyNames()) {
                    using var languages=tips.OpenSubKey(tip+@"\LanguageProfile");
                    if(languages is null)continue;
                    foreach(string language in languages.GetSubKeyNames()) {
                        using var profiles=languages.OpenSubKey(language);
                        if(profiles is null)continue;
                        foreach(string id in profiles.GetSubKeyNames()) {
                            using var profile=profiles.OpenSubKey(id);
                            found|=Tab5InputMethod.IsDoubao(profile?.GetValue("Description") as string??"");
                        }
                    }
                }
            }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or SecurityException){complete=false;}
        }
        bool? running=null;
        try {
            var processes=Process.GetProcessesByName("ImeService");
            try{running=processes.Length>0;}finally{foreach(var process in processes)process.Dispose();}
        }catch(Exception ex) when(ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException){ }
        return (found?true:complete?false:null,running);
    }
    internal static void SelfTest() {
        Tab5InputMethod.SelfTest();
        if(!new Tab5VoiceSettings().Enabled||System.Text.Json.JsonSerializer.Deserialize<Tab5VoiceSettings>("{\"Enabled\":false}")!.Enabled)
            throw new Exception("Voice defaults or legacy settings deserialization changed");
        void Check(bool value){if(!value)throw new InvalidOperationException("Doubao availability classification failed");}
        Check(IsInputMethodProduct("豆包输入法")&&IsInputMethodProduct("Doubao IME")&&!IsInputMethodProduct("豆包")&&!IsInputMethodProduct("Doubao"));
        Check(Message(false,false).Contains("请先安装"));
        Check(Message(true,false).Contains("已安装但未运行"));
        Check(Message(true,true).Contains("服务暂不可用"));
        Check(!Message(null,false).Contains("未检测到")&&!Message(false,null).Contains("未检测到"));
        Check(Message(false,true).Contains("服务暂不可用"));
        Console.WriteLine("DOUBAO_AVAILABILITY_OK absent installed_stopped running_unavailable unknown non_IME_product");
    }
}
