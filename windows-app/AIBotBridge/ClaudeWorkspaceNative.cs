using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;

internal sealed partial class ClaudeWorkspace
{
    private readonly ClaudeHarnessAdapters _harnesses;
    private readonly ClaudeSessionSources _sources;
    private readonly bool _nativeClients;
    private readonly Func<Guid,IClaudeNativeSession?> _nativeFactory;
    internal Action<string,ClaudeNativeSend.Result>? DiagnosticResult {get;init;}
    private readonly bool _customNativeFactory;
    private sealed record NativeView(Guid Session,Guid Prompt,string Turn,long Started,long Ended,string State,string Outcome,bool Running,bool Waiting,bool CanSend,DateTimeOffset Observed,bool CanAppend=false,bool CanInterrupt=false,string[]? EarlierReplies=null);
    private readonly Dictionary<string,NativeView> _nativeViews=[];
    private readonly Dictionary<string,(string Turn,string Outcome)> _nativeCompletionViews=[];
    private readonly HashSet<string> _nativeAnnounced=[],_nativeIssued=[];
    private readonly HashSet<string> _nativeProbing=[];
    private readonly Dictionary<string,long> _nativeObservations=[];
    private long _nativeObservation;
    private int _nativeRefreshing;
    private (ClaudeCatalog.Entry Entry,long QueuedAt)? _selectedProbe;
    private bool _selectedWorker;
    private string? _nativeWriteTask;
    private long NextNativeObservation()=>Interlocked.Increment(ref _nativeObservation);
    // A session's input capability does not depend on which reply was last read.
    // Null explicitly revokes stale/unavailable controls in the bounded catalog.
    // This is display metadata; SendNativeAsync still validates the live target.
    private object? NativeControl(string task)
    {
        if(Environment.TickCount64-_catalogAt>30000||!_nativeViews.TryGetValue(task,out var view)||
            DateTimeOffset.UtcNow-view.Observed>TimeSpan.FromSeconds(30))return null;
        return new{taskId=task,turnId=view.Turn,running=view.Running,waiting=view.Waiting,
            canSend=view.CanSend,canSteer=view.CanAppend,canInterrupt=view.CanInterrupt};
    }
    private bool ConfirmedNativeStop(string task,string turn){
        Tab5CodexJournal[] journals;
        lock(_sync)journals=_journals.Values.ToArray();
        return journals.Any(journal=>journal.ConfirmsNativeStop(task,turn));
    }
    private NativeView? ObserveNative(ClaudeCatalog.Entry entry,out ClaudeSessionSource? source,ClaudeHistory.Value? suppliedHistory=null,IClaudeNativeSession? suppliedClient=null)
    {
        source=null;
        try {
            var client=suppliedClient??_nativeFactory(entry.Session);if(client is null)return null;
            string runtime=client.State;bool waiting=runtime=="waiting",running=waiting||runtime is "busy" or "working",idle=runtime=="idle";
            source=client.Source;
            var history=suppliedHistory??ReadNativeHistory(entry,client);var latest=history.Turns.LastOrDefault();
            if(latest is null){bool canSend=idle&&client.CanAddressInput;
                return client.State!=runtime?null:new(entry.Session,Guid.Empty,"",0,0,idle?"idle":"unknown","unknown",running,waiting,canSend,DateTimeOffset.UtcNow);}
            string turn=(string)latest["id"];Guid prompt=Guid.Empty;
            foreach(string id in history.Accepted)if(Guid.TryParse(id,out var user)&&Turn(entry,id)==turn){prompt=user;break;}
            bool stopped=ConfirmedNativeStop(entry.Alias,turn);
            string outcome=stopped?"interrupted":running?"inProgress":(string)latest["status"];
            long started=Convert.ToInt64(latest["startedAt"]),ended=latest.TryGetValue("endedAt",out var at)?Convert.ToInt64(at):0;
            string state=waiting?"waiting":running?"working":idle&&outcome is "completed" or "interrupted"?"idle":"unknown";
            bool canWrite=client.CanAddressInput;
            bool targetReady=canWrite&&running&&!waiting&&prompt!=Guid.Empty&&(client is not ClaudeSessionApi api||api.IsRunning(prompt));
            // Only an accepted human prompt in this session proves that its
            // earlier public replies have been followed by the user's action.
            string[] earlier=prompt==Guid.Empty?[]:history.Turns.SkipLast(1).Where(t=>
                (string)t["status"]=="completed"&&t["items"] is object[] {Length:>0})
                .Select(t=>(string)t["id"]).ToArray();
            return client.State!=runtime?null:new(entry.Session,prompt,turn,started,ended,state,outcome,running,waiting,idle&&canWrite,DateTimeOffset.UtcNow,targetReady&&client.CanAppend,targetReady&&client.CanInterrupt,earlier);
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or
            System.Runtime.InteropServices.COMException or System.Windows.Automation.ElementNotAvailableException){return null;}
    }
    private void RefreshNative()
    {
        if(_disposed||Interlocked.Exchange(ref _nativeRefreshing,1)!=0)return;
        try{
            long observation=NextNativeObservation();
            var live=_customNativeFactory?null:_harnesses.LiveSessions();
            var entries=_catalog.Snapshot();
            var known=entries.Select(e=>e.Alias).ToHashSet();
            lock(_sync){
                foreach(string task in _nativeObservations.Where(p=>p.Value<=observation&&!known.Contains(p.Key)).Select(p=>p.Key).ToArray()){
                    _nativeObservations.Remove(task);_nativeViews.Remove(task);_nativeCompletionViews.Remove(task);
                }
            }
            if(live is not null)foreach(var entry in entries.Where(e=>!live.Contains(e.Session))){
                // Revoke a former owner promptly; don't overwrite an exact
                // observation that began after this live-owner snapshot.
                bool observed;lock(_sync)observed=_nativeViews.ContainsKey(entry.Alias)||_nativeProbing.Contains(entry.Alias);
                if(observed)PublishNativeObservation(entry,null,observation,trackCompletion:false);
            }
            var active=entries.Where(e=>live is null||live.Contains(e.Session));
            Parallel.ForEach(active,new ParallelOptions{MaxDegreeOfParallelism=2},entry=>ProbeNative(entry,null));
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or CryptographicException){}
        finally{Volatile.Write(ref _nativeRefreshing,0);}
    }
    private void QueueSelectedProbe(ClaudeCatalog.Entry entry){
        lock(_sync){
            if(_disposed)return;
            _selectedProbe=(entry,Environment.TickCount64);
            if(_selectedWorker)return;
            _selectedWorker=true;
        }
        _=Task.Run(()=>{
            while(true){
                (ClaudeCatalog.Entry Entry,long QueuedAt)? next;
                lock(_sync){
                    next=_selectedProbe;_selectedProbe=null;
                    if(_disposed||next is null){_selectedWorker=false;return;}
                }
                ProbeNative(next.Value.Entry,next.Value.QueuedAt);
            }
        });
    }
    private void ProbeNative(ClaudeCatalog.Entry entry,long? queuedAt){
        if(_disposed||!_enabled())return;
        lock(_sync)if(_nativeWriteTask==entry.Alias||!_nativeProbing.Add(entry.Alias))return;
        try{
            long started=queuedAt??Environment.TickCount64,observation=NextNativeObservation();
            var view=ObserveNative(entry,out var source);
            PublishNativeObservation(entry,view,observation,trackCompletion:true,source);
            long elapsed=Environment.TickCount64-started;
            if(queuedAt.HasValue||elapsed>=1000)Tab5LocalDiagnostics.Write($"CLAUDE_CONTROL selected={queuedAt.HasValue} elapsedMs={elapsed} available={view?.CanSend==true||view?.CanAppend==true}");
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or CryptographicException){}
        finally{lock(_sync)_nativeProbing.Remove(entry.Alias);}
    }
    private void PublishNativeObservation(ClaudeCatalog.Entry entry,NativeView? info,long observation,bool trackCompletion,ClaudeSessionSource? source=null){
        if(_disposed||!_enabled()||_catalog.Resolve(entry.Alias)?.Session!=entry.Session)return;
        string task=entry.Alias;
        Tab5CodexJournal[] journals;
        lock(_sync){
            if(_nativeObservations.GetValueOrDefault(task)>observation)return;
            _nativeObservations[task]=observation;
            if(source is not null){_sources.Observe(task,source);_sources.Save();}
            if(info is null){_nativeViews.Remove(task);if(_draftTask==task)_control=null;return;}
            _nativeViews[task]=info;
            if(_draftTask==task)_control=NativeControl(task);
            if(!trackCompletion)return;
            // Reply reads and temporary loss of input ownership update live
            // controls, but must not consume or erase the completion edge.
            bool known=_nativeCompletionViews.TryGetValue(task,out var previous);
            _nativeCompletionViews[task]=(info.Turn,info.Outcome);
            if(info.Prompt!=Guid.Empty&&info.EarlierReplies is {Length:>0} earlier){
                if(_ready.TryGetValue(task,out var pending)){
                    if(pending!=info.Turn&&earlier.Contains(pending))_read[task]=pending;
                }else if(_read.GetValueOrDefault(task)!=info.Turn)_read[task]=earlier[^1];
            }
            if(info.Outcome!="completed"||info.Running||info.Turn.Length==0)return;
            bool changed=known&&(previous.Turn!=info.Turn||previous.Outcome!="completed");
            bool notify=changed||_nativeIssued.Contains(info.Turn);
            if(_nativeAnnounced.Add(info.Turn)&&notify)_ready[task]=info.Turn;
            journals=_journals.Values.ToArray();
        }
        foreach(var journal in journals)journal.Finish(task,info.Turn);
    }
    private ClaudeIdentity.Selection? NativeSelection(ClaudeIdentity.Selection? bound,DateTimeOffset now)
    {
        lock(_sync){var choices=new List<ClaudeIdentity.Selection>();
            foreach(var pair in _nativeViews){var info=pair.Value;if(info.Prompt==Guid.Empty)continue;
                bool stale=Environment.TickCount64-_catalogAt>30000,unread=_ready.GetValueOrDefault(pair.Key)==info.Turn&&_read.GetValueOrDefault(pair.Key)!=info.Turn;
                long stamp=info.Running?now.ToUnixTimeSeconds():Math.Max(info.Started,info.Ended);
                var activity=stamp>0?DateTimeOffset.FromUnixTimeSeconds(stamp):info.Observed;
                choices.Add(new(info.Session,info.Prompt,activity,stale?"unknown":info.Waiting?"working":info.State,info.Outcome,
                    !stale&&info.Waiting,!stale&&unread&&info.Outcome=="completed"?info.Ended:0,0,info.Turn));
            }
            return ClaudeIdentity.Resolve(bound,choices);
        }
    }
    private ClaudeHistory.Value ReadNativeHistory(ClaudeCatalog.Entry entry,IClaudeNativeSession client)=>
        ClaudeHistory.Read(_root,entry.Session,Key(),client is ClaudeNativeCode code?code.IsNewPanel:client.IsVerifiedBlank,backgroundImages:client.UsesFileImageReferences);
    internal bool MatchesDiagnosticPrompt(string task,string turn,string prompt){
        var entry=_catalog.Resolve(task);
        if(entry is null||DiagnosticSession!=entry.Session)return false;
        var history=ClaudeHistory.Read(_root,entry.Session,Key());
        string digest=ClaudeNativeContent.Digest(prompt,[]);
        return !history.Limited&&history.Accepted.Any(id=>Turn(entry,id)==turn&&history.InputDigests.GetValueOrDefault(id)==digest);
    }
    private string? NativeReceipt(ClaudeCatalog.Entry entry,Tab5SendRecord record)
    {
        try {
            var client=_nativeFactory(entry.Session);
            if(record.ResolutionSource=="session_api_pending"&&client is ClaudeSessionApi api&&client.Scope()==record.NativeScope&&Guid.TryParse(record.Id,out var request)){
                var receipt=api.Receipt(request);
                if(record.Operation=="interrupt")return receipt?.State=="stopped"?record.ExpectedTurn:null;
                if(receipt?.State=="accepted"&&receipt.User==request)return Turn(entry,record.Id);
            }
            // The per-device journal reserves the caller's UUID before writing.
            // The VS Code session API uses exactly that UUID for its native user
            // row. This evidence remains available after the original host exits.
            if(record.ResolutionSource=="session_api_pending"&&record.Operation!="interrupt"&&
                record.NativeReceipt is {} saved&&saved.Session==entry.Session&&saved.Scope==record.NativeScope&&
                record.NativeScope.Length>0&&Guid.TryParse(record.Id,out var requestId)){
                var transcript=ClaudeHistory.Read(_root,entry.Session,Key());
                if(!saved.Limited&&!transcript.Limited&&transcript.Accepted.Contains(record.Id)&&saved.Known.IsSubsetOf(transcript.Accepted)&&
                    !saved.Known.Contains(record.Id)&&transcript.InputDigests.GetValueOrDefault(record.Id)==saved.InputDigest)
                    return Turn(entry,requestId.ToString());
            }
            if(client is null||record.ResolutionSource=="session_api_pending"||record.Operation=="interrupt")return null;
            var history=ReadNativeHistory(entry,client);
            var baseline=record.NativeReceipt;if(baseline is null||baseline.Session!=entry.Session)return null;
            var observed=ClaudeNativeReceipt.Observe(history,baseline,client.Scope());
            return observed.Confirm(baseline) is {} user?Turn(entry,user.ToString()):null;
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception){return null;}
    }
    private bool CompletedBeforeStop(ClaudeCatalog.Entry entry,Tab5SendRecord record){
        if(record.Operation!="interrupt"||record.NativeScope.Length==0||!Guid.TryParse(record.ExpectedTurn,out _))return false;
        try{
            if(_nativeFactory(entry.Session) is not ClaudeBackgroundApi client||client.Scope()!=record.NativeScope)return false;
            var history=ReadNativeHistory(entry,client);
            return !history.Limited&&history.Turns.Any(turn=>(string)turn["id"]==record.ExpectedTurn&&
                (string)turn["status"]=="completed"&&Convert.ToInt64(turn["endedAt"])>0);
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception){return false;}
    }
    private async Task<(int Status,object Body)> SendNativeAsync(ClaudeCatalog.Entry entry,Tab5CodexJournal journal,string device,string request,string operation,string text,string expected,string[] images,CancellationToken token,Func<bool>? authorized)
    {
        bool Allowed()=>_enabled()&&!_disposed&&authorized?.Invoke()==true;
        if(!Allowed())return(403,new{error="unauthorized"});
        var client=_nativeFactory(entry.Session);if(client is null)return(409,new{error="native_session_unavailable"});
        if(!client.CanAddressInput)return(409,new{error="native_session_api_unavailable",deliveryFinal=true});
        if(operation=="steer"&&!client.CanAppend||operation=="interrupt"&&!client.CanInterrupt)return(409,new{error="native_operation_unavailable"});
        if(images.Length>0&&!client.CanImages)return(409,new{error="native_images_unavailable"});
        bool dispatched=false;
        var record=new Tab5SendRecord(request,entry.Alias,Tab5CodexJournal.Fingerprint(entry.Alias,text,operation,expected,images),operation,expected,"dispatching","",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),Device:device,ResolutionSource:"native_pending");
        ClaudeNativeReceipt? baseline=null;
        try {
            string[] paths=operation=="interrupt"?[]:_images.Resolve(device,entry.Alias,images,true);
            string[] digests=paths.Select(ClaudeNativeContent.FileDigest).ToArray();
            string scope=client.Scope();var original=ReadNativeHistory(entry,client);string turnBefore=(string?)original.Turns.LastOrDefault()?.GetValueOrDefault("id")??"";
            if(operation!="send"&&(expected.Length==0||turnBefore!=expected||client.State is not ("busy" or "working")))return(409,new{error="turn_changed"});
            // The caller verified the new terminal before staging. Typing into its
            // composer does not create a transcript until Enter is accepted.
            ClaudeHistory.Value ReadAttemptHistory()=>client is ClaudeNativeTerminal&&!original.Limited&&original.Turns.Count==0
                ?ClaudeHistory.Read(_root,entry.Session,Key(),()=>client.Scope()==scope,backgroundImages:true)
                :ReadNativeHistory(entry,client);
            bool Current(){var now=ReadAttemptHistory();return client.Scope()==scope&&((string?)now.Turns.LastOrDefault()?.GetValueOrDefault("id")??"")==turnBefore;}
            ClaudeHistory.Value History(){var history=ReadAttemptHistory();if(operation!="interrupt")baseline??=ClaudeNativeReceipt.Capture(history,entry.Session,scope,text,digests);return history;}
            void Dispatch(){if(!Allowed()||!Current()||operation!="interrupt"&&baseline is null)throw new IOException("Native request authorization or baseline changed");record=record with{NativeReceipt=baseline,NativeScope=scope,ResolutionSource=client is ClaudeSessionApi?"session_api_pending":record.ResolutionSource};journal.Save(record,operation=="interrupt"?null:text,images);dispatched=true;}
            Guid? parent=original.Accepted.FirstOrDefault(id=>Turn(entry,id)==turnBefore) is {} raw&&Guid.TryParse(raw,out var guid)?guid:null;
            var result=await client.ExecuteAsync(new(operation,text,paths,digests,Current,Guid.Parse(request),parent),History,Dispatch,Allowed,token);
            DiagnosticResult?.Invoke(request,result);
            if(result.Stopped&&operation=="interrupt") {
                journal.Save(record with{State="completed",Status=202,TurnId=expected,ResolutionSource="native_interrupt_confirmed"});
                return(202,new{status="submitted",taskId=entry.Alias,requestId=request,turnId=expected});
            }
            if(result.Delivered&&result.User is {} user){
                string turn=Turn(entry,user.ToString());journal.Save(record with{State="accepted",Status=202,TurnId=turn,NativeReceipt=null});
                lock(_sync)_nativeIssued.Add(turn);
                return(202,new{status="submitted",taskId=entry.Alias,requestId=request,turnId=turn});
            }
            record=record with{NativeFailureStage=result.FailureStage??result.Error};
            if(dispatched&&!result.SubmitAttempted&&operation!="interrupt"){
                // The adapter returned without invoking Submit. Keep both drafts
                // and the request ID, so a receipt query can unlock the device.
                string reason=result.Error=="desktop_draft_present"?"desktop_draft_present":"composer_changed";
                journal.Save(record with{State="rejected",Status=409,Error=reason,
                    ResolutionSource="native_submit_not_invoked",NativeReceipt=null});
                return(409,new{error=reason,deliveryFinal=true});
            }
            if(dispatched){
                journal.Save(record);
                return(504,new{error="delivery_unknown",canConfirmDelivered=journal.CanConfirm(record,device)});
            }
            int status=result.Error=="unauthorized"?403:result.Error=="native_input_unsupported"?400:409;
            journal.Save(record with{State="rejected",Status=status,Error=result.Error,ResolutionSource="native_submit_not_invoked"});
            return(status,new{error=result.Error,deliveryFinal=true});
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or CryptographicException or JsonException or ArgumentException or InvalidOperationException or OperationCanceledException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException or System.Windows.Automation.ElementNotAvailableException){
            return(dispatched?504:503,new{error=dispatched?"delivery_unknown":"native_session_unavailable"});
        }
    }
}
