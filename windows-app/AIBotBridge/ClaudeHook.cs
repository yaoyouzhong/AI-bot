using System.Text.Json;
namespace AIBotBridge;

internal static class ClaudeHook
{
    private static readonly HashSet<string> Events=["UserPromptSubmit","PreToolUse","PostToolUse","PostToolUseFailure","PostToolBatch","PermissionRequest","Elicitation","Stop","StopFailure","Notification","SessionEnd"];
    internal static Dictionary<string,object>? Project(ReadOnlyMemory<byte> bytes,DateTimeOffset at)
    {
        if(bytes.Length>8388608)return null;
        try {
            using var doc=JsonDocument.Parse(bytes);var raw=doc.RootElement;
            string? Text(string key)=>raw.ValueKind==JsonValueKind.Object&&raw.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
            var kind=Text("hook_event_name");var sid=Text("session_id");var pid=Text("prompt_id");
            if(kind is null||!Events.Contains(kind)||raw.TryGetProperty("agent_id",out _)||!Guid.TryParse(sid,out _)||!Guid.TryParse(pid,out _))return null;
            var result=new Dictionary<string,object>{{"agent","claude"},{"event",kind},{"sessionId",sid!},{"promptId",pid!},{"emittedAt",at.ToUnixTimeMilliseconds()}};
            if(kind=="Notification"){if(Text("notification_type")!="idle_prompt")return null;result["notificationType"]="idle_prompt";}
            return result;
        }catch(JsonException){return null;}
    }
    internal static async Task RunAsync(int port)
    {
        try {
            var at=DateTimeOffset.UtcNow;using var input=Console.OpenStandardInput();using var bytes=new MemoryStream();var buffer=new byte[65536];int count;
            while((count=await input.ReadAsync(buffer))>0){bytes.Write(buffer,0,count);if(bytes.Length>8388608)return;}
            if(Project(bytes.ToArray(),at) is not {} body)return;
            using var handler=new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false};using var http=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(1)};
            using var content=new StringContent(JsonSerializer.Serialize(body),System.Text.Encoding.UTF8,"application/json");
            using var response=await http.PostAsync($"http://127.0.0.1:{port}/event",content);
        }catch(Exception e) when(e is IOException or System.Net.Http.HttpRequestException or OperationCanceledException){ }
    }
}
