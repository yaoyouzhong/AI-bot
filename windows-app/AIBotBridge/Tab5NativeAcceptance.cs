using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;

// A diagnostic instance serves only paired USB. Synthetic draft setup happens on
// the host; send/steer/interrupt must arrive through the device's authenticated RPC.
internal abstract class Tab5UsbDiagnostic
{
    internal abstract ClaudeWorkspace CreateWorkspace(Func<bool> enabled);
    internal abstract Task PrepareAsync(Tab5Service service,string device,CancellationToken token);
    internal abstract bool Allows(string device,string client,string op,JsonElement root);
    internal abstract void Observe(string op,JsonElement root,(int Status,object Body) reply);
    internal abstract Task RunAsync(Tab5AutomationRunner.Backend backend,CancellationToken token);
}

internal sealed class Tab5NativeAcceptance : Tab5UsbDiagnostic
{
    internal const string PhotoPrompt="请查看附件，只回复图片主色（一个中文颜色词）。本次测试后续如果收到单独字母 q 或 w，请从 1 到 500 逐行列出数字。全程只回复文字，不调用工具。";
    private const string CountPrompt="请从 1 数到 500，每行写一个数字。收到追加的 w 后，从 501 继续逐行列数到 1000。全程只回复文字，不调用工具。";
    private const string ActivePrompt="请从501逐行数到1500；收到w后从1501逐行数到2000。只回复数字，不调用工具。";
    internal const string ForegroundWaitPrompt="请用 Bash 执行 sleep 35，结束后回复 WAIT_DONE。收到追加 w 时也先执行 sleep 35，再回复 APPEND_DONE。只运行这个等待命令。";
    private const string ApiPrompt="这是 TAB5 接口验收。请从1到600逐行写出整数及其平方。收到追加 w 时，继续从601逐行写到1000。只输出算式，不调用工具。";
    internal sealed record Step(string Name,string Operation,string Text,string ExpectedTurn,string[] Images){
        internal bool Matches(string operation,JsonElement request)=>operation==Operation&&
            Tab5NativeAcceptance.Text(request,"message")==Text&&
            Tab5NativeAcceptance.Text(request,"expectedTurn")==ExpectedTurn&&
            Tab5NativeAcceptance.Images(request).SequenceEqual(Images);
        internal bool AcceptedTurn(string turn)=>Guid.TryParse(turn,out _)&&
            (Operation=="interrupt"?ExpectedTurn==turn:ExpectedTurn!=turn);
    }
    private readonly string _directory,_host,_scope;
    private readonly object _gate=new();
    private readonly Dictionary<string,string> _evidence=[];
    private readonly List<object> _events=[];
    private readonly List<object> _nativeObservations=[];
    private Guid _session;
    private Tab5Service? _service;
    private string _task="",_device="",_image="",_draft="",_colorReplyTurn="";
    private bool _loaded;
    private sealed record Resume(string Report,string Digest,string Candidate,string Turn,string Key,string Keyboard,string Attempts,int Sequence);
    private Resume? _resume;
    private string? _freshKeyboard;
    private bool _awaitRunning;
    private bool _apiRun;
    private bool _foregroundRun;
    private string? _acceptedPhotoTurn;
    private string? _acceptedWorkTurn;
    private bool _photoOnly;
    private bool _stopOnly;
    private string _deviceStoppedTurn="";
    private string _previousTurn="";
    private Step? _armed;
    private string? _request,_confirmed,_failure;

    private Tab5NativeAcceptance(string directory,string host,string scope){
        _directory=directory;_host=host;_scope=scope;
    }

