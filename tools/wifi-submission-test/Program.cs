using AIBotBridge;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Ordinary-user protocol checks with public synthetic bytes only.
long now=100;var server=new Tab5WifiSubmissionTest(()=>now);
JsonElement Json(object v)=>JsonSerializer.SerializeToElement(v);
void Check(bool ok,string name){if(!ok)throw new Exception(name);}
string Start(){var s=Json(server.Start());return s.GetProperty("run").GetString()!;}
JsonElement Call(object v,string transport="Wi-Fi")=>Json(server.Handle(Json(v),transport).Body);
string Seal(JsonElement offer,bool tamper=false,string fixture="4826") {
    byte[] key=Convert.FromBase64String(offer.GetProperty("key").GetString()!),packet=new byte[32],clear=Encoding.ASCII.GetBytes(fixture);RandomNumberGenerator.Fill(packet.AsSpan(0,12));
    using var aes=new AesGcm(key,16);aes.Encrypt(packet.AsSpan(0,12),clear,packet.AsSpan(28),packet.AsSpan(12,16),Encoding.ASCII.GetBytes(offer.GetProperty("offer").GetString()!));
    if(tamper)packet[^1]^=1;CryptographicOperations.ZeroMemory(key);CryptographicOperations.ZeroMemory(clear);return Convert.ToBase64String(packet);
}
string run=Start();
foreach(string transport in new[]{"USB","BLE"})Check(server.Handle(Json(new{op="begin",run,@case="accept"}),transport).Status==409,"transport rejected");
Check(Call(new{op="begin",run,@case="accept",pin="forbidden"}).GetProperty("error").GetString()=="unexpected_field","clear field rejected");
foreach(string name in new[]{"accept","tamper","cancel","expiry"}) {
    var offer=Call(new{op="begin",run,@case=name});string packet=Seal(offer,name=="tamper");string id=offer.GetProperty("offer").GetString()!;
    if(name=="cancel")Call(new{op="cancel",run,@case=name,offer=id});if(name=="expiry")now+=15000;
    var request=new{op="submit",run,@case=name,offer=id,@sealed=packet};var result=Call(request);
    string? expected=name switch {"tamper"=>"packet_rejected","cancel"=>"cancelled","expiry"=>"offer_expired",_=>null};
    if(expected is null){Check(result.GetProperty("testReceived").GetBoolean()&&!result.GetProperty("unlocked").GetBoolean(),"synthetic only");Check(Call(request).GetProperty("error").GetString()=="attempt_finished","replay");}
    else Check(result.GetProperty("error").GetString()==expected,name);
}
Check(Call(new{op="finish",run,passed=true}).GetProperty("ok").GetBoolean(),"five cases passed");
Check(server.Command is null,"completed command cleared");
run=Start();var wrong=Call(new{op="begin",run,@case="accept"});Check(Call(new{op="submit",run,@case="accept",offer=wrong.GetProperty("offer").GetString(),@sealed=Seal(wrong,fixture:"0000")}).GetProperty("error").GetString()=="fixture_mismatch","only public fixture accepted");
now+=120000;Check(server.Command is null&&Json(server.Snapshot).GetProperty("state").GetString()=="timeout","run expires");
Console.WriteLine("WIFI_SUBMISSION_PROTOCOL_OK: ordinary user; synthetic fixture only; replay/tamper/cancel/expiry; USB/BLE rejected; no login or keyboard API");
