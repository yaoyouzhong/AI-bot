using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;

internal sealed partial class Tab5Service
{
    private readonly object _rpcLock=new();
    private readonly Tab5Gallery _gallery=new();
    internal Tab5QuickConsole QuickConsole {get;set;}=new();
    private readonly Dictionary<string,long> _rpcNonces=[];
    private int _rpcRequests;
    private long _rpcOtaUntil;
    private Tab5RpcImage? _rpcImage;
    private volatile string _imageUploadDiagnostic="尚无图片上传";
    internal bool ImageUploadActive {get {lock(_rpcLock)return Volatile.Read(ref _benchmarkRunning)>0||Environment.TickCount64<Interlocked.Read(ref _rpcOtaUntil)||_rpcImage is {} image&&image.Received<image.Bytes.Length&&Environment.TickCount64-image.Touched<90000;}}
    private volatile string _rpcAuthDiagnostic="尚无请求";
    private (int Status,byte[]? Packet) RejectRpc(string reason) {_rpcAuthDiagnostic=reason;return(401,null);}
    internal string ProfileDiagnostic {
        get {
            try {
                var path=AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var disk=new Tab5PairingStore().Current;
                return $"profile={path}; test={AppPaths.IsPublicSelfTest}; pairMatchesDisk={_store.Current==disk}; modeOnDisk={BridgeSettings.Load().Get("display_mode")}; pairFileTime={File.GetLastWriteTimeUtc(Path.Combine(path,"AI-bot","tab5-pairing.dat")):O}";
            }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or CryptographicException){return "profile-check="+ex.GetType().Name;}
        }
    }
    internal void ObserveFirmware(string id,string version) {
        _firmware.Observe(id,version);
        _assets.ObserveFirmware(version);
        if(Volatile.Read(ref _ota)?.Version==version)Interlocked.Exchange(ref _rpcOtaUntil,0);
    }
    private sealed class Tab5RpcImage(string device,string task,string id,int size,string sha)
    {
        internal readonly string Device=device,Task=task,Id=id,Sha=sha;
        internal readonly byte[] Bytes=new byte[size];
        internal int Received;
        internal readonly long Started=Environment.TickCount64;
        internal long Touched=Environment.TickCount64;
    }
    internal async Task<(int Status,byte[]? Packet)> RpcAsync(string id,string nonce,string proof,byte[] packet,CancellationToken token,bool allowOta=true,string transport="USB")
    {
        var pair=_store.Current;
        if(pair is null||pair.DeviceId!=id)return RejectRpc("device identity rejected");
        if(!Tab5Protocol.ValidNonce(nonce))return RejectRpc("nonce format rejected");
        if(packet.Length is <28 or >Tab5RpcBinary.RequestMaximum)return RejectRpc("packet length rejected");
        var key=Convert.FromBase64String(pair.Key);
        var digest=Convert.ToHexString(SHA256.HashData(packet)).ToLowerInvariant();
        if(!Tab5Protocol.Verify(key,$"POST|/tab5/v1/rpc|{id}|{nonce}|{digest}",proof))return RejectRpc("HMAC rejected");
        byte[] clear;
        try{clear=Tab5Protocol.Decrypt(key,nonce,packet,Tab5RpcBinary.RequestMaximum);}catch(CryptographicException){return RejectRpc("AES-GCM rejected");}
        _rpcAuthDiagnostic="verified";
        lock(_lifecycle){if(_stopping)return(503,null);_rpcRequests++;}
        try {
            (int Status,object Body) result;bool binaryReply=false,bulkReply=false;
            try {
                var opened=Tab5RpcBinary.Open(clear);using var doc=opened.Metadata;var root=doc.RootElement;
                binaryReply=root.TryGetProperty("binaryReply",out var binary)&&binary.ValueKind==JsonValueKind.True;
                bulkReply=binaryReply&&root.TryGetProperty("bulkReply",out var bulk)&&bulk.ValueKind==JsonValueKind.True;
                bool valid=root.ValueKind==JsonValueKind.Object&&Text(root,"session")==_session&&
                    root.TryGetProperty("issuedAt",out var at)&&at.TryGetInt64(out long issued)&&
                    issued>=DateTimeOffset.UtcNow.AddSeconds(-90).ToUnixTimeMilliseconds()&&issued<=DateTimeOffset.UtcNow.AddSeconds(10).ToUnixTimeMilliseconds();
                bool replay=false;
                if(valid)lock(_rpcLock) {
                    long now=Environment.TickCount64;
                    foreach(string old in _rpcNonces.Where(x=>now-x.Value>100000).Select(x=>x.Key).ToArray())_rpcNonces.Remove(old);
                    // Never evict a nonce while its timestamp could still pass.
                    replay=_rpcNonces.Count>=8192||!_rpcNonces.TryAdd(nonce,now);
                }
                if(!valid)result=(400,new{error="invalid_session_or_time"});
                else if(replay)result=(409,new{error="already_submitted"});
                else if(!opened.Data.IsEmpty&&Text(root,"kind") is not ("image-chunk" or "benchmark"))result=(400,new{error="unexpected_binary_data"});
                else if(Text(root,"kind") is "ota" or "ota_probe")result=allowOta?RpcOta(root,transport,binaryReply,bulkReply):(409,new{error="firmware_requires_ota_transport"});
                else if(Text(root,"kind")=="image-chunk") {
                    result=RpcImage(root,id,opened.Data);
                    lock(_rpcLock) {
                        var image=_rpcImage;
                        _imageUploadDiagnostic=$"{DateTimeOffset.Now:HH:mm:ss} transport={transport}; status={result.Status}; received={image?.Received??0}/{image?.Bytes.Length??0}; elapsed={ (image is null?0:Environment.TickCount64-image.Started)}ms";
                    }
                }
                else if(Text(root,"kind")=="gallery")result=BackgroundTransferPaused||ImageUploadActive||(_voice is Tab5VoiceHost galleryVoice&&galleryVoice.Busy)
                    ?(409,new{error="gallery_busy"}):_gallery.Handle(root,binaryReply,bulkReply);
                else if(Text(root,"kind")=="display-settings")result=ReceiveDisplaySettings(root);
                else if(Text(root,"kind")=="desktop")result=_voice is Tab5VoiceHost host&&host.Busy
                    ?(409,new{error="voice_busy"}):await QuickConsole.HandleAsync(root,token);
                else if(Text(root,"kind")=="benchmark")result=Tab5TransportBenchmark.Handle(root,opened.Data,binaryReply,bulkReply);
                else if(Text(root,"kind")=="voice") {
                    // Reuse the same voice session, replay checks and explicit
                    // review/send behavior. USB only changes its transport.
                    var voice=await VoiceAsync(id,nonce,Tab5Protocol.Proof(key,$"POST|/tab5/v1/voice|{id}|{nonce}|{digest}"),packet,token,compact:true);
                    if(voice.Status!=200||voice.Packet is null)result=(voice.Status,new{error="voice_rejected"});
                    else {using var decoded=JsonDocument.Parse(Tab5Protocol.Decrypt(key,nonce,voice.Packet));result=(200,decoded.RootElement.Clone());}
                }
                else if(Text(root,"kind")=="codex") {
                    string op=Text(root,"op");
                    // Same authenticated payload and journal as LAN, no duplicate
                    // sending implementation and no cross-link automatic retry.
                    string legacyProof=Tab5Protocol.Proof(key,$"POST|{id}|{nonce}|{digest}");
                    result=await SubmitCodexAsync(id,nonce,legacyProof,packet,token,op is "read" or "draft-load" or "receipt",rpcEnvelope:true);
                } else result=(400,new{error="invalid_operation"});
            }catch(Exception ex) when(ex is JsonException or FormatException or InvalidOperationException or ArgumentException) {
                result=(400,new{error="invalid_request"});
            }
            byte[] response=result.Body is Tab5RpcDataBody raw?
                Tab5RpcBinary.Pack(JsonSerializer.SerializeToUtf8Bytes(new{status=result.Status,body=raw.Metadata},JsonDefaults.Options),raw.Data.Span,bulk:bulkReply):
                JsonSerializer.SerializeToUtf8Bytes(new{status=result.Status,body=result.Body},JsonDefaults.Options);
            int maximum=bulkReply?Tab5RpcBinary.MailboxMaximum:Tab5Protocol.MaximumFrame;
            if(response.Length>maximum-28)response=JsonSerializer.SerializeToUtf8Bytes(new{status=413,body=new{error="response_too_large"}});
            if(binaryReply&&result.Body is not Tab5RpcDataBody)response=Tab5RpcBinary.Encode(response);
            return(200,Tab5Protocol.Encrypt(key,nonce,response,maximum));
        }finally{lock(_lifecycle)_rpcRequests--;}
    }
    private static string Text(JsonElement root,string key)=>root.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()!:"";
    private (int Status,object Body) RpcOta(JsonElement root,string transport,bool binaryReply,bool bulkReply)
    {
        var offer=Volatile.Read(ref _ota);
        if(offer is null||Text(root,"sha256")!=offer.Sha256||Text(root,"offerId")!=offer.OfferId)return(404,new{error="offer_changed"});
        if(!root.TryGetProperty("offset",out var o)||!o.TryGetInt32(out int offset)||!root.TryGetProperty("count",out var c)||!c.TryGetInt32(out int count)||
            offset<0||count<1||count>(bulkReply?Tab5RpcBinary.BulkData:transport=="USB"?16384:8192)||offset>offer.Image.Length-count)return(400,new{error="invalid_range"});
        // Keep removal/restart disabled between chunks. A lost device releases
        // the lease after 90 seconds; each authenticated chunk renews it.
        bool probe=Text(root,"kind")=="ota_probe";
        if(probe&&transport!="BLE")return(400,new{error="probe_requires_ble"});
        long now=Environment.TickCount64;
        if(!probe) {
            if(now>=Interlocked.Read(ref _rpcOtaUntil)){
                Interlocked.Exchange(ref _usbRpcGateWaitMs,0);Interlocked.Exchange(ref _usbRpcGateWaitPeakMs,0);
            }
            Interlocked.Exchange(ref _rpcOtaUntil,now+90000);
            _otaTransferDiagnostic=$"{transport}；已提供：{offset+count}/{offer.Image.Length}；等待设备校验";
        }
        return(200,binaryReply?Tab5OtaCompression.Range(offer.Image.AsMemory(offset,count),offset,
            (transport=="BLE"||transport=="USB")&&bulkReply&&Text(root,"acceptEncoding")=="zlib"):new{offset,data=Convert.ToBase64String(offer.Image,offset,count)});
    }
    private (int Status,object Body) RpcImage(JsonElement root,string device,ReadOnlyMemory<byte> raw)
    {
        string task=Text(root,"taskId"),id=Text(root,"requestId"),sha=Text(root,"sha256");
        if(!Guid.TryParse(task,out _)||!Guid.TryParse(id,out _)||sha.Length!=64||sha.Any(c=>!char.IsAsciiHexDigit(c))||
            !root.TryGetProperty("size",out var s)||!s.TryGetInt32(out int size)||size is <1 or >2097152||
            !root.TryGetProperty("offset",out var o)||!o.TryGetInt32(out int offset))return(400,new{error="invalid_image"});
        ReadOnlySpan<byte> chunk=raw.IsEmpty?Convert.FromBase64String(Text(root,"data")):raw.Span;
        if(chunk.Length<1||chunk.Length>(raw.IsEmpty?8192:Tab5RpcBinary.BulkData)||offset<0||offset>size-chunk.Length)return(400,new{error="invalid_range"});
        lock(_rpcLock) {
            if(_rpcImage is {} stale&&Environment.TickCount64-stale.Touched>90000)_rpcImage=null;
            var image=_rpcImage;
            if(offset==0&&(image is null||image.Device!=device||image.Task!=task||image.Id!=id))
                image=_rpcImage=new(device,task,id,size,sha);
            if(image is null||image.Device!=device||image.Task!=task||image.Id!=id||image.Bytes.Length!=size||image.Sha!=sha)
                return(409,new{error="image_transfer_changed"});
            if(offset==image.Received){chunk.CopyTo(image.Bytes.AsSpan(offset));image.Received+=chunk.Length;}
            else if(offset+chunk.Length>image.Received||!image.Bytes.AsSpan(offset,chunk.Length).SequenceEqual(chunk))return(409,new{error="image_offset"});
            image.Touched=Environment.TickCount64;
            if(offset+chunk.Length<size)return(200,new{offset=offset+chunk.Length});
            if(!Convert.ToHexString(SHA256.HashData(image.Bytes)).Equals(sha,StringComparison.OrdinalIgnoreCase)){_rpcImage=null;return(400,new{error="image_hash"});}
            // Existing decoder, pixel limits, device/task ownership and encrypted
            // on-disk storage remain authoritative. Repeated final chunks use its
            // existing image ID/content check and cannot duplicate an attachment.
            return _codexTasks.UploadImage(device,task,id,Convert.ToBase64String(image.Bytes));
        }
    }
}
