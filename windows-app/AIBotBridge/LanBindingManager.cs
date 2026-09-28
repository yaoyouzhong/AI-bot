using System.Net;
using System.Net.Sockets;

namespace AIBotBridge;

internal static class LanBindingManager
{
    private sealed record Binding(LanPairing Pairing, CancellationTokenSource Stop, Task Run);

    internal static async Task RunAsync(int port, SerialPublisher serial,
        Func<StatusSnapshot> snapshot, Func<IReadOnlyList<ResourcePayload>> resources,
        CancellationToken cancellationToken,
        Func<string?, IPAddress?>? selectAddress = null,
        Func<IPAddress, LanPairing>? createPairing = null,
        LanDiscoveryServer? discovery = null, Tab5Service? tab5 = null,
        Func<Tab5Service?>? tab5Provider = null, Func<bool>? legacyEnabled = null, Action? legacyActivity = null)
    {
        Binding? current = null;
        tab5Provider??=()=>tab5;legacyEnabled??=()=>true;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (current is not null && current.Run.IsCompleted)
                {
                    await StopAsync(current);
                    current = null;
                    serial.SetPairing(null);
                    discovery?.SetBinding(null);
                }

                var address = (selectAddress ?? LanPairingFactory.FindPrivateAddress)(
                    serial.DeviceHost ?? discovery?.DeviceHost);
                if (address is null && current is not null)
                {
                    serial.SetPairing(null);
                    discovery?.SetBinding(null);
                    await StopAsync(current);
                    current = null;
                }
                else if (address is not null && !address.Equals(current?.Pairing.Address))
                {
                    var pairing = current is null
                        ? createPairing?.Invoke(address) ?? LanPairingFactory.CreateForAddress(port, address)
                        : current.Pairing with { Address = address };
                    if (pairing is not null)
                    {
                        var next = await TryStartAsync(pairing, snapshot, resources, cancellationToken, tab5Provider, legacyEnabled, legacyActivity);
                        if (next is not null)
                        {
                            var previous = current;
                            current = next;
                            serial.SetPairing(legacyEnabled()?pairing:null);
                            discovery?.SetBinding(pairing);
                            if (previous is not null) await StopAsync(previous);
                        }
                    }
                }

                serial.SetPairing(legacyEnabled()?current?.Pairing:null);
                tab5Provider()?.SetBinding(current?.Pairing);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            serial.SetPairing(null);
            discovery?.SetBinding(null);
            tab5Provider()?.SetBinding(null);
            if (current is not null) await StopAsync(current);
        }
    }

    private static async Task<Binding?> TryStartAsync(LanPairing pairing,
        Func<StatusSnapshot> snapshot, Func<IReadOnlyList<ResourcePayload>> resources,
        CancellationToken cancellationToken, Func<Tab5Service?> tab5Provider, Func<bool> legacyEnabled, Action? legacyActivity)
    {
        var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = new LanStatusServer(pairing, resources, tab5Provider:tab5Provider, legacyEnabled:legacyEnabled, legacyActivity:legacyActivity).RunAsync(snapshot, stop.Token,
            () => ready.TrySetResult());
        try
        {
            var first = await Task.WhenAny(ready.Task, run).WaitAsync(TimeSpan.FromSeconds(4), cancellationToken);
            if (first == run) await run;
            return new Binding(pairing, stop, run);
        }
        catch (Exception ex) when (ex is SocketException or IOException or TimeoutException)
        {
            if (ex is SocketException socket)
                QuotaRequestDiagnostics.RecordLocalServerFailure(socket.SocketErrorCode.ToString());
            return null;
        }
        finally
        {
            if (!ready.Task.IsCompletedSuccessfully)
            {
                stop.Cancel();
                try { await run; }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
                catch (SocketException) { }
                stop.Dispose();
            }
        }
    }

    private static async Task StopAsync(Binding binding)
    {
        binding.Stop.Cancel();
        try { await binding.Run; }
        catch (OperationCanceledException) when (binding.Stop.IsCancellationRequested) { }
        catch (SocketException ex)
        {
            QuotaRequestDiagnostics.RecordLocalServerFailure(ex.SocketErrorCode.ToString());
        }
        catch (IOException) { }
        finally { binding.Stop.Dispose(); }
    }
}
