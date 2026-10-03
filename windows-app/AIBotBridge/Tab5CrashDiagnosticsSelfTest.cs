using System.Net;
using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5CrashDiagnosticsSelfTest
{
    internal static async Task RunAsync() {
        void Check(bool pass,string name){if(!pass)throw new InvalidOperationException(name);}
        var crashes=new Tab5CrashDiagnostics();
        Check(!crashes.Observe(100,1)&&crashes.Observe(10,4),"new panic boot needs capture");
        using(var trace=JsonDocument.Parse("{\"trace\":{\"crash\":1,\"cause\":27,\"stackGuard\":[[512,1,2,3],[0,0,0,0]]}}")) {
            crashes.Record("0.2.81-ui",trace.RootElement);
            Check(!crashes.Observe(11,4),"same boot is captured only once");
            for(int boot=0;boot<10;boot++){Check(crashes.Observe(1,4),"next panic boot is detected");crashes.Record("0.2.81-ui",trace.RootElement);crashes.Observe(100,4);}
        }
        using(var saved=JsonDocument.Parse(crashes.Snapshot))Check(saved.RootElement.GetArrayLength()==8&&saved.RootElement[7].GetProperty("trace").GetProperty("stackGuard")[0][0].GetInt32()==512,"bounded records survive source document disposal");
        var unavailable=new Tab5CrashDiagnostics();
        Check(unavailable.Observe(1,4)&&unavailable.Observe(2,4)&&unavailable.Observe(3,4)&&!unavailable.Observe(4,4),"failed capture attempts are bounded within a boot");
        using(var invalid=JsonDocument.Parse("{\"trace\":{\"crash\":true}}")) {
            bool rejected=false;try{unavailable.Record("0.2.81-ui",invalid.RootElement);}catch(IOException){rejected=true;}
            Check(rejected,"malformed diagnostics rejected");
        }
        var reserve=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);reserve.Start();int port=((IPEndPoint)reserve.LocalEndpoint).Port;reserve.Stop();
        using var stop=new CancellationTokenSource();var server=new LocalStatusServer(port,tab5Crashes:()=>crashes.Snapshot);
        var run=server.RunAsync(SessionActivityReader.Capture,stop.Token);
        using var http=new HttpClient(new HttpClientHandler{UseProxy=false});
        try {
            using var reply=await http.GetAsync($"http://127.0.0.1:{port}/diagnostics/tab5-crashes");
            Check(reply.StatusCode==HttpStatusCode.OK,"local crash endpoint unavailable");
            using var json=JsonDocument.Parse(await reply.Content.ReadAsStringAsync());Check(json.RootElement.GetArrayLength()==8,"local endpoint lost retained boots");
            using var request=new HttpRequestMessage(HttpMethod.Get,$"http://127.0.0.1:{port}/diagnostics/tab5-crashes");request.Headers.Add("Origin","https://example.org");
            using var blocked=await http.SendAsync(request);Check(blocked.StatusCode==HttpStatusCode.NotFound,"browser origin could read device fault records");
        }finally{stop.Cancel();await run;}
        Console.WriteLine("TAB5_CRASH_DIAGNOSTICS_OK multi-boot retention; bounded retry; JSON lifetime; local HTTP and origin restriction");
    }
}
