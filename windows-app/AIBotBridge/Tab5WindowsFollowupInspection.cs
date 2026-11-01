using System.Diagnostics;
using System.Text.Json;

namespace AIBotBridge;

// Explicit read-only inspection. Never opens a conversation, writes a draft,
// sends a message, captures sound or prints user text/queue IDs.
internal static class Tab5WindowsFollowupInspection
{
    internal static async Task BindingAsync(string task,bool prepare) {
        if(!Guid.TryParse(task,out _))throw new ArgumentException("Specify a local task ID");
        AppProfileGuard.EnsureDesktopProfile();
        if(prepare)await Tab5CodexComposer.PrepareReaderAsync();
        Tab5CodexComposer.DraftSnapshot? previous=null;
        for(int sample=0;sample<3;sample++) {
            var clock=Stopwatch.StartNew();var snapshot=Tab5CodexComposer.Capture(task);
            Console.WriteLine(JsonSerializer.Serialize(new{sample,elapsedMs=clock.ElapsedMilliseconds,available=snapshot is not null,same=previous is not null&&previous==snapshot}));
            previous=snapshot;
        }
    }
    internal static async Task RunAsync(string? task=null) {
        task??=Environment.GetEnvironmentVariable("CODEX_THREAD_ID");
        if(!Guid.TryParse(task,out _))throw new ArgumentException("Specify a local Codex task ID for read-only inspection");
        var timer=Stopwatch.StartNew();var capture=Tab5CodexComposer.Capture(task);
        Console.WriteLine($"W29_UIA elapsedMs={timer.ElapsedMilliseconds}; targetAvailable={capture is not null}; question={capture?.Identity.StartsWith("question:")==true}; diagnostic={Tab5CodexComposer.EditorDiagnostic}");
        if(capture is not null)Console.WriteLine("W31_NATIVE_LAYOUT "+Tab5CodexComposer.NativeLayoutGuard(task,capture.Identity));
        Console.WriteLine("W29_UIA_IDENTITY "+JsonSerializer.Serialize(Tab5CodexComposer.InspectIdentity(task)));
        Console.WriteLine("W29_DRAFT_READ_PERF "+JsonSerializer.Serialize(Tab5CodexComposer.InspectDraftReadPerformance(task)));
        Console.WriteLine("W29_DRAFT_WRITE_READINESS "+JsonSerializer.Serialize(Tab5CodexComposer.InspectDraftWriteReadiness(task)));
        using(var readinessTimeout=new CancellationTokenSource(5000))
            Console.WriteLine("W29_SUBMIT_READINESS "+JsonSerializer.Serialize(await Tab5CodexComposer.InspectSubmitReadinessAsync(task,readinessTimeout.Token)));
        var timings=new List<long>();
        for(int sample=0;sample<3;sample++) {
            using var timeout=new CancellationTokenSource(2500);timer.Restart();
            try {
                var snapshot=await new Tab5CodexDesktop().SubmissionSnapshotAsync(task,timeout.Token);
                timings.Add(timer.ElapsedMilliseconds);
                var receipt=Tab5CodexDeliveryReceipt.Parse(snapshot.State,task,snapshot.Owner,"inspection","aibot-read-only-no-match-"+Guid.NewGuid());
                Console.WriteLine($"W29_IPC sample={sample+1}; elapsedMs={timer.ElapsedMilliseconds}; version11=True; receiptSchema={receipt is not null}");
            }catch(Exception ex) when(ex is IOException or InvalidOperationException or OperationCanceledException){Console.WriteLine($"W29_IPC sample={sample+1}; elapsedMs={timer.ElapsedMilliseconds}; unavailable={ex.GetType().Name}");}
        }
        using(var timeout=new CancellationTokenSource(1800)) {
            timer.Restart();const string expected="aibot-read-only-queue-probe-no-user-message";
            var server=await Tab5CodexDeliveryReceipt.ReadServerAsync(task,expected,timeout.Token);
            var local=Tab5CodexDeliveryReceipt.ReadLocal(task,expected);
            Console.WriteLine($"W29_QUEUE elapsedMs={timer.ElapsedMilliseconds}; localComplete={local is not null}; serverComplete={server is not null}; accountScope={Tab5CodexReadState.ReadIdentity() is not null}");
        }
        Console.WriteLine($"W29_INSPECTION_DONE successfulReads={timings.Count}; medianMs={(timings.Count==0?-1:timings.Order().ElementAt(timings.Count/2))}; no_mutations=True");
    }
}
