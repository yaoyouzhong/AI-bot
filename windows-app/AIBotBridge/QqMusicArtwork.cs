namespace AIBotBridge;

// Native cover acquisition is independent of the music refresh cadence. A result
// is visible only for its exact track/URL; abandoned downloads cannot replace it.
internal sealed class QqMusicArtwork(Func<Uri,CancellationToken,Task<byte[]?>>? download=null):IDisposable
{
    private readonly Func<Uri,CancellationToken,Task<byte[]?>> _download=download??DownloadAsync;
    private string? _key;
    private CancellationTokenSource? _stop;
    private Task<byte[]?>? _task;
    private long _retryAt;
    internal byte[]? Read(string identity,string? url,CancellationToken token)
    {
        var uri=NormalizeUrl(url);if(uri is null){Clear();return null;}
        string key=identity+"\n"+uri.AbsoluteUri;
        if(_key!=key||_task is {IsCompletedSuccessfully:true,Result:null}&&Environment.TickCount64>=_retryAt) {
            Clear();_key=key;_stop=CancellationTokenSource.CreateLinkedTokenSource(token);_retryAt=Environment.TickCount64+10000;
            _task=FetchAsync(uri,_stop.Token);
        }
        return _task is {IsCompletedSuccessfully:true}?_task.Result:null;
    }
    private async Task<byte[]?> FetchAsync(Uri uri,CancellationToken token)
    {
        try{return await _download(uri,token);}catch(Exception ex) when(ex is HttpRequestException or IOException or OperationCanceledException or ArgumentException){return null;}
    }
    internal static Uri? NormalizeUrl(string? url)
    {
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.UserInfo.Length>0||!uri.IsDefaultPort||uri.Scheme is not ("http" or "https"))return null;
        if(uri.Host!="y.qq.com"&&uri.Host!="gtimg.cn"&&!uri.Host.EndsWith(".gtimg.cn",StringComparison.OrdinalIgnoreCase))return null;
        return new UriBuilder(uri){Scheme="https",Port=-1}.Uri;
    }
    private static async Task<byte[]?> DownloadAsync(Uri uri,CancellationToken token)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(6));token=timeout.Token;
        var addresses=await System.Net.Dns.GetHostAddressesAsync(uri.DnsSafeHost,token);
        if(addresses.Length==0||addresses.Any(a=>!BrowserMusicArtwork.PublicAddress(a)))throw new InvalidDataException("Non-public QQ Music artwork host");
        using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(6)};
        using var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength>2_000_000)return null;
        await using var input=await response.Content.ReadAsStreamAsync(token);using var bytes=new MemoryStream();var buffer=new byte[8192];int count;
        while((count=await input.ReadAsync(buffer,token))>0){if(bytes.Length+count>2_000_000)return null;bytes.Write(buffer,0,count);}
        return bytes.Length>0?bytes.ToArray():null;
    }
    private void Clear(){_stop?.Cancel();_stop?.Dispose();_stop=null;_task=null;_key=null;}
    public void Dispose()=>Clear();
}
