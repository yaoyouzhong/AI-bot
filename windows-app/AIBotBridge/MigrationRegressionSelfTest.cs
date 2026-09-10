using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class MigrationRegressionSelfTest
{
    private static void Require(bool value,string name) { if(!value)throw new InvalidOperationException(name); }
    internal static async Task RunAsync()
    {
        var now=DateTimeOffset.Now;
        var original=LegacyPetImport.Read(Path.Combine(AppContext.BaseDirectory,"TestFixtures","claude_sprite.h"),"claude");
        using(var colors=original.BitmapAt(0))
            Require(colors.GetPixel(0,0).ToArgb()==Color.Red.ToArgb()&&colors.GetPixel(1,0).G==255&&colors.GetPixel(2,0).ToArgb()==Color.Blue.ToArgb(),"legacy pre-swapped RGB primary colors");
        var signals=new ActivitySignals();var idle=new ToolState("idle",0);
        Require(signals.Record("claude","PermissionRequest",at:now),"permission accepted");
        Require(signals.Apply("claude",idle,now).NeedsInput,"attention on");
        Require(!signals.Apply("claude",idle,now.AddMinutes(5)).NeedsInput,"attention expires");
        signals.Record("claude","PreToolUse",at:now);
        Require(!signals.Apply("claude",idle,now).NeedsInput&&signals.Apply("claude",idle,now).State=="working","work clears attention");
        signals.Record("codex","Stop",at:now);
        Require(!signals.Apply("codex",idle,now).CompletionActive,"Stop is not completion");
        signals.Record("codex","TaskComplete",at:now);
        Require(signals.Apply("codex",new("working",0),now).CompletionActive,"completion active");
        Require(signals.Apply("codex",new("working",1),now.AddSeconds(1)).CompletionActive,"stale working does not erase completion");
        Require(signals.Apply("codex",new("working",20),now.AddSeconds(20)).CompletionActive,"old working remains settled");
        signals.Acknowledge();Require(!signals.Apply("codex",idle,now).CompletionActive,"ack clears");
        signals.Record("codex","TaskComplete",at:now.AddSeconds(2));signals.Apply("codex",idle,now.AddSeconds(2));
        signals.Record("codex","PreToolUse",at:now.AddSeconds(3));Require(!signals.Apply("codex",idle,now.AddSeconds(3)).CompletionActive,"new work clears");
        Require(!signals.Record("unknown","TaskComplete"),"unknown agent rejected");
        string Row(object message,DateTimeOffset? at=null)=>JsonSerializer.Serialize(new{timestamp=(at??now).ToString("O"),message});
        var rows=new[]{ Row(new{model="qwen3",usage=new{input_tokens=10,output_tokens=20,cache_creation_input_tokens=30,cache_read_input_tokens=40}}),Row(new{model="claude-sonnet",usage=new{input_tokens=7,output_tokens=3}}),Row(new{model="qwen3",usage=new{input_tokens=999}},now.AddDays(-1)),Row(new{model="qwen3",usage=new{input_tokens="bad"}}),"[1,2]","{broken"};
        var usage=LocalUsageReader.Parse(rows,false,now);
        Require(usage["alibaba"].TokensToday==100&&usage["claude"].TokensToday==10,"local provider/day/cache accounting");
        string Codex(long count)=>JsonSerializer.Serialize(new{timestamp=now.ToString("O"),payload=new{type="token_count",info=new{total_token_usage=new{total_tokens=count}}}});
        Require(LocalUsageReader.Parse(new[]{Codex(100),Codex(110),Codex(110)},true,now)["codex"].TokensToday==110,"codex cumulative dedup");

        byte[] bytes=[0,1,127,128,255];var resource=new ResourcePayload(BinaryResourceKind.ClaudePetAnimation,1,bytes);
        var crc=BinaryResourceProtocol.Crc32(bytes);var path=$"/resources/10/{crc}";
        Require(LanResourceCatalog.Respond(path,[resource]).Body.SequenceEqual(bytes),"binary bytes unchanged");
        Require(LanResourceCatalog.Respond($"/resources/10/{crc^1}",[resource]).Status.StartsWith("409"),"stale revision refused");
        Require(LanResourceCatalog.Respond("/resources/../../secret",[resource]).Status.StartsWith("404"),"path traversal refused");
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
        const string token="migration-test-only-token-never-production";
        using var stop=new CancellationTokenSource();int reads=0;
        var server=new LanStatusServer(new(IPAddress.Loopback,port,token),()=>{reads++;return [resource];});
        var run=server.RunAsync(SessionActivityReader.Capture,stop.Token);
        try
        {
            using var http=new HttpClient{BaseAddress=new Uri($"http://127.0.0.1:{port}"),Timeout=TimeSpan.FromSeconds(10)};
            using(var rejected=await http.GetAsync("/resources"))Require(rejected.StatusCode==HttpStatusCode.Unauthorized&&reads==0,"auth before resource reads");
            http.DefaultRequestHeaders.Add("X-AIBot-Token",token);
            using(var catalog=JsonDocument.Parse(await http.GetStringAsync("/resources")))Require(catalog.RootElement.GetProperty("resources")[0].GetProperty("crc").GetUInt32()==crc,"catalog CRC");
            Require((await http.GetByteArrayAsync(path)).SequenceEqual(bytes),"LAN actual binary response");
        }
        finally {stop.Cancel();await run;}
        listener=new TcpListener(IPAddress.Loopback,0);listener.Start();port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
        using var localStop=new CancellationTokenSource();var local=new LocalStatusServer(port);var localRun=local.RunAsync(SessionActivityReader.Capture,localStop.Token);
        try
        {
            using var http=new HttpClient{BaseAddress=new Uri($"http://127.0.0.1:{port}"),Timeout=TimeSpan.FromSeconds(10)};
            using(var response=await http.PostAsync("/event",new StringContent("{\"agent\":\"claude\",\"event\":\"PermissionRequest\",\"message\":\"等待批准\"}",Encoding.UTF8,"application/json")))Require(response.IsSuccessStatusCode,"UTF8 hook accepted");
            using(var response=await http.PostAsync("/event",new StringContent("{\"agent\":42,\"event\":true}")))Require(!response.IsSuccessStatusCode,"invalid fields rejected without crash");
            http.DefaultRequestHeaders.Add("Origin","https://example.invalid");
            using(var response=await http.PostAsync("/completion/ack",new StringContent("")))Require(!response.IsSuccessStatusCode,"browser mutation rejected");
        }
        finally {localStop.Cancel();await localRun;}
        Console.WriteLine("MIGRATION_REGRESSION_OK signals tokens LAN-resource-auth-CRC loopback-hooks");
    }
}
