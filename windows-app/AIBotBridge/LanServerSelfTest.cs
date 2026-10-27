using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;

namespace AIBotBridge;

internal static class LanServerSelfTest
{
    internal static async Task RunAsync()
    {
        await VerifyTcpUploadPolicyAsync();
        var port = ReservePort();
        const string token = "self-test-token-that-is-never-used-in-production";
        using var shutdown = new CancellationTokenSource();
        string? firmware="not-observed";var observed=0;
        var server = new LanStatusServer(new LanPairing(IPAddress.Loopback, port, token),legacyFirmware:value=>{firmware=EspFirmwareVersion.Read(value);Interlocked.Increment(ref observed);});
        var now=DateTimeOffset.UtcNow;
        var fixture=new StatusSnapshot(1,"12:00",now.ToUnixTimeSeconds(),28800,now,new("idle",null),new("idle",null),
            Weather:new("测试城市","晴",20,25,15,50,100,null,null,"fixture",now,false) {
                Hourly=Enumerable.Range(0,24).Select(i=>new WeatherHour(now.AddHours(i).ToString("O"),"晴",20,10)).ToArray(),
                Daily=Enumerable.Range(0,7).Select(i=>new WeatherDay(now.AddDays(i).ToString("yyyy-MM-dd"),"晴",15,25,10,0)).ToArray()},
            Stocks:new([new("test","000001","完整中文名称","10.00","+1.00",1)],now,false),
            SystemMetrics:new(12,34,100,200,now){SampleSequence=2,Samples=[new(100,200),new(101,201)]},
            RuntimeHealth:new("fixture",1,now,Enumerable.Range(0,8).Select(i=>new RuntimeHealthCheck("test"+i,"诊断","ok",new string('x',400),"detail","advice",now)).ToArray(),[]));
        var original=JsonSerializer.Serialize(fixture,JsonDefaults.Options);
        var serverTask = server.RunAsync(()=>fixture, shutdown.Token);

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            await WaitUntilListeningAsync(client);
            await ExpectStatusAsync(client, null, HttpStatusCode.Unauthorized);
            await ExpectStatusAsync(client, "wrong-token", HttpStatusCode.Unauthorized);
            if(observed!=0)throw new Exception("Unauthenticated firmware observed");
            await ExpectStatusAsync(client, token, HttpStatusCode.OK, requireSnapshot: true);
            using(var request=new HttpRequestMessage(HttpMethod.Get,"/status")) {
                request.Headers.Add("X-AIBot-Token",token);
                using var response=await client.SendAsync(request);response.EnsureSuccessStatusCode();
                var text=await response.Content.ReadAsStringAsync();using var doc=JsonDocument.Parse(text);var root=doc.RootElement;
                if(root.TryGetProperty("runtimeHealth",out _)||root.GetProperty("weather").TryGetProperty("hourly",out _)||root.GetProperty("weather").TryGetProperty("daily",out _))
                    throw new Exception("ESP LAN includes unused desktop/TAB5 views");
                if(root.GetProperty("stocks").GetProperty("quotes")[0].GetProperty("name").GetString()!="完整中文名称"||
                    root.GetProperty("systemMetrics").GetProperty("samples").GetArrayLength()!=2||root.GetProperty("weather").GetProperty("temperature").GetDouble()!=20)
                    throw new Exception("ESP LAN lost rendered labels, current weather or incremental graph samples");
                if(System.Text.Encoding.UTF8.GetByteCount(text)>=6144||original.Length<text.Length*2||JsonSerializer.Serialize(fixture,JsonDefaults.Options)!=original)
                    throw new Exception("ESP payload reduction or source snapshot isolation regressed");
                Console.WriteLine($"ESP_LAN_PAYLOAD_OK full={System.Text.Encoding.UTF8.GetByteCount(original)} esp={System.Text.Encoding.UTF8.GetByteCount(text)} labels/samples retained; source unchanged");
            }
            for(int i=0;i<50&&observed<1;i++)await Task.Delay(10);
            if(firmware!=EspFirmwareVersion.Legacy)throw new Exception("Legacy firmware header not handled");
            using(var request=new HttpRequestMessage(HttpMethod.Get,"/status")) {
                request.Headers.Add("X-AIBot-Token",token);request.Headers.Add("X-AIBot-Firmware","0.5.1");
                using var response=await client.SendAsync(request);response.EnsureSuccessStatusCode();
                for(int i=0;i<50&&observed<3;i++)await Task.Delay(10);
                if(firmware!="0.5.1")throw new Exception("Authenticated firmware not observed");
            }
            using(var request=new HttpRequestMessage(HttpMethod.Get,"/status")) {
                request.Headers.Add("X-AIBot-Token","wrong-token");request.Headers.Add("X-AIBot-Firmware","9.0.0");
                using var response=await client.SendAsync(request);
                if(response.StatusCode!=HttpStatusCode.Unauthorized||firmware!="0.5.1")throw new Exception("Unauthorized version replaced firmware");
            }
            Console.WriteLine("LAN_SELF_TEST_OK");
        }
        finally
        {
            shutdown.Cancel();
            await serverTask;
        }
    }

    private static int ReservePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task VerifyTcpUploadPolicyAsync()
    {
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        try {
            using var client=new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback,((IPEndPoint)listener.LocalEndpoint).Port);
            using var accepted=await listener.AcceptTcpClientAsync();
            string policy=Tab5TcpUpload.Configure(accepted.Client);
            if(policy!=Tab5TcpUpload.Configure(accepted.Client))throw new Exception("TCP upload policy changed on reuse");
            byte[] expected=Enumerable.Range(0,262144).Select(i=>(byte)(i*31+17)).ToArray();
            var read=new byte[expected.Length];
            var receive=accepted.GetStream().ReadExactlyAsync(read).AsTask();
            for(int offset=0;offset<expected.Length;offset+=997)
                await client.GetStream().WriteAsync(expected.AsMemory(offset,Math.Min(997,expected.Length-offset)));
            await receive.WaitAsync(TimeSpan.FromSeconds(5));
            if(!expected.AsSpan().SequenceEqual(read))throw new Exception("TCP upload bytes changed");
            using var udp=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);
            if(!Tab5TcpUpload.Configure(udp).StartsWith("ack=default;",StringComparison.Ordinal))
                throw new Exception("Unsupported ACK option did not retain default policy");
            Console.WriteLine($"TCP_UPLOAD_SOCKET_OK {policy}; 256 KiB exact fragmented upload; socket reuse; unsupported-option fallback");
        }finally {listener.Stop();}
    }

    private static async Task WaitUntilListeningAsync(HttpClient client)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using var response = await client.GetAsync("/status");
                return;
            }
            catch (HttpRequestException) when (attempt < 19)
            {
                await Task.Delay(25);
            }
        }
    }

    private static async Task ExpectStatusAsync(
        HttpClient client, string? token, HttpStatusCode expected, bool requireSnapshot = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/status");
        if (token is not null)
            request.Headers.Add("X-AIBot-Token", token);
        using var response = await client.SendAsync(request);
        if (response.StatusCode != expected)
            throw new InvalidOperationException($"Expected {(int)expected}, got {(int)response.StatusCode}.");
        if (!requireSnapshot)
            return;

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (document.RootElement.GetProperty("version").GetInt32() != 1)
            throw new InvalidOperationException("Authenticated response was not a version 1 snapshot.");
    }
}
