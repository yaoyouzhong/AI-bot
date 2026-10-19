using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;

// Paired Wi-Fi requests only carry intent/progress. The saved Windows PIN never
// leaves this process except as key events to the authenticated local service.
internal sealed class Tab5WifiUnlock : IDisposable
{
    private readonly Tab5PinStore store;
    private readonly Func<CancellationToken,Task<WifiKeyboardRfb>> connect;
    private readonly Func<ComputerLockScreenState> screen;
    private readonly Func<bool> enabled, console;
    private readonly Func<long> now;
    private readonly SemaphoreSlim gate=new(1);
    private readonly System.Threading.Timer timer;
    private readonly Dictionary<string,long> used=[];
    private WifiKeyboardRfb? keyboard;
    private Tab5Pairing? paired;
    private string owner="", request="", offer="";
    private byte[] pin=[];
    private int next,cursor,length,phase,wakes;
    private bool coverSent,entered,disposed;
    private long lease,deadline,prepareUntil,readySince,settleUntil,confirmUntil,startedAt,sendingAt;
    private string diagnostic="尚无请求";
    internal string Diagnostic=>Volatile.Read(ref diagnostic);
    internal Tab5WifiUnlock(Tab5PinStore store,Func<CancellationToken,Task<WifiKeyboardRfb>>? connect=null,
        Func<ComputerLockScreenState>? screen=null,Func<bool>? enabled=null,Func<bool>? console=null,Func<long>? clock=null)
    {
        this.store=store;this.connect=connect??WifiKeyboardRfb.ConnectAsync;
        this.screen=screen??WindowsLockScreen.Read;this.enabled=enabled??(()=>WifiKeyboardSettings.Configured);
        this.console=console??(()=>{using var process=Process.GetCurrentProcess();return process.SessionId==WTSGetActiveConsoleSessionId();});now=clock??(()=>Environment.TickCount64);
        timer=new(_=>{if(gate.Wait(0))try{Expire();}finally{gate.Release();}},null,250,250);
    }
    private void Clear()
    {
        CryptographicOperations.ZeroMemory(pin);pin=[];
        keyboard?.Dispose();keyboard=null;paired=null;owner=request=offer="";
        next=cursor=length=phase=wakes=0;coverSent=entered=false;readySince=0;
    }
    private void Expire(){if(keyboard is not null&&(now()>=lease||now()>=deadline)){Volatile.Write(ref diagnostic,"expired");Clear();}}
    private static string Text(JsonElement root,string name)=>root.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()!:"";
    private static (int Status,object Body) Error(string code)=>(200,new{ok=false,error=code,unlocked=false});
    private (int Status,object Body) Stop(string code){Volatile.Write(ref diagnostic,code);Clear();return Error(code);}
    private (int Status,object Body) Progress(string stage){lease=now()+2500;return(200,new{ok=true,offer,next,stage,unlocked=false});}
    internal async Task<(int Status,object Body)> HandleAsync(JsonElement root,Tab5Pairing pair,string transport,string session,CancellationToken token)
    {
        if(transport!="Wi-Fi")return(409,new{error="unlock_requires_wifi"});
        if(root.ValueKind!=JsonValueKind.Object)return Error("invalid_unlock_request");
        string[] allowed=["kind","op","requestId","offer","next","session","issuedAt","binaryReply","bulkReply"];
        if(root.EnumerateObject().Any(p=>!allowed.Contains(p.Name))||root.EnumerateObject().GroupBy(p=>p.Name).Any(g=>g.Count()!=1))return Error("invalid_unlock_fields");
        string op=Text(root,"op"),id=Text(root,"requestId");
        if(op is not ("begin" or "poll" or "cancel")||!Guid.TryParseExact(id,"D",out var requestGuid))return Error("invalid_unlock_request");
        id=requestGuid.ToString("D");
        await gate.WaitAsync(token);
        try{
            if(disposed)return Error("unlock_stopped");
            Expire();string identity=pair.DeviceId+"|"+session;
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(2000);
            if(op=="begin"){
                foreach(var old in used.Where(p=>now()-p.Value>120000).Select(p=>p.Key).ToArray())used.Remove(old);
                if(used.Count>=256||!used.TryAdd(identity+"|"+id,now()))return Error("unlock_request_consumed");
                if(keyboard is not null)return Error("unlock_busy");
                if(!console()||screen().Locked!=true)return Error("session_not_locked");
                if(!enabled())return Error("wifi_unlock_disabled");
                if(!store.IsEnabled(pair))return Error("pin_not_configured");
                long started=now();keyboard=await connect(timeout.Token);
                // Recheck after the asynchronous connection, before retaining intent.
                if(!console()||!enabled()||screen().Locked!=true)return Stop("session_changed");
                paired=pair;owner=identity;request=id;
                offer=Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
                deadline=now()+40000;prepareUntil=now()+12000;readySince=0;startedAt=started;sendingAt=0;
                Volatile.Write(ref diagnostic,"preparing");
                return Progress("preparing");
            }
            if(keyboard is null||paired!=pair||owner!=identity||request!=id||Text(root,"offer")!=offer)return Error("unlock_offer_changed");
            if(op=="cancel"){Volatile.Write(ref diagnostic,"cancelled");Clear();return(200,new{ok=true,cancelled=true,unlocked=false});}
            if(!root.TryGetProperty("next",out var number)||number.ValueKind!=JsonValueKind.Number||!number.TryGetInt32(out int index)||index!=next)return Stop("unlock_step_consumed");
            next++; // Consume before any key or credential read; never replay an uncertain step.
            if(!console()||!enabled()||!store.Enabled(pair))return Stop("session_changed");
            var state=screen();
            if(state.Locked==false){
                bool completed=phase>0&&cursor==length&&length>=4;
                if(completed)Volatile.Write(ref diagnostic,$"unlocked; totalMs={now()-startedAt}; preparingMs={sendingAt-startedAt}; inputAndConfirmationMs={now()-sendingAt}");
                Clear();return completed?(200,new{ok=true,unlocked=true,stage="unlocked"}):Error("session_changed");
            }
            if(state.Locked!=true)return Stop("session_changed");
            if(now()<settleUntil)return Progress("preparing");
            if(phase==0){
                if(now()>=prepareUntil)return Stop("screen_not_ready");
                if(state.DisplayState==0){
                    readySince=0;
                    if(wakes>=2)return Progress("preparing");
                    wakes++;await keyboard.WakeAsync(false,timeout.Token);settleUntil=now()+1500;
                    return Progress("preparing");
                }
                if(state.DisplayState is not (1 or 2)){readySince=0;return Progress("preparing");}
                if(state.LockScreenCover==true){
                    readySince=0;
                    if(!coverSent){coverSent=true;await keyboard.WakeAsync(true,timeout.Token);settleUntil=now()+350;}
                    return Progress("preparing");
                }
                // Require stable, fresh locked/on observations instead of an
                // unconditional six-second pause. Unknown/off/cover resets it.
                if(readySince==0)readySince=now();
                if(now()-readySince<500)return Progress("preparing");
                // The detector does not prove keyboard focus or the login method.
                // Users must keep their normal numeric PIN method selected.
                pin=store.Read(pair,requireEnabled:true)??[];
                if(!Tab5PinStore.Valid(pin))return Stop("pin_not_configured");
                length=pin.Length;phase=1;sendingAt=now();return Progress("sending");
            }
            if(phase==1){
                if(state.DisplayState is not (1 or 2)||state.LockScreenCover==true)return Stop("screen_changed");
                if(cursor<length){
                    byte digit=pin[cursor];pin[cursor++]=0;
                    await keyboard.PressAsync(digit,timeout.Token);
                    if(cursor==length){CryptographicOperations.ZeroMemory(pin);pin=[];settleUntil=now()+350;}
                    return Progress("sending");
                }
                if(entered)return Stop("unlock_step_consumed");
                entered=true;phase=2;confirmUntil=now()+10000;
                await keyboard.PressAsync(0xff0d,timeout.Token);
                return Progress("confirming");
            }
            if(now()>=confirmUntil)return Stop("unlock_not_confirmed");
            return Progress("confirming");
        }catch(Exception ex) when(ex is IOException or System.Net.Sockets.SocketException or OperationCanceledException or CryptographicException or UnauthorizedAccessException or FormatException){return Stop("unlock_stopped_no_retry");}
        finally{gate.Release();}
    }
    public void Dispose(){timer.Dispose();gate.Wait();try{disposed=true;Clear();}finally{gate.Release();}}
    [DllImport("kernel32.dll")]private static extern uint WTSGetActiveConsoleSessionId();
}
