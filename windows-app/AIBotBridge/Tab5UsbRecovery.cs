namespace AIBotBridge;

internal static class Tab5UsbRecovery
{
    // Check ownership after acquiring the gate. A handler can finish after a
    // reconnect; its old reply must neither use nor close the new connection.
    internal static async Task<T> InSessionAsync<T>(SemaphoreSlim gate,long generation,Func<long> current,
        Func<Task<T>> transfer,Action close,Action<long> waited,CancellationToken token)
    {
        long started=Environment.TickCount64;
        await gate.WaitAsync(token);
        try {
            waited(Environment.TickCount64-started);
            if(generation!=current())throw new IOException("USB RPC connection replaced");
            try {return await transfer();}
            catch {close();throw;} // Text and binary partial replies both poison the stream.
        } finally {gate.Release();}
    }
    // SerialStream can cancel an overlapped read when USB disappears, without
    // cancellation of the bridge lifetime. That is a disconnect, not shutdown.
    internal static bool IsDisconnect(Exception error,CancellationToken lifetime)=>
        error is IOException or InvalidOperationException or UnauthorizedAccessException or TimeoutException or System.ComponentModel.Win32Exception or System.Text.Json.JsonException or KeyNotFoundException ||
        error is OperationCanceledException&&!lifetime.IsCancellationRequested;
    // Firmware USB descriptors or Windows enumeration may change the instance ID.
    // A different COM port is never sufficient evidence to rebind pairing.
    internal static async Task<FlashUsbDevice?> FindAsync(Tab5Pairing pairing,IReadOnlyList<FlashUsbDevice> devices,
        Func<FlashUsbDevice,CancellationToken,Task<string>> identify,CancellationToken token)
    {
        var exact=devices.FirstOrDefault(d=>d.Identity.Equals(pairing.UsbIdentity,StringComparison.OrdinalIgnoreCase));
        if(exact is not null)return exact;
        foreach(var candidate in devices.Where(d=>d.Identity.StartsWith("USB\\VID_303A&",StringComparison.OrdinalIgnoreCase))) {
            token.ThrowIfCancellationRequested();
            try{if(await identify(candidate,token)==pairing.DeviceId)return candidate;}
            catch(Exception ex) when(IsDisconnect(ex,token)){}
        }
        return null;
    }
}
