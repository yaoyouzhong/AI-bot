using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO.Compression;

namespace AIBotBridge;

internal static class Tab5HostBindingSelfTest
{
    internal static void Run() {
        int checks=0;
        void Check(bool value){checks++;if(!value)throw new InvalidOperationException("Host binding assertion "+checks);}
        var pair=new Tab5Pairing("e8f60ae2ec56","USB\\VID_303A&PID_4005\\TEST",Convert.ToBase64String(Enumerable.Repeat((byte)0x11,32).ToArray()));
        var other=pair with{Key=Convert.ToBase64String(Enumerable.Repeat((byte)0x22,32).ToArray())};
        const string nonce="0123456789abcdef0123456789abcdef";
        string original=pair.Key,id=Tab5HostBinding.Id(pair);
        Check(id.Length==32&&Tab5Protocol.ValidNonce(id));Check(id!=Tab5HostBinding.Id(other));Check(pair.Key==original);
        Check(id=="246dd3f8363795589894eb1dc61b2e34");
        using var claim=JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(Tab5HostBinding.Claim(pair,nonce,"192.168.1.20",18765,"Windows")));
        var r=claim.RootElement;
        Check(r.GetProperty("hostId").GetString()==id);
        Check(Tab5Protocol.Verify(Convert.FromBase64String(pair.Key),$"TAB5_HOST_V1|{pair.DeviceId}|{id}|{nonce}|192.168.1.20|18765|Windows",r.GetProperty("proof").GetString()));
        Check(!Tab5Protocol.Verify(Convert.FromBase64String(pair.Key),$"TAB5_HOST_V1|{pair.DeviceId}|{id}|{nonce}|192.168.1.21|18765|Windows",r.GetProperty("proof").GetString()));
        using var ack=JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(new{deviceId=pair.DeviceId,hostId=id,proof=Tab5Protocol.Proof(Convert.FromBase64String(pair.Key),$"TAB5_HOST_ACK_V1|{pair.DeviceId}|{id}|{nonce}")}));
        Tab5HostBinding.Confirm(pair,nonce,ack.RootElement);checks++;
        foreach(var rejected in new[]{other,pair with{DeviceId="e8f60ae2ec57"}}){try{Tab5HostBinding.Confirm(rejected,nonce,ack.RootElement);throw new InvalidOperationException("Foreign acknowledgement accepted");}catch(IOException){checks++;}}
        try{Tab5HostBinding.Confirm(pair,new string('a',32),ack.RootElement);throw new InvalidOperationException("Stale acknowledgement accepted");}catch(IOException){checks++;}
        using(var busy=JsonDocument.Parse("{\"error\":\"device_busy\"}")){try{Tab5HostBinding.Confirm(pair,nonce,busy.RootElement);throw new InvalidOperationException("Owner changed during firmware installation");}catch(IOException ex) when(ex.Message.Contains("正在升级")){checks++;}}
        foreach(var bad in new[]{"","line\nbreak","bad|name",new string('a',64)}){try{Tab5HostBinding.Claim(pair,nonce,"",0,bad);throw new InvalidOperationException("Invalid name accepted");}catch(IOException){checks++;}}
        string name=Tab5HostBinding.Name(string.Concat(Enumerable.Repeat("电脑😀",30)));
        Check(Encoding.UTF8.GetByteCount(name)<=63&&!name.EndsWith('\ud83d'));
        Check(Tab5HostBinding.Name("a|b\nc")=="a b c");
        // Pairing is untouched, so the existing DPAPI PIN binding remains valid.
        var pinStore=new Tab5PinStore();pinStore.Save(pair,"x"u8);pinStore.SetEnabled(pair,true);
        _=Tab5HostBinding.Claim(pair,nonce,"",0,"Windows");var pin=pinStore.Read(pair,true);
        try{Check(pin is not null&&pin.AsSpan().SequenceEqual("x"u8));}finally{if(pin is not null)CryptographicOperations.ZeroMemory(pin);}
        Check(!Tab5HostBinding.NeedsFullStatus(false,12000,0));
        Check(Tab5HostBinding.NeedsFullStatus(true,3000,4000));
        var state=new Tab5UsbState();long fullAck=0,deviceLeaseSeen=0;int deltas=0;
        string catalog=string.Join(',',Enumerable.Range(0,500).Select(i=>(i*16777619L).ToString("X")));
        for(int step=0;step<13;step++) {
            long now=100000+step*2000;
            byte[] frame=JsonSerializer.SerializeToUtf8Bytes(new{version=1,type="tab5_status",deviceId=pair.DeviceId,session=nonce,sequence=step+1,data=new{codex=new{},claude=new{},catalog,counter=step}});
            bool full=Tab5HostBinding.NeedsFullStatus(true,now,fullAck);
            byte[] wire=state.Wire(frame,true,!full);
            using var envelope=JsonDocument.Parse(wire);var root=envelope.RootElement;JsonDocument? unpacked=null;
            try {
                if(root.GetProperty("type").GetString()=="tab5_packed") {
                    using var input=new MemoryStream(Convert.FromBase64String(root.GetProperty("payload").GetString()!));
                    using var inflater=new ZLibStream(input,CompressionMode.Decompress);using var output=new MemoryStream();inflater.CopyTo(output);
                    unpacked=JsonDocument.Parse(output.ToArray());root=unpacked.RootElement;
                }
                if(root.GetProperty("type").GetString()=="tab5_status")deviceLeaseSeen=now;else deltas++;
                Check(now-deviceLeaseSeen<8000);
                if(full){Check(root.GetProperty("type").GetString()=="tab5_status");fullAck=now;}
                state.Confirm(frame,wire);
            }finally{unpacked?.Dispose();}
        }
        Check(deltas>0);
        Console.WriteLine($"TAB5_HOST_BINDING_SELF_TEST_OK checks={checks}; identity={id}; keys_preserved; endpoint_and_nonce_bound; foreign_ack_rejected; one_character_credential_preserved; full_status_renews_USB_lease_with_deltas");
    }
}
