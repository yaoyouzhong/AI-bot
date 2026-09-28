using System.Text;
using System.Text.Json;
using System.Diagnostics;

namespace AIBotBridge;

internal sealed class CodexLifecycleTracker
{
    private readonly Func<bool> _isRunning;
    internal CodexLifecycleTracker(Func<bool>? isRunning=null) { _isRunning=isRunning??IsCodexRunning; }
    private static bool IsCodexRunning()
    {
        var processes=Process.GetProcessesByName("codex");
        try { return processes.Length>0; }
        finally { foreach(var process in processes) process.Dispose(); }
    }
    internal static string ResolveState(long? age,string? eventState,TimeSpan eventAge,bool running)
    {
        if(!running) return "offline";
        if(eventState=="working" && eventAge<TimeSpan.FromSeconds(600)) return "working";
        // A completed turn stays idle until a new start, rather than aging into offline.
        if(eventState=="idle") return "idle";
        return age is >=0 and <90 ? "working" : "idle";
    }
    private sealed class Cursor { internal long Position; internal byte[] Partial = []; internal bool? IsRoot; internal string Id="",Turn="",State=""; internal bool Completed; internal DateTimeOffset ActivityAt; }
    internal sealed record TaskActivity(string Id,string State,long UpdatedAt,string TurnId="",bool Completed=false);
    private TaskActivity[] _taskActivity=[];
    internal IReadOnlyList<TaskActivity> TaskActivities => Volatile.Read(ref _taskActivity);
    private readonly Dictionary<string, Cursor> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _completed = new(StringComparer.Ordinal);
    private readonly Queue<string> _completedOrder = new();
    private readonly DateTimeOffset _started = DateTimeOffset.UtcNow;
    private bool _primed;
    private DateTimeOffset _lastEvent;
    private string? _eventState;
    private long _completionAt, _sequence;
    private readonly object _sync = new();

