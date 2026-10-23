using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Calls the built Windows implementation, without launching the bridge,
// accessing a profile, pairing a device or starting any transport.
if (args.Length != 2) throw new ArgumentException("reference DLL and synthetic fixture required");
string dll = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (_, name) => {
    string path = Path.Combine(Path.GetDirectoryName(dll)!, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);
using var fixture = JsonDocument.Parse(File.ReadAllBytes(args[1]));
var f = fixture.RootElement;
byte[] Bytes(string field) => Convert.FromHexString(f.GetProperty(field).GetString()!);
object? Call(string type, string method, params object?[] parameters) => assembly.GetType("AIBotBridge." + type, true)!
    .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, parameters);
void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); }
var key = Bytes("keyHex"); string challenge = f.GetProperty("challenge").GetString()!;
var clear = (byte[])Call("Tab5Protocol", "Decrypt", key, challenge, Bytes("packetHex"), 32768)!;
Check(clear.SequenceEqual(Bytes("clearHex")), "Windows could not open independent fixture");
Check((string)Call("Tab5Protocol", "Proof", key, f.GetProperty("proofMessage").GetString()!)! == f.GetProperty("proof").GetString(), "HMAC mismatch");
for (int i = 0; i < 4; i++) {
    var corrupted = Bytes("packetHex"); corrupted[new[] { 0, 12, 28, corrupted.Length - 1 }[i]] ^= 1;
    try { Call("Tab5Protocol", "Decrypt", key, challenge, corrupted, 32768); throw new InvalidOperationException("tampered packet accepted"); }
    catch (TargetInvocationException ex) when (ex.InnerException is CryptographicException) { }
}
var reply = (byte[])Call("Tab5Protocol", "Encrypt", key, challenge, clear, 32768)!;
var discovery = (byte[])Call("Tab5DiscoveryProtocol", "Respond", Encoding.ASCII.GetBytes(f.GetProperty("discovery").GetString()!),
    f.GetProperty("device").GetString()!, key, "192.168.1.20", 8765)!;
Check(Encoding.ASCII.GetString(discovery) == f.GetProperty("discoveryReply").GetString(), "discovery mismatch");
var settingsType = assembly.GetType("AIBotBridge.Tab5DisplaySettings", true)!;
var validate = settingsType.GetMethod("Validate", BindingFlags.Instance | BindingFlags.NonPublic)!;
var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
validate.Invoke(JsonSerializer.Deserialize(f.GetProperty("settings"), settingsType, options), null);
int rejected = 0;
foreach (var item in f.GetProperty("invalidSettings").EnumerateArray()) {
    try { validate.Invoke(JsonSerializer.Deserialize(item, settingsType, options), null); }
    catch (TargetInvocationException ex) when (ex.InnerException is InvalidDataException) { rejected++; }
}
Check(rejected == 8, "invalid display settings accepted");
Console.WriteLine(JsonSerializer.Serialize(new { result = "WINDOWS_TAB5_REFERENCE_OK", invalidSettingsRejected = rejected,
    tamperedPacketsRejected = 4, responsePacketHex = Convert.ToHexString(reply).ToLowerInvariant() }));
