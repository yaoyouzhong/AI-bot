using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal sealed class TrialProtocol(Func<bool> ready, Func<byte[], bool> submit, Func<long>? clock = null) : IDisposable
{
    private readonly Func<long> now = clock ?? (() => Environment.TickCount64);
    private byte[]? key;
    private string offer = "";
    private long deadline;
    internal bool Attempted { get; private set; }
    private static object Error(string error) => new { ok = false, error };
    internal object Handle(JsonElement request)
    {
        string op = Text(request, "op");
        if (op == "cancel") { Dispose(); return new { ok = true, cancelled = true }; }
        if (Attempted) return Error("attempt_finished");
        if (op == "begin") {
            Dispose();
            if (!request.TryGetProperty("confirmPinPage", out var confirmed) || confirmed.ValueKind != JsonValueKind.True)
                return Error("confirm_pin_page");
            if (!ready()) return Error("pin_page_not_ready");
            key = RandomNumberGenerator.GetBytes(32);
            offer = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            deadline = now() + 15000;
            return new { ok = true, offer, key = Convert.ToBase64String(key), expiresMs = 15000 };
        }
        if (op != "submit") return Error("invalid_operation");
        if (key is null || Text(request, "offer") != offer) return Error("offer_changed");
        if (now() >= deadline) { Dispose(); return Error("offer_expired"); }
        // Consume before decryption and before any input. Even a malformed packet
        // cannot be retried using this helper run.
        Attempted = true;
        byte[] packet = [], clear = [];
        try {
            string encoded = Text(request, "sealed");
            if (encoded.Length is < 44 or > 128) return Error("invalid_packet");
            packet = Convert.FromBase64String(encoded);
            if (packet.Length is < 32 or > 92) return Error("invalid_packet");
            clear = new byte[packet.Length - 28];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(packet.AsSpan(0, 12), packet.AsSpan(28), packet.AsSpan(12, 16), clear, Encoding.ASCII.GetBytes(offer));
            if (clear.Length is < 4 or > 64 || clear.Any(b => b < '0' || b > '9')) return Error("digits_only");
            if (!ready()) return Error("pin_page_changed");
            bool unlocked = submit(clear);
            return new { ok = unlocked, unlocked, error = unlocked ? "" : "unlock_not_confirmed" };
        } catch (CryptographicException) { return Error("packet_rejected"); }
        catch (FormatException) { return Error("invalid_packet"); }
        finally { CryptographicOperations.ZeroMemory(clear); CryptographicOperations.ZeroMemory(packet); Dispose(); }
    }
    internal static string Text(JsonElement root, string name) => root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
    public void Dispose() { if (key is not null) CryptographicOperations.ZeroMemory(key); key = null; offer = ""; deadline = 0; }
}
