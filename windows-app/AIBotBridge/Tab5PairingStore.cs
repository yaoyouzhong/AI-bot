using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal sealed record Tab5Pairing(string DeviceId, string UsbIdentity, string Key);

internal sealed class Tab5PairingStore
{
    private readonly object _gate = new();
    private readonly string _path;
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AI-bot TAB5 pairing v1");
    private Tab5Pairing? _current;
    internal Tab5Pairing? Current { get { lock(_gate) return _current; } }
    internal Tab5PairingStore(string? path = null)
    {
        _path=path??Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData),"AI-bot","tab5-pairing.dat");
        if(!File.Exists(_path)) return;
        // Fail closed: damaged or foreign-user pairing is never silently replaced.
        var clear=ProtectedData.Unprotect(File.ReadAllBytes(_path),Entropy,DataProtectionScope.CurrentUser);
        try {
            var value=JsonSerializer.Deserialize<Tab5Pairing>(clear);
            if(value is null || !Tab5Protocol.ValidId(value.DeviceId) || Convert.FromBase64String(value.Key).Length!=32)
                throw new InvalidDataException("TAB5 配对记录无效，请重新通过 USB 配对。");
            _current=value;
        } finally { CryptographicOperations.ZeroMemory(clear); }
    }
    internal Tab5Pairing Pair(string id,string usbIdentity)
    {
        if(!Tab5Protocol.ValidId(id) || !FlashDeviceDiscovery.IsUsbIdentity(usbIdentity)) throw new ArgumentException("Invalid TAB5 identity");
        lock(_gate) {
            var value = _current is { } existing && existing.DeviceId==id
                ? existing with {UsbIdentity=usbIdentity}
                : new Tab5Pairing(id,usbIdentity,Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var clear=JsonSerializer.SerializeToUtf8Bytes(value);
            try { File.WriteAllBytes(_path+".tmp",ProtectedData.Protect(clear,Entropy,DataProtectionScope.CurrentUser)); File.Move(_path+".tmp",_path,true); }
            finally { CryptographicOperations.ZeroMemory(clear); }
            _current=value; return value;
        }
    }
}
