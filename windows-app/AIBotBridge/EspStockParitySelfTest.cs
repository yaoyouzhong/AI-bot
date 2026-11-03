using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class EspStockParitySelfTest
{
    private static void Check(bool condition,string name){if(!condition)throw new InvalidOperationException("ESP Windows: "+name);}
    internal static async Task RunAsync(string directory){
        Directory.CreateDirectory(directory);
        var cache=new EspUsbResourceCache();var kind=BinaryResourceKind.StockLabels;
        byte[] bytes=[1,2,3];string first=EspUsbResourceCache.Fingerprint(bytes);
        Check(!cache.Contains(kind,first),"unacknowledged or failed resource is not cached");
        cache.Confirm(kind,first);
        Check(cache.Contains(kind,EspUsbResourceCache.Fingerprint([1,2,3])),"same bytes with a new array or revision do not resend");
        bytes[1]=8;Check(!cache.Contains(kind,EspUsbResourceCache.Fingerprint(bytes)),"changed bytes resend even with unchanged revision");
        Check(!cache.Contains(BinaryResourceKind.WeatherText,first),"different kinds cannot share an ACK");
        cache.Forget(kind);
        Check(!cache.Contains(kind,first),"uncertain replacement cannot reuse an older ACK when content reverts");
        cache.Confirm(kind,first);
        cache.Clear();Check(!cache.Contains(kind,first),"reconnection requires a new device ACK");
        var telemetry=new EspUsbDiagnostics();telemetry.Attempt();telemetry.Connected();telemetry.ResourceStarted(kind,3);
        telemetry.ChunkWritten(42,false);telemetry.ChunkWritten(42,true);telemetry.ResourceFinished(kind,3,true,10);telemetry.ResourceSkipped();
        telemetry.Brightness(40,true);telemetry.Brightness(40,true);telemetry.StatusWritten();
        telemetry.Error(new IOException("fixture-private-detail"));
        telemetry.Received("fixture-private-noise");telemetry.Received("@AIBOT {broken-private-payload");
        telemetry.Received("@AIBOT {\"type\":\"device_info\",\"data\":\"fixture-private-account\"}");
        telemetry.Received("@AIBOT {\"type\":\"pong\"}");telemetry.Received("@AIBOT {\"type\":\"resource_ack\"}");
        telemetry.Received("fixture-private-fragment",drained:true);
        telemetry.ReadTimedOut(12);telemetry.PingFinished(true);telemetry.PingFinished(false);
        telemetry.InfoQueued();telemetry.InfoStarted(4000);telemetry.InfoFinished(false,true,6400);
        telemetry.InfoQueued();telemetry.InfoStarted(0);telemetry.InfoFinished(true,false,30);
        using(var metadata=JsonSerializer.SerializeToDocument(telemetry.Snapshot(true,true,"fixture"),JsonDefaults.Options)){
            var root=metadata.RootElement;
            Check(root.GetProperty("resourceTransfers").GetInt64()==1&&root.GetProperty("resourceAcknowledged").GetInt64()==1&&
                root.GetProperty("resourceRetries").GetInt64()==1&&root.GetProperty("resourceCachedSkips").GetInt64()==1,"transfer counters distinguish writes, retries and skips");
            Check(root.GetProperty("brightnessWrites").GetInt64()==2&&root.GetProperty("repeatedBrightnessRequests").GetInt64()==1,"brightness duplicates observed without dropping user delivery");
            Check(root.GetProperty("receivedLines").GetInt64()==5&&root.GetProperty("nonProtocolLines").GetInt64()==1&&root.GetProperty("invalidJsonLines").GetInt64()==1,
                "noise, malformed and complete reply lines remain distinguishable");
            Check(root.GetProperty("deviceInfoLines").GetInt64()==1&&root.GetProperty("pongLines").GetInt64()==1&&root.GetProperty("resourceAckLines").GetInt64()==1&&
                root.GetProperty("pingTimeouts").GetInt64()==1&&root.GetProperty("deviceInfoTimeouts").GetInt64()==1&&root.GetProperty("deviceInfoMatchedReplies").GetInt64()==1&&root.GetProperty("deviceInfoPending").GetInt64()==0,
                "reply arrival does not imply matched requests or successful queries");
            Check(root.GetProperty("pendingInputAtLastTimeout").GetInt32()==12,"partial input at a read timeout is observable");
            Check(!metadata.RootElement.GetRawText().Contains("fixture-private"),"private exception and wire payloads omitted");
        }
        for(int i=0;i<60;i++)telemetry.Brightness(i%101,true);
        telemetry.Received("Soft WDT reset\nException (0):\nepc1=0x40210c18 epc2=0x00000000\n>>>stack>>>\nctx: cont\n3ffffabc: 73656372 65742d64 61746100 40210000\nfixture-private-stack",drained:true);
        using(var metadata=JsonSerializer.SerializeToDocument(telemetry.Snapshot(true,true,null),JsonDefaults.Options))
        {
            Check(metadata.RootElement.GetProperty("events").GetArrayLength()==40,"bounded event history");
            var crash=metadata.RootElement.GetProperty("crash");
            Check(crash.GetProperty("watchdogMarkers").GetInt64()==1&&crash.GetProperty("exceptionMarkers").GetInt64()==1&&
                crash.GetProperty("lastEpc1").GetString()=="40210C18"&&crash.GetProperty("lastContext").GetString()=="cont","recognize fixed ESP postmortem headers from existing drains");
            Check(!metadata.RootElement.GetRawText().Contains("fixture-private")&&!metadata.RootElement.GetRawText().Contains("73656372"),"never retain stack memory or raw serial text");
        }
        await EndpointAsync(telemetry);
        await UsbManagementSelfTest.RunAsync();
        Console.WriteLine("ESP_USB_CONTENT_CACHE_OK byte_identity changed_content failed_ACK reconnect isolation metadata_only loopback_origin_guard");

        string[] cases=["600519","000001","AAPL","TSLA","ABCDEFGHJKLM"];
        for(int i=0;i<cases.Length;i++){
            var raster=EspStockCodeRaster.Raster(cases[i]);
            var expected=File.ReadAllBytes(Path.Combine(directory,$"stock-code-{i}.pgm"));
            var header=Encoding.ASCII.GetBytes("P5\n56 16\n255\n");
            Check(expected.Length==header.Length+56*16&&expected.AsSpan(0,header.Length).SequenceEqual(header),"native reference dimensions");
            for(int pixel=0;pixel<raster.Length;pixel++)
                Check(expected[header.Length+pixel]==((raster[pixel]>>11)&31)*255/31,"pixel parity with actual firmware renderer");
        }
        Check(EspStockCodeRaster.Raster("ABCDEFGHJKLM").SequenceEqual(EspStockCodeRaster.Raster("ABCDEFGZZZZZ")),"long symbols use the firmware truncation marker");
        Check(!EspStockCodeRaster.Raster("ABCDEFGH").SequenceEqual(EspStockCodeRaster.Raster("ABCDEFGHJKLM")),"eight characters remain visible before truncation");
        using(var bounded=new Bitmap(100,40)){
            using var graphics=Graphics.FromImage(bounded);graphics.Clear(Color.Magenta);EspStockCodeRaster.Draw(graphics,"TOO-LONG-STOCK-CODE",14,6);
            for(int y=0;y<40;y++)for(int x=0;x<100;x++)if(x<14||x>=70||y<6||y>=22)
                Check(bounded.GetPixel(x,y).ToArgb()==Color.Magenta.ToArgb(),"code cannot overwrite stock names or adjacent rows");
        }
        var quotes=new[]{new StockQuote("sh600519","600519","贵州茅台","1599.00","+0.80%",1),
            new StockQuote("usAAPL","AAPL","苹果","245.30","-0.50%",-1),
            new StockQuote("usBRK.B","BRK.B","伯克希尔","489.20","+0.30%",1),
            new StockQuote("usABCDEFG","ABCDEFG","七字符示例","12.30","0.00%",0),
            new StockQuote("usABCDEFGH","ABCDEFGH","八字符示例","16.50","+1.20%",1),
            new StockQuote("usABCDEFGHIJKL","ABCDEFGHIJKL","长代码截断示例","23.40","-1.20%",-1)};
        var stamp=DateTimeOffset.UnixEpoch;
        var status=new StatusSnapshot(1,"12:00",0,28800,stamp,new("idle",0),new("idle",0),Stocks:new(quotes,stamp,false));
        using(var firstPage=MirrorForm.RenderSnapshot(status,"stocks"))firstPage.Save(Path.Combine(directory,"windows-stock-page-1.png"));
        using(var secondPage=MirrorForm.RenderSnapshot(status with{EpochUtc=5},"stocks"))secondPage.Save(Path.Combine(directory,"windows-stock-page-2.png"));
        Console.WriteLine("ESP_STOCK_RASTER_OK shared_OFL_table exact_native_reference 8x16 compact8 truncation bounded_column offscreen_pages");
    }
    private static async Task EndpointAsync(EspUsbDiagnostics telemetry){
        var probe=new TcpListener(IPAddress.Loopback,0);probe.Start();int port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
        using var stop=new CancellationTokenSource();
        var server=new LocalStatusServer(port,espUsbInfo:()=>telemetry.Snapshot(true,true,"fixture"));
        var stamp=DateTimeOffset.UnixEpoch;
        var serving=server.RunAsync(()=>new(1,"12:00",0,28800,stamp,new("idle",0),new("idle",0)),stop.Token);
        try{
            using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(4)};
            using var allowed=await client.GetAsync($"http://127.0.0.1:{port}/diagnostics/esp-usb");
            Check(allowed.IsSuccessStatusCode,"loopback diagnostics available");
            using var request=new HttpRequestMessage(HttpMethod.Get,$"http://127.0.0.1:{port}/diagnostics/esp-usb");request.Headers.Add("Origin","https://fixture.invalid");
            using var blocked=await client.SendAsync(request);Check(blocked.StatusCode==HttpStatusCode.NotFound,"browser origin cannot read USB diagnostics");
        }finally{stop.Cancel();await serving;}
    }
}
