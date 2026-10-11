using System.IO.Ports;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

// Explicit local diagnostic only. No payload, credential or user-data logging.
// The serial gate stays owned throughout; a failed transaction still closes.
internal sealed partial class Tab5Service
{
    internal static string? UsbMetricsTracePath;
    internal static int UsbMetricsTraceIntervalMs=250;
    private byte[]? _usbMetricsDiagnosticPrevious;
    private async Task SendUsbMetricsAsync(SerialPort port,byte[] frame,CancellationToken token)
    {
        string? trace=UsbMetricsTracePath;
        string? before=null,after=null,probeFailure=null;
        string? failure=null,replay=null;bool newlineNeeded=false,metricsAcked=false;
        byte[]? previous=_usbMetricsDiagnosticPrevious;
        long began=Environment.TickCount64;
        async Task<string> ProbeAsync() {
            Send(port,new{version=1,type="tab5_ping"});
            using var hello=await ReadReplyAsync(port,"tab5_hello",token);
            if(hello.RootElement.GetProperty("deviceId").GetString()!=_store.Current?.DeviceId)
                throw new IOException("USB diagnostic identity mismatch");
            return hello.RootElement.GetProperty("usbDiag").GetString()??"";
        }
        try {
            if(trace is not null)before=await ProbeAsync();
            port.WriteLine(Tab5Protocol.Prefix+Encoding.UTF8.GetString(frame));
            using var ack=await ReadReplyAsync(port,"tab5_metrics_ack",token);
            using var sent=JsonDocument.Parse(frame);
            if(ack.RootElement.GetProperty("sequence").GetInt64()!=sent.RootElement.GetProperty("sequence").GetInt64())
                throw new IOException("USB metrics acknowledgement mismatch");
            metricsAcked=true;
            if(trace is not null)_usbMetricsDiagnosticPrevious=frame;
            if(trace is not null)after=await ProbeAsync();
        } catch(Exception ex) when(ex is IOException or TimeoutException) {
            failure=ex is Tab5UsbReplyTimeout timeout?timeout.Diagnostic:ex.GetType().Name;
            if(trace is not null&&before is not null&&!token.IsCancellationRequested) {
                try {after=await ProbeAsync();}
                catch(Exception probe) when(probe is IOException or TimeoutException) {
                    probeFailure=probe.GetType().Name;
                    // An isolated diagnostic probe, never a production retry.
                    // Metrics owns a text transaction, with no binary body pending.
                    try {newlineNeeded=true;port.Write("\n");after=await ProbeAsync();}
                    catch(Exception retry) when(retry is IOException or TimeoutException) {probeFailure+="/"+retry.GetType().Name;}
                }
                if(ex is Tab5UsbReplyTimeout&&!metricsAcked&&after is not null&&previous is not null) {
                    // Exact replay of the previous acknowledged metrics, never
                    // of a command. The device accepts equality without applying
                    // again; it rejects this older sequence if the lost-ACK frame
                    // was applied. All operations retain the same serial gate.
                    try {
                        port.WriteLine(Tab5Protocol.Prefix+Encoding.UTF8.GetString(previous));
                        using var oldAck=await ReadReplyAsync(port,"tab5_metrics_ack",token);
                        using var oldFrame=JsonDocument.Parse(previous);
                        replay=oldAck.RootElement.GetProperty("sequence").GetInt64()==oldFrame.RootElement.GetProperty("sequence").GetInt64()
                            ?"previous_accepted_original_not_applied":"mismatched_ack";
                    }catch(Tab5UsbRejected){replay="previous_rejected_original_may_be_applied";}
                    catch(Exception retry) when(retry is IOException or TimeoutException){replay="inconclusive_"+retry.GetType().Name;}
                }
            }
            throw;
        } finally {
            if(trace is not null) {
                var record=new{at=DateTimeOffset.Now,frameBytes=frame.Length+Encoding.UTF8.GetByteCount(Tab5Protocol.Prefix)+1,
                    elapsedMs=Environment.TickCount64-began,before,after,failure,probeFailure,newlineNeeded,metricsAcked,replay};
                File.AppendAllText(trace,JsonSerializer.Serialize(record)+"\n");
            }
        }
    }
}
