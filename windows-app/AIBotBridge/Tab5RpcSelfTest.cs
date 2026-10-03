using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;
internal static class Tab5RpcSelfTest
{
    internal static async Task RunAsync()
    {
        string root=Path.Combine(Path.GetTempPath(),"tab5-rpc-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            var store=new Tab5PairingStore(Path.Combine(root,"pair.dat"));var pair=store.Pair("001122aabbcc",@"USB\TEST");var key=Convert.FromBase64String(pair.Key);
            string task=Guid.NewGuid().ToString(),imageId=Guid.NewGuid().ToString();
            var images=new Tab5CodexImages(Path.Combine(root,"images"));
            using var tasks=new Tab5CodexTasks(new Tab5CodexDesktop("unused-rpc-test"),()=>[new(task,"test","test",0)],new Tab5CodexJournal(Path.Combine(root,"draft.dat")),images);
            using var service=new Tab5Service(store,tasks);
            service.Publish(new StatusSnapshot(1,"12:00",1000,28800,DateTimeOffset.UtcNow,new("idle",0),new("idle",0)));
            using var frame=JsonDocument.Parse(service.CurrentFrame!);string session=frame.RootElement.GetProperty("session").GetString()!;
            int calls=0;
            async Task<JsonDocument> Call(object value,int mtu=247,int fragment=480,int responseChunk=0,bool allowOta=true,bool binary=false,bool bulk=false) {
                // Firmware cJSON does not HTML-escape '+' in base64. Match the
                // production device bytes when checking the 12 KiB envelope.
                string nonce=Guid.NewGuid().ToString("N");byte[] clear=JsonSerializer.SerializeToUtf8Bytes(value,new JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
                if(binary){var obj=System.Text.Json.Nodes.JsonNode.Parse(clear)!.AsObject();obj["binaryReply"]=true;clear=Tab5RpcBinary.Encode(JsonSerializer.SerializeToUtf8Bytes(obj,JsonDefaults.Options),response:false);}
                if(bulk) {
                    var obj=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(value))!.AsObject();
                    obj["binaryReply"]=true;obj["bulkReply"]=true;
                    byte[] raw=obj["data"] is {} data?Convert.FromBase64String(data.GetValue<string>()):[];obj.Remove("data");
                    byte[] meta=JsonSerializer.SerializeToUtf8Bytes(obj,JsonDefaults.Options);
                    clear=raw.Length>0?Tab5RpcBinary.Pack(meta,raw,false,true):meta;
                }
                byte[] encrypted=Tab5Protocol.Encrypt(key,nonce,clear,bulk?Tab5RpcBinary.RequestMaximum:Tab5Protocol.MaximumFrame);
                string proof=Tab5Protocol.Proof(key,$"POST|/tab5/v1/rpc|{pair.DeviceId}|{nonce}|{Convert.ToHexString(SHA256.HashData(encrypted)).ToLowerInvariant()}");
                byte[] request=Encoding.ASCII.GetBytes(nonce+proof).Concat(encrypted).ToArray();int at=0;var response=new List<byte>();
                var pump=new Tab5BleVoice(ct=> {
                    int n=Math.Min(fragment,request.Length-at);byte[] p=new byte[n+8];BinaryPrimitives.WriteUInt32LittleEndian(p,27);
                    BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(4),(ushort)at);BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(6),(ushort)request.Length);request.AsSpan(at,n).CopyTo(p.AsSpan(8));return Task.FromResult(p);
                },(p,ct)=> {
                    if(p[0]==1)at=BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(5));
                    else {if(p.Length>(responseChunk>0?responseChunk+9:mtu-3)||BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(5))!=response.Count)throw new Exception("RPC fragment mismatch");response.AddRange(p[9..]);}
                    return Task.CompletedTask;
                },async(n,p,b,ct)=>{calls++;return await service.RpcAsync(pair.DeviceId,n,p,b,ct,allowOta);},mtu,bulk?65535:32768,75,fragment,responseChunk,maximumRequest:bulk?65535:12288);
                int before=calls;await pump.PumpAsync(CancellationToken.None);await pump.PumpAsync(CancellationToken.None);
                if(calls!=before+1||pump.CompletedCount!=1)throw new Exception("RPC executed twice or duplicate response counted as new work");
                var replay=await service.RpcAsync(pair.DeviceId,nonce,proof,encrypted,CancellationToken.None);
                using var replayJson=JsonDocument.Parse(Tab5Protocol.Decrypt(key,nonce,replay.Packet!));
                if(replayJson.RootElement.GetProperty("status").GetInt32()!=409)throw new Exception("RPC nonce replay accepted");
                if((await service.RpcAsync(pair.DeviceId,nonce,new string('0',64),encrypted,CancellationToken.None)).Status!=401)throw new Exception("Bad HMAC accepted");
                return JsonDocument.Parse(Tab5RpcBinary.Decode(Tab5Protocol.Decrypt(key,nonce,response.ToArray(),bulk?65535:32768),response:true));
            }
            long Now()=>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach(bool upload in new[]{false,true}) {
                object value=upload?new{kind="benchmark",session,issuedAt=Now(),offset=0,count=8192,data=Convert.ToBase64String(Tab5TransportBenchmark.Pattern(0,8192))}:
                    new{kind="benchmark",session,issuedAt=Now(),offset=0,count=8192};
                using var r=await Call(value,binary:true);
                if(r.RootElement.GetProperty("status").GetInt32()!=200)throw new Exception("Binary benchmark rejected");
                if(!upload&&!Convert.FromBase64String(r.RootElement.GetProperty("body").GetProperty("data").GetString()!).SequenceEqual(Tab5TransportBenchmark.Pattern(0,8192)))throw new Exception("Binary benchmark changed bytes");
            }
            foreach(int mtu in new[]{23,247,517})foreach(bool upload in new[]{false,true}) {
                byte[] data=Tab5TransportBenchmark.Pattern(0,49152);
                object value=upload?new{kind="benchmark",session,issuedAt=Now(),offset=0,count=data.Length,data=Convert.ToBase64String(data)}:
                    new{kind="benchmark",session,issuedAt=Now(),offset=0,count=data.Length};
                using var result=await Call(value,mtu,bulk:true);
                if(result.RootElement.GetProperty("status").GetInt32()!=200)throw new Exception("Bulk authenticated exchange failed");
                if(!upload&&!Convert.FromBase64String(result.RootElement.GetProperty("body").GetProperty("data").GetString()!).SequenceEqual(data))throw new Exception("Bulk authenticated bytes changed");
            }
            byte[] photo;using(var bmp=new Bitmap(256,256))using(var stream=new MemoryStream()) {
                for(int y=0;y<256;y++)for(int x=0;x<256;x++)bmp.SetPixel(x,y,Color.FromArgb(x,y,(x*y)%256));
                bmp.Save(stream,System.Drawing.Imaging.ImageFormat.Png);photo=stream.ToArray();
            }
            string sha=Convert.ToHexString(SHA256.HashData(photo)).ToLowerInvariant();
            for(int repeat=0;repeat<3;repeat++)
            for(int offset=0;offset<photo.Length;offset+=(repeat==0?8192:repeat==1?6144:49152)) {
                int count=Math.Min(repeat==0?8192:repeat==1?6144:49152,photo.Length-offset);
                using var r=await Call(new{kind="image-chunk",session,issuedAt=Now(),taskId=task,requestId=imageId,sha256=sha,size=photo.Length,offset,data=Convert.ToBase64String(photo,offset,count)},offset==0?23:247,allowOta:false,binary:repeat==1,bulk:repeat==2);
                if(r.RootElement.GetProperty("status").GetInt32()!=200)throw new Exception("Chunk upload failed: "+r.RootElement);
                if(service.ImageUploadActive!=(repeat==0&&offset+count<photo.Length))throw new Exception("Image pacing lease does not match pending bytes or completed replay");
            }
            if(!File.ReadAllBytes(images.Resolve(pair.DeviceId,task,[imageId],true).Single()).SequenceEqual(photo))throw new Exception("Attachment bytes changed");
            using(var r=await Call(new{kind="image-chunk",session,issuedAt=Now(),taskId=Guid.NewGuid().ToString(),requestId=imageId,sha256=sha,size=photo.Length,offset=6144,data=Convert.ToBase64String(photo,6144,100)}))
                if(r.RootElement.GetProperty("status").GetInt32()!=409)throw new Exception("Cross-task attachment accepted");
            using(var r=await Call(new{kind="codex",op="draft-save",session,issuedAt=Now(),taskId=task,requestId=Guid.NewGuid().ToString(),message="USB/BLE draft",images=new[]{imageId}},247,2048,2048))
                if(r.RootElement.GetProperty("status").GetInt32()!=200)throw new Exception("USB draft RPC failed");
            // Synthetic valid package header; real firmware identity is checked by
            // the production package loader, transfer bytes must remain exact.
            byte[] firmware=TestFirmwareImage.Create("0.2.39-test",99008);
            string path=Path.Combine(root,"app.bin");File.WriteAllBytes(path,firmware);service.OfferOta(path);
            var package=Tab5OtaPackage.Load(path);
            // Offer IDs are deliberately fresh on each load; obtain service offer.
            service.Publish(new StatusSnapshot(2,"12:00",1001,28800,DateTimeOffset.UtcNow,new("idle",0),new("idle",0)));
            using var offered=JsonDocument.Parse(service.CurrentFrame!);string offer=offered.RootElement.GetProperty("data").GetProperty("ota").GetProperty("offerId").GetString()!;
            using(var r=await Call(new{kind="ota",session,issuedAt=Now(),sha256=package.Sha256,offerId=offer,offset=0,count=8192},allowOta:false))
                if(r.RootElement.GetProperty("status").GetInt32()!=409||r.RootElement.GetProperty("body").GetProperty("error").GetString()!="firmware_requires_usb_or_wifi")throw new Exception("Slow BLE firmware transfer was allowed");
            foreach(int mtu in new[]{23,247})for(int offset=0;offset<firmware.Length;offset+=8192) {
                int count=Math.Min(8192,firmware.Length-offset);
                using var r=await Call(new{kind="ota",session,issuedAt=Now(),sha256=package.Sha256,offerId=offer,offset,count},mtu);
                if(r.RootElement.GetProperty("status").GetInt32()!=200||!Convert.FromBase64String(r.RootElement.GetProperty("body").GetProperty("data").GetString()!).SequenceEqual(firmware.AsSpan(offset,count).ToArray()))throw new Exception("Firmware range corrupted");
            }
            foreach(int responseChunk in new[]{2048,16384})for(int offset=0;offset<firmware.Length;offset+=16384) {
                int count=Math.Min(16384,firmware.Length-offset);
                using var r=await Call(new{kind="ota",session,issuedAt=Now(),sha256=package.Sha256,offerId=offer,offset,count},247,responseChunk,responseChunk,binary:true);
                if(r.RootElement.GetProperty("status").GetInt32()!=200||!Convert.FromBase64String(r.RootElement.GetProperty("body").GetProperty("data").GetString()!).SequenceEqual(firmware.AsSpan(offset,count).ToArray()))throw new Exception("USB 16K firmware range corrupted");
            }
            using(var r=await Call(new{kind="ota",session,issuedAt=Now(),sha256=package.Sha256,offerId=offer,offset=0,count=16385},247,16384,16384))
                if(r.RootElement.GetProperty("status").GetInt32()!=400)throw new Exception("Oversize USB range accepted");
            for(int offset=0;offset<firmware.Length;offset+=49152) {
                int count=Math.Min(49152,firmware.Length-offset);
                using var result=await Call(new{kind="ota",session,issuedAt=Now(),sha256=package.Sha256,offerId=offer,offset,count},517,16384,16384,bulk:true);
                if(result.RootElement.GetProperty("status").GetInt32()!=200||!Convert.FromBase64String(result.RootElement.GetProperty("body").GetProperty("data").GetString()!).SequenceEqual(firmware.AsSpan(offset,count).ToArray()))throw new Exception("Bulk OTA bytes changed");
            }
            service.CancelOta();
            using(var r=await Call(new{kind="ota",session,issuedAt=Now(),sha256=package.Sha256,offerId=offer,offset=0,count=8192}))
                if(r.RootElement.GetProperty("status").GetInt32()!=404)throw new Exception("Cancelled offer still transferred");
            Console.WriteLine("TAB5_RPC_OK MTU23/247/USB, authenticated encryption, no duplicate execution, replay rejection, image ownership/exact bytes, draft journal, OTA ranges/cancel (simulated transports)");
        }finally{Directory.Delete(root,true);}
    }
}
