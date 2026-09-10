using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal sealed class CodexLifecycleTracker
{
    private sealed class Cursor { internal long Position; internal byte[] Partial = []; internal bool? IsRoot; }
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
                        cursor.IsRoot ??= RootSession(path);
                        if (cursor.IsRoot == true && (newest is null || info.LastWriteTimeUtc > newest)) newest = info.LastWriteTimeUtc;
                        if (!_primed) continue;
                        if (info.Length < cursor.Position) { cursor.Position = info.Length; cursor.Partial = []; continue; }
                        if (cursor.IsRoot != true) { cursor.Position = info.Length; cursor.Partial = []; continue; }
                        ReadNew(path, cursor);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            _primed = true;
            var now = DateTimeOffset.UtcNow;
            long? age = newest is null ? null : Math.Max(0, (long)(now - newest.Value).TotalSeconds);
            var state = age is null ? "offline" : age < 90 ? "working" : age <= 900 ? "idle" : "offline";
            if (_eventState is not null && now - _lastEvent < TimeSpan.FromSeconds(_eventState == "working" ? 600 : 60)) state = _eventState;
            return new(state, age, _completionAt, _sequence);
        }
    }

    private static bool? RootSession(string path)
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
            Observe(path, Encoding.UTF8.GetString(merged, start, i - start)); start = i + 1;
        }
        cursor.Partial = merged.Length - start <= 1024 * 1024 ? merged.AsSpan(start).ToArray() : [];
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
