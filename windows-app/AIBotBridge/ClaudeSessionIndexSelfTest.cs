using System.Text.Json;
using System.Text;

namespace AIBotBridge;

internal static class ClaudeSessionIndexSelfTest
{
    internal static void Run()
    {
        ClaudeIdentitySelfTest.Run();
        ClaudePublicRepliesSelfTest.Run();
        var root=Path.Combine(Path.GetTempPath(),"aibot-claude-index-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            var now=DateTimeOffset.UtcNow;var first=Guid.NewGuid();var second=Guid.NewGuid();var child=Guid.NewGuid();
            string Event(Guid id,DateTimeOffset at)=>JsonSerializer.Serialize(new {type="user",sessionId=id,timestamp=at,message=new {content="private fixture"}})+"\n";
            string Name(Guid id,string name)=>JsonSerializer.Serialize(new {type="custom-title",sessionId=id,customTitle=name})+"\n";
            var firstFile=Path.Combine(root,first+".jsonl");var secondFile=Path.Combine(root,second+".jsonl");
            File.WriteAllText(firstFile,Event(first,now.AddDays(-2))+Name(first,"Older"));
            var sub=Path.Combine(root,"subagents");Directory.CreateDirectory(sub);File.WriteAllText(Path.Combine(sub,child+".jsonl"),Event(child,now));
            var index=new ClaudeSessionIndex(root);Check(index.Scan(now)?.Title=="Older","old session and subagent exclusion");
            File.WriteAllText(secondFile,Event(second,now.AddSeconds(-10))+Name(first,"Wrong session"));
            var current=index.Scan(now);Check(current?.Id==second&&current.Title is null,"new unnamed session must not borrow old title");
            Check(index.Scan(now,first)?.Title=="Older","explicit identity selects the older session");
            Check(index.Scan(now,child) is null&&index.Scan(now,Guid.NewGuid()) is null,"unknown or subagent identity must not fall back");
            File.AppendAllText(secondFile,Name(second,"Current"));Check(index.Scan(now)?.Title=="Current","title update");
            File.Delete(secondFile);Check(index.Scan(now)?.Id==first,"deleted session invalidation");
            Check(index.Scan(now,second) is null,"deleted identity must not fall back");
            var partial=Guid.NewGuid();var partialFile=Path.Combine(root,partial+".jsonl");
            File.WriteAllText(partialFile,Event(partial,now.AddSeconds(-1)).TrimEnd('\n'));
            Check(index.Scan(now)?.Id==first,"incomplete final line ignored");
            File.AppendAllText(partialFile,"\n");Check(index.Scan(now)?.Id==partial,"completed final line rechecked");
            File.Delete(partialFile);
            var future=Guid.NewGuid();File.WriteAllText(Path.Combine(root,future+".jsonl"),Event(future,now.AddMinutes(5)));
            Check(index.Scan(now)?.Id==first,"future observation rejected");Check(index.Scan(now.AddMinutes(6))?.Id==future,"future observation rechecked");
            var previous=Encoding.UTF8.GetBytes("{\"data\":{\"claudeSession\":{\"title\":\"old private title\"}}}");
            var incoming=JsonSerializer.SerializeToUtf8Bytes(new {data=new {claudeSession=(object?)null,filler=new string('x',40000)}});
            var recovered=Tab5StatusRecovery.Build(incoming,previous,out _);
            using(var doc=JsonDocument.Parse(recovered))Check(doc.RootElement.GetProperty("data").GetProperty("claudeSession").ValueKind==JsonValueKind.Null,"recovery must not restore a revoked title");
            var signals=new ActivitySignals();var idle=new ToolState("idle",0);
            signals.Record("claude","PermissionRequest",at:now);signals.Record("claude","SubagentStop",at:now);signals.Record("claude","Stop",at:now);
            Check(signals.Apply("claude",idle,now).NeedsInput&&signals.Apply("claude",idle,now).State=="unknown"&&!signals.Apply("claude",idle,now).CompletionActive,"unbound Claude stop remains unknown and preserves waiting without completion");
            signals.Record("claude","UserPromptSubmit",at:now);Check(!signals.Apply("claude",idle,now).NeedsInput,"new prompt clears waiting");
            Console.WriteLine("CLAUDE_SESSION_INDEX_OK synthetic files only");
        } finally {Directory.Delete(root,true);}
    }
    private static void Check(bool value,string name){if(!value)throw new InvalidOperationException("Claude session test: "+name);}
}
