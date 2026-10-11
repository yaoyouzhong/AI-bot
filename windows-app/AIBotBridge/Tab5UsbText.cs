using System.IO.Ports;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal sealed class Tab5UsbRejected(string expected,int reason) : IOException("TAB5 拒绝数据："+expected)
{
    internal string Expected=>expected;
    internal int Reason=>reason;
}

internal sealed class Tab5UsbReplyTimeout(string expected,int bytes,int lines,int pending,long elapsed) : TimeoutException("未收到 TAB5 确认："+expected)
{
    // Only protocol names and numeric counters; never retain received content.
    internal string Diagnostic=>$"expected={expected}; receivedBytes={bytes}; completeLines={lines}; pendingBytes={pending}; readMs={elapsed}";
}

internal static class Tab5UsbText
{
    internal static Task<JsonDocument> ReadAsync(SerialPort port,string type,CancellationToken token)=>Task.Run(()=> {
        int previous=port.ReadTimeout;
        try {return ReadBlocking((bytes,timeout)=>{port.ReadTimeout=timeout;return port.Read(bytes,0,bytes.Length);},type,Environment.TickCount64+4000,token);}
        finally {try{port.ReadTimeout=previous;}catch(IOException){}catch(InvalidOperationException){}}
    });
    // The caller retains the serial gate until this worker exits. No callbacks,
    // abandoned reads or timer polling; UTF-8 is decoded only after a full line.
    internal static JsonDocument ReadBlocking(Func<byte[],int,int> read,string type,long deadline,CancellationToken token) {
        var input=new byte[512];using var line=new MemoryStream();
        long started=Environment.TickCount64;int receivedBytes=0,completeLines=0;
        int limit=type=="tab5_rpc"?32768:type=="tab5_crash_diagnostic"?6144:4096;
        while(true) {
            token.ThrowIfCancellationRequested();long remaining=deadline-Environment.TickCount64;
            if(remaining<=0)throw new Tab5UsbReplyTimeout(type,receivedBytes,completeLines,(int)line.Length,Environment.TickCount64-started);
            int count;
            try {count=read(input,(int)Math.Min(remaining,50));}catch(TimeoutException){continue;}
            if(count<=0||count>input.Length)throw new IOException("TAB5 响应流长度无效。");
            receivedBytes+=count;
            for(int i=0;i<count;i++) {
                if(input[i]!='\n') {
                    if(line.Length>=limit)throw new IOException("TAB5 响应过长。");
                    line.WriteByte(input[i]);continue;
                }
                completeLines++;
                string value=Encoding.UTF8.GetString(line.GetBuffer(),0,(int)line.Length).Trim();line.SetLength(0);
                if(!value.StartsWith(Tab5Protocol.Prefix,StringComparison.Ordinal))continue;
                JsonDocument doc;
                try {doc=JsonDocument.Parse(value[Tab5Protocol.Prefix.Length..]);}catch(JsonException){continue;}
                if(doc.RootElement.ValueKind==JsonValueKind.Object&&doc.RootElement.TryGetProperty("type",out var t)&&t.ValueKind==JsonValueKind.String) {
                    string? received=t.GetString();
                    if(received==type)return doc;
                    if(received==type+"_rejected") {
                        int reason=doc.RootElement.TryGetProperty("rejectReason",out var r)?r.TryGetInt32(out int code)&&code is >=0 and <=5?code:-1:0;
                        doc.Dispose();throw new Tab5UsbRejected(type,reason);
                    }
                    if(type=="tab5_wifi_forgotten"&&received=="tab5_wifi_rejected") {doc.Dispose();throw new InvalidOperationException("删除未完成：设备忙、网络已不存在或保存失败。");}
                }
                doc.Dispose();
            }
        }
    }
}