    internal static int Run(string directory,string host,string scope,bool probeOnly,string? previous=null,string? previousDigest=null,string? freshKeyboard=null,bool awaitRunning=false,bool apiRun=false,bool photoOnly=false,bool stopOnly=false,bool foregroundRun=false,string? acceptedPhotoTurn=null,string? acceptedWorkTurn=null){
        directory=Path.GetFullPath(directory);
        string report=directory+".json";
        if(File.Exists(report)||Directory.Exists(directory)||host is not ("terminal" or "vscode")||
            scope.Length!=64||!scope.All(Uri.IsHexDigit))return 2;
        var suite=new Tab5NativeAcceptance(directory,host,scope){_freshKeyboard=freshKeyboard,_awaitRunning=awaitRunning,_apiRun=apiRun,_photoOnly=photoOnly,_stopOnly=stopOnly,_foregroundRun=foregroundRun,_acceptedPhotoTurn=acceptedPhotoTurn,_acceptedWorkTurn=acceptedWorkTurn};
        string stage="desktop-profile",reason="";
        try{
            AppProfileGuard.EnsureDesktopProfile();
            if(!Tab5DesktopActivation.IsInteractive())throw new IOException("desktop_locked_or_unavailable");
            if(previous is not null)suite._resume=ReadResume(previous,previousDigest??"",host,scope);
            stage="original-client";
            suite.Bind();
            bool background=apiRun&&host=="terminal"&&suite.Find(suite._session) is ClaudeBackgroundApi;
            if(freshKeyboard is not null && (!File.Exists(freshKeyboard)||!background&&!foregroundRun&&
                (host!="vscode"||(apiRun?suite.Find(suite._session) is not ClaudeSessionApi api||!api.IsFresh():suite.Find(suite._session) is not ClaudeNativeCode code||(!awaitRunning&&!code.IsNewPanel())))))
                throw new IOException("fresh_original_panel_required");
            if(probeOnly)stage="ready";
            else{
                stage="physical-device";
                Tab5AutomationRunner.Run(directory,suite);
                stage="passed";
            }
        }catch(Exception ex) when(ex is not OutOfMemoryException){
            reason=ex.Message==AppProfileGuard.Recovery?"desktop_profile_redirected":
                ex is OperationCanceledException?"deadline_or_disconnected":
                ex.Message.Length<120&&!ex.Message.Contains(Path.DirectorySeparatorChar)?ex.Message:ex.GetType().Name;
        }
        string Case(string name)=>suite._evidence.ContainsKey(name)?"passed":File.Exists(suite.Attempt(name))?"unconfirmed":"not_run";
        using var binary=File.OpenRead(typeof(Tab5NativeAcceptance).Assembly.Location);
        string candidateSha256=Convert.ToHexString(SHA256.HashData(binary)).ToLowerInvariant();
        File.WriteAllText(report,JsonSerializer.Serialize(new{host,scope,candidateSha256,stage,reason,passed=stage=="passed",
            cases=new{photo=Case("photo"),colorReply=Case("deviceColorReply"),work=Case("work"),append=Case("append"),interrupt=Case("interrupt")},
            evidence=suite._evidence,events=suite._events,nativeObservations=suite._nativeObservations},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(JsonSerializer.Serialize(new{host,stage,reason,report}));
        return stage is "passed" or "ready"?0:1;
    }
    private string Attempt(string name)=>Path.Combine(_resume?.Attempts??_directory,
        _resume is null?name+".attempt":name+"-"+_resume.Sequence+".attempt");
    private static Resume ReadResume(string path,string digest,string host,string scope){
        path=Path.GetFullPath(path);
        if(new FileInfo(path).Length>65536)throw new IOException("photo_evidence_changed");
        var bytes=File.ReadAllBytes(path);
        if(bytes.Length>65536||Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()!=digest)
            throw new IOException("photo_evidence_changed");
        using var json=JsonDocument.Parse(bytes);var prior=json.RootElement;
        var cases=prior.GetProperty("cases");var evidence=prior.GetProperty("evidence");
        if(Text(prior,"host")!=host||Text(prior,"scope")!=scope||Text(cases,"photo")!="passed"||
            Text(cases,"colorReply")!="passed"||Text(cases,"append")!="not_run"||Text(cases,"interrupt")!="not_run"||
            Text(prior,"reason")!="device_label_unconfirmed_key-q"||Text(evidence,"deviceColorReply")!="blue"||
            Text(evidence,"hardware")!="paired USB, 1280x720, 0.2.318-ui")
            throw new IOException("photo_evidence_not_resumable");
        string turn=Text(evidence,"photo");
        var sent=prior.GetProperty("events").EnumerateArray().Where(e=>Text(e,"step")=="photo"&&Text(e,"op")=="send"&&
            e.GetProperty("status").GetInt32()==202&&Text(e,"turn")==turn).ToArray();
        if(sent.Length!=1||!Guid.TryParse(turn,out _))throw new IOException("photo_receipt_unconfirmed");
        string old=Path.ChangeExtension(path,null);
        string request=Text(sent[0],"request");
        var delivered=prior.GetProperty("nativeObservations").EnumerateArray().Where(e=>Text(e,"step")=="photo"&&
            Text(e,"request")==request&&e.GetProperty("SubmitAttempted").GetBoolean()&&e.GetProperty("Delivered").GetBoolean()).ToArray();
        using var attempt=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(old,"photo.attempt")));
        if(delivered.Length!=1||Text(attempt.RootElement,"request")!=request)throw new IOException("photo_dispatch_unconfirmed");
        foreach(string step in new[]{"work","append","interrupt"})
            if(File.Exists(Path.Combine(old,step+".attempt")))throw new IOException("control_already_attempted");
        string key=Path.Combine(old,"fixture","alias-key.dat"),keyboard=Path.Combine(old,"006-key-q-unavailable.png");
        if(!File.Exists(key)||!File.Exists(keyboard))throw new IOException("photo_fixture_unavailable");
        string attempts=Path.Combine(Path.GetDirectoryName(path)!,"controls-"+digest);
        var records=Directory.Exists(attempts)?Directory.GetFiles(attempts,"*.attempt"):[];
        foreach(string record in records)
            if(!RejectedBeforeInput(record,scope,digest))throw new IOException("controls_already_attempted");
        return new(path,digest,Text(prior,"candidateSha256"),turn,key,keyboard,attempts,records.Length+1);
    }
    private static bool RejectedBeforeInput(string attemptPath,string scope,string photoDigest){
        string proof=attemptPath+".result.json";
        if(!File.Exists(proof))return false;
        using var marker=JsonDocument.Parse(File.ReadAllBytes(attemptPath));
        using var pointer=JsonDocument.Parse(File.ReadAllBytes(proof));
        string report=Text(pointer.RootElement,"path");
        if(new FileInfo(report).Length>65536)return false;
        byte[] bytes=File.ReadAllBytes(report);
        if(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()!=Text(pointer.RootElement,"sha256"))return false;
        using var json=JsonDocument.Parse(bytes);var root=json.RootElement;
        if(Text(root,"host")!="vscode"||Text(root,"scope")!=scope||
            Text(root.GetProperty("evidence"),"photoEvidenceSha256")!=photoDigest)return false;
        string request=Text(marker.RootElement,"request");
        var events=root.GetProperty("events").EnumerateArray().Where(e=>Text(e,"request")==request).ToArray();
        var observed=root.GetProperty("nativeObservations").EnumerateArray().Where(e=>Text(e,"request")==request).ToArray();
        return events.Length==1&&Text(events[0],"step")=="work"&&events[0].GetProperty("status").GetInt32()==409&&
            observed.Length==1&&!observed[0].GetProperty("StagingAttempted").GetBoolean()&&
            !observed[0].GetProperty("SubmitAttempted").GetBoolean()&&!observed[0].GetProperty("Delivered").GetBoolean();
    }
    internal static void ContinuationSelfTest(){
        string root=Path.Combine(Path.GetTempPath(),"aibot-control-proof-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root,"prior","fixture"));
        try{
            string report=Path.Combine(root,"prior.json"),scope=new('A',64);
            string turn=Guid.NewGuid().ToString(),request=Guid.NewGuid().ToString();
            var prior=new{host="vscode",scope,candidateSha256=new string('b',64),reason="device_label_unconfirmed_key-q",
                cases=new{photo="passed",colorReply="passed",append="not_run",interrupt="not_run"},
                evidence=new{photo=turn,deviceColorReply="blue",hardware="paired USB, 1280x720, 0.2.318-ui"},
                events=new[]{new{step="photo",op="send",status=202,turn,request}},
                nativeObservations=new[]{new{step="photo",request,SubmitAttempted=true,Delivered=true}}};
            File.WriteAllBytes(report,JsonSerializer.SerializeToUtf8Bytes(prior));
            File.WriteAllText(Path.Combine(root,"prior","photo.attempt"),JsonSerializer.Serialize(new{request}));
            File.WriteAllBytes(Path.Combine(root,"prior","fixture","alias-key.dat"),new byte[32]);
            File.WriteAllBytes(Path.Combine(root,"prior","006-key-q-unavailable.png"),new byte[1]);
            string digest=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(report))).ToLowerInvariant();
            var proof=ReadResume(report,digest,"vscode",scope);
            if(proof.Turn!=turn)throw new Exception("Accepted photo evidence lost");
            void Reject(string hash,string host,string selected){
                try{ReadResume(report,hash,host,selected);}catch(IOException){return;}
                throw new Exception("Unsafe continuation accepted");
            }
            Reject(new string('0',64),"vscode",scope);
            Reject(digest,"terminal",scope);
            Reject(digest,"vscode",new string('C',64));
            string work=Path.Combine(root,"prior","work.attempt");
            File.WriteAllText(work,"reserved");Reject(digest,"vscode",scope);File.Delete(work);
            Directory.CreateDirectory(proof.Attempts);
            File.WriteAllText(Path.Combine(proof.Attempts,"work.attempt"),"reserved");
            Reject(digest,"vscode",scope);
            string rejected=Path.Combine(root,"rejected.json"),mark=Path.Combine(proof.Attempts,"work.attempt");
            File.WriteAllText(mark,JsonSerializer.Serialize(new{request}));
            void Rejection(bool submitted,int status){
                File.WriteAllBytes(rejected,JsonSerializer.SerializeToUtf8Bytes(new{host="vscode",scope,
                    evidence=new{photoEvidenceSha256=digest},
                    events=new[]{new{step="work",request,status}},
                    nativeObservations=new[]{new{request,StagingAttempted=false,SubmitAttempted=submitted,Delivered=false}}}));
                string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(rejected))).ToLowerInvariant();
                File.WriteAllText(mark+".result.json",JsonSerializer.Serialize(new{path=rejected,sha256=hash}));
            }
            Rejection(false,409);
            if(ReadResume(report,digest,"vscode",scope).Sequence!=2)throw new Exception("Final pre-input rejection could not continue");
            Rejection(true,409);Reject(digest,"vscode",scope);
            Rejection(false,504);Reject(digest,"vscode",scope);
            Console.WriteLine("NATIVE_CONTINUATION_SELF_TEST_OK accepted photo bound to report/hash/scope/request; prior or shared control reservations reject replay");
        }finally{Directory.Delete(root,true);}
    }

    private IClaudeNativeSession? Find(Guid session){
        if(_foregroundRun)return ClaudeNativeTerminal.ForControlAcceptance(session,_scope);
        if(!_apiRun)return _host=="terminal"?(IClaudeNativeSession?)ClaudeNativeTerminal.Find(session):ClaudeNativeCode.Find(session);
        if(_host!="terminal")return ClaudeSessionApi.Find(session);
        return _stopOnly?ClaudeBackgroundApi.FindForInterruptAcceptance(session,_scope):ClaudeBackgroundApi.Find(session);
    }
    private bool Original(Guid session){
        try{return session==_session&&Find(session)?.Scope()==_scope;}catch(IOException){return false;}
    }
    private void Bind(){
        var ids=_host=="terminal"?(_apiRun?ClaudeBackgroundApi.LiveSessions().ToHashSet():ClaudeNativeTerminal.LiveSessions()):ClaudeNativeCode.LiveSessions();
        var matched=ids.Where(id=>Find(id)?.Scope()==_scope).ToArray();
        if(matched.Length!=1)throw new IOException("original_scope_unavailable");
        _session=matched[0];
        if(_apiRun){if(Find(_session)?.State!="idle")throw new IOException("original_session_busy");return;}
        var inspection=JsonSerializer.SerializeToElement(_host=="terminal"?ClaudeNativeTerminal.Find(_session)!.Inspect():ClaudeNativeCode.Find(_session)!.Inspect());
        if(inspection.GetProperty("state").GetString()!="idle"||!inspection.GetProperty("draftEmpty").GetBoolean()&&
           !(_foregroundRun&&inspection.GetProperty("suggestionLike").GetBoolean()))
            throw new IOException("original_composer_busy_or_nonempty");
        if(_foregroundRun&&(!inspection.GetProperty("canAddressInput").GetBoolean()||!_awaitRunning&&_acceptedPhotoTurn==null&&_acceptedWorkTurn==null&&!Find(_session)!.IsVerifiedBlank()))
            throw new IOException("new_exclusive_foreground_session_required");
    }
    internal override ClaudeWorkspace CreateWorkspace(Func<bool> enabled){
        if((_apiRun||_foregroundRun)&&_resume is null){
            // The device checkpoints the task ID and attachment references across
            // reboots. Use the ordinary encrypted store for this bound test
            // session so its receipts and drafts survive the tray handoff.
            return new(enabled,nativeFactory:id=>Original(id)?Find(id):null){
                DiagnosticSession=_session,DiagnosticResult=RecordNativeResult};
        }
        string fixture=Path.Combine(_directory,"fixture");
        if(_resume is not null){
            Directory.CreateDirectory(fixture);
            // Preserve only this test workspace's encrypted alias salt so the
            // accepted photo turn retains its ID. No account/pairing keys copied.
            File.Copy(_resume.Key,Path.Combine(fixture,"alias-key.dat"),false);
        }
        return new(enabled,directory:fixture,nativeFactory:id=>Original(id)?Find(id):null){
            DiagnosticSession=_session,DiagnosticResult=RecordNativeResult};
    }

    private void RecordNativeResult(string request,ClaudeNativeSend.Result result){
        lock(_gate){
            if(_armed is not {} step||request!=_request)return;
            _nativeObservations.Add(new{step=step.Name,request,result.StagingAttempted,result.SubmitAttempted,
                result.Delivered,result.Stopped,stage=result.FailureStage??result.Error});
        }
    }

    internal override async Task PrepareAsync(Tab5Service service,string device,CancellationToken token){
        _service=service;_device=device;
        if(!service.ActivitySourceEnabled)throw new IOException("device_activity_source_disabled");
        await Until(()=>service.ClaudeWorkspace.Alias(_session)!=null,20,"original_session_not_in_catalog",token);
        _task=service.ClaudeWorkspace.Alias(_session)!;_image=Guid.NewGuid().ToString();
        if((_apiRun||_foregroundRun)&&_resume is null){
            var existing=service.ClaudeWorkspace.Journal(device);
            string draft=existing.Draft(_task);
            bool ownedDraft=_acceptedPhotoTurn!=null&&draft==ApiPrompt||_acceptedWorkTurn!=null&&draft=="w";
            if(existing.Pending(_task)!=null||draft.Length>0&&!ownedDraft||existing.Images(_task).Length>0)
                throw new IOException("original_device_draft_not_empty");
        }
        if(_acceptedPhotoTurn is not null){
            var prior=await ReadLatest(token);
            if(Text(prior,"turnId")!=_acceptedPhotoTurn||Text(prior,"text").Trim().TrimEnd('。','.') is not ("蓝色" or "蓝")||
               Text(prior.GetProperty("replyView"),"state")!="completed")throw new IOException("accepted_photo_turn_changed");
            _evidence["priorPhotoTurn"]=_acceptedPhotoTurn;
        }
        if(_freshKeyboard is not null&&!_photoOnly&&(!_foregroundRun||_awaitRunning||_acceptedPhotoTurn!=null||_acceptedWorkTurn!=null)){
            if(_awaitRunning){
                var prior=await ReadLatest(token);
                _previousTurn=Text(prior,"turnId");
                bool rejectedWait=_foregroundRun&&_acceptedWorkTurn==_previousTurn&&Find(_session)?.State=="idle"&&
                    Text(prior,"text").StartsWith("API Error: 422 Failed to deserialize",StringComparison.Ordinal)&&
                    Text(prior,"text").Contains("tool_definition",StringComparison.Ordinal)&&
                    service.ClaudeWorkspace.MatchesDiagnosticPrompt(_task,_previousTurn,ForegroundWaitPrompt);
                if(!Guid.TryParse(_previousTurn,out _)||Text(prior.GetProperty("replyView"),"state")!="completed"&&!rejectedWait)throw new IOException("previous_completed_turn_required");
                if(rejectedWait)_evidence["priorWaitFailure"]="explicit API 422; original terminal idle";
                if(_acceptedWorkTurn!=null&&_previousTurn!=_acceptedWorkTurn)throw new IOException("previous_work_changed");
                if(_stopOnly)return;
            }
            var countDraft=JsonSerializer.SerializeToElement(new{op="draft-save",taskId=_task,requestId=Guid.NewGuid(),message=_awaitRunning?"w":_apiRun||_foregroundRun?ApiPrompt:CountPrompt,images=Array.Empty<string>()});
            if((await service.ClaudeWorkspace.HandleAsync(countDraft,device,token,()=>true)).Status!=200)throw new IOException("fixture_draft_unavailable");
            return;
        }
        if(_resume is not null){
            var read=await service.ClaudeWorkspace.HandleAsync(JsonSerializer.SerializeToElement(new{op="read",taskId=_task}),device,token,()=>true);
            var body=JsonSerializer.SerializeToElement(read.Body);
            if(read.Status!=200||Text(body,"turnId")!=_resume.Turn||
                Text(body.GetProperty("replyView"),"state")!="completed"||
                Text(body,"text").Trim().TrimEnd('。','.') is not ("蓝色" or "蓝"))
                throw new IOException("original_photo_reply_changed");
            _evidence["photo"]=_resume.Turn;_evidence["deviceColorReply"]="blue";
            _evidence["photoEvidenceSha256"]=_resume.Digest;_evidence["photoCandidateSha256"]=_resume.Candidate;
            var empty=JsonSerializer.SerializeToElement(new{op="draft-save",taskId=_task,requestId=Guid.NewGuid(),message="",images=Array.Empty<string>()});
            if((await service.ClaudeWorkspace.HandleAsync(empty,device,token,()=>true)).Status!=200)throw new IOException("fixture_draft_unavailable");
            return;
        }
        using var image=new Bitmap(96,96);
        using(var graphics=Graphics.FromImage(image))graphics.Clear(Color.FromArgb(26,90,235));
        using var data=new MemoryStream();image.Save(data,ImageFormat.Png);var photo=data.ToArray();
        if(service.ClaudeWorkspace.Upload(device,_task,_image,photo).Status!=200)throw new IOException("fixture_photo_unavailable");
        var seed=JsonSerializer.SerializeToElement(new{op="draft-save",taskId=_task,requestId=Guid.NewGuid(),message=PhotoPrompt,images=new[]{_image}});
        if((await service.ClaudeWorkspace.HandleAsync(seed,device,token,()=>true)).Status!=200)throw new IOException("fixture_draft_unavailable");
        _evidence["photoSha256"]=Convert.ToHexString(SHA256.HashData(photo));
    }

    private static string Text(JsonElement value,string key)=>ClaudeHistory.Text(value,key)??"";
    private static string[] Images(JsonElement value)=>value.TryGetProperty("images",out var list)&&list.ValueKind==JsonValueKind.Array?
        list.EnumerateArray().Select(x=>x.GetString()??"").ToArray():[];

    internal override bool Allows(string device,string client,string op,JsonElement root){
        if(device!=_device||client!="claude"||_task.Length==0||Text(root,"taskId")!=_task)return false;
        if(op is "read" or "read-ack" or "draft-load" or "receipt")return true;
        string message=Text(root,"message");var images=Images(root);
        if(op=="draft-save")return (message is "" or "q" or "w" && images.Length==0)||(_freshKeyboard is not null&&message==(_apiRun||_foregroundRun?ApiPrompt:CountPrompt)&&images.Length==0)||(message==PhotoPrompt&&images.SequenceEqual([_image]));
        lock(_gate){
            if(_armed is not {} step||_request!=null)return false;
            if(!step.Matches(op,root)){
                // Metadata only, bound to this isolated fixture. Distinguish a
                // stale device turn from a touch that never produced an RPC.
                _events.Add(new{step=step.Name,op,status=409,diagnostic="fixture_request_mismatch",
                    operationMatches=op==step.Operation,messageMatches=Text(root,"message")==step.Text,
                    turnMatches=Text(root,"expectedTurn")==step.ExpectedTurn,imagesMatch=Images(root).SequenceEqual(step.Images),utc=DateTime.UtcNow});
                _failure="fixture_request_mismatch";return false;
            }
            if(!Guid.TryParse(Text(root,"requestId"),out var id)||!Original(_session))return false;
            // Reserve before native input. Never reissue an uncertain mutation.
            string mark=Attempt(step.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(mark)!);
            using var file=new FileStream(mark,FileMode.CreateNew,FileAccess.Write,FileShare.None);
            file.Write(JsonSerializer.SerializeToUtf8Bytes(new{request=id,op,step.ExpectedTurn}));file.Flush(true);
            _request=id.ToString();return true;
        }
    }

    internal override void Observe(string op,JsonElement root,(int Status,object Body) reply){
        var body=JsonSerializer.SerializeToElement(reply.Body);
        lock(_gate){
            if(op=="draft-load"&&reply.Status==200)_loaded=true;
            if(op=="draft-save"&&reply.Status==200)_draft=Text(root,"message");
            if(op=="read"&&reply.Status==200&&body.TryGetProperty("replyView",out var view)&&Text(view,"state")=="completed"){
                if(Text(body,"text").Trim().TrimEnd('。','.') is "蓝色" or "蓝")_colorReplyTurn=Text(body,"turnId");
            }
            if(op=="read"&&reply.Status==200&&body.TryGetProperty("replyView",out var stoppedView)&&Text(stoppedView,"state")=="interrupted")
                _deviceStoppedTurn=Text(body,"turnId");
            if(_armed is not {} step||_request==null||Text(root,"requestId")!=_request||op!=step.Operation&&op!="receipt")return;
            string turn=Text(body,"turnId");
            _events.Add(new{step=step.Name,op,status=reply.Status,request=_request,turn,utc=DateTime.UtcNow});
            if(reply.Status==202&&step.AcceptedTurn(turn))_confirmed=turn;
            else if(reply.Status!=504)_failure=Text(body,"error") is {Length:>0} e?e:"receipt_mismatch";
        }
    }

    private async Task Until(Func<bool> condition,int seconds,string reason,CancellationToken token){
        long until=Environment.TickCount64+seconds*1000L;
        while(!condition()){
            token.ThrowIfCancellationRequested();
            lock(_gate){if(_failure!=null)throw new IOException(_failure);}
            if(Environment.TickCount64>=until)throw new IOException(reason);
            await Task.Delay(150,token);
        }
    }
    private bool Control(string turn,string flag){
        var snapshot=JsonSerializer.SerializeToElement(_service!.ClaudeWorkspace.Snapshot(requiredTask:_task,device:_device));
        return snapshot.TryGetProperty("control",out var c)&&c.ValueKind==JsonValueKind.Object&&Text(c,"taskId")==_task&&
            Text(c,"turnId")==turn&&c.TryGetProperty(flag,out var v)&&v.ValueKind==JsonValueKind.True;
    }
    private async Task<string> Submit(Tab5NativeScreen screen,Step step,string button,CancellationToken token,Point? located=null){
        lock(_gate){_armed=step;_request=null;_confirmed=null;_failure=null;}
        Point tapped;
        if(located is {} point){tapped=point;await screen.TapLocated(point,token);}else tapped=await screen.ActionButton(button,token);
        // Background reply reads temporarily disable the firmware's stop
        // button without changing its label. A touch acknowledgement alone
        // therefore does not prove an interrupt request was produced.
        // Only this isolated diagnostic may repeat the touch, and only while
        // no request has been reserved. Allows still accepts exactly one
        // original request before any native input; a late first touch cannot
        // cause a second interrupt, even if it races the following capture.
        // Reuse the freshly located, stationary control for this short burst.
        // Re-capturing the whole screen between touches adds seconds and can
        // repeatedly align with the next 750 ms background read.
        if(step.Operation=="interrupt")for(int retry=0;retry<6;retry++){
            await Task.Delay(200,token);
            lock(_gate){if(_request is not null||_failure is not null)break;}
            _evidence["interruptTouchRetries"]=(retry+1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            await screen.TapLocated(tapped,token);
        }
        await Until(()=>{lock(_gate)return _confirmed!=null;},65,step.Name+"_device_receipt_missing",token);
        lock(_gate){string turn=_confirmed!;_armed=null;_evidence[step.Name]=turn;return turn;}
    }
    private async Task<JsonElement> ReadLatest(CancellationToken token){
        var read=await _service!.ClaudeWorkspace.HandleAsync(JsonSerializer.SerializeToElement(new{op="read",taskId=_task}),_device,token,()=>true);
        if(read.Status!=200)throw new IOException("live_turn_unavailable");
        return JsonSerializer.SerializeToElement(read.Body);
    }
    private async Task RunApiControls(Tab5NativeScreen screen,CancellationToken token){
        var session=Find(_session)??throw new IOException("api_transport_required");
        if(!session.HasSessionApi&&!(_foregroundRun&&session is ClaudeNativeTerminal&&session.CanAddressInput))throw new IOException("api_transport_required");
        string workText=ApiPrompt;
        if(_acceptedPhotoTurn!=null){
            // Firmware keeps an already-selected task's local editor between
            // USB handoffs. It may legitimately skip draft-load.
            bool empty=await screen.InspectControlDraft(token);
            if(empty){await TypeLetter(screen,"q",token);workText="q";}
        }
        var points=await screen.ComposerActions(token);
        await screen.VerifyKeyboard(token);
        _evidence["transport"]=_foregroundRun?"original interactive Claude CLI, exclusive Win32 console":session is ClaudeBackgroundApi?"Claude terminal supervisor reply API, same session UUID":"VS Code original CLI stream via authenticated same-user pipe";
        var work=await Submit(screen,new("work","send",workText,"",[]),"发送",token,points.Submit);
        await screen.TypeVerifiedW(token);
        await Until(()=>{lock(_gate)return _draft=="w";},12,"device_append_draft_missing",token);
        _evidence["deviceDraft_w"]="exact task-bound draft-save";
        await EnsureControl(work,"canSteer",token);
        await Task.Delay(700,token);
        var append=await Submit(screen,new("append","steer","w",work,[]),"追加",token,points.Submit);
        await screen.TapLocated(points.Reply,token);
        if(!session.CanInterrupt){
            _evidence["interruptAvailability"]="not enabled for this transport";
            await screen.Save("after-append",token);
            if(!Original(_session))throw new IOException("original_scope_changed");
            return;
        }
        await EnsureControl(append,"canInterrupt",token);
        await Submit(screen,new("interrupt","interrupt","",append,[]),"停止任务",token);
        await Until(()=>session.State=="idle",10,"original_client_did_not_settle",token);
        await Until(()=>{lock(_gate)return _deviceStoppedTurn==append;},35,"device_interrupted_state_missing",token);
        _evidence["deviceStoppedTurn"]=append;
        await screen.Save("final",token);
        if(!Original(_session))throw new IOException("original_scope_changed");
    }
    private async Task EnsureControl(string turn,string operation,CancellationToken token){
        var until=Environment.TickCount64+(operation=="canInterrupt"?90000:10000);
        do{
            await ReadLatest(token);
            if(Control(turn,operation))return;
            await Task.Delay(125,token);
        }while(Environment.TickCount64<until);
        throw new IOException("original_turn_not_running_"+operation);
    }
    private async Task AcceptOnNextTurn(Tab5NativeScreen screen,CancellationToken token){
        var actions=await screen.ComposerActions(token);
        if(_foregroundRun&&!_stopOnly){
            // An authenticated draft from this task, or visible single w,
            // must precede the external starter. No working turn is consumed
            // while the device is still selecting the test task.
            if(await screen.HasEmptyOrW(token))await TypeLetter(screen,"w",token);
        }
        if(_stopOnly)await screen.TapLocated(actions.Reply,token);
        File.WriteAllText(Path.Combine(_directory,"awaiting-new-turn"),"device ready");
        string running="";long deadline=Environment.TickCount64+240000;
        var native=Find(_session)??throw new IOException("original_scope_unavailable");
        while(Environment.TickCount64<deadline){
            token.ThrowIfCancellationRequested();
            if(native.Scope()!=_scope)throw new IOException("native_scope_changed");
            if(native.State is "busy" or "working"){
                var view=await ReadLatest(token);
                string turn=Text(view,"turnId");
                if(turn!=_previousTurn&&Guid.TryParse(turn,out _)&&Control(turn,_stopOnly?"canInterrupt":"canSteer")&&
                    _service!.ClaudeWorkspace.MatchesDiagnosticPrompt(_task,turn,_foregroundRun?ForegroundWaitPrompt:ActivePrompt)){running=turn;break;}
            }
            await Task.Delay(150,token);
        }
        if(running.Length==0)throw new IOException("new_running_turn_not_observed");
        _evidence["runningTurn"]=running;
        // Let the device receive the updated turn, staying on the composer.
        await Task.Delay(650,token);
        if(_stopOnly){
            await Submit(screen,new("interrupt","interrupt","",running,[]),"停止任务",token);
            await Until(()=>{lock(_gate)return _deviceStoppedTurn==running;},35,"device_interrupted_state_missing",token);
            if(!Original(_session)||native.State!="idle")throw new IOException("original_scope_changed_after_interrupt");
            _evidence["deviceStoppedTurn"]=running;
            await screen.Save("stopped",token);return;
        }
        var appended=await Submit(screen,new("append","steer","w",running,[]),"追加",token,actions.Submit);
        await screen.TapLocated(actions.Reply,token);
        if(!native.CanInterrupt){
            _evidence["interruptAvailability"]="not enabled for this transport";
            await screen.Save("after-append",token);
            if(!Original(_session))throw new IOException("original_scope_changed");
            return;
        }
        await EnsureControl(appended,"canInterrupt",token);
        await Submit(screen,new("interrupt","interrupt","",appended,[]),"停止任务",token);
        await Until(()=>native.State=="idle",10,"original_client_did_not_settle",token);
        if(_foregroundRun){
            await Until(()=>{lock(_gate)return _deviceStoppedTurn==appended;},35,"device_interrupted_state_missing",token);
            _evidence["deviceStoppedTurn"]=appended;
        }
        await screen.Save("final",token);
        if(!Original(_session))throw new IOException("original_scope_changed");
    }
    private async Task TypeLetter(Tab5NativeScreen screen,string key,CancellationToken token){
        lock(_gate)_draft="";
        await screen.TypeLetter(key,token);
        await Until(()=>{lock(_gate)return _draft==key;},15,"device_draft_did_not_match_"+key,token);
        _evidence["deviceDraft_"+key]="exact task-bound draft-save";
    }

    internal override async Task RunAsync(Tab5AutomationRunner.Backend backend,CancellationToken token){
        var before=await backend.Settings(token);
        var viewport=await backend.Action(JsonSerializer.SerializeToElement(new{op="probe"}),token);
        if(viewport.Width!=1280||viewport.Height!=720||_service!.DeviceView.Firmware!="0.2.318-ui")throw new IOException("unexpected_TAB5_firmware_or_viewport");
        _evidence["hardware"]="paired USB, 1280x720, 0.2.318-ui";
        var screen=new Tab5NativeScreen(backend,viewport,_directory,_freshKeyboard??_resume?.Keyboard);
        try{
            await screen.EnterClaude(token);
            if(_resume is null&&_acceptedPhotoTurn==null&&_acceptedWorkTurn==null)
                await Until(()=>{lock(_gate)return _loaded;},30,"device_did_not_load_fixture",token);
            if(_awaitRunning){await AcceptOnNextTurn(screen,token);return;}
            if((_apiRun||_acceptedPhotoTurn!=null)&&!_photoOnly){await RunApiControls(screen,token);return;}
            if(_photoOnly||_foregroundRun||_resume is null&&_freshKeyboard is null){
                var photo=await Submit(screen,new("photo","send",PhotoPrompt,"",[_image]),"发送",token);
                await screen.OpenReply(token);
                await Until(()=>{lock(_gate)return _colorReplyTurn==photo;},60,"same_turn_blue_reply_missing",token);
                _evidence["deviceColorReply"]="blue";
                if(_foregroundRun)await screen.Save("photo-reply",token);
                if(_photoOnly){
                    await screen.Save("photo-reply",token);
                    if(!Original(_session))throw new IOException("original_scope_changed");
                    return;
                }
                await screen.ReturnToComposer(token);
            }
            else if(_resume is not null)await screen.ClearFixture(token);
            bool typeWork=_freshKeyboard is null||_foregroundRun;
            if(typeWork)await TypeLetter(screen,"q",token);
            var running=await Submit(screen,new("work","send",typeWork?"q":CountPrompt,"",[]),"发送",token);
            await screen.OpenReply(token);
            await Until(()=>Control(running,"canSteer"),20,"original_turn_not_running",token);
            await screen.ReturnToComposer(token);
            await TypeLetter(screen,"w",token);
            var appended=await Submit(screen,new("append","steer","w",running,[]),"追加",token);
            await screen.OpenReply(token);
            await Until(()=>Control(appended,"canInterrupt"),20,"appended_turn_not_interruptible",token);
            await Submit(screen,new("interrupt","interrupt","",appended,[]),"停止任务",token);
            await Until(()=>Find(_session)?.State=="idle",10,"original_client_did_not_settle",token);
            if(_foregroundRun){
                await Until(()=>{lock(_gate)return _deviceStoppedTurn==appended;},35,"device_interrupted_state_missing",token);
                _evidence["deviceStoppedTurn"]=appended;
                _evidence["transport"]="original interactive Claude CLI, exclusive Win32 console";
            }
            await screen.Save("final",token);
            if(!Original(_session))throw new IOException("original_scope_changed");
        }finally{
            lock(_gate)_armed=null;
            var after=await backend.Settings(token);
            if(JsonSerializer.Serialize(before)!=JsonSerializer.Serialize(after))throw new IOException("device_display_settings_changed");
            _evidence["displaySettings"]="preserved";
        }
    }
}
