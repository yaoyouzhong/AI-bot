using System.Text.Json;

namespace AIBotBridge;

// Observe production authenticated requests and model lifecycle metadata only.
// A successful unauthenticated probe is never business-connection evidence.
internal sealed class CodexConnectionHealth
{
    internal static CodexConnectionHealth Shared {get;}=new();
    private sealed class Channel {
        internal string Kind="unknown";internal int Failures,Successes;
        internal DateTimeOffset At,LastSuccess;
        internal readonly Queue<DateTimeOffset> Reconnects=[];
    }
    private readonly object _sync=new();
    private readonly Dictionary<string,Channel> _channels=[];
    internal void Observe(string source,string kind,DateTimeOffset at) {
        if(source is not ("usage" or "model")||kind is not ("ok" or "network" or "timeout" or "auth" or "limited" or "server" or "unknown"))return;
        lock(_sync) {
            if(!_channels.TryGetValue(source,out var channel))_channels[source]=channel=new();
            if(at<=channel.At)return;
            if(at-channel.At>TimeSpan.FromMinutes(5)){channel.Failures=channel.Successes=0;channel.Kind="unknown";channel.Reconnects.Clear();}
            channel.At=at;
            if(kind=="ok") {
                channel.LastSuccess=at;channel.Successes++;
                if(channel.Kind is "unknown" or "ok"||channel.Successes>=2){if(channel.Failures>=3)channel.Reconnects.Clear();channel.Kind="ok";channel.Failures=0;}
            } else {
                channel.Successes=0;channel.Failures=channel.Kind==kind?channel.Failures+1:1;channel.Kind=kind;
                if(kind is "network" or "timeout") {
                    channel.Reconnects.Enqueue(at);
                    while(channel.Reconnects.Count>32||channel.Reconnects.TryPeek(out var oldest)&&at-oldest>TimeSpan.FromMinutes(5))channel.Reconnects.Dequeue();
                    channel.Failures=Math.Max(channel.Failures,channel.Reconnects.Count);
                }
            }
        }
    }
    internal void ObserveHttp(int status,DateTimeOffset at)=>Observe("usage",status is >=200 and <300?"ok":status==401?"auth":status is 403 or 429?"limited":status>=500?"server":"unknown",at);
    internal void ObserveModelEvent(string line,DateTimeOffset now) {
        // Error messages may contain private text. Only fixed error categories
        // and timestamps survive; prompts, replies, IDs and URLs are discarded.
        if(!line.Contains("event_msg",StringComparison.Ordinal))return;
        try {
            using var doc=JsonDocument.Parse(line);var root=doc.RootElement;
            if(root.GetProperty("type").GetString()!="event_msg"||!root.TryGetProperty("payload",out var payload)||
                !DateTimeOffset.TryParse(root.GetProperty("timestamp").GetString(),out var at)||at<now.AddSeconds(-30)||at>now.AddSeconds(5))return;
            string type=payload.TryGetProperty("type",out var t)&&t.ValueKind==JsonValueKind.String?t.GetString()!:"";
            if(type is "task_complete" or "agent_message"){Observe("model","ok",at);return;}
            if(type is not ("error" or "stream_error"))return;
            string info=payload.TryGetProperty("codex_error_info",out var error)?error.ToString():"";
            string kind=info.Contains("Unauthorized",StringComparison.OrdinalIgnoreCase)?"auth":
                info.Contains("TooManyRequests",StringComparison.OrdinalIgnoreCase)||info.Contains("UsageLimit",StringComparison.OrdinalIgnoreCase)?"limited":
                info.Contains("Stream",StringComparison.OrdinalIgnoreCase)||info.Contains("ConnectionFailed",StringComparison.OrdinalIgnoreCase)?"network":
                info.Contains("Timeout",StringComparison.OrdinalIgnoreCase)?"timeout":info.Contains("InternalServer",StringComparison.OrdinalIgnoreCase)?"server":"unknown";
            Observe("model",kind,at);
        }catch(Exception ex) when(ex is JsonException or InvalidOperationException or KeyNotFoundException){/* Unrecognized metadata is not an outage. */}
    }
    internal RuntimeHealthCheck Capture(DateTimeOffset now) {
        lock(_sync) {
            var current=_channels.Values.Where(c=>now-c.At<TimeSpan.FromMinutes(5)).ToArray();
            var failure=current.Where(c=>c.Kind is not ("ok" or "unknown")&&c.Failures>=(c.Kind is "auth" or "limited"?2:3)).OrderByDescending(c=>c.Kind=="auth").FirstOrDefault();
            var success=_channels.Values.Select(c=>c.LastSuccess).DefaultIfEmpty().Max();
            string state=failure is null?current.Any(c=>c.Kind!="ok")?"observing":current.Length>0?"ok":"unknown":failure.Kind=="auth"?"error":"warning";
            string title=failure?.Kind switch {"auth"=>"Codex 登录需检查","limited"=>"Codex 访问受限","server"=>"Codex 服务响应异常","timeout"=>"Codex 连接超时","network"=>"Codex 连接不稳",_=>state=="ok"?"实际连接正常":state=="observing"?"正在确认连接":"暂无近期连接结果"};
            string advice=failure?.Kind switch {"auth"=>"检查 Codex 登录状态，必要时重新登录。","limited"=>"稍后重试，检查 PAC 路径。","server"=>"稍后重试服务。","network" or "timeout"=>"检查 VPN / PAC，必要时更换节点。",_=>""};
            return new("codex","Codex 实际连接",state,title,failure is null?"依据现有认证请求和模型事件；没有新请求时保留待确认。":$"近期异常 {failure.Failures} 次；未用探测成功覆盖业务错误。",advice,now) {LastSuccessfulAt=success==default?null:success};
        }
    }
}
