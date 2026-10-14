using System.IO.Compression;
using System.Text.Json;

namespace AIBotBridge;

internal sealed partial class Tab5Service
{
    internal Func<SystemMetricsSnapshot?>? SystemMetricsCapture {get;set;}
    private readonly object _telemetryLock=new();
    private readonly byte[]?[] _lastTelemetryFull=new byte[3][];
    private readonly long[] _lastTelemetryFullAt=new long[3];
    private readonly byte[]?[] _ackTelemetry=new byte[3][],_pendingTelemetry=new byte[3][];
    internal Func<long> TelemetryClock {get;set;}=()=>Environment.TickCount64;
    private readonly bool[] _metricsTurn=new bool[3];
    private byte[]? _packedSource,_packedFrame;
    private volatile int _usbTelemetryVersion;
    private volatile bool _usbPacked;
    private volatile string _usbControlFailure="无";
    private long _usbMetricsDeferred;
    private long _usbStatusRecoveryAttempts,_usbStatusRecoverySucceeded;
    private void RecordUsbFailure(string stage,Exception ex) {
        _usbRpcLastFailure=$"{DateTimeOffset.Now:HH:mm:ss} USB {stage}; {ex.GetType().Name} / 0x{ex.HResult:X8}";
        if(ex is Tab5UsbReplyTimeout timeout)_usbRpcLastFailure+="; "+timeout.Diagnostic;
        if(ex is Tab5UsbRejected rejected)_usbRpcLastFailure+=$"; expected={rejected.Expected}; rejectReason={rejected.Reason}";
        _usbControlFailure=_usbRpcLastFailure;
        if(_usbRpcFirstFailure=="无")_usbRpcFirstFailure=_usbRpcLastFailure;
    }
    private byte[] PackedFullFrame(byte[] full) {
        lock(_telemetryLock) {
            if(!ReferenceEquals(_packedSource,full)){_packedFrame=PackTelemetry(full,CompressionLevel.Optimal);_packedSource=full;}
            return _packedFrame!;
        }
    }

    internal void ResetTelemetryChannel(int channel) {
        lock(_telemetryLock) {
            _lastTelemetryFull[channel]=null;
            _lastTelemetryFullAt[channel]=0;
            _metricsTurn[channel]=false;
            _ackTelemetry[channel]=_pendingTelemetry[channel]=null;
        }
    }

