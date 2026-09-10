using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace AIBotBridge;

internal sealed record LanPairing(IPAddress Address, int Port, string Token);

internal static class LanPairingFactory
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AI-bot LAN pairing v1");

    internal static LanPairing? Create(int port)
    {
        var address = FindPrivateAddress();
        return address is null ? null : new LanPairing(address, port, LoadOrCreateToken());
    }

    private static IPAddress? FindPrivateAddress()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up &&
                adapter.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
            .Where(adapter => adapter.GetIPProperties().GatewayAddresses.Any(gateway =>
                gateway.Address.AddressFamily == AddressFamily.InterNetwork &&
                !IPAddress.Any.Equals(gateway.Address)))
            .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
            .Select(address => address.Address)
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork && IsPrivate(address))
            .OrderBy(address => address.ToString(), StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool IsPrivate(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
            bytes[0] == 192 && bytes[1] == 168 ||
            bytes[0] == 172 && bytes[1] is >= 16 and <= 31;
    }

    private static string LoadOrCreateToken()
    {
        var directory = Path.Combine(
            AIBotBridge.AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AI-bot");
        var path = Path.Combine(directory, "pairing.dat");
        try
        {
            if (File.Exists(path))
            {
                var protectedBytes = File.ReadAllBytes(path);
                var clearBytes = ProtectedData.Unprotect(
                    protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                var existing = Encoding.UTF8.GetString(clearBytes);
                if (existing.Length >= 32)
                    return existing;
            }
        }
        catch (Exception ex) when (ex is CryptographicException or IOException)
        {
            // A corrupt, locked, or foreign-user token is replaced below.
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Directory.CreateDirectory(directory);
        var protectedToken = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(token), Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, protectedToken);
        return token;
    }
}
