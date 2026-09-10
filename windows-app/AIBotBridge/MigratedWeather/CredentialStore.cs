#nullable disable
using System.Runtime.InteropServices;
using System.Text;

namespace AIBotBridge.MigratedWeather;

// Stores provider secrets in Windows Credential Manager. Only the stable
// target name is kept in source; credential values never enter settings.json.
static class CredentialStore
{
    const int Generic = 1;
    const int PersistLocalMachine = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NativeCredential
    {
        public int Flags;
        public int Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CredWrite(ref NativeCredential credential, int flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll")]
    static extern void CredFree(IntPtr buffer);

    public static void Write(string target, string secret)
    {
        if (AIBotBridge.AppPaths.IsPublicSelfTest) throw new InvalidOperationException("Credential writes forbidden in public self-test.");
        if (string.IsNullOrEmpty(secret)) return;
        var blob = Marshal.StringToCoTaskMemUni(secret);
        try
        {
            var credential = new NativeCredential
            {
                Type = Generic,
                TargetName = target,
                Comment = "AI-bot provider credential",
                CredentialBlobSize = Encoding.Unicode.GetByteCount(secret),
                CredentialBlob = blob,
                Persist = PersistLocalMachine,
                UserName = Environment.UserName,
            };
            if (!CredWrite(ref credential, 0))
                throw new InvalidOperationException($"Windows 凭据保存失败（{Marshal.GetLastWin32Error()}）");
        }
        finally
        {
            Marshal.ZeroFreeCoTaskMemUnicode(blob);
        }
    }

    public static string Read(string target)
    {
        if (AIBotBridge.AppPaths.IsPublicSelfTest) return "";
        if (!CredRead(target, Generic, 0, out var pointer))
        {
            // Read-only compatibility: never copy or overwrite the legacy credential.
            var legacy = target switch
            {
                "AI-bot/QWeatherApiKey" => "AIClockBridge/QWeatherApiKey",
                "AI-bot/MiniMaxTokenPlanKey" => "AIClockBridge/MiniMaxTokenPlanKey",
                _ => null
            };
            if (legacy == null || !CredRead(legacy, Generic, 0, out pointer)) return "";
        }
        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            return credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0
                ? ""
                : Marshal.PtrToStringUni(credential.CredentialBlob, credential.CredentialBlobSize / 2) ?? "";
        }
        finally
        {
            CredFree(pointer);
        }
    }
}
