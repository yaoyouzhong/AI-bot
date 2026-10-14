using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;

namespace AIBotBridge;

// Bounded, numeric-only timing for the isolated gallery benchmark. No packet,
// device identity, artwork, credential, or system-wide network tracing.
internal sealed class Tab5GalleryHostProfile
{
    private readonly object _gate=new();
    private readonly List<object> _samples=[];
    private int _dropped,_idleReads;
    private long _idleUs;
    internal static long Now=>Stopwatch.GetTimestamp();
    internal static long Us(long start,long end)=>(long)((end-start)*1000000d/Stopwatch.Frequency);
    internal void Add(object sample) {lock(_gate){if(_samples.Count<256)_samples.Add(sample);else _dropped++;}}
    internal void Idle(long us){lock(_gate){_idleReads++;_idleUs+=us;}}
    internal string Json {get{lock(_gate)return JsonSerializer.Serialize(new{dropped=_dropped,idleReads=_idleReads,idleUs=_idleUs,samples=_samples});}}
    internal static object Tcp(Socket socket)
    {
        // Windows SDK mstcpip.h: _WSAIORW(IOC_VENDOR,39), DWORD version 0,
        // TCP_INFO_v0 (88 bytes). Read-only and no elevation required.
        if(!OperatingSystem.IsWindows())return new{available=false,reason="platform"};
        try {
            byte[] bytes=new byte[88];int n=socket.IOControl(unchecked((int)0xD8000027),new byte[4],bytes);
            return ParseTcp(bytes.AsSpan(0,n));
        }catch(SocketException ex){return new{available=false,socketError=(int)ex.SocketErrorCode};}
        catch(ObjectDisposedException){return new{available=false,reason="closed"};}
    }
    internal static object ParseTcp(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length<88)return new{available=false,reason="truncated"};
        return new{available=true,mss=BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]),
            ageMs=BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..]),
            rttUs=BinaryPrimitives.ReadUInt32LittleEndian(bytes[20..]),minRttUs=BinaryPrimitives.ReadUInt32LittleEndian(bytes[24..]),
            inFlight=BinaryPrimitives.ReadUInt32LittleEndian(bytes[28..]),cwnd=BinaryPrimitives.ReadUInt32LittleEndian(bytes[32..]),
            sendWindow=BinaryPrimitives.ReadUInt32LittleEndian(bytes[36..]),
            bytesOut=BinaryPrimitives.ReadUInt64LittleEndian(bytes[48..]),bytesIn=BinaryPrimitives.ReadUInt64LittleEndian(bytes[56..]),
            retransBytes=BinaryPrimitives.ReadUInt32LittleEndian(bytes[68..]),duplicateAcks=BinaryPrimitives.ReadUInt32LittleEndian(bytes[76..]),
            timeouts=BinaryPrimitives.ReadUInt32LittleEndian(bytes[80..])};
    }
}

internal sealed partial class Tab5Service
{
    private Tab5GalleryHostProfile? _galleryHostProfile;
    internal Tab5GalleryHostProfile? ActiveGalleryHostProfile=>Volatile.Read(ref _benchmarkRunning)==2?Volatile.Read(ref _galleryHostProfile):null;
    private string GalleryHostDiagnostic=>"\n图库电脑分段："+(Volatile.Read(ref _galleryHostProfile)?.Json??"尚未测速");
}
