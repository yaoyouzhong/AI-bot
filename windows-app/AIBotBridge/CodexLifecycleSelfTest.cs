using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class CodexLifecycleSelfTest
{
    internal static void Run()
    {
        var folder = Path.Combine(Environment.CurrentDirectory, "artifacts", "lifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "root.jsonl");
        const string meta = "{\"type\":\"session_meta\",\"payload\":{\"source\":\"cli\"}}\n";
        string Event(string type, string turn, DateTimeOffset time) => JsonSerializer.Serialize(new { type = "event_msg", timestamp = time, payload = new { type, turn_id = turn } }) + "\n";
        void Append(string target, ReadOnlySpan<byte> bytes) { using var file = new FileStream(target, FileMode.Append, FileAccess.Write); file.Write(bytes); }
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
        File.WriteAllText(path, meta + Event("task_complete", "old", DateTimeOffset.UtcNow.AddMinutes(-2)));
        var tracker = new CodexLifecycleTracker();
        Check(tracker.Capture(folder).CompletionSequence == 0, "Startup replayed historical completion.");
        Append(path, Encoding.UTF8.GetBytes(Event("task_started", "new", DateTimeOffset.UtcNow)));
        Check(tracker.Capture(folder).State == "working", "Explicit start not recognized.");
        var completed = Encoding.UTF8.GetBytes(Event("task_complete", "new", DateTimeOffset.UtcNow));
        Append(path, completed.AsSpan(0, completed.Length / 2));
        Check(tracker.Capture(folder).CompletionSequence == 0, "Partial JSON generated completion.");
        Append(path, completed.AsSpan(completed.Length / 2));
        var done = tracker.Capture(folder);
        Check(done.CompletionSequence == 1 && done.State == "idle", "Completed turn remains working.");
        Append(path, completed); Check(tracker.Capture(folder).CompletionSequence == 1, "Duplicate completion counted twice.");
        File.WriteAllText(Path.Combine(folder, "child.jsonl"), "{\"type\":\"session_meta\",\"payload\":{\"source\":{\"subagent\":{}}}}\n" + Event("task_complete", "child", DateTimeOffset.UtcNow));
        Check(tracker.Capture(folder).CompletionSequence == 1, "Subagent generated main completion.");
        Append(path, Encoding.UTF8.GetBytes(Event("task_complete", "another", DateTimeOffset.UtcNow)));
        Check(tracker.Capture(folder).CompletionSequence == 2, "Distinct same-second turn was lost.");
        File.WriteAllText(path, meta); Check(tracker.Capture(folder).CompletionSequence == 2, "Truncation replayed old events.");
        Console.WriteLine("CODEX_LIFECYCLE_SELF_TEST_OK startup/explicit-state/partial/duplicate/subagent/same-second/truncate; no sound played");
    }
}
