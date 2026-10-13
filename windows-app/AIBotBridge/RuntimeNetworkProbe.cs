using System.Diagnostics;
using System.Net;

namespace AIBotBridge;

internal sealed record RuntimeProbeResult(bool Responded,int? Status,double? Milliseconds,string Failure)
{
    internal bool Reachable=>Responded&&Status is >=200 and <500;
    internal bool Restricted=>Status is 403 or 429;
}
internal sealed record RuntimeNetworkRound(RuntimeHealthCheck[] Checks,double? ThroughputMbps=null,DateTimeOffset? ThroughputAt=null,bool RouteChanged=false,RuntimeLatencyPoint[]? LatencyHistory=null);
internal sealed class RuntimeNetworkProbe
{
    internal static readonly Uri ChatGpt=new("https://chatgpt.com/backend-api/wham/usage");
    internal static readonly Uri Assets=new("https://cdn.oaistatic.com/");
    private readonly Queue<bool> _failures=[];
    private readonly Queue<double> _baseline=[];
    private readonly Queue<RuntimeLatencyPoint> _history=[];
    private string _routeIdentity="";
    private string _verifiedRoutes="";
    private DateTimeOffset _nextSpeed;
    private double? _lastLatency;
    private DateTimeOffset? _lastSuccess;
    internal Func<Uri,Uri?,CancellationToken,Task<RuntimeProbeResult>> Send {get;init;}=ProbeAsync;
    internal Func<WindowsProxyConfiguration> Configuration {get;init;}=WindowsPacRoute.Read;
    internal Func<Uri,Uri?> ProcessProxy {get;init;}=WindowsPacRoute.EnvironmentProxy;
    internal Func<string,Uri,PacRoute> Resolve {get;init;}=WindowsPacRoute.Resolve;
    internal Func<string,CancellationToken,Task<RuntimeProbeResult>> FetchPac {get;init;}=ProbePacAsync;
    internal static async Task<RuntimeProbeResult> ProbeAsync(Uri target,Uri? proxy,CancellationToken stop) {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop);deadline.CancelAfter(6000);
        using var handler=new SocketsHttpHandler {UseProxy=proxy is not null,Proxy=proxy is null?null:new WebProxy(proxy),
            AllowAutoRedirect=false,ConnectTimeout=TimeSpan.FromSeconds(4),UseCookies=false};
        using var client=new HttpClient(handler){Timeout=Timeout.InfiniteTimeSpan};
        using var request=new HttpRequestMessage(HttpMethod.Get,target);
        request.Headers.UserAgent.ParseAdd("AI-bot-Runtime-Monitor/1.0");
        var watch=Stopwatch.StartNew();
        try {
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            return new(true,(int)response.StatusCode,watch.Elapsed.TotalMilliseconds,"");
        }catch(OperationCanceledException) when(!stop.IsCancellationRequested){return new(false,null,null,"超时");}
        catch(HttpRequestException ex){return new(false,null,null,ex.HttpRequestError switch {
            HttpRequestError.NameResolutionError=>"DNS 失败",HttpRequestError.SecureConnectionError=>"TLS 失败",_=>"连接失败"});}
    }
    internal async Task<RuntimeNetworkRound> CaptureAsync(RuntimeHealthOptions options,CancellationToken stop) {
        if(!options.Network)return new([new("vpn","VPN / 代理","disabled","网络检测已关闭","","",DateTimeOffset.UtcNow),
            new("pac","PAC 路由","disabled","PAC 检测已关闭","","",DateTimeOffset.UtcNow)]);
        var config=Configuration();
        bool configured=!string.IsNullOrWhiteSpace(config.PacUrl);
        RuntimeProbeResult? pacResponse=null;PacRoute? chatRoute=null,assetRoute=null;
        if(configured) {
            pacResponse=await FetchPac(config.PacUrl!,stop);
            if(pacResponse.Reachable) {
                chatRoute=await Task.Run(()=>Resolve(config.PacUrl!,ChatGpt),stop);
                stop.ThrowIfCancellationRequested();
                assetRoute=await Task.Run(()=>Resolve(config.PacUrl!,Assets),stop);
            }
        }
        stop.ThrowIfCancellationRequested();
        // A retained static proxy is a diagnostic comparison when PAC is down.
        // Never confuse the PAC script server with a traffic ingress.
        Uri? ingress=ProcessProxy(ChatGpt)??WindowsPacRoute.ParseProxy(config.StaticProxy)??chatRoute?.Proxy;
        if(chatRoute?.Resolved==true&&assetRoute?.Resolved==true)_verifiedRoutes=(chatRoute.Proxy?.AbsoluteUri??chatRoute.Label)+"|"+(assetRoute.Proxy?.AbsoluteUri??assetRoute.Label);
        string identity=(ingress?.AbsoluteUri??"direct")+"|"+config.PacUrl+"|"+_verifiedRoutes;
        bool routeChanged=identity!=_routeIdentity;
        if(routeChanged){_routeIdentity=identity;_baseline.Clear();_failures.Clear();_history.Clear();_nextSpeed=default;_lastLatency=null;_lastSuccess=null;}
        var results=await Task.WhenAll(Send(ChatGpt,ingress,stop),Send(Assets,ingress,stop));
        var now=DateTimeOffset.UtcNow;
        bool failed=results.Any(r=>!r.Reachable);_failures.Enqueue(failed);while(_failures.Count>10)_failures.Dequeue();
        double? latency=results.All(r=>r.Milliseconds.HasValue)?results.Max(r=>r.Milliseconds):null;
        if(!failed&&latency.HasValue){_lastLatency=latency;_lastSuccess=now;}
        _history.Enqueue(new(now.ToUnixTimeSeconds(),latency,!failed));while(_history.Count>10)_history.Dequeue();
        double? median=_baseline.Count>=4?_baseline.Order().ElementAt(_baseline.Count/2):null;
        bool slow=latency>options.SlowMilliseconds||median is {} baseMs&&latency>Math.Max(1000,baseMs*3+500);
        if(!failed&&!slow&&latency is {} elapsed){_baseline.Enqueue(elapsed);while(_baseline.Count>20)_baseline.Dequeue();}
        string endpoint=ingress is null?"直连探测":"代理出口探测";
        string detail=$"ChatGPT {Describe(results[0])}；资源服务 {Describe(results[1])}。近 {_failures.Count} 次失败 {_failures.Count(x=>x)} 次。";
        if(failed&&_lastLatency.HasValue)detail+=$"上次成功 {_lastLatency:0} ms（{_lastSuccess?.ToLocalTime():HH:mm:ss}）。";
        var vpn=new RuntimeHealthCheck("vpn","VPN / 代理",failed?"error":slow?"warning":results.Any(r=>r.Restricted)?"unknown":"ok",
            failed?(results.All(r=>!r.Reachable)?"代理连接异常":"目标响应异常"):slow?"VPN 变慢":results.Any(r=>r.Restricted)?"目标限制访问":"出口响应正常",detail,
            failed?(results.All(r=>!r.Reachable)?"检查 VPN 客户端和出口；持续失败时尝试更换节点。":"检查对应服务；单个目标异常不能确定是节点故障。"):
            slow?"多个目标持续变慢时，检查节点或更换节点。":"",now,latency,endpoint) {LastSuccessfulLatencyMs=_lastLatency,LastSuccessfulAt=_lastSuccess};
        RuntimeHealthCheck pac;
        if(!configured)pac=new("pac","PAC 路由","disabled","未配置 PAC","当前使用环境代理、静态代理或直连；不判定 PAC 故障。","",now);
        else if(pacResponse?.Reachable!=true)pac=new("pac","PAC 路由","error","PAC 异常",
            "PAC 脚本服务不可用。"+(results.All(r=>r.Reachable)?"代理出口仍能响应。":"代理出口也需检查。"),
            results.All(r=>r.Reachable)?"建议重启 VPN 客户端，然后重新检查 PAC。":"先检查 VPN 客户端，再复查节点和 PAC。",now);
        else if(chatRoute?.Resolved!=true||assetRoute?.Resolved!=true)pac=new("pac","PAC 路由","error","PAC 异常",
            $"PAC 服务可访问，脚本选路失败（{chatRoute?.Error??0}/{assetRoute?.Error??0}）。","建议重启 VPN 客户端并检查 PAC 规则。",now);
        else {
            string route=$"ChatGPT：{chatRoute.Label}；资源服务：{assetRoute.Label}";
            bool mismatch=options.ExpectChatGptProxy&&(chatRoute.Proxy is null||assetRoute.Proxy is null);
            var routed=await Task.WhenAll(Send(ChatGpt,chatRoute.Proxy,stop),Send(Assets,assetRoute.Proxy,stop));
            now=DateTimeOffset.UtcNow;
            bool bad=routed.Any(r=>!r.Reachable);
            pac=new("pac","PAC 路由",mismatch||bad?"error":routed.Any(r=>r.Restricted)?"unknown":"ok",mismatch||bad?"PAC 异常":"PAC 路由正常",
                route+$"。选路实测：{Describe(routed[0])} / {Describe(routed[1])}。",
                mismatch?"关键域名未按预期走代理，请检查 PAC 规则；可先重启 VPN 客户端。":
                bad&&results.All(r=>r.Reachable)?"备用出口可用，建议重启 VPN 客户端后复查 PAC。":bad?"检查 VPN 客户端和对应出口。":"",now,Route:route);
        }
        double? throughput=null;DateTimeOffset? speedAt=null;
        if(options.Throughput&&ingress is not null&&!failed&&now>=_nextSpeed) {
            _nextSpeed=now.AddMinutes(5);throughput=await SampleThroughputAsync(ingress,stop);if(throughput.HasValue)speedAt=DateTimeOffset.UtcNow;
        }
        return new([vpn,pac],throughput,speedAt,routeChanged,_history.ToArray());
    }
    private static string Describe(RuntimeProbeResult result)=>result.Responded?$"HTTP {result.Status} · {result.Milliseconds:0} ms":result.Failure;
    private static async Task<RuntimeProbeResult> ProbePacAsync(string text,CancellationToken stop) {
        if(!Uri.TryCreate(text,UriKind.Absolute,out var uri)||uri.Scheme is not ("http" or "https")||uri.UserInfo.Length>0)
            return new(false,null,null,"PAC 地址不支持");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop);timeout.CancelAfter(5000);
        using var client=new HttpClient(new SocketsHttpHandler{UseProxy=false,UseCookies=false,AllowAutoRedirect=false}){Timeout=Timeout.InfiniteTimeSpan};
        var clock=Stopwatch.StartNew();
        try {
            using var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
            if(!response.IsSuccessStatusCode)return new(false,(int)response.StatusCode,null,"PAC HTTP 异常");
            if(response.Content.Headers.ContentLength>524288)return new(false,null,null,"PAC 脚本过大");
            await using var body=await response.Content.ReadAsStreamAsync(timeout.Token);
            var buffer=new byte[8192];int total=0,count;while((count=await body.ReadAsync(buffer,timeout.Token))>0){total+=count;if(total>524288)return new(false,null,null,"PAC 脚本过大");}
            return new(total>0,(int)response.StatusCode,clock.Elapsed.TotalMilliseconds,total==0?"PAC 内容为空":"");
        }catch(OperationCanceledException) when(!stop.IsCancellationRequested){return new(false,null,null,"PAC 超时");}
        catch(Exception ex) when(ex is HttpRequestException or IOException){return new(false,null,null,"PAC 连接失败");}
    }
    private static async Task<double?> SampleThroughputAsync(Uri proxy,CancellationToken stop) {
        // 128 KiB once per five minutes, bounded read. This is a small-transfer
        // observation, not a full bandwidth measurement or node identification.
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop);deadline.CancelAfter(8000);
        using var client=new HttpClient(new SocketsHttpHandler{UseProxy=true,Proxy=new WebProxy(proxy),UseCookies=false,AllowAutoRedirect=false}){Timeout=Timeout.InfiniteTimeSpan};
        try {
            var clock=Stopwatch.StartNew();
            using var response=await client.GetAsync("https://speed.cloudflare.com/__down?bytes=131072",HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            if(!response.IsSuccessStatusCode)return null;
            await using var stream=await response.Content.ReadAsStreamAsync(deadline.Token);
            byte[] buffer=new byte[8192];int bytes=0,n;while(bytes<=131072&&(n=await stream.ReadAsync(buffer,deadline.Token))>0)bytes+=n;
            return bytes==131072?Math.Round(bytes*8/Math.Max(.001,clock.Elapsed.TotalSeconds)/1000000,2):null;
        }catch(OperationCanceledException) when(!stop.IsCancellationRequested){return null;}
        catch(Exception ex) when(ex is HttpRequestException or IOException){return null;}
    }
}
