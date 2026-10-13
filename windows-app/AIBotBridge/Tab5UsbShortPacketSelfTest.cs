using System.Text;
using System.Text.Json;

namespace AIBotBridge;
internal static class Tab5UsbShortPacketSelfTest
{
    internal static void Run() {
        foreach(int size in Enumerable.Range(0,130).Concat(new[]{511,512,513,4095,4096,4097,49152,65535})) {
            byte[] body=Tab5TransportBenchmark.Pattern(0,size);
            byte[] wire=Tab5UsbShortPacket.Terminate(body);
            int padding=size>0&&size%64==0?1:0;
            if(wire.Length!=size+padding||!wire.AsSpan(0,size).SequenceEqual(body)||padding==1&&wire[^1]!=(byte)'\n')throw new Exception("Short termination changed declared binary bytes");
            if(size>0&&wire.Length%64==0)throw new Exception("CDC batch can remain pending on an exact USB packet boundary");
            if(padding==0&&!ReferenceEquals(body,wire))throw new Exception("Unaligned writes should not allocate/copy");
        }
        foreach(string capability in new[]{"{}","{\"usbRxBatch\":64}","{\"usbRxBatch\":8192}","{\"usbRxBatch\":\"4096\"}","{\"usbRxBatch\":null}","{\"usbRxBatch\":4294967296}"}) {
            using var hello=JsonDocument.Parse(capability);
            if(Tab5UsbShortPacket.SupportsBatch(hello.RootElement))throw new Exception("Invalid/legacy CDC batch capability accepted");
        }
        using(var hello=JsonDocument.Parse("{\"usbRxBatch\":4096}"))if(!Tab5UsbShortPacket.SupportsBatch(hello.RootElement))throw new Exception("CDC batch capability missing");
        for(int count=0;count<64;count++) {
            byte[] line=Encoding.UTF8.GetBytes(Tab5Protocol.Prefix+"{\"text\":\"书法"+new string('x',count)+"\"}\n");
            byte[] ended=Tab5UsbShortPacket.Terminate(line);
            string first=Encoding.UTF8.GetString(ended).Split('\n')[0];
            using var json=JsonDocument.Parse(first[Tab5Protocol.Prefix.Length..]);
            if(json.RootElement.GetProperty("text").GetString()!="书法"+new string('x',count))throw new Exception("UTF8 JSON changed with short termination");
        }
        Console.WriteLine("USB_SHORT_PACKET_OK binary and UTF8 line boundaries, empty/unaligned zero-copy, one outside-body newline, legacy/malformed capability rejection");
    }
}
