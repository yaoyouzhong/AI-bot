using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;
internal static class UpdateSelfTest
{
    internal sealed class Handler : HttpMessageHandler
    {
        internal Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> Reply=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("[]")});
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Reply(request,token);
    }
    private static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
    private static async Task Reject(Func<Task> action) {
        try{await action();}catch(Exception ex) when(ex is IOException or InvalidDataException or ArgumentException or OperationCanceledException or HttpRequestException){return;}
        throw new InvalidOperationException("Invalid update was accepted");
    }
    internal static byte[] Zip(Dictionary<string,byte[]> entries) {
        using var memory=new MemoryStream();using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true))
            foreach(var pair in entries){using var stream=zip.CreateEntry(pair.Key).Open();stream.Write(pair.Value);}
        return memory.ToArray();
    }
    internal static object Release(string component,string version,string name,byte[] package,string? url=null)=>new {
        tag_name=component+"-v"+version,draft=false,prerelease=false,body="## 更新\n- 示例更新说明",
        assets=new[]{new{name,browser_download_url=url??$"https://github.com/yaoyouzhong/AI-bot/releases/download/{component}-v{version}/{name}",size=package.Length},
            new{name="SHA256SUMS.txt",browser_download_url=$"https://github.com/yaoyouzhong/AI-bot/releases/download/{component}-v{version}/SHA256SUMS.txt",size=0}}
    };
    internal static async Task RunAsync() {
        byte[] image=new byte[2048];image[0]=0xe9;
        byte[] zip=Zip(new(){["VERSION"]="0.6.0\n"u8.ToArray(),["firmware.bin"]=image,["COMPONENT.json"]="{\"component\":\"esp8266\",\"version\":\"0.6.0\",\"protocolVersion\":1}"u8.ToArray()});
        string name="AI-bot-0.6.0-firmware-materials.zip",hash=Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant();
        string json=JsonSerializer.Serialize(new[]{Release("esp8266","0.6.0",name,zip)});
        var handler=new Handler();bool corrupt=false,fail=false;int requests=0;
        handler.Reply=(request,token)=>{requests++;string path=request.RequestUri!.AbsolutePath;
            if(fail)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            byte[] bytes=path.EndsWith("SHA256SUMS.txt")?Encoding.UTF8.GetBytes(hash+"  "+name+"\n"):path.EndsWith(".zip")?(corrupt?zip.Select((b,i)=>i==0?(byte)(b^1):b).ToArray():zip):Encoding.UTF8.GetBytes(json);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)});};
        using var service=new UpdateService(handler);await service.CheckAsync(default);
        var release=service.Available["esp8266"];
        var device=new UpdateDevice("esp","小屏","0.5.0","在线",true,"flash","USB");
        Check(UpdateService.Blocked(device,release) is null,"New ESP update unavailable");
        Check(UpdateService.Blocked(device with{Version="0.6.0"},release) is not null,"Equal version allowed");
        Check(UpdateService.Blocked(device with{Version="0.7.0"},release) is not null,"Downgrade allowed");
        Check(UpdateService.Blocked(device with{Version="待连接读取"},release) is not null,"Unknown version allowed");
        Check(UpdateService.Blocked(device with{Enabled=false},release) is not null,"Disabled target allowed");
        var ready=await service.DownloadAsync(release,"0.5.1",null,default);ready.VerifyUnchanged();
        Check(ready.Tab5 is null&&File.ReadAllBytes(ready.File).SequenceEqual(zip),"Download changed package");
        string root=Path.GetDirectoryName(Path.GetDirectoryName(ready.File))!;
        int before=Directory.GetDirectories(root).Length;corrupt=true;
        await Reject(async()=>await service.DownloadAsync(release,"0.5.1",null,default));
        Check(Directory.GetDirectories(root).Length==before,"Corrupt partial package retained");corrupt=false;
        using(var stop=new CancellationTokenSource()){stop.Cancel();await Reject(async()=>await service.DownloadAsync(release,"0.5.1",null,stop.Token));}
        fail=true;await Reject(()=>service.CheckAsync(default));Check(service.Available["esp8266"]==release&&service.Error is not null,"Failure replaced last good catalog");fail=false;
        File.AppendAllText(ready.File,"changed");await Reject(()=>{ready.VerifyUnchanged();return Task.CompletedTask;});
        await Reject(()=>{UpdateService.ReadChecksum(hash+"  "+name+"\n"+hash+"  "+name,name);return Task.CompletedTask;});
        using(var unsafeDoc=JsonDocument.Parse(JsonSerializer.Serialize(new[]{Release("esp8266","0.6.0",name,zip,"https://example.org/firmware.zip")})))
            Check(ComponentUpdateCatalog.Read(unsafeDoc.RootElement)["esp8266"].Package is null,"Untrusted package URL accepted");
        string path=Path.Combine(Path.GetDirectoryName(ready.File)!,"wrong.zip");
        var incompatible=Zip(new(){["VERSION"]="0.6.0"u8.ToArray(),["firmware.bin"]=image,["COMPONENT.json"]="{\"component\":\"esp8266\",\"version\":\"0.6.0\",\"protocolVersion\":1,\"minimumBridgeVersion\":\"9.0.0\"}"u8.ToArray()});
        File.WriteAllBytes(path,incompatible);await Reject(()=>{UpdateService.ValidatePackage(release,path,Convert.ToHexString(SHA256.HashData(incompatible)),"0.5.1");return Task.CompletedTask;});
        var preferences=new UpdatePreferences();var pending=UpdateReminder.Pending([device],service.Available,preferences);Check(pending.Length==1,"Missing reminder");
        UpdateReminder.Remember(preferences,pending);Check(UpdateReminder.Pending([device],service.Available,UpdateReminder.Read()).Length==0,"Reminder repeated after reload");
        handler.Reply=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect){Headers={Location=new Uri("https://example.org/redirect")}});
        await Reject(async()=>await service.DownloadAsync(release,"0.5.1",null,default));
        Console.WriteLine("UNIFIED_UPDATE_OK metadata, trusted URLs, exact hash/download, cancellation, corruption cleanup, stale catalog, device/version gates, minimum bridge, reminder persistence; no device write");
    }
}
