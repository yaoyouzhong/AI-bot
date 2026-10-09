using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace AIBotBridge;

// Independent bounded reader. The two instruction signatures / field layout are
// compatibility facts from Kyle's MIT reference; see licenses/netease-clock-reference/.
// Only the current user's cloudmusic.dll and current song fields are read. No writes/injection.
internal sealed class NeteasePlaybackReader : IDisposable
{
    internal sealed record Clock(string Id, double Elapsed, double Duration, bool Playing);
    internal sealed record Sample(Clock Clock, NeteaseTrackMetadata Track);
    internal sealed record Layout(int AudioRva, int ClockRva);
    internal const string AudioSignature = "48 8D 0D ? ? ? ? E8 ? ? ? ? 48 8D 0D ? ? ? ? E8 ? ? ? ? 90 48 8D 0D ? ? ? ? E8 ? ? ? ? 48 8D 05 ? ? ? ? 48 8D A5 ? ? ? ? 5F 5D C3 CC CC CC CC CC 48 89 4C 24 ? 55 57 48 81 EC ? ? ? ? 48 8D 6C 24 ? 48 8D 7C 24";
    internal const string ClockSignature = "66 0F 2E 0D ? ? ? ? 7A ? 75 ? 66 0F 2E 15";
    private Process? _owner;
    private SafeProcessHandle? _handle;
    private long _audio, _clock;
    private long _retryAt;
    private string? _moduleKey;
    private Layout? _layout;
    private NeteaseTrackMetadata? _track;
    private string _diagnostic = "not sampled";
    internal string Diagnostic => Volatile.Read(ref _diagnostic);

    internal Sample? Read(string source, string title, string artist, string album)
    {
        if (!IsNetease(source)) return null;
        try
        {
            if (_owner is null || _owner.HasExited)
            {
                ReleaseProcess();
                if (Environment.TickCount64 < _retryAt) return null;
                FindPlayer();
            }
            if (_owner is null) return null;
            var clock = ReadClock();
            if (clock is null) { _diagnostic = "no valid playback clock"; return null; }
            if (_track?.Id != clock.Id) _track = NeteaseTrackMetadata.Read(clock.Id);
            // Match SMTC to the native song before enriching it. Never mix a previous title with a new clock.
            if (_track is null || !_track.Matches(title, artist, album)) { _diagnostic = "track metadata missing or mismatched"; return null; }
            var latest = ReadClock();
            if (latest is null || latest.Id != clock.Id) { _diagnostic = "track changed during sample"; return null; }
            _diagnostic = $"netease-native; pid={_owner.Id}; track={latest.Id}; elapsed={latest.Elapsed:0.00}; duration={latest.Duration:0.00}; playing={latest.Playing}";
            return new(latest, _track);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or
                                      InvalidOperationException or ArgumentException or JsonException or OverflowException or BadImageFormatException)
        {
            _diagnostic = "read unavailable: " + ex.GetType().Name;
            // A transient playlist rewrite must not force another module scan.
            if (ex is Win32Exception) { ReleaseProcess(); _retryAt = Environment.TickCount64 + 3000; }
            return null;
        }
    }

    internal static bool IsNetease(string source) => source.Contains("cloudmusic", StringComparison.OrdinalIgnoreCase) ||
        source.Contains("netease", StringComparison.OrdinalIgnoreCase);

    private void FindPlayer()
    {
        _retryAt = Environment.TickCount64 + 30000;
        using var currentProcess = Process.GetCurrentProcess();
        int session = currentProcess.SessionId;
        var candidates = Process.GetProcessesByName("cloudmusic");
        try { foreach (var process in candidates)
        {
            bool keep = false;
            try
            {
                if (process.SessionId != session) continue;
                foreach (ProcessModule module in process.Modules)
                {
                    if (!module.ModuleName.Equals("cloudmusic.dll", StringComparison.OrdinalIgnoreCase)) continue;
                    var info = new FileInfo(module.FileName);
                    var key = module.FileName + ":" + info.Length + ":" + info.LastWriteTimeUtc.Ticks;
                    if (_moduleKey != key)
                    {
                        _layout = LoadLayout(module.FileName);
                        _moduleKey = key;
                    }
                    if (_layout is null) { _diagnostic = "unsupported module signatures or architecture"; break; }
                    var handle = OpenProcess(0x1010, false, process.Id); // VM_READ | QUERY_LIMITED_INFORMATION
                    if (handle.IsInvalid) { handle.Dispose(); _diagnostic = "process read access denied"; break; }
                    _handle = handle;
                    _audio = checked(module.BaseAddress.ToInt64() + _layout.AudioRva);
                    _clock = checked(module.BaseAddress.ToInt64() + _layout.ClockRva);
                    if (ReadClock() is not null)
                    {
                        _owner = process; keep = true; _retryAt = 0;
                        return;
                    }
                    _handle.Dispose(); _handle = null;
                    break;
                }
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException or BadImageFormatException)
            {
                _diagnostic = "locate unavailable: " + ex.GetType().Name;
                _handle?.Dispose(); _handle = null;
            }
            finally { if (!keep) process.Dispose(); }
        } } finally { foreach (var candidate in candidates) if (!ReferenceEquals(candidate, _owner)) candidate.Dispose(); }
    }

