using AIBot.LoginMethod;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if(args.SequenceEqual(new[]{"--preflight-install"})) {
            using var current=Process.GetCurrentProcess();Console.WriteLine("HELPER_PREFLIGHT session="+current.SessionId+" locked="+Native.Locked(current.SessionId));
            try {dynamic service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;service.Connect();dynamic folder=service.GetFolder("\\");dynamic task=folder.GetTask(HelperContract.TaskName);Console.WriteLine("HELPER_TASK_PRESENT");}
            catch(Exception ex){Console.WriteLine("HELPER_TASK_QUERY "+ex.GetType().Name+" hresult=0x"+ex.HResult.ToString("X8"));}
            return 0;
        }
        if (args.SequenceEqual(new[] {"--self-test"})) {
            MethodSwitch.SelfTest();
            long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var reply = new HelperReply(1,"nonce",1,"pin",true,time,800,"ready");
            if (!HelperContract.Accept(reply,"nonce",1,"pin",time,time) || HelperContract.Accept(reply,"other",1,"pin",time,time) ||
                HelperContract.Accept(reply,"nonce",2,"pin",time,time) || HelperContract.Accept(reply,"nonce",1,"password",time,time) ||
                HelperContract.Accept(reply,"nonce",1,"pin",time,time+5000) || HelperContract.Accept(reply with {Ready=false},"nonce",1,"pin",time,time)) return 1;
            Console.WriteLine("LOGIN_HELPER_SELF_TEST_OK (method/nonce/session/freshness guards; real unlock not tested)"); return 0;
        }
        if (args.Length == 2 && args[0] is "--install" or "--uninstall" && int.TryParse(args[1], out int caller))
            return HelperInstaller.Run(args[0] == "--install", caller);
        if (args.Length != 5 || args[0] is not ("--prepare" or "--inspect") || !Guid.TryParseExact(args[1],"N",out _) ||
            !int.TryParse(args[2],out int session) || session <= 0 || args[3] is not ("pin" or "password")) return 64;
        return Request(args);
    }

    internal static int Request(string[] args)
    {
        if (!WindowsIdentity.GetCurrent().IsSystem || !long.TryParse(args[4], out long issued)) return 77;
        int session = int.Parse(args[2]); string nonce = args[1], target = args[3];
        if (!HelperContract.Fresh(issued, Now()) || !Path.GetFullPath(AppContext.BaseDirectory).TrimEnd('\\').Equals(HelperContract.InstallPath, StringComparison.OrdinalIgnoreCase)) return 77;
        var manifest = HelperContract.Manifest(); if (manifest is null) return 77;
        var elapsed = Stopwatch.StartNew();
        try {
            Native.Check(Native.OpenProcessToken(Native.GetCurrentProcess(), 0x2B, out nint own));
            try { Native.EnableTcb(own); } finally { Native.CloseHandle(own); }
            if (!OwnerSession(session, manifest.OwnerSid) || Native.Locked(session) != true) return 77;
            if (args[0] == "--prepare") return Launch(session,nonce,target,issued,elapsed);
            if (Process.GetCurrentProcess().SessionId != session) return 77;
            using var watchdog = new Timer(_ => Environment.Exit(124),null,4500,Timeout.Infinite);
            bool ready = MethodSwitch.Prepare(session,target,() => HelperContract.Fresh(issued,Now()) && OwnerSession(session,manifest.OwnerSid));
            ready = ready && HelperContract.Fresh(issued,Now()) && OwnerSession(session,manifest.OwnerSid) && Native.Locked(session)==true;
            Write(new(1,nonce,session,target,ready,Now(),(int)elapsed.ElapsedMilliseconds,ready?"ready":"method_not_ready"));
            return ready?0:6;
        } catch {
            Write(new(1,nonce,session,target,false,Now(),(int)elapsed.ElapsedMilliseconds,"helper_stopped")); return 7;
        }
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    internal static bool OwnerSession(int session, string sid)
    {
        if (Native.WTSGetActiveConsoleSessionId() != (uint)session || !Native.WTSQueryUserToken((uint)session,out nint token)) return false;
        try { using var identity = new WindowsIdentity(token); return identity.User?.Value == sid; }
        finally { Native.CloseHandle(token); }
    }

    private static int Launch(int session, string nonce, string target, long issued, Stopwatch elapsed)
    {
        if (Process.GetCurrentProcess().SessionId != 0) return 77;
        Native.Check(Native.OpenProcessToken(Native.GetCurrentProcess(),0x2B,out nint token));
        try {
            Native.Check(Native.DuplicateTokenEx(token,0x1AF,0,2,1,out nint duplicate));
            try {
                Native.Check(Native.SetTokenInformation(duplicate,12,ref session,sizeof(int)));
                string exe = Environment.ProcessPath!;
                var start = new Native.StartupInfo {Size=Marshal.SizeOf<Native.StartupInfo>(),Desktop=@"WinSta0\Winlogon"};
                Native.Check(Native.CreateProcessAsUser(duplicate,exe,new StringBuilder($"\"{exe}\" --inspect {nonce} {session} {target} {issued}"),0,0,false,0x08000000,0,AppContext.BaseDirectory,ref start,out var child));
                try {
                    if (Native.WaitForSingleObject(child.Process,4800)!=0) {
                        Native.TerminateProcess(child.Process,124); Native.WaitForSingleObject(child.Process,1000);
                        Write(new(1,nonce,session,target,false,Now(),(int)elapsed.ElapsedMilliseconds,"helper_timeout")); return 124;
                    }
                    Native.Check(Native.GetExitCodeProcess(child.Process,out uint code)); return (int)code;
                } finally { Native.CloseHandle(child.Thread);Native.CloseHandle(child.Process); }
            } finally { Native.CloseHandle(duplicate); }
        } finally { Native.CloseHandle(token); }
    }

    private static void Write(HelperReply reply)
    {
        string target=Path.Combine(HelperContract.InstallPath,"result.json"), temporary=target+".tmp";
        File.WriteAllText(temporary,JsonSerializer.Serialize(reply)); File.Move(temporary,target,true);
    }
}
