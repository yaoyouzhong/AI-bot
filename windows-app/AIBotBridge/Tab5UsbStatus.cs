using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5UsbStatus
{
    // Negotiate on the validated hello, including the first publication after
    // reconnect. Waiting for a status ACK first forced a large raw bootstrap
    // frame even though the device had already advertised packed support.
    internal static bool SupportsPacked(JsonElement hello,string expectedId)=>
        hello.TryGetProperty("type",out var type)&&type.ValueKind==JsonValueKind.String&&type.GetString()=="tab5_hello"&&
        hello.TryGetProperty("version",out var version)&&version.ValueKind==JsonValueKind.Number&&version.TryGetInt32(out int v)&&v==1&&
        hello.TryGetProperty("deviceId",out var id)&&id.ValueKind==JsonValueKind.String&&id.GetString()==expectedId&&
        hello.TryGetProperty("usbPacked",out var packed)&&packed.ValueKind==JsonValueKind.Number&&packed.TryGetInt32(out int p)&&p==1;
    // Keep the caller's serial gate and the exact publication/sequence. A
    // rejected compressed representation gets one raw retry, never a new
    // authenticated session or a retry of identity/sequence rejection.
    internal static async Task<(JsonDocument Ack,bool Retried)> SendAsync(byte[] full,byte[] wire,
        Action<byte[]> write,Func<Task<JsonDocument>> read,Action<string> stage)
    {
        stage("status_write");write(wire);stage("status_ack");
        try {return(await read(),false);}
        catch(Tab5UsbRejected ex) when(ex.Expected=="tab5_ack"&&(ex.Reason is 0 or 1 or 2)&&!ReferenceEquals(wire,full)) {
            using var envelope=JsonDocument.Parse(wire);
            if(envelope.RootElement.GetProperty("type").GetString() is not ("tab5_packed" or "tab5_delta"))throw;
            stage("status_recovery_write");write(full);stage("status_recovery_ack");
            return(await read(),true);
        }
    }
}
