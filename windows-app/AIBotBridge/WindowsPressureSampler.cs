using System.Runtime.InteropServices;

namespace AIBotBridge;

internal sealed class WindowsPressureSampler : IDisposable
{
    private IntPtr _query;
    private readonly IntPtr[] _counters=new IntPtr[4];
    private static readonly string[] Paths=[@"\System\Processor Queue Length",@"\PhysicalDisk(_Total)\Current Disk Queue Length",
        @"\PhysicalDisk(_Total)\Avg. Disk sec/Transfer",@"\Memory\Pages Input/sec"];
    internal WindowsPressureSampler() {
        if(PdhOpenQuery(null,UIntPtr.Zero,out _query)!=0){_query=IntPtr.Zero;return;}
        for(int i=0;i<Paths.Length;i++)if(PdhAddEnglishCounter(_query,Paths[i],UIntPtr.Zero,out _counters[i])!=0)_counters[i]=IntPtr.Zero;
        PdhCollectQueryData(_query);
    }
    internal SystemPressure Capture(DateTimeOffset now) {
        bool collected=_query!=IntPtr.Zero&&PdhCollectQueryData(_query)==0;
        double? Read(int index) {
            if(!collected||_counters[index]==IntPtr.Zero||PdhGetFormattedCounterValue(_counters[index],0x200,out _,out var value)!=0||
                value.Status>1||!double.IsFinite(value.Value)||value.Value<0)return null;
            return Math.Round(value.Value,index==2?5:1);
        }
        var memory=new MemoryStatus {Length=(uint)Marshal.SizeOf<MemoryStatus>()};
        long? free=GlobalMemoryStatusEx(ref memory)?(long)(memory.AvailablePhysical/1048576):null;
        return new(Read(0),Read(1),Read(2)*1000,Read(3),free,Environment.ProcessorCount,now);
    }
    public void Dispose(){if(_query!=IntPtr.Zero){PdhCloseQuery(_query);_query=IntPtr.Zero;}}
    [StructLayout(LayoutKind.Explicit,Size=16)] private struct CounterValue {
        [FieldOffset(0)]internal uint Status;[FieldOffset(8)]internal double Value;
    }
    [StructLayout(LayoutKind.Sequential)]private struct MemoryStatus {
        internal uint Length,Load;internal ulong TotalPhysical,AvailablePhysical,TotalPageFile,AvailablePageFile,TotalVirtual,AvailableVirtual,AvailableExtendedVirtual;
    }
    [DllImport("pdh.dll",EntryPoint="PdhOpenQueryW",CharSet=CharSet.Unicode)]private static extern uint PdhOpenQuery(string? source,UIntPtr data,out IntPtr query);
    [DllImport("pdh.dll",EntryPoint="PdhAddEnglishCounterW",CharSet=CharSet.Unicode)]private static extern uint PdhAddEnglishCounter(IntPtr query,string path,UIntPtr data,out IntPtr counter);
    [DllImport("pdh.dll")]private static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll")]private static extern uint PdhGetFormattedCounterValue(IntPtr counter,uint format,out uint type,out CounterValue value);
    [DllImport("pdh.dll")]private static extern uint PdhCloseQuery(IntPtr query);
    [DllImport("kernel32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GlobalMemoryStatusEx(ref MemoryStatus value);
}
