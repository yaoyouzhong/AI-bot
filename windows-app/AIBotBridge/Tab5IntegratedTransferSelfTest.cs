using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIBotBridge;
internal static class Tab5IntegratedTransferSelfTest
{
    internal static void Run(string directory) {
        Directory.CreateDirectory(directory);
        Tab5BulkTransferSelfTest.Run(directory);
        var baseline=new JsonObject{["version"]=1,["type"]="tab5_status",["deviceId"]="001122334455",["session"]="delta-test",["sequence"]=1000,
            ["data"]=new JsonObject{["epochMilliseconds"]=1000,["codex"]=new JsonObject{["keep"]=new string('x',16000),["nested"]=new JsonObject{["old"]=true,["nullable"]=1}},["claude"]=new JsonObject()}};
        var next=baseline.DeepClone();next["sequence"]=1001;next["data"]!["epochMilliseconds"]=2000;
        var nested=next["data"]!["codex"]!["nested"]!.AsObject();nested.Remove("old");nested["nullable"]=null;nested["array"]=new JsonArray(1,2,3);
        byte[] Encode(JsonNode node)=>JsonSerializer.SerializeToUtf8Bytes(node,JsonDefaults.Options);
        var delta=Tab5TelemetryDelta.Create(Encode(baseline),Encode(next))??throw new Exception("Delta missing");
        if(delta.Length>1000)throw new Exception("Unchanged catalog was copied into delta");
        File.WriteAllBytes(Path.Combine(directory,"delta-baseline.json"),Encode(baseline));
        File.WriteAllBytes(Path.Combine(directory,"delta-update.json"),delta);
        File.WriteAllBytes(Path.Combine(directory,"delta-expected.json"),Encode(next));
        next["session"]="other";if(Tab5TelemetryDelta.Create(Encode(baseline),Encode(next)) is not null)throw new Exception("Cross-session delta");
        next["session"]="delta-test";next["data"]!["ota"]=new JsonObject{["version"]="new"};
        if(Tab5TelemetryDelta.Create(Encode(baseline),Encode(next)) is not null)throw new Exception("OTA offer skipped its full-frame consumer");
        byte[] header=new byte[16];"AIB2"u8.CopyTo(header);BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4),42);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8),16393);BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12),1);
        if(Tab5UsbBinary.Length(header,42)!=16393)throw new Exception("USB maximum changed");
        for(int fault=0;fault<4;fault++) {
            var bad=header.ToArray();if(fault==0)bad[0]++;if(fault==1)bad[4]++;if(fault==2)bad[8]++;if(fault==3)bad[12]=0;
            try{Tab5UsbBinary.Length(bad,42);throw new Exception("USB invalid response accepted");}catch(IOException){}
        }
        foreach(int length in new[]{1,8192}) {
            using var request=JsonDocument.Parse(JsonSerializer.Serialize(new{offset=8192,count=length,data=Convert.ToBase64String(Tab5TransportBenchmark.Pattern(8192,length))}));
            if(Tab5TransportBenchmark.Handle(request.RootElement).Status!=200)throw new Exception("Benchmark upload rejected");
        }
        using(var wrong=JsonDocument.Parse("{\"offset\":0,\"count\":1,\"data\":\"AA==\"}"))if(Tab5TransportBenchmark.Handle(wrong.RootElement).Status==200)throw new Exception("Corrupt benchmark bytes accepted");
        foreach(int fragment in new[]{1,7,512}) {
            var source=Tab5TransportBenchmark.Pattern(0,Tab5UsbBinary.Maximum);int cursor=0;
            var actual=Tab5UsbBinary.ReadAsync((buffer,at,want)=>{int n=Math.Min(fragment,Math.Min(want,source.Length-cursor));Array.Copy(source,cursor,buffer,at,n);cursor+=n;return n;},source.Length,Environment.TickCount64+2000,CancellationToken.None).GetAwaiter().GetResult();
            if(!actual.SequenceEqual(source))throw new Exception("Fragmented USB binary bytes changed");
        }
        try{Tab5UsbBinary.ReadAsync((_,_,_)=>0,16,Environment.TickCount64+5,CancellationToken.None).GetAwaiter().GetResult();throw new Exception("Truncated USB stream accepted");}catch(IOException){}
        using(var stopped=new CancellationTokenSource()) {stopped.Cancel();try{Tab5UsbBinary.ReadAsync((_,_,_)=>throw new Exception("Cancelled read executed"),16,Environment.TickCount64+1000,stopped.Token).GetAwaiter().GetResult();throw new Exception("USB cancel ignored");}catch(OperationCanceledException){}}
        VerifyIoAsync().GetAwaiter().GetResult();
        foreach(int size in new[]{1,8192,16384}) {
            byte[] payload=Tab5TransportBenchmark.Pattern(0,size);
            byte[] json=JsonSerializer.SerializeToUtf8Bytes(new{status=200,body=new{offset=0,data=Convert.ToBase64String(payload)}},JsonDefaults.Options);
            byte[] wire=Tab5RpcBinary.Encode(json);
            var opened=Tab5RpcBinary.Open(wire,true);using(var meta=opened.Metadata) {
                if(!opened.Data.Span.SequenceEqual(payload)||meta.RootElement.GetProperty("body").TryGetProperty("data",out _))throw new Exception("Raw RPC path reconstructed data");
                if(!Tab5RpcBinary.Pack(JsonSerializer.SerializeToUtf8Bytes(meta.RootElement,JsonDefaults.Options),opened.Data.Span).SequenceEqual(wire))throw new Exception("Raw RPC format changed");
            }
            using var decoded=JsonDocument.Parse(Tab5RpcBinary.Decode(wire,true));
            if(!Convert.FromBase64String(decoded.RootElement.GetProperty("body").GetProperty("data").GetString()!).SequenceEqual(payload))throw new Exception("RPC binary bytes changed");
            File.WriteAllBytes(Path.Combine(directory,$"rpc-binary-{size}.bin"),wire);
            var bad=wire.ToArray();bad[4]=255;bad[5]=255;
            try{Tab5RpcBinary.Decode(bad,true);throw new Exception("RPC metadata bound ignored");}catch(ArgumentException){}
        }
        using(var benchmark=JsonDocument.Parse("{\"offset\":0,\"count\":8192}")) {
            var raw=Tab5TransportBenchmark.Pattern(0,8192);
            if(Tab5TransportBenchmark.Handle(benchmark.RootElement,raw,true).Status!=200)throw new Exception("Raw upload failed");
            raw[4096]^=1;
            if(Tab5TransportBenchmark.Handle(benchmark.RootElement,raw,true).Status!=400)throw new Exception("Raw corruption accepted");
            var download=Tab5TransportBenchmark.Handle(benchmark.RootElement,default,true);
            if(download.Body is not Tab5RpcDataBody body||!body.Data.Span.SequenceEqual(Tab5TransportBenchmark.Pattern(0,8192)))throw new Exception("Raw download failed");
        }
        Console.WriteLine($"TAB5_INTEGRATED_TRANSFER_OK delta={delta.Length} baseline={Encode(baseline).Length}; session/OTA fallback; USB identity/status/bounds; benchmark byte integrity");
    }
    private static async Task VerifyIoAsync() {
        byte[] nativeSource=Tab5TransportBenchmark.Pattern(0,8192);int nativeAt=0,nativeCalls=0;
        byte[] native=Tab5UsbBinary.ReadBlocking((buffer,at,want,timeout)=> {
            if(timeout is <1 or >50)throw new Exception("Native USB wait is not cancellation bounded");
            if(++nativeCalls==1)throw new TimeoutException();
            int n=Math.Min(want,317);Array.Copy(nativeSource,nativeAt,buffer,at,n);nativeAt+=n;return n;
        },nativeSource.Length,Environment.TickCount64+1000,CancellationToken.None);
        if(!native.SequenceEqual(nativeSource))throw new Exception("Native USB partial read changed bytes");
        using(var cancelNative=new CancellationTokenSource()) {
            int reads=0;
            try{Tab5UsbBinary.ReadBlocking((_,_,_,_)=>{reads++;cancelNative.Cancel();throw new TimeoutException();},16,Environment.TickCount64+1000,cancelNative.Token);throw new Exception("Native USB cancellation ignored");}catch(OperationCanceledException){}
            if(reads!=1)throw new Exception("Cancelled USB worker consumed another transaction");
        }
        try{Tab5UsbBinary.ReadBlocking((_,_,_,timeout)=>{Thread.Sleep(timeout);throw new TimeoutException();},16,Environment.TickCount64+5,CancellationToken.None);throw new Exception("Native USB deadline ignored");}catch(IOException){}
        // Both an event before await and an event after await must wake exactly
        // once, preserve partial bytes, and leave cancellation bounded.
        foreach(bool early in new[]{false,true}) {
            using var signal=new SemaphoreSlim(0,1);int available=early?16:0;
            if(early)signal.Release();
            var pending=Tab5UsbBinary.ReadAsync((b,at,want)=>{int n=Math.Min(Volatile.Read(ref available),want);if(n==0)return 0;Array.Fill(b,(byte)42,at,n);Interlocked.Add(ref available,-n);return n;},16,Environment.TickCount64+2000,CancellationToken.None,ct=>signal.WaitAsync(ct));
            if(!early){await Task.Yield();Volatile.Write(ref available,16);signal.Release();}
            if(!(await pending).All(x=>x==42))throw new Exception("USB event wake lost bytes");
        }
        using(var cancel=new CancellationTokenSource(30))try{await Tab5UsbBinary.ReadAsync((_,_,_)=>0,16,Environment.TickCount64+2000,cancel.Token,ct=>Task.Delay(Timeout.Infinite,ct));throw new Exception("USB waiting cancellation ignored");}catch(OperationCanceledException){}
        byte[] body=Tab5TransportBenchmark.Pattern(0,8192);
        var header=System.Text.Encoding.ASCII.GetBytes("POST /a HTTP/1.1\r\nContent-Length: 8192\r\n\r\n");
        var next=System.Text.Encoding.ASCII.GetBytes("GET /b HTTP/1.1\r\n\r\n");
        using var stream=new MemoryStream(header.Concat(body).Concat(next).ToArray());var reader=new HttpConnectionReader(stream);
        if((await reader.ReadHeaderAsync(CancellationToken.None))[0]!="POST /a HTTP/1.1")throw new Exception("HTTP header changed");
        byte[] actual=new byte[body.Length];await reader.ReadExactlyAsync(actual,CancellationToken.None);
        if(!actual.SequenceEqual(body)||(await reader.ReadHeaderAsync(CancellationToken.None))[0]!="GET /b HTTP/1.1")throw new Exception("HTTP read-ahead lost body/next request");
        using var truncated=new MemoryStream(new byte[3]);
        try{await new HttpConnectionReader(truncated).ReadExactlyAsync(new byte[4],CancellationToken.None);throw new Exception("HTTP truncated body accepted");}catch(EndOfStreamException){}
    }
}
