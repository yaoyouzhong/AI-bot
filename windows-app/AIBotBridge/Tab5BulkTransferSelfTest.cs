using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5BulkTransferSelfTest
{
    internal static void Run(string directory) {
        foreach(int size in new[]{16385,49152}) {
            byte[] bytes=Tab5TransportBenchmark.Pattern(0,size);
            byte[] metadata=JsonSerializer.SerializeToUtf8Bytes(new{status=200,body=new{offset=0}});
            byte[] wire=Tab5RpcBinary.Pack(metadata,bytes,bulk:true);
            var opened=Tab5RpcBinary.Open(wire,true);using(opened.Metadata) {
                if(!opened.Data.Span.SequenceEqual(bytes))throw new Exception("Bulk bytes changed");
            }
            File.WriteAllBytes(Path.Combine(directory,$"rpc-binary-{size}.bin"),wire);
            wire[3]=(byte)'1';
            try{Tab5RpcBinary.Open(wire,true);throw new Exception("Legacy payload bounds widened");}catch(ArgumentException){}
            using var request=JsonDocument.Parse(JsonSerializer.Serialize(new{offset=0,count=size}));
            if(Tab5TransportBenchmark.Handle(request.RootElement,bytes,true).Status!=400)throw new Exception("Unnegotiated bulk accepted");
            if(Tab5TransportBenchmark.Handle(request.RootElement,bytes,true,true).Status!=200)throw new Exception("Negotiated bulk rejected");
            bytes[^1]^=1;
            if(Tab5TransportBenchmark.Handle(request.RootElement,bytes,true,true).Status!=400)throw new Exception("Corrupt bulk accepted");
        }
        try{Tab5RpcBinary.Pack("{}"u8.ToArray(),new byte[49153],bulk:true);throw new Exception("Bulk overflow accepted");}catch(ArgumentException){}
        byte[] duplicate=Tab5RpcBinary.Pack("{\"data\":\"\"}"u8.ToArray(),new byte[8],false,true);
        try{Tab5RpcBinary.Open(duplicate);throw new Exception("Duplicate bulk data accepted");}catch(ArgumentException){}
        foreach(int fragment in new[]{1,7,512}) {
            byte[] wire=Encoding.UTF8.GetBytes("log\n@AIBOT invalid\n@AIBOT {\"type\":\"other\"}\n@AIBOT {\"type\":\"tab5_ack\",\"text\":\"语音照片\"}\n");int cursor=0,reads=0;
            using var result=Tab5UsbText.ReadBlocking((buffer,timeout)=> {
                if(timeout is <1 or >50)throw new Exception("Text cancellation bound");
                if(reads++==0)throw new TimeoutException();
                int n=Math.Min(fragment,Math.Min(buffer.Length,wire.Length-cursor));Array.Copy(wire,cursor,buffer,0,n);cursor+=n;return n;
            },"tab5_ack",Environment.TickCount64+2000,CancellationToken.None);
            if(result.RootElement.GetProperty("text").GetString()!="语音照片")throw new Exception("Fragmented UTF-8 changed");
        }
        using(var stop=new CancellationTokenSource()) {
            int reads=0;
            try {Tab5UsbText.ReadBlocking((_,_)=>{reads++;stop.Cancel();throw new TimeoutException();},"tab5_ack",Environment.TickCount64+1000,stop.Token);throw new Exception("Text cancellation ignored");}catch(OperationCanceledException){}
            if(reads!=1)throw new Exception("Abandoned text read");
        }
        try{Tab5UsbText.ReadBlocking((_,t)=>{Thread.Sleep(t);throw new TimeoutException();},"tab5_ack",Environment.TickCount64+5,CancellationToken.None);throw new Exception("Text deadline ignored");}catch(TimeoutException){}
        foreach(string wire in new[]{"@AIBOT {\"type\":\"tab5_ack_rejected\"}\n",new string('x',4097)}) {
            byte[] data=Encoding.UTF8.GetBytes(wire);int at=0;
            try{Tab5UsbText.ReadBlocking((b,_)=>{int n=Math.Min(b.Length,data.Length-at);Array.Copy(data,at,b,0,n);at+=n;return n;},"tab5_ack",Environment.TickCount64+1000,CancellationToken.None);throw new Exception("Rejected/overlong text accepted");}catch(IOException){}
        }
        Console.WriteLine("TAB5_BULK_TRANSFER_OK 48K bounds, legacy rejection, byte integrity, fragmented USB UTF8, timeout/cancel/reject ownership");
    }
}
