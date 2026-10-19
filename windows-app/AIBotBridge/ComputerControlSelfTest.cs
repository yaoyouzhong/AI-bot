using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIBotBridge;
internal static class ComputerControlSelfTest
{
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private sealed class FixtureClock:TimeProvider {
        internal DateTimeOffset Now=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>Now;
    }
    private sealed class Backend:IComputerControlBackend {
        internal ComputerControlState State=new(VolumeAvailable:true,Volume:40,CanLock:true,MediaAvailable:true,CanPlayPause:true,CanPrevious:true,CanNext:true);
        internal int Calls;internal bool Fail,Throw;internal string? Launched;
        internal ComputerLockScreenState LockScreen=new();
        public ComputerLockScreenState ReadLockScreen()=>LockScreen;
        public Task<ComputerControlState> ReadAsync(CancellationToken token)=>Task.FromResult(State);
        public Task<bool> ExecuteAsync(ComputerCommand command,ComputerLauncher? launcher,CancellationToken token) {
            Calls++;if(Throw)throw new IOException("simulated OS failure");if(Fail)return Task.FromResult(false);
            if(command.Op=="volume")State=State with{Volume=command.Volume!.Value};
            if(command.Op=="mute")State=State with{Muted=command.Muted!.Value};
            if(command.Op=="media")State=State with{Playing=!State.Playing};
            if(command.Op=="launch")Launched=launcher?.Path;
            return Task.FromResult(true);
        }
    }
    internal static void Run(string directory) {
        if(!AppPaths.IsPublicSelfTest)throw new InvalidOperationException("Computer control tests require an isolated profile.");
        Check(WindowsSessionLock.Decode(0)==true&&WindowsSessionLock.Decode(1)==false&&WindowsSessionLock.Decode(-1) is null,"session lock flags decoded incorrectly");
        Check(WindowsSessionLock.Read() is not null,"actual desktop lock state unavailable");
        Check(WindowsDisplayPower.Read() is null,"display state assumed before registration");
        Check(WindowsDisplayPower.Decode(0)==0&&WindowsDisplayPower.Decode(1)==1&&WindowsDisplayPower.Decode(2)==2&&
            WindowsDisplayPower.Decode(-1) is null&&WindowsDisplayPower.Decode(3) is null,"invalid display state accepted");
        string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string cover=Path.Combine(windows,"SystemApps","Microsoft.LockApp_cw5n1h2txyewy","LockApp.exe");
        Check(WindowsLockScreen.IsCover(true,"Windows.UI.Core.CoreWindow",cover,1,1,windows),"real cover metadata not recognized");
        Check(!WindowsLockScreen.IsCover(false,"Windows.UI.Core.CoreWindow",cover,1,1,windows)&&
            !WindowsLockScreen.IsCover(null,"Windows.UI.Core.CoreWindow",cover,1,1,windows)&&
            !WindowsLockScreen.IsCover(true,"other",cover,1,1,windows)&&
            !WindowsLockScreen.IsCover(true,"Windows.UI.Core.CoreWindow",cover,2,1,windows)&&
            !WindowsLockScreen.IsCover(true,"Windows.UI.Core.CoreWindow",Path.Combine(directory,"LockApp.exe"),1,1,windows)&&
            !WindowsLockScreen.IsCover(true,"Windows.UI.Core.CoreWindow",null,1,1,windows),"ambiguous/spoofed metadata permits Space");
        Directory.CreateDirectory(directory);var store=new ComputerLauncherStore(Path.Combine(directory,"computer-launchers.json"));
        string target=Path.Combine(directory,"fixture-folder");Directory.CreateDirectory(target);
        var item=new ComputerLauncher(Guid.NewGuid().ToString("D"),"项目文件夹",target);store.Save([item]);
        Check(new ComputerLauncherStore(Path.Combine(directory,"computer-launchers.json")).Load().Single()==item,"launcher configuration did not persist");
        foreach(string malformed in new[]{"[null]","[{\"Id\":null,\"Name\":null,\"Path\":null}]"}) {
            string malformedPath=Path.Combine(directory,"invalid-launchers.json");File.WriteAllText(malformedPath,malformed);
            bool rejected=false;try{new ComputerLauncherStore(malformedPath).Load();}catch(InvalidDataException){rejected=true;}Check(rejected,"null launcher entry or fields not rejected");
        }
        foreach(var invalid in new[]{new[]{item with{Path="https://example.com"}},new[]{item with{Path=@"\\server\share"}},new[]{item,item},Enumerable.Range(0,5).Select(_=>item with{Id=Guid.NewGuid().ToString("D")}).ToArray()}) {
            bool rejected=false;try{store.Save(invalid);}catch(InvalidDataException){rejected=true;}Check(rejected,"invalid launcher configuration accepted");
        }
        var icons=new ComputerLauncherIcons();
        string program=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"notepad.exe");
        var app=new ComputerLauncher(Guid.NewGuid().ToString("D"),"记事本",program);
        var firstIcons=icons.Get([app,item],"");
        Check(firstIcons.Views[0].Kind=="app"&&firstIcons.Views[1].Kind=="folder","launcher types incorrect");
        for(int i=0;i<2;i++) {
            byte[] pixels=Convert.FromBase64String(firstIcons.Views[i].Icon??"");
            Check(pixels.Length==4096&&pixels.Where((_,n)=>n%4==3).Any(a=>a>0),"shell icon missing or transparent");
            File.WriteAllBytes(Path.Combine(directory,$"launcher-icon-{i}.bgra"),pixels);
        }
        Check(icons.Get([app,item],firstIcons.Revision).Views.All(x=>x.Icon is null),"unchanged icons retransmitted");
        var changed=icons.Get([app with{Path=item.Path},item],firstIcons.Revision);
        Check(changed.Revision!=firstIcons.Revision&&changed.Views[0].Kind=="folder"&&changed.Views[0].Icon is not null,"changed target retained old icon");
        Check(icons.Get([app with{Path=Path.Combine(directory,"missing.exe")}],"").Views[0].Icon is null,"missing target did not use fallback");
        RunAsync(store,item).GetAwaiter().GetResult();RpcAsync(directory,store).GetAwaiter().GetResult();
        var music=new MusicSnapshot("Displayed song","Artist","Album",true,0,120,DateTimeOffset.UtcNow){SystemMediaSource="player-a"};
        Check(WindowsComputerControlBackend.Matches(music,"player-a","Displayed song","Artist"),"matching displayed media disabled");
        Check(!WindowsComputerControlBackend.Matches(music,"player-b","Displayed song","Artist")&&!WindowsComputerControlBackend.Matches(music,"player-a","Other song","Artist")&&
            !WindowsComputerControlBackend.Matches(music with{SystemMediaSource=null},"player-a","Displayed song","Artist"),"unrelated or non-SMTC displayed music controls another player");
        IEnumerable<Control> All(Control parent)=>parent.Controls.Cast<Control>().SelectMany(c=>new[]{c}.Concat(All(c)));
        foreach(float scale in new[]{1f,1.5f,2f})using(var form=new ComputerControlForm(store)) {
            var owned=new List<Font>();form.Show();var fonts=All(form).Select(c=>(Control:c,Font:c.Font)).ToArray();
            if(scale!=1){form.Scale(new SizeF(scale,scale));foreach(var f in fonts){var font=new Font(f.Font.FontFamily,f.Font.SizeInPoints*scale,f.Font.Style);owned.Add(font);f.Control.Font=font;}}
            form.PerformLayout();Application.DoEvents();var save=form.Controls.Find("save-launchers",true).Single();
            Check(save.Visible&&form.ClientRectangle.Contains(form.RectangleToClient(save.RectangleToScreen(save.ClientRectangle))),"save button hidden or clipped");
            using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));
            bitmap.Save(Path.Combine(directory,$"computer-control-{scale:0.0}.png"));form.Close();foreach(var font in owned)font.Dispose();
        }
        var confirm=UnsavedChanges.Confirm;try {
            int prompts=0;UnsavedChanges.Confirm=_=>{prompts++;return false;};using var form=new ComputerControlForm(store);form.Show();Application.DoEvents();
            var path=(TextBox)form.Controls.Find("launcher-path-0",true).Single();string original=path.Text;path.Text=directory;form.Close();
            Check(!form.IsDisposed&&prompts==1,"path-only edits were silently discarded");path.Text=original;form.Close();Check(form.IsDisposed,"unchanged form cannot close");
        }finally{UnsavedChanges.Confirm=confirm;}
        File.WriteAllText(Path.Combine(directory,"result.txt"),"COMPUTER_CONTROL_SELF_TEST_OK\nFake OS only: volume/mute/lock/launch/media; encrypted RPC authentication; idempotency/conflicts; unsupported capability; launcher persistence; unsaved-path protection; UI font/geometry simulations 100/150/200% (not OS monitor DPI changes).\n");
        Console.WriteLine("COMPUTER_CONTROL_SELF_TEST_OK");
    }
    private static async Task RunAsync(ComputerLauncherStore store,ComputerLauncher item) {
        var backend=new Backend();var clock=new FixtureClock();var service=new ComputerControlService(backend,store,clock);
        async Task<(int Status,JsonElement Body)> Call(object command) {using var document=JsonDocument.Parse(JsonSerializer.Serialize(command));var reply=await service.HandleAsync(document.RootElement,CancellationToken.None);return(reply.Status,JsonSerializer.SerializeToElement(reply.Body,JsonDefaults.Options));}
        var status=await Call(new{op="status"});Check(status.Status==200&&status.Body.GetProperty("launchers").GetArrayLength()==1&&!status.Body.GetProperty("launchers")[0].TryGetProperty("path",out _),"status leaks a path or misses launchers");
        Check(!status.Body.TryGetProperty("displayState",out var missingDisplay)||missingDisplay.ValueKind==JsonValueKind.Null,"unknown display assumed on");
        foreach(int display in new[]{0,1,2}) {
            backend.State=backend.State with{DisplayState=display};
            var screen=await Call(new{op="status"});
            Check(screen.Body.GetProperty("displayState").GetInt32()==display&&backend.Calls==0,"ordinary screen report mutated OS or lost state");
        }
        backend.State=backend.State with{DisplayState=null};
        var unknown=await Call(new{op="lock-screen"});
        Check(unknown.Status==200&&(!unknown.Body.TryGetProperty("lockScreenCover",out var unknownCover)||unknownCover.ValueKind==JsonValueKind.Null)&&backend.Calls==0,"unknown lock page became confirmed cover or invoked OS");
        backend.LockScreen=new(Locked:true,LockScreenCover:true);
        var cover=await Call(new{op="lock-screen"});Check(cover.Body.GetProperty("lockScreenCover").GetBoolean()&&backend.Calls==0,"read-only cover probe failed");
        Check((await Call(new{op="lock-screen",pin="public-fixture"})).Status==400&&backend.Calls==0,"probe accepts credential parameter");
        foreach(int display in new[]{0,1,2}) {
            backend.LockScreen=new(Locked:true,DisplayState:display);
            var power=await Call(new{op="lock-screen"});
            Check(power.Body.GetProperty("displayState").GetInt32()==display&&backend.Calls==0,"power probe mutated or lost state");
        }
        backend.LockScreen=new(Locked:true);
        string revision=status.Body.GetProperty("iconRevision").GetString()!;
        Check(status.Body.GetProperty("launchers")[0].GetProperty("icon").GetString()!.Length==5464,"icon not included in initial state");
        var cached=await Call(new{op="status",iconRevision=revision});
        Check(!cached.Body.GetProperty("launchers")[0].TryGetProperty("icon",out var cachedIcon)||cachedIcon.ValueKind==JsonValueKind.Null,"status icon cache ignored");
        string request=Guid.NewGuid().ToString("D");var set=new{op="volume",volume=63,requestId=request};
        var parallel=await Task.WhenAll(Enumerable.Range(0,6).Select(_=>Call(set)));Check(parallel.All(x=>x.Status==200)&&backend.Calls==1&&backend.State.Volume==63,"concurrent retry duplicated volume change");
        Check((await Call(new{op="volume",volume=64,requestId=request})).Status==409&&backend.Calls==1,"conflicting payload accepted");
        foreach(object invalid in new object[]{new{op="volume",volume=101,requestId=Guid.NewGuid()},new{op="volume",volume="20",requestId=Guid.NewGuid()},new{op="mute",muted=1,requestId=Guid.NewGuid()},new{op="lock",requestId="not-a-uuid"},new{op="launch",launcherId=item.Id,path=item.Path,requestId=Guid.NewGuid()},new{op="media",action="stop",requestId=Guid.NewGuid()},new{op="format-disk",requestId=Guid.NewGuid()}})
            Check((await Call(invalid)).Status==400&&backend.Calls==1,"invalid request invoked OS");
        foreach(object command in new object[]{new{op="mute",muted=true,requestId=Guid.NewGuid()},new{op="lock",requestId=Guid.NewGuid()},new{op="launch",launcherId=item.Id,requestId=Guid.NewGuid()},new{op="media",action="play-pause",requestId=Guid.NewGuid()},new{op="media",action="previous",requestId=Guid.NewGuid()},new{op="media",action="next",requestId=Guid.NewGuid()}}) {
            int before=backend.Calls;var first=await Call(command);var retry=await Call(command);Check(first.Status==200&&retry.Status==200&&backend.Calls==before+1,"side effect did not deduplicate");
        }
        Check(backend.State.Muted&&backend.Launched==item.Path,"configured launcher was not resolved locally");
        backend.State=backend.State with{Locked=true,CanLock=false};
        var lockedState=await Call(new{op="status"});
        Check(lockedState.Body.GetProperty("locked").GetBoolean()&&(await Call(new{op="lock",requestId=Guid.NewGuid()})).Status==409,"locked state missing or duplicate lock allowed");
        backend.State=backend.State with{Locked=false,CanLock=true};
        Check(!(await Call(new{op="status"})).Body.GetProperty("locked").GetBoolean(),"unlock state did not refresh");
        int count=backend.Calls;
        Check((await Call(new{op="launch",launcherId=Guid.NewGuid(),requestId=Guid.NewGuid()})).Status==404&&backend.Calls==count,"unknown launcher invoked OS");
        backend.State=backend.State with{MediaAvailable=false};Check((await Call(new{op="media",action="next",requestId=Guid.NewGuid()})).Status==409&&backend.Calls==count,"missing media session reported success");
        backend.State=backend.State with{VolumeAvailable=false};Check((await Call(new{op="mute",muted=false,requestId=Guid.NewGuid()})).Status==409&&backend.Calls==count,"missing volume endpoint reported success");
        backend.Fail=true;var failed=new{op="lock",requestId=Guid.NewGuid()};Check((await Call(failed)).Status==503&&(await Call(failed)).Status==503&&backend.Calls==count+1,"failed command retried side effect");
        backend.Fail=false;backend.Throw=true;var thrown=new{op="lock",requestId=Guid.NewGuid()};Check((await Call(thrown)).Status==503&&(await Call(thrown)).Status==503&&backend.Calls==count+2,"OS exception retried side effect");
        backend.Throw=false;var cappedBackend=new Backend();var capped=new ComputerControlService(cappedBackend,store,clock);
        async Task<int> Fill(Guid id){using var doc=JsonDocument.Parse(JsonSerializer.Serialize(new{op="lock",requestId=id}));return(await capped.HandleAsync(doc.RootElement,CancellationToken.None)).Status;}
        var firstId=Guid.NewGuid();Check(await Fill(firstId)==200,"first receipt missing");for(int i=1;i<256;i++)Check(await Fill(Guid.NewGuid())==200,"capacity reached too early");
        Check(await Fill(Guid.NewGuid())==429&&await Fill(firstId)==200&&cappedBackend.Calls==256,"capacity evicted active receipt");
        clock.Now=clock.Now.AddSeconds(121);Check(await Fill(Guid.NewGuid())==200,"expired receipts did not release capacity");
    }
    private static async Task RpcAsync(string directory,ComputerLauncherStore launchers) {
        var pairing=new Tab5PairingStore(Path.Combine(directory,"pair.dat"));var pair=pairing.Pair("001122aabbcc",@"USB\COMPUTER-CONTROL-TEST");byte[] key=Convert.FromBase64String(pair.Key);
        using var tasks=new Tab5CodexTasks(new Tab5CodexDesktop("unused-computer-control-test"),()=>[],new Tab5CodexJournal(Path.Combine(directory,"draft.dat")),new Tab5CodexImages(Path.Combine(directory,"images")));
        var backend=new Backend();using var service=new Tab5Service(pairing,tasks,new Tab5Gallery(Path.Combine(directory,"gallery"))){ComputerControl=new(backend,launchers)};
        service.Publish(new StatusSnapshot(1,"12:00",1000,28800,DateTimeOffset.UtcNow,new("idle",0),new("idle",0)));
        using var frame=JsonDocument.Parse(service.CurrentFrame!);string session=frame.RootElement.GetProperty("session").GetString()!;
        async Task<int> Call(object command,bool validProof=true,bool stale=false,string? sessionOverride=null) {
            var root=JsonSerializer.SerializeToNode(command)!.AsObject();root["kind"]="computer-control";root["session"]=sessionOverride??session;root["issuedAt"]=DateTimeOffset.UtcNow.AddSeconds(stale?-121:0).ToUnixTimeMilliseconds();
            string nonce=Guid.NewGuid().ToString("N");byte[] packet=Tab5Protocol.Encrypt(key,nonce,JsonSerializer.SerializeToUtf8Bytes(root));
            string proof=validProof?Tab5Protocol.Proof(key,$"POST|/tab5/v1/rpc|{pair.DeviceId}|{nonce}|{Convert.ToHexString(SHA256.HashData(packet)).ToLowerInvariant()}"):new string('0',64);
            var reply=await service.RpcAsync(pair.DeviceId,nonce,proof,packet,CancellationToken.None);
            if(reply.Status!=200)return reply.Status;using var json=JsonDocument.Parse(Tab5Protocol.Decrypt(key,nonce,reply.Packet!));return json.RootElement.GetProperty("status").GetInt32();
        }
        var command=new{op="lock",requestId=Guid.NewGuid()};
        string history="1;"+string.Concat(new uint[]{1,8,10000,1200,50,uint.MaxValue,uint.MaxValue,0x10000,300}.Select(x=>x.ToString("X8")));
        Check(await Call(new{op="status",unlockHistory=history},false)==401&&await Call(new{op="status",unlockHistory=history},stale:true)==400&&!service.DiagnosticSummary.Contains("attempt=1; transport=USB"),"history bypasses authentication or freshness");
        Check(await Call(new{op="status",unlockHistory=history})==200&&service.DiagnosticSummary.Contains("attempt=1; transport=USB; result=failure"),"authenticated history not retained");
        Check(await Call(new{op="status"})==200&&service.DiagnosticSummary.Contains("attempt=1; transport=USB; result=failure"),"ordinary status erased history");
        Check(await Call(new{op="status",unlockHistory="unbounded-text"})==400&&await Call(new{op="lock-screen",unlockHistory=history})==400,"history allowed unbounded text or mutation probe");
        Check(await Call(new{op="lock-screen"},false)==401&&await Call(new{op="lock-screen"},stale:true)==400&&
            await Call(new{op="lock-screen"},sessionOverride:"old-session")==400&&await Call(new{op="lock-screen"})==200&&backend.Calls==0,"lock-screen bypasses authenticated session or causes a side effect");
        Check(await Call(command,false)==401&&await Call(command,stale:true)==400&&await Call(command,sessionOverride:"old-session")==400&&backend.Calls==0,"unauthenticated/stale RPC invoked OS");
        Check(await Call(command)==200&&await Call(command)==200&&backend.Calls==1,"authenticated RPC duplicate invoked OS twice");
        Check(await Call(new{op="mute",muted=true,command.requestId})==409&&backend.Calls==1,"authenticated RPC conflict invoked OS");
        var launch=new{op="launch",launcherId=launchers.Load().Single().Id,requestId=Guid.NewGuid()};
        Check(await Call(launch)==200&&await Call(launch)==200&&backend.Calls==2&&backend.Launched==launchers.Load().Single().Path,"authenticated launcher did not resolve or deduplicate");
    }
}
