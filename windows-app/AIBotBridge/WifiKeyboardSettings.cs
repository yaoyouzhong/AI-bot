using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace AIBotBridge;

internal static class WifiKeyboardSettings
{
    private static string AccessFile => Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AI-bot", "wifi-keyboard-access.dat");
    internal static bool Configured => new WifiKeyboardAccessStore().Enabled;
    internal static (int Port, byte[] Password) Load()
    {
        try { return LoadCore(); }
        catch (System.Security.SecurityException) { throw new IOException("keyboard_server_configuration_unreadable"); }
    }
    private static (int Port, byte[] Password) LoadCore()
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (identity.IsSystem || new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
            throw new IOException("keyboard_requires_ordinary_bridge");
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var config = root.OpenSubKey(@"SOFTWARE\TightVNC\Server");
        int Read(string key) => config?.GetValue(key) is int value ? value : -1;
        if (Read("AllowLoopback") != 1 || Read("LoopbackOnly") != 1 || Read("UseVncAuthentication") != 1 ||
            Read("AcceptHttpConnections") != 0 || Read("LogLevel") != 0 || Read("AcceptRfbConnections") != 1 || Read("BlockRemoteInput") != 0 || Read("DisconnectAction") != 0)
            throw new IOException("keyboard_server_configuration_required");
        int port = Read("RfbPort");
        if (port is < 1024 or > 65535 || !Configured) throw new IOException("keyboard_not_configured");
        var access = new WifiKeyboardAccessStore().Read();
        byte[] password = access.Password;
        if (!access.Enabled || !WifiKeyboardAccessStore.Usable(password)) { CryptographicOperations.ZeroMemory(password); throw new IOException("keyboard_not_configured"); }
        return (port, password);
    }

    // Invoked explicitly by the user during candidate setup, never by a network
    // request. This stores a separate VNC access key, never a Windows credential.
    internal static void Configure() { using var form=new WifiUnlockForm();form.ShowDialog(); }

    internal static bool TrustedPeer(TcpClient tcp)=>TrustedPeer(tcp,out _);
    internal static bool TrustedPeer(TcpClient tcp,out string reason)
    {
        reason="endpoint";
        if (tcp.Client.LocalEndPoint is not IPEndPoint local || tcp.Client.RemoteEndPoint is not IPEndPoint remote ||
            !remote.Address.Equals(IPAddress.Loopback)) return false;
        int size = 0; GetExtendedTcpTable(IntPtr.Zero, ref size, false, 2, 5, 0);
        reason="tcp_table_size";
        if (size is < 4 or > 16 * 1024 * 1024) return false;
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try {
            reason="tcp_table";if (GetExtendedTcpTable(buffer, ref size, false, 2, 5, 0) != 0) return false;
            byte[] rows = new byte[size]; Marshal.Copy(buffer, rows, 0, size);
            int count = BinaryPrimitives.ReadInt32LittleEndian(rows);
            reason="no_established_peer";
            for (int i = 0; i < count && 4 + (i + 1) * 24 <= size; i++) {
                var row = rows.AsSpan(4 + i * 24, 24);
                if (BinaryPrimitives.ReadInt32LittleEndian(row) != 5 || !row.Slice(4, 4).SequenceEqual(remote.Address.GetAddressBytes()) || !row.Slice(12, 4).SequenceEqual(local.Address.GetAddressBytes()) ||
                    BinaryPrimitives.ReadUInt16BigEndian(row[8..]) != remote.Port || BinaryPrimitives.ReadUInt16BigEndian(row[16..]) != local.Port) continue;
                int pid = BinaryPrimitives.ReadInt32LittleEndian(row[20..]);
                using var process = Process.GetProcessById(pid);
                reason="peer_session_"+process.SessionId;
                if (process.SessionId != 0) return false; // Official service mode only.
                string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "TightVNC", "tvnserver.exe");
                reason="scm_identity";if (!InstalledService(pid, expected)) return false;
                using var handle = OpenProcess(0x1000, false, pid);
                // TightVNC denies ordinary users even limited process queries.
                // SCM still identifies its exact running PID and protected image.
                reason="process_query_"+Marshal.GetLastPInvokeError();if (handle.IsInvalid) return Marshal.GetLastPInvokeError() == 5;
                var path = new StringBuilder(1024); int chars = path.Capacity;
                reason="process_image";return QueryFullProcessImageName(handle, 0, path, ref chars) &&
                    string.Equals(path.ToString(), expected, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or System.Security.SecurityException) { reason=ex.GetType().Name+"/0x"+ex.HResult.ToString("X8");return false; }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    internal readonly record struct ServiceIdentity(uint Type, uint State, uint ProcessId, string Command, string Account);
    internal static bool ServiceMatches(ServiceIdentity identity, int peerPid, string expected) =>
        peerPid > 0 && identity.Type == 0x10 && identity.State == 4 && identity.ProcessId == (uint)peerPid &&
        string.Equals(identity.Command, $"\"{expected}\" -service", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(identity.Account, "LocalSystem", StringComparison.OrdinalIgnoreCase);
    private static bool InstalledService(int peerPid, string expected)
    {
        using var manager = OpenSCManager(null, null, 1); // Connect only.
        if (manager.IsInvalid) return false;
        using var service = OpenService(manager, "tvnserver", 5); // Query config/status only.
        if (service.IsInvalid) return false;
        IntPtr config = Marshal.AllocHGlobal(8192);
        try {
            if (!QueryServiceConfig(service, config, 8192, out _) ||
                !QueryServiceStatusEx(service, 0, out var status, Marshal.SizeOf<ServiceStatus>(), out _)) return false;
            var info = Marshal.PtrToStructure<ServiceConfig>(config);
            return info.Type == status.Type && ServiceMatches(new(status.Type, status.State, status.ProcessId,
                Marshal.PtrToStringUni(info.Command) ?? "", Marshal.PtrToStringUni(info.Account) ?? ""), peerPid, expected);
        } finally { Marshal.FreeHGlobal(config); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct ServiceConfig
    {
        public uint Type, StartType, ErrorControl;
        public IntPtr Command, LoadOrderGroup;
        public uint TagId;
        public IntPtr Dependencies, Account, DisplayName;
    }
    [StructLayout(LayoutKind.Sequential)] private struct ServiceStatus
    {
        public uint Type, State, ControlsAccepted, Win32ExitCode, ServiceExitCode, CheckPoint, WaitHint, ProcessId, Flags;
    }
    private sealed class ServiceHandle() : SafeHandleZeroOrMinusOneIsInvalid(true)
    {
        protected override bool ReleaseHandle() => CloseServiceHandle(handle);
    }
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ServiceHandle OpenSCManager(string? machine, string? database, uint access);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ServiceHandle OpenService(ServiceHandle manager, string name, uint access);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool QueryServiceConfig(ServiceHandle service, IntPtr config, int size, out int needed);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool QueryServiceStatusEx(ServiceHandle service, int level, out ServiceStatus status, int size, out int needed);
    [DllImport("advapi32.dll")] private static extern bool CloseServiceHandle(IntPtr handle);
    [DllImport("iphlpapi.dll")] private static extern int GetExtendedTcpTable(IntPtr table, ref int size, bool order, int family, int kind, uint reserved);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder path, ref int size);
}
