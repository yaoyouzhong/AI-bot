using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5StatusCapacity
{
    // 0.2.198-ui is the first receiver with 64 KiB status/decompression/tree
    // bounds. Reuse its existing signed firmware identity: BLE Info is capped
    // at 512 bytes and cannot safely grow another capability/proof pair.
    internal static int ForFirmware(string? firmware)=>firmware is not null&&firmware.EndsWith("-ui",StringComparison.Ordinal)&&
        Version.TryParse(firmware[..^3],out var version)&&version>=new Version(0,2,198)
        ?Tab5Protocol.MaximumStatusFrame:Tab5Protocol.MaximumFrame;
    internal static int Budget(int maximum)=>maximum>=Tab5Protocol.MaximumStatusFrame
        ?Tab5Protocol.StatusTargetFrame-28:Tab5Protocol.MaximumFrame-28-512;
    internal static int FromUsbHello(JsonElement hello,string id)=>
        hello.TryGetProperty("version",out var v)&&v.ValueKind==JsonValueKind.Number&&v.TryGetInt32(out int version)&&version==1&&
        hello.TryGetProperty("type",out var t)&&t.ValueKind==JsonValueKind.String&&t.GetString()=="tab5_hello"&&
        hello.TryGetProperty("deviceId",out var d)&&d.ValueKind==JsonValueKind.String&&d.GetString()==id&&
        hello.TryGetProperty("firmware",out var f)&&f.ValueKind==JsonValueKind.String
            ?ForFirmware(f.GetString()):Tab5Protocol.MaximumFrame;
    internal static int FromSignedFirmware(JsonElement info,Tab5Pairing pair,string nonce)=>
        info.TryGetProperty("firmware",out var f)&&f.ValueKind==JsonValueKind.String&&
        info.TryGetProperty("firmwareProof",out var p)&&p.ValueKind==JsonValueKind.String&&
        Tab5Protocol.Verify(Convert.FromBase64String(pair.Key),"FIRMWARE|"+nonce+"|"+f.GetString(),p.GetString())
            ?ForFirmware(f.GetString()):Tab5Protocol.MaximumFrame;
}
