using System.Text;
using System.Text.Json;

namespace AIBotBridge;

// Some desktop rollouts return full turns with empty items through app-server.
// Read only the matching completion's public answer. Never expose reasoning,
// tool output, earlier turns, or an arbitrary file supplied by the device.
internal static class Tab5CodexReplyHistory
{
    private const int MaxTailBytes=2*1024*1024;
    internal static string? Read(string path,string turnId,string home) {
        try {
            string Normalize(string value)=>Path.GetFullPath(value.StartsWith(@"\\?\",StringComparison.Ordinal)?value[4..]:value);
            var full=Normalize(path);var root=Normalize(home);
            if(!full.EndsWith(".jsonl",StringComparison.OrdinalIgnoreCase)||
               !new[]{"sessions","archived_sessions"}.Any(dir=>full.StartsWith(Path.Combine(root,dir)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)))return null;
            using var file=new FileStream(full,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            int count=(int)Math.Min(file.Length,MaxTailBytes);
            long start=file.Length-count;file.Position=start;
            byte[] bytes=new byte[count];int used=0;
            while(used<count) {int read=file.Read(bytes,used,count-used);if(read==0)break;used+=read;}
            var tail=Encoding.UTF8.GetString(bytes,0,used);
            if(start>0) {int newline=tail.IndexOf('\n');if(newline<0)return null;tail=tail[(newline+1)..];}
            return Parse(tail,turnId);
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) {return null;}
    }
    internal static string? Parse(string tail,string turnId) {
        if(string.IsNullOrEmpty(turnId))return null;
        string? answer=null;
        foreach(var line in tail.Split('\n')) {
            if(string.IsNullOrWhiteSpace(line))continue;
            try {
                using var doc=JsonDocument.Parse(line);var root=doc.RootElement;
                if(root.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("type",out var type)||type.GetString()!="event_msg"||
                   !root.TryGetProperty("payload",out var payload)||payload.ValueKind!=JsonValueKind.Object||
                   !payload.TryGetProperty("type",out type)||type.GetString()!="task_complete"||
                   !payload.TryGetProperty("turn_id",out var id)||id.ValueKind!=JsonValueKind.String||id.GetString()!=turnId||
                   !payload.TryGetProperty("last_agent_message",out var text)||text.ValueKind!=JsonValueKind.String)continue;
                answer=Tab5CodexTasks.BoundResponse(text.GetString());
            }catch(Exception ex) when(ex is JsonException or InvalidOperationException) { }
        }
        return answer;
    }
}
