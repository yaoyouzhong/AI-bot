using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5OtaCompressionSelfTest
{
    internal static async Task RunAsync(string imagePath,string directory) {
        var package=Tab5OtaPackage.Load(imagePath);Directory.CreateDirectory(directory);
        string temporary=Path.Combine(Path.GetTempPath(),"tab5-zlib-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temporary);
        var vectors=new List<object>();long encodedBytes=0,wireBytes=0;int compressed=0;var watch=Stopwatch.StartNew();
        try {
            var store=new Tab5PairingStore(Path.Combine(temporary,"pair.dat"));var pair=store.Pair("001122aabbcc",@"USB\TEST");var key=Convert.FromBase64String(pair.Key);
            string task=Guid.NewGuid().ToString();
            using var tasks=new Tab5CodexTasks(new Tab5CodexDesktop("unused-compression-test"),()=>[new(task,"test","test",0)],new Tab5CodexJournal(Path.Combine(temporary,"draft.dat")),new Tab5CodexImages(Path.Combine(temporary,"images")));
            using var service=new Tab5Service(store,tasks);service.OfferOta(imagePath);
            service.Publish(new StatusSnapshot(1,"12:00",1000,28800,DateTimeOffset.UtcNow,new("idle",0),new("idle",0)));
            using var frame=JsonDocument.Parse(service.CurrentFrame!);
            string session=frame.RootElement.GetProperty("session").GetString()!,offer=frame.RootElement.GetProperty("data").GetProperty("ota").GetProperty("offerId").GetString()!;
            async Task<(JsonDocument Metadata,byte[] Data)> Call(int offset,int count,string? encoding="zlib",string transport="BLE",string? offerId=null,bool allow=true,bool probe=false) {
                string nonce=Guid.NewGuid().ToString("N");
                var request=JsonSerializer.SerializeToUtf8Bytes(new{kind=probe?"ota_probe":"ota",session,issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),sha256=package.Sha256,
                    offerId=offerId??offer,offset,count,binaryReply=true,bulkReply=true,acceptEncoding=encoding},JsonDefaults.Options);
                var cipher=Tab5Protocol.Encrypt(key,nonce,request,65535);
                string proof=Tab5Protocol.Proof(key,$"POST|/tab5/v1/rpc|{pair.DeviceId}|{nonce}|{Convert.ToHexString(SHA256.HashData(cipher)).ToLowerInvariant()}");
                var result=await service.RpcAsync(pair.DeviceId,nonce,proof,cipher,CancellationToken.None,allowOta:allow,transport:transport);
                if(result.Status!=200||result.Packet is null)throw new Exception("Authenticated range failed");
                wireBytes+=result.Packet.Length;
                var opened=Tab5RpcBinary.Open(Tab5Protocol.Decrypt(key,nonce,result.Packet,65535),response:true);
                var replay=await service.RpcAsync(pair.DeviceId,nonce,proof,cipher,CancellationToken.None,allowOta:allow,transport:transport);
                using var rejected=JsonDocument.Parse(Tab5Protocol.Decrypt(key,nonce,replay.Packet!,65535));
                if(rejected.RootElement.GetProperty("status").GetInt32()!=409)throw new Exception("Compressed request replay accepted");
                if((await service.RpcAsync(pair.DeviceId,nonce,new string('0',64),cipher,CancellationToken.None,transport:transport)).Status!=401)throw new Exception("Invalid compression HMAC accepted");
                return(opened.Metadata,opened.Data.ToArray());
            }
            void SaveVector(JsonElement body,byte[] data,ReadOnlySpan<byte> expected,int offset) {
                string name=$"range-{vectors.Count:D4}";
                File.WriteAllBytes(Path.Combine(directory,name+".wire"),data);
                File.WriteAllBytes(Path.Combine(directory,name+".raw"),expected.ToArray());
                File.WriteAllText(Path.Combine(directory,name+".json"),body.GetRawText());
                vectors.Add(new{name,offset,count=expected.Length,packed=data.Length});
            }
            for(int offset=0;offset<package.Image.Length;offset+=Tab5RpcBinary.BulkData) {
                int count=Math.Min(Tab5RpcBinary.BulkData,package.Image.Length-offset);
                var response=await Call(offset,count,probe:true);using var metadata=response.Metadata;
                if(service.BackgroundTransferPaused)throw new Exception("RAM preflight acquired an OTA lease");
                if(metadata.RootElement.GetProperty("status").GetInt32()!=200)throw new Exception("Range status failed");
                var body=metadata.RootElement.GetProperty("body");byte[] decoded=response.Data;
                if(body.TryGetProperty("encoding",out var encoding)) {
                    if(encoding.GetString()!="zlib"||body.GetProperty("decodedSize").GetInt32()!=count||decoded.Length+96>=count)throw new Exception("Compression range bounds invalid");
                    using var input=new MemoryStream(decoded);using var inflate=new ZLibStream(input,CompressionMode.Decompress);using var output=new MemoryStream();inflate.CopyTo(output);decoded=output.ToArray();compressed++;
                }
                if(body.GetProperty("offset").GetInt32()!=offset||!decoded.AsSpan().SequenceEqual(package.Image.AsSpan(offset,count)))throw new Exception("Decoded OTA range differs from source image");
                encodedBytes+=response.Data.Length;SaveVector(body,response.Data,package.Image.AsSpan(offset,count),offset);
            }
            long fullWireBytes=wireBytes;
            var wrongTransport=await Call(0,49152,transport:"USB",probe:true);using(wrongTransport.Metadata)
                if(wrongTransport.Metadata.RootElement.GetProperty("status").GetInt32()!=400)throw new Exception("RAM preflight silently allowed another transport");
            string success=$"complete;BLE,down,1,{package.Image.Length},60000,0,140,0,0,0,1,1,1,1,1;image,{package.Sha256},{package.Image.Length};";
            if(Tab5FirmwareProbe.Seconds(success,package.Sha256,package.Image.Length)!=97)throw new Exception("Full-image gate estimate invalid");
            foreach(string invalid in new[]{success.Replace("complete;","cancelled;"),success.Replace("complete;","failed;"),success.Replace(",60000,0,",",110000,0,"),
                success.Replace(",60000,0,",",60000,-6,"),success.Replace("BLE,","USB,"),success.Replace("image,"+package.Sha256,"image,"+new string('0',64)),success+"extra;"})
                if(Tab5FirmwareProbe.Seconds(invalid,package.Sha256,package.Image.Length)!=null)throw new Exception("Invalid full-image result passed gate");
            foreach(var mode in new[]{(Encoding:(string?)null,Transport:"BLE"),(Encoding:(string?)"unknown",Transport:"BLE"),(Encoding:(string?)null,Transport:"USB")}) {
                var response=await Call(0,49152,mode.Encoding,mode.Transport);using var metadata=response.Metadata;var body=metadata.RootElement.GetProperty("body");
                if(body.TryGetProperty("encoding",out _)||!response.Data.AsSpan().SequenceEqual(package.Image.AsSpan(0,49152)))throw new Exception("Legacy compatibility changed");
            }
            var usbCompressed=await Call(0,49152,"zlib","USB");using(usbCompressed.Metadata)
                if(usbCompressed.Metadata.RootElement.GetProperty("body").GetProperty("encoding").GetString()!="zlib")throw new Exception("Negotiated USB compression missing");
            foreach(string transport in new[]{"USB","BLE"})for(int offset=0;offset<package.Image.Length;offset+=49152) {
                int count=Math.Min(49152,package.Image.Length-offset);
                var response=await Call(offset,count,transport:transport);using var metadata=response.Metadata;
                var body=metadata.RootElement.GetProperty("body");byte[] decoded=response.Data;
                if(body.TryGetProperty("encoding",out _)) {
                    using var input=new MemoryStream(decoded);using var inflate=new ZLibStream(input,CompressionMode.Decompress);
                    using var output=new MemoryStream();inflate.CopyTo(output);decoded=output.ToArray();
                }
                if(!decoded.AsSpan().SequenceEqual(package.Image.AsSpan(offset,count)))throw new Exception(transport+" full OTA image mismatch");
            }
            byte[] wifiStream=Tab5OtaCompression.Stream(package.Image);
            File.WriteAllBytes(Path.Combine(directory,"wifi-stream.bin"),wifiStream);
            foreach(var negotiation in new[]{("",Tab5OtaCompression.StreamEncoding),("tcp-v1",Tab5OtaCompression.StreamEncoding),("tcp-v2",""),("tcp-v2","unknown"),("tcp-v2",Tab5OtaCompression.StreamEncoding)}) {
                using var wire=new MemoryStream();byte[] source=package.Image[..1024];
                await service.TransferOtaAsync(wire,source,pair.DeviceId,negotiation.Item1,CancellationToken.None,negotiation.Item2);
                byte[] reply=wire.ToArray();string text=System.Text.Encoding.ASCII.GetString(reply);
                int boundary=text.IndexOf("\r\n\r\n",StringComparison.Ordinal);string headers=text[..boundary];
                bool compressedHttp=negotiation.Item1=="tcp-v2"&&negotiation.Item2==Tab5OtaCompression.StreamEncoding;
                byte[] expected=compressedHttp?Tab5OtaCompression.Stream(source):source;
                if(headers.Contains("X-AIBot-OTA-Encoding:")!=compressedHttp||!headers.Contains($"Content-Length: {expected.Length}")||!reply.AsSpan(boundary+4).SequenceEqual(expected))throw new Exception("HTTP compression negotiation changed raw compatibility");
            }
            foreach(var fault in new[]{(Offset:-1,Count:16,Offer:offer,Allow:true,Status:400),(Offset:0,Count:49153,Offer:offer,Allow:true,Status:400),
                (Offset:package.Image.Length-1,Count:2,Offer:offer,Allow:true,Status:400),(Offset:0,Count:16,Offer:"stale",Allow:true,Status:404),(Offset:0,Count:16,Offer:offer,Allow:false,Status:409)}) {
                var response=await Call(fault.Offset,fault.Count,offerId:fault.Offer,allow:fault.Allow,probe:true);using var metadata=response.Metadata;
                if(metadata.RootElement.GetProperty("status").GetInt32()!=fault.Status||response.Data.Length!=0)throw new Exception("Invalid OTA range/offer/transport accepted");
            }
            foreach(int size in new[]{1,28,235,479,8192,49152})foreach(bool random in new[]{false,true}) {
                byte[] source=random?RandomNumberGenerator.GetBytes(size):new byte[size];var range=Tab5OtaCompression.Range(source,0,true);
                using var metadata=JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(range.Metadata));
                if(random&&range.Data.Length!=source.Length)throw new Exception("Incompressible data expanded or unexpectedly compressed");
                SaveVector(metadata.RootElement,range.Data.ToArray(),source,0);
            }
            File.WriteAllText(Path.Combine(directory,"vectors.json"),JsonSerializer.Serialize(vectors));
            var report=new{image=Path.GetFileName(imagePath),package.Sha256,rawBytes=package.Image.Length,encodedBytes,encryptedReplyBytes=fullWireBytes,wifiFramedBytes=wifiStream.Length,compressedRanges=compressed,
                vectors=vectors.Count,elapsedMs=watch.ElapsedMilliseconds,remainingRatio=(double)encodedBytes/package.Image.Length,
                evidence="Authenticated in-process RPC plus PC zlib roundtrip; native firmware range decoder checked separately; no hardware, Flash or radio measurement."};
            string json=JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true});File.WriteAllText(Path.Combine(directory,"report.json"),json);
            Console.WriteLine(json);Console.WriteLine("TAB5_OTA_COMPRESSION_OK full_USB_BLE_authenticated_images_WiFi_negotiation_replay_HMAC_legacy_raw_fallback_bounds");
        } finally {Directory.Delete(temporary,true);}
    }
}
