using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIBot.LoginMethod;
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
        bool rejected=false;try{store.Save(pair,"12\t4"u8);}catch(ArgumentException){rejected=true;}Check(rejected,"control character accepted");
        foreach(var invalid in new[]{Array.Empty<byte>(),new byte[65],"中文密码"u8.ToArray(),"abc\u007f"u8.ToArray()})Check(!Tab5PinStore.Valid(invalid),"unsupported credential accepted");
        foreach(string single in new[]{"0","a","A","!"," "}){store.Save(pair,Encoding.ASCII.GetBytes(single));Check(store.Read(pair)!.AsSpan().SequenceEqual(Encoding.ASCII.GetBytes(single)),"single character encrypted round trip");}
        byte[] mixed=" Aa0!@#$_+{}[]:;\"'\\|<>?,./~` "u8.ToArray();
        store.Save(pair,mixed);Check(store.Read(pair)!.AsSpan().SequenceEqual(mixed),"case, symbols or edge spaces changed during encrypted round trip");
        Check(!Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(Encoding.ASCII.GetString(mixed)),"mixed plaintext persisted");
        var screen=new ComputerLockScreenState(Locked:true,LockScreenCover:null,DisplayState:1);
        var broker=new Tab5PinBroker(store,()=>screen);
        using(var legacy=JsonDocument.Parse(JsonSerializer.Serialize(new{kind="unlock-pin",op="read",requestId=Guid.NewGuid().ToString("D")})))
            Check(broker.Handle(legacy.RootElement,pair,"USB").Status==409,"old numeric firmware received unsupported text");
        foreach(string transport in new[]{"USB","BLE"})using(var extended=JsonDocument.Parse(JsonSerializer.Serialize(new{kind="unlock-pin",op="read",requestId=Guid.NewGuid().ToString("D"),credentialVersion=2}))) {
            var result=broker.Handle(extended.RootElement,pair,transport);Check(result.Status==200,"extended credential denied");
            var body=JsonSerializer.SerializeToElement(result.Body);string id=body.GetProperty("requestId").GetString()!;
            byte[] clear=Tab5Protocol.Decrypt(Convert.FromBase64String(pair.Key),id.Replace("-",""),Convert.FromBase64String(body.GetProperty("packet").GetString()!),128);
            Check(clear.AsSpan().SequenceEqual(mixed),"encrypted credential changed");CryptographicOperations.ZeroMemory(clear);
        }
        using(var malformed=JsonDocument.Parse(JsonSerializer.Serialize(new{kind="unlock-pin",op="read",requestId=Guid.NewGuid().ToString("D"),credentialVersion="2"})))
            Check(broker.Handle(malformed.RootElement,pair,"USB").Status==400,"malformed capability accepted");
        store.Save(pair,"654321"u8);CryptographicOperations.ZeroMemory(mixed);
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
        Methods(directory,pair);
        foreach(float scale in new[]{1f,1.5f,2f}){
            using var form=new Tab5PinForm(store,()=>pair,()=>true,ComputerUnlockSetupSelfTest.Ready(Path.Combine(directory,"setup-"+scale)));
            // Layout/font simulation; does not change the user's Windows DPI.
            var fonts=Desc(form).Prepend(form).Select(c=>(control:c,font:c.Font)).ToArray();
            form.Scale(new SizeF(scale,scale));foreach(var item in fonts)item.control.Font=new Font(item.font.FontFamily,item.font.Size*scale,item.font.Style);
            form.Show();Application.DoEvents();
            var controls=Desc(form).ToArray();var input=(TextBox)controls.Single(c=>c.Name=="saved-pin");var repeat=(TextBox)controls.Single(c=>c.Name=="repeat-pin");
            Check(input.UseSystemPasswordChar&&repeat.UseSystemPasswordChar,"unmasked credential field");
            var toggle=(CheckBox)controls.Single(c=>c.Name=="enable-oneclick");Check(!toggle.Checked,"UI default enabled");
            ((ComboBox)controls.Single(c=>c.Name=="login-method")).SelectedIndex=1;
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
            input.Text=repeat.Text=" TestAa9!@#[]{} ";((Button)controls.Single(c=>c.Name=="save-pin")).PerformClick();Check(store.Read(pair)!.AsSpan().SequenceEqual(" TestAa9!@#[]{} "u8),"UI changed case, symbols or spaces");
            input.Text=repeat.Text="中文密码";((Button)controls.Single(c=>c.Name=="save-pin")).PerformClick();Check(store.Read(pair)!.AsSpan().SequenceEqual(" TestAa9!@#[]{} "u8)&&input.Text.Length==0,"UI replaced unsupported Unicode with ASCII question marks");
            foreach(string single in new[]{"0","a","A","!"," "}){input.Text=repeat.Text=single;((Button)controls.Single(c=>c.Name=="save-pin")).PerformClick();Check(store.Read(pair)!.AsSpan().SequenceEqual(Encoding.ASCII.GetBytes(single))&&input.Text.Length==0&&repeat.Text.Length==0,"UI rejected or trimmed single character");}
            // FitScreen intentionally caps scaled dialogs to the monitor. The
            // production body scrolls, and feedback may have scrolled it down.
            // Verify each control is reachable, then retain strict clipping checks.
            Check(!controls.Any(c=>c.Name is "wifi-unlock-settings" or "login-helper" or "unlock-channel-status")&&controls.Single(c=>c.Name=="pin-status").Text=="已就绪","single-page settings retained separate setup or duplicate readiness");
            foreach(var name in new[]{"saved-pin","repeat-pin","saved-pin-eye","repeat-pin-eye","save-pin","delete-pin","enable-oneclick","login-method"}){
                var c=controls.Single(c=>c.Name==name);
                for(Control? ancestor=c.Parent;ancestor!=null;ancestor=ancestor.Parent)
                    if(ancestor is ScrollableControl{AutoScroll:true} viewport)viewport.ScrollControlIntoView(c);
                Application.DoEvents();
                var bounds=form.RectangleToClient(c.RectangleToScreen(c.ClientRectangle));
                Check(form.ClientRectangle.Contains(bounds)&&bounds.Height>=20,$"clipped PIN control {name}; scale={scale}; bounds={bounds}; client={form.ClientRectangle}");
                for(Control? ancestor=c.Parent;ancestor!=null;ancestor=ancestor.Parent)
                    if(ancestor is ScrollableControl{AutoScroll:true} viewport)
                        Check(viewport.ClientRectangle.Contains(viewport.RectangleToClient(c.RectangleToScreen(c.ClientRectangle))),$"PIN viewport clipped {name}; scale={scale}");
            }
            var footer=controls.Where(c=>c.Name is "save-pin" or "delete-pin").ToArray();Check(footer[0].Top==footer[1].Top&&footer[0].Height==footer[1].Height,"PIN footer alignment");
            using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(directory,$"pin-settings-{scale:0.0}.png"));
            ((Button)controls.Single(c=>c.Name=="delete-pin")).PerformClick();Check(!store.Has(pair)&&!toggle.Checked&&!store.IsEnabled(pair),"UI delete failed");
            input.Text="123456";Event(eye,"OnMouseDown",down);Event(form,"OnKeyDown",new KeyEventArgs(Keys.Escape));Check(form.IsDisposed&&input.Text.Length==0&&input.UseSystemPasswordChar,"Esc close retained input");
        }
        store.Delete();CryptographicOperations.ZeroMemory(pin);
        Console.WriteLine($"TAB5_PIN_SELF_TEST_OK checks={_checks}; synthetic credentials only; no device input");
    }
    private static void Methods(string directory,Tab5Pairing pair)
    {
        string path=Path.Combine(directory,"method-record.dat");var store=new Tab5PinStore(path);store.Delete();
        store.Save(pair,"1234"u8);store.SetEnabled(pair,true);
        Check(store.Method(pair)==LoginMethodKind.Manual,"numeric credential inferred as PIN");
        store.SetMethod(pair,LoginMethodKind.Password);store.SetEnabled(pair,false);store.SetEnabled(pair,true);
        Check(store.Method(pair)==LoginMethodKind.Password&&store.Read(pair,true)!.AsSpan().SequenceEqual("1234"u8),"method change or toggle changed credential");
        foreach(byte version in new byte[]{1,2}) {
            int offset=version==1?33:34;byte[] clear=new byte[offset+4];clear[0]=version;
            SHA256.HashData(Encoding.UTF8.GetBytes(pair.DeviceId+"|"+pair.Key)).CopyTo(clear,1);
            if(version==2)clear[33]=1;"1234"u8.CopyTo(clear.AsSpan(offset));
            File.WriteAllBytes(path,ProtectedData.Protect(clear,Encoding.UTF8.GetBytes("AI-bot TAB5 unlock PIN v1"),DataProtectionScope.CurrentUser));CryptographicOperations.ZeroMemory(clear);
            Check(store.Method(pair)==LoginMethodKind.Manual&&store.Read(pair)!.AsSpan().SequenceEqual("1234"u8),"legacy method or credential changed");
            store.SetMethod(pair,LoginMethodKind.Pin);Check(store.Method(pair)==LoginMethodKind.Pin&&store.Read(pair)!.AsSpan().SequenceEqual("1234"u8),"legacy upgrade lost credential");
        }
        store.SetMethod(pair,LoginMethodKind.Password);store.SetEnabled(pair,true);
        var state=new ComputerLockScreenState(Locked:true,DisplayState:1);bool ready=false;LoginMethodKind called=LoginMethodKind.Manual;
        var broker=new Tab5PinBroker(store,()=>state,prepare:method=>{called=method;return ready;});
        JsonDocument Request()=>JsonDocument.Parse(JsonSerializer.Serialize(new{kind="unlock-pin",op="read",credentialVersion=2,requestId=Guid.NewGuid().ToString("D")}));
        using(var request=Request()){
            var result=broker.Handle(request.RootElement,pair,"USB");
            Check(result.Status==409&&!JsonSerializer.Serialize(result.Body).Contains("packet")&&called==LoginMethodKind.Password,"unverified or inferred method released credential");
            ready=true;Check(broker.Handle(request.RootElement,pair,"USB").Status==409,"uncertain method request replayed");
        }
        using(var request=Request())Check(broker.Handle(request.RootElement,pair,"BLE").Status==200,"confirmed method denied");
        var changing=new Tab5PinBroker(store,()=>state,prepare:method=>{state=new(Locked:false,DisplayState:1);return true;});
        using(var request=Request())Check(changing.Handle(request.RootElement,pair,"USB").Status==409,"session changed after selection released credential");
        store.SetMethod(pair,LoginMethodKind.Manual);
        using(var form=new Tab5PinForm(store,()=>pair,()=>true,ComputerUnlockSetupSelfTest.Ready(Path.Combine(directory,"method-setup")))) {
            form.Show();Application.DoEvents();var controls=Desc(form).ToArray();var method=(ComboBox)controls.Single(c=>c.Name=="login-method");
            method.SelectedIndex=(int)LoginMethodKind.Password;((Button)controls.Single(c=>c.Name=="save-pin")).PerformClick();
            Check(store.Method(pair)==LoginMethodKind.Password&&store.Read(pair)!.AsSpan().SequenceEqual("1234"u8),"UI method-only save lost credential");
            form.Close();
        }
        store.Delete();
    }
    private static async Task Rpc(string directory,Tab5PinStore store)
    {
        var pairing=new Tab5PairingStore(Path.Combine(directory,"pair.dat"));var pair=pairing.Pair("001122aabbcc",@"USB\PIN-RPC-TEST");byte[] key=Convert.FromBase64String(pair.Key);store.Save(pair,"123456"u8);store.SetEnabled(pair,true);
        using var tasks=new Tab5CodexTasks(new Tab5CodexDesktop("unused-pin-test"),()=>[],new Tab5CodexJournal(Path.Combine(directory,"draft.dat")),new Tab5CodexImages(Path.Combine(directory,"images")));
        using var service=new Tab5Service(pairing,tasks,new Tab5Gallery(Path.Combine(directory,"gallery")));
        typeof(Tab5Service).GetField("_pinBroker",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(service,new Tab5PinBroker(store,()=>new(Locked:true,DisplayState:1)));
        service.Publish(new StatusSnapshot(1,"12:00",1000,28800,DateTimeOffset.UtcNow,new("idle",0),new("idle",0)));
        using var frame=JsonDocument.Parse(service.CurrentFrame!);string session=frame.RootElement.GetProperty("session").GetString()!;
        async Task<int> Call(bool proofOk=true,bool stale=false,bool oldSession=false,string transport="USB",string? requestId=null,bool binary=true,string kind="unlock-pin",bool extended=false){
            string nonce=Guid.NewGuid().ToString("N");byte[] json=JsonSerializer.SerializeToUtf8Bytes(new{kind,op=kind=="host-unlock"?"begin":"read",requestId=requestId??Guid.NewGuid().ToString("D"),session=oldSession?"old":session,issuedAt=DateTimeOffset.UtcNow.AddSeconds(stale?-121:0).ToUnixTimeMilliseconds(),binaryReply=binary});
            if(extended){var value=System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();value["credentialVersion"]=2;json=JsonSerializer.SerializeToUtf8Bytes(value);}
            byte[] packet=Tab5Protocol.Encrypt(key,nonce,binary?Tab5RpcBinary.Encode(json):json);
            string proof=proofOk?Tab5Protocol.Proof(key,$"POST|/tab5/v1/rpc|{pair.DeviceId}|{nonce}|{Convert.ToHexString(SHA256.HashData(packet)).ToLowerInvariant()}"):new string('0',64);
            var reply=await service.RpcAsync(pair.DeviceId,nonce,proof,packet,CancellationToken.None,transport:transport);
            if(reply.Status!=200)return reply.Status;using var body=Tab5RpcBinary.Open(Tab5Protocol.Decrypt(key,nonce,reply.Packet!)).Metadata;
            int status=body.RootElement.GetProperty("status").GetInt32();
            if(kind=="host-unlock"){
                var unsupported=body.RootElement.GetProperty("body");
                Check(status==409&&unsupported.GetProperty("error").GetString()=="host_unlock_unsupported"&&!unsupported.GetProperty("unlocked").GetBoolean()&&!unsupported.TryGetProperty("packet",out _),"host intent must not return a PIN");
                return status;
            }
            if(status==200){var envelope=body.RootElement.GetProperty("body");string aad=envelope.GetProperty("requestId").GetString()!.Replace("-","");var clearPin=Tab5Protocol.Decrypt(key,aad,Convert.FromBase64String(envelope.GetProperty("packet").GetString()!),128);byte[] expected=store.Read(pair)!;Check(clearPin.AsSpan().SequenceEqual(expected),"RPC envelope credential mismatch");CryptographicOperations.ZeroMemory(clearPin);CryptographicOperations.ZeroMemory(expected);}
            return status;
        }
        Check(await Call(proofOk:false)==401&&await Call(stale:true)==400&&await Call(oldSession:true)==400,"RPC authentication bypass");
        foreach(string value in new[]{"0","a","A","!"," "," A9![]{}\\\" "}){
            store.Save(pair,Encoding.ASCII.GetBytes(value));Check(await Call()==409,"legacy firmware received incompatible credential");
            foreach(string transport in new[]{"USB","BLE"})foreach(bool binary in new[]{false,true})Check(await Call(transport:transport,binary:binary,extended:true)==200,"single/mixed credential authenticated RPC failed");
        }
        store.Save(pair,"123456"u8);
        Check(await Call(transport:"Wi-Fi")==400,"Wi-Fi route enabled");
        Check(await Call(kind:"host-unlock")==409&&await Call(transport:"BLE",kind:"host-unlock")==409&&await Call(transport:"Wi-Fi",kind:"host-unlock")==409,"unsupported host intent route");
        string id=Guid.NewGuid().ToString("D");Check(await Call(requestId:id)==200&&await Call(transport:"BLE",requestId:id)==409,"cross-transport replay");
        Check(await Call(transport:"BLE")==200&&await Call(binary:false)==200,"authenticated BLE or JSON failed");store.SetEnabled(pair,false);Check(await Call()==404&&await Call(transport:"BLE")==404&&store.Has(pair),"RPC ignored disabled switch");
        store.SetEnabled(pair,true);Check(await Call()==200,"RPC reenable failed");store.Delete();Check(await Call()==404,"RPC deleted credential returned");
    }
}
