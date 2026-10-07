using System.Buffers.Binary;
using System.IO.Ports;
using System.Security.Cryptography;

namespace AIBotBridge;
internal static class Tab5UsbBinary
{
    internal const int Maximum=65535;
    internal const int LargeChunk=Maximum-9;
    internal static int Chunk(System.Text.Json.JsonElement hello,bool binary) {
        if(binary&&hello.TryGetProperty("rpcUsbBinaryChunk",out var large)&&large.TryGetInt32(out int n)&&n==LargeChunk)return LargeChunk;
        return hello.TryGetProperty("rpcUsbChunk",out var old)&&old.TryGetInt32(out int legacy)&&legacy==16384?16384:2048;
    }
    internal static int Length(byte[] header,uint tag) {
        if(header.Length!=16||!header.AsSpan(0,4).SequenceEqual("AIB2"u8)||BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4))!=tag||
            BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12))!=1)throw new IOException("USB binary identity/status mismatch");
        uint length=BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8));
        if(length>Maximum)throw new IOException("USB binary packet too large");return (int)length;
    }
    internal static uint Tag()=>BinaryPrimitives.ReadUInt32LittleEndian(RandomNumberGenerator.GetBytes(4));
    internal static Task<byte[]> ReadAsync(SerialPort port,int length,long deadline,CancellationToken token) => Task.Run(()=> {
        // SerialStream's DataReceived callback races with handler removal, even
        // during SerialPort.Dispose. Do not subscribe. A native blocking read
        // wakes on incoming bytes without a timer-poll delay or a lost signal.
        // _usbGate owns the port until this worker has stopped; cancellation
        // cannot leave an abandoned reader consuming the next transaction.
        int previous=port.ReadTimeout;
        try{return ReadBlocking((bytes,at,want,timeout)=>{port.ReadTimeout=timeout;return port.Read(bytes,at,want);},length,deadline,token);}
        finally{try{port.ReadTimeout=previous;}catch(IOException){}catch(InvalidOperationException){}}
    });
    internal static byte[] ReadBlocking(Func<byte[],int,int,int,int> read,int length,long deadline,CancellationToken token) {
        if(length is <0 or >Maximum)throw new IOException("USB binary read length invalid");
        var bytes=new byte[length];int at=0;
        while(at<length) {
            token.ThrowIfCancellationRequested();long remaining=deadline-Environment.TickCount64;
            if(remaining<=0)throw new IOException("USB binary response timeout");
            int count;
            try{count=read(bytes,at,length-at,(int)Math.Min(remaining,50));}catch(TimeoutException){continue;}
            if(count<=0||count>length-at)throw new IOException("USB binary stream ended or exceeded frame");
            at+=count;
        }
        return bytes;
    }
    internal static async Task<byte[]> ReadAsync(Func<byte[],int,int,int> read,int length,long deadline,CancellationToken token,Func<CancellationToken,Task>? wait=null) {
        if(length is <0 or >Maximum)throw new IOException("USB binary read length invalid");
        var bytes=new byte[length];int at=0;
        while(at<length) {
            token.ThrowIfCancellationRequested();
            if(Environment.TickCount64>=deadline)throw new IOException("USB binary response timeout");
            int count=read(bytes,at,length-at);
            if(count==0){if(wait is null)await Task.Delay(1,token);else await wait(token);continue;}
            if(count<0||count>length-at)throw new IOException("USB binary reader exceeded frame");
            at+=count;
        }
        return bytes;
    }
}
