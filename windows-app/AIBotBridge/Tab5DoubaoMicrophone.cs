using System.Buffers.Binary;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;

namespace AIBotBridge;

// Independently implemented client for the installed IME's local settings pipe.
// Only the microphone field is patched. No vendor code or binaries are shipped.
internal sealed class Tab5DoubaoMicrophone
{
    private sealed record Lease(string Previous,string Temporary);
    private readonly string _journal;
    private readonly Func<string,object?,Task<JsonElement>> _call;
    internal Tab5DoubaoMicrophone(string? journal=null,Func<string,object?,Task<JsonElement>>? call=null) {
        _journal=journal??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AIBotBridge","doubao-microphone-lease.json");
        _call=call??CallAsync;
    }
    internal async Task AcquireAsync() {
        await RestoreAsync().ConfigureAwait(false);
        var list=await _call("settings.getMicrophoneList",null).ConfigureAwait(false);
        string previous=list.GetProperty("selectedMicrophoneId").GetString()??"";
        var cables=list.GetProperty("devices").EnumerateArray()
            .Where(d=>Tab5VoiceAudio.IsCableCapture(d.GetProperty("name").GetString()??"")).ToArray();
        if(cables.Length!=1)throw new InvalidOperationException("豆包未找到唯一的 CABLE Output 麦克风");
        string cable=cables[0].GetProperty("id").GetString()!;
        if(previous==cable)return;
        Directory.CreateDirectory(Path.GetDirectoryName(_journal)!);
        File.WriteAllText(_journal,JsonSerializer.Serialize(new Lease(previous,cable)));
        // Journal first: an uncertain update/bridge crash remains recoverable.
        await SelectAsync(cable).ConfigureAwait(false);
    }
    internal async Task RestoreAsync() {
        if(!File.Exists(_journal))return;
        var lease=JsonSerializer.Deserialize<Lease>(await File.ReadAllTextAsync(_journal).ConfigureAwait(false))
            ??throw new InvalidOperationException("豆包麦克风恢复记录无效");
        var list=await _call("settings.getMicrophoneList",null).ConfigureAwait(false);
        string current=list.GetProperty("selectedMicrophoneId").GetString()??"";
        // A manual choice made during the session wins over our saved value.
        if(current==lease.Temporary)await SelectAsync(lease.Previous).ConfigureAwait(false);
        File.Delete(_journal);
    }
    private async Task SelectAsync(string id) {
        await _call("settings.update",new {patch=new {voice=new {selectedMicrophoneId=id}}}).ConfigureAwait(false);
        var check=await _call("settings.getMicrophoneList",null).ConfigureAwait(false);
        if(check.GetProperty("selectedMicrophoneId").GetString()!=id)
            throw new InvalidOperationException("豆包未确认麦克风切换，请检查语音设置");
    }
    internal static async Task<JsonElement> CallAsync(string method,object? payload) {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var token=timeout.Token;
        try {
            using var pipe=new NamedPipeClientStream(".",@"DoubaoIme\settings-rpc",PipeDirection.InOut,PipeOptions.Asynchronous,TokenImpersonationLevel.None);
            await pipe.ConnectAsync(token).ConfigureAwait(false);
            string requestId=Guid.NewGuid().ToString("N");
            byte[] body=JsonSerializer.SerializeToUtf8Bytes(new {version=1,requestId,method,payload});
            byte[] header=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(header,body.Length);
            await pipe.WriteAsync(header,token).ConfigureAwait(false);
            await pipe.WriteAsync(body,token).ConfigureAwait(false);
            await pipe.ReadExactlyAsync(header,token).ConfigureAwait(false);
            int length=BinaryPrimitives.ReadInt32LittleEndian(header);
            if(length is <1 or >1048576)throw new InvalidOperationException("豆包设置回复长度无效");
            byte[] response=new byte[length];await pipe.ReadExactlyAsync(response,token).ConfigureAwait(false);
            using var json=JsonDocument.Parse(response);var root=json.RootElement;
            if(!root.TryGetProperty("ok",out var ok)||ok.ValueKind!=JsonValueKind.True)
                throw new InvalidOperationException("豆包拒绝了麦克风设置请求");
            if(root.TryGetProperty("requestId",out var echo)&&echo.GetString()!=requestId)
                throw new InvalidOperationException("豆包设置回复不匹配");
            return root.GetProperty("payload").Clone();
        } catch(Exception ex) when(ex is IOException or OperationCanceledException or JsonException or UnauthorizedAccessException) {
            throw new InvalidOperationException("无法完成豆包麦克风切换，请检查豆包是否运行",ex);
        }
    }
}
