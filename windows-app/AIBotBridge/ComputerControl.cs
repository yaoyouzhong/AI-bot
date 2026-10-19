using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using NAudio.CoreAudioApi;
using Windows.Media.Control;

namespace AIBotBridge;

internal sealed record ComputerLauncher(string Id,string Name,string Path);
internal sealed record ComputerLauncherView(string Id,string Name,string Kind="app",string? Icon=null);
internal sealed record ComputerControlState(int Version=1,bool VolumeAvailable=false,int Volume=0,bool Muted=false,
    bool CanLock=false,bool MediaAvailable=false,bool CanPlayPause=false,bool CanPrevious=false,bool CanNext=false,
    bool Playing=false,ComputerLauncherView[]? Launchers=null,string IconRevision="",bool? Locked=null,int? DisplayState=null,bool OneClickUnlock=false);
internal sealed record ComputerCommand(string Op,int? Volume=null,bool? Muted=null,string? LauncherId=null,string? Action=null);

internal sealed class ComputerLauncherStore
{
    private readonly object _gate=new();
    private readonly string _path;
    internal ComputerLauncherStore(string? path=null)=>_path=path??UserPreferenceFile.PathFor("computer-launchers.json");
    internal ComputerLauncher[] Load() {
        lock(_gate) {
            if(!File.Exists(_path))return [];
            if(new FileInfo(_path).Length>32768)throw new InvalidDataException("常用启动配置文件过大。");
            var entries=JsonSerializer.Deserialize<ComputerLauncher[]>(File.ReadAllText(_path))??throw new InvalidDataException("常用启动配置为空。");
            Validate(entries,false);return entries;
        }
    }
    internal static void Validate(ComputerLauncher[] entries,bool requireExists) {
        if(entries.Any(x=>x is null)||entries.Length>4||entries.Select(x=>x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=entries.Length)throw new InvalidDataException("最多设置 4 个有效常用启动，且标识不能重复。");
        foreach(var item in entries) {
            if(!Guid.TryParseExact(item.Id,"D",out _)||string.IsNullOrWhiteSpace(item.Name)||item.Name.Length>16||item.Name.Any(char.IsControl))throw new InvalidDataException("启动名称须为 1–16 个字符。");
            if(string.IsNullOrWhiteSpace(item.Path)||!Path.IsPathFullyQualified(item.Path)||item.Path.StartsWith(@"\\",StringComparison.Ordinal)||item.Path.Length>1024||item.Path.Any(char.IsControl))throw new InvalidDataException("请选择本机软件或文件夹。");
            if(requireExists&&!Directory.Exists(item.Path)&&!(File.Exists(item.Path)&&string.Equals(Path.GetExtension(item.Path),".exe",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("软件或文件夹已不存在，请重新选择。");
        }
    }
    internal void Save(ComputerLauncher[] entries) {
        Validate(entries,true);lock(_gate) {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);string temporary=_path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllText(temporary,JsonSerializer.Serialize(entries));File.Move(temporary,_path,true);}
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
}

internal interface IComputerControlBackend
{
    ComputerLockScreenState ReadLockScreen()=>new();
    Task<ComputerControlState> ReadAsync(CancellationToken token);
    Task<bool> ExecuteAsync(ComputerCommand command,ComputerLauncher? launcher,CancellationToken token);
}

internal sealed class WindowsComputerControlBackend(Func<MusicSnapshot?>? displayedMusic=null) : IComputerControlBackend
{
    public ComputerLockScreenState ReadLockScreen()=>WindowsLockScreen.Read();
    [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool LockWorkStation();
    private GlobalSystemMediaTransportControlsSessionManager? _media;
    private async Task<GlobalSystemMediaTransportControlsSession?> SessionAsync(CancellationToken token) {
        _media??=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(token);
        var session=_media.GetCurrentSession();if(session is null)return null;
        var expected=displayedMusic?.Invoke();if(expected is null||string.IsNullOrWhiteSpace(expected.SystemMediaSource))return null;
        var properties=await session.TryGetMediaPropertiesAsync().AsTask(token);
        return Matches(expected,session.SourceAppUserModelId,properties.Title,properties.Artist)?session:null;
    }
    internal static bool Matches(MusicSnapshot? expected,string source,string title,string artist)=>expected is {Title.Length:>0,SystemMediaSource.Length:>0}&&
        expected.SystemMediaSource==source&&expected.Title.Trim()==title.Trim()&&expected.Artist.Trim()==artist.Trim();
    public async Task<ComputerControlState> ReadAsync(CancellationToken token) {
        bool? locked=WindowsSessionLock.Read();
        var state=new ComputerControlState(CanLock:Environment.UserInteractive&&locked!=true,Locked:locked);
        try {using var enumerator=new MMDeviceEnumerator();using var device=enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);
            state=state with{VolumeAvailable=true,Volume=(int)Math.Round(device.AudioEndpointVolume.MasterVolumeLevelScalar*100),Muted=device.AudioEndpointVolume.Mute};
        }catch(Exception ex) when(ex is COMException or InvalidOperationException or UnauthorizedAccessException){ }
        try {var session=await SessionAsync(token);if(session is not null) {
            var playback=session.GetPlaybackInfo();var controls=playback.Controls;
            state=state with{MediaAvailable=true,CanPlayPause=controls.IsPlayPauseToggleEnabled,CanPrevious=controls.IsPreviousEnabled,CanNext=controls.IsNextEnabled,
                Playing=playback.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing};
        }}catch(Exception ex) when(ex is COMException or InvalidOperationException or UnauthorizedAccessException or OperationCanceledException){ }
        return state with{DisplayState=WindowsDisplayPower.Read()};
    }
    public async Task<bool> ExecuteAsync(ComputerCommand command,ComputerLauncher? launcher,CancellationToken token) {
        token.ThrowIfCancellationRequested();
        if(command.Op is "volume" or "mute") {
            using var enumerator=new MMDeviceEnumerator();using var device=enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);
            if(command.Op=="volume")device.AudioEndpointVolume.MasterVolumeLevelScalar=command.Volume!.Value/100f;
            else device.AudioEndpointVolume.Mute=command.Muted!.Value;
            return true;
        }
        if(command.Op=="lock")return Environment.UserInteractive&&LockWorkStation();
        if(command.Op=="launch") {
            if(launcher is null)return false;ComputerLauncherStore.Validate([launcher],true);
            var start=new ProcessStartInfo(launcher.Path){UseShellExecute=true};
            if(Directory.Exists(launcher.Path)) {
                start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe")){UseShellExecute=false};
                start.ArgumentList.Add(launcher.Path);
            }
            using var process=Process.Start(start);return process is not null;
        }
        if(command.Op=="media") {
            var session=await SessionAsync(token);if(session is null)return false;
            var controls=session.GetPlaybackInfo().Controls;
            return command.Action switch {
                "play-pause" when controls.IsPlayPauseToggleEnabled=>await session.TryTogglePlayPauseAsync().AsTask(token),
                "previous" when controls.IsPreviousEnabled=>await session.TrySkipPreviousAsync().AsTask(token),
                "next" when controls.IsNextEnabled=>await session.TrySkipNextAsync().AsTask(token),_=>false};
        }
        return false;
    }
}

internal sealed class ComputerControlService(IComputerControlBackend? backend=null,ComputerLauncherStore? launchers=null,TimeProvider? clock=null,Func<bool>? savedPinAvailable=null)
{
    private readonly ComputerLauncherIcons _icons=new();
    private readonly IComputerControlBackend _backend=backend??new WindowsComputerControlBackend();
    private readonly ComputerLauncherStore _launchers=launchers??new();
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly TimeProvider _clock=clock??TimeProvider.System;
    // RPC accepts issuedAt only from -90s to +10s. Retries retain issuedAt.
    // Keep receipts beyond that window, and never evict a still-valid receipt.
    private readonly Dictionary<Guid,(ComputerCommand Command,(int Status,object Body) Reply,DateTimeOffset At)> _completed=[];
    private static string Text(JsonElement root,string name)=>root.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()!:"";
    private async Task<ComputerControlState> StateAsync(CancellationToken token,string knownIcons="") {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(2));
        ComputerControlState state;
        try{state=await _backend.ReadAsync(timeout.Token);}catch(Exception ex) when(IsUnavailable(ex)){state=new();}
        var icons=_icons.Get(_launchers.Load(),knownIcons);
        return state with{Launchers=icons.Views,IconRevision=icons.Revision,OneClickUnlock=savedPinAvailable?.Invoke()==true};
    }
    private static bool IsUnavailable(Exception ex)=>ex is COMException or InvalidOperationException or IOException or UnauthorizedAccessException or OperationCanceledException or System.ComponentModel.Win32Exception;
    internal async Task<(int Status,object Body)> HandleAsync(JsonElement root,CancellationToken token) {
        if(root.ValueKind!=JsonValueKind.Object)return(400,new{error="invalid_request"});
        string op=Text(root,"op");ComputerCommand command=new(op);
        if(op is not ("status" or "lock-screen" or "volume" or "mute" or "lock" or "launch" or "media"))return(400,new{error="invalid_operation"});
        string[] fields=op switch {"volume"=>["volume"],"mute"=>["muted"],"launch"=>["launcherId"],"media"=>["action"],_=>[]};
        string[] envelope=["kind","op","requestId","session","issuedAt","binaryReply","bulkReply","iconRevision","unlockHistory"];
        if(root.EnumerateObject().Any(p=>!envelope.Contains(p.Name)&&!fields.Contains(p.Name))||root.EnumerateObject().Select(p=>p.Name).Distinct().Count()!=root.EnumerateObject().Count())return(400,new{error="invalid_parameters"});
        if(root.TryGetProperty("unlockHistory",out _)&&(op!="status"||!Tab5UnlockDiagnostic.TryReadHistory(root,out _)))return(400,new{error="invalid_unlock_history"});
        string knownIcons=Text(root,"iconRevision");
        if(knownIcons.Length>64||knownIcons.Any(c=>!Uri.IsHexDigit(c)))return(400,new{error="invalid_icon_revision"});
        // A fresh, read-only probe independent of audio/media/icon work and
        // command receipts. Unknown remains unknown; no Windows key injection.
        if(op=="lock-screen")return(200,_backend.ReadLockScreen());
        if(op=="volume") {if(!root.TryGetProperty("volume",out var v)||v.ValueKind!=JsonValueKind.Number||!v.TryGetInt32(out int level)||level is <0 or >100)return(400,new{error="invalid_volume"});command=command with{Volume=level};}
        if(op=="mute") {if(!root.TryGetProperty("muted",out var m)||m.ValueKind is not (JsonValueKind.True or JsonValueKind.False))return(400,new{error="invalid_muted"});command=command with{Muted=m.GetBoolean()};}
        if(op=="launch") {if(!Guid.TryParseExact(Text(root,"launcherId"),"D",out var launcherId))return(400,new{error="invalid_launcher"});command=command with{LauncherId=launcherId.ToString("D")};}
        if(op=="media") {string action=Text(root,"action");if(action is not ("play-pause" or "previous" or "next"))return(400,new{error="invalid_media_action"});command=command with{Action=action};}
        if(op=="status") {try{return(200,await StateAsync(token,knownIcons));}catch(Exception ex) when(IsUnavailable(ex)||ex is JsonException){return(503,new{error="configuration_unavailable"});}}
        if(!Guid.TryParseExact(Text(root,"requestId"),"D",out var requestId))return(400,new{error="invalid_request_id"});
        await _gate.WaitAsync(token);
        try {
            var now=_clock.GetUtcNow();foreach(var expired in _completed.Where(x=>now-x.Value.At>TimeSpan.FromSeconds(120)).Select(x=>x.Key).ToArray())_completed.Remove(expired);
            if(_completed.TryGetValue(requestId,out var previous))return previous.Command==command?previous.Reply:(409,new{error="request_id_conflict"});
            if(_completed.Count>=256)return(429,new{error="command_capacity_reached"});
            (int Status,object Body) result;
            try {
                var state=await StateAsync(token,knownIcons);ComputerLauncher? launcher=null;
                string? error=null;int status=409;
                if(op is "volume" or "mute"&&!state.VolumeAvailable)error="volume_unavailable";
                if(op=="lock"&&!state.CanLock)error="lock_unavailable";
                if(op=="launch") {launcher=_launchers.Load().SingleOrDefault(x=>x.Id.Equals(command.LauncherId,StringComparison.OrdinalIgnoreCase));if(launcher is null){error="launcher_not_found";status=404;}}
                if(op=="media"&&(!state.MediaAvailable||command.Action=="play-pause"&&!state.CanPlayPause||command.Action=="previous"&&!state.CanPrevious||command.Action=="next"&&!state.CanNext))error="media_unavailable";
                if(error is not null)result=(status,new{error,state});
                else {
                    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(3));
                    bool ok=await _backend.ExecuteAsync(command,launcher,timeout.Token);
                    state=await StateAsync(CancellationToken.None,knownIcons);
                    result=ok?(200,new{ok=true,state}):(503,new{error="command_failed",state});
                }
            }catch(Exception ex) when(IsUnavailable(ex)||ex is JsonException or ArgumentException){result=(503,new{error="command_failed"});}
            _completed.Add(requestId,(command,result,now));return result;
        }finally{_gate.Release();}
    }
}
