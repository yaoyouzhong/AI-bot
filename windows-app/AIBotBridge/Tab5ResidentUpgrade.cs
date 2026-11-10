using System.Diagnostics;
using System.Text.Json;

namespace AIBotBridge;

// The updater is only a client of the already-running product. The resident
// bridge owns its original LAN listener, pairing, OTA and display RPCs.
internal static class Tab5ResidentUpgrade
{
    private static string Text(JsonElement row,string name)=>row.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";
    private static bool Yes(JsonElement row,string name)=>row.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.True;
    private static JsonElement Body(JsonElement reply)=>Yes(reply,"ok")?reply.GetProperty("body"):throw new IOException("tab5_resident_command_rejected");

    private static async Task<JsonElement> WaitInfo(Func<JsonElement,bool> ready,int seconds){
        var timer=Stopwatch.StartNew();
        while(timer.Elapsed<TimeSpan.FromSeconds(seconds)){
            JsonElement info;
            try{info=Body(await Tab5OtaControl.CallAsync("info"));}
            catch(Exception ex) when(ex is IOException or TimeoutException or OperationCanceledException){await Task.Delay(800);continue;}
            if(ready(info))return info;
            await Task.Delay(800);
        }
        throw new IOException("tab5_resident_update_wait_timed_out");
    }

    internal static async Task RunAsync(string path,string sha){
        var image=Tab5OtaPackage.Load(path);
        if(image.Sha256!=sha)throw new IOException("tab5_upgrade_image_mismatch");
        var initial=await WaitInfo(info=>Yes(info,"online")&&!Yes(info,"busy")&&Tab5Protocol.ValidId(Text(info,"deviceId")),90);
        string device=Text(initial,"deviceId");
        var before=Body(await Tab5OtaControl.CallAsync("display-settings",device));
        _=Body(await Tab5OtaControl.CallAsync("offer",device,sha,imagePath:path));
        bool Bound(JsonElement info)=>Text(info,"deviceId")==device&&Text(info,"sha256")==sha;
        string previousPartition="";
        if(Text(initial,"installedVersion")==image.Version){
            // If this is an identical-image reinstall, don't mistake the old
            // valid partition for a successful update after a late ACK.
            var verify=await Tab5OtaControl.CallAsync("verify",device,sha);
            if(Yes(verify,"ok"))previousPartition=Text(Body(await Tab5OtaControl.CallAsync("info")),"verifiedPartition");
        }
        _=await WaitInfo(info=>Bound(info)&&Yes(info,"canStart"),60);
        Tab5LocalDiagnostics.Write("TAB5_RESIDENT_UPGRADE stage=start_requested");
        try{_ = await Tab5OtaControl.CallAsync("start",device,sha);}
        catch(Exception ex) when(ex is IOException or TimeoutException or OperationCanceledException){
            Tab5LocalDiagnostics.Write("TAB5_RESIDENT_UPGRADE stage=start_unconfirmed");
        }
        // Never replay the start. Even after a lost reply, the resident can
        // complete the committed transfer; inspect it read-only.
        _=await WaitInfo(info=>Bound(info)&&Yes(info,"finalReleased"),150);
        var deadline=Stopwatch.StartNew();string partition="",transfer="";
        while(deadline.Elapsed<TimeSpan.FromSeconds(210)){
            var info=await WaitInfo(candidate=>Bound(candidate)&&Yes(candidate,"canVerify"),40);
            transfer=Text(info,"transfer");
            var verify=await Tab5OtaControl.CallAsync("verify",device,sha);
            if(Yes(verify,"ok")){
                info=Body(await Tab5OtaControl.CallAsync("info"));partition=Text(info,"verifiedPartition");
                if(Bound(info)&&partition is "ota_0" or "ota_1"&&partition!=previousPartition)break;
                partition="";
            }
            await Task.Delay(3000);
        }
        if(partition.Length==0)throw new IOException("tab5_resident_boot_not_verified");
        _=await WaitInfo(info=>Bound(info)&&Yes(info,"online")&&!Yes(info,"busy"),60);
        var after=Body(await Tab5OtaControl.CallAsync("display-settings",device));
        if(before.GetRawText()!=after.GetRawText())throw new IOException("tab5_resident_display_settings_changed");
        Tab5LocalDiagnostics.Write($"TAB5_RESIDENT_UPGRADE stage=verified partition={partition} prior={(previousPartition is "ota_0" or "ota_1"?previousPartition:"unknown")} settingsPreserved=True wifi={transfer.StartsWith("Wi-Fi",StringComparison.Ordinal)}");
    }
}
