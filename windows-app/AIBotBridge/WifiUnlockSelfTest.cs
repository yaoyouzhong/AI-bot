using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace AIBotBridge;

internal static class WifiUnlockSelfTest
{
    private static int checks;
    private static void Check(bool value,string name){checks++;if(!value)throw new InvalidOperationException(name);}
    private sealed class Wire(byte[]? incoming=null):Stream
    {
        private readonly MemoryStream input=new(incoming??[]);
        internal readonly MemoryStream Sent=new();internal bool Closed;internal int FailWrite=-1,Writes;
        public override bool CanRead=>true;public override bool CanWrite=>true;public override bool CanSeek=>false;
        public override long Length=>input.Length;public override long Position{get=>input.Position;set=>throw new NotSupportedException();}
        public override int Read(byte[] b,int o,int c)=>input.Read(b,o,Math.Min(c,3));
        public override ValueTask<int> ReadAsync(Memory<byte> b,CancellationToken t=default)=>input.ReadAsync(b[..Math.Min(3,b.Length)],t);
        public override void Write(byte[] b,int o,int c){if(++Writes==FailWrite)throw new IOException("synthetic disconnect");Sent.Write(b,o,c);}
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> b,CancellationToken t=default){t.ThrowIfCancellationRequested();Write(b.ToArray(),0,b.Length);return ValueTask.CompletedTask;}
        public override void Flush(){}public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long v)=>throw new NotSupportedException();
        protected override void Dispose(bool disposing){Closed=true;input.Dispose();base.Dispose(disposing);}
    }
    private static byte[] Hello(byte type=2,uint auth=0,uint name=0){using var m=new MemoryStream();m.Write("RFB 003.008\n"u8);m.Write([1,type]);m.Write(Enumerable.Range(0,16).Select(i=>(byte)i).ToArray());byte[] result=new byte[4];BinaryPrimitives.WriteUInt32BigEndian(result,auth);m.Write(result);byte[] init=new byte[24];BinaryPrimitives.WriteUInt32BigEndian(init.AsSpan(20),name);m.Write(init);return m.ToArray();}
    private sealed class Flow:IDisposable
    {
        internal long Time=100;internal bool Enabled=true,Console=true;internal ComputerLockScreenState Screen=new(Locked:true,DisplayState:1);
        internal readonly Tab5PinStore Store;internal readonly Tab5Pairing Pair;internal readonly Tab5WifiUnlock Unlock;
        internal Wire? Active;internal int Connections;internal string Id="",Offer="";internal int Next;
        internal Flow(string folder,byte[]? pin=null){Directory.CreateDirectory(folder);Store=new(Path.Combine(folder,"synthetic-pin.dat"));Pair=new("001122aabbcc",@"USB\SYNTHETIC",Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));Store.Save(Pair,pin??"4826"u8.ToArray());Store.SetEnabled(Pair,true);
            Unlock=new(Store,_=>{Connections++;Active=new();return Task.FromResult(new WifiKeyboardRfb(Active));},()=>Screen,()=>Enabled,()=>Console,()=>Time);}
        internal async Task<JsonElement> Call(object value,string transport="Wi-Fi",string session="session-a",Tab5Pairing? pair=null){using var doc=JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(value));return JsonSerializer.SerializeToElement((await Unlock.HandleAsync(doc.RootElement,pair??Pair,transport,session,CancellationToken.None)).Body);}
        internal async Task<JsonElement> Begin(){Id=Guid.NewGuid().ToString("D");var result=await Call(new{kind="wifi-unlock",op="begin",requestId=Id});if(result.TryGetProperty("offer",out var offer)){Offer=offer.GetString()!;Next=result.GetProperty("next").GetInt32();}return result;}
        internal async Task<JsonElement> Poll(int advance=100){while(advance>1000){await Poll(1000);advance-=1000;}Time+=advance;var result=await Call(new{op="poll",requestId=Id,offer=Offer,next=Next});if(result.TryGetProperty("next",out var next))Next=next.GetInt32();return result;}
        internal async Task Prepare(){for(int i=0;i<20;i++){var result=await Poll();if(!Ok(result))throw new InvalidOperationException("synthetic preparation failed");if(result.GetProperty("stage").GetString()=="sending")return;}throw new InvalidOperationException("synthetic preparation too slow");}
        internal Task<JsonElement> Cancel()=>Call(new{op="cancel",requestId=Id,offer=Offer});
        public void Dispose()=>Unlock.Dispose();
    }
    private static bool Ok(JsonElement r)=>r.TryGetProperty("ok",out var ok)&&ok.GetBoolean();
    private static byte[] Buffer(Tab5WifiUnlock flow)=>(byte[])typeof(Tab5WifiUnlock).GetField("pin",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(flow)!;
    internal static async Task RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        const string vendorPath=@"C:\Program Files\TightVNC\tvnserver.exe";
        var service=new WifiKeyboardSettings.ServiceIdentity(0x10,4,1234,$"\"{vendorPath}\" -service","LocalSystem");
        Check(WifiKeyboardSettings.ServiceMatches(service,1234,vendorPath),"SCM running vendor service accepted");
        Check(!WifiKeyboardSettings.ServiceMatches(service,4321,vendorPath),"unrelated socket owner refused");
        Check(!WifiKeyboardSettings.ServiceMatches(service with{State=1},1234,vendorPath),"stopped service PID refused");
        Check(!WifiKeyboardSettings.ServiceMatches(service with{Type=0x20},1234,vendorPath),"shared service process refused");
        Check(!WifiKeyboardSettings.ServiceMatches(service with{Account="LocalService"},1234,vendorPath),"unexpected service account refused");
        Check(!WifiKeyboardSettings.ServiceMatches(service with{Command=@"C:\Users\Public\tvnserver.exe -service"},1234,vendorPath),"same filename outside protected install refused");
        Check(!WifiKeyboardSettings.ServiceMatches(service with{Command=vendorPath+" -service"},1234,vendorPath),"unquoted service command refused");
        Check(!WifiKeyboardSettings.ServiceMatches(service with{Command=service.Command+" -extra"},1234,vendorPath),"unexpected service arguments refused");
        var wire=new Wire(Hello());using(var client=new WifiKeyboardRfb(wire)){
            await client.HandshakeAsync("vncTest9"u8.ToArray(),CancellationToken.None);byte[] sent=wire.Sent.ToArray();
            Check(sent.Length==30&&sent[12]==2&&sent[^1]==1,"RFB shared authenticated handshake");
            Check(Convert.ToHexString(sent.AsSpan(13,16))=="54E0EB568FD156B79321811C316A6EAF","independent DES vector");
            await client.PressAsync('4',CancellationToken.None);Check(wire.Sent.Length==46,"key down/up");
            bool rejected=false;try{await client.PressAsync('A',CancellationToken.None);}catch(IOException){rejected=true;}Check(rejected,"general keys denied");
        }
        var authOnly=new Wire(Hello().AsSpan(0,34).ToArray());using(var client=new WifiKeyboardRfb(authOnly)) {
            await client.HandshakeAsync("vncTest9"u8.ToArray(),CancellationToken.None,initialize:false);
            Check(authOnly.Sent.Length==29,"Settings authentication started desktop initialization or sent input");
        }
        foreach(var bytes in new[]{Hello(1),Hello(2,1),Hello(2,0,5000),"BAD PROTOCOL"u8.ToArray()}){using var c=new WifiKeyboardRfb(new Wire(bytes));bool bad=false;try{await c.HandshakeAsync("vncTest9"u8.ToArray(),CancellationToken.None);}catch(IOException){bad=true;}Check(bad,"RFB invalid authentication or protocol denied");}
        var access=new WifiKeyboardAccessStore(Path.Combine(directory,"synthetic-access.dat"));Check(!access.Enabled,"Wi-Fi defaults off");access.Save(false,"vncTest9"u8);Check(!access.Enabled,"disabled access preserved");access.Save(true,"vncTest9"u8);Check(access.Enabled,"explicit enable");
        Check(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(directory,"synthetic-access.dat"))).Contains("vncTest9"),"access secret encrypted");
        IEnumerable<Control> Desc(Control c){foreach(Control child in c.Controls){yield return child;foreach(var nested in Desc(child))yield return nested;}}
        var previewPin=new Tab5PinStore(Path.Combine(directory,"preview-synthetic-pin.dat"));var previewPair=new Tab5Pairing("aabbcc001122",@"USB\PREVIEW",Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));previewPin.Save(previewPair,"4826"u8);previewPin.SetEnabled(previewPair,true);
        foreach(float scale in new[]{1f,1.5f,2f})using(var form=new WifiUnlockForm(access,()=>true,_=>Task.CompletedTask,new TightVncSelfTest.FakeController(access,new(TightVncKind.Managed,true,true,new string('1',32))))){
            form.Show();Application.DoEvents();
            var fonts=Desc(form).Prepend(form).Select(c=>(control:c,font:c.Font)).ToArray();form.Scale(new SizeF(scale,scale));
            foreach(var item in fonts)item.control.Font=new Font(item.font.FontFamily,item.font.Size*scale,item.font.Style);
            Application.DoEvents();
            foreach(string name in new[]{"wifi-enabled","wifi-component-state","wifi-save","wifi-check"}){
                var c=Desc(form).Single(c=>c.Name==name);Check(form.ClientRectangle.Contains(form.RectangleToClient(c.RectangleToScreen(c.ClientRectangle))),"Wi-Fi settings control clipped");
            }
            var actions=Desc(form).Where(c=>c.Name is "wifi-save" or "wifi-check").ToArray();Check(actions[0].Top==actions[1].Top&&actions[0].Height==actions[1].Height,"Wi-Fi footer alignment");
            using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(directory,scale==1?"wifi-settings.png":$"wifi-settings-{scale:0.0}.png"));form.Close();
            using var pinForm=new Tab5PinForm(previewPin,()=>previewPair,()=>true);
            var pinFonts=Desc(pinForm).Prepend(pinForm).Select(c=>(control:c,font:c.Font)).ToArray();pinForm.Scale(new SizeF(scale,scale));foreach(var item in pinFonts)item.control.Font=new Font(item.font.FontFamily,item.font.Size*scale,item.font.Style);
            pinForm.Show();Application.DoEvents();using var pinBitmap=new Bitmap(pinForm.Width,pinForm.Height);pinForm.DrawToBitmap(pinBitmap,new Rectangle(Point.Empty,pinForm.Size));pinBitmap.Save(Path.Combine(directory,scale==1?"pin-settings.png":$"pin-settings-{scale:0.0}.png"));pinForm.Close();
        }
        int sequence=0;Flow New(byte[]? pin=null)=>new(Path.Combine(directory,"case-"+(sequence++)),pin);
        using(var f=New()){
            await f.Begin();await f.Poll();await f.Poll(300);
            Check(Buffer(f.Unlock).Length==0&&f.Active!.Sent.Length==0,"short stable interval neither stages PIN nor types");
            f.Screen=f.Screen with{DisplayState=null};await f.Poll();
            f.Screen=f.Screen with{DisplayState=1};await f.Poll();await f.Poll(400);
            Check(Buffer(f.Unlock).Length==0&&f.Active!.Sent.Length==0,"unknown display resets stability interval");
            await f.Poll();byte[] staged=Buffer(f.Unlock);
            Check(staged.Length==4&&f.Active!.Sent.Length==0&&f.Time<1500,"stable ready state prepares in under 1.5 seconds without typing");
            await f.Cancel();Check(staged.All(b=>b==0)&&f.Active!.Sent.Length==0&&f.Active.Closed,"cancel before first digit wipes without input");
        }
        using(var f=New()){
            foreach(string t in new[]{"USB","BLE"})Check(!Ok(await f.Call(new{op="begin",requestId=Guid.NewGuid().ToString("D")},t)),"other transport refused");
            Check(!Ok(await f.Call(new{op="begin",requestId=Guid.NewGuid().ToString("D"),pin="public-fixture"})),"PIN fields refused");
            f.Enabled=false;Check(!Ok(await f.Begin())&&f.Connections==0,"disabled no connect");f.Enabled=true;
            f.Console=false;Check(!Ok(await f.Begin()),"nonconsole refused");f.Console=true;
            f.Screen=f.Screen with{Locked=null};Check(!Ok(await f.Begin()),"unknown lock refused");f.Screen=f.Screen with{Locked=true};
            Check(Ok(await f.Begin()),"begin");string id=f.Id;
            Check(!Ok(await f.Call(new{op="begin",requestId=id.ToUpperInvariant()}))&&f.Connections==1,"UUID case cannot replay begin");
            Check(!Ok(await f.Call(new{op="begin",requestId=id}))&&f.Connections==1,"request ID single use");
            Check(!Ok(await f.Call(new{op="cancel",requestId=id,offer=f.Offer},session:"other"))&&!f.Active!.Closed,"foreign session cannot cancel");
            Check(Ok(await f.Poll())&&f.Active!.Sent.Length==0,"opening does not read PIN/type early");
            await f.Prepare();byte[] retained=Buffer(f.Unlock);Check(retained.Length==4,"local PIN staged");
            for(int i=0;i<4;i++)Check(Ok(await f.Poll()),"digit authorized once");
            Check(retained.All(b=>b==0)&&Buffer(f.Unlock).Length==0,"PIN wiped after digits");
            f.Screen=f.Screen with{Locked=false};Check((await f.Poll(400)).GetProperty("unlocked").GetBoolean(),"real lock confirmation");
            Check(f.Active!.Sent.Length==64,"auto-submit no Enter");
            Check(f.Unlock.Diagnostic.StartsWith("unlocked; totalMs=")&&!f.Unlock.Diagnostic.Contains("4826"),"success timing excludes PIN");
        }
        using(var f=New()){
            await f.Begin();await f.Prepare();for(int i=0;i<4;i++)await f.Poll();
            await f.Poll(400);Check(f.Active!.Sent.Length==80,"one Enter after settle");
            await f.Poll(500);Check(f.Active.Sent.Length==80,"confirmation no repeated Enter");
            Check(!Ok(await f.Poll(10000))&&f.Active.Closed,"no-confirmation stops");
        }
        using(var f=New(Enumerable.Repeat((byte)'4',64).ToArray())){
            await f.Begin();await f.Prepare();for(int i=0;i<64;i++)await f.Poll();
            f.Screen=f.Screen with{Locked=false};Check(Ok(await f.Poll(400))&&f.Active!.Sent.Length==1024,"64 repeated digits release each");
        }
        using(var f=New()){
            await f.Begin();await f.Prepare();byte[] retained=Buffer(f.Unlock);await f.Poll();
            Check(!Ok(await f.Call(new{op="poll",requestId=f.Id,offer=f.Offer,next=f.Next-1}))&&f.Active!.Closed&&retained.All(b=>b==0),"fresh-nonce duplicate step stops/wipes");
        }
        using(var f=New()){
            await f.Begin();Check(!Ok(await f.Call(new{op="poll",requestId=f.Id,offer=f.Offer,next="0"}))&&f.Active!.Closed,"malformed sequence stops without input");
        }
        foreach(string scenario in new[]{"cancel","lease","session","disable","pin-disable","screen","disconnect","unlocked-early"})using(var f=New()){
            await f.Begin();await f.Prepare();byte[] retained=Buffer(f.Unlock);
            switch(scenario){
                case "cancel":await f.Cancel();break;
                case "lease":f.Time+=2600;await Task.Delay(350);break;
                case "session":f.Console=false;await f.Poll();break;
                case "disable":f.Enabled=false;await f.Poll();break;
                case "pin-disable":f.Store.SetEnabled(f.Pair,false);await f.Poll();break;
                case "screen":f.Screen=f.Screen with{DisplayState=0};await f.Poll();break;
                case "disconnect":f.Active!.FailWrite=2;Check(!Ok(await f.Poll()),"uncertain write stops");break;
                case "unlocked-early":f.Screen=f.Screen with{Locked=false};Check(!Ok(await f.Poll()),"early unlock no success");break;
            }
            Check(f.Active!.Closed&&retained.All(b=>b==0),scenario+" wipes and disconnects");
            Check(f.Active.Sent.Length<32,"never sends next digit after failure");
        }
        using(var f=New()){
            f.Screen=f.Screen with{DisplayState=0};await f.Begin();await f.Poll();Check(f.Active!.Sent.Length==16,"off wake Shift only");
            await f.Poll(1600);Check(f.Active.Sent.Length==32,"at most second wake");for(int i=0;i<120;i++)await f.Poll();
            Check(f.Active.Closed&&f.Active.Sent.Length==32,"wake budget bounded no PIN");
        }
        using(var f=New()){
            f.Screen=f.Screen with{LockScreenCover=true};await f.Begin();await f.Poll(6000);Check(f.Active!.Sent.Length==16,"cover Space once");
            for(int i=0;i<70;i++)await f.Poll();Check(f.Active.Sent.Length==16&&f.Active.Closed,"cover uncertainty no PIN");
        }
        await RpcAsync(directory);
        File.WriteAllText(Path.Combine(directory,"result.json"),JsonSerializer.Serialize(new{passed=true,checks,syntheticOnly=true,inputSentToWindows=false,hardwareUnlockTested=false}));
        Console.WriteLine($"WIFI_UNLOCK_SELF_TEST_OK checks={checks}; synthetic RFB, lifecycle, DPAPI, UI, encrypted RPC; no real input");
    }
    private static async Task RpcAsync(string directory)
    {
        string root=Path.Combine(directory,"rpc");Directory.CreateDirectory(root);
        var pairs=new Tab5PairingStore(Path.Combine(root,"pair.dat"));var pair=pairs.Pair("001122aabbcc",@"USB\TEST");
        using var tasks=new Tab5CodexTasks(new Tab5CodexDesktop("unused-wifi-unlock-test"),()=>[],new Tab5CodexJournal(Path.Combine(root,"draft.dat")),new Tab5CodexImages(Path.Combine(root,"images")));
        using var service=new Tab5Service(pairs,tasks,new Tab5Gallery(Path.Combine(root,"gallery")));
        service.Publish(new StatusSnapshot(1,"12:00",1000,28800,DateTimeOffset.UtcNow,new("idle",0),new("idle",0)));
        using var frame=JsonDocument.Parse(service.CurrentFrame!);string session=frame.RootElement.GetProperty("session").GetString()!;
        var pinStore=new Tab5PinStore(Path.Combine(root,"synthetic-pin.dat"));pinStore.Save(pair,"4826"u8);pinStore.SetEnabled(pair,true);
        long clock=100;var wire=new Wire();service.WifiUnlock=new(pinStore,_=>Task.FromResult(new WifiKeyboardRfb(wire)),()=>new(Locked:true,DisplayState:1),()=>true,()=>true,()=>clock);
        byte[] key=Convert.FromBase64String(pair.Key);string requestId=Guid.NewGuid().ToString("D");
        async Task<JsonElement> Call(string op,string offer="",int next=0,string transport="Wi-Fi",string? requestedSession=null,bool invalidTime=false){
            string nonce=Guid.NewGuid().ToString("N");byte[] packet=Tab5Protocol.Encrypt(key,nonce,JsonSerializer.SerializeToUtf8Bytes(new{kind="wifi-unlock",op,requestId,offer,next,session=requestedSession??session,issuedAt=DateTimeOffset.UtcNow.AddMinutes(invalidTime?-5:0).ToUnixTimeMilliseconds()}));
            string proof=Tab5Protocol.Proof(key,$"POST|/tab5/v1/rpc|{pair.DeviceId}|{nonce}|{Convert.ToHexString(SHA256.HashData(packet)).ToLowerInvariant()}");
            var result=await service.RpcAsync(pair.DeviceId,nonce,proof,packet,CancellationToken.None,transport:transport);
            using var decoded=JsonDocument.Parse(Tab5Protocol.Decrypt(key,nonce,result.Packet!));
            var replay=await service.RpcAsync(pair.DeviceId,nonce,proof,packet,CancellationToken.None,transport:transport);
            using var repeat=JsonDocument.Parse(Tab5Protocol.Decrypt(key,nonce,replay.Packet!));Check(repeat.RootElement.GetProperty("status").GetInt32()==(invalidTime||requestedSession is not null?400:409),"outer nonce replay denied");
            Check((await service.RpcAsync(pair.DeviceId,nonce,new string('0',64),packet,CancellationToken.None,transport:transport)).Status==401,"bad HMAC denied");
            string response=decoded.RootElement.GetRawText();Check(!response.Contains("4826")&&!response.Contains("packet")&&!response.Contains("key"),"PIN and encryption keys absent in response");
            return decoded.RootElement.Clone();
        }
        foreach(string t in new[]{"USB","BLE"})Check((await Call("begin",transport:t)).GetProperty("status").GetInt32()==409,"RPC transport denied");
        Check((await Call("begin",requestedSession:"other")).GetProperty("status").GetInt32()==400,"RPC session denied");
        Check((await Call("begin",invalidTime:true)).GetProperty("status").GetInt32()==400,"RPC stale time denied");
        var begin=(await Call("begin")).GetProperty("body");Check(Ok(begin),"real encrypted begin dispatch");string offer=begin.GetProperty("offer").GetString()!;
        // Invalid session/time nonces are not admitted into the replay cache, so
        // the common helper below tests valid requests only for repeated nonces.
        int step=0;bool prepared=false;
        for(int i=0;i<20;i++){clock+=100;var body=(await Call("poll",offer,step++)).GetProperty("body");if(body.GetProperty("stage").GetString()=="sending"){prepared=true;break;}}
        Check(prepared&&wire.Sent.Length==0,"encrypted preparation stable before input");
        for(int i=0;i<4;i++){var body=(await Call("poll",offer,step++)).GetProperty("body");Check(Ok(body),"encrypted poll");clock+=100;}
        Check(wire.Sent.Length==64,"encrypted RPC types four digits exactly once");
        await Call("cancel",offer);Check(wire.Closed,"encrypted cancel closes");CryptographicOperations.ZeroMemory(key);
    }
}
