using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;

// Authenticated, viewer-leased screenshots. No image cache, log or photo upload.
internal sealed class Tab5ScreenPreview
{
    internal sealed record Context(string Device,string Host,HashSet<string> Transports,bool Available=true,bool HighDefinition=false,bool Responsive=false);
    internal sealed record Frame(int Width,int Height,byte[] Pixels,long Received);
    private sealed class Request(string id,string transport,int width,int height,long started,long expires,int version,string encoding)
    {
        internal readonly string Id=id,Transport=transport;
        internal int Width=width,Height=height;
        internal readonly int Version=version;
        internal readonly string Encoding=encoding;
        internal long Progress=started;
        internal readonly long Started=started,Expires=expires;
        internal byte[]? Bytes;
        internal int Received;
        internal string Sha="";
    }
    private readonly object _gate=new();
    private readonly Func<long> _tick,_epoch;
    private Context? _context;
    private string _stream="",_note="",_frameSha="";
    private long _lease,_next;
    private Request? _request;
    private Frame? _frame;
    private int _memoryLevel,_fallbackFrames;
    private long _qualityRetry,_qualityBackoff=30000;
    internal Tab5ScreenPreview(Func<long>? tick=null,Func<long>? epoch=null) {
        _tick=tick??(()=>Environment.TickCount64);_epoch=epoch??(()=>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }
    internal string Start(Context? context) {
        lock(_gate) {
            _context=context;_stream=Guid.NewGuid().ToString("N");_lease=_tick()+5000;
            _request=null;_frame=null;_frameSha="";_memoryLevel=0;_fallbackFrames=0;_qualityRetry=0;_qualityBackoff=30000;_next=0;_note="正在连接设备，等待首帧…";return _stream;
        }
    }
    private void Clear(){_context=null;_stream="";_request=null;_frame=null;_frameSha="";_lease=0;}
    internal void Stop(string? token=null) {
        lock(_gate){if(token is not null&&token!=_stream)return;Clear();_note="预览已关闭。";}
    }
    internal void Renew(string token,Context? current) {
        lock(_gate) {
            if(_stream.Length==0||token!=_stream)return;
            if(_context is not {} old||current is null||old.Device!=current.Device||old.Host!=current.Host) {
                Clear();_note="设备或电脑归属已变化，请重新打开设备预览。";return;
            }
            long now=_tick();
            if(now>_lease){_request=null;_frame=null;}
            _lease=now+5000;
            if(!current.Available||current.Transports.Count==0){_request=null;_note="设备未连接，等待连接恢复。";return;}
            if(_request is {} previous&&(!current.Transports.Contains(previous.Transport)||now-previous.Started>30000||now-previous.Progress>(previous.Encoding=="jpeg"&&previous.Received==0?15000:previous.Transport=="BLE"?10000:5000))) {
                _request=null;_next=now+1000;_note="读取超时或连接已变化，等待下一帧。";
            }
            _context=current;
            if(_request is null&&now>=_next) {
                string transport=current.Transports.Contains("USB")?"USB":current.Transports.Contains("Wi-Fi")?"Wi-Fi":"BLE";
                if(current.HighDefinition&&transport!="BLE"&&_memoryLevel>0&&_fallbackFrames>=3&&now>=_qualityRetry){_memoryLevel=0;_fallbackFrames=0;}
                bool hd=current.HighDefinition&&transport!="BLE"&&_memoryLevel==0;
                bool small=transport=="BLE"||_memoryLevel>=2;
                _request=new(Guid.NewGuid().ToString("N"),transport,hd?1280:small?320:640,hd?720:small?180:360,now,_epoch()+30000,current.HighDefinition?2:1,hd?"jpeg":"rgb565-rle1");
            }
        }
    }
    internal object? Command {get{lock(_gate)return CommandLocked(null);}}
    internal object? CommandForFrame(long frameEpoch){lock(_gate)return CommandLocked(frameEpoch);}
    private object? CommandLocked(long? frameEpoch) {
        if(_tick()>_lease){_request=null;_frame=null;}
        if(_stream.Length==0||_tick()>_lease||_request is not {} p||_tick()-p.Started>30000||_epoch()>p.Expires)return null;
        long expires=p.Expires;
        if(frameEpoch is {} epoch) {
            if(epoch<=0||epoch>long.MaxValue-30000)return null;
            expires=Math.Min(expires,epoch+30000);if(expires<=_epoch())return null;
        }
        return new{version=p.Version,streamId=_stream,requestId=p.Id,transport=p.Transport,expiresAt=expires,width=p.Width,height=p.Height,encoding=p.Encoding};
    }
    internal (Frame? Frame,string Note) Snapshot(string token) {
        lock(_gate){if(token!=_stream||_tick()>_lease)return(null,_note.Length>0?_note:"预览已停止。");return(_frame,_frame is {} f&&_tick()-f.Received>5000?"画面未更新 · "+_note:_note);}
    }
    private static string Text(JsonElement value,string key)=>value.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";
    private static int? Number(JsonElement value,string key)=>value.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.Number&&v.TryGetInt32(out int n)?n:null;
    private static (int,object) Error(int code,string reason)=>(code,new{error=reason});
    internal (int Status,object Body) Receive(JsonElement root,byte[] bytes,Context? current,string device,string transport) {
        lock(_gate) {
            if(root.ValueKind!=JsonValueKind.Object)return Error(400,"preview_operation");
            if(_stream.Length==0||_tick()>_lease||_context is not {} old||current is null||!current.Available||
               current.Device!=old.Device||current.Host!=old.Host||device!=old.Device||!current.Transports.Contains(transport)||
               Text(root,"streamId")!=_stream||_request is not {} p||Text(root,"requestId")!=p.Id||transport!=p.Transport||
               _tick()-p.Started>30000||_epoch()>p.Expires)return Error(409,"preview_expired");
            switch(Text(root,"op")) {
                case "begin":
                    if(bytes.Length!=0)return Error(400,"preview_data");
                    if(p.Version==2) {
                        int? w=Number(root,"width"),h=Number(root,"height");
                        if(Text(root,"encoding")!=p.Encoding||!((w==p.Width&&h==p.Height)||(p.Encoding=="jpeg"&&w==p.Height&&h==p.Width)))return Error(400,"preview_dimensions");
                        p.Width=w!.Value;p.Height=h!.Value;
                    }
                    p.Progress=_tick();return(200,new{accepted=true,width=p.Width,height=p.Height,encoding=p.Encoding,chunkSize=4096});
                case "unchanged":
                    if(p.Version!=2||bytes.Length!=0||_frame is not {} oldFrame||oldFrame.Width!=p.Width||oldFrame.Height!=p.Height||
                       Text(root,"encoding")!=p.Encoding||Text(root,"sha256")!=_frameSha||Number(root,"width")!=p.Width||Number(root,"height")!=p.Height)return Error(409,"preview_changed");
                    _frame=oldFrame with{Received=_tick()};FrameSucceeded();_request=null;_next=_tick()+CaptureCooldown(current,transport,true);_note=FrameNote(p,transport);
                    return(200,new{accepted=true,complete=true});
                case "error":
                    var notes=new Dictionary<string,string>{{"busy","设备忙，稍后继续。"},{"memory","设备内存不足，暂时无法预览。"},{"capture","暂未取得完整画面。"},{"encoding","画面编码失败。"},{"connection","画面连接已中断。"}};
                    string reason=Text(root,"error");
                    if(bytes.Length!=0||!notes.TryGetValue(reason,out string? note))return Error(400,"preview_error");
                    _note=note;
                    if(reason=="memory"&&p.Version==2&&p.Width>320) {
                        _memoryLevel=p.Encoding=="jpeg"?1:2;
                        _fallbackFrames=0;_qualityRetry=_tick()+_qualityBackoff;_qualityBackoff=Math.Min(300000,_qualityBackoff*2);
                        _note=_memoryLevel==1?"内存受限，正在切换标准画质…":"内存受限，正在切换小图预览…";
                    }
                    _request=null;_next=_tick()+(reason=="memory"?1000:2000);return(200,new{accepted=true});
                case "frame":
                    if(bytes.Length==0&&root.TryGetProperty("data",out var base64)&&base64.ValueKind==JsonValueKind.String)
                        try{bytes=Convert.FromBase64String(base64.GetString()??"");}catch(FormatException){return Error(400,"preview_data");}
                    int? total=Number(root,"size"),offset=Number(root,"offset");string sha=Text(root,"sha256");
                    if(Text(root,"encoding")!=p.Encoding||Number(root,"width")!=p.Width||Number(root,"height")!=p.Height||
                       total is not >=4 or >524288||offset is null||offset<0||offset>total||bytes.Length is <1 or >4096||bytes.Length>total-offset||
                       sha.Length!=64||sha.Any(c=>c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))return Error(400,"preview_frame");
                    if(p.Bytes is null){if(offset!=0)return Error(409,"preview_offset");p.Bytes=new byte[total.Value];p.Sha=sha;}
                    if(p.Bytes.Length!=total||p.Sha!=sha)return Error(409,"preview_changed");
                    if(offset<p.Received) {
                        if(offset+bytes.Length>p.Received||!p.Bytes.AsSpan(offset.Value,bytes.Length).SequenceEqual(bytes))return Error(409,"preview_changed");
                    } else {
                        if(offset!=p.Received)return Error(409,"preview_offset");bytes.CopyTo(p.Bytes,p.Received);p.Received+=bytes.Length;p.Progress=_tick();
                    }
                    if(p.Received==p.Bytes.Length) {
                        byte[]? decoded=Convert.ToHexString(SHA256.HashData(p.Bytes)).ToLowerInvariant()==sha?DecodeFrame(p.Bytes,p.Width,p.Height,p.Encoding):null;
                        if(decoded is null){_request=null;_next=_tick()+1000;_note="画面校验失败，等待下一帧。";return Error(400,"preview_corrupt");}
                        _frame=new(p.Width,p.Height,decoded,_tick());_frameSha=p.Sha;FrameSucceeded();_request=null;_next=_tick()+CaptureCooldown(current,transport,false);
                        _note=FrameNote(p,transport);
                        return(200,new{accepted=true,nextOffset=p.Received,complete=true});
                    }
                    if(_frame is null)_note=$"正在读取画面 · {p.Received*100/p.Bytes.Length}%";
                    return(200,new{accepted=true,nextOffset=p.Received,complete=false});
                default:return Error(400,"preview_operation");
            }
        }
    }
    private void FrameSucceeded(){if(_memoryLevel>0)_fallbackFrames=Math.Min(3,_fallbackFrames+1);else _qualityBackoff=30000;}
    private string FrameNote(Request p,string transport)=>$"实时镜像 · {p.Width} × {p.Height} · {transport}"+
        (_memoryLevel==1?" · 标准画质":_memoryLevel>=2?" · 小图预览":"");
    internal static int CaptureCooldown(Context? context,string transport,bool unchanged)=>
        context?.Responsive==true&&transport is "USB" or "Wi-Fi"?100:unchanged?2000:1000;
    internal static bool SupportsResponsiveCapture(string? firmware)=>SupportsHD(firmware)&&
        Version.Parse(firmware![..^3])>=new Version(0,2,277);
    internal static bool SupportsHD(string? firmware) {
        if(firmware is null||!firmware.EndsWith("-ui",StringComparison.Ordinal)||!Version.TryParse(firmware[..^3],out var version))return false;
        return version.Revision==-1&&version.Build>=0&&version>=new Version(0,2,270);
    }
    internal static byte[]? DecodeFrame(byte[] bytes,int width,int height,string encoding) =>
        encoding=="rgb565-rle1"?Decode(bytes,width,height):encoding=="jpeg"?Tab5PreviewJPEG.Decode(bytes,width,height):null;
    internal static byte[]? Decode(ReadOnlySpan<byte> bytes,int width,int height) {
        if(width is <1 or >640||height is <1 or >360||bytes.Length>524288)return null;
        byte[] output=new byte[width*height*2];int at=0,written=0;
        while(at<bytes.Length) {
            if(at+2>bytes.Length)return null;
            int control=bytes[at]|bytes[at+1]<<8,n=control&32767;at+=2;
            if(n==0||n>(output.Length-written)/2)return null;
            if((control&32768)!=0) {
                if(at+2>bytes.Length)return null;
                for(int i=0;i<n;i++){output[written++]=bytes[at];output[written++]=bytes[at+1];}at+=2;
            } else {
                if(n>(bytes.Length-at)/2)return null;bytes.Slice(at,n*2).CopyTo(output.AsSpan(written));at+=n*2;written+=n*2;
            }
        }
        return written==output.Length?output:null;
    }
}
