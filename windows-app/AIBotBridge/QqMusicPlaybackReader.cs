using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace AIBotBridge;

// Independently implemented bounded reader of current-song fields. Compatibility
// signature/layout: Kyle's MIT reference; licenses/qqmusic-clock-reference/.
internal sealed class QqMusicPlaybackReader : IDisposable
{
    internal sealed record Sample(uint Id,string Title,string Artist,string Album,string CoverUrl,double Elapsed,double Duration,bool Playing)
    {
        internal string Identity=>Id.ToString()+"\n"+Title+"\n"+Artist+"\n"+Album;
        internal bool Matches(string title,string artist,string album)=>Title==title.Trim()&&
            Artist.Equals(artist.Trim(),StringComparison.OrdinalIgnoreCase)&&(album.Length==0||Album==album.Trim());
        internal MusicSnapshot Snapshot(DateTimeOffset at)=>new(Title,Artist,Album,Playing,Elapsed,Duration,at);
    }
    internal const string Signature="A2 ? ? ? ? A3 ? ? ? ? C7 05 ? ? ? ? ? ? ? ? A2 ? ? ? ? A3 ? ? ? ? C7 05 ? ? ? ? ? ? ? ? A2 ? ? ? ? A3";
    private static readonly UTF8Encoding Utf8=new(false,true);
    private Process? _owner;
    private SafeProcessHandle? _handle;
    private long _address,_retryAt;
    private string _diagnostic="not sampled";
    internal string Diagnostic=>_diagnostic;
    internal static bool IsQq(string source)=>source.Contains("qqmusic",StringComparison.OrdinalIgnoreCase);

    internal Sample? Read()
    {
        try {
            if(_owner is null||_owner.HasExited) {
                Release();if(Environment.TickCount64<_retryAt)return null;FindPlayer();
            }
            if(_owner is null)return null;
            var sample=ReadCurrent(ReadBytes,_address);
            _diagnostic=sample is null?"QQ Music has no stable playing/paused song":$"qqmusic-native; pid={_owner.Id}; track={sample.Id}; elapsed={sample.Elapsed:0.00}; duration={sample.Duration:0.00}; playing={sample.Playing}";
            return sample;
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException or ArgumentException or OverflowException or BadImageFormatException) {
            _diagnostic="QQ Music read unavailable: "+ex.GetType().Name;Release();_retryAt=Environment.TickCount64+3000;return null;
        }
    }
    private void FindPlayer()
    {
        _retryAt=Environment.TickCount64+10000;
        _diagnostic="QQ Music not running in current session";
        using var self=Process.GetCurrentProcess();
        var processes=Process.GetProcessesByName("QQMusic");
        try {
            foreach(var process in processes.Take(32)) {
                try {
                    if(process.SessionId!=self.SessionId)continue;
                    foreach(ProcessModule module in process.Modules) {
                        if(!module.ModuleName.Equals("QQMusic.dll",StringComparison.OrdinalIgnoreCase))continue;
                        int? rva=LoadLayout(module.FileName);if(rva is null){_diagnostic="QQ Music module architecture/signature is unsupported";continue;}
                        using var handle=OpenProcess(0x1010,false,process.Id);
                        if(handle.IsInvalid){_diagnostic="QQ Music read access denied";continue;}
                        long address=module.BaseAddress.ToInt64()+rva.Value;
                        var sample=ReadCurrent((at,n)=>ReadBytes(handle,at,n),address);
                        // Multiple independent main players are ambiguous. Do not bind one arbitrarily.
                        if(_owner is not null){Release();_diagnostic="Multiple QQ Music players; native selection unavailable";return;}
                        _handle=OpenProcess(0x1010,false,process.Id);
                        if(_handle.IsInvalid){_handle.Dispose();_handle=null;continue;}
                        _owner=process;_address=address;
                        // Keep a structurally supported idle process so the first song is detected promptly.
                        _diagnostic=sample is null?"QQ Music connected; waiting for a song":"QQ Music connected";
                        break;
                    }
                }catch(Exception ex) when(ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException or BadImageFormatException) {
                    _diagnostic="QQ Music locate unavailable: "+ex.GetType().Name;
                }
            }
        }finally{foreach(var process in processes)if(!ReferenceEquals(process,_owner))process.Dispose();}
    }

