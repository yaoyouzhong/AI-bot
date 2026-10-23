using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace AIBotBridge;

internal static class PackageResumeSelfTest
{
    private sealed class Progress(Action<int> action):IProgress<int> {public void Report(int value)=>action(value);}
    private static void Check(bool value,string why){if(!value)throw new InvalidOperationException(why);}
    internal static async Task RunAsync() {
        foreach(string component in new[]{"bridge","esp8266"}) {
            byte[] data=new byte[3*1024*1024];new Random(268).NextBytes(data);data[0]=0xe9;
            byte[] bytes=component=="bridge"?data:UpdateSelfTest.Zip(new(){["VERSION"]="0.6.8"u8.ToArray(),["firmware.bin"]=data,
                ["COMPONENT.json"]="{\"component\":\"esp8266\",\"version\":\"0.6.8\",\"protocolVersion\":1}"u8.ToArray()});
            if(component=="bridge"){bytes[0]=(byte)'M';bytes[1]=(byte)'Z';}
            string name=component=="bridge"?"AI-bot-bridge-v0.6.8-windows-x64-setup.exe":"AI-bot-esp8266-0.6.8-firmware.zip";
            var uri=new Uri("https://github.com/yaoyouzhong/AI-bot/releases/download/"+component+"-v0.6.8/");
            string hash=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var update=new ComponentUpdate(component,"0.6.8",new Version(0,6,8),"",new(name,new Uri(uri,name),bytes.Length),new("SHA256SUMS.txt",new Uri(uri,"SHA256SUMS.txt"),0));
            var ranges=new List<long>();bool ignore=false,badRange=false;int downloads=0;
            UpdateSelfTest.Handler Handler() {
                var h=new UpdateSelfTest.Handler();h.Reply=(request,token)=>{
                    if(request.RequestUri!.AbsolutePath.EndsWith("SHA256SUMS.txt"))return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(hash+"  "+name)});
                    downloads++;var range=request.Headers.Range?.Ranges.Single();
                    if(range is null||ignore)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)});
                    long from=range.From!.Value,to=range.To??bytes.Length-1;ranges.Add(from);
                    var content=new ByteArrayContent(bytes[(int)from..((int)to+1)]);
                    content.Headers.ContentRange=new ContentRangeHeaderValue(badRange?from+1:from,to,bytes.Length);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PartialContent){Content=content});
                };return h;
            }
            string? partial=null;
            async Task Interrupt() {
                using var stop=new CancellationTokenSource();using var service=new UpdateService(Handler());
                try{await service.DownloadAsync(update,"0.6.8",new Progress(v=>{if(v>=10)stop.Cancel();}),stop.Token);throw new InvalidOperationException("Cancellation ignored");}
                catch(OperationCanceledException) when(stop.IsCancellationRequested){}
                string root=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AI-bot","updates","package-downloads");
                partial=Directory.GetFiles(root,name+".part",SearchOption.AllDirectories).Single();
                Check(new FileInfo(partial).Length>0&&new FileInfo(partial).Length<bytes.Length,"Cancelled bytes not retained");
            }
            await Interrupt();long saved=new FileInfo(partial!).Length;
            badRange=true;
            using(var service=new UpdateService(Handler())) {
                try{await service.DownloadAsync(update,"0.6.8",null,default);throw new InvalidOperationException("Invalid range accepted");}catch(InvalidDataException){}
            }
            Check(new FileInfo(partial!).Length==saved,"Rejected range damaged prefix");badRange=false;ranges.Clear();
            PreparedUpdate ready;
            using(var service=new UpdateService(Handler()))ready=await service.DownloadAsync(update,"0.6.8",null,default);
            Check(ranges.First()==saved,"New service did not resume exact offset");ready.VerifyUnchanged();
            Check(File.ReadAllBytes(ready.File).SequenceEqual(bytes),"Resumed package bytes changed");
            int prior=downloads;
            using(var service=new UpdateService(Handler()))await service.DownloadAsync(update,"0.6.8",null,default);
            Check(downloads==prior,"Validated completed cache was downloaded again");
            File.Delete(ready.File);await Interrupt();ignore=true;
            using(var service=new UpdateService(Handler()))ready=await service.DownloadAsync(update,"0.6.8",null,default);
            ready.VerifyUnchanged();Check(new FileInfo(ready.File).Length==bytes.Length,"Ignored Range appended duplicate bytes");
        }
        Console.WriteLine("PACKAGE_RESUME_OK bridge/firmware cancel, new-instance resume, exact range, invalid range, ignored Range, cached hash; no installation");
    }
}
