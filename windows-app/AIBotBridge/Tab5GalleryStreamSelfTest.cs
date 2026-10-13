using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5GalleryStreamSelfTest
{
    internal static void Run(string directory)
    {
        RunProtocolAsync().GetAwaiter().GetResult();
        byte[] key=Enumerable.Range(0,32).Select(i=>(byte)i).ToArray();string nonce=new('a',32);
        foreach(var range in new[]{(0,1),(0,49152),(0,1048576),(196608,700000)}) {
            byte[] bytes=Enumerable.Range(0,range.Item2).Select(i=>(byte)((i+range.Item1)*31+17)).ToArray();
            byte[] wire=Tab5GalleryStream.Encode(new(new{offset=range.Item1,count=bytes.Length},bytes),key,nonce);
            int at=16,received=0,blocks=BinaryPrimitives.ReadInt32LittleEndian(wire.AsSpan(12));
            for(int block=0;block<blocks;block++) {
                int size=BinaryPrimitives.ReadInt32LittleEndian(wire.AsSpan(at));at+=4;
                byte[] encrypted=wire.AsSpan(at,size).ToArray();at+=size;
                var decoded=Tab5RpcBinary.Open(Tab5Protocol.Decrypt(key,nonce,encrypted,65535),true);
                using(decoded.Metadata) {
                    var body=decoded.Metadata.RootElement.GetProperty("body");
                    if(body.GetProperty("block").GetInt32()!=block||body.GetProperty("offset").GetInt32()!=range.Item1+received||
                       body.GetProperty("streamBytes").GetInt32()!=bytes.Length||!decoded.Data.Span.SequenceEqual(bytes.AsSpan(received,decoded.Data.Length)))throw new Exception("Gallery stream order/content changed");
                    received+=decoded.Data.Length;
                }
                encrypted[^1]^=1;
                try{Tab5Protocol.Decrypt(key,nonce,encrypted,65535);throw new Exception("Tampered stream accepted");}catch(CryptographicException){}
            }
            if(at!=wire.Length||received!=bytes.Length)throw new Exception("Stream framing length mismatch");
            File.WriteAllBytes(Path.Combine(directory,$"gallery-stream-{range.Item1}-{bytes.Length}.bin"),wire);
        }
        // Same nonce is AAD, not the AES IV; independently random IVs are kept.
        byte[] first=Tab5GalleryStream.Encode(new(new{offset=0},new byte[100000]),key,nonce);
        byte[] second=Tab5GalleryStream.Encode(new(new{offset=0},new byte[100000]),key,nonce);
        if(first.AsSpan(20,12).SequenceEqual(second.AsSpan(20,12)))throw new Exception("Stream reused AES IV");
        Console.WriteLine("GALLERY_STREAM_WIRE_OK bounded independently authenticated blocks; exact order/ranges; 1 MiB continuous reply; partial-offset vectors; tamper rejection; independent AES IVs");
    }
    internal static async Task RunProtocolAsync()
    {
        string sample=Tab5Service.FormatBenchmark("complete;gallery,Wi-Fi,8,8388608,8000,6000,3000,42,0");
        if(!sample.Contains("1024.0 KiB/s",StringComparison.Ordinal)||!sample.Contains("8/8",StringComparison.Ordinal)||!sample.Contains("42 次",StringComparison.Ordinal))throw new Exception("Gallery benchmark effective-byte/timing report changed");
        if(Tab5Service.FormatBenchmark("running;gallery,USB,0,0,0,0,0,0,0").Contains("NaN",StringComparison.Ordinal))throw new Exception("Empty gallery timing divided by zero");
        string profile=Tab5Service.FormatBenchmark("complete;gallery_io,2097152,1048576,100,200,30,8,3144704,1024;gallery_rpc,10,20,30,40,8");
        if(!profile.Contains("读卡 100 ms / 2.00 MiB",StringComparison.Ordinal)||!profile.Contains("落盘 30 ms（8 次）",StringComparison.Ordinal)||!profile.Contains("等待写卡 40 ms",StringComparison.Ordinal))throw new Exception("Gallery stage profile lost units/fields");
        string receive=Tab5Service.FormatBenchmark("complete;gallery_rx,100,200,8,65536;usb_rx,2097152,256,8192,10,20,250,45,128,1,32,62");
        if(!receive.Contains("并行接收 8 次",StringComparison.Ordinal)||!receive.Contains("缓冲最高 64.0 KiB",StringComparison.Ordinal)||!receive.Contains("USB 收包 2048.0 KiB",StringComparison.Ordinal)||!receive.Contains("接收唤醒最长 45 μs",StringComparison.Ordinal))throw new Exception("Receive pipeline diagnostics lost units or were parsed as generic speed samples");
        foreach(string bad in new[]{"gallery_io,1,2,3","gallery_rpc,1,2,3,4,-1","gallery_rpc,1,2,3,4,4294967296","gallery_rx,1,2,3,-1","usb_rx,1,2,3,4,5,6,7,8,9,10,4294967296"})
            if(!Tab5Service.FormatBenchmark("complete;"+bad).Contains("分段耗时数据无效",StringComparison.Ordinal))throw new Exception("Invalid gallery profile accepted");
        foreach(string counters in new[]{"0,0,0,0","1048576,1048576,243,17","4294967295,4294967295,4294967295,4294967295"}) {
            var value=JsonSerializer.SerializeToElement(new{galleryTransferDiag=counters});
            if(Tab5GalleryStream.TransferDiagnostic(value)!=counters)throw new Exception("Real gallery counters rejected as a versioned probe");
        }
        foreach(string counters in new[]{"-1,0,0,0","4294967296,0,0,0","0,0,0","0,0,0,0,0","1,2,x,4"," 0,0,0,0"}) {
            var value=JsonSerializer.SerializeToElement(new{galleryTransferDiag=counters});
            if(Tab5GalleryStream.TransferDiagnostic(value)!="invalid")throw new Exception("Invalid gallery counter accepted");
        }
        Console.WriteLine("GALLERY_TRANSFER_DIAGNOSTIC_OK idle and transferred bytes; unsigned bounds; no version prefix");
        string root=Path.Combine(Path.GetTempPath(),"aibot-stream-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            byte[] image=Enumerable.Range(0,1048576).Select(i=>(byte)(i*31+17)).ToArray();
            File.WriteAllBytes(Path.Combine(root,"image.jpg"),image);
            var work=new Tab5Gallery.Artwork("fixture","painting","fixture","fixture","fixture","CC0",["image.jpg"],"fixture",["image.jpg"]);
            File.WriteAllText(Path.Combine(root,"catalog.json"),JsonSerializer.Serialize(new[]{work},JsonDefaults.Options));
            var store=new Tab5PairingStore(Path.Combine(root,"pair.dat"));var pair=store.Pair("001122aabbcc",@"USB\TEST");
            byte[] key=Convert.FromBase64String(pair.Key);using var tasks=new Tab5CodexTasks(new Tab5CodexDesktop("unused-stream-fixture"),()=>[],new Tab5CodexJournal(Path.Combine(root,"draft.dat")),new Tab5CodexImages(Path.Combine(root,"images")));
            using var service=new Tab5Service(store,tasks,new Tab5Gallery(root));
            service.Publish(new StatusSnapshot(1,"12:00",1000,28800,DateTimeOffset.UtcNow,new("idle",0),new("idle",0)));
            using var state=JsonDocument.Parse(service.CurrentFrame!);string session=state.RootElement.GetProperty("session").GetString()!;
            foreach(string transport in new[]{"Wi-Fi","USB","BLE"}) {
                string nonce=Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
                byte[] clear=JsonSerializer.SerializeToUtf8Bytes(new{kind="gallery",op="read",category="painting",date="2026-10-12",frame=0,offset=196608,count=851968,sha256=Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant(),session,issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),binaryReply=true,bulkReply=true,streamReply=true});
                byte[] packet=Tab5Protocol.Encrypt(key,nonce,clear);
                string proof=Tab5Protocol.Proof(key,$"POST|/tab5/v1/rpc|{pair.DeviceId}|{nonce}|{Convert.ToHexString(SHA256.HashData(packet)).ToLowerInvariant()}");
                var response=await service.RpcAsync(pair.DeviceId,nonce,proof,packet,CancellationToken.None,transport:transport);
                if(response.Status!=200||response.Packet is null)throw new Exception("Authenticated gallery read failed");
                if(transport=="Wi-Fi") {
                    if(!response.Packet.AsSpan(0,4).SequenceEqual("T5S1"u8))throw new Exception("Authenticated Wi-Fi stream not selected");
                    int at=16,received=0,block=0;
                    while(at<response.Packet.Length) {
                        int size=BinaryPrimitives.ReadInt32LittleEndian(response.Packet.AsSpan(at));at+=4;
                        var data=Tab5RpcBinary.Open(Tab5Protocol.Decrypt(key,nonce,response.Packet.AsSpan(at,size).ToArray(),65535),true);at+=size;
                        using(data.Metadata) {
                            if(data.Metadata.RootElement.GetProperty("body").GetProperty("block").GetInt32()!=block++||!data.Data.Span.SequenceEqual(image.AsSpan(196608+received,data.Data.Length)))throw new Exception("Protocol stream range changed");
                            received+=data.Data.Length;
                        }
                    }
                    if(received!=851968)throw new Exception("Protocol stream truncated");
                } else {
                    using var rejected=JsonDocument.Parse(Tab5Protocol.Decrypt(key,nonce,response.Packet,65535));
                    if(rejected.RootElement.GetProperty("status").GetInt32()!=400)throw new Exception("USB/BLE bounds widened by Wi-Fi capability");
                }
                var replay=await service.RpcAsync(pair.DeviceId,nonce,proof,packet,CancellationToken.None,transport:transport);
                using var replayDoc=JsonDocument.Parse(Tab5Protocol.Decrypt(key,nonce,replay.Packet!,65535));
                if(replayDoc.RootElement.GetProperty("status").GetInt32()!=409)throw new Exception("Stream replay accepted");
                if((await service.RpcAsync(pair.DeviceId,nonce,new string('0',64),packet,CancellationToken.None,transport:transport)).Status!=401)throw new Exception("Stream bad HMAC accepted");
            }
            Console.WriteLine("GALLERY_STREAM_RPC_OK genuine request/session/time/HMAC/AES/replay checks; immutable SHA/range; Wi-Fi capability only; USB/BLE original bounds");
        } finally {Directory.Delete(root,true);}
    }
}
