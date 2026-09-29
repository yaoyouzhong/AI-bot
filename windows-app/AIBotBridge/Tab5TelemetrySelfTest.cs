using System.IO.Compression;
using System.Text.Json;

namespace AIBotBridge;
internal static class Tab5TelemetrySelfTest
{
    internal static void Run(string? vectors=null)
    {
        var directory=Path.Combine(Path.GetTempPath(),"tab5-telemetry-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try {
            var store=new Tab5PairingStore(Path.Combine(directory,"pair.dat"));store.Pair("001122334455",@"USB\TEST");
            using var tasks=new Tab5CodexTasks(new Tab5CodexDesktop(),()=>[],new Tab5CodexJournal(Path.Combine(directory,"draft.dat")));
            using var service=new Tab5Service(store,tasks);
            long sample=1;
            service.SystemMetricsCapture=()=>new(9,55,100,200,DateTimeOffset.UtcNow){SampleSession="metrics-test",SampleSequence=sample,Samples=[new(100,200)]};
            var snapshot=new StatusSnapshot(1,"12:00",1000,28800,DateTimeOffset.UtcNow,new("idle",0),new("idle",0));
            service.Publish(snapshot);
            byte[] full=service.CurrentFrame!;
            if(!ReferenceEquals(service.TelemetryFrame(1,false),full))throw new Exception("Legacy firmware changed wire format");
            if(!ReferenceEquals(service.TelemetryFrame(0,true,false),full))throw new Exception("Unsafe 0.2.40/0.2.41 receiver got compressed state");
            byte[] packed=service.TelemetryFrame(1,true)!;
            using var envelope=JsonDocument.Parse(packed);
            if(envelope.RootElement.GetProperty("type").GetString()!="tab5_packed")throw new Exception("Test snapshot did not compress");
            using var input=new MemoryStream(Convert.FromBase64String(envelope.RootElement.GetProperty("payload").GetString()!));
            using var unzip=new ZLibStream(input,CompressionMode.Decompress);using var output=new MemoryStream();unzip.CopyTo(output);
            if(!full.AsSpan().SequenceEqual(output.ToArray()))throw new Exception("Packed state lost data");
            byte[] first=service.TelemetryFrame(1,true)!;
            sample=2;byte[] second=service.TelemetryFrame(1,true)!;
            using var a=JsonDocument.Parse(first);using var b=JsonDocument.Parse(second);
            if(a.RootElement.GetProperty("type").GetString()!="tab5_metrics"||
               b.RootElement.GetProperty("systemMetrics").GetProperty("sampleSequence").GetInt64()!=2||
               b.RootElement.GetProperty("sequence").GetInt64()<=a.RootElement.GetProperty("sequence").GetInt64()||second.Length>2048)
                throw new Exception("Latest metric sample is not a bounded independent frame");
            if(vectors is not null) {
                Directory.CreateDirectory(vectors);
                File.WriteAllBytes(Path.Combine(vectors,"full.json"),full);File.WriteAllBytes(Path.Combine(vectors,"packed.json"),packed);
                File.WriteAllBytes(Path.Combine(vectors,"metrics1.json"),first);File.WriteAllBytes(Path.Combine(vectors,"metrics2.json"),second);
            }
            Console.WriteLine($"TAB5_TELEMETRY_OK full={full.Length} packed={packed.Length} metrics={second.Length}; lossless compression, legacy fallback, latest samples");
            var history=Enumerable.Range(0,64).Select(i=>new NetworkSample(i*12345,i*54321)).ToArray();
            service.SystemMetricsCapture=()=>new(9,55,100,200,DateTimeOffset.UtcNow,history){SampleSession="metrics-test",SampleSequence=64,Samples=history.TakeLast(12).ToArray()};
            byte[] delayed=service.TelemetryFrame(1,true)!;
            if(vectors is not null)File.WriteAllBytes(Path.Combine(vectors,"metrics-delayed.json"),delayed);
            using var delayedEnvelope=JsonDocument.Parse(delayed);
            using var delayedInput=new MemoryStream(Convert.FromBase64String(delayedEnvelope.RootElement.GetProperty("payload").GetString()!));
            using var delayedUnzip=new ZLibStream(delayedInput,CompressionMode.Decompress);using var delayedOutput=new MemoryStream();delayedUnzip.CopyTo(delayedOutput);
            using var delayedJson=JsonDocument.Parse(delayedOutput.ToArray());
            var samples=delayedJson.RootElement.GetProperty("systemMetrics").GetProperty("samples");
            if(samples.GetArrayLength()!=64||samples[0].GetProperty("upload").GetInt64()!=0||samples[63].GetProperty("download").GetInt64()!=63*54321||delayed.Length>=delayedOutput.Length)
                throw new Exception("Delayed samples were truncated or compression changed values");
            Console.WriteLine($"TAB5_DELAYED_METRICS_OK samples=64 raw={delayedOutput.Length} packed={delayed.Length}");
            service.Publish(snapshot with {CapturedAt=DateTimeOffset.UtcNow});
            using(var interactive=JsonDocument.Parse(service.TelemetryFrame(2,true,true,true)!)) {
                using var packedMetrics=new MemoryStream(Convert.FromBase64String(interactive.RootElement.GetProperty("payload").GetString()!));
                using var unpackMetrics=new ZLibStream(packedMetrics,CompressionMode.Decompress);
                using var decodedMetrics=JsonDocument.Parse(unpackMetrics);
                if(decodedMetrics.RootElement.GetProperty("type").GetString()!="tab5_metrics"||decodedMetrics.RootElement.GetProperty("systemMetrics").GetProperty("samples").GetArrayLength()!=64)throw new Exception("Interactive paging queued a catalog or lost metrics");
            }
            using(var resumed=JsonDocument.Parse(service.TelemetryFrame(2,true)!)) {
                if(resumed.RootElement.GetProperty("type").GetString()!="tab5_packed")throw new Exception("Deferred catalog lost after interactive paging");
            }
            Console.WriteLine("TAB5_INTERACTIVE_TELEMETRY_OK metrics during paging; deferred full state resumes");
        }finally{Directory.Delete(directory,true);}
    }
}
