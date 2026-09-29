using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;

internal sealed partial class Tab5Service
{
    private readonly object _rpcLock=new();
    private readonly Dictionary<string,long> _rpcNonces=[];
    private int _rpcRequests;
    private long _rpcOtaUntil;
    private Tab5RpcImage? _rpcImage;
    private void ObserveFirmware(string id,string version) {
        _firmware.Observe(id,version);
        if(Volatile.Read(ref _ota)?.Version==version)Interlocked.Exchange(ref _rpcOtaUntil,0);
    }
    private sealed class Tab5RpcImage(string device,string task,string id,int size,string sha)
    {
        internal readonly string Device=device,Task=task,Id=id,Sha=sha;
        internal readonly byte[] Bytes=new byte[size];
        internal int Received;
        internal long Touched=Environment.TickCount64;
    }
    internal async Task<(int Status,byte[]? Packet)> RpcAsync(string id,string nonce,string proof,byte[] packet,CancellationToken token,bool allowOta=true,string transport="USB")
    {
        var pair=_store.Current;
        if(pair is null||pair.DeviceId!=id||!Tab5Protocol.ValidNonce(nonce)||packet.Length is <28 or >12192)return(401,null);
        var key=Convert.FromBase64String(pair.Key);
        var digest=Convert.ToHexString(SHA256.HashData(packet)).ToLowerInvariant();
        if(!Tab5Protocol.Verify(key,$"POST|/tab5/v1/rpc|{id}|{nonce}|{digest}",proof))return(401,null);
        byte[] clear;
        try{clear=Tab5Protocol.Decrypt(key,nonce,packet);}catch(CryptographicException){return(401,null);}
        lock(_lifecycle){if(_stopping)return(503,null);_rpcRequests++;}
        try {
            (int Status,object Body) result;
            try {
                using var doc=JsonDocument.Parse(clear);var root=doc.RootElement;
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
                else if(Text(root,"kind")=="ota")result=allowOta?RpcOta(root,transport):(409,new{error="firmware_requires_usb_or_wifi"});
                else if(Text(root,"kind")=="image-chunk")result=RpcImage(root,id);
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
            byte[] response=JsonSerializer.SerializeToUtf8Bytes(new{status=result.Status,body=result.Body},JsonDefaults.Options);
            if(response.Length>32740)response=JsonSerializer.SerializeToUtf8Bytes(new{status=413,body=new{error="response_too_large"}});
            return(200,Tab5Protocol.Encrypt(key,nonce,response));
        }finally{lock(_lifecycle)_rpcRequests--;}
    }
    private static string Text(JsonElement root,string key)=>root.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()!:"";
    private (int Status,object Body) RpcOta(JsonElement root,string transport)
    {
        var offer=Volatile.Read(ref _ota);
        if(offer is null||Text(root,"sha256")!=offer.Sha256||Text(root,"offerId")!=offer.OfferId)return(404,new{error="offer_changed"});
        if(!root.TryGetProperty("offset",out var o)||!o.TryGetInt32(out int offset)||!root.TryGetProperty("count",out var c)||!c.TryGetInt32(out int count)||
            offset<0||count<1||count>(transport=="USB"?16384:8192)||offset>offer.Image.Length-count)return(400,new{error="invalid_range"});
        // Keep removal/restart disabled between chunks. A lost device releases
        // the lease after 90 seconds; each authenticated chunk renews it.
        Interlocked.Exchange(ref _rpcOtaUntil,Environment.TickCount64+90000);
        _otaTransferDiagnostic=$"{transport}；已提供：{offset+count}/{offer.Image.Length}；等待设备校验";
        return(200,new{offset,data=Convert.ToBase64String(offer.Image,offset,count)});
    }
    private (int Status,object Body) RpcImage(JsonElement root,string device)
    {
        string task=Text(root,"taskId"),id=Text(root,"requestId"),sha=Text(root,"sha256");
        if(!Guid.TryParse(task,out _)||!Guid.TryParse(id,out _)||sha.Length!=64||sha.Any(c=>!char.IsAsciiHexDigit(c))||
            !root.TryGetProperty("size",out var s)||!s.TryGetInt32(out int size)||size is <1 or >2097152||
            !root.TryGetProperty("offset",out var o)||!o.TryGetInt32(out int offset))return(400,new{error="invalid_image"});
        byte[] chunk=Convert.FromBase64String(Text(root,"data"));
        if(chunk.Length is <1 or >6144||offset<0||offset>size-chunk.Length)return(400,new{error="invalid_range"});
        lock(_rpcLock) {
            if(_rpcImage is {} stale&&Environment.TickCount64-stale.Touched>90000)_rpcImage=null;
            var image=_rpcImage;
            if(offset==0&&(image is null||image.Device!=device||image.Task!=task||image.Id!=id))
                image=_rpcImage=new(device,task,id,size,sha);
            if(image is null||image.Device!=device||image.Task!=task||image.Id!=id||image.Bytes.Length!=size||image.Sha!=sha)
                return(409,new{error="image_transfer_changed"});
            if(offset==image.Received){chunk.CopyTo(image.Bytes,offset);image.Received+=chunk.Length;}
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
