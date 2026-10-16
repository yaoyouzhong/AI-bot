using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5DoubaoMicrophoneSelfTest
{
    private sealed class Settings(string directory,string name) {
        internal readonly string Journal=Path.Combine(directory,name,"lease.json");
        internal string Selected="dji";
        internal bool Idle=true,LoseReply,JournalBeforeWrite=true;
        internal int Writes,Reads,RejectWrites,FailReads,IgnoreWrites;
        internal long Now;
        internal TaskCompletionSource? ReadGate;
        internal Action? AfterRead;
        internal Tab5DoubaoMicrophone Create()=>new(Journal,Call,()=>Idle,()=>Now);
        private async Task<JsonElement> Call(string method,object? payload) {
            if(method=="settings.update") {
                JournalBeforeWrite&=File.Exists(Journal);
                Writes++;
                if(RejectWrites>0){RejectWrites--;throw new IOException("settings pipe temporarily unavailable");}
                string id=JsonSerializer.SerializeToElement(payload).GetProperty("patch").GetProperty("voice").GetProperty("selectedMicrophoneId").GetString()!;
                if(IgnoreWrites>0)IgnoreWrites--;else Selected=id;
                if(LoseReply){LoseReply=false;Idle=false;throw new IOException("reply lost after applied update");}
                return JsonSerializer.SerializeToElement(new{});
            }
            Reads++;
            if(FailReads>0){FailReads--;throw new IOException("settings pipe unavailable");}
            if(ReadGate is not null)await ReadGate.Task;
            AfterRead?.Invoke();
            return JsonSerializer.SerializeToElement(new {selectedMicrophoneId=Selected,devices=new[]{
                new{id="cable",name="CABLE Output (VB-Audio Virtual Cable)"},new{id="dji",name="麦克风 (Wireless Mic Rx)"}}});
        }
    }
    private static void Check(bool value,string label){if(!value)throw new InvalidOperationException("Doubao microphone lease: "+label);}
    private static async Task MustFail(Func<Task> action) {
        try{await action();}catch(Exception ex) when(Tab5DoubaoMicrophone.IsRestoreError(ex)){return;}
        throw new InvalidOperationException("Expected microphone request to fail");
    }
    private static Task CheckBusyDraftAsync(string directory) {
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=> {
            try {
                foreach(int state in new[]{1,2,19,32,-1,0}) {
                    var settings=new Settings(directory,"preflight-"+state);int controls=0;
                    var voice=new Tab5DoubaoVoice(message=>{if(message!=Tab5DoubaoVoice.Query)controls++;return state;},()=>false);
                    // state=0 here models an unavailable RPC export, not an idle recognizer.
                    using var draft=new Tab5VoiceDraft(()=>"",_=>{controls++;return true;},settings.Create(),()=>state==0?null:voice);
                    MustFail(()=>draft.PrepareAsync(CancellationToken.None)).GetAwaiter().GetResult();
                    Check(!draft.Visible&&!draft.IsHandleCreated&&controls==0&&settings.Writes==0,
                        "busy/unavailable preflight must precede window creation, focus, keyboard toggle and microphone changes");
                }
                completion.SetResult();
            } catch(Exception ex){completion.SetException(ex);}
        }){IsBackground=true};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();return completion.Task;
    }
    internal static async Task RunAsync() {
        string directory=Path.Combine(Path.GetTempPath(),"tab5-mic-"+Guid.NewGuid().ToString("N"));
        try {
            var settings=new Settings(directory,"basic");var mic=settings.Create();
            await mic.AcquireAsync();Check(settings.Selected=="cable"&&mic.HasPendingRestore,"acquire journal");
            int reads=settings.Reads;mic.RetryRestore();await mic.Recovery;
            Check(settings.Selected=="cable"&&settings.Reads==reads,"timer must not restore an active take");
            await MustFail(mic.AcquireAsync);Check(settings.Selected=="cable","second acquire must not release the first take");
            mic.Release();await mic.Recovery;
            Check(settings.Selected=="dji"&&!mic.HasPendingRestore&&settings.JournalBeforeWrite,"release restores original device with committed journal");
            settings.Selected="";await mic.AcquireAsync();await mic.RestoreAsync();Check(settings.Selected=="","restore system-default selection");
            settings.Selected="dji";await mic.AcquireAsync();settings.Selected="manual";int before=settings.Writes;
            settings.Idle=false;mic.Release();await mic.Recovery;
            Check(settings.Selected=="manual"&&settings.Writes==before&&!mic.HasPendingRestore,"manual choice wins even while IME is busy");
            settings.Idle=true;settings.Selected="cable";before=settings.Writes;
            await mic.AcquireAsync();mic.Release();await mic.Recovery;
            Check(settings.Writes==before&&!mic.HasPendingRestore,"original CABLE choice is preserved");

            settings=new(directory,"shared-dji");mic=settings.Create();await mic.AcquireAsync("DJI");
            Check(settings.Selected=="dji"&&settings.Writes==0&&!mic.HasPendingRestore,"shared DJI leaves desktop microphone and recovery journal untouched");
            mic.Release();await mic.Recovery;
            Check(settings.Selected=="dji"&&settings.Writes==0,"shared DJI needs no channel restoration");
            settings.Selected="manual";await MustFail(()=>mic.AcquireAsync("dji"));await mic.Recovery;
            Check(settings.Selected=="manual"&&settings.Writes==0&&!mic.HasPendingRestore,"shared mode cannot silently change a different desktop microphone");

            settings=new(directory,"retry");mic=settings.Create();await mic.AcquireAsync();settings.RejectWrites=2;
            mic.Release();await mic.Recovery;
            Check(settings.Selected=="cable"&&mic.HasPendingRestore&&mic.Diagnostic.Contains("restoreFailures=1"),"failed restore remains pending");
            before=settings.Writes;
            for(int i=0;i<100;i++)mic.RetryRestore();await mic.Recovery;
            Check(settings.Writes==before,"timer respects retry backoff");
            settings.Now=1000;mic.RetryRestore();await mic.Recovery;
            Check(mic.HasPendingRestore&&settings.Selected=="cable","second transient error retains journal");
            settings.Now=3999;mic.RetryRestore();await mic.Recovery;before=settings.Writes;
            settings.Now=4000;mic.RetryRestore();await mic.Recovery;
            Check(settings.Selected=="dji"&&!mic.HasPendingRestore&&settings.Writes==before+1&&mic.Diagnostic.Contains("restoreFailures=0"),"automatic retry succeeds without a new voice request");

            settings=new(directory,"verify");mic=settings.Create();await mic.AcquireAsync();settings.IgnoreWrites=1;
            mic.Release();await mic.Recovery;
            Check(settings.Selected=="cable"&&mic.HasPendingRestore,"update acknowledgement alone cannot delete journal");
            settings.Now=1000;mic.RetryRestore();await mic.Recovery;
            Check(settings.Selected=="dji"&&!mic.HasPendingRestore,"read-back confirmation completes retry");

            settings=new(directory,"crash");mic=settings.Create();await mic.AcquireAsync();
            mic=settings.Create();settings.FailReads=1;mic.RetryRestore();await mic.Recovery;
            Check(mic.HasPendingRestore&&settings.Selected=="cable","startup failure retains old process journal");
            settings.Now=1000;mic.RetryRestore();await mic.Recovery;
            Check(settings.Selected=="dji"&&!mic.HasPendingRestore,"startup retries without acquiring or enabling voice");

            settings=new(directory,"uncertain"){LoseReply=true};mic=settings.Create();
            await MustFail(mic.AcquireAsync);await mic.Recovery;
            Check(settings.Selected=="cable"&&mic.HasPendingRestore&&settings.JournalBeforeWrite,"uncertain applied update is recoverable");
            settings.Idle=true;settings.Now=1000;mic.RetryRestore();await mic.Recovery;
            Check(settings.Selected=="dji"&&!mic.HasPendingRestore,"uncertain update recovers once IME is idle");

            settings=new(directory,"busy"){Idle=false};mic=settings.Create();
            await MustFail(mic.AcquireAsync);await mic.Recovery;
            Check(settings.Writes==0&&settings.Reads==0&&!mic.HasPendingRestore,"busy or unknown IME must not change microphone");
            settings.Idle=true;settings.AfterRead=()=>settings.Idle=false;
            await MustFail(mic.AcquireAsync);await mic.Recovery;
            Check(settings.Selected=="dji"&&settings.Writes==0&&!mic.HasPendingRestore,"busy transition between preflight and update cannot claim microphone");
            settings.AfterRead=null;settings.Idle=true;await mic.AcquireAsync();settings.Idle=false;before=settings.Writes;
            mic.Release();await mic.Recovery;
            Check(mic.HasPendingRestore&&settings.Writes==before,"pending IME worker or other recording blocks restore");
            settings.Idle=true;settings.Now=1000;mic.RetryRestore();await mic.Recovery;
            Check(settings.Selected=="dji"&&!mic.HasPendingRestore,"restore follows acknowledged idle");

            settings=new(directory,"serialize");mic=settings.Create();await mic.AcquireAsync();
            settings.ReadGate=new(TaskCreationOptions.RunContinuationsAsynchronously);mic.Release();
            var pending=mic.Recovery;mic.RetryRestore();Check(ReferenceEquals(pending,mic.Recovery),"one recovery operation at a time");
            var acquire=mic.AcquireAsync();Check(!acquire.IsCompleted,"new take waits for in-flight restore");
            settings.ReadGate.SetResult();await pending;await acquire;
            Check(settings.Selected=="cable"&&mic.HasPendingRestore,"completed restore cannot race and undo a newer take");
            mic.Release();await mic.Recovery;Check(settings.Selected=="dji"&&!mic.HasPendingRestore,"serialized take retains the correct original device");

            settings=new(directory,"corrupt");mic=settings.Create();Directory.CreateDirectory(Path.GetDirectoryName(settings.Journal)!);
            File.WriteAllText(settings.Journal,"{broken");mic.RetryRestore();await mic.Recovery;
            Check(mic.HasPendingRestore&&settings.Writes==0&&File.ReadAllText(settings.Journal)=="{broken","corrupt journal is retained without guessing an audio device");
            await CheckBusyDraftAsync(directory);
        } finally {
            if(Path.GetDirectoryName(Path.GetFullPath(directory))==Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))&&
                Path.GetFileName(directory).StartsWith("tab5-mic-",StringComparison.Ordinal)&&Directory.Exists(directory))Directory.Delete(directory,true);
        }
        Console.WriteLine("TAB5_DOUBAO_MICROPHONE_OK release/retry/backoff/read-back/startup/manual/busy/uncertain/serialization/corrupt journal; synthetic only");
    }
}
