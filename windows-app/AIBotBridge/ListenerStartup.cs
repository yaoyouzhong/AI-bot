using System.Net.Sockets;

namespace AIBotBridge;

internal static class ListenerStartup
{
    internal static async Task StartAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            try { listener.Start(); return; }
            catch (SocketException ex) when (ex.SocketErrorCode is SocketError.AccessDenied or SocketError.AddressAlreadyInUse) {
                QuotaRequestDiagnostics.RecordLocalServerFailure(ex.SocketErrorCode.ToString());
                listener.Stop();
                await Task.Delay(2000, cancellationToken);
            }
        }
    }
}
