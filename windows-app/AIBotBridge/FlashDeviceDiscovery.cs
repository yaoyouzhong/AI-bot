using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace AIBotBridge;

internal sealed record FlashUsbDevice(string Port, string Identity, string Name);

// A COM number alone is not identity. Only currently present USB serial devices
// are candidates; Bluetooth and old registry entries must not become targets.
internal static class FlashDeviceDiscovery
{
    internal static IReadOnlyList<FlashUsbDevice> Read()
    {
        // Some CH340 drivers do not keep GUID_DEVINTERFACE_COMPORT registered
        // while the bridge owns the port. Enumerate present Ports-class devices,
        // as Device Manager does, rather than relying on that optional interface.
        var guid = new Guid("4d36e978-e325-11ce-bfc1-08002be10318"); // GUID_DEVCLASS_PORTS
        var set = SetupDiGetClassDevsW(ref guid, null, IntPtr.Zero, 2); // DIGCF_PRESENT
        if (set == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var result = new List<FlashUsbDevice>();
        try
        {
            for (uint index = 0; ; index++)
            {
                var data = new DeviceInfo { Size = (uint)Marshal.SizeOf<DeviceInfo>() };
                if (!SetupDiEnumDeviceInfo(set, index, ref data))
                {
                    if (Marshal.GetLastWin32Error() != 259) throw new Win32Exception(Marshal.GetLastWin32Error());
                    break;
                }
                var identity = new StringBuilder(1024);
                if (!SetupDiGetDeviceInstanceIdW(set, ref data, identity, identity.Capacity, out _)) continue;
                var id = identity.ToString();
                if (!IsUsbIdentity(id)) continue;
                var keyHandle = SetupDiOpenDevRegKey(set, ref data, 1, 0, 1, 0x0001); // KEY_QUERY_VALUE
                if (keyHandle == new IntPtr(-1)) continue;
                using var key = RegistryKey.FromHandle(new SafeRegistryHandle(keyHandle, true));
                if (key.GetValue("PortName") is not string port || !System.Text.RegularExpressions.Regex.IsMatch(port, @"^COM[1-9][0-9]*$")) continue;
                var buffer = new byte[2048];
                var name = SetupDiGetDeviceRegistryPropertyW(set, ref data, 12, out _, buffer, buffer.Length, out _) ? Encoding.Unicode.GetString(buffer).TrimEnd('\0') : "USB device";
                result.Add(new(port, id, name));
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return result.OrderBy(d => d.Port).ToArray();
    }

    internal static bool IsUsbIdentity(string id) => id.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase) || id.StartsWith("FTDIBUS\\", StringComparison.OrdinalIgnoreCase);

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInfo { public uint Size; public Guid ClassGuid; public uint DevInst; public UIntPtr Reserved; }
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevsW(ref Guid guid, string? enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref DeviceInfo data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiGetDeviceInstanceIdW(IntPtr set, ref DeviceInfo data, StringBuilder id, int length, out int required);
    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiOpenDevRegKey(IntPtr set, ref DeviceInfo data, uint scope, uint profile, uint type, uint access);
    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiGetDeviceRegistryPropertyW(IntPtr set, ref DeviceInfo data, uint property, out uint type, byte[] buffer, int length, out int required);
    [DllImport("setupapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
}

internal sealed class FlashDeviceSelection
{
    private HashSet<string>? _previous;
    private string? _expectedIdentity;
    private readonly string? _bridgePort;
    private DateTime? _missingSince;
    internal FlashUsbDevice? Selected { get; private set; }
    internal bool RecognizedBridge { get; private set; }
    internal string Message { get; private set; } = "正在识别小屏…";
    internal FlashDeviceSelection(string? bridgePort = null) { _bridgePort = bridgePort; }

    internal void Reset(IReadOnlyList<FlashUsbDevice> devices)
    {
        _previous = devices.Select(d => d.Identity).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _expectedIdentity = null; Selected = null; RecognizedBridge = false; _missingSince = null;
        _previous = null;
        Update(devices);
    }

    internal void Update(IReadOnlyList<FlashUsbDevice> devices)
    {
        var canSelectSingle = _previous is null || _previous.Count == 0;
        var now = devices.Select(d => d.Identity).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (_previous is null)
        {
            _previous = now;
            var bridge = devices.SingleOrDefault(d => d.Port.Equals(_bridgePort, StringComparison.OrdinalIgnoreCase));
            if (bridge is not null) { Selected = bridge; _expectedIdentity = bridge.Identity; RecognizedBridge = true; }
        }
        else if (_expectedIdentity is null)
        {
            var added = devices.Where(d => !_previous.Contains(d.Identity)).ToArray();
            if (added.Length == 1) { Selected = added[0]; _expectedIdentity = Selected.Identity; }
        }
        if (_expectedIdentity is null && canSelectSingle && devices.Count == 1)
            _expectedIdentity = devices[0].Identity;
        if (_expectedIdentity is not null) Selected = devices.SingleOrDefault(d => d.Identity.Equals(_expectedIdentity, StringComparison.OrdinalIgnoreCase));
        _previous = now;
        if (Selected is not null) _missingSince = null;
        else _missingSince ??= DateTime.UtcNow;
        Message = Selected is not null ? (RecognizedBridge ? "小屏已连接" : "USB 设备已连接")
            : _expectedIdentity is not null ? (DateTime.UtcNow - _missingSince < TimeSpan.FromSeconds(3) ? "正在确认连接…" : "等待小屏重新连接…")
            : devices.Count == 0 ? "请连接小屏" : "发现多个 USB 设备，请拔插一次小屏";
    }

    internal void ScanFailed()
    {
        // Keep identity across transient SetupAPI/registry errors. Disable actions
        // until a successful scan confirms the same physical device again.
        Selected = null;
        Message = "正在重新检测连接…";
    }

    internal static void RequireSame(FlashUsbDevice expected, IReadOnlyList<FlashUsbDevice> devices)
    {
        if (!devices.Any(d => d.Port == expected.Port && d.Identity.Equals(expected.Identity, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("小屏连接已变化，请重新识别后再开始。");
    }
}
