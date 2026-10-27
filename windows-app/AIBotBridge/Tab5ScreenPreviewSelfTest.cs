using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5ScreenPreviewSelfTest
{
    internal static int Run() {
        int checks=0;
        void Check(bool value,string name){if(!value)throw new InvalidOperationException("Screen preview: "+name);checks++;}
        Check(!Tab5ScreenPreview.SupportsResponsiveCapture(null)&&!Tab5ScreenPreview.SupportsResponsiveCapture("0.2.276-ui")&&
            !Tab5ScreenPreview.SupportsResponsiveCapture("0.2.277.0-ui")&&Tab5ScreenPreview.SupportsResponsiveCapture("0.2.277-ui"),"responsive capability version gate");
        foreach(string transport in new[]{"USB","Wi-Fi","BLE"})foreach(bool responsive in new[]{false,true})foreach(bool unchanged in new[]{false,true}) {
            var c=new Tab5ScreenPreview.Context("fixture","host",[transport],Responsive:responsive);
            Check(Tab5ScreenPreview.CaptureCooldown(c,transport,unchanged)==(responsive&&transport!="BLE"?100:unchanged?2000:1000),"bounded transport-specific cadence");
        }
        byte[] wire=[2,0,0,0xf8,0xe0,7,4,0x80,0x1f,0],pixels=[0,0xf8,0xe0,7,0x1f,0,0x1f,0,0x1f,0,0x1f,0];
        Check(Tab5ScreenPreview.Decode(wire,3,2)?.SequenceEqual(pixels)==true,"C/Swift shared wire vector");
        foreach(byte[] invalid in new byte[][]{[0,0],[7,0x80,0,0],[1,0,0],[5,0x80,0,0],wire.Concat(new byte[]{1}).ToArray()})
            Check(Tab5ScreenPreview.Decode(invalid,3,2) is null,"malformed/overflow/trailing data");
        Check(Tab5ScreenPreview.Decode(wire,641,1) is null,"dimension limit");
        long now=10000;var model=new Tab5ScreenPreview(()=>now,()=>1000000);
        var context=new Tab5ScreenPreview.Context("fixture","host-a",["USB","Wi-Fi"]);
        var token=model.Start(context);model.Renew(token,context);
        JsonElement Command()=>JsonSerializer.SerializeToElement(model.Command);
        var command=Command();string id=command.GetProperty("requestId").GetString()!;
        Check(command.GetProperty("transport").GetString()=="USB","USB priority");
        var root=new Dictionary<string,object>{{"streamId",token},{"requestId",id},{"op","begin"}};
        int Receive(Dictionary<string,object> value,byte[] bytes,Tab5ScreenPreview.Context? current=null,string device="fixture",string transport="USB")=>
            model.Receive(JsonSerializer.SerializeToElement(value),bytes,current??context,device,transport).Status;
        Check(Receive(root,[])==200,"active viewer begin");
        var encoded=new List<byte>();int remaining=640*360;
        while(remaining>0){int n=Math.Min(remaining,32767),control=n|32768;encoded.AddRange([(byte)control,(byte)(control>>8),0xe0,7]);remaining-=n;}
        byte[] data=encoded.ToArray();int split=data.Length/2;
        root["op"]="frame";root["width"]=640;root["height"]=360;root["encoding"]="rgb565-rle1";root["size"]=data.Length;
        root["sha256"]=Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();root["offset"]=0;
        Check(Receive(root,data[..split])==200&&model.Snapshot(token).Frame is null,"partial never published");
        Check(Receive(root,data[..split])==200,"identical retry");
        var bad=new Dictionary<string,object>(root);bad["width"]=true;
        Check(Receive(bad,data[..split])==400,"boolean dimensions rejected");
        bad=new(root);bad["offset"]=split+1;
        Check(Receive(bad,data[(split+1)..])==409,"gap rejected");
        byte[] changed=data[..split];changed[0]^=1;
        Check(Receive(root,changed)==409,"changed prefix rejected");
        Check(Receive(root,data[..split],device:"foreign")==409,"foreign device rejected");
        Check(Receive(root,data[..split],transport:"Wi-Fi")==409,"other channel rejected");
        Check(Receive(root,data[..split],context with {Host="host-b"})==409,"other host rejected");
        root["offset"]=split;
        Check(Receive(root,data[split..])==200,"complete frame accepted");
        var good=model.Snapshot(token).Frame;
        Check(good?.Pixels.Length==640*360*2&&good.Pixels[0]==0xe0&&good.Pixels[1]==7,"verified pixels only");
        Check(Receive(root,data[split..])==409,"completed request rejected");
        now+=1000;model.Renew(token,context);root["requestId"]=Command().GetProperty("requestId").GetString()!;root["offset"]=0;
        root["sha256"]=new string('0',64);
        Check(Receive(root,data)==400&&ReferenceEquals(good,model.Snapshot(token).Frame),"bad checksum preserves last good frame");
        now+=6000;Check(model.Command is null&&model.Snapshot(token).Frame is null,"viewer lease expires");
        model.Renew(token,context);Check(model.Command is not null,"fresh request after lease");
        var wifi=context with {Transports=["Wi-Fi"]};model.Renew(token,wifi);now+=1100;model.Renew(token,wifi);
        Check(Command().GetProperty("transport").GetString()=="Wi-Fi","channel change creates new request");
        string newer=model.Start(wifi);model.Renew(newer,wifi);model.Stop(token);
        Check(model.Command is not null,"old window cannot stop replacement");
        model.Renew(newer,wifi with {Available=false});Check(model.Command is null,"disabled stops request");
        model.Renew(newer,wifi with {Host="replacement"});Check(model.Command is null&&model.Snapshot(newer).Frame is null,"ownership clears image");
        model.Stop();Check(model.Command is null,"close clears request");
        Check(Receive(root,data)==409,"late frame rejected");
        Check(!Tab5ScreenPreview.SupportsHD("0.2.269-ui")&&Tab5ScreenPreview.SupportsHD("0.2.270-ui")&&!Tab5ScreenPreview.SupportsHD("0.2.270.0-ui"),"HD version negotiation");
        token=model.Start(context);model.Renew(token,context);
        var bounded=JsonSerializer.SerializeToElement(model.CommandForFrame(999000));
        Check(bounded.GetProperty("expiresAt").GetInt64()==1029000,"snapshot clock narrows thirty-second lease");
        string stalled=Command().GetProperty("requestId").GetString()!;
        for(int i=0;i<6;i++){now+=1000;model.Renew(token,context);}
        Check(model.Command is null,"idle request abandoned before thirty seconds");
        now+=1000;model.Renew(token,context);Check(Command().GetProperty("requestId").GetString()!=stalled,"stalled worker replaced");
        var v2=context with{Transports=["BLE"],HighDefinition=true};token=model.Start(v2);model.Renew(token,v2);
        var v2Command=Command();Check(v2Command.GetProperty("version").GetInt32()==2&&v2Command.GetProperty("encoding").GetString()=="rgb565-rle1","BLE version2 preserves bounded lossless format");
        id=v2Command.GetProperty("requestId").GetString()!;
        root=new(){{"streamId",token},{"requestId",id},{"op","begin"},{"encoding","rgb565-rle1"},{"width",320},{"height",180}};
        int V2()=>model.Receive(JsonSerializer.SerializeToElement(root),[],v2,"fixture","BLE").Status;
        Check(V2()==200,"version2 geometry begin");root["op"]="unchanged";root["sha256"]=new string('0',64);
        Check(V2()==409,"no phantom unchanged first image");
        encoded.Clear();remaining=320*180;while(remaining>0){int n=Math.Min(remaining,32767),control=n|32768;encoded.AddRange([(byte)control,(byte)(control>>8),0xe0,7]);remaining-=n;}
        data=encoded.ToArray();root["op"]="frame";root["size"]=data.Length;root["offset"]=0;root["sha256"]=Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        Check(model.Receive(JsonSerializer.SerializeToElement(root),data,v2,"fixture","BLE").Status==200,"version2 complete frame");
        var retained=model.Snapshot(token).Frame!;now+=1000;model.Renew(token,v2);root["requestId"]=Command().GetProperty("requestId").GetString()!;
        root["op"]="begin";Check(V2()==200,"unchanged request begins");root["op"]="unchanged";
        Check(V2()==200&&ReferenceEquals(retained.Pixels,model.Snapshot(token).Frame!.Pixels),"unchanged proof reuses pixels without upload");
        model.Stop();Check(V2()==409,"late unchanged cannot revive closed viewer");
        var hd=context with{HighDefinition=true};token=model.Start(hd);model.Renew(token,hd);
        Check(Command().GetProperty("encoding").GetString()=="jpeg","new stream first requests HD");
        int MemoryError() {
            var error=new{op="error",error="memory",streamId=token,requestId=Command().GetProperty("requestId").GetString()};
            return model.Receive(JsonSerializer.SerializeToElement(error),[],hd,"fixture","USB").Status;
        }
        Check(MemoryError()==200,"HD memory rejection accepted");
        now+=1100;model.Renew(token,hd);var fallback=Command();
        Check(fallback.GetProperty("version").GetInt32()==2&&fallback.GetProperty("encoding").GetString()=="rgb565-rle1"&&fallback.GetProperty("width").GetInt32()==640,"memory fallback uses existing270 wire");
        Check(model.Snapshot(token).Note.Contains("标准画质"),"fallback quality explicit");
        Check(MemoryError()==200,"second memory rejection accepted");
        now+=1100;model.Renew(token,hd);fallback=Command();
        Check(fallback.GetProperty("width").GetInt32()==320&&fallback.GetProperty("height").GetInt32()==180,"bounded minimum preview");
        model.Stop();Check(model.Command is null,"closed fallback cannot publish");
        token=model.Start(hd);model.Renew(token,hd);
        string preparing=Command().GetProperty("requestId").GetString()!;
        for(int i=0;i<10;i++){now+=1000;model.Renew(token,hd);}
        Check(Command().GetProperty("requestId").GetString()==preparing,"HD preparation outlives five-second idle threshold");
        for(int i=0;i<10;i++) {
            var beat=new{op="begin",streamId=token,requestId=preparing,width=1280,height=720,encoding="jpeg"};
            Check(model.Receive(JsonSerializer.SerializeToElement(beat),[],hd,"fixture","USB").Status==200,"preparation heartbeat accepted");
            now+=1000;model.Renew(token,hd);
        }
        for(int i=0;i<11;i++){now+=1000;model.Renew(token,hd);}
        Check(model.Command is null||Command().GetProperty("requestId").GetString()!=preparing,"heartbeat cannot extend absolute deadline");
        token=model.Start(hd);model.Renew(token,hd);Check(MemoryError()==200,"recovery starts at fallback");
        encoded.Clear();remaining=640*360;
        while(remaining>0){int n=Math.Min(remaining,32767),control=n|32768;encoded.AddRange([(byte)control,(byte)(control>>8),0xe0,7]);remaining-=n;}
        data=encoded.ToArray();int completed=0;bool restored=false;
        for(int i=0;i<36;i++) {
            now+=1000;model.Renew(token,hd);if(model.Command is null)continue;
            var cmd=Command();if(cmd.GetProperty("encoding").GetString()=="jpeg"){restored=true;break;}
            var packet=new{op="frame",streamId=token,requestId=cmd.GetProperty("requestId").GetString(),width=640,height=360,encoding="rgb565-rle1",size=data.Length,offset=0,sha256=Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant()};
            Check(model.Receive(JsonSerializer.SerializeToElement(packet),data,hd,"fixture","USB").Status==200,"fallback remains usable during recovery cooldown");completed++;
        }
        Check(restored&&completed>=3&&model.Snapshot(token).Frame?.Width==640,"bounded HD retry preserves last fallback frame");
        model.Stop();
        var fast=context with{Responsive=true};token=model.Start(fast);model.Renew(token,fast);
        var fastPacket=new{op="frame",streamId=token,requestId=Command().GetProperty("requestId").GetString(),width=640,height=360,encoding="rgb565-rle1",size=data.Length,offset=0,sha256=Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant()};
        Check(model.Receive(JsonSerializer.SerializeToElement(fastPacket),data,fast,"fixture","USB").Status==200,"responsive full frame accepted");
        now+=99;model.Renew(token,fast);Check(model.Command is null,"responsive wait remains bounded");
        now++;model.Renew(token,fast);Check(model.Command is not null,"responsive frame renews at 100ms");
        model.Stop();
        Console.WriteLine($"TAB5_SCREEN_PREVIEW_OK checks={checks}; synthetic protocol and pacing; no hardware timing claim");
        return checks;
    }
}
