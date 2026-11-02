using System.Runtime.InteropServices;
using System.Text.Json;
using NAudio.CoreAudioApi;

namespace AIBotBridge;

internal sealed record Tab5VoiceSettings(bool Enabled=true,string DjiId="",string Shortcut="LeftAltSpace",string Provider="doubao")
{
    private static string PathName=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AIBotBridge","tab5-voice.json");
    internal static Tab5VoiceSettings Load() {
        try{return JsonSerializer.Deserialize<Tab5VoiceSettings>(File.ReadAllText(PathName))??new();}
        catch(Exception ex) when(ex is IOException or JsonException or UnauthorizedAccessException){return new();}
    }
    internal void Save(){Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);File.WriteAllText(PathName,JsonSerializer.Serialize(this));}
}

// Owns a private draft box. Never reads clipboard contents or types into Codex/other windows.
internal sealed class Tab5VoiceDraft(Func<string> shortcut,Func<string,bool>? physicalToggle=null,
    Tab5DoubaoMicrophone? microphone=null,Func<Tab5DoubaoVoice?>? voiceFactory=null) : Form, ITab5VoiceEditor
{
    private readonly TextBox _text=new(){Multiline=true,Dock=DockStyle.Fill,MaxLength=10000,Font=new Font("Microsoft YaHei UI",14),ImeMode=ImeMode.On};
    private bool _allowClose;
    private string _speechPrefix="";
    private bool _normalizeSpeech;
    private IntPtr _previousWindow;
    private uint _previousProcess;
    private Tab5DoubaoVoice? _doubao;
    private readonly Tab5DoubaoMicrophone _microphone=microphone??new();
    private bool _completionHandled;
    private Tab5VoiceFocusRecovery? _focusRecovery;
    private string _focusEvent="none";
    private int _textUpdates,_textPeak;
    private readonly Label _status=new(){Text="使用豆包识别，回到 TAB5 检查后手动发送。",AutoSize=true,MaximumSize=new Size(420,0),Padding=new Padding(8),Dock=DockStyle.Top};
    internal string TriggerMode=>_doubao is null?"native control not yet checked":"native control (runtime checked)";
    internal string MicrophoneDiagnostic=>_microphone.Diagnostic+$"; focusRecovery={_focusRecovery?.Outcome??"not-started"}; focusAttempts={_focusRecovery?.Attempts??0}; recognizerState={_doubao?.LastState.ToString()??"unknown"}; recognizerStates={_doubao?.StateDiagnostic??""}; textUpdates={_textUpdates}; textPeakChars={_textPeak}; lastFocusEvent={_focusEvent}";
    internal void MaintainMicrophone(bool captureFinished) {
        if(captureFinished&&(_doubao is null||_doubao.CaptureStopped))_microphone.Release();
        else _microphone.RetryRestore();
    }
    internal void InitializeDraft() {
        SuspendLayout();AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;Font=SettingsWindow.BodyFont();
        Text="TAB5 语音输入";ClientSize=new Size(440,190);StartPosition=FormStartPosition.Manual;
        FormBorderStyle=FormBorderStyle.FixedToolWindow;ShowInTaskbar=false;Opacity=0;
        _text.KeyDown+=(_,e)=>{if(!e.Alt&&!e.Control)_normalizeSpeech=false;};
        _text.TextChanged+=(_,_)=>{_textUpdates++;_textPeak=Math.Max(_textPeak,_text.TextLength);};
        Controls.Add(_text);
        Controls.Add(_status);
        SettingsWindow.FitScreen(this);ResumeLayout(true);
    }
    string ITab5VoiceEditor.Text=>_normalizeSpeech?Tab5SpeechTranscript.Project(_text.Text,_speechPrefix):_text.Text;
    internal string InputLayout=>Tab5InputMethod.CurrentName();
    public bool SafeFocus=>Visible&&GetForegroundWindow()==Handle&&_text.Focused;
    public bool FocusRecoveryPending=>_focusRecovery?.Pending==true;
    public bool CanResumeCapture=>_doubao?.CanRestoreFocus==true;
    public bool? CaptureRunning=>_doubao?.CaptureRunning;
    public bool TryRestoreFocus() {
        if(IsDisposed||!Visible||_completionHandled){_focusRecovery?.Finish();return false;}
        string? before=_focusRecovery?.Outcome;
        var foreground=GetForegroundWindow();
        bool restored=_focusRecovery?.TryRestore()==true;
        if(before!=_focusRecovery?.Outcome)RecordFocusEvent(foreground);
        return restored;
    }
    private void RecordFocusEvent(IntPtr foreground) {
        GetWindowThreadProcessId(foreground,out uint process);
        string owner=foreground==IntPtr.Zero?"none":foreground==Handle?"draft":process==Environment.ProcessId?
            Control.FromHandle(foreground)?.GetType().Name??"bridge-native":"other-process";
        // Retain the last transition across takes, never window titles or text.
        _focusEvent=$"{DateTimeOffset.Now:HH:mm:ss}/{_focusRecovery?.Outcome}/foreground={owner}/editFocused={_text.Focused}/ime={_doubao?.LastState}/attempts={_focusRecovery?.Attempts}";
    }
    private bool RestoreDraftFocus() {
        // Restore only this owned draft while the same take is still recording.
        // No toggle/restart, shortcut, text mutation or global input-method change.
        Tab5DesktopActivation.Raise(Handle,allowInput:false);
        if(GetForegroundWindow()!=Handle)return false;
        ActiveControl=_text;_text.Select();
        return SafeFocus&&IsDoubaoLayout()&&_doubao?.CanRestoreFocus==true;
    }
    internal void Prepare() {
        _completionHandled=false;_status.Text="使用豆包识别，回到 TAB5 检查后手动发送。";
        // The IME still needs a real focused edit control, but normal TAB5
        // dictation must not expose a second desktop editor. Failures reveal it.
        ShowInTaskbar=false;Opacity=0;Show();ActiveControl=_text;Activate();_text.Select();
    }
    internal void ShowForSetup() {ShowInTaskbar=true;Opacity=1;Show();ActiveControl=_text;Activate();_text.Select();}
    internal void RevealFailure(string? message=null) {if(Visible){ShowInTaskbar=true;Opacity=1;if(message is not null)_status.Text=message;}}
    internal async Task PrepareAsync(CancellationToken token,Func<Task<string?>>? prepareInput=null,Func<bool>? leasePhysical=null) {
        long prepareStarted=Environment.TickCount64;
        await _microphone.Recovery.WaitAsync(token);
        Tab5VoiceTiming.Log("prepare-recovery",prepareStarted);
        // Check BEFORE activating the hidden editor: a focus change can itself
        // end the user's desktop IME recording. Unknown state must fail closed.
        long controlStarted=Environment.TickCount64;
        _doubao??=voiceFactory is null?Tab5DoubaoVoice.TryCreate(()=>SafeFocus&&IsDoubaoLayout(),()=>_focusRecovery?.CanStopOwnedCapture==true):voiceFactory();
        if(_doubao is null)throw new InvalidOperationException(Tab5DoubaoAvailability.UnavailableMessage());
        if(!_doubao.IsIdle)throw new InvalidOperationException(_doubao.LastState<0?Tab5DoubaoAvailability.UnavailableMessage():"豆包正在处理其他语音，请先结束后重试");
        Tab5VoiceTiming.Log("prepare-control",controlStarted);
        var foreground=GetForegroundWindow();
        if(foreground!=Handle) {
            _previousWindow=foreground;
            GetWindowThreadProcessId(foreground,out _previousProcess);
        }
        var area=Screen.FromPoint(Cursor.Position).WorkingArea;
        Location=new Point(area.Right-Width-20,area.Bottom-Height-20);
        if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;
        Prepare();
        // Yield to the message loop: focus and TSF activation are not synchronous
        // with Show(). Never retry the toggle, which could stop an active recording.
        long started=Environment.TickCount64;
        if(await Tab5VoiceFocusPreparation.WaitAsync(
            ()=>{if(IsDisposed)throw new OperationCanceledException(token);return SafeFocus;},
            ()=>!Tab5DesktopActivation.Raise(Handle).InputBlocked,
            ()=>{ActiveControl=_text;Activate();_text.Select();},token,
            observe:(phase,ms)=>Tab5LocalDiagnostics.Write($"VOICE_FOCUS phase={phase} ms={ms}"))) {
            Tab5VoiceTiming.Log("prepare-focus-stable",started);
            long imeStarted=Environment.TickCount64;
            if(!Tab5InputMethod.Ensure(()=>SafeFocus,IsDoubaoLayout,()=>Tab5InputMethod.ActivateDoubao(()=>SafeFocus)))throw new InvalidOperationException(Tab5DoubaoAvailability.SwitchInput);
            Tab5VoiceTiming.Log("prepare-input-method",imeStarted);
            if(!_doubao.IsIdle)throw new InvalidOperationException(_doubao.LastState<0?Tab5DoubaoAvailability.UnavailableMessage():"豆包正在处理其他语音，请先结束后重试");
            try {
                long inputStarted=Environment.TickCount64;
                string? input=prepareInput is null?null:await prepareInput();bool physical=leasePhysical?.Invoke()==true;
                Tab5VoiceTiming.Log("prepare-input",inputStarted);
                long leaseStarted=Environment.TickCount64;
                await _microphone.AcquireAsync(input,physical);token.ThrowIfCancellationRequested();
                Tab5VoiceTiming.Log("prepare-microphone-lease",leaseStarted);
            }
            catch {_microphone.Release();throw;}
            if(!SafeFocus) {_microphone.Release();throw new InvalidOperationException("语音输入焦点已变化，请重新开始");}
            return;
        }
        RevealFailure();
        throw new InvalidOperationException("Windows 未允许语音输入获得焦点，请点一下电脑上的语音输入框后重试");
    }
    internal async Task PrepareSwitchAsync(CancellationToken token,Func<Task<string?>> prepareInput) {
        if(!SafeFocus||!((ITab5VoiceEditor)this).RecognitionComplete)
            throw new InvalidOperationException("请先等待大疆识别结束，原文字已保留");
        _microphone.Release();
        await PrepareAsync(token,prepareInput);
        // A second IME segment appends to the committed first segment.
        _text.SelectionStart=_text.TextLength;_text.SelectionLength=0;
    }
    internal void CompleteReview(bool keepVisible=false) {
        if(!Visible||_completionHandled)return;
        if(_doubao is not null&&!_doubao.CaptureStopped)return;
        _completionHandled=true;
        _focusRecovery?.Finish();
        _microphone.Release();
        if(keepVisible)return;
        var previous=_previousWindow;_previousWindow=IntPtr.Zero;
        // Restore only while we still own the foreground. Do not interrupt a
        // window the user deliberately switched to during recognition.
        if(GetForegroundWindow()==Handle&&previous!=IntPtr.Zero&&IsWindow(previous)&&IsWindowVisible(previous)) {
            GetWindowThreadProcessId(previous,out var process);
            if(process==_previousProcess)SetForegroundWindow(previous);
        }
        Hide();
    }
    bool ITab5VoiceEditor.Start() {
        if(_normalizeSpeech)_text.Text=Tab5SpeechTranscript.Project(_text.Text,_speechPrefix);
        _speechPrefix=_text.Text;_normalizeSpeech=true;
        if(!SafeFocus)throw new InvalidOperationException("语音草稿窗口未获得输入焦点");
        if(!IsDoubaoLayout())throw new InvalidOperationException(Tab5DoubaoAvailability.SwitchInput);
        _doubao??=Tab5DoubaoVoice.TryCreate(()=>SafeFocus&&IsDoubaoLayout());
        if(_doubao is not null) {
            bool started=_doubao.Start();
            if(started) {
                _focusRecovery=new(()=>SafeFocus,()=>_doubao.CanRestoreFocus,Tab5DesktopActivation.IsInteractive,LastDesktopInput,RestoreDraftFocus);
                _focusRecovery.Begin();
            }
            return started;
        }
        if(!(physicalToggle?.Invoke(shortcut())??false))throw new InvalidOperationException("此豆包版本尚未支持无线触发，USB 键盘也未连接");
        return true;
    }
    bool ITab5VoiceEditor.Stop()=>_doubao is not null?_doubao.Stop():SafeFocus&&(physicalToggle?.Invoke(shortcut())??false);
    bool ITab5VoiceEditor.CaptureStopped=>_doubao?.CaptureStopped??true;
    bool ITab5VoiceEditor.RecognitionComplete=>_doubao?.CaptureStopped==true;
    void ITab5VoiceEditor.ObserveSafeFocus() {
        _doubao?.ObserveCapture();
        bool pending=_focusRecovery?.Pending==true;
        _focusRecovery?.ObserveSafeFocus();
        if(pending)RecordFocusEvent(GetForegroundWindow());
    }
    void ITab5VoiceEditor.Clear(){_normalizeSpeech=false;_speechPrefix="";_text.Clear();_textUpdates=_textPeak=0;}
    private static bool IsDoubaoLayout() {
        return Tab5InputMethod.IsDoubao(Tab5InputMethod.CurrentName());
    }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(IntPtr window);
    [StructLayout(LayoutKind.Sequential)] private struct LastInputInfo {public uint Size,Time;}
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetLastInputInfo(ref LastInputInfo info);
    private static uint? LastDesktopInput() {
        var info=new LastInputInfo{Size=(uint)Marshal.SizeOf<LastInputInfo>()};
        return GetLastInputInfo(ref info)?info.Time:null;
    }
    protected override void OnFormClosing(FormClosingEventArgs e){if(!_allowClose&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}base.OnFormClosing(e);}
    internal void Shutdown(){
        try {
            _microphone.Recovery.GetAwaiter().GetResult();
            if(_doubao is null||_doubao.CaptureStopped)_microphone.RestoreAsync().GetAwaiter().GetResult();
        } catch(Exception ex) when(Tab5DoubaoMicrophone.IsRestoreError(ex)) { /* Startup recovery retains the journal. */ }
        finally {_allowClose=true;Close();Dispose();}
    }
}