    // Full state still refreshes every two seconds. The small metrics frame
    // carries the bridge's once-per-second snapshot with measured 250 ms raw
    // samples. Duplicate sample sequences leave numbers and curves unchanged.
    internal byte[]? TelemetryFrame(int channel,bool supported,bool compressionSafe=true,bool preferMetrics=false,bool deltaSupported=false)
    {
        var full=CurrentFrame;
        if(full is null||!supported)return full;
        bool settingsPending=DisplayCommand is not null||RemoteOtaPending;
        lock(_telemetryLock) {
            // Give reply RPCs priority briefly, but keep full state moving:
            // metrics do not renew the device's task/session freshness.
            // A new connection must establish its authenticated session/catalog
            // before metrics or interactive pacing can refer to that session.
            bool fullDue=_lastTelemetryFull[channel] is not null&&!ReferenceEquals(_lastTelemetryFull[channel],full)&&
                TelemetryClock()-_lastTelemetryFullAt[channel]>=4000;
            bool musicUrgent=_lastTelemetryFull[channel] is {} previous && MusicChangedUrgently(previous,full);
            if(!settingsPending&&_lastTelemetryFull[channel] is not null&&preferMetrics&&!fullDue&&MetricsFrame(compressionSafe) is {} interactiveMetrics)return interactiveMetrics;
            // Even if a slow radio transfer spans a full-state publication,
            // give current metrics a turn before sending another catalog.
            if(_metricsTurn[channel]) {
                _metricsTurn[channel]=false;
                if(!settingsPending&&!fullDue&&!musicUrgent) {
                    // Music publishes every 500 ms. Give pending artwork this
                    // turn even when a newer full frame is already waiting.
                    if(!preferMetrics&&ResourceFrame(channel) is {} resources)return resources;
                    if(MetricsFrame(compressionSafe) is {} sample)return sample;
                }
            }
            if(!ReferenceEquals(_lastTelemetryFull[channel],full)) {
                _lastTelemetryFull[channel]=full;
                _lastTelemetryFullAt[channel]=TelemetryClock();
                _metricsTurn[channel]=true;
                _pendingTelemetry[channel]=full;
                if(!compressionSafe)return full;
                // Full catalogs occupy seconds of radio time. Compress once
                // per publication; keep high-rate metrics on the fast path.
                if(!ReferenceEquals(_packedSource,full)){_packedFrame=PackTelemetry(full,CompressionLevel.Optimal);_packedSource=full;}
                if(deltaSupported&&!RemoteOtaPending&&_ackTelemetry[channel] is {} baseline&&Tab5TelemetryDelta.Create(baseline,full) is {} delta) {
                    var packedDelta=PackTelemetry(delta,CompressionLevel.Optimal);
                    if(packedDelta.Length<_packedFrame!.Length)return packedDelta;
                }
                return _packedFrame;
            }
            // Resource traffic reuses the established authenticated session. It
            // does not resend the catalog or change task freshness. RPC still wins.
            if(!preferMetrics&&ResourceFrame(channel) is {} resourceFrame)return resourceFrame;
            return MetricsFrame(compressionSafe)??(compressionSafe?_packedFrame:full);
        }
    }
    private byte[]? ResourceFrame(int channel) {
        if(BackgroundTransferPaused||!_assets.JpegSupported||!_assets.HasPending||_store.Current is not {} pair)return null;
        var resources=_assets.NextBatch(channel==1?8:4);
        return resources.Length==0?null:JsonSerializer.SerializeToUtf8Bytes(new {
            version=1,type="tab5_resources",deviceId=pair.DeviceId,session=_session,
            sequence=Interlocked.Increment(ref _sequence),resources
        },JsonDefaults.Options);
    }
    private static bool MusicChangedUrgently(byte[] previous,byte[] current) {
        if(ReferenceEquals(previous,current))return false;
        using var before=JsonDocument.Parse(previous);using var after=JsonDocument.Parse(current);
        var old=before.RootElement.GetProperty("data").GetProperty("music");
        var next=after.RootElement.GetProperty("data").GetProperty("music");
        if(old.ValueKind!=JsonValueKind.Object||next.ValueKind!=JsonValueKind.Object)return old.ValueKind!=next.ValueKind;
        foreach(string key in new[]{"title","artist","album","playing","timelineAvailable","durationSeconds"})
            if(old.GetProperty(key).GetRawText()!=next.GetProperty(key).GetRawText())return true;
        double positionDelta=next.GetProperty("elapsedSeconds").GetDouble()-old.GetProperty("elapsedSeconds").GetDouble();
        double age=(after.RootElement.GetProperty("data").GetProperty("epochMilliseconds").GetInt64()-
                    before.RootElement.GetProperty("data").GetProperty("epochMilliseconds").GetInt64())/1000.0;
        return Math.Abs(positionDelta-(next.GetProperty("playing").GetBoolean()?Math.Max(0,age):0))>2;
    }
    internal void AcknowledgeTelemetry(int channel,long sequence) {
        lock(_telemetryLock) {
            if(_pendingTelemetry[channel] is not {} pending)return;
            using var doc=JsonDocument.Parse(pending);
            if(doc.RootElement.GetProperty("sequence").GetInt64()!=sequence)return;
            _ackTelemetry[channel]=pending;_pendingTelemetry[channel]=null;
        }
    }
    private byte[]? MetricsFrame(bool compressionSafe=false)
    {
        var pair=_store.Current;var metrics=SystemMetricsCapture?.Invoke();
        if(pair is null||metrics is null||CurrentFrame is null)return null;
        byte[] frame=JsonSerializer.SerializeToUtf8Bytes(new {
            version=1,type="tab5_metrics",deviceId=pair.DeviceId,session=_session,
            sequence=Interlocked.Increment(ref _sequence),
            // BLE can spend more than three seconds on a page or status frame.
            // Keep 16 seconds of actual samples to backfill delayed delivery.
            systemMetrics=metrics with {Samples=(metrics.History??metrics.Samples)?.TakeLast(64).ToArray()}
        },JsonDefaults.Options);
        return compressionSafe&&frame.Length>1024?PackTelemetry(frame):frame;
    }
    internal static byte[] PackTelemetry(byte[] full,CompressionLevel level=CompressionLevel.Fastest)
    {
        using var doc=JsonDocument.Parse(full);
        using var compressed=new MemoryStream();
        using(var zipper=new ZLibStream(compressed,level,true))zipper.Write(full);
        var packed=JsonSerializer.SerializeToUtf8Bytes(new {version=1,type="tab5_packed",
            sequence=doc.RootElement.GetProperty("sequence").GetInt64(),size=full.Length,
            payload=Convert.ToBase64String(compressed.ToArray())},JsonDefaults.Options);
        return packed.Length<full.Length?packed:full;
    }
    private async Task RunUsbMetricsAsync(CancellationToken token)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromMilliseconds(UsbMetricsTracePath is null?250:UsbMetricsTraceIntervalMs));
        while(await timer.WaitForNextTickAsync(token)) {
            if(_usbTelemetryVersion!=1||BackgroundTransferPaused)continue;
            if(!await TryUsbMetricsAsync(_usbGate,async ()=> {
                try {
                    // Capture after ownership: a queued old sample/session must
                    // not delay RPC or be sent after a reconnect. History retains
                    // skipped samples for the next successful metrics frame.
                    if(_usbTelemetryVersion!=1||_usbPort is not {} port||UsbMetricsFrame() is not {} frame)return;
                    await SendUsbMetricsAsync(port,frame,token);
                }catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
                catch(Exception ex) when(Tab5UsbRecovery.IsDisconnect(ex,token)) {
                    RecordUsbFailure("metrics",ex);CloseUsbPort();_usbStatus="USB 已断开，等待重新连接";
                }
            },token))Interlocked.Increment(ref _usbMetricsDeferred);
        }
    }
    internal static async Task<bool> TryUsbMetricsAsync(SemaphoreSlim gate,Func<Task> send,CancellationToken token) {
        if(!await gate.WaitAsync(0,token))return false;
        try {await send();return true;}finally{gate.Release();}
    }
    internal byte[]? UsbMetricsFrame()=>BackgroundTransferPaused?null:MetricsFrame();
}
