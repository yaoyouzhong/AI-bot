using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace AIBotBridge;

internal static class Tab5PinSelfTest
{
    private static int _checks;
    private static void Check(bool ok,string message){_checks++;if(!ok)throw new InvalidOperationException(message);}
    private static IEnumerable<Control> Desc(Control c){foreach(Control child in c.Controls){yield return child;foreach(var nested in Desc(child))yield return nested;}}
    internal static void Run(string directory)
    {
        Directory.CreateDirectory(directory);string path=Path.Combine(directory,"synthetic-pin.dat");
        string Row(uint seq,uint flags)=>string.Concat(new uint[]{seq,flags,10000,1200,50,400,800,0x200C8,100}.Select(x=>x.ToString("X8")));
        string history="1;"+Row(1,8|3<<4)+";"+Row(2,4|1<<13);
        using(var doc=JsonDocument.Parse(JsonSerializer.Serialize(new{unlockHistory=history}))) {
            Check(Tab5UnlockDiagnostic.TryReadHistory(doc.RootElement,out string decoded)&&decoded.Contains("attempt=1; transport=USB; result=failure")&&decoded.Contains("attempt=2; transport=USB; result=success")&&decoded.Contains("lockAgeMs=50")&&decoded.Contains("controlOp=lock-screen; controlStatus=200"),"bounded history lost previous failure or timing");
        }
        foreach(string malformed in new[]{"", "1;", "2;"+Row(1,4),"1;"+Row(1,7),"1;"+Row(1,0),"1;"+Row(1,0x80000004),"1;"+Row(1,4)+";"+Row(3,4),"1;"+string.Join(';',Enumerable.Range(1,9).Select(i=>Row((uint)i,4))),"1;credential-content"})
            using(var doc=JsonDocument.Parse(JsonSerializer.Serialize(new{unlockHistory=malformed})))Check(!Tab5UnlockDiagnostic.TryReadHistory(doc.RootElement,out _),"malformed history accepted");
        using(var diagnostic=JsonDocument.Parse("{\"f\":\"00120845\",\"unlockFailureUptimeMs\":1234}")){
            string decoded=Tab5UnlockDiagnostic.Read(diagnostic.RootElement);Check(decoded.Contains("connection_not_ready")&&decoded.Contains("transport=USB")&&decoded.Contains("deviceUptimeMs=1234"),"failure diagnostic decode");}
        foreach(string malformed in new[]{"{}","{\"f\":4}","{\"f\":\"bad\"}","{\"f\":\"credential-content\"}"})using(var diagnostic=JsonDocument.Parse(malformed))Check(Tab5UnlockDiagnostic.Read(diagnostic.RootElement)=="","unbounded diagnostic text accepted");
        using(var diagnostic=JsonDocument.Parse("{\"f\":\"00000000\"}"))Check(Tab5UnlockDiagnostic.Read(diagnostic.RootElement)=="unlockLastFailure=none_since_boot","diagnostic reset");
        var pair=new Tab5Pairing("001122aabbcc",@"USB\PIN-TEST",Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        var store=new Tab5PinStore(path);store.Delete();byte[] pin="123456"u8.ToArray();
        Check(!store.Has(pair)&&!store.Enabled(pair)&&!store.IsEnabled(pair),"new store enabled");
        store.SetEnabled(pair,true);Check(store.Enabled(pair)&&!store.Has(pair)&&!store.IsEnabled(pair)&&store.Read(pair,true) is null,"switch alone grants unlock");
        store.SetEnabled(pair,false);store.Save(pair,pin);Check(store.Has(pair)&&!store.IsEnabled(pair)&&store.Read(pair,true) is null,"saving enabled switch implicitly");
        store.SetEnabled(pair,true);Check(new Tab5PinStore(path).IsEnabled(pair),"switch not persisted");
        Check(!Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains("123456",StringComparison.Ordinal),"plaintext persisted");
        Check(store.Has(pair)&&store.Read(pair)!.AsSpan().SequenceEqual(pin),"save/read failed");
        Check(!store.Has(pair with{DeviceId="aabbcc001122"})&&!store.Has(pair with{Key=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))}),"foreign/repaired device read PIN");
        store.Save(pair,"654321"u8);Check(store.Read(pair)!.AsSpan().SequenceEqual("654321"u8),"modify failed");
        bool rejected=false;try{store.Save(pair,"12a4"u8);}catch(ArgumentException){rejected=true;}Check(rejected,"invalid PIN accepted");
        var screen=new ComputerLockScreenState(Locked:true,LockScreenCover:null,DisplayState:1);
        var broker=new Tab5PinBroker(store,()=>screen);
        JsonDocument Request(string? id=null)=>JsonDocument.Parse(JsonSerializer.Serialize(new{kind="unlock-pin",op="read",requestId=id??Guid.NewGuid().ToString("D")}));
        store.SetEnabled(pair,false);
        foreach(var transport in new[]{"USB","BLE"}){using var request=Request();Check(broker.Handle(request.RootElement,pair,transport).Status==404,"disabled switch released PIN");}
        store.Save(pair,"654321"u8);Check(!store.IsEnabled(pair)&&store.Has(pair),"modify implicitly enabled");store.SetEnabled(pair,true);
        using(var request=Request()){
            var result=broker.Handle(request.RootElement,pair,"USB");Check(result.Status==200,"prepared USB denied");
            using var body=JsonDocument.Parse(JsonSerializer.Serialize(result.Body));string id=body.RootElement.GetProperty("requestId").GetString()!;
            var packet=Convert.FromBase64String(body.RootElement.GetProperty("packet").GetString()!);byte[] key=Convert.FromBase64String(pair.Key);
            var clear=Tab5Protocol.Decrypt(key,id.Replace("-",""),packet,128);Check(clear.AsSpan().SequenceEqual("654321"u8),"inner envelope incompatible");CryptographicOperations.ZeroMemory(clear);
            Check(broker.Handle(request.RootElement,pair,"BLE").Status==409,"request replay released twice");
            rejected=false;try{Tab5Protocol.Decrypt(key,Guid.NewGuid().ToString("N"),packet,128);}catch(CryptographicException){rejected=true;}Check(rejected,"wrong request AAD accepted");
            packet[^1]^=1;rejected=false;try{Tab5Protocol.Decrypt(key,id.Replace("-",""),packet,128);}catch(CryptographicException){rejected=true;}Check(rejected,"tampered envelope accepted");
        }
        using(var request=Request())Check(broker.Handle(request.RootElement,pair,"Wi-Fi").Status==400,"Wi-Fi PIN retrieval enabled");
        foreach(var invalid in new[]{new ComputerLockScreenState(Locked:false,DisplayState:1),new ComputerLockScreenState(Locked:null,DisplayState:1),new ComputerLockScreenState(Locked:true,DisplayState:0),new ComputerLockScreenState(Locked:true,DisplayState:null),new ComputerLockScreenState(Locked:true,DisplayState:1,LockScreenCover:true)}){
            screen=invalid;using var request=Request();Check(broker.Handle(request.RootElement,pair,"BLE").Status==409,"unready screen granted PIN");
        }
        screen=new(Locked:true,DisplayState:1);using(var request=Request())Check(broker.Handle(request.RootElement,pair,"BLE").Status==200,"BLE denied");
        store.Delete();Check(!File.Exists(path)&&!store.Has(pair)&&!store.Enabled(pair),"delete retained credential or switch");
        using(var request=Request())Check(broker.Handle(request.RootElement,pair,"USB").Status==404,"deleted PIN granted");
        store.Save(pair,pin);var corrupt=File.ReadAllBytes(path);corrupt[^1]^=1;File.WriteAllBytes(path,corrupt);Check(!store.Has(pair),"corrupt record enabled");store.Delete();
        Rpc(directory,store).GetAwaiter().GetResult();
        foreach(float scale in new[]{1f,1.5f,2f}){
            using var form=new Tab5PinForm(store,()=>pair,()=>true);
            // Layout/font simulation; does not change the user's Windows DPI.
            var fonts=Desc(form).Prepend(form).Select(c=>(control:c,font:c.Font)).ToArray();
            form.Scale(new SizeF(scale,scale));foreach(var item in fonts)item.control.Font=new Font(item.font.FontFamily,item.font.Size*scale,item.font.Style);
            form.Show();Application.DoEvents();
            var controls=Desc(form).ToArray();var input=(TextBox)controls.Single(c=>c.Name=="saved-pin");var repeat=(TextBox)controls.Single(c=>c.Name=="repeat-pin");
            Check(input.UseSystemPasswordChar&&repeat.UseSystemPasswordChar,"unmasked credential field");
            var toggle=(CheckBox)controls.Single(c=>c.Name=="enable-oneclick");Check(!toggle.Checked,"UI default enabled");
            toggle.Checked=true;Check(store.Enabled(pair)&&!store.IsEnabled(pair),"UI switch without PIN active");
            var eye=(Button)controls.Single(c=>c.Name=="saved-pin-eye");var repeatEye=(Button)controls.Single(c=>c.Name=="repeat-pin-eye");
            void Event(Control c,string method,object argument)=>c.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(c,[argument]);
            var down=new MouseEventArgs(MouseButtons.Left,1,12,12,0);
            input.Text=repeat.Text="123456";input.Select(2,1);
            Event(eye,"OnMouseDown",down);Check(!input.UseSystemPasswordChar&&repeat.UseSystemPasswordChar&&input.SelectionStart==2,"press did not independently reveal");
            Event(eye,"OnMouseUp",down);Check(input.UseSystemPasswordChar,"release did not mask");eye.PerformClick();Check(input.UseSystemPasswordChar,"click latched plaintext");
            Event(eye,"OnMouseDown",down);Event(eye,"OnMouseLeave",EventArgs.Empty);Check(input.UseSystemPasswordChar,"drag-out retained plaintext");
            Event(eye,"OnMouseDown",down);eye.Capture=false;Check(input.UseSystemPasswordChar,"capture loss retained plaintext");
            Event(repeatEye,"OnKeyDown",new KeyEventArgs(Keys.Space));Check(!repeat.UseSystemPasswordChar&&input.UseSystemPasswordChar,"keyboard hold did not reveal independently");
            Event(repeatEye,"OnKeyUp",new KeyEventArgs(Keys.Space));Check(repeat.UseSystemPasswordChar,"keyboard release did not mask");
            Event(eye,"OnMouseDown",down);Event(form,"OnDeactivate",EventArgs.Empty);Check(input.UseSystemPasswordChar&&repeat.UseSystemPasswordChar,"deactivation retained plaintext");
            input.Clear();repeat.Clear();
            input.Text="123456";repeat.Text="654321";((Button)controls.Single(c=>c.Name=="save-pin")).PerformClick();Check(!store.Has(pair)&&input.Text.Length==0&&repeat.Text.Length==0,"mismatch saved or kept plaintext UI");
            input.Text=repeat.Text="123456";Event(eye,"OnMouseDown",down);((Button)controls.Single(c=>c.Name=="save-pin")).PerformClick();Check(store.Has(pair)&&input.Text.Length==0&&repeat.Text.Length==0&&input.UseSystemPasswordChar,"UI save failed");Check(store.IsEnabled(pair),"UI switch plus PIN not enabled");
            toggle.Checked=false;Check(store.Has(pair)&&!store.IsEnabled(pair),"UI off deleted PIN or remained enabled");toggle.Checked=true;Check(store.IsEnabled(pair),"UI reenable failed");
            input.Text=repeat.Text="654321";((Button)controls.Single(c=>c.Name=="save-pin")).PerformClick();Check(store.Read(pair)!.AsSpan().SequenceEqual("654321"u8),"UI modify failed");
            foreach(var name in new[]{"saved-pin","repeat-pin","saved-pin-eye","repeat-pin-eye","save-pin","delete-pin","enable-oneclick"}){var c=controls.Single(c=>c.Name==name);var bounds=form.RectangleToClient(c.RectangleToScreen(c.ClientRectangle));Check(form.ClientRectangle.Contains(bounds)&&bounds.Height>=20,"clipped PIN control");}
            var footer=controls.Where(c=>c.Name is "save-pin" or "delete-pin").ToArray();Check(footer[0].Top==footer[1].Top&&footer[0].Height==footer[1].Height,"PIN footer alignment");
            using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(directory,$"pin-settings-{scale:0.0}.png"));
            ((Button)controls.Single(c=>c.Name=="delete-pin")).PerformClick();Check(!store.Has(pair)&&!toggle.Checked&&!store.IsEnabled(pair),"UI delete failed");
            input.Text="123456";Event(eye,"OnMouseDown",down);Event(form,"OnKeyDown",new KeyEventArgs(Keys.Escape));Check(form.IsDisposed&&input.Text.Length==0&&input.UseSystemPasswordChar,"Esc close retained input");
        }
        store.Delete();CryptographicOperations.ZeroMemory(pin);
        Console.WriteLine($"TAB5_PIN_SELF_TEST_OK checks={_checks}; synthetic credentials only; no device input");
    }
    private static async Task Rpc(string directory,Tab5PinStore store)
    {
        var pairing=new Tab5PairingStore(Path.Combine(directory,"pair.dat"));var pair=pairing.Pair("001122aabbcc",@"USB\PIN-RPC-TEST");byte[] key=Convert.FromBase64String(pair.Key);store.Save(pair,"123456"u8);store.SetEnabled(pair,true);
        using var tasks=new Tab5CodexTasks(new Tab5CodexDesktop("unused-pin-test"),()=>[],new Tab5CodexJournal(Path.Combine(directory,"draft.dat")),new Tab5CodexImages(Path.Combine(directory,"images")));
        using var service=new Tab5Service(pairing,tasks,new Tab5Gallery(Path.Combine(directory,"gallery")));
        typeof(Tab5Service).GetField("_pinBroker",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(service,new Tab5PinBroker(store,()=>new(Locked:true,DisplayState:1)));
        service.Publish(new StatusSnapshot(1,"12:00",1000,28800,DateTimeOffset.UtcNow,new("idle",0),new("idle",0)));
        using var frame=JsonDocument.Parse(service.CurrentFrame!);string session=frame.RootElement.GetProperty("session").GetString()!;
        async Task<int> Call(bool proofOk=true,bool stale=false,bool oldSession=false,string transport="USB",string? requestId=null,bool binary=true){
            string nonce=Guid.NewGuid().ToString("N");byte[] json=JsonSerializer.SerializeToUtf8Bytes(new{kind="unlock-pin",op="read",requestId=requestId??Guid.NewGuid().ToString("D"),session=oldSession?"old":session,issuedAt=DateTimeOffset.UtcNow.AddSeconds(stale?-121:0).ToUnixTimeMilliseconds(),binaryReply=binary});
            byte[] packet=Tab5Protocol.Encrypt(key,nonce,binary?Tab5RpcBinary.Encode(json):json);
            string proof=proofOk?Tab5Protocol.Proof(key,$"POST|/tab5/v1/rpc|{pair.DeviceId}|{nonce}|{Convert.ToHexString(SHA256.HashData(packet)).ToLowerInvariant()}"):new string('0',64);
            var reply=await service.RpcAsync(pair.DeviceId,nonce,proof,packet,CancellationToken.None,transport:transport);
            if(reply.Status!=200)return reply.Status;using var body=Tab5RpcBinary.Open(Tab5Protocol.Decrypt(key,nonce,reply.Packet!)).Metadata;
            int status=body.RootElement.GetProperty("status").GetInt32();
            if(status==200){var envelope=body.RootElement.GetProperty("body");string aad=envelope.GetProperty("requestId").GetString()!.Replace("-","");var clearPin=Tab5Protocol.Decrypt(key,aad,Convert.FromBase64String(envelope.GetProperty("packet").GetString()!),128);Check(clearPin.AsSpan().SequenceEqual("123456"u8),"RPC envelope PIN mismatch");CryptographicOperations.ZeroMemory(clearPin);}
            return status;
        }
        Check(await Call(proofOk:false)==401&&await Call(stale:true)==400&&await Call(oldSession:true)==400,"RPC authentication bypass");
        Check(await Call(transport:"Wi-Fi")==400,"Wi-Fi route enabled");
        string id=Guid.NewGuid().ToString("D");Check(await Call(requestId:id)==200&&await Call(transport:"BLE",requestId:id)==409,"cross-transport replay");
        Check(await Call(transport:"BLE")==200&&await Call(binary:false)==200,"authenticated BLE or JSON failed");store.SetEnabled(pair,false);Check(await Call()==404&&await Call(transport:"BLE")==404&&store.Has(pair),"RPC ignored disabled switch");
        store.SetEnabled(pair,true);Check(await Call()==200,"RPC reenable failed");store.Delete();Check(await Call()==404,"RPC deleted credential returned");
    }
}
