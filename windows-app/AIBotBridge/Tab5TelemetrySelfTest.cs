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
            // Reboot/reconnect discards the device's BLE baseline. Even while
            // paging, its first frame must be full state before any metrics.
            byte[] baseline=service.TelemetryFrame(2,true,true,true)!;
            using(var initial=JsonDocument.Parse(baseline)) {
                using var initialBytes=new MemoryStream(Convert.FromBase64String(initial.RootElement.GetProperty("payload").GetString()!));
                using var initialUnzip=new ZLibStream(initialBytes,CompressionMode.Decompress);
                using var initialJson=JsonDocument.Parse(initialUnzip);
                if(initialJson.RootElement.GetProperty("type").GetString()!="tab5_status")throw new Exception("Fresh connection received metrics before full state");
            }
            service.ResetTelemetryChannel(2);
            if(!baseline.AsSpan().SequenceEqual(service.TelemetryFrame(2,true,true,true)))throw new Exception("Reconnected device did not receive the full baseline again");
            _=service.TelemetryFrame(2,true);
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
            Console.WriteLine("TAB5_RECONNECT_TELEMETRY_OK full baseline before metrics, including interactive reconnect");
            using(var cover=new Bitmap(640,360)) {
                using(var graphics=Graphics.FromImage(cover))graphics.Clear(Color.Red);
                var images=NowPlayingService.RenderCoverImages(cover);
                service.ObserveFirmware("001122334455","0.2.80-ui");
                service.Publish(snapshot with {Music=new("Video","Artist","",true,0,100,DateTimeOffset.UtcNow){CoverRgb565=images.Legacy,Tab5CoverRgb565=images.Tab5,Tab5CoverJpeg=images.Jpeg}});
                byte[]? burst=null;
                for(int i=0;i<4;i++){var candidate=service.TelemetryFrame(1,true)!;using var candidateJson=JsonDocument.Parse(candidate);if(candidateJson.RootElement.GetProperty("type").GetString()=="tab5_resources"){burst=candidate;break;}}
                if(burst is null||burst.Length>Tab5Protocol.MaximumFrame)throw new Exception("Pending artwork did not get a bounded independent burst");
                using var burstJson=JsonDocument.Parse(burst);
                if(burstJson.RootElement.GetProperty("resources").GetArrayLength()>8||burstJson.RootElement.TryGetProperty("data",out _))throw new Exception("Image burst resent a status catalog");
                long resourceClock=10000;service.TelemetryClock=()=>resourceClock;
                var activeMusic=snapshot with {Music=new("Video","Artist","",true,0,100,DateTimeOffset.UtcNow){CoverRgb565=images.Legacy,Tab5CoverRgb565=images.Tab5,Tab5CoverJpeg=images.Jpeg}};
                foreach(int channel in new[]{1,2}) {
                    service.ResetTelemetryChannel(channel);service.Publish(activeMusic);_ = service.TelemetryFrame(channel,true);
                    int bursts=0;
                    for(int round=0;round<6;round++) {
                        resourceClock+=500;service.Publish(activeMusic with {CapturedAt=DateTimeOffset.UtcNow.AddMilliseconds(round*500)});
                        using var busy=JsonDocument.Parse(service.TelemetryFrame(channel,true)!);
                        if(busy.RootElement.GetProperty("type").GetString()=="tab5_resources")bursts++;
                    }
                    if(bursts<2)throw new Exception("Frequent music publications starved artwork on channel "+channel);
                    resourceClock+=500;service.Publish(activeMusic with {Music=activeMusic.Music! with {Playing=false}});
                    using var pauseWire=JsonDocument.Parse(service.TelemetryFrame(channel,true)!);
                    using var pauseBytes=new MemoryStream(Convert.FromBase64String(pauseWire.RootElement.GetProperty("payload").GetString()!));
                    using var pauseZip=new ZLibStream(pauseBytes,CompressionMode.Decompress);using var pause=JsonDocument.Parse(pauseZip);
                    if(pause.RootElement.GetProperty("type").GetString()!="tab5_status"||pause.RootElement.GetProperty("data").GetProperty("music").GetProperty("playing").GetBoolean())
                        throw new Exception("Artwork delayed a music pause");
                    resourceClock+=500;service.Publish(activeMusic with {Music=activeMusic.Music! with {Playing=false,ElapsedSeconds=40}});
                    using var seekWire=JsonDocument.Parse(service.TelemetryFrame(channel,true)!);
                    using var seekBytes=new MemoryStream(Convert.FromBase64String(seekWire.RootElement.GetProperty("payload").GetString()!));
                    using var seekZip=new ZLibStream(seekBytes,CompressionMode.Decompress);using var seek=JsonDocument.Parse(seekZip);
                    if(seek.RootElement.GetProperty("type").GetString()!="tab5_status"||seek.RootElement.GetProperty("data").GetProperty("music").GetProperty("elapsedSeconds").GetDouble()!=40)
                        throw new Exception("Artwork delayed a paused seek");
                }
                Console.WriteLine("TAB5_MUSIC_ARTWORK_FAIRNESS_OK Wi-Fi/BLE bursts despite 500ms state changes; pause remains urgent");
                service.OtaTransferActive(true);
                service.Publish(snapshot with {Music=new("Video","Artist","",true,0,100,DateTimeOffset.UtcNow){CoverRgb565=images.Legacy,Tab5CoverRgb565=images.Tab5,Tab5CoverJpeg=images.Jpeg}});
                using(var upgrading=JsonDocument.Parse(service.CurrentFrame!)){
                    var data=upgrading.RootElement.GetProperty("data");
                    if(data.GetProperty("resource").ValueKind!=JsonValueKind.Null||data.GetProperty("epochMilliseconds").GetInt64()<=0||upgrading.RootElement.GetProperty("session").GetString() is not {Length:>0})throw new Exception("OTA heartbeat lost freshness or included artwork");
                }
                if(service.UsbMetricsFrame() is not null)throw new Exception("OTA did not suspend USB sampling");
                for(int i=0;i<4;i++){using var quiet=JsonDocument.Parse(service.TelemetryFrame(1,true)!);if(quiet.RootElement.GetProperty("type").GetString()=="tab5_resources")throw new Exception("OTA consumed pending artwork fragments");}
                service.OtaTransferActive(false);
                if(service.UsbMetricsFrame() is null)throw new Exception("Completed streaming upgrade did not resume sampling");
                bool resumedArtwork=false;
                for(int i=0;i<4;i++){using var resumed=JsonDocument.Parse(service.TelemetryFrame(1,true)!);resumedArtwork|=resumed.RootElement.GetProperty("type").GetString()=="tab5_resources";}
                if(!resumedArtwork)throw new Exception("Upgrade discarded deferred artwork");
                Console.WriteLine("TAB5_OTA_BACKGROUND_OK USB metrics and artwork paused; session heartbeat retained; pending traffic resumes");
                service.Publish(snapshot);service.ObserveFirmware("001122334455","0.2.78-ui");
                Console.WriteLine("TAB5_ARTWORK_BURST_OK bounded image fragments without catalog; legacy version fallback");
            }
            // Continuous live-reply reads keep the BLE RPC activity flag set.
            // They must not starve the full state that keeps that same reader alive.
            long clock=10000;service.TelemetryClock=()=>clock;service.ResetTelemetryChannel(2);
            service.TelemetryFrame(2,true,true,true);
            bool IsFull(byte[] value) {
                using var wire=JsonDocument.Parse(value);
                if(wire.RootElement.GetProperty("type").GetString()!="tab5_packed")return wire.RootElement.GetProperty("type").GetString()=="tab5_status";
                using var bytes=new MemoryStream(Convert.FromBase64String(wire.RootElement.GetProperty("payload").GetString()!));
                using var zip=new ZLibStream(bytes,CompressionMode.Decompress);using var json=JsonDocument.Parse(zip);
                return json.RootElement.GetProperty("type").GetString()=="tab5_status";
            }
            for(int round=0;round<3;round++) {
                clock+=3999;service.Publish(snapshot with {CapturedAt=DateTimeOffset.UtcNow});
                if(IsFull(service.TelemetryFrame(2,true,true,true)!))throw new Exception("Interactive catalog was not deferred within its budget");
                clock++;
                if(!IsFull(service.TelemetryFrame(2,true,true,true)!))throw new Exception("Continuous BLE reads starved full state beyond four seconds");
            }
            service.ResetTelemetryChannel(2);
            if(!IsFull(service.TelemetryFrame(2,true,true,true)!))throw new Exception("Reset lost first full state during active reads");
            Console.WriteLine("TAB5_CONTINUOUS_READ_FRESHNESS_OK pending full state every four seconds despite continuous RPC activity");
            service.ResetTelemetryChannel(2);_ = service.TelemetryFrame(2,true,true,true,true);
            clock+=4000;service.Publish(snapshot with {CapturedAt=DateTimeOffset.UtcNow.AddSeconds(1)});
            if(!IsFull(service.TelemetryFrame(2,true,true,true,true)!))throw new Exception("Unacknowledged baseline used for delta");
            using(var confirmed=JsonDocument.Parse(service.CurrentFrame!))service.AcknowledgeTelemetry(2,confirmed.RootElement.GetProperty("sequence").GetInt64());
            clock+=4000;service.Publish(snapshot with {CapturedAt=DateTimeOffset.UtcNow.AddSeconds(2)});
            var change=service.TelemetryFrame(2,true,true,true,true)!;
            if(IsFull(change))throw new Exception("ACKed baseline failed to reduce unchanged state");
            service.ResetTelemetryChannel(2);
            if(!IsFull(service.TelemetryFrame(2,true,true,true,true)!))throw new Exception("Reconnect reused stale delta baseline");
            Console.WriteLine("TAB5_DELTA_ACK_OK baseline only after ACK; changed snapshot sends delta; reconnect returns full state");
        }finally{Directory.Delete(directory,true);}
    }
}
