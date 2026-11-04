using System.Text.Json;
namespace AIBotBridge;

internal static class ClaudeIdentitySelfTest
{
    internal static void Run()
    {
        var tracker=new ClaudeIdentity();var a=Guid.NewGuid();var b=Guid.NewGuid();var p=Guid.NewGuid();var next=Guid.NewGuid();var baseline=DateTimeOffset.UtcNow;int tick=0;
        bool Send(string kind,Guid? session=null,Guid? prompt=null,string? reason=null,bool? error=null,int? turns=null) {
            var at=baseline.AddSeconds(++tick);
            var fields=new Dictionary<string,object?>{{"agent","claude"},{"event",kind},{"sessionId",session??a},{"promptId",prompt??p},{"emittedAt",at.ToUnixTimeMilliseconds()},{"notificationType","idle_prompt"},{"terminalReason",reason},{"isError",error},{"numTurns",turns}};
            using var doc=JsonDocument.Parse(JsonSerializer.Serialize(fields));return tracker.Record(doc.RootElement,at);
        }
        void Check(bool value,string name){if(!value)throw new InvalidOperationException("Claude identity: "+name);}
        Check(!Send("Stop"),"no terminal event without start");
        Check(Send("UserPromptSubmit"),"start");Check(Send("PermissionRequest"),"wait");
        Check(tracker.Select(baseline)!.NeedsInput,"same turn wait");
        Check(tracker.Select(baseline.AddSeconds(tick+300)) is {NeedsInput:false,State:"unknown"},"waiting expires after five minutes without completion");
        Check(Send("Stop")&&Send("StopFailure")&&Send("Notification"),"failure then idle");
        Check(tracker.Select(baseline) is {Outcome:"failed",CompletionAt:0},"idle does not erase failure");
        Check(Send("UserPromptSubmit",prompt:next),"new turn");
        Check(!Send("StopFailure"),"old turn failure rejected");
        Check(Send("SessionEnd",prompt:next),"print process exits before final result consumed");
        Check(Send("TurnResult",prompt:next,reason:"completed",error:false,turns:1),"final result");
        var complete=tracker.Select(baseline)!;Check(complete.Tool.CompletionActive,"explicit success");
        Check(complete.Activity.ToUnixTimeMilliseconds()==baseline.AddSeconds(tick).ToUnixTimeMilliseconds(),"final activity belongs to same selected turn");
        Check(Send("TurnResult",prompt:next,reason:"completed",error:false,turns:1),"duplicate final");
        Check(tracker.Select(baseline)!.CompletionSequence==complete.CompletionSequence,"exactly once");
        tracker.Acknowledge();Check(!tracker.Select(baseline)!.Tool.CompletionActive,"ack");
        Check(Send("UserPromptSubmit",session:b),"other session");Check(tracker.Select(baseline)!.SessionId==b,"active session wins");
        Check(Send("TurnInterrupted",session:b)&&Send("Notification",session:b),"interrupt then idle");
        Check(tracker.Select(baseline) is {Outcome:"interrupted",CompletionAt:0},"interruption retained");
        var old=new ClaudeIdentity.Selection(a,p,baseline,"working","running",false,0,0);
        var done=new ClaudeIdentity.Selection(a,next,baseline.AddSeconds(1),"idle","completed",false,1,0,"opaque-turn");
        Check(ClaudeIdentity.Resolve(old,[done])?.Outcome=="completed","owned exact result settles older same-session hook");
        Check(ClaudeIdentity.Resolve(old with {Activity=baseline.AddSeconds(2)},[done])?.OwnedTurn is null,"new external prompt supersedes older owned result");
        Check(ClaudeIdentity.Resolve(old with {SessionId=b,NeedsInput=true},[done])?.SessionId==b,"other waiting session preserved");
        var raw=JsonSerializer.SerializeToUtf8Bytes(new {hook_event_name="UserPromptSubmit",session_id=a,prompt_id=p,prompt="private",cwd="private",transcript_path="private",session_title="private"});
        var projected=ClaudeHook.Project(raw,baseline)!;
        Check(projected.Count==5&&!JsonSerializer.Serialize(projected).Contains("private"),"allowlist privacy");
        using var invalid=JsonDocument.Parse("{\"agent\":\"claude\",\"event\":\"Stop\",\"sessionId\":\"bad\"}");
        Check(!tracker.Record(invalid.RootElement),"malformed metadata");
        Console.WriteLine("CLAUDE_IDENTITY_OK synthetic lifecycle and privacy");
    }
}
