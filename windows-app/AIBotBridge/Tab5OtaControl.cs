using System.Buffers.Binary;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;

namespace AIBotBridge;

// Same-user local IPC only. No HTTP listener or profile/credential override.
internal static class Tab5OtaControl
{
    internal static string PipeName=>"AIBot.Tab5Ota."+WindowsIdentity.GetCurrent().User!.Value;
    private static async Task<byte[]> ReadAsync(Stream stream,CancellationToken token) {
        byte[] header=new byte[4];await stream.ReadExactlyAsync(header,token);
        uint length=BinaryPrimitives.ReadUInt32LittleEndian(header);
        if(length is <2 or >4096)throw new IOException("Invalid local control length");
        byte[] bytes=new byte[length];await stream.ReadExactlyAsync(bytes,token);return bytes;
    }
    private static async Task WriteAsync(Stream stream,object value,CancellationToken token) {
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(value,JsonDefaults.Options);
        if(bytes.Length>4096)throw new IOException("Local control response too large");
        byte[] header=new byte[4];BinaryPrimitives.WriteUInt32LittleEndian(header,(uint)bytes.Length);
        await stream.WriteAsync(header,token);await stream.WriteAsync(bytes,token);await stream.FlushAsync(token);
    }
    internal static async Task RunAsync(Func<Tab5Service?> service,CancellationToken token,string? name=null) {
        while(!token.IsCancellationRequested) {
            using var pipe=new NamedPipeServerStream(name??PipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
            try {
                await pipe.WaitForConnectionAsync(token);
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(45));
                using var request=JsonDocument.Parse(await ReadAsync(pipe,deadline.Token));
                object response;
                try {
                    var current=service()??throw new InvalidOperationException("没有已启用的 TAB5。");
                    string op=request.RootElement.GetProperty("op").GetString()??"";
                    if(op=="info")response=new{ok=true,body=current.OtaControlInfo};
                    else if(op=="start") {
                        string id=request.RootElement.GetProperty("deviceId").GetString()??"",sha=request.RootElement.GetProperty("sha256").GetString()??"";
                        await current.StartScopedOtaAsync(id,sha,deadline.Token);response=new{ok=true,status=current.RemoteOtaStatus};
                    }else throw new InvalidOperationException("不支持的本机命令。");
                }catch(Exception ex) when(ex is IOException or InvalidOperationException or JsonException or KeyNotFoundException) {response=new{ok=false,error=ex.Message};}
                await WriteAsync(pipe,response,deadline.Token);
            }catch(OperationCanceledException) when(token.IsCancellationRequested){return;}
            catch(Exception ex) when(ex is IOException or JsonException or OperationCanceledException) {/* Close this bounded connection; no retry of an installation. */}
        }
    }
    internal static async Task<JsonElement> CallAsync(string op,string? device=null,string? sha=null,string? name=null) {
        using var token=new CancellationTokenSource(TimeSpan.FromSeconds(45));
        using var pipe=new NamedPipeClientStream(".",name??PipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(2000,token.Token);await WriteAsync(pipe,new{op,deviceId=device,sha256=sha},token.Token);
        using var response=JsonDocument.Parse(await ReadAsync(pipe,token.Token));return response.RootElement.Clone();
    }
}

internal sealed partial class Tab5Service
{
    internal object OtaControlInfo {
        get {var image=Volatile.Read(ref _ota);return new{deviceId=PairedId,installedVersion=_firmware.LastVersion(PairedId),
            offeredVersion=image?.Version,sha256=image?.Sha256,elfSha256=image?.ElfSha256,canStart=CanStartOta,status=OtaSummary,hint=OtaStartHint};}
    }
    internal Task StartScopedOtaAsync(string id,string sha,CancellationToken token) {
        lock(_lifecycle) {
            var image=Volatile.Read(ref _ota);
            if(!Tab5Protocol.ValidId(id)||id!=PairedId||image is null||sha!=image.Sha256)
                throw new InvalidOperationException("设备或固件校验值不一致，升级未发起。");
            return StartOtaAsync(token);
        }
    }
}