    internal ToolState Capture(string root)
    {
        lock (_sync)
        {
            DateTimeOffset? newest = null;
            try
            {
                var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
                foreach (var path in Directory.Exists(root) ? Directory.EnumerateFiles(root, "*.jsonl", options) : [])
                {
                    try
                    {
                        var info = new FileInfo(path);
                        if (!_files.TryGetValue(path, out var cursor))
                        {
                            cursor = new() { Position = _primed ? 0 : info.Length };
                            _files[path] = cursor;
                        }
                        if(cursor.IsRoot is null)cursor.IsRoot=RootSession(path,cursor);
                        if (cursor.IsRoot == true && (newest is null || info.LastWriteTimeUtc > newest)) newest = info.LastWriteTimeUtc;
                        if (!_primed) {if(cursor.IsRoot==true&&info.LastWriteTimeUtc>=DateTime.UtcNow.AddMinutes(-10))PrimeTask(path,cursor);continue;}
                        if (info.Length < cursor.Position) { cursor.Position = info.Length; cursor.Partial = []; cursor.State="";cursor.Turn="";cursor.Completed=false;continue; }
                        if (cursor.IsRoot != true) { cursor.Position = info.Length; cursor.Partial = []; continue; }
                        if (info.Length > cursor.Position) ReadNew(path, cursor);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            _primed = true;
            var now = DateTimeOffset.UtcNow;
            bool running=_isRunning();
            Volatile.Write(ref _taskActivity,_files.Values.Where(c=>c.IsRoot==true&&c.Id.Length>0)
                .Select(c=>new TaskActivity(c.Id,running&&c.State=="working"&&now-c.ActivityAt<TimeSpan.FromMinutes(10)?"working":"idle",c.ActivityAt.ToUnixTimeSeconds(),c.Turn,c.Completed)).ToArray());
            long? age = newest is null ? null : Math.Max(0, (long)(now - newest.Value).TotalSeconds);
            var state = ResolveState(age,_eventState,now-_lastEvent,running);
            return new(state, age, _completionAt, _sequence);
        }
    }

    private static bool? RootSession(string path,Cursor cursor)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var bytes = new byte[Math.Min(file.Length, 32768)]; file.ReadExactly(bytes);
        foreach (var line in Encoding.UTF8.GetString(bytes).Split('\n'))
        {
            if (!line.Contains("session_meta", StringComparison.Ordinal)) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (Text(root, "type") != "session_meta" || !root.TryGetProperty("payload", out var payload)) continue;
                if(Guid.TryParse(Text(payload,"id"),out var id))cursor.Id=id.ToString();
                return !payload.TryGetProperty("source", out var source) || source.ValueKind != JsonValueKind.Object || !source.TryGetProperty("subagent", out _);
            }
            catch (JsonException) { }
        }
        return null;
    }

    private void ReadNew(string path, Cursor cursor)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        file.Position = cursor.Position;
        var bytes = new byte[(int)Math.Min(262144, file.Length - file.Position)];
        int count = file.Read(bytes); cursor.Position += count;
        var merged = new byte[cursor.Partial.Length + count];
        cursor.Partial.CopyTo(merged, 0); bytes.AsSpan(0, count).CopyTo(merged.AsSpan(cursor.Partial.Length));
        int start = 0;
        for (int i = 0; i < merged.Length; i++)
        {
            if (merged[i] != 10) continue;
            var line=Encoding.UTF8.GetString(merged,start,i-start);ObserveTask(cursor,line);Observe(path,line);start=i+1;
        }
        cursor.Partial = merged.Length - start <= 1024 * 1024 ? merged.AsSpan(start).ToArray() : [];
    }
    private static void PrimeTask(string path,Cursor cursor) {
        // Seed the per-thread state without replaying global completion alerts.
        // Read backwards until a lifecycle marker is found, so long tool output
        // cannot hide the current turn's start. Only event metadata is retained.
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        long end=Math.Min(file.Length,cursor.Position);string carry="";
        DateTimeOffset newest=default;
        while(end>0) {
            int size=(int)Math.Min(65536,end);end-=size;file.Position=end;
            byte[] bytes=new byte[size];file.ReadExactly(bytes);
            string text=Encoding.UTF8.GetString(bytes)+carry;int first=end>0?text.IndexOf('\n'):-1;
            carry=first<0&&end>0?text:text[..Math.Max(0,first)];
            if(carry.Length>1024*1024)carry=""; // Skip oversized non-event payloads.
            var lines=(first<0&&end>0?"":text[(first+1)..]).Split('\n');
            for(int i=lines.Length-1;i>=0;i--) {
                ObserveTask(cursor,lines[i]);
                if(cursor.ActivityAt>newest)newest=cursor.ActivityAt;
                if(cursor.State.Length>0){cursor.ActivityAt=newest;return;}
            }
        }
    }
    private static void ObserveTask(Cursor cursor,string line) {
        try {
            using var doc=JsonDocument.Parse(line);var root=doc.RootElement;
            if(!DateTimeOffset.TryParse(Text(root,"timestamp"),out var at)||at>DateTimeOffset.UtcNow.AddSeconds(5))return;
            if(at>cursor.ActivityAt)cursor.ActivityAt=at;
            if(Text(root,"type")!="event_msg"||!root.TryGetProperty("payload",out var payload))return;
            string kind=Text(payload,"type")??"",turn=Text(payload,"turn_id")??"";
            if(kind=="task_started"&&turn.Length>0){cursor.Turn=turn;cursor.State="working";cursor.Completed=false;}
            else if(kind is "task_complete" or "turn_aborted" && (cursor.Turn.Length==0||turn==cursor.Turn)) {cursor.State="idle";cursor.Turn=turn;cursor.Completed=kind=="task_complete"&&turn.Length>0;}
        }catch(JsonException) { }
    }

    private void Observe(string path, string line)
    {
        if (!line.Contains("event_msg", StringComparison.Ordinal)) return;
        try
        {
            using var doc = JsonDocument.Parse(line); var root = doc.RootElement;
            if (Text(root, "type") != "event_msg" || !root.TryGetProperty("payload", out var payload) ||
                !DateTimeOffset.TryParse(Text(root, "timestamp"), out var timestamp) || timestamp < _started || timestamp > DateTimeOffset.UtcNow.AddSeconds(5)) return;
            var type = Text(payload, "type");
            if (type is not ("task_started" or "task_complete" or "turn_aborted")) return;
            if (timestamp >= _lastEvent) { _lastEvent = timestamp; _eventState = type == "task_started" ? "working" : "idle"; }
            if (type != "task_complete") return;
            var key = path + ":" + (Text(payload, "turn_id") ?? timestamp.ToUnixTimeMilliseconds().ToString());
            if (!_completed.Add(key)) return;
            _completedOrder.Enqueue(key); while (_completedOrder.Count > 2048) _completed.Remove(_completedOrder.Dequeue());
            _sequence++; _completionAt = Math.Max(_completionAt, timestamp.ToUnixTimeSeconds());
        }
        catch (JsonException) { }
    }
    private static string? Text(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() : null;
}