internal interface ITab5VoiceEndpoint : IDisposable
{
    Task<Tab5VoiceReply> HandleAsync(JsonElement request,CancellationToken token);
    Task<Tab5VoiceReply> HandleAsync(JsonElement request,CancellationToken token,bool waitForFinal)=>HandleAsync(request,token);
    void ShowSettings(IWin32Window owner);
}
internal sealed class Tab5VoiceHost : ITab5VoiceEndpoint
{
    internal string Diagnostic=>_session.Diagnostic+(_native is not null?"; recognizer=Windows SAPI; directPcm=true":"; trigger="+_draft.TriggerMode+"; "+_draft.MicrophoneDiagnostic);
    internal bool Busy => _preparing || _session.Active;
    internal bool SettingsEnabled => true;
    private readonly Control _dispatcher=new();
    private readonly System.Windows.Forms.Timer _timer=new(){Interval=100};
    private Tab5VoiceSettings _settings;
    private readonly Tab5VoiceDraft _draft;
    private Tab5VoiceSession _session;
    private ITab5VoiceAudio _audio;
    private WindowsSpeechBackend? _native;
    private readonly bool _allowNativeBackend;
    private bool _disposed;
    private readonly bool _usesDraft;
    private bool _preparing;
    internal Tab5VoiceHost(Tab5VoiceSettings? settings=null,ITab5VoiceAudio? audio=null,ITab5VoiceEditor? editor=null,Func<string,bool>? physicalToggle=null) {
        // Retain the legacy field for file compatibility; recording is explicitly
        // initiated by the user and no longer needs a second enable switch.
        _settings=(settings??Tab5VoiceSettings.Load()) with {Enabled=true};
        _usesDraft=editor is null;
        _allowNativeBackend=audio is null&&editor is null;
        _dispatcher.CreateControl();
        _draft=new(()=>_settings.Shortcut,physicalToggle);_draft.InitializeDraft();
        _audio=audio??new Tab5VoiceAudio(()=>_settings.DjiId);
        _session=new(_audio,editor??_draft);
        _timer.Tick+=(_,_)=> {try{
            using var timing=new Tab5UiWorkTiming("voice");
            _session.Tick();
            timing.Step("session");
            // Keep the selected input until text delivery finishes as well as
            // capture. Native idle alone must not reconfigure a pending result.
            _draft.MaintainMicrophone(!_preparing&&!_session.Active);
            timing.Step("microphone");
            if(_native is null&&!_preparing&&_session.Snapshot().State is "review" or "cancelled")_draft.CompleteReview();
            if(_native is null&&!_preparing&&_session.Snapshot().State=="error") {
                _draft.RevealFailure(_session.Snapshot().Message);
                if(((ITab5VoiceEditor)_draft).CaptureStopped)_draft.CompleteReview(keepVisible:true);
            }
            timing.Step("review");
        }catch(Exception ex) when(IsAudioError(ex)){_session.Fail("音频设备已断开，请重新开始");}};
        _timer.Start();
        // Populate metadata off the UI thread, without opening capture or
        // changing the IME. Cold enumeration need not wait for a microphone tap.
        if(_audio is Tab5VoiceAudio actual)_=actual.PrefetchAsync(false);
        if(_usesDraft)_=Tab5CodexComposer.PrepareReaderAsync();
    }
    internal static bool IsAudioError(Exception ex)=>ex is InvalidOperationException or NAudio.MmException or COMException;
    private void SelectBackend(){
        if(!_allowNativeBackend)return;
        if(_settings.Provider is not ("windows" or "doubao"))throw new InvalidOperationException("语音识别方式无效，请在语音设置中重新选择。");
        bool windows=_settings.Provider=="windows";
        if(windows==(_native is not null))return;
        _session.Dispose();
        if(windows){_native=new(()=>_settings.DjiId);_audio=_native;_session=new(_native,_native);}
        else{_native=null;_audio=new Tab5VoiceAudio(()=>_settings.DjiId);_session=new(_audio,_draft);}
    }
    public Task<Tab5VoiceReply> HandleAsync(JsonElement request,CancellationToken token)=>HandleAsync(request,token,false);
    public Task<Tab5VoiceReply> HandleAsync(JsonElement request,CancellationToken token,bool waitForFinal) {
        var completion=new TaskCompletionSource<Tab5VoiceReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        if(_disposed){completion.SetException(new InvalidOperationException("语音服务已关闭"));return completion.Task;}
        long queued=Environment.TickCount64;
        _dispatcher.BeginInvoke(async ()=> {
            if(token.IsCancellationRequested){completion.TrySetCanceled(token);return;}
            bool ownsPreparation=false;
            try {
                if(request.ValueKind==JsonValueKind.Object&&request.TryGetProperty("op",out var requestOp)&&requestOp.ValueKind==JsonValueKind.String&&requestOp.GetString()=="start")Tab5VoiceTiming.Log("ui-dispatch",queued);
                if(_preparing)throw new Tab5VoiceRequestException("语音正在准备，请稍候");
                bool starts=!_session.Active&&request.TryGetProperty("op",out var startOp)&&startOp.GetString()=="start"&&
                    request.TryGetProperty("taskId",out var startTask)&&startTask.ValueKind==JsonValueKind.String&&Guid.TryParseExact(startTask.GetString(),"D",out _);
                if(starts)SelectBackend();
                if(_native is {} native){
                    if(starts){_preparing=true;ownsPreparation=true;await native.PrepareAsync(token);}
                    else if(_session.NeedsSourcePreparation(request)){_preparing=true;ownsPreparation=true;}
                    var nativeResult=await _session.HandleAsync(request,token,prepareTab5:native.PrepareAsync,waitForFinal:waitForFinal);
                    if(_disposed)throw new OperationCanceledException(token);
                    completion.TrySetResult(nativeResult);return;
                }
                if(_usesDraft&&_session.NeedsSourcePreparation(request)) {_preparing=true;ownsPreparation=true;}
                if(_usesDraft&&!_session.Active&&request.TryGetProperty("op",out var op)&&op.GetString()=="start"&&
                    request.TryGetProperty("taskId",out var task)&&task.ValueKind==JsonValueKind.String&&Guid.TryParseExact(task.GetString(),"D",out _)) {
                    _preparing=true;ownsPreparation=true;
                    long focusStarted=Environment.TickCount64;
                    var metadata=(_audio as Tab5VoiceAudio)?.PrefetchAsync(Tab5VoiceSession.ForceTab5(request))??Task.CompletedTask;
                    await _draft.PrepareAsync(token,async ()=> {
                        await metadata;token.ThrowIfCancellationRequested();
                        if(_audio is not Tab5VoiceAudio actual)return null;
                        actual.Prepare(Tab5VoiceSession.ForceTab5(request));return actual.ExistingInputId;
                    },()=>_audio is Tab5VoiceAudio {LeasePhysicalInput:true});
                    Tab5VoiceTiming.Log("focus",focusStarted);
                    if(_disposed)throw new OperationCanceledException(token);
                }
                long handleStarted=Environment.TickCount64;
                var result=await _session.HandleAsync(request,token,prepareTab5:_usesDraft?async ct=> {
                    var metadata=(_audio as Tab5VoiceAudio)?.PrefetchAsync(true)??Task.CompletedTask;
                    await _draft.PrepareSwitchAsync(ct,async ()=> {
                        await metadata;ct.ThrowIfCancellationRequested();
                        if(_audio is not Tab5VoiceAudio actual)return null;
                        actual.Prepare(true);return actual.ExistingInputId;
                    });
                    if(_disposed)throw new OperationCanceledException(ct);
                }:null,waitForFinal:waitForFinal);
                if(_disposed)throw new OperationCanceledException(token);
                if(ownsPreparation)Tab5VoiceTiming.Log("audio-and-shortcut",handleStarted);
                // Restore the owned foreground before releasing a review reply:
                // automatic staging may arrive before the next 100ms UI tick.
                if(_usesDraft&&result.State is "review" or "cancelled")_draft.CompleteReview();
                completion.TrySetResult(result);
            }catch(OperationCanceledException){
                completion.TrySetCanceled();
            }catch(Tab5VoiceRequestException ex){
                // A delayed request for an old voice session must not stop the current one.
                completion.TrySetResult(new("","","","error",ex.Message,""));
            }catch(Exception ex) when(IsAudioError(ex)||ex is ArgumentException or IOException or JsonException or TimeoutException or UnauthorizedAccessException){
                _session.Fail(ex is ArgumentException or InvalidOperationException?ex.Message:
                    ex is IOException or JsonException or TimeoutException or UnauthorizedAccessException?"语音音源确认失败，请检查电脑语音设置":"音频设备不可用，请检查电脑");
                completion.TrySetResult(_session.Snapshot());
            }finally{
                if(ownsPreparation) {
                    _preparing=false;
                    if(!_session.Active) {
                        try{_audio.Stop();}catch(Exception ex) when(IsAudioError(ex)){Tab5VoiceTiming.Log("voice-preparation-audio-close-failed",Environment.TickCount64);}
                        if(_native is null)_draft.CompleteReview(keepVisible:_session.Snapshot().State=="error");
                    }
                }
            }
        });
        return completion.Task;
    }
    private Form? _settingsWindow;
    internal Form? SettingsWindowForm=>_settingsWindow is {IsDisposed:false}?_settingsWindow:null;
    public void ShowSettings(IWin32Window owner)=>ShowNamedSettings(owner,"TAB5 · 语音设置");
    internal void ShowNamedSettings(IWin32Window owner,string title) {
        if(_session.Active){MessageBox.Show(owner,"请先结束当前语音输入。","TAB5 语音");return;}
        if(_settingsWindow is null||_settingsWindow.IsDisposed){_settingsWindow=CreateSettings();_settingsWindow.Text=title;UnsavedChanges.Track(_settingsWindow);}
        if(owner is Form parent){while(parent.Owner is {} ancestor)parent=ancestor;_settingsWindow.Owner=parent;}
        SettingsWindow.Present(_settingsWindow);
    }
    internal Form CreateSettings(bool preview=false,Form? embeddedOwner=null) => new Tab5VoiceSettingsForm(
        _settings,()=>Busy,Tab5VoiceSettings.Load,value=>{value.Save();_settings=value;},preview,embeddedOwner);
    public void Dispose(){
        if(_disposed)return;_disposed=true;_timer.Stop();_timer.Dispose();
        try{_session.Dispose();}finally{try{_draft.Shutdown();}finally{_dispatcher.Dispose();}}
    }
}
