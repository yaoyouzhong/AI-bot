using System.IO.Compression;
using System.Text.Json;

namespace AIBotBridge;

internal sealed partial class Tab5Service
{
    internal Func<SystemMetricsSnapshot?>? SystemMetricsCapture {get;set;}
    private readonly object _telemetryLock=new();
    private readonly byte[]?[] _lastTelemetryFull=new byte[3][];
    private readonly bool[] _metricsTurn=new bool[3];
    private byte[]? _packedSource,_packedFrame;
    private volatile int _usbTelemetryVersion;

    // Full state still refreshes every two seconds. The small metrics frame
    // carries the same 250 ms samples as the original display, without sending
    // the task catalog, weather and quota history four times per second.
    internal byte[]? TelemetryFrame(int channel,bool supported,bool compressionSafe=true,bool preferMetrics=false)
    {
        var full=CurrentFrame;
        if(full is null||!supported)return full;
        lock(_telemetryLock) {
            // While the user pages through replies, leave the catalog pending
            // and send only current metrics between interactive RPCs.
            if(preferMetrics&&MetricsFrame(compressionSafe) is {} interactiveMetrics)return interactiveMetrics;
            // Even if a slow radio transfer spans a full-state publication,
            // give current metrics a turn before sending another catalog.
            if(_metricsTurn[channel]){_metricsTurn[channel]=false;if(MetricsFrame(compressionSafe) is {} sample)return sample;}
            if(!ReferenceEquals(_lastTelemetryFull[channel],full)) {
                _lastTelemetryFull[channel]=full;
                _metricsTurn[channel]=true;
                if(!compressionSafe)return full;
                if(!ReferenceEquals(_packedSource,full)){_packedFrame=PackTelemetry(full);_packedSource=full;}
                return _packedFrame;
            }
            return MetricsFrame(compressionSafe)??(compressionSafe?_packedFrame:full);
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
    internal static byte[] PackTelemetry(byte[] full)
    {
        using var doc=JsonDocument.Parse(full);
        using var compressed=new MemoryStream();
        using(var zipper=new ZLibStream(compressed,CompressionLevel.Fastest,true))zipper.Write(full);
        var packed=JsonSerializer.SerializeToUtf8Bytes(new {version=1,type="tab5_packed",
            sequence=doc.RootElement.GetProperty("sequence").GetInt64(),size=full.Length,
            payload=Convert.ToBase64String(compressed.ToArray())},JsonDefaults.Options);
        return packed.Length<full.Length?packed:full;
    }
    private async Task RunUsbMetricsAsync(CancellationToken token)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        while(await timer.WaitForNextTickAsync(token)) {
            if(_usbTelemetryVersion!=1||MetricsFrame() is not {} frame)continue;
            await _usbGate.WaitAsync(token);
            try {
                if(_usbTelemetryVersion!=1||_usbPort is not {} port)continue;
                port.WriteLine(Tab5Protocol.Prefix+System.Text.Encoding.UTF8.GetString(frame));
                using var ack=await ReadReplyAsync(port,"tab5_metrics_ack",token);
                using var sent=JsonDocument.Parse(frame);
                if(ack.RootElement.GetProperty("sequence").GetInt64()!=sent.RootElement.GetProperty("sequence").GetInt64())throw new IOException("USB metrics acknowledgement mismatch");
            }catch(OperationCanceledException) when(token.IsCancellationRequested){return;}
            catch(Exception ex) when(ex is IOException or InvalidOperationException or TimeoutException or UnauthorizedAccessException or JsonException or KeyNotFoundException) {
                CloseUsbPort();_usbStatus="USB 已断开，等待重新连接";
            }finally{_usbGate.Release();}
        }
    }
}
