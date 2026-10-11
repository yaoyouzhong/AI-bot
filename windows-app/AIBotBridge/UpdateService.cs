using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIBotBridge;

internal sealed record PreparedUpdate(ComponentUpdate Release,string File,string Sha256,Tab5OtaPackage? Tab5=null)
{
    internal void VerifyUnchanged() {
        if(!FirmwareFlasher.MatchesHash(File,Sha256))throw new IOException("下载文件已改变，请重新下载。");
    }
}

// Public release metadata and files only. No device writes or installer launches.
internal sealed class UpdateService : IDisposable
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _query=new(1,1);
    internal IReadOnlyDictionary<string,ComponentUpdate> Available {get;private set;}=new Dictionary<string,ComponentUpdate>();
    internal IReadOnlyDictionary<string,GalleryUpdate> Galleries {get;private set;}=new Dictionary<string,GalleryUpdate>();
    internal DateTimeOffset? CheckedAt {get;private set;}
    internal string? Error {get;private set;}
    internal event Action? Changed;
    internal event Action<string>? TransferTrace;
    internal UpdateService(HttpMessageHandler? handler=null) {
        _http=new(handler??new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromMinutes(10)};
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AI-bot/"+Application.ProductVersion.Split('+')[0]);
    }
    internal async Task CheckAsync(CancellationToken cancellation) {
        await _query.WaitAsync(cancellation);
        try {
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(TimeSpan.FromSeconds(45));
            var next=new Dictionary<string,ComponentUpdate>(StringComparer.Ordinal);
            var galleries=new Dictionary<string,GalleryUpdate>(StringComparer.Ordinal);
            for(int page=1;page<=10;page++) {
                byte[] bytes=await ReadAsync(new($"https://api.github.com/repos/yaoyouzhong/AI-bot/releases?per_page=100&page={page}"),2*1024*1024,deadline.Token);
                using var doc=JsonDocument.Parse(bytes);
                foreach(var item in ComponentUpdateCatalog.Read(doc.RootElement))
                    if(!next.TryGetValue(item.Key,out var old)||item.Value.Number>old.Number)next[item.Key]=item.Value;
                foreach(var item in GalleryUpdateCatalog.Read(doc.RootElement))
                    if(!galleries.TryGetValue(item.Key,out var old)||item.Value.Number>old.Number)galleries[item.Key]=item.Value;
                if(doc.RootElement.GetArrayLength()<100){Available=next;Galleries=galleries;CheckedAt=DateTimeOffset.UtcNow;Error=null;Changed?.Invoke();return;}
            }
            throw new InvalidDataException("发布记录超过查询范围，请稍后重试。");
        }
        catch(Exception ex){Error=ex is OperationCanceledException?"检查已取消或超时":ex.Message;Changed?.Invoke();throw;}
        finally{_query.Release();}
    }
    internal static string? Blocked(UpdateDevice device,ComponentUpdate? release) {
        if(!device.Enabled)return "设备未启用";
        if(release is null)return "暂无可确认的正式版本";
        if(!ComponentUpdateCatalog.TryNumber(device.Version,release.Component=="tab5",out var installed))return "连接设备后确认运行版本";
        if(release.Number==installed)return "已是最新版本";
        if(release.Number<installed)return "当前版本较新，保留现有版本";
        if(release.Package is null||release.Checksums is null)return "发布包缺少可信下载或校验信息";
        return null;
    }
    // Missing legacy firmware identity is a manual migration, never a newer-version notification.
    internal static bool IsLegacyEsp(UpdateDevice device)=>device.Component=="esp8266"&&device.Version==EspFirmwareVersion.Legacy;
    internal static string? PreparationBlocked(UpdateDevice device,ComponentUpdate? release) {
        if(!IsLegacyEsp(device))return Blocked(device,release);
        if(!device.Enabled)return "设备未启用";
        if(!device.Online)return "请先连接小屏";
        if(release is null||release.Component!="esp8266")return "暂无可确认的正式版本";
        return release.Package is null||release.Checksums is null?"发布包缺少可信下载或校验信息":null;
    }
    internal static bool SameTarget(UpdateDevice before,[System.Diagnostics.CodeAnalysis.NotNullWhen(true)] UpdateDevice? current,ComponentUpdate release)=>
        current is not null&&current.Id==before.Id&&current.Component==before.Component&&current.Component==release.Component&&
        current.Version==before.Version&&PreparationBlocked(current,release) is null;
    internal static string ReadChecksum(string text,string filename) {
        var matches=new List<string>();
        foreach(string line in text.TrimStart('\uFEFF').Split('\n')) {
            var match=Regex.Match(line.TrimEnd('\r'),@"^([a-fA-F0-9]{64})[ \t]+\*?(.+)$");
            if(match.Success&&match.Groups[2].Value==filename)matches.Add(match.Groups[1].Value.ToLowerInvariant());
        }
        if(matches.Count!=1)throw new InvalidDataException("校验清单缺少对应文件或存在重复项。");
        return matches[0];
    }
    private async Task<HttpResponseMessage> OpenAsync(Uri uri,CancellationToken token,long? from=null,long? to=null) {
        for(int redirect=0;redirect<6;redirect++) {
            if(uri.Scheme!="https"||uri.UserInfo!=""||uri.Port!=443||uri.Host is not ("api.github.com" or "github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com"))
                throw new InvalidDataException("更新下载地址不受信任。");
            using var request=new HttpRequestMessage(HttpMethod.Get,uri);
            if(from is not null)request.Headers.Range=new(from,to);
            TransferTrace?.Invoke($"host={uri.Host} request range={from}-{to}");
            var response=await _http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false);
            TransferTrace?.Invoke($"host={uri.Host} status={(int)response.StatusCode}");
            if((int)response.StatusCode is >=300 and <400) {
                var location=response.Headers.Location;response.Dispose();
                if(location is null)throw new IOException("下载跳转缺少地址。");
                uri=location.IsAbsoluteUri?location:new Uri(uri,location);continue;
            }
            try{response.EnsureSuccessStatusCode();return response;}catch{response.Dispose();throw;}
        }
        throw new IOException("下载跳转次数过多。");
    }
    private async Task<byte[]> ReadAsync(Uri uri,int maximum,CancellationToken token) {
        using var result=new MemoryStream();await TransferAsync(uri,result,maximum,0,null,token);return result.ToArray();
    }
    private async Task TransferAsync(Uri uri,Stream target,long maximum,long expected,IProgress<int>? progress,CancellationToken token) {
        using var response=await OpenAsync(uri,token);
        long total=response.Content.Headers.ContentLength??0;
        if(total>maximum||expected>0&&total>0&&total!=expected)throw new InvalidDataException("下载文件大小与发布记录不一致。");
        using var input=await response.Content.ReadAsStreamAsync(token);byte[] buffer=new byte[65536];long received=0;
        while(true) {
            int count=await input.ReadAsync(buffer,token);if(count==0)break;
            received+=count;if(received>maximum)throw new InvalidDataException("下载文件超过允许大小。");
            await target.WriteAsync(buffer.AsMemory(0,count),token);
            if(total>0)progress?.Report((int)(received*100/total));
        }
        if(total>0&&received!=total||expected>0&&received!=expected)throw new IOException("下载未完成，请重试。");
    }
    internal async Task<PreparedUpdate> DownloadAsync(ComponentUpdate update,string bridgeVersion,IProgress<int>? progress,CancellationToken token) {
        var asset=update.Package??throw new InvalidDataException("缺少下载文件。");
        var checks=update.Checksums??throw new InvalidDataException("缺少校验文件。");
        string expected=ReadChecksum(Encoding.UTF8.GetString(await ReadAsync(checks.Url,256*1024,token)),asset.Name);
        string root=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AI-bot","updates");
        string directory=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try {
            if(Path.GetFileName(asset.Name)!=asset.Name)throw new InvalidDataException("下载文件名无效。");
            string file=Path.Combine(directory,asset.Name),partial=file+".part";
            await using(var output=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true))
                await TransferAsync(asset.Url,output,256*1024*1024,asset.Size,progress,token);
            token.ThrowIfCancellationRequested();
            if(!FirmwareFlasher.MatchesHash(partial,expected))throw new InvalidDataException("文件校验失败，未提供给设备。");
            File.Move(partial,file);
            var prepared=await Task.Run(()=>ValidatePackage(update,file,expected,bridgeVersion),token);
            token.ThrowIfCancellationRequested();return prepared;
        }catch{
            // This fresh, owned directory contains only files created by this call.
            if(Directory.Exists(directory))Directory.Delete(directory,true);
            throw;
        }
    }
    internal async Task<PreparedGalleryUpdate> DownloadGalleryAsync(GalleryUpdate update,IProgress<int>? progress,CancellationToken token) {
        token.ThrowIfCancellationRequested();
        if(GalleryUpdateCatalog.Blocked(null,update) is {} reason)throw new InvalidDataException(reason);
        var asset=update.Package!;
        string expected=ReadChecksum(Encoding.UTF8.GetString(await ReadAsync(update.Checksums!.Url,256*1024,token)),asset.Name);
        if(Path.GetFileName(asset.Name)!=asset.Name)throw new InvalidDataException("下载文件名无效。");
        string directory=GalleryDownloadDirectory(expected);
        Directory.CreateDirectory(directory);
        string metadata=Path.Combine(directory,"DOWNLOAD.json"),file=Path.Combine(directory,asset.Name),partial=file+".part";
        var identity=new GalleryDownloadIdentity(1,update.Category,update.Version,asset.Name,asset.Size,expected);
        if(File.Exists(metadata)) {
            if(JsonSerializer.Deserialize<GalleryDownloadIdentity>(File.ReadAllText(metadata),JsonDefaults.Options)!=identity)
                throw new InvalidDataException("断点记录与当前图库包不一致，未混用下载内容。");
        }else {
            string staging=metadata+".tmp";
            File.WriteAllText(staging,JsonSerializer.Serialize(identity,JsonDefaults.Options));File.Move(staging,metadata);
        }
        if(File.Exists(file)) {
            if(new FileInfo(file).Length==asset.Size&&await Task.Run(()=>FirmwareFlasher.MatchesHash(file,expected),token))return new(update,file,expected);
            File.Delete(file);
        }
        await using(var output=new FileStream(partial,FileMode.OpenOrCreate,FileAccess.Write,FileShare.None,65536,true)) {
            if(output.Length>asset.Size)output.SetLength(0);
            progress?.Report((int)(output.Length*100/asset.Size));
            await TransferGalleryAsync(asset,output,progress,token);
        }
        token.ThrowIfCancellationRequested();
        if(!await Task.Run(()=>FirmwareFlasher.MatchesHash(partial,expected),token)) {
            File.Delete(partial);throw new InvalidDataException("图库下载校验失败，已清除损坏的断点，原图库保持不变。");
        }
        token.ThrowIfCancellationRequested();File.Move(partial,file);return new(update,file,expected);
    }
    private static string GalleryDownloadDirectory(string sha) {
        if(!Regex.IsMatch(sha,@"^[a-f0-9]{64}$",RegexOptions.CultureInvariant))throw new InvalidDataException("图库校验值无效。");
        return Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AI-bot","updates","gallery-downloads",sha);
    }
    internal static void ClearGalleryDownload(PreparedGalleryUpdate prepared) {
        string directory=GalleryDownloadDirectory(prepared.Sha256),file=Path.Combine(directory,prepared.Release.Package!.Name);
        if(Path.GetFullPath(file)!=Path.GetFullPath(prepared.File)||Path.GetFileName(prepared.Release.Package.Name)!=prepared.Release.Package.Name)
            throw new InvalidDataException("图库下载缓存路径不一致。");
        foreach(string path in new[]{file,file+".part",Path.Combine(directory,"DOWNLOAD.json"),Path.Combine(directory,"DOWNLOAD.json.tmp")})File.Delete(path);
        Directory.Delete(directory);
    }
    private async Task TransferGalleryAsync(UpdateAsset asset,Stream target,IProgress<int>? progress,CancellationToken token) {
        if(asset.Size<=4*1024*1024&&target.Length==0){await TransferAsync(asset.Url,target,2L*1024*1024*1024,asset.Size,progress,token);return;}
        const int chunk=2*1024*1024;byte[] buffer=new byte[65536];long offset=target.Length;
        while(offset<asset.Size) {
            long end=Math.Min(asset.Size-1,offset+chunk-1);
            for(int attempt=0;;attempt++) {
                token.ThrowIfCancellationRequested();target.SetLength(offset);target.Position=offset;
                try {
                    using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromMinutes(2));
                    using var response=await OpenAsync(asset.Url,deadline.Token,offset,end);
                    bool full=response.StatusCode==System.Net.HttpStatusCode.OK&&offset==0;
                    var range=response.Content.Headers.ContentRange;
                    if(!full&&(response.StatusCode!=System.Net.HttpStatusCode.PartialContent||range is null||range.Unit!="bytes"||range.From!=offset||range.To!=end||range.Length!=asset.Size))
                        throw new InvalidDataException("图库分段响应与发布文件不一致。");
                    if(full)deadline.CancelAfter(TimeSpan.FromMinutes(10));
                    long length=full?asset.Size:end-offset+1,received=0;
                    if(response.Content.Headers.ContentLength is {} size&&size!=length)throw new InvalidDataException("图库分段大小异常。");
                    using var input=await response.Content.ReadAsStreamAsync(deadline.Token);
                    while(true) {
                        int count=await input.ReadAsync(buffer,deadline.Token);if(count==0)break;received+=count;
                        if(received>length)throw new InvalidDataException("图库分段内容超限。");
                        await target.WriteAsync(buffer.AsMemory(0,count),deadline.Token);
                        progress?.Report((int)((offset+received)*100/asset.Size));
                    }
                    if(received!=length)throw new IOException("图库分段下载未完成。");
                    offset+=received;break;
                }catch(Exception ex) when(attempt<2&&!token.IsCancellationRequested&&ex is HttpRequestException or IOException or OperationCanceledException) {
                    await Task.Delay(500*(attempt+1),token);
                }
            }
        }
    }
    internal static PreparedUpdate ValidatePackage(ComponentUpdate update,string path,string sha,string bridgeVersion) {
        if(!FirmwareFlasher.MatchesHash(path,sha))throw new InvalidDataException("文件校验失败。");
        if(update.Component=="bridge") {
            using var stream=File.OpenRead(path);
            if(stream.ReadByte()!='M'||stream.ReadByte()!='Z')throw new InvalidDataException("不是 Windows 安装程序。");
            return new(update,path,sha);
        }
        using var zip=ZipFile.OpenRead(path);
        if(zip.Entries.Select(e=>e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=zip.Entries.Count)
            throw new InvalidDataException("固件包包含重复路径。");
        byte[] Entry(string name,int max) {
            var item=zip.GetEntry(name)??throw new InvalidDataException("固件包缺少 "+name);
            if(item.Length<1||item.Length>max)throw new InvalidDataException("固件包内容大小异常。");
            using var input=item.Open();using var output=new MemoryStream();byte[] buffer=new byte[65536];
            for(int count;(count=input.Read(buffer))>0;){if(output.Length+count>max)throw new InvalidDataException("固件包内容超限。");output.Write(buffer,0,count);}
            return output.ToArray();
        }
        void Compatibility(JsonElement meta) {
            if(meta.TryGetProperty("minimumBridgeVersion",out var minimum)&&
               (!Version.TryParse(minimum.GetString(),out var required)||!Version.TryParse(bridgeVersion,out var current)||current<required))
                throw new InvalidDataException("请先更新电脑端 AI-bot，再升级设备固件。");
            if(meta.TryGetProperty("protocolVersion",out var protocol)&&protocol.GetInt32()!=1)throw new InvalidDataException("固件协议不兼容，请先更新电脑端。");
        }
        if(update.Component=="tab5") {
            if(zip.GetEntry("factory.bin") is not null)throw new InvalidDataException("首次安装包不能用于固件升级。");
            if(zip.GetEntry("EDITION.json") is not null){using var edition=JsonDocument.Parse(Entry("EDITION.json",8192));Compatibility(edition.RootElement);}
            string directory=Path.Combine(Path.GetDirectoryName(path)!,"application");Directory.CreateDirectory(directory);
            string binary=Path.Combine(directory,"aibot_tab5.bin");
            File.WriteAllBytes(binary,Entry("aibot_tab5.bin",Tab5OtaPackage.MaximumSize));
            File.WriteAllBytes(binary+".notes.json",Entry("aibot_tab5.bin.notes.json",8192));
            var image=Tab5OtaPackage.Load(binary);
            if(image.Version!=update.Version)throw new InvalidDataException("固件内置版本与发布记录不一致。");
            return new(update,path,sha,image);
        }
        if(update.Component!="esp8266")throw new InvalidDataException("未知固件类型。");
        string version=Encoding.UTF8.GetString(Entry("VERSION",128)).Trim();
        if(version!=update.Version)throw new InvalidDataException("小屏固件版本与发布记录不一致。");
        if(zip.GetEntry("COMPONENT.json") is not null) {
            using var meta=JsonDocument.Parse(Entry("COMPONENT.json",8192));Compatibility(meta.RootElement);
            if(meta.RootElement.GetProperty("component").GetString()!="esp8266"||meta.RootElement.GetProperty("version").GetString()!=version)throw new InvalidDataException("固件不适用于 ESP8266 小屏。");
        }
        byte[] esp=Entry("firmware.bin",4*1024*1024);
        if(esp.Length<1024||esp[0]!=0xe9||zip.GetEntry("aibot_tab5.bin") is not null)throw new InvalidDataException("不是有效的小屏固件包。");
        return new(update,path,sha);
    }
    public void Dispose(){_http.Dispose();}
}
