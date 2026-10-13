using System.IO.Ports;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;
internal static class Tab5UsbShortPacket
{
    internal static bool SupportsBatch(JsonElement hello)=>
        hello.TryGetProperty("usbRxBatch",out var value)&&value.ValueKind==JsonValueKind.Number&&value.TryGetInt32(out int size)&&size==4096;
    internal static int ReadyWait(JsonElement hello)=>
        hello.TryGetProperty("rpcUsbWait",out var value)&&value.ValueKind==JsonValueKind.Number&&value.TryGetInt32(out int size)&&size==20?20:0;
    // A newline outside the declared binary body/JSON line is ignored by the
    // existing parser. It terminates a 64-byte-aligned CDC write without
    // requiring Windows SerialPort to issue a ZLP. Empty writes stay empty.
    internal static byte[] Terminate(byte[] data) {
        if(data.Length==0||data.Length%64!=0)return data;
        byte[] ended=new byte[data.Length+1];data.CopyTo(ended,0);ended[^1]=(byte)'\n';return ended;
    }
    internal static void Write(SerialPort port,byte[] data) {
        byte[] ended=Terminate(data);port.Write(ended,0,ended.Length);
    }
    internal static void Line(SerialPort port,string line)=>Write(port,Encoding.UTF8.GetBytes(line+port.NewLine));
}
