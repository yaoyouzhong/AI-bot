using System.Globalization;
using System.Text.Json;
namespace AIBotBridge;

// Strictly bounded device diagnostics. Never used by credential or HID decisions.
internal static class Tab5UnlockDiagnostic
{
    // v1; up to eight rows of nine uint32 words. No free-form device text.
    internal static bool TryReadHistory(JsonElement root,out string result)
    {
        result="";
        if(!root.TryGetProperty("unlockHistory",out var field)||field.ValueKind!=JsonValueKind.String)return false;
        string? raw=field.GetString();if(raw is null||raw.Length>585||!raw.StartsWith("1;",StringComparison.Ordinal))return false;
        string[] rows=raw[2..].Split(';');if(rows.Length is <1 or >8)return false;
        List<string> lines=[];uint previous=0;
        foreach(string row in rows) {
            if(row.Length!=72||!row.All(Uri.IsHexDigit))return false;
            uint[] w=new uint[9];for(int i=0;i<9;i++)if(!uint.TryParse(row.AsSpan(i*8,8),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out w[i]))return false;
            uint flags=w[1],transport=flags&3,outcome=(flags>>2)&3,reason=(flags>>4)&31;
            if(w[0]==0||(previous!=0&&w[0]!=previous+1)||transport>2||outcome==0||(flags&0xFE000000)!=0||(w[7]>>16)>2||(w[7]&65535)>599)return false;
            previous=w[0];
            string Milliseconds(uint v)=>v==uint.MaxValue?"unknown":v.ToString(CultureInfo.InvariantCulture);
            string[] missingNames=["connected","secure","subscribed","bridgeFresh","hidReady","sameConnection","lockFresh","locked","keyboardIdle"];
            string missing=string.Join(',',missingNames.Where((_,i)=>(flags&(1u<<(16+i)))!=0));
            string cause=reason switch {0=>"none",1=>"preparation_timeout",2=>"wake_interrupted",3=>"peer_or_lock_state_changed",4=>"display_not_confirmed",5=>"connection_not_ready",6=>"credential_request_not_started",7=>"credential_reply_failed",8=>"input_interrupted",9=>"unlock_not_confirmed",10=>"panel_expired",12=>"pin_not_configured",13=>"pin_store_unavailable",14=>"screen_not_ready",15=>"credential_request_consumed",16=>"credential_transport_failed",17=>"credential_envelope_invalid",18=>"credential_timeout",19=>"wifi_unlock_disabled",20=>"wifi_request_failed",_=>"unknown"};
            lines.Add($"attempt={w[0]}; transport={new[]{"USB","BLE","Wi-Fi"}[transport]}; result={new[]{"","success","failure","cancelled"}[outcome]}; reason={cause}; phase={(flags>>9)&7}; automatic={(flags&(1u<<12))!=0}; hidSubmitted={(flags&(1u<<13))!=0}; deviceUptimeMs={w[2]}; elapsedMs={w[3]}; lockAgeMs={Milliseconds(w[4])}; prepareMs={Milliseconds(w[5])}; inputConfirmMs={Milliseconds(w[6])}; missing={missing}; controlOp={new[]{"none","status","lock-screen"}[w[7]>>16]}; controlStatus={w[7]&65535}; controlAgeMs={Milliseconds(w[8])}");
        }
        result=string.Join('\n',lines);return true;
    }
    internal static string Read(JsonElement root)
    {
        if(!root.TryGetProperty("f",out var field)||field.ValueKind!=JsonValueKind.String)return "";
        string? raw=field.GetString();
        if(raw is null||raw.Length!=8||!raw.All(Uri.IsHexDigit)||!uint.TryParse(raw,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out uint value))return "";
        if(value==0)return "unlockLastFailure=none_since_boot";
        string reason=(value&31) switch {
            1=>"preparation_timeout",2=>"wake_interrupted",3=>"peer_or_lock_state_changed",4=>"display_not_confirmed",
            5=>"connection_not_ready",6=>"credential_request_not_started",7=>"credential_reply_failed",
            8=>"input_interrupted",9=>"unlock_not_confirmed",10=>"panel_expired",12=>"pin_not_configured",
            13=>"pin_store_unavailable",14=>"screen_not_ready",15=>"credential_request_consumed",
            16=>"credential_transport_failed",17=>"credential_envelope_invalid",18=>"credential_timeout",_=>"unknown"};
        string[] names=["connected","secure","subscribed","bridgeFresh","hidReady","sameConnection","lockFresh","locked","keyboardIdle"];
        uint missing=(value>>5)&511;string[] absent=names.Where((_,i)=>(missing&(1u<<i))!=0).ToArray();
        string result=$"unlockLastFailure={value:X8}; reason={reason}; missing={string.Join(',',absent)}; transport={((value&(1u<<17))!=0?"USB":"BLE")}; phase={(value>>14)&7}; attemptMod4096={value>>20}; automatic={(value&(1u<<18))!=0}; submitted={(value&(1u<<19))!=0}";
        if(root.TryGetProperty("unlockFailureUptimeMs",out var uptime)&&uptime.ValueKind==JsonValueKind.Number&&uptime.TryGetUInt32(out uint ms))result+=$"; deviceUptimeMs={ms}";
        return result;
    }
}
