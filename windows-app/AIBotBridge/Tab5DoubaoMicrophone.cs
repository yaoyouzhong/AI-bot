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
    private readonly Func<bool> _isIdle;
    private readonly Func<long> _clock;
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly object _recoveryGate=new();
    private Task _recovery=Task.CompletedTask;
    private bool _leased;
    private int _failures;
    private long _retryAt;
    internal Tab5DoubaoMicrophone(string? journal=null,Func<string,object?,Task<JsonElement>>? call=null,
        Func<bool>? isIdle=null,Func<long>? clock=null) {
        _journal=journal??Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AIBotBridge","doubao-microphone-lease.json");
        _call=call??CallAsync;
        _isIdle=isIdle??(()=>Tab5DoubaoVoice.TryCreate(()=>false)?.IsIdle==true);
        _clock=clock??(()=>Environment.TickCount64);
    }
    internal bool HasPendingRestore=>File.Exists(_journal);
    internal Task Recovery {get{lock(_recoveryGate)return _recovery;}}
    internal string Diagnostic {get{lock(_recoveryGate)return $"microphone={(_leased?"in-use":HasPendingRestore?"restore-pending":"restored")}; restoreFailures={_failures}";}}
    internal Task AcquireAsync()=>AcquireAsync(null);
    internal async Task AcquireAsync(string? existingInputId) {
        Task recovery;
        lock(_recoveryGate) {
            if(_leased)throw new InvalidOperationException("麦克风正在使用，请稍后重试");
            _leased=true;recovery=_recovery;
        }
        try {
            await recovery.ConfigureAwait(false);
            await _gate.WaitAsync().ConfigureAwait(false);
            try {
                await RestoreCoreAsync().ConfigureAwait(false);
                EnsureIdle();
                var list=await _call("settings.getMicrophoneList",null).ConfigureAwait(false);
                string previous=list.GetProperty("selectedMicrophoneId").GetString()??"";
                if(existingInputId is not null) {
                    // Shared DJI mode never changes the desktop microphone selection.
                    if(!string.Equals(previous,existingInputId,StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("请先在电脑豆包中选择同一个大疆麦克风，再开始 TAB5 语音");
                    return;
                }
                var cables=list.GetProperty("devices").EnumerateArray()
                    .Where(d=>Tab5VoiceAudio.IsCableCapture(d.GetProperty("name").GetString()??"")).ToArray();
                if(cables.Length!=1)throw new InvalidOperationException("豆包未找到唯一的 CABLE Output 麦克风");
                string cable=cables[0].GetProperty("id").GetString()!;
                if(previous==cable)return;
                Directory.CreateDirectory(Path.GetDirectoryName(_journal)!);
                string temporary=_journal+".tmp";
                File.WriteAllText(temporary,JsonSerializer.Serialize(new Lease(previous,cable)));
                File.Move(temporary,_journal,true);
                // Commit the recovery journal before attempting any settings update.
                await SelectAsync(cable).ConfigureAwait(false);
            } finally {_gate.Release();}
        } catch {Release();throw;}
    }
    internal async Task RestoreAsync() {
        lock(_recoveryGate)_leased=false;
        await RestoreOnceAsync().ConfigureAwait(false);
    }
    internal void Release() {
        lock(_recoveryGate) {
            if(_leased){_leased=false;_retryAt=0;}
        }
        RetryRestore();
    }
    // Called by the existing UI timer, including on startup with voice disabled.
    // Failed settings requests keep their journal and retry without another take.
    internal void RetryRestore() {
        lock(_recoveryGate) {
            if(_leased||!_recovery.IsCompleted||!HasPendingRestore||_clock()<_retryAt)return;
            _recovery=RecoverAsync();
        }
    }
    private async Task RecoverAsync() {
        try {
            await RestoreOnceAsync().ConfigureAwait(false);
            lock(_recoveryGate){_failures=0;_retryAt=0;}
        } catch(Exception ex) when(IsRestoreError(ex)) {
            lock(_recoveryGate) {
                _failures=Math.Min(_failures+1,4);
                _retryAt=_clock()+(_failures switch{1=>1000,2=>3000,3=>10000,_=>30000});
            }
            Tab5VoiceTiming.Log("doubao-microphone-restore-pending",Environment.TickCount64);
        }
    }
    internal static bool IsRestoreError(Exception ex)=>ex is InvalidOperationException or IOException or UnauthorizedAccessException or JsonException or TimeoutException or OperationCanceledException;
    private async Task RestoreOnceAsync() {
        await _gate.WaitAsync().ConfigureAwait(false);
        try {await RestoreCoreAsync().ConfigureAwait(false);}
        finally {_gate.Release();}
    }
    private async Task RestoreCoreAsync() {
        if(!File.Exists(_journal))return;
        var lease=JsonSerializer.Deserialize<Lease>(await File.ReadAllTextAsync(_journal).ConfigureAwait(false))
            ??throw new InvalidOperationException("豆包麦克风恢复记录无效");
        var list=await _call("settings.getMicrophoneList",null).ConfigureAwait(false);
        string current=list.GetProperty("selectedMicrophoneId").GetString()??"";
        // A manual choice made during the session wins over our saved value.
        if(current==lease.Temporary)await SelectAsync(lease.Previous).ConfigureAwait(false);
        File.Delete(_journal);
    }
    private void EnsureIdle() {
        if(!_isIdle())throw new InvalidOperationException("豆包正在录音、识别或状态无法确认，暂不切换麦克风");
    }
    private async Task SelectAsync(string id) {
        EnsureIdle();
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
