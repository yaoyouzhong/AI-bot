using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;
internal static class Tab5ImageSelfTest
{
    internal static async Task RunAsync() {
        string root=Path.Combine(Path.GetTempPath(),"tab5-image-http-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            string task=Guid.NewGuid().ToString(),id=Guid.NewGuid().ToString();
            var store=new Tab5PairingStore(Path.Combine(root,"pair.dat"));var pair=store.Pair("001122aabbcc",@"USB\VID_303A&PID_1001\TEST");
            byte[] key=Convert.FromBase64String(pair.Key);
            var images=new Tab5CodexImages(Path.Combine(root,"images"));
            using var tasks=new Tab5CodexTasks(new Tab5CodexDesktop("unused-test-pipe"),()=>[new(task,"test","test",0)],new Tab5CodexJournal(Path.Combine(root,"draft.dat")),images);
            using var service=new Tab5Service(store,tasks);
            var snapshot=new StatusSnapshot(1,"12:00",1000,28800,DateTimeOffset.UtcNow,new("idle",0),new("idle",0));service.Publish(snapshot);
            using var frame=JsonDocument.Parse(service.CurrentFrame!);string session=frame.RootElement.GetProperty("session").GetString()!;
            byte[] bytes;using(var bitmap=new Bitmap(256,256))using(var stream=new MemoryStream()) {
                for(int y=0;y<256;y++)for(int x=0;x<256;x++)bitmap.SetPixel(x,y,Color.FromArgb(x,y,(x*y)%256));
                bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);bytes=stream.ToArray();
            }
            if(bytes.Length<8192)throw new Exception("Fixture must exercise the dedicated large-image route");
            using var reserve=new TcpListener(IPAddress.Loopback,0);reserve.Start();int port=((IPEndPoint)reserve.LocalEndpoint).Port;reserve.Stop();
            using var stop=new CancellationTokenSource();var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var server=new LanStatusServer(new(IPAddress.Loopback,port,"test"),tab5:service).RunAsync(()=>snapshot,stop.Token,()=>ready.SetResult());
            try {
                await ready.Task;using var client=new HttpClient();
                string nonce=Guid.NewGuid().ToString("N");
                var clear=JsonSerializer.SerializeToUtf8Bytes(new{op="image-upload",session,taskId=task,requestId=id,message="",image=Convert.ToBase64String(bytes),issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()});
                var packet=Tab5Protocol.Encrypt(key,nonce,clear,Tab5CodexImages.MaxPacket);
                var proof=Tab5Protocol.Proof(key,$"POST|{pair.DeviceId}|{nonce}|{Convert.ToHexString(SHA256.HashData(packet)).ToLowerInvariant()}");
                async Task<HttpStatusCode> Post(string route,string suppliedProof) {
                    using var request=new HttpRequestMessage(HttpMethod.Post,$"http://127.0.0.1:{port}/tab5/v1/codex/{route}"){Content=new ByteArrayContent(packet)};
                    request.Headers.Add("X-AIBot-Device",pair.DeviceId);request.Headers.Add("X-AIBot-Nonce",nonce);request.Headers.Add("X-AIBot-Proof",suppliedProof);
                    using var response=await client.SendAsync(request);return response.StatusCode;
                }
                if(await Post("turn",proof)!=HttpStatusCode.RequestEntityTooLarge)throw new Exception("Large image cannot use message route");
                if(await Post("image",new string('0',64))!=HttpStatusCode.Unauthorized)throw new Exception("Photo upload requires pairing proof");
                if(await Post("image",proof)!=HttpStatusCode.OK)throw new Exception("Encrypted image upload over real loopback TCP");
                if(await Post("image",proof)!=HttpStatusCode.Conflict)throw new Exception("Upload nonce replay rejected");
                string path=images.Resolve(pair.DeviceId,task,[id],true).Single();
                if(!File.ReadAllBytes(path).SequenceEqual(bytes))throw new Exception("Exact photo bytes survive encrypted HTTP and storage");
            } finally {stop.Cancel();await server;}
        }finally{Directory.Delete(root,true);}
        Console.WriteLine("TAB5_IMAGE_HTTP_OK large encrypted upload, route bounds, authentication, replay rejection and exact pixels (synthetic image only)");
    }
}
