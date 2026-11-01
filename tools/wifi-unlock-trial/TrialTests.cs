using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class TrialTests
{
    private static JsonElement Call(TrialProtocol trial, object request)
    {
        using var input = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(request));
        return JsonSerializer.SerializeToElement(trial.Handle(input.RootElement));
    }
    private static (string Offer, byte[] Key) Begin(TrialProtocol trial)
    {
        var reply = Call(trial, new { op = "begin", confirmPinPage = true });
        return (reply.GetProperty("offer").GetString()!, Convert.FromBase64String(reply.GetProperty("key").GetString()!));
    }
    private static string Seal(string offer, byte[] key, byte[] pin)
    {
        byte[] packet = new byte[28 + pin.Length]; RandomNumberGenerator.Fill(packet.AsSpan(0, 12));
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(packet.AsSpan(0, 12), pin, packet.AsSpan(28), packet.AsSpan(12, 16), Encoding.ASCII.GetBytes(offer));
        return Convert.ToBase64String(packet);
    }
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); }
    internal static int Run()
    {
        int calls = 0; byte[]? received = null; long now = 100;
        using (var trial = new TrialProtocol(() => true, pin => { calls++; received = pin; return true; }, () => now)) {
            Check(Call(trial, new { op = "begin" }).GetProperty("error").GetString() == "confirm_pin_page", "confirmation");
            var (offer, key) = Begin(trial);
            byte[] fixture = [49, 50, 51, 52]; string sealedPin = Seal(offer, key, fixture);
            var request = new { op = "submit", offer, @sealed = sealedPin };
            Check(Call(trial, request).GetProperty("unlocked").GetBoolean(), "valid packet");
            Check(calls == 1 && received!.All(b => b == 0), "one input and clear erasure");
            Check(Call(trial, request).GetProperty("error").GetString() == "attempt_finished" && calls == 1, "replay");
            CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(fixture);
        }
        using (var trial = new TrialProtocol(() => true, _ => { calls++; return true; }, () => now)) {
            var (offer, key) = Begin(trial); now += 15000;
            Check(Call(trial, new { op = "submit", offer, @sealed = Seal(offer, key, [49, 50, 51, 52]) }).GetProperty("error").GetString() == "offer_expired", "expiry");
            CryptographicOperations.ZeroMemory(key);
        }
        using (var trial = new TrialProtocol(() => true, _ => { calls++; return true; })) {
            var (offer, key) = Begin(trial); var packet = Convert.FromBase64String(Seal(offer, key, [49, 50, 51, 52])); packet[^1] ^= 1;
            Check(Call(trial, new { op = "submit", offer, @sealed = Convert.ToBase64String(packet) }).GetProperty("error").GetString() == "packet_rejected", "tamper");
            Check(trial.Attempted, "tamper consumed"); CryptographicOperations.ZeroMemory(key);
        }
        bool ready = true;
        using (var trial = new TrialProtocol(() => ready, _ => { calls++; return true; })) {
            var (offer, key) = Begin(trial); ready = false;
            Check(Call(trial, new { op = "submit", offer, @sealed = Seal(offer, key, [49, 50, 51, 52]) }).GetProperty("error").GetString() == "pin_page_changed", "focus change");
            CryptographicOperations.ZeroMemory(key);
        }
        using (var trial = new TrialProtocol(() => true, _ => { calls++; return true; })) {
            var (offer, key) = Begin(trial); Call(trial, new { op = "cancel" });
            Check(Call(trial, new { op = "submit", offer, @sealed = Seal(offer, key, [49, 50, 51, 52]) }).GetProperty("error").GetString() == "offer_changed", "cancel");
            CryptographicOperations.ZeroMemory(key);
        }
        Check(calls == 1, "failure paths inject nothing");
        Check(Marshal.SizeOf<Extra.Input>() == 40 && Marshal.OffsetOf<Extra.Input>(nameof(Extra.Input.Data)).ToInt32() == 8, "x64 INPUT ABI");
        Console.WriteLine("WIFI_UNLOCK_TRIAL_SELF_TEST_OK: encryption, expiry, replay, cancellation, target change, memory clearing, INPUT layout; no real input");
        return 0;
    }
}