    private static int? LoadLayout(string path)
    {
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        if(file.Length is <64 or >128*1024*1024)return null;
        using var pe=new PEReader(file);
        if(pe.PEHeaders.CoffHeader.Machine!=Machine.I386||pe.PEHeaders.PEHeader is not {} header)return null;
        var section=pe.PEHeaders.SectionHeaders.FirstOrDefault(s=>s.Name==".text");
        if(section.VirtualSize is <=0 or >32*1024*1024)return null;
        var text=pe.GetSectionData(section.VirtualAddress).GetContent(0,Math.Min(section.VirtualSize,section.SizeOfRawData)).ToArray();
        return Locate(text,header.ImageBase,header.SizeOfImage);
    }
    internal static int? Locate(byte[] text,ulong preferredBase,int imageSize)
    {
        int at=NeteasePlaybackReader.FindUnique(text,Signature);if(at<0)return null;
        ulong address=BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(at+1,4));
        if(address<preferredBase||address-preferredBase>(ulong)Math.Max(0,imageSize-0x74))return null;
        // The first byte-store closes the title initializer. The following
        // size/capacity stores initialize the NEXT string, before its byte-store.
        // Validate both interleaved string initializers, not just the opcodes.
        if(BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(at+6,4))!=address+0x28||
            BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(at+12,4))!=address+0x2c||
            BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(at+16,4))!=15||
            BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(at+21,4))!=address+0x18||
            BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(at+26,4))!=address+0x40||
            BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(at+32,4))!=address+0x44||
            BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(at+36,4))!=15||
            BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(at+41,4))!=address+0x30)return null;
        return checked((int)(address-preferredBase));
    }
    internal static Sample? ReadCurrent(Func<long,int,byte[]> read,long address)
    {
        byte[] initial=read(address,0x74);if(initial.Length!=0x74)throw new IOException("Truncated QQ Music song fields");
        string[] text=Enumerable.Range(0,4).Select(i=>ReadString(read,initial.AsSpan(i*0x18,0x18))).ToArray();
        byte[] final=read(address,0x74);
        if(final.Length!=0x74||!initial.AsSpan(0,0x6c).SequenceEqual(final.AsSpan(0,0x6c)))return null;
        uint id=BinaryPrimitives.ReadUInt32LittleEndian(final.AsSpan(0x60,4));
        uint duration=BinaryPrimitives.ReadUInt32LittleEndian(final.AsSpan(0x68,4)),position=BinaryPrimitives.ReadUInt32LittleEndian(final.AsSpan(0x6c,4)),state=BinaryPrimitives.ReadUInt32LittleEndian(final.AsSpan(0x70,4));
        if(text[0].Length==0||state is not (0 or 1)||duration is 0 or >86_400_000||position>duration+2000)return null;
        return new(id,text[0],text[1],text[2],text[3],Math.Min(position,duration)/1000d,duration/1000d,state==1);
    }
    private static string ReadString(Func<long,int,byte[]> read,ReadOnlySpan<byte> descriptor)
    {
        uint length=BinaryPrimitives.ReadUInt32LittleEndian(descriptor[0x10..]),capacity=BinaryPrimitives.ReadUInt32LittleEndian(descriptor[0x14..]);
        if(length>4096||capacity<length||capacity>16*1024*1024)throw new IOException("Invalid QQ Music text bounds");
        if(length==0)return "";
        byte[] bytes;
        if(capacity<16){if(length>15)throw new IOException("Invalid inline QQ Music text");bytes=descriptor[..(int)length].ToArray();}
        else {
            uint pointer=BinaryPrimitives.ReadUInt32LittleEndian(descriptor);
            if(pointer<0x10000||(ulong)pointer+length>0x1_0000_0000)throw new IOException("Invalid QQ Music text pointer");
            bytes=read(pointer,checked((int)length));if(bytes.Length!=length)throw new IOException("Truncated QQ Music text");
        }
        string value=Utf8.GetString(bytes).Trim();
        if(value.Any(char.IsControl))throw new IOException("Invalid QQ Music text");return value;
    }
    private byte[] ReadBytes(long address,int length)=>ReadBytes(_handle??throw new IOException("QQ Music handle unavailable"),address,length);
    private static byte[] ReadBytes(SafeProcessHandle handle,long address,int length)
    {
        if(address<=0||length is <=0 or >4096)throw new IOException("Invalid QQ Music read");
        var bytes=new byte[length];if(!ReadProcessMemory(handle,(nint)address,bytes,(nuint)length,out nuint count)||count!=(nuint)length)throw new Win32Exception(Marshal.GetLastWin32Error());return bytes;
    }
    private void Release(){_handle?.Dispose();_handle=null;_owner?.Dispose();_owner=null;_address=0;}
    public void Dispose()=>Release();
    [DllImport("kernel32.dll",SetLastError=true)] private static extern SafeProcessHandle OpenProcess(uint access,[MarshalAs(UnmanagedType.Bool)]bool inherit,int pid);
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(SafeProcessHandle handle,nint address,byte[] bytes,nuint count,out nuint read);
}
