using System.Buffers.Binary;
using System.IO.Ports;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;
// Opt-in diagnostic, bridge must be stopped. Reads identity/idle mailbox only;
// never pairs, acknowledges a request, writes configuration or starts an OTA.
internal static class Tab5UsbReadHardwareTest
{
    internal static async Task RunAsync(string name,string expectedId) {
        if(!Tab5Protocol.ValidId(expectedId))throw new ArgumentException("Invalid device ID");
        int frames=0;string firmware="";
        for(int connection=0;connection<5;connection++) {
            using var port=new SerialPort(name,460800){NewLine="\n",ReadTimeout=500,WriteTimeout=2500,DtrEnable=true,RtsEnable=false,Encoding=Encoding.UTF8};
            port.Open();port.DiscardInBuffer();
            port.WriteLine(Tab5Protocol.Prefix+"{\"version\":1,\"type\":\"tab5_ping\"}");
            string line=port.ReadLine().Trim();
            if(!line.StartsWith(Tab5Protocol.Prefix,StringComparison.Ordinal))throw new IOException("Unexpected USB identity response");
            using(var identity=JsonDocument.Parse(line[Tab5Protocol.Prefix.Length..])) {
                var r=identity.RootElement;
                if(r.GetProperty("type").GetString()!="tab5_hello"||r.GetProperty("deviceId").GetString()!=expectedId||r.GetProperty("rpcUsbBinary").GetInt32()!=1)throw new IOException("USB device/capability mismatch");
                firmware=r.GetProperty("firmware").GetString()!;
            }
            for(int i=0;i<60;i++) {
                uint tag=Tab5UsbBinary.Tag();
                port.WriteLine(Tab5Protocol.Prefix+JsonSerializer.Serialize(new{version=1,type="tab5_rpc_binary",deviceId=expectedId,tag,count=0}));
                long until=Environment.TickCount64+2000;
                byte[] header=await Tab5UsbBinary.ReadAsync(port,16,until,CancellationToken.None);
                int count=Tab5UsbBinary.Length(header,tag);
                byte[] body=await Tab5UsbBinary.ReadAsync(port,count,until,CancellationToken.None);
                if(body.Length!=8||BinaryPrimitives.ReadUInt32LittleEndian(body)!=0)throw new IOException("Device mailbox is busy; stopped without consuming its request");
                frames++;
            }
            using var stop=new CancellationTokenSource(70);long began=Environment.TickCount64;
            try{await Tab5UsbBinary.ReadAsync(port,16,began+2000,stop.Token);throw new IOException("Unexpected unsolicited USB bytes");}catch(OperationCanceledException){}
            if(Environment.TickCount64-began>500)throw new IOException("Native USB cancellation exceeded diagnostic budget");
            if(port.ReadTimeout!=500)throw new IOException("USB read timeout was not restored");
        }
        Console.WriteLine($"TAB5_USB_NATIVE_LIVE_OK firmware={firmware}; idleFrames={frames}; reopen=5; cancellation=5; identity/tag/length/idle exact; no configuration writes");
    }
}
