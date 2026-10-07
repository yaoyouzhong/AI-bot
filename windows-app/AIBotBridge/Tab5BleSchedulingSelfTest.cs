using System.Buffers.Binary;
using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5BleSchedulingSelfTest
{
    internal static async Task RunAsync() {
        await VerifyBulkGapAsync();
        await VerifyQueuedTelemetryAsync();
        await VerifyQueueComparisonAsync();
        await VerifyQueueComparisonAsync(true);
        await VerifyBenchmarkReadbackAsync();
        VerifyProbeTrace();
        VerifyRadioProbeCompatibility();
        Console.WriteLine("BLE_CONTINUOUS_SCHEDULING_OK bounded_gap_lease_no_poll_renewal_failure_cancel_release_gate_recheck_heartbeat_voice_trace_lifecycle");
    }
    private sealed class Lease:IDisposable {public void Dispose(){}}
    private static async Task VerifyBenchmarkReadbackAsync() {
        int calls=0;
        string result=await Tab5Service.ReadBenchmarkStatusAsync(_=>++calls==1?Task.FromException<string>(new IOException("port recovering")):Task.FromResult("complete;retained-result"),CancellationToken.None);
        if(calls!=2||result!="complete;retained-result")throw new Exception("Transient control reconnect lost the retained benchmark result");
        calls=0;
        try {await Tab5Service.ReadBenchmarkStatusAsync(_=>{calls++;return Task.FromException<string>(new TimeoutException());},CancellationToken.None);throw new Exception("Persistent control loss was hidden");}
        catch(TimeoutException){if(calls!=4)throw new Exception("Control retry is not bounded");}
        using var stop=new CancellationTokenSource();stop.Cancel();calls=0;
        try {await Tab5Service.ReadBenchmarkStatusAsync(_=>{calls++;return Task.FromResult("complete");},stop.Token);throw new Exception("Cancelled control read continued");}
        catch(OperationCanceledException){if(calls!=0)throw new Exception("Cancellation sent another command");}
    }
    private static async Task VerifyQueueComparisonAsync(bool compareWindow=false) {
        static Tab5BleQueueProbe Probe(int ms)=>new("complete;"+string.Join(';',Enumerable.Range(1,3).Select(i=>$"BLE,down,{i},262144,{ms},0,6,1,4900,1,100,263127,100000,8000000,63488")),
            "1,1,15000,0,251,251,0,0,0,0,1650,789381,11000,16630,30000,0","1,0,0,0,16000,1,0","{}",6863568);
        var comparison=new Tab5BleQueueComparison();var limits=new List<int>();
        int normal=compareWindow?32:7,trial=compareWindow?64:31;
        int Current()=>compareWindow?comparison.WriteWindow:comparison.NativeLimit;
        bool Restored()=>comparison.NativeLimit==7&&comparison.WriteWindow==64;
        if(!Restored())throw new Exception("Initial production write window is not 64");
        await comparison.RunAsync((limit,_)=> {
            if(Current()!=limit)throw new Exception("Comparison parameter not active during probe");
            if(compareWindow&&comparison.NativeLimit!=7)throw new Exception("Window trial changed native concurrency");
            if(!compareWindow&&comparison.WriteWindow!=32)throw new Exception("Queue trial changed its fixed wire window");
            limits.Add(limit);return Task.FromResult(Probe(5000+limits.Count));
        },CancellationToken.None,compareWindow);
        using(var doc=JsonDocument.Parse(comparison.Json)) {
            var r=doc.RootElement;var samples=r.GetProperty("samples");
            if(!limits.SequenceEqual(new[]{normal,trial,normal})||!Restored()||r.GetProperty("state").GetString()!="complete"||samples.GetArrayLength()!=3||
                !samples[0].GetProperty("result").GetString()!.Contains(",5001,0,")||!samples[2].GetProperty("result").GetString()!.Contains(",5003,0,"))
                throw new Exception("ABA comparison lost order, restoration or independent snapshots");
            if(r.GetProperty("activeWriteWindow").GetInt32()!=64||samples[1].GetProperty("writeWindow").GetInt32()!=(compareWindow?64:32)||
                samples[1].GetProperty("nativeLimit").GetInt32()!=(compareWindow?7:31))throw new Exception("Comparison axes not recorded independently");
        }
        foreach(int fault in new[]{1,2,3,4}) {
            int calls=0;using var stop=new CancellationTokenSource();
            try {
                await comparison.RunAsync((limit,ct)=> {
                    calls++;if(calls!=2)return Task.FromResult(Probe(5000));
                    if(limit!=trial||Current()!=trial)throw new Exception("Missing temporary queue setting");
                    if(fault==1)throw new IOException("comparison failure");
                    if(fault==2){stop.Cancel();ct.ThrowIfCancellationRequested();}
                    return Task.FromResult(fault==3?Probe(5000) with{Result="failed;BLE,down,1,1,20000,-2"}:
                        Probe(5000) with{WifiIsolation="1,4,0,0,16000,0,0"});
                },stop.Token,compareWindow);
                if(fault<=2)throw new Exception("Comparison ignored exception");
            }catch(IOException ex) when(fault==1&&ex.Message=="comparison failure"){}
            catch(OperationCanceledException) when(fault==2){}
            using var doc=JsonDocument.Parse(comparison.Json);var r=doc.RootElement;
            if(calls!=2||!Restored()||r.GetProperty("activeNativeLimit").GetInt32()!=7||r.GetProperty("samples").GetArrayLength()!=(fault<=2?1:2)||
                r.GetProperty("state").GetString()!=(fault==1?"failed":fault==2?"cancelled":"incomplete"))
                throw new Exception("Failed comparison continued or failed to restore/preserve evidence");
        }
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var run=comparison.RunAsync(async(limit,_)=>{if(limit==trial){entered.SetResult();await release.Task;}return Probe(5000);},CancellationToken.None,compareWindow);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try {
            await comparison.RunAsync((_,_)=>throw new Exception("Concurrent probe started"),CancellationToken.None,compareWindow);
            throw new Exception("Concurrent comparison accepted");
        }catch(InvalidOperationException){}
        if(Current()!=trial)throw new Exception("Rejected comparison changed running parameter");
        release.SetResult();await run.WaitAsync(TimeSpan.FromSeconds(2));
        if(!Restored())throw new Exception("Completed comparison retained temporary setting");
        Console.WriteLine($"BLE_COMPARISON_OK window={compareWindow} ABA_snapshot_fault_cancel_partial_restore_concurrent_rejection_rerun");
    }
    private static void VerifyRadioProbeCompatibility() {
        if(Tab5Service.ProbeAction(true,false,false)!="ble_ota_probe"||Tab5Service.ProbeAction(true,true,true)!="ble_ota_probe_isolated")throw new Exception("BLE probe action mismatch");
        foreach(var flags in new[]{(false,false,false),(true,false,true)}) {
            try{Tab5Service.ProbeAction(flags.Item1,flags.Item2,flags.Item3);throw new Exception("Unsupported probe was sent");}catch(NotSupportedException){}
        }
        using var doc=JsonDocument.Parse("{\"wifiIsolation\":\"1,0,0,0,20000,1,0\",\"bleRadio\":\"1,1,15000,0,251,251,0,0,0,0,1650,789381,11000,16630,30000,0\",\"invalid\":\"1,0,text,0,0,0,0\"}");
        if(Tab5Service.NumericProbeDiagnostic(doc.RootElement,"wifiIsolation",7)!="1,0,0,0,20000,1,0"||Tab5Service.NumericProbeDiagnostic(doc.RootElement,"bleRadio",16)=="invalid"||
           Tab5Service.NumericProbeDiagnostic(doc.RootElement,"invalid",7)!="invalid"||Tab5Service.NumericProbeDiagnostic(doc.RootElement,"missing",7)!="unsupported")throw new Exception("Radio diagnostic validation failed");
    }
    private static async Task VerifyBulkGapAsync() {
        foreach(int fault in new[]{0,1,2}) {
            long now=10000;uint id=1;int responseBytes=49152,handled=0,injected=0;
            using var stop=new CancellationTokenSource();
            var pump=new Tab5BleVoice(_=> {
                var packet=new byte[id==0?8:132];BinaryPrimitives.WriteUInt32LittleEndian(packet,id);
                if(id!=0)BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(6),124);
                return Task.FromResult(packet);
            },(_,ct)=> {
                now+=2;
                if(injected==1)throw new IOException("write failed");
                if(injected==2){stop.Cancel();ct.ThrowIfCancellationRequested();}
                return Task.CompletedTask;
            },(_,_,_,_)=>{handled++;now+=3;return Task.FromResult<(int,byte[]?)>((200,new byte[responseBytes]));},517,maximumResponse:65535,clock:()=>now);
            if(pump.BulkPriorityActive(now))throw new Exception("Unused BLE connection acquired bulk priority");
            await pump.PumpAsync(stop.Token);long completed=now;
            if(pump.BulkActive||!pump.BulkPriorityActive(completed)||pump.LastTiming is not {} timing||timing.HandlerMs!=3||timing.CompletedMs!=completed)
                throw new Exception("Completed bulk timing or inter-chunk priority lost");
            var changes=new List<bool>();using var preference=new Tab5BleConnectionPreference(b=>{changes.Add(b);return new Lease();});
            preference.Update(pump.BulkPriorityActive(now));
            now=completed+400;await pump.PumpAsync(stop.Token); // repeated ID, no handler or lease renewal
            if(handled!=1||pump.CompletedCount!=1)throw new Exception("Duplicate request executed");
            id=0;now=completed+500;await pump.PumpAsync(stop.Token);
            preference.Update(pump.BulkPriorityActive(now));
            if(!changes.SequenceEqual(new[]{true})||pump.BulkPriorityActive(completed-1))throw new Exception("Gap toggled preference or accepted a backwards clock");
            id=2;responseBytes=28;now=completed+600;await pump.PumpAsync(stop.Token);
            if(!pump.BulkPriorityActive(now)||pump.BulkPriorityActive(completed+1000))throw new Exception("Small reply renewed bulk lease or prematurely cleared it");
            preference.Update(pump.BulkPriorityActive(completed+1000));
            if(!changes.SequenceEqual(new[]{true,false}))throw new Exception("Idle connection did not restore preference");
            id=3;responseBytes=49152;now=completed+1100;await pump.PumpAsync(stop.Token);
            if(!pump.BulkPriorityActive(now+999)||pump.BulkPriorityActive(now+1000))throw new Exception("Next real bulk did not renew bounded lease");
            if(fault==0)continue;
            id=4;injected=fault;
            try{await pump.PumpAsync(stop.Token);throw new Exception("Failed bulk returned success");}
            catch(IOException) when(fault==1){}
            catch(OperationCanceledException) when(fault==2){}
            if(pump.BulkActive||pump.BulkPriorityActive(now)||pump.CompletedCount!=3)throw new Exception("Failed/cancelled bulk retained priority or counted as complete");
        }
    }
    private static async Task VerifyQueuedTelemetryAsync() {
        using var gate=new SemaphoreSlim(1,1);await gate.WaitAsync();
        bool recent=false;long now=1200,lastTelemetry=1000;int sent=0;
        var waiting=Tab5BleClient.SendTelemetryAsync(gate,()=>Tab5BleClient.DeferTelemetry(now,lastTelemetry,recent,false),
            ()=>{sent++;return Task.FromResult(true);},CancellationToken.None);
        recent=true;now=1300;gate.Release();
        if(await waiting||sent!=0||gate.CurrentCount!=1)throw new Exception("Queued telemetry ignored newly active bulk transfer");
        now=7000;
        if(!await Tab5BleClient.SendTelemetryAsync(gate,()=>Tab5BleClient.DeferTelemetry(now,lastTelemetry,recent,false),
            ()=>{sent++;return Task.FromResult(true);},CancellationToken.None)||sent!=1)throw new Exception("Bulk lease starved required status refresh");
        if(Tab5BleClient.VoicePollDelay(now,now-100,true)!=15||Tab5BleClient.VoicePollDelay(now,0,true)!=500||Tab5BleClient.VoicePollDelay(now,0,false)!=250)
            throw new Exception("Bulk gap changed active voice or normal polling");
    }
    private static void VerifyProbeTrace() {
        var trace=new Tab5BleProbeTrace();
        if(trace.Ticket(0)!=0||trace.Json!="尚未预检")throw new Exception("Trace captured before preflight");
        trace.Begin(1000);long first=trace.Ticket(1100);
        trace.RecordRpc(first,new(new(1100,1400,30,20,250,296,49317),11,new(19,99,100,4,190,4,220,4),"actualIntervalUs=15000; txPhy=2M"));
        long last=trace.Ticket(1450);trace.End(1600);
        if(trace.Ticket(1600)!=0)throw new Exception("Post-stop traffic entered trace at same clock tick");
        trace.RecordRpc(last,new(new(1450,1700,20,10,220,296,16549),7,new(13,33,50,2,180,2,200,2),"txPhy=2M"));
        trace.RecordActivity(first,"telemetry",1200,1350);trace.RecordActivity(first,"voice-poll",1300,1400);
        trace.RecordActivity(first,"rpc-idle",1400,1440);trace.RecordActivity(0,"failure",1600,1700);
        using(var doc=JsonDocument.Parse(trace.Json)) {
            var r=doc.RootElement;
            if(r.GetProperty("running").GetBoolean()||r.GetProperty("rpcCount").GetInt32()!=2||r.GetProperty("controlMs").GetInt32()!=600||
               r.GetProperty("responseBytes").GetInt32()!=65866||r.GetProperty("requestMs").GetInt32()!=50||r.GetProperty("handlerMs").GetInt32()!=30||
               r.GetProperty("responseMs").GetInt32()!=470||r.GetProperty("queueMs").GetInt32()!=18||r.GetProperty("interRpcGapMs").GetInt32()!=50||
               r.GetProperty("firstRpcDelayMs").GetInt32()!=100||r.GetProperty("ackCount").GetInt32()!=6||r.GetProperty("ackWaitSumMs").GetInt32()!=370||
               r.GetProperty("telemetryWallMs").GetInt32()!=150||r.GetProperty("voicePollCount").GetInt32()!=1||r.GetProperty("rpcIdlePollWallMs").GetInt32()!=40||
               r.GetProperty("failures").GetInt32()!=0||r.GetProperty("samples")[1].GetProperty("endMs").GetInt32()!=700)
                throw new Exception("Trace totals, phase boundaries or final in-flight completion lost");
        }
        trace.Begin(2000);
        trace.RecordRpc(last,new(new(2100,2200,0,0,100,124,32),0,null,"stale"));
        if(trace.Ticket(1999)!=0||trace.Ticket(37000)!=0)throw new Exception("Trace capture budget invalid");
        long current=trace.Ticket(2000);
        for(int i=0;i<40;i++)trace.RecordRpc(current,new(new(2000+i*5,2002+i*5,0,0,2,124,32),0,null,""));
        trace.RecordActivity(current,"failure",2200,2201);trace.End(2300);
        using(var doc=JsonDocument.Parse(trace.Json)) {
            var r=doc.RootElement;
            if(r.GetProperty("rpcCount").GetInt32()!=40||r.GetProperty("droppedSamples").GetInt32()!=8||r.GetProperty("samples").GetArrayLength()!=32||
               r.GetProperty("responseBytes").GetInt32()!=1280||r.GetProperty("failures").GetInt32()!=1||trace.Json.Contains("stale",StringComparison.Ordinal))
                throw new Exception("Trace unbounded or previous run contaminated new preflight");
        }
        trace.Begin(1000,160000);
        if(trace.Ticket(150000)==0||trace.Ticket(161000)!=0)throw new Exception("Full-image trace duration invalid");
        for(int i=0;i<170;i++)trace.RecordRpc(trace.Ticket(1000+i*800),new(new(1000+i*800,1100+i*800,0,0,100,100,100),0,null,""));
        trace.End(150000);
        using(var doc=JsonDocument.Parse(trace.Json))
            if(doc.RootElement.GetProperty("samples").GetArrayLength()!=160||doc.RootElement.GetProperty("droppedSamples").GetInt32()!=10)
                throw new Exception("Full-image trace lost totals or exceeded its sample bound");
    }
}
