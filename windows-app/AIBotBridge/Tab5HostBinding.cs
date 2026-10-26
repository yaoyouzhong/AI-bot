using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO.Ports;

namespace AIBotBridge;

// Host identity derives from the existing per-computer pairing key. Adding
// host ownership never rotates that key or invalidates the saved credential.
internal static class Tab5HostBinding
{
    // .236 renews the USB owner lease on a full status, while accepted deltas
    // retain their separate task freshness. Keep full ACKs inside its 8s lease.
    internal static bool NeedsFullStatus(bool binding,long now,long lastFullAck)=>
        binding&&(lastFullAck<=0||now<lastFullAck||now-lastFullAck>=4000);
    internal static string Id(Tab5Pairing pair)=>Tab5Protocol.Proof(Convert.FromBase64String(pair.Key),"AIBOT_TAB5_HOST_V1")[..32];
    internal static string Name(string value) {
        var text=new StringBuilder();int bytes=0;
        foreach(var rune in value.EnumerateRunes()) {
            var r=Rune.IsControl(rune)||rune.Value=='|'?new Rune(' '):rune;
            if(bytes+r.Utf8SequenceLength>63)break;
            text.Append(r);bytes+=r.Utf8SequenceLength;
        }
        return text.Length>0?text.ToString():"Windows";
    }
    internal static object Claim(Tab5Pairing pair,string nonce,string address,int port,string name) {
        if(!Tab5Protocol.ValidNonce(nonce)||port is <0 or >65535||Encoding.UTF8.GetByteCount(name) is <1 or >63||name.Any(c=>char.IsControl(c)||c=='|'))throw new IOException("电脑身份握手无效。");
        string id=Id(pair),message=$"TAB5_HOST_V1|{pair.DeviceId}|{id}|{nonce}|{address}|{port}|{name}";
        return new{version=1,type="tab5_host_claim",deviceId=pair.DeviceId,hostId=id,nonce,host=address,port,name,proof=Tab5Protocol.Proof(Convert.FromBase64String(pair.Key),message)};
    }
    internal static void Confirm(Tab5Pairing pair,string nonce,JsonElement reply) {
        string Text(string key)=>reply.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";
        if(Text("error")=="device_busy")throw new IOException("设备正在升级，请完成后再连接这台电脑。");
        if(Text("deviceId")!=pair.DeviceId||Text("hostId")!=Id(pair)||!Tab5Protocol.Verify(Convert.FromBase64String(pair.Key),$"TAB5_HOST_ACK_V1|{pair.DeviceId}|{Id(pair)}|{nonce}",Text("proof")))
            throw new IOException("此电脑尚未通过设备认证，请在设备中心通过 USB 配对一次；已有可信电脑会保留。");
    }
}

internal sealed partial class Tab5Service
{
    private bool _usbHostBinding,_usbHostClaimed;
    private string _usbHostNonce="",_usbHostEndpoint="";
    private volatile bool _usbAuthenticationRejected;
    private long _usbHostFullStatusAt;
    private void ReadHostBinding(JsonElement hello) {
        _usbHostBinding=hello.TryGetProperty("hostBindingVersion",out var v)&&v.TryGetInt32(out int version)&&version==1;
        _usbHostNonce=hello.TryGetProperty("hostNonce",out var n)&&n.ValueKind==JsonValueKind.String?n.GetString()??"":"";
        if(_usbHostBinding&&hello.TryGetProperty("usbHostAuthenticated",out var authenticated)&&authenticated.ValueKind==JsonValueKind.False)_usbHostClaimed=false;
    }
    private async Task ClaimUsbHostAsync(SerialPort port,Tab5Pairing pair,CancellationToken token) {
        if(!_usbHostBinding)return;
        string address=_host??"",endpoint=address+":"+_port;
        if(_usbHostClaimed&&_usbHostEndpoint==endpoint)return;
        if(_usbHostClaimed)await ReadIdentityAsync(port,token,ReadHostBinding);
        string nonce=_usbHostNonce;
        Send(port,Tab5HostBinding.Claim(pair,nonce,address,_port,Tab5HostBinding.Name(Environment.MachineName)));
        using var reply=await ReadReplyAsync(port,"tab5_host_claimed",token);
        try{Tab5HostBinding.Confirm(pair,nonce,reply.RootElement);}
        catch(IOException){if(!reply.RootElement.TryGetProperty("error",out var error)||error.ValueKind!=JsonValueKind.String||error.GetString()!="device_busy")_usbAuthenticationRejected=true;throw;}
        _usbHostClaimed=true;_usbHostEndpoint=endpoint;_usbAuthenticationRejected=false;
    }
    private string HostBindingSummary=>_usbAuthenticationRejected?"当前电脑未通过配对认证，请通过 USB 验证配对。":
        _usbHostClaimed?"当前电脑："+Environment.MachineName+"（USB 已认证）":"电脑归属：由设备选择；其他电脑无线待命。";
}
