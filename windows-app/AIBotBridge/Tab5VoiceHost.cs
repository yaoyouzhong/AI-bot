using System.Runtime.InteropServices;
using System.Text.Json;
using NAudio.CoreAudioApi;

namespace AIBotBridge;

internal sealed record Tab5VoiceSettings(bool Enabled=false,string DjiId="",string Shortcut="LeftAltSpace")
{
    private static string PathName=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AIBotBridge","tab5-voice.json");
    internal static Tab5VoiceSettings Load() {
        try{return JsonSerializer.Deserialize<Tab5VoiceSettings>(File.ReadAllText(PathName))??new();}
        catch(Exception ex) when(ex is IOException or JsonException or UnauthorizedAccessException){return new();}
    }
    internal void Save(){Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);File.WriteAllText(PathName,JsonSerializer.Serialize(this));}
}

// Owns a private draft box. Never reads clipboard contents or types into Codex/other windows.
internal sealed class Tab5VoiceDraft(Func<string> shortcut,Func<string,bool>? physicalToggle=null) : Form, ITab5VoiceEditor
{
    private readonly TextBox _text=new(){Multiline=true,Dock=DockStyle.Fill,MaxLength=10000,Font=new Font("Microsoft YaHei UI",14),ImeMode=ImeMode.On};
    private bool _allowClose;
    private IntPtr _previousWindow;
    private uint _previousProcess;
    private Tab5DoubaoVoice? _doubao;
    private readonly Tab5DoubaoMicrophone _microphone=new();
    private Task _microphoneRestore=Task.CompletedTask;
    private bool _completionHandled;
    private readonly Label _status=new(){Text="使用豆包识别，回到 TAB5 检查后手动发送。",AutoSize=true,MaximumSize=new Size(420,0),Padding=new Padding(8),Dock=DockStyle.Top};
    internal string TriggerMode=>_doubao is null?"USB HID compatibility":"verified native control";
    private string? _microphoneRestoreError;
    internal void InitializeDraft() {
        SuspendLayout();AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;Font=new Font("Microsoft YaHei UI",9F);
        Text="TAB5 语音输入";ClientSize=new Size(440,190);StartPosition=FormStartPosition.Manual;
        FormBorderStyle=FormBorderStyle.FixedToolWindow;ShowInTaskbar=false;Opacity=0;
        Controls.Add(_text);
        Controls.Add(_status);
        SettingsWindow.FitScreen(this);ResumeLayout(true);
    }
    string ITab5VoiceEditor.Text=>_text.Text;
    internal string InputLayout=>Tab5InputMethod.CurrentName();
    public bool SafeFocus=>Visible&&GetForegroundWindow()==Handle&&_text.Focused;
    internal void Prepare() {
        _completionHandled=false;_status.Text="使用豆包识别，回到 TAB5 检查后手动发送。";
        // The IME still needs a real focused edit control, but normal TAB5
        // dictation must not expose a second desktop editor. Failures reveal it.
        ShowInTaskbar=false;Opacity=0;Show();ActiveControl=_text;Activate();_text.Select();
    }
    internal void ShowForSetup() {ShowInTaskbar=true;Opacity=1;Show();ActiveControl=_text;Activate();_text.Select();}
    internal void RevealFailure(string? message=null) {if(Visible){ShowInTaskbar=true;Opacity=1;if(message is not null)_status.Text=message;}}
    internal async Task PrepareAsync(CancellationToken token) {
        await _microphoneRestore;
        if(_microphoneRestoreError is not null) {
            await _microphone.RestoreAsync();_microphoneRestoreError=null;
        }
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
        long started=Environment.TickCount64,deadline=started+2200,stableSince=0;
        bool activationRequested=false;
        while(Environment.TickCount64<deadline) {
            await Task.Delay(50,token);
            if(IsDisposed)throw new OperationCanceledException(token);
            if(SafeFocus) {
                if(stableSince==0)stableSince=Environment.TickCount64;
                if(Environment.TickCount64-stableSince>=350) {
                    // Do not discard ownership while the native worker is still
                    // completing the previous stop (for example after focus loss).
                    if(_doubao is not null&&!_doubao.CaptureStopped) {
                        _doubao.Stop();
                        long stopUntil=Environment.TickCount64+2500;
                        while(!_doubao.CaptureStopped&&Environment.TickCount64<stopUntil)await Task.Delay(50,token);
                        if(!_doubao.CaptureStopped)throw new InvalidOperationException("上一轮豆包语音尚未结束，请稍后重试");
                        if(!SafeFocus)throw new InvalidOperationException("语音输入焦点已变化，请重新开始");
                    }
                    _doubao=Tab5DoubaoVoice.TryCreate(()=>SafeFocus&&IsDoubaoLayout());
                    if(_doubao is not null) {
                        if(!_doubao.IsIdle)throw new InvalidOperationException("豆包正在处理其他语音，请先结束后重试");
                    }
                    // The USB keyboard fallback also needs the bridge's audio
                    // endpoint. Otherwise an unavailable saved mic can make the
                    // IME silently listen to a different physical microphone.
                    try {await _microphone.AcquireAsync();token.ThrowIfCancellationRequested();}
                    catch {await _microphone.RestoreAsync();throw;}
                    if(!SafeFocus) {await _microphone.RestoreAsync();throw new InvalidOperationException("语音输入焦点已变化，请重新开始");}
                    return;
                }
            } else {
                stableSince=0;
                if(!activationRequested&&Environment.TickCount64-started>=250) {
                    activationRequested=true;
                    RequestForegroundForVoice();
                }
                ActiveControl=_text;Activate();_text.Select();
            }
        }
        RevealFailure();
        throw new InvalidOperationException("Windows 未允许语音输入获得焦点，请点一下电脑上的语音输入框后重试");
    }
    internal void CompleteReview(bool keepVisible=false) {
        if(!Visible||_completionHandled)return;
        if(_doubao is not null&&!_doubao.CaptureStopped)return;
        _completionHandled=true;
        _microphoneRestore=RestoreMicrophoneAsync();
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
    private async Task RestoreMicrophoneAsync() {
        try {await _microphone.RestoreAsync().ConfigureAwait(false);_microphoneRestoreError=null;}
        catch(Exception ex) when(ex is InvalidOperationException or IOException or UnauthorizedAccessException or JsonException or TimeoutException or OperationCanceledException) {
            _microphoneRestoreError="豆包麦克风尚未恢复，请在语音设置中检查";
            Tab5VoiceTiming.Log("doubao-microphone-restore-pending",Environment.TickCount64);
        }
    }
    private void RequestForegroundForVoice() {
        if(SetForegroundWindow(Handle))return;
        // Only during an explicit TAB5 start request. An ALT press releases the
        // foreground lock; it is not the Doubao shortcut (no Space is sent).
        // Do not combine it with keys/buttons the user is holding down.
        int[] heldKeys=[0x10,0x11,0x12,0x5B,0x5C,0x01,0x02];
        if(GetForegroundWindow()==IntPtr.Zero||heldKeys.Any(key=>(GetAsyncKeyState(key)&0x8000)!=0))return;
        Input[] tap=[Key(0xA4,false),Key(0xA4,true)];
        uint sent=SendInput(2,tap,Marshal.SizeOf<Input>());
        if(sent==1)SendInput(1,[Key(0xA4,true)],Marshal.SizeOf<Input>());
        if(sent==2)SetForegroundWindow(Handle);
    }
    bool ITab5VoiceEditor.Start() {
        if(!SafeFocus)throw new InvalidOperationException("语音草稿窗口未获得输入焦点");
        if(!IsDoubaoLayout())throw new InvalidOperationException("请在语音草稿输入框切换到豆包输入法");
        _doubao??=Tab5DoubaoVoice.TryCreate(()=>SafeFocus&&IsDoubaoLayout());
        if(_doubao is not null)return _doubao.Start();
        if(!(physicalToggle?.Invoke(shortcut())??false))throw new InvalidOperationException("此豆包版本尚未支持无线触发，USB 键盘也未连接");
        return true;
    }
    bool ITab5VoiceEditor.Stop()=>_doubao is not null?_doubao.Stop():SafeFocus&&(physicalToggle?.Invoke(shortcut())??false);
    bool ITab5VoiceEditor.CaptureStopped=>_doubao?.CaptureStopped??true;
    bool ITab5VoiceEditor.RecognitionComplete=>_doubao?.CaptureStopped==true;
    void ITab5VoiceEditor.Clear()=>_text.Clear();
    private static bool IsDoubaoLayout() {
        return Tab5InputMethod.IsDoubao(Tab5InputMethod.CurrentName());
    }
    private static Input Key(ushort key,bool up)=>new(){Type=1,Data=new InputUnion{Keyboard=new KeyboardInput{Vk=key,Flags=(up?2u:0u)|(key==0xA5?1u:0u)}}};
    [StructLayout(LayoutKind.Sequential)] private struct Input {public uint Type;public InputUnion Data;}
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion {[FieldOffset(0)]public KeyboardInput Keyboard;[FieldOffset(0)]public MouseInput Mouse;}
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput {public ushort Vk,Scan;public uint Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput {public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll",SetLastError=true)] private static extern uint SendInput(uint count,Input[] inputs,int size);
    protected override void OnFormClosing(FormClosingEventArgs e){if(!_allowClose&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}base.OnFormClosing(e);}
    internal void Shutdown(){
        try {
            _microphoneRestore.GetAwaiter().GetResult();
            if(_doubao is null||_doubao.CaptureStopped)_microphone.RestoreAsync().GetAwaiter().GetResult();
        } catch(InvalidOperationException) { /* Recovery journal is retained for the next start. */ }
        finally {_allowClose=true;Close();Dispose();}
    }
}

internal interface ITab5VoiceEndpoint : IDisposable
{
    Task<Tab5VoiceReply> HandleAsync(JsonElement request,CancellationToken token);
    void ShowSettings(IWin32Window owner);
}
internal sealed class Tab5VoiceHost : ITab5VoiceEndpoint
{
    internal string Diagnostic=>_session.Diagnostic+"; trigger="+_draft.TriggerMode;
    internal bool Busy => _preparing || _session.Active;
    internal bool SettingsEnabled => Volatile.Read(ref _settings).Enabled;
    private sealed class PreviewForm : Form { protected override bool ShowWithoutActivation=>true; }
    private readonly Control _dispatcher=new();
    private readonly System.Windows.Forms.Timer _timer=new(){Interval=100};
    private Tab5VoiceSettings _settings;
    private readonly Tab5VoiceDraft _draft;
    private readonly Tab5VoiceSession _session;
    private bool _disposed;
    private readonly bool _usesDraft;
    private bool _preparing;
    internal Tab5VoiceHost(Tab5VoiceSettings? settings=null,ITab5VoiceAudio? audio=null,ITab5VoiceEditor? editor=null,Func<string,bool>? physicalToggle=null) {
        _settings=settings??Tab5VoiceSettings.Load();
        _usesDraft=editor is null;
        _dispatcher.CreateControl();
        _draft=new(()=>_settings.Shortcut,physicalToggle);_draft.InitializeDraft();
        _session=new(audio??new Tab5VoiceAudio(()=>_settings.DjiId),editor??_draft);
        _timer.Tick+=(_,_)=> {try{
            _session.Tick();
            if(!_preparing&&_session.Snapshot().State is "review" or "cancelled")_draft.CompleteReview();
            if(!_preparing&&_session.Snapshot().State=="error") {
                _draft.RevealFailure(_session.Snapshot().Message);
                if(((ITab5VoiceEditor)_draft).CaptureStopped)_draft.CompleteReview(keepVisible:true);
            }
        }catch(Exception ex) when(IsAudioError(ex)){_session.Fail("音频设备已断开，请重新开始");}};
        _timer.Start();
    }
    internal static bool IsAudioError(Exception ex)=>ex is InvalidOperationException or NAudio.MmException or COMException;
    public Task<Tab5VoiceReply> HandleAsync(JsonElement request,CancellationToken token) {
        var completion=new TaskCompletionSource<Tab5VoiceReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        if(_disposed){completion.SetException(new InvalidOperationException("语音服务已关闭"));return completion.Task;}
        _dispatcher.BeginInvoke(async ()=> {
            if(token.IsCancellationRequested){completion.TrySetCanceled(token);return;}
            bool ownsPreparation=false;
            try {
                if(!_settings.Enabled)throw new InvalidOperationException("请先在电脑 TAB5 连接设置中配置语音");
                if(_preparing)throw new Tab5VoiceRequestException("语音正在准备，请稍候");
                if(_usesDraft&&!_session.Active&&request.TryGetProperty("op",out var op)&&op.GetString()=="start"&&
                    request.TryGetProperty("taskId",out var task)&&task.ValueKind==JsonValueKind.String&&Guid.TryParseExact(task.GetString(),"D",out _)) {
                    _preparing=true;ownsPreparation=true;
                    long focusStarted=Environment.TickCount64;
                    await _draft.PrepareAsync(token);
                    Tab5VoiceTiming.Log("focus",focusStarted);
                    if(_disposed)throw new OperationCanceledException(token);
                }
                long handleStarted=Environment.TickCount64;
                var result=_session.Handle(request);
                if(ownsPreparation)Tab5VoiceTiming.Log("audio-and-shortcut",handleStarted);
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
            }finally{if(ownsPreparation)_preparing=false;}
        });
        return completion.Task;
    }
    public void ShowSettings(IWin32Window owner)=>ShowNamedSettings(owner,"TAB5 · 语音设置");
    internal void ShowNamedSettings(IWin32Window owner,string title) {
        if(_session.Active){MessageBox.Show(owner,"请先结束当前语音输入。","TAB5 语音");return;}
        using var form=CreateSettings();form.Text=title;form.ShowDialog(owner);
    }
    internal Form CreateSettings(bool preview=false) {
        Form form=preview?new PreviewForm():new Form();
        form.SuspendLayout();
        form.AutoScaleDimensions=new SizeF(96,96);form.AutoScaleMode=AutoScaleMode.Dpi;
        form.Font=new Font("Microsoft YaHei UI",9F);form.MinimumSize=new Size(500,300);
        form.Text="M5Stack TAB5 · 语音设置";form.ClientSize=new Size(560,270);form.StartPosition=FormStartPosition.CenterParent;
        var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(20)};form.Controls.Add(panel);
        var enabled=new CheckBox{Text="启用语音输入",AutoSize=true,Checked=_settings.Enabled,Margin=new Padding(3,0,3,12)};panel.Controls.Add(enabled);
        panel.Controls.Add(new Label {AutoSize=true,Text="优先麦克风"});
        var inputs=new ComboBox{Width=590,DropDownStyle=ComboBoxStyle.DropDownList};panel.Controls.Add(inputs);
        inputs.Items.Add(new Tab5AudioDevice("","自动识别 DJI / Wireless Mic Rx"));
        foreach(var d in Tab5VoiceAudio.Devices(DataFlow.Capture).Where(d=>!Tab5VoiceAudio.IsCableCapture(d.Name)))inputs.Items.Add(d);
        inputs.SelectedIndex=0;
        for(int i=1;i<inputs.Items.Count;i++)if(inputs.Items[i] is Tab5AudioDevice device&&device.Id==_settings.DjiId)inputs.SelectedIndex=i;
        if(_settings.DjiId.Length>0&&inputs.SelectedIndex==0) {
            inputs.Items.Add(new Tab5AudioDevice(_settings.DjiId,"之前选择的大疆设备（当前未连接）"));inputs.SelectedIndex=inputs.Items.Count-1;
        }
        panel.Controls.Add(new Label{Text="不可用时使用 M5Stack TAB5 麦克风。",AutoSize=true,ForeColor=Color.DimGray,Margin=new Padding(3,6,3,12)});
        var troubleshoot=new Button{Text="问题排查",AutoSize=true,Name="voice-troubleshoot"};SettingsWindow.StyleButton(troubleshoot);panel.Controls.Add(troubleshoot);
        var advanced=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Visible=false,Margin=Padding.Empty,Name="voice-advanced"};panel.Controls.Add(advanced);SettingsWindow.FitFlow(advanced);
        troubleshoot.Click+=(_,_)=>{
            advanced.Visible=!advanced.Visible;troubleshoot.Text=advanced.Visible?"收起排查":"问题排查";
            int height=(int)Math.Round((advanced.Visible?500:270)*form.DeviceDpi/96d);
            int available=Screen.FromControl(form).WorkingArea.Height-32-(form.Height-form.ClientSize.Height);
            form.ClientSize=new Size(form.ClientSize.Width,Math.Min(height,available));
        };
        string[] shortcuts=["LeftAltSpace","RightAltSpace","CtrlAltSpace"];
        advanced.Controls.Add(new Label{AutoSize=true,Text="旧版备用快捷键",Margin=new Padding(3,12,3,3)});
        var keys=new ComboBox{Width=590,DropDownStyle=ComboBoxStyle.DropDownList};keys.Items.AddRange(["左 Alt + 空格","右 Alt + 空格","Ctrl + Alt + 空格"]);keys.SelectedIndex=Math.Max(0,Array.IndexOf(shortcuts,_settings.Shortcut));advanced.Controls.Add(keys);
        var test=new Button{Text="测试识别"};advanced.Controls.Add(test);
        test.Click+=(_,_)=>_draft.ShowForSetup();

        var status=new Label {AutoSize=true,MaximumSize=new Size(590,0),ForeColor=Color.DimGray};advanced.Controls.Add(status);
        Tab5VoiceAvailability? RefreshAvailability() {
            try {
                var result=Tab5VoiceAudio.Inspect(Tab5VoiceAudio.Devices(DataFlow.Capture),Tab5VoiceAudio.Devices(DataFlow.Render),((Tab5AudioDevice)inputs.SelectedItem!).Id);
                status.Text=result.Message;return result;
            }catch(Exception ex) when(IsAudioError(ex)){status.Text="无法读取音频设备，请检查连接后重试。";return null;}
        }
        var refresh=new Button{Text="检查音频"};advanced.Controls.Add(refresh);
        refresh.Click+=(_,_)=>RefreshAvailability();inputs.SelectedIndexChanged+=(_,_)=>RefreshAvailability();RefreshAvailability();
        var save=new Button{Text="保存"};var cancel=new Button{Text="取消",DialogResult=DialogResult.Cancel,CausesValidation=false};
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,Padding=new Padding(20,8,20,12)};
        actions.Controls.AddRange([save,cancel]);form.Controls.Add(actions);panel.BringToFront();
        cancel.Click+=(_,_)=>form.Close();form.CancelButton=cancel;SettingsWindow.StyleButton(cancel);
        save.Click+=(_,_)=> {
            if(enabled.Checked&&RefreshAvailability()?.CableReady!=true){MessageBox.Show(form,"请先完成音频通路配置。");return;}
            try{var settings=new Tab5VoiceSettings(enabled.Checked,((Tab5AudioDevice)inputs.SelectedItem!).Id,shortcuts[keys.SelectedIndex]);settings.Save();_settings=settings;form.Close();}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){MessageBox.Show(form,"设置未保存："+ex.GetType().Name);}
        };
        SettingsWindow.FitFlow(panel);
        foreach(var button in new[]{test,refresh,save})SettingsWindow.StyleButton(button);
        SettingsWindow.StyleButton(save,true);SettingsWindow.FitScreen(form);form.ResumeLayout(true);
        return form;
    }
    public void Dispose(){
        if(_disposed)return;_disposed=true;_timer.Stop();_timer.Dispose();
        try{_session.Dispose();}finally{try{_draft.Shutdown();}finally{_dispatcher.Dispose();}}
    }
}
