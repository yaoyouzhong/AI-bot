using System.Security.Cryptography;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;

namespace AIBotBridge;
internal static class Tab5DisplaySettingsSelfTest
{
    internal static async Task RunAsync() {
        AppPaths.BeginPublicSelfTest();
        var store=new Tab5PairingStore();var pair=store.Pair("001122aabbcc",@"USB\TEST");byte[] key=Convert.FromBase64String(pair.Key);
        using var tasks=new Tab5CodexTasks(new Tab5CodexDesktop("unused-display-test"),()=>[],new Tab5CodexJournal());
        using var service=new Tab5Service(store,tasks);
        var snapshot=new StatusSnapshot(1,"12:00",DateTimeOffset.UtcNow.ToUnixTimeSeconds(),0,DateTimeOffset.UtcNow,new("idle",null),new("idle",null));
        service.Publish(snapshot);
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(20));var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server=new LanStatusServer(new(IPAddress.Loopback,port,"unused"),tab5:service,legacyEnabled:()=>false);
        var serving=server.RunAsync(()=>snapshot,stop.Token,()=>ready.SetResult());await ready.Task;
        using var http=new HttpClient();
        try {
        string nonce=Guid.NewGuid().ToString("N");
        if(service.Respond(pair.DeviceId,nonce,Tab5Protocol.Proof(key,$"GET|{pair.DeviceId}|{nonce}")) is null)throw new Exception("Test authentication failed");
        var expected=new Tab5DisplaySettings("1234abcd",75,35,false,-1,true,15,5,true,[1,1,1,1,1,1,1,0],[0,1,2,3,4,5,6,7]);
        async Task<(int Status,byte[]? Packet)> Reply(Task<Tab5DisplaySettings> pending,bool ok,string error="") {
            using var frame=JsonDocument.Parse(service.CurrentFrame!);
            var command=frame.RootElement.GetProperty("data").GetProperty("connectionHealth").GetProperty("displayCommand");
            if(command.ValueKind!=JsonValueKind.Object)throw new Exception("Settings request waited for periodic publication");
            using var selected=JsonDocument.Parse(service.TelemetryFrame(2,true,true,true)!);
            JsonDocument? unpacked=null;
            try {
                var wire=selected.RootElement;
                if(wire.GetProperty("type").GetString()=="tab5_packed") {
                    using var bytes=new MemoryStream(Convert.FromBase64String(wire.GetProperty("payload").GetString()!));
                    using var unzip=new System.IO.Compression.ZLibStream(bytes,System.IO.Compression.CompressionMode.Decompress);
                    unpacked=JsonDocument.Parse(unzip);wire=unpacked.RootElement;
                }
                if(wire.GetProperty("data").GetProperty("connectionHealth").GetProperty("displayCommand").GetProperty("requestId").GetString()!=command.GetProperty("requestId").GetString())throw new Exception("Interactive telemetry deferred settings");
            }finally{unpacked?.Dispose();}
            string session=frame.RootElement.GetProperty("session").GetString()!;
            string n=Guid.NewGuid().ToString("N");
            byte[] packet=Tab5Protocol.Encrypt(key,n,JsonSerializer.SerializeToUtf8Bytes(new {kind="display-settings",session,issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),requestId=command.GetProperty("requestId").GetString(),ok,error,settings=expected},JsonDefaults.Options));
            var proof=Tab5Protocol.Proof(key,$"POST|/tab5/v1/rpc|{pair.DeviceId}|{n}|{Convert.ToHexString(SHA256.HashData(packet)).ToLowerInvariant()}");
            var bad=await service.RpcAsync(pair.DeviceId,n,"invalid",packet,CancellationToken.None);if(bad.Status!=401||pending.IsCompleted)throw new Exception("Unauthenticated settings accepted");
            using var request=new HttpRequestMessage(HttpMethod.Post,$"http://127.0.0.1:{port}/tab5/v1/rpc"){Content=new ByteArrayContent(packet)};
            request.Headers.Add("X-AIBot-Device",pair.DeviceId);request.Headers.Add("X-AIBot-Nonce",n);request.Headers.Add("X-AIBot-Proof",proof);
            using var response=await http.SendAsync(request);byte[] result=await response.Content.ReadAsByteArrayAsync();
            if(response.StatusCode!=HttpStatusCode.OK)throw new Exception("Wi-Fi settings report failed: "+response.StatusCode);
            using var decoded=JsonDocument.Parse(Tab5Protocol.Decrypt(key,n,result));
            return((int)response.StatusCode,result);
        }
        var read=service.DisplaySettingsAsync(null,CancellationToken.None);await Reply(read,true);
        if((await read).Brightness!=75)throw new Exception("Readback lost device settings");
        var save=service.DisplaySettingsAsync(expected with {Brightness=80},CancellationToken.None);await Reply(save,false,"conflict");
        try{await save;throw new Exception("Conflict was treated as saved");}catch(IOException ex) when(ex.Message.Contains("改变")){}
        using var cancel=new CancellationTokenSource();var waiting=service.DisplaySettingsAsync(null,cancel.Token);cancel.Cancel();
        try{await waiting;throw new Exception("Cancel not observed");}catch(OperationCanceledException){}
        if(service.Busy)throw new Exception("Cancelled settings leaked busy state");
        try{(expected with {Order=[0,0,2,3,4,5,6,7]}).Validate();throw new Exception("Duplicate page accepted");}catch(InvalidDataException){}
        Console.WriteLine("TAB5_DISPLAY_SETTINGS_OK encrypted Wi-Fi HTTP readback, authentication, conflict, cancellation, page validation");
        }finally{stop.Cancel();await serving;}
    }
}
