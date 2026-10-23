using Microsoft.Win32.SafeHandles;
using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;

namespace AIBotBridge;

internal static class TightVncElevation
{
    private sealed record Request(string Action,string Instance,string Nonce,byte[] Password);
    private sealed record Reply(string Stage,bool Restart=false,TightVncResult? Result=null);
    private sealed record Commit(bool Accept,string Nonce);
    internal static async Task<TightVncResult> RunAsync(string action,string instance,byte[] password,Func<bool,Task<bool>> commit,CancellationToken token) {
        string name="AI-bot-component-"+Guid.NewGuid().ToString("N"),nonce=Guid.NewGuid().ToString("N");
        var security=new PipeSecurity();security.SetAccessRuleProtection(true,false);
        foreach(var sid in new[]{new SecurityIdentifier(TightVncPackage.Sid),new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null),new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null)})
            security.AddAccessRule(new(sid,PipeAccessRights.FullControl,AccessControlType.Allow));
        using var pipe=NamedPipeServerStreamAcl.Create(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,8192,8192,security);
        var start=new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"AIBotBridge.exe")) {UseShellExecute=true,Verb="runas",WorkingDirectory=AppContext.BaseDirectory,WindowStyle=ProcessWindowStyle.Hidden};
        foreach(string value in new[]{"--tightvnc-worker",name,Environment.ProcessId.ToString(),nonce})start.ArgumentList.Add(value);
        using var worker=Process.Start(start)??throw new IOException("管理员安装进程未能启动。");
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromMinutes(10));
        bool acknowledged=false;
        try {
            await pipe.WaitForConnectionAsync(deadline.Token);
            if(!GetNamedPipeClientProcessId(pipe.SafePipeHandle,out uint pid)||pid!=worker.Id)throw new IOException("组件辅助进程身份不匹配。");
            await WriteAsync(pipe,new Request(action,instance,nonce,password),deadline.Token);
            var reply=await ReadAsync<Reply>(pipe,deadline.Token);
            if(reply.Stage=="prepared") {
                bool accepted=false;
                try{accepted=await commit(reply.Restart);}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or CryptographicException or OperationCanceledException or System.Net.Sockets.SocketException){}
                await WriteAsync(pipe,new Commit(accepted,nonce),deadline.Token);acknowledged=accepted;
                reply=await ReadAsync<Reply>(pipe,deadline.Token);
            }
            if(reply.Stage!="finished"||reply.Result is null)throw new IOException("组件辅助回复无效。");
            return reply.Result;
        }catch(Exception ex) when(acknowledged&&ex is (IOException or OperationCanceledException)) {
            // A positive commit may already have arrived. Preserve the encrypted
            // credential rather than leave a committed service with a lost key.
            return new(false,"配置已保存，但完成状态未确认，请重新检查组件。",KeepCredentials:true);
        }
    }
    internal static async Task<int> WorkerAsync(string name,string parentText,string nonce) {
        if(!name.StartsWith("AI-bot-component-",StringComparison.Ordinal)||name.Length!=49||
           !Guid.TryParseExact(name[17..],"N",out _)||!Guid.TryParseExact(nonce,"N",out _)||
           !int.TryParse(parentText,out int parent)||parent<=0)return 2;
        using var identity=WindowsIdentity.GetCurrent();
        if(!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))return 3;
        using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(10));
        using var pipe=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous,TokenImpersonationLevel.Identification);
        Request? request=null;
        try {
            await pipe.ConnectAsync(30000,deadline.Token);
            if(!GetNamedPipeServerProcessId(pipe.SafePipeHandle,out uint server)||server!=parent)throw new IOException("组件调用进程身份不匹配。");
            string owner=ProcessSid(parent);
            request=await ReadAsync<Request>(pipe,deadline.Token);
            if(request.Nonce!=nonce||request.Action is not ("install" or "start" or "authorize" or "uninstall")||request.Instance is null||request.Password is null||request.Instance.Length>32||request.Password.Length>8)throw new IOException("组件操作无效。");
            TightVncResult result;
            try {
                result=await TightVncMachine.ChangeAsync(request.Action,owner,request.Instance,request.Password,async restart=> {
                    await WriteAsync(pipe,new Reply("prepared",restart),deadline.Token);
                    var response=await ReadAsync<Commit>(pipe,deadline.Token);return response.Nonce==nonce&&response.Accept;
                });
            }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or Win32Exception or System.Security.SecurityException or CryptographicException or OperationCanceledException) {
                result=new(false,"组件操作未完成，请重新检查安装状态。原有 PIN 和配对保留。");
            }
            await WriteAsync(pipe,new Reply("finished",Result:result),deadline.Token);
            return result.Ok?0:1;
        }catch(Exception ex) when(ex is IOException or OperationCanceledException or UnauthorizedAccessException or JsonException or Win32Exception){return 1;}
        finally{if(request is not null)CryptographicOperations.ZeroMemory(request.Password);}
    }
    private static string ProcessSid(int pid) {
        using var process=Process.GetProcessById(pid);
        if(!OpenProcessToken(process.Handle,8,out var token))throw new Win32Exception();
        using(token)using(var identity=new WindowsIdentity(token.DangerousGetHandle()))return identity.User?.Value??throw new IOException("调用用户无效。");
    }
    internal static async Task WriteAsync<T>(Stream stream,T value,CancellationToken token) {
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(value),header=new byte[4];
        try {
            if(bytes.Length>8192)throw new IOException("组件消息过长。");
            BinaryPrimitives.WriteInt32LittleEndian(header,bytes.Length);await stream.WriteAsync(header,token);await stream.WriteAsync(bytes,token);await stream.FlushAsync(token);
        }finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    internal static async Task<T> ReadAsync<T>(Stream stream,CancellationToken token) {
        byte[] header=new byte[4];await stream.ReadExactlyAsync(header,token);int length=BinaryPrimitives.ReadInt32LittleEndian(header);
        if(length is <2 or >8192)throw new IOException("组件消息长度无效。");
        byte[] bytes=new byte[length];
        try{await stream.ReadExactlyAsync(bytes,token);return JsonSerializer.Deserialize<T>(bytes)??throw new IOException("组件消息无效。");}
        finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    [DllImport("kernel32.dll",SetLastError=true)]private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe,out uint pid);
    [DllImport("kernel32.dll",SetLastError=true)]private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe,out uint pid);
    [DllImport("advapi32.dll",SetLastError=true)]private static extern bool OpenProcessToken(IntPtr process,uint rights,out SafeAccessTokenHandle token);
}