    private Clock? ReadClock()
    {
        var id = ReadIdentity();
        int state = BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(_audio + 0x60, 4));
        double duration = BitConverter.ToDouble(ReadBytes(_audio + 0xa8, 8));
        double elapsed = BitConverter.ToDouble(ReadBytes(_clock, 8));
        if (id != ReadIdentity() || !ValidClock(id, elapsed, duration, state)) return null;
        return new(id, Math.Clamp(elapsed, 0, duration), duration, state == 1);
    }

    internal static bool ValidClock(string id, double elapsed, double duration, int state) =>
        id.Length is > 0 and <= 32 && id.All(char.IsAsciiDigit) && state is 1 or 2 &&
        double.IsFinite(duration) && duration is > 0 and <= 86400 && double.IsFinite(elapsed) && elapsed >= 0 && elapsed <= duration + 2;

    private string ReadIdentity()
    {
        long pointer = BinaryPrimitives.ReadInt64LittleEndian(ReadBytes(_audio + 0x50, 8));
        if (pointer == 0) return "";
        long storage = checked(pointer + 0x10);
        ulong length = BinaryPrimitives.ReadUInt64LittleEndian(ReadBytes(storage + 0x10, 8));
        if (length is 0 or > 128) return "";
        if (length > 15) storage = BinaryPrimitives.ReadInt64LittleEndian(ReadBytes(storage, 8));
        var raw = Encoding.UTF8.GetString(ReadBytes(storage, (int)length));
        int separator = raw.IndexOf('_');
        return separator < 0 ? raw : raw[..separator];
    }

    private byte[] ReadBytes(long address, int length)
    {
        if (_handle is null || _handle.IsInvalid || address <= 0 || length is <= 0 or > 4096) throw new IOException("Invalid playback address");
        var bytes = new byte[length];
        if (!ReadProcessMemory(_handle, checked((nint)address), bytes, (nuint)length, out nuint read) || read != (nuint)length)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return bytes;
    }

    private static Layout? LoadLayout(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (file.Length is < 64 or > 128 * 1024 * 1024) return null;
        using var pe = new PEReader(file);
        if (pe.PEHeaders.CoffHeader.Machine != Machine.Amd64 || !Environment.Is64BitProcess || pe.PEHeaders.PEHeader is not { } header) return null;
        var section = pe.PEHeaders.SectionHeaders.FirstOrDefault(s => s.Name == ".text");
        if (section.VirtualSize is <= 0 or > 32 * 1024 * 1024) return null;
        var text = pe.GetSectionData(section.VirtualAddress).GetContent(0, Math.Min(section.VirtualSize, section.SizeOfRawData)).ToArray();
        return Locate(text, section.VirtualAddress, header.SizeOfImage);
    }

    internal static Layout? Locate(byte[] text, int rva, int imageSize)
    {
        int audio = FindUnique(text, AudioSignature), clock = FindUnique(text, ClockSignature);
        if (audio < 0 || clock < 0) return null;
        long audioRva = (long)rva + audio + 7 + BinaryPrimitives.ReadInt32LittleEndian(text.AsSpan(audio + 3, 4));
        long clockRva = (long)rva + clock + 8 + BinaryPrimitives.ReadInt32LittleEndian(text.AsSpan(clock + 4, 4));
        return audioRva > 0 && audioRva <= imageSize - 0xb0 && clockRva > 0 && clockRva <= imageSize - 8
            ? new((int)audioRva, (int)clockRva) : null;
    }

    internal static int FindUnique(byte[] data, string signature)
    {
        var pattern = signature.Split(' ').Select(s => s == "?" ? -1 : Convert.ToInt32(s, 16)).ToArray();
        int found = -1;
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++) if (pattern[j] >= 0 && pattern[j] != data[i + j]) { match = false; break; }
            if (!match) continue;
            if (found >= 0) return -1; // Ambiguous signatures never select an arbitrary address.
            found = i;
        }
        return found;
    }

    private void ReleaseProcess()
    {
        _handle?.Dispose(); _handle = null;
        _owner?.Dispose(); _owner = null;
        _track = null;
    }
    public void Dispose() => ReleaseProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(SafeProcessHandle process, nint address, byte[] buffer, nuint count, out nuint read);
}
