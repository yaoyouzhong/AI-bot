using System.Net;
using System.Text.Json;

namespace AIBotBridge;

internal static class BrowserMusicArtworkSelfTest
{
    internal static async Task RunAsync() {
        void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        using var image=new Bitmap(640,360);using(var graphics=Graphics.FromImage(image))graphics.Clear(Color.Red);
        using var memory=new MemoryStream();image.Save(memory,System.Drawing.Imaging.ImageFormat.Jpeg);byte[] bytes=memory.ToArray();
        int downloads=0;bool fail=false;var urls=new List<string>();
        var source=new BrowserMusicArtwork((url,_)=>{downloads++;urls.Add(url);if(fail)throw new IOException("offline");return Task.FromResult(bytes);});
        string Sample(string title,string artist,string id,params string[] artwork)=>JsonSerializer.Serialize(new{title,artist,youtubeVideoId=id,artwork});
        Require(await source.AcceptAsync(Sample("Video A","Artist A","abcdefghijk"),default),"First automatic source was ignored");
        var first=source.Find("Video A","Artist A");
        Require(first is not null&&source.Find("Video B","Artist A") is null&&source.Find("Video A","Artist B") is null,"Artwork matched another title/artist");
        Require(!await source.AcceptAsync(Sample("Video A","Artist A","abcdefghijk"),default)&&downloads==1&&ReferenceEquals(first,source.Find("Video A","Artist A")),"Unchanged metadata fetched/replaced cached artwork");
        Require(await source.AcceptAsync(Sample("Video B","Artist B","lmnopqrstuv"),default)&&urls[1].Contains("lmnopqrstuv"),"Next video did not use its own identifier");
        Require(await source.AcceptAsync(Sample("Song C","Artist C","","https://images.example.org/cover.jpg"),default),"Generic Media Session artwork was ignored");
        fail=true;Require(!await source.AcceptAsync(Sample("Video A","Artist A","12345678901"),default)&&ReferenceEquals(first,source.Find("Video A","Artist A")),"Failed fetch erased last successful artwork");
        foreach(string address in new[]{"127.0.0.1","10.0.0.1","172.16.2.3","192.168.0.1","169.254.1.1","::1","fe80::1","fc00::1"})Require(!BrowserMusicArtwork.PublicAddress(IPAddress.Parse(address)),"Private artwork address accepted");
        Require(BrowserMusicArtwork.PublicAddress(IPAddress.Parse("8.8.8.8")),"Public artwork address rejected");
        Require(!BrowserMusicArtwork.Authorized("https://www.youtube.com",new string('a',64)),"Web page origin accepted");
        Directory.CreateDirectory(Path.GetDirectoryName(BrowserMusicArtwork.KeyPath)!);File.WriteAllText(BrowserMusicArtwork.KeyPath,new string('a',64));
        Require(BrowserMusicArtwork.Authorized("chrome-extension://"+new string('b',32),new string('a',64))&&!BrowserMusicArtwork.Authorized(null,new string('c',64)),"Local pairing key authentication failed");
        var reserve=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);reserve.Start();int port=((IPEndPoint)reserve.LocalEndpoint).Port;reserve.Stop();
        int accepted=0;string received="";using var stop=new CancellationTokenSource();
        var server=new LocalStatusServer(port,musicArtwork:(body,_)=>{accepted++;received=body;return Task.CompletedTask;});
        var run=server.RunAsync(()=>new(1,"",0,0,DateTimeOffset.UtcNow,new("idle",0),new("idle",0)),stop.Token);
        try {
            using var client=new System.Net.Http.HttpClient();
            async Task<int> Post(string origin,string? key) {
                using var request=new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post,$"http://127.0.0.1:{port}/music/artwork");
                request.Headers.Add("Origin",origin);if(key is not null)request.Headers.Add("X-AIBot-Music-Key",key);
                request.Content=new System.Net.Http.StringContent("{\"title\":\"歌曲封面\"}",System.Text.Encoding.UTF8,"application/json");
                using var response=await client.SendAsync(request);return (int)response.StatusCode;
            }
            Require(await Post("https://www.youtube.com",new string('a',64))==404&&accepted==0,"Web origin called local artwork handler");
            Require(await Post("chrome-extension://"+new string('b',32),null)==404&&accepted==0,"Unpaired companion called artwork handler");
            Require(await Post("chrome-extension://"+new string('b',32),new string('a',64))==200&&accepted==1&&received.Contains("歌曲封面"),"Paired companion UTF8 request did not reach handler");
        }finally{stop.Cancel();await run;}
        Console.WriteLine("BROWSER_MUSIC_ARTWORK_OK automatic-next-video/generic-metadata/exact-title-artist/cache/offline-retention/public-address/local-auth");
    }
}
