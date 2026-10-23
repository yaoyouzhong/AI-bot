using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace AIBotBridge;

// Independently implemented RFC 6143 subset: authentication and KeyEvent only.
// No framebuffer request, clipboard, file transfer, mouse, or remote host option.
internal sealed class WifiKeyboardRfb(Stream stream, IDisposable? owner = null) : IDisposable
{
    internal static Task<WifiKeyboardRfb> ConnectAsync(CancellationToken token)=>ConnectAsync(token,true);
    internal static async Task<WifiKeyboardRfb> ConnectAsync(CancellationToken token,bool initialize)
    {
        var settings = WifiKeyboardSettings.Load();
        var tcp = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
        try {
            await tcp.ConnectAsync(IPAddress.Loopback, settings.Port, token);
            if (!WifiKeyboardSettings.TrustedPeer(tcp)) throw new IOException("keyboard_peer_untrusted");
            var client = new WifiKeyboardRfb(tcp.GetStream(), tcp);
            await client.HandshakeAsync(settings.Password, token,initialize:initialize);
            return client;
        } catch { tcp.Dispose(); throw; }
        finally { CryptographicOperations.ZeroMemory(settings.Password); }
    }

    internal async Task HandshakeAsync(byte[] password, CancellationToken token,Action<string>? progress=null,bool initialize=true)
    {
        byte[] hello = new byte[12], challenge = new byte[16], key = new byte[8], response = [];
        try {
            progress?.Invoke("greeting");await stream.ReadExactlyAsync(hello, token);
            // TightVNC supports 3.8; fail closed on undocumented negotiations.
            if (!hello.AsSpan().SequenceEqual("RFB 003.008\n"u8)) throw new IOException("keyboard_protocol_unsupported");
            await stream.WriteAsync(hello, token);
            progress?.Invoke("security-types");byte[] count = new byte[1]; await stream.ReadExactlyAsync(count, token);
            if (count[0] is 0 or > 32) throw new IOException("keyboard_security_invalid");
            byte[] types = new byte[count[0]]; await stream.ReadExactlyAsync(types, token);
            if (!types.Contains((byte)2)) throw new IOException("keyboard_auth_required");
            await stream.WriteAsync(new byte[] { 2 }, token); // Never choose security type None.
            progress?.Invoke("challenge");await stream.ReadExactlyAsync(challenge, token);
            if (password.Length != 8) throw new IOException("keyboard_access_key_invalid");
            for (int i = 0; i < 8; i++) {
                byte b = password[i], reversed = 0;
                for (int bit = 0; bit < 8; bit++) { reversed = (byte)((reversed << 1) | (b & 1)); b >>= 1; }
                key[i] = reversed;
            }
            // Legacy DES is required by RFB VNCAuth, restricted to authenticated
            // loopback to the installed vendor service, never exposed over Wi-Fi.
            using var des = DES.Create(); des.Key = key;
            response = des.EncryptEcb(challenge, PaddingMode.None);
            await stream.WriteAsync(response, token);
            progress?.Invoke("authentication-result");byte[] result = new byte[4]; await stream.ReadExactlyAsync(result, token);
            if (BinaryPrimitives.ReadUInt32BigEndian(result) != 0) throw new IOException("keyboard_auth_failed");
            // Setup/check needs proof of authentication only. Starting a desktop
            // is reserved for an actual input session, never a settings check.
            if(!initialize){progress?.Invoke("authenticated");return;}
            await stream.WriteAsync(new byte[] { 1 }, token); // Shared; do not evict another viewer.
            progress?.Invoke("server-init");byte[] init = new byte[24]; await stream.ReadExactlyAsync(init, token);
            uint length = BinaryPrimitives.ReadUInt32BigEndian(init.AsSpan(20));
            if (length > 4096) throw new IOException("keyboard_server_init_invalid");
            byte[] name = new byte[length]; await stream.ReadExactlyAsync(name, token);
            CryptographicOperations.ZeroMemory(name);
            progress?.Invoke("complete");
        } finally {
            CryptographicOperations.ZeroMemory(challenge); CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(response);
        }
    }

    internal async Task PressAsync(uint key, CancellationToken token)
    {
        if (key is not (>= 0x20 and <= 0x7e) and not 0xff0d) throw new IOException("keyboard_key_rejected");
        await KeyAsync(key,token);
    }
    // Only the bounded preparation state machine can request these two keys.
    internal Task WakeAsync(bool cover,CancellationToken token)=>KeyAsync(cover?0x20u:0xffe1u,token);
    private async Task KeyAsync(uint key,CancellationToken token)
    {
        byte[] message = new byte[8]; message[0] = 4;
        BinaryPrimitives.WriteUInt32BigEndian(message.AsSpan(4), key);
        bool down=false;
        try {
            message[1] = 1; await stream.WriteAsync(message, token);
            down=true;
            // No retries. On uncertainty the caller closes this RFB session.
            await Task.Delay(40, token);
            message[1] = 0; await stream.WriteAsync(message, token);
        } catch {
            // Release only; never repeat a key-down after uncertain delivery.
            if(down)try{using var release=new CancellationTokenSource(100);message[1]=0;await stream.WriteAsync(message,release.Token);}catch(Exception ex) when(ex is IOException or OperationCanceledException or ObjectDisposedException){}
            throw;
        } finally { CryptographicOperations.ZeroMemory(message); }
    }

    public void Dispose() { stream.Dispose(); owner?.Dispose(); }
}
