using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5UsbStatusSelfTest
{
    internal static async Task RunAsync()
    {
        const string device="001122334455";
        var state=new Tab5UsbState();
        byte[] Baseline(long sequence,int unread)=>JsonSerializer.SerializeToUtf8Bytes(new{version=1,type="tab5_status",deviceId=device,session="fixture",sequence,data=new{catalog=Enumerable.Range(0,400).Select(i=>new{id=i,text=$"work-{i}-"+new string((char)('a'+i%26),25)}),unread}});
        byte[] baseline=Baseline(1,9),changed=Baseline(2,0);
        if(state.Wire(baseline,true,true).Length==0)throw new Exception("USB first full state missing");
        state.Confirm(baseline);
        using(var delta=JsonDocument.Parse(state.Wire(changed,true,true))) {
            if(delta.RootElement.GetProperty("type").GetString()!="tab5_delta"||delta.RootElement.GetProperty("baseSequence").GetInt64()!=1||
               !delta.RootElement.GetProperty("changes").EnumerateArray().Any(x=>x.GetProperty("path")[0].GetString()=="unread"&&x.GetProperty("value").GetInt32()==0))throw new Exception("USB incremental unread state missing");
        }
        if(state.Wire(changed,true,true).Length>=Tab5Service.PackTelemetry(changed).Length/4)throw new Exception("Unchanged catalog still owns USB lane");
        byte[] acknowledged=state.Wire(changed,true,true);state.Confirm(changed,acknowledged);
        if(!ReferenceEquals(state.Wire(changed,true,true),acknowledged))throw new Exception("Repeated publication resent the catalog instead of exact ACKed delta");
        state.Reset();
        using(var reset=JsonDocument.Parse(state.Wire(changed,false,true)))if(reset.RootElement.GetProperty("type").GetString()!="tab5_status")throw new Exception("Reconnect omitted full baseline");
        using(var hello=JsonDocument.Parse("{\"type\":\"tab5_hello\",\"version\":1,\"deviceId\":\""+device+"\",\"usbPacked\":1,\"usbDelta\":1}"))
            if(!Tab5UsbState.SupportsDelta(hello.RootElement,device)||Tab5UsbState.SupportsDelta(hello.RootElement,"ffeeddccbbaa"))throw new Exception("USB delta negotiated without identity");
        foreach(string fields in new[]{"\"usbPacked\":1","\"usbPacked\":0","\"usbPacked\":2","\"usbPacked\":\"1\"","\"usbPacked\":null","\"other\":1"}) {
            using var hello=JsonDocument.Parse("{\"type\":\"tab5_hello\",\"version\":1,\"deviceId\":\""+device+"\","+fields+"}");
            if(Tab5UsbStatus.SupportsPacked(hello.RootElement,device)!=(fields=="\"usbPacked\":1")||
               Tab5UsbStatus.SupportsPacked(hello.RootElement,"ffeeddccbbaa"))throw new Exception("USB bootstrap capability/identity mismatch");
        }
        byte[] full=JsonSerializer.SerializeToUtf8Bytes(new{type="tab5_status",sequence=42,data=new{fixture=new string('x',2000)}});
        byte[] packed=Tab5Service.PackTelemetry(full);
        if(ReferenceEquals(full,packed))throw new Exception("Status fixture did not compress");
        const string ack="@AIBOT {\"type\":\"tab5_ack\",\"deviceId\":\"001122334455\",\"sequence\":42}\n";
        JsonDocument Read(string value) {
            byte[] input=Encoding.UTF8.GetBytes(value);int at=0;
            return Tab5UsbText.ReadBlocking((buffer,_)=>{
                int count=Math.Min(buffer.Length,input.Length-at);input.AsSpan(at,count).CopyTo(buffer);at+=count;return count;
            },"tab5_ack",Environment.TickCount64+1000,CancellationToken.None);
        }
        foreach(int reason in new[]{0,1,2,3,4,5,6}) {
            int reads=0;var writes=new List<byte[]>();
            string rejected=reason==0?"@AIBOT {\"type\":\"tab5_ack_rejected\"}\n":$"@AIBOT {{\"type\":\"tab5_ack_rejected\",\"rejectReason\":{reason}}}\n";
            try {
                var result=await Tab5UsbStatus.SendAsync(full,packed,writes.Add,()=>Task.FromResult(Read(reads++==0?rejected:ack)),_=>{});
                using(result.Ack) {
                    if(reason>2||!result.Retried||reads!=2||writes.Count!=2||!writes[1].AsSpan().SequenceEqual(full)||
                       result.Ack.RootElement.GetProperty("sequence").GetInt64()!=42)throw new Exception("Status recovery changed publication or retry policy");
                }
            } catch(Tab5UsbRejected ex) {
                if(reason<=2||reads!=1||writes.Count!=1||ex.Reason!=(reason==6?-1:reason))throw new Exception("Identity, sequence or unknown rejection retried");
            }
        }
        foreach(bool raw in new[]{false,true}) {
            int reads=0,writes=0;
            try {
                await Tab5UsbStatus.SendAsync(full,raw?full:packed,_=>writes++,()=>{
                    reads++;return Task.FromResult(Read("@AIBOT {\"type\":\"tab5_ack_rejected\",\"rejectReason\":2}\n"));
                },_=>{});
                throw new Exception("Repeated rejection swallowed");
            } catch(Tab5UsbRejected) {
                int expected=raw?1:2;if(reads!=expected||writes!=expected)throw new Exception("Unbounded or raw status retry");
            }
        }
        foreach(Exception failure in new Exception[]{new IOException("fixture"),new TimeoutException("fixture"),new OperationCanceledException()}) {
            int writes=0;
            try {await Tab5UsbStatus.SendAsync(full,packed,_=>writes++,()=>throw failure,_=>{});throw new Exception("Status transport failure swallowed");}
            catch(Exception ex) when(ReferenceEquals(ex,failure)) {if(writes!=1)throw new Exception("Broken transport retried in place");}
        }
        var success=await Tab5UsbStatus.SendAsync(full,packed,_=>{},()=>Task.FromResult(Read(ack)),_=>{});
        using(success.Ack)if(success.Retried)throw new Exception("Healthy status used fallback");
        Console.WriteLine("USB_STATUS_RECOVERY_OK validated hello negotiates packed bootstrap; one raw retry of same publication; identity/sequence/unknown rejection, raw failure, timeout, IO and cancellation remain failures");
    }
}
