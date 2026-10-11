using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5CodexReadStateSelfTest
{
    internal static void Run()
    {
        void Check(bool value,string scenario) { if(!value)throw new InvalidOperationException("Codex read sync: "+scenario); }
        string task=Guid.NewGuid().ToString(),other=Guid.NewGuid().ToString();
        long now=0;Tab5CodexReadState.Snapshot? source=new("account-a",[task]);
        var sync=new Tab5CodexReadState(()=>source,()=>now);
        var ready=new Dictionary<string,string>{{task,"turn-one"},{other,"other-turn"}};
        Check(sync.Synchronize(ready).Count==0,"initial persistence settle");
        Check(sync.Unread.GetValueOrDefault(task)=="turn-one"&&!sync.Unread.ContainsKey(other),"desktop unread baseline");
        now=5000;var ack=sync.Synchronize(ready);
        Check(!ack.ContainsKey(task)&&ack.GetValueOrDefault(other)=="other-turn","unread retained, read acknowledged");
        source=new("account-a",[]);now=10000;ack=sync.Synchronize(ready);
        Check(ack.GetValueOrDefault(task)=="turn-one","desktop read exact turn");
        ready[task]="turn-two";now=10001;ack=sync.Synchronize(ready);
        Check(!ack.ContainsKey(task),"old acknowledgement not inherited");
        source=new("account-a",[task]);now=15001;ack=sync.Synchronize(ready);
        Check(!ack.ContainsKey(task),"new unread write retained");
        source=null;now=21000;Check(sync.Synchronize(ready).Count==0,"unavailable not read");
        Check(sync.Unread.Count==0,"unavailable not unread");
        source=new("account-b",[]);Check(sync.Synchronize(ready).Count==0,"account switch resets");
        now=26000;Check(sync.Synchronize(ready)[task]=="turn-two","reconnect current turn");

        string Hash(object o)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(o))).ToLowerInvariant();
        string identity=Hash(new[]{"chatgpt","account","user"}),host="local:"+Hash(new object?[]{"local","local",null});
        string Claims(long expiry)=>Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Dictionary<string,object>{
            ["exp"]=expiry,["https://api.openai.com/auth"]=new{chatgpt_account_id="account",user_id="user"}}))).TrimEnd('=').Replace('+','-').Replace('/','_');
        string Auth(long expiry)=>JsonSerializer.Serialize(new{tokens=new{access_token="test."+Claims(expiry)+".test"}});
        string State(string id,string h,int version=1)=>JsonSerializer.Serialize(new Dictionary<string,object>{["electron-thread-read-state-v1"]=new{
            version,unreadByIdentity=new Dictionary<string,object>{{id,new Dictionary<string,object>{{h,new[]{task}}}}}}});
        string auth=Auth(DateTimeOffset.UtcNow.ToUnixTimeSeconds()+3600);
        Check(Tab5CodexReadState.Parse(auth,State(identity,host))?.Unread.SetEquals([task])==true,"current account local host");
        Check(Tab5CodexReadState.Parse(auth,State("other-account",host)) is null,"other account rejected");
        Check(Tab5CodexReadState.Parse(auth,State(identity,"local:other-host")) is null,"other host rejected");
        Check(Tab5CodexReadState.Parse(auth,State(identity,host,2)) is null,"unknown schema rejected");
        Check(Tab5CodexReadState.Parse(Auth(1),State(identity,host)) is null,"expired identity rejected");
        Console.WriteLine("CODEX_DESKTOP_READ_STATE_OK identity, host, expiry, settle, desktop_read, new_turn, disconnect, account_switch");
    }
}
