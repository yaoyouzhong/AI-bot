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
    internal sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now=new(2026,11,21,0,0,0,TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow()=>_now;
        internal void Advance(TimeSpan elapsed)=>_now+=elapsed;
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
    internal static object Release(string component,string version,string name,byte[] package,string? url=null,string checksumName="SHA256SUMS.txt",string? productTag=null) {
        string tag=productTag??component+"-v"+version;
        string download=$"https://github.com/yaoyouzhong/AI-bot/releases/download/{tag}/";
        return new {tag_name=tag,draft=false,prerelease=false,body="## 更新\n- 示例更新说明",
            assets=new[]{new{name,browser_download_url=url??download+name,size=package.Length},
                new{name=checksumName,browser_download_url=download+checksumName,size=100}}};
    }
    internal static async Task RunAsync() {
        ComponentUpdateSelfTest.Run();
        await CheckCadenceAsync();
        byte[] image=new byte[2048];image[0]=0xe9;
        byte[] zip=Zip(new(){["VERSION"]="0.6.0\n"u8.ToArray(),["firmware.bin"]=image,["COMPONENT.json"]="{\"component\":\"esp8266\",\"version\":\"0.6.0\",\"protocolVersion\":1}"u8.ToArray()});
        string name="AI-bot-esp8266-0.6.0-flash.zip",hash=Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant();
        string json=JsonSerializer.Serialize(new[]{Release("esp8266","0.6.0",name,zip,checksumName:name+".sha256",productTag:"v0.6.26")});
        var handler=new Handler();bool corrupt=false,fail=false;int requests=0;
        handler.Reply=(request,token)=>{requests++;string path=request.RequestUri!.AbsolutePath;
            if(fail)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            byte[] bytes=path.EndsWith(name+".sha256")?Encoding.UTF8.GetBytes(hash+"  "+name+"\n"):path.EndsWith(".zip")?(corrupt?zip.Select((b,i)=>i==0?(byte)(b^1):b).ToArray():zip):Encoding.UTF8.GetBytes(json);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)});};
        using var service=new UpdateService(handler);await service.CheckAsync(default);
        var release=service.Available["esp8266"];
        Check(release.Package?.Name==name&&release.Checksums?.Name==name+".sha256","Compact ESP package did not bind to its checksum");
        var device=new UpdateDevice("esp","小屏","0.5.0","在线",true,"flash","USB");
        Check(UpdateService.Blocked(device,release) is null,"New ESP update unavailable");
        Check(UpdateService.Blocked(device with{Version="0.6.0"},release) is not null,"Equal version allowed");
        Check(UpdateService.Blocked(device with{Version="0.7.0"},release) is not null,"Downgrade allowed");
        Check(UpdateService.Blocked(device with{Version="待连接读取"},release) is not null,"Unknown version allowed");
        Check(UpdateService.Blocked(device with{Enabled=false},release) is not null,"Disabled target allowed");
        var ready=await service.DownloadAsync(release,"0.5.1",null,default);ready.VerifyUnchanged();
        Check(ready.Tab5 is null&&File.ReadAllBytes(ready.File).SequenceEqual(zip),"Download changed package");
        string root=Path.GetDirectoryName(Path.GetDirectoryName(ready.File))!;
        int before=Directory.GetDirectories(root).Length;File.Delete(ready.File);corrupt=true;
        await Reject(async()=>await service.DownloadAsync(release,"0.5.1",null,default));
        Check(Directory.GetDirectories(root).Length==before,"Corrupt download changed cache directory count");corrupt=false;
        using(var stop=new CancellationTokenSource()){stop.Cancel();await Reject(async()=>await service.DownloadAsync(release,"0.5.1",null,stop.Token));}
        fail=true;await Reject(()=>service.CheckAsync(default));Check(service.Available["esp8266"]==release&&service.Error is not null,"Failure replaced last good catalog");fail=false;
        ready=await service.DownloadAsync(release,"0.5.1",null,default);
        File.AppendAllText(ready.File,"changed");await Reject(()=>{ready.VerifyUnchanged();return Task.CompletedTask;});
        await Reject(()=>{UpdateService.ReadChecksum(hash+"  "+name+"\n"+hash+"  "+name,name);return Task.CompletedTask;});
        using(var unsafeDoc=JsonDocument.Parse(JsonSerializer.Serialize(new[]{Release("esp8266","0.6.0",name,zip,"https://example.org/firmware.zip",name+".sha256")})))
            Check(!ComponentUpdateCatalog.Read(unsafeDoc.RootElement).ContainsKey("esp8266"),"Untrusted compact package URL accepted");
        string path=Path.Combine(Path.GetDirectoryName(ready.File)!,"wrong.zip");
        var incompatible=Zip(new(){["VERSION"]="0.6.0"u8.ToArray(),["firmware.bin"]=image,["COMPONENT.json"]="{\"component\":\"esp8266\",\"version\":\"0.6.0\",\"protocolVersion\":1,\"minimumBridgeVersion\":\"9.0.0\"}"u8.ToArray()});
        File.WriteAllBytes(path,incompatible);await Reject(()=>{UpdateService.ValidatePackage(release,path,Convert.ToHexString(SHA256.HashData(incompatible)),"0.5.1");return Task.CompletedTask;});
        var preferences=new UpdatePreferences();var pending=UpdateReminder.Pending([device],service.Available,preferences);Check(pending.Length==1,"Missing reminder");
        UpdateReminder.Remember(preferences,pending);Check(UpdateReminder.Pending([device],service.Available,UpdateReminder.Read()).Length==0,"Reminder repeated after reload");
        handler.Reply=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect){Headers={Location=new Uri("https://example.org/redirect")}});
        await Reject(async()=>await service.DownloadAsync(release,"0.5.1",null,default));
        Console.WriteLine("UNIFIED_UPDATE_OK metadata, trusted URLs, exact hash/download, cancellation, corruption cleanup, stale catalog, device/version gates, minimum bridge, reminder persistence; no device write");
    }

    private static async Task CheckCadenceAsync()
    {
        var clock=new Clock();
        var handler=new Handler();
        var release=new byte[]{1};
        const string name="AI-bot-bridge-v0.6.27-windows-x64-setup.exe";
        var published=JsonSerializer.Serialize(new[]{Release("bridge","0.6.27",name,release,productTag:"v0.6.27")});
        bool offline=false,newVersion=false;
        int requests=0;
        var firstResponse=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.Reply=async(_,token)=>{
            requests++;
            await firstResponse.Task.WaitAsync(token);
            return new HttpResponseMessage(offline?HttpStatusCode.ServiceUnavailable:HttpStatusCode.OK)
                {Content=new StringContent(newVersion?published:"[]")};
        };
        using var service=new UpdateService(handler,clock);
        var background=service.CheckAsync(default,UpdateCheckTrigger.Background);
        var foreground=service.CheckAsync(default,UpdateCheckTrigger.Foreground);
        firstResponse.SetResult();
        await Task.WhenAll(background,foreground);
        Check(requests==1,"Opening the page during a background check should share the resulting catalog");
        clock.Advance(TimeSpan.FromMinutes(5)-TimeSpan.FromSeconds(1));
        await service.CheckAsync(default,UpdateCheckTrigger.Foreground);
        Check(requests==1,"Opening the page should reuse a fresh catalog");
        clock.Advance(TimeSpan.FromSeconds(1));
        Check(service.IsCheckDue(UpdateCheckTrigger.Foreground)&&!service.IsCheckDue(UpdateCheckTrigger.Background),
            "Opening the page must refresh before the next background check");
        await service.CheckAsync(default,UpdateCheckTrigger.Foreground);
        Check(requests==2,"Opening the page with an old catalog must query GitHub");
        clock.Advance(TimeSpan.FromMinutes(59));
        await service.CheckAsync(default,UpdateCheckTrigger.Background);
        Check(requests==2,"Background should wait one hour from the latest successful check");
        clock.Advance(TimeSpan.FromMinutes(1));
        offline=true;
        await Reject(()=>service.CheckAsync(default,UpdateCheckTrigger.Background));
        Check(requests==3&&service.Error is not null&&!service.IsCheckDue(UpdateCheckTrigger.Foreground),
            "An offline attempt must back off without discarding the last successful result");
        clock.Advance(TimeSpan.FromMinutes(1));
        Check(service.IsCheckDue(UpdateCheckTrigger.Foreground)&&!service.IsCheckDue(UpdateCheckTrigger.Background),
            "Reopening the page must allow earlier recovery from a failed background check");
        clock.Advance(TimeSpan.FromMinutes(14));
        offline=false;
        newVersion=true;
        await service.CheckAsync(default,UpdateCheckTrigger.Background);
        var installed=new UpdateDevice("bridge","AI-bot","0.6.26","运行中",true,"bridge","打开安装程序");
        Check(requests==4&&service.Error is null&&UpdateReminder.Pending([installed],service.Available,new()).SequenceEqual(["bridge:0.6.27"]),
            "Background must recover after fifteen minutes and report the new release");
        await service.CheckAsync(default);
        Check(requests==5,"Manual check must bypass the cache");
        Console.WriteLine("UPDATE_CHECK_CADENCE_OK cache, hourly background, offline retry, manual refresh");
    }
}
