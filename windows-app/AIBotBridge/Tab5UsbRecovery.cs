namespace AIBotBridge;

internal static class Tab5UsbRecovery
{
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
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException or System.ComponentModel.Win32Exception or System.Text.Json.JsonException){}
        }
        return null;
    }
}
