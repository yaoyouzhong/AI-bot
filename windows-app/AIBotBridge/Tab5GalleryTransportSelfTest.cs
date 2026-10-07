using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace AIBotBridge;

// Isolated synthetic profile. Real HTTP/router/crypto and USB/BLE mailbox pumps;
// no physical radios, user pairing records or installed galleries are touched.
internal static class Tab5GalleryTransportSelfTest
{
    internal static async Task RunAsync()
    {
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        var files=new Dictionary<string,byte[]>();
        foreach(string category in new[]{"painting","calligraphy"}) {
            string directory=Path.Combine(GalleryPack.Root,category);Directory.CreateDirectory(directory);
            string[] landscape=new string[3],portrait=new string[3];
            for(int page=0;page<3;page++)foreach(bool vertical in new[]{false,true}) {
                string name=$"{category}-{page}-{vertical}.jpg";
                using var bitmap=new Bitmap(1280,720,PixelFormat.Format24bppRgb);
                var bits=bitmap.LockBits(new(0,0,1280,720),ImageLockMode.WriteOnly,PixelFormat.Format24bppRgb);
                try {byte[] pixels=new byte[bits.Stride*720];new Random(files.Count+1).NextBytes(pixels);Marshal.Copy(pixels,0,bits.Scan0,pixels.Length);}
                finally{bitmap.UnlockBits(bits);}
                using var stream=new MemoryStream();bitmap.Save(stream,ImageFormat.Jpeg);
                byte[] bytes=stream.ToArray();Check(bytes.Length>49152&&bytes.Length<1048576,"Fixture must span bulk ranges");
                files.Add(name,bytes);File.WriteAllBytes(Path.Combine(directory,name),bytes);
                (vertical?portrait:landscape)[page]=name;
            }
            File.WriteAllText(Path.Combine(directory,"catalog.json"),JsonSerializer.Serialize(new[]{
                new Tab5Gallery.Artwork(category,category,"fixture","fixture","https://example.invalid/test","test",landscape,category,portrait)
            },JsonDefaults.Options));
        }
        var store=new Tab5PairingStore(Path.Combine(GalleryPack.Root,"test-pair.dat"));
        var pair=store.Pair("001122aabbcc",@"USB\GALLERY-TEST");byte[] key=Convert.FromBase64String(pair.Key);
        using var service=new Tab5Service(store);
        var snapshot=new StatusSnapshot(1,"12:00",1000,28800,DateTimeOffset.UtcNow,new("idle",0),new("idle",0));
        service.Publish(snapshot);using var state=JsonDocument.Parse(service.CurrentFrame!);
        string session=state.RootElement.GetProperty("session").GetString()!;
        var probe=new TcpListener(IPAddress.Loopback,0);probe.Start();int port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
        using var stop=new CancellationTokenSource();
        var server=new LanStatusServer(new(IPAddress.Loopback,port,"fixture"),tab5:service);
        var ready=new TaskCompletionSource();var serving=server.RunAsync(()=>snapshot,stop.Token,()=>ready.SetResult());
        using var http=new HttpClient();await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        int calls=0;
        async Task<(int Status,JsonElement Body,byte[] Data)> Call(string link,object input,bool bulk=true) {
            var root=JsonSerializer.SerializeToNode(input)!.AsObject();root["kind"]="gallery";root["session"]=session;
            root["issuedAt"]=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();root["binaryReply"]=true;root["bulkReply"]=bulk;
            string nonce=Guid.NewGuid().ToString("N");byte[] packet=Tab5Protocol.Encrypt(key,nonce,JsonSerializer.SerializeToUtf8Bytes(root));
            string proof=Tab5Protocol.Proof(key,$"POST|/tab5/v1/rpc|{pair.DeviceId}|{nonce}|{Convert.ToHexString(SHA256.HashData(packet)).ToLowerInvariant()}");
            byte[] reply;
            if(link=="Wi-Fi") {
                using var request=new HttpRequestMessage(HttpMethod.Post,$"http://127.0.0.1:{port}/tab5/v1/rpc"){Content=new ByteArrayContent(packet)};
                request.Headers.Add("X-AIBot-Device",pair.DeviceId);request.Headers.Add("X-AIBot-Nonce",nonce);request.Headers.Add("X-AIBot-Proof",proof);
                using var response=await http.SendAsync(request);Check(response.StatusCode==HttpStatusCode.OK,"HTTP gallery RPC failed");
                reply=await response.Content.ReadAsByteArrayAsync();
            } else {
                byte[] wire=Encoding.ASCII.GetBytes(nonce+proof).Concat(packet).ToArray();int at=0;
                using var received=new MemoryStream();int chunk=link=="USB"?Tab5UsbBinary.LargeChunk:488-9;
                var pump=new Tab5BleVoice(_=> {
                    int count=Math.Min(480,wire.Length-at);byte[] part=new byte[8+count];
                    BinaryPrimitives.WriteUInt32LittleEndian(part,1);BinaryPrimitives.WriteUInt16LittleEndian(part.AsSpan(4),(ushort)at);
                    BinaryPrimitives.WriteUInt16LittleEndian(part.AsSpan(6),(ushort)wire.Length);wire.AsSpan(at,count).CopyTo(part.AsSpan(8));return Task.FromResult(part);
                },(part,_)=> {
                    if(part[0]==1)at=BinaryPrimitives.ReadUInt16LittleEndian(part.AsSpan(5));
                    else {Check(part.Length<=chunk+9&&BinaryPrimitives.ReadUInt16LittleEndian(part.AsSpan(5))==received.Length,"Gallery mailbox fragment mismatch");received.Write(part,9,part.Length-9);}
                    return Task.CompletedTask;
                },(n,p,b,ct)=>service.RpcAsync(pair.DeviceId,n,p,b,ct,transport:link),517,65535,75,responseChunk:chunk,maximumRequest:65535);
                await pump.PumpAsync(stop.Token);Check(pump.CompletedCount==1,"Gallery mailbox did not finish");reply=received.ToArray();
            }
            var opened=Tab5RpcBinary.Open(Tab5Protocol.Decrypt(key,nonce,reply,65535),true);using var doc=opened.Metadata;calls++;
            return(doc.RootElement.GetProperty("status").GetInt32(),doc.RootElement.GetProperty("body").Clone(),opened.Data.ToArray());
        }
        try {
            var rejected=await Call("BLE",new{op="manifest",category="unknown",date="2026-10-07"});
            Check(rejected.Status==400&&!service.BleGalleryTransferActive,"Rejected request acquired BLE gallery priority");
            foreach(string link in new[]{"USB","Wi-Fi","BLE"}) {
                int images=0;
                foreach(string category in new[]{"painting","calligraphy"})foreach(string orientation in new[]{"landscape","portrait"})for(int page=0;page<3;page++) {
                    const string date="2026-10-07";
                    var manifest=await Call(link,new{op="manifest",category,orientation,date,frame=page});
                    Check(manifest.Status==200&&manifest.Body.GetProperty("orientation").GetString()==orientation,"Gallery layout missing");
                    Check(service.BleGalleryTransferActive==(link=="BLE")&&!service.ImageUploadActive,"Gallery priority changed another transport or blocked its own ranges");
                    int size=manifest.Body.GetProperty("size").GetInt32();string sha=manifest.Body.GetProperty("sha256").GetString()!;
                    using var data=new MemoryStream();
                    // Deny any second file read while serving ranges. Metadata
                    // checks remain available; payload and digest must be reused.
                    using var fileLock=File.Open(Path.Combine(GalleryPack.Root,category,$"{category}-{page}-{orientation=="portrait"}.jpg"),FileMode.Open,FileAccess.Read,FileShare.None);
                    for(int offset=0;offset<size;offset+=49152) {
                        var read=await Call(link,new{op="read",category,orientation,date,frame=page,offset,count=49152,sha256=sha});
                        Check(read.Status==200&&read.Body.GetProperty("offset").GetInt32()==offset&&read.Data.Length==Math.Min(49152,size-offset),"Gallery range incomplete");data.Write(read.Data);
                    }
                    byte[] expected=files[$"{category}-{page}-{orientation=="portrait"}.jpg"];
                    Check(data.ToArray().SequenceEqual(expected)&&Convert.ToHexString(SHA256.HashData(data.ToArray())).ToLowerInvariant()==sha,"Gallery bytes/hash mismatch");
                    data.Position=0;using var image=Image.FromStream(data);Check(image.Width==1280&&image.Height==720,"Gallery JPEG decode failed");images++;
                    var stale=await Call(link,new{op="read",category,orientation,date,frame=page,offset=0,count=8192,sha256="stale"});
                    Check(stale.Status==409&&stale.Data.Length==0,"Stale gallery revision accepted");
                    var legacy=await Call(link,new{op="read",category,orientation,date,frame=page,offset=0,count=8192,sha256=sha},false);
                    Check(legacy.Status==200&&legacy.Data.SequenceEqual(expected.Take(8192)),"Legacy gallery range mismatch");
                }
                Console.WriteLine($"GALLERY_TRANSPORT_OK {link}: {images} images, 2 categories, 2 orientations, 3 pages, 48 KiB/8 KiB ranges, SHA-256 and JPEG decode");
            }
            await Task.Delay(3100);
            Check(!service.BleGalleryTransferActive,"Gallery throughput preference did not expire");
            var invalidated=await Call("BLE",new{op="read",category="painting",orientation="portrait",date="2026-10-07",frame=0,offset=0,count=8192,sha256="stale"});
            Check(invalidated.Status==409&&!service.BleGalleryTransferActive,"Stale image request renewed BLE gallery priority");
            var old=await Call("USB",new{op="manifest",category="painting",orientation="portrait",date="2026-10-07",frame=0});
            string changedPath=Path.Combine(GalleryPack.Root,"painting","painting-0-True.jpg");
            byte[] changed=files["painting-0-True.jpg"].ToArray();changed[^1]^=1;
            DateTime stamp=File.GetLastWriteTimeUtc(changedPath);File.WriteAllBytes(changedPath,changed);File.SetLastWriteTimeUtc(changedPath,stamp.AddSeconds(2));
            var replaced=await Call("USB",new{op="read",category="painting",orientation="portrait",date="2026-10-07",frame=0,offset=0,count=8192,sha256=old.Body.GetProperty("sha256").GetString()});
            Check(replaced.Status==409,"Same-size image replacement did not invalidate cached digest");
            var fresh=await Call("USB",new{op="manifest",category="painting",orientation="portrait",date="2026-10-07",frame=0});
            Check(fresh.Body.GetProperty("sha256").GetString()==Convert.ToHexString(SHA256.HashData(changed)).ToLowerInvariant(),"Replaced image digest was stale");
            File.Delete(changedPath);
            var missing=await Call("USB",new{op="manifest",category="painting",orientation="portrait",date="2026-10-07",frame=0});
            Check(missing.Status==503,"Removed pack image was served from the bridge cache");
            Console.WriteLine("GALLERY_BRIDGE_CACHE_OK ranges survive exclusive file lock; same-size replacement and removal invalidate cached image");
            Console.WriteLine($"GALLERY_TRANSPORTS_SELF_TEST_OK {calls} authenticated requests; loopback HTTP and simulated USB/BLE; hardware acceptance remains required");
        } finally {stop.Cancel();await serving;}
    }
}
