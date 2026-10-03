using System.IO.Ports;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

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
        int limit=type=="tab5_rpc"?32768:4096;
        while(true) {
            token.ThrowIfCancellationRequested();long remaining=deadline-Environment.TickCount64;
            if(remaining<=0)throw new TimeoutException("未收到 TAB5 确认："+type);
            int count;
            try {count=read(input,(int)Math.Min(remaining,50));}catch(TimeoutException){continue;}
            if(count<=0||count>input.Length)throw new IOException("TAB5 响应流长度无效。");
            for(int i=0;i<count;i++) {
                if(input[i]!='\n') {
                    if(line.Length>=limit)throw new IOException("TAB5 响应过长。");
                    line.WriteByte(input[i]);continue;
                }
                string value=Encoding.UTF8.GetString(line.GetBuffer(),0,(int)line.Length).Trim();line.SetLength(0);
                if(!value.StartsWith(Tab5Protocol.Prefix,StringComparison.Ordinal))continue;
                JsonDocument doc;
                try {doc=JsonDocument.Parse(value[Tab5Protocol.Prefix.Length..]);}catch(JsonException){continue;}
                if(doc.RootElement.ValueKind==JsonValueKind.Object&&doc.RootElement.TryGetProperty("type",out var t)&&t.ValueKind==JsonValueKind.String) {
                    string? received=t.GetString();
                    if(received==type)return doc;
                    if(received==type+"_rejected") {doc.Dispose();throw new IOException("TAB5 拒绝数据："+type);}
                    if(type=="tab5_wifi_forgotten"&&received=="tab5_wifi_rejected") {doc.Dispose();throw new InvalidOperationException("删除未完成：设备忙、网络已不存在或保存失败。");}
                }
                doc.Dispose();
            }
        }
    }
}
