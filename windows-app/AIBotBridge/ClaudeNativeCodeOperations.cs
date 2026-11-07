using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace AIBotBridge;

internal sealed partial class ClaudeNativeCode
{
    internal object ProbeAttachment(string scope){
        Require(Scope()==scope,"native_session_changed");
        var before=Read();
        Require(before.Empty,"desktop_draft_present");
        Require(GetForegroundWindow()==before.Window,"native_foreground_unconfirmed");
        string directory=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AI-bot","native-attachment-probe",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,"blue-probe.png");
        using(var bitmap=new Bitmap(96,96)){
            using(var graphics=Graphics.FromImage(bitmap))graphics.Clear(Color.FromArgb(26,90,235));
            bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);
        }
        before.Editor.SetFocus();
        object? picker=null;
        string openType;
        try{
            openType=AttachFiles(before,[path],[ClaudeNativeContent.FileDigest(path)],()=>Scope()==scope,
                snapshot=>picker=snapshot);
        }catch(Exception e){e.Data["picker"]=picker;throw;}
        var after=Read();
        Require(after.Text.Length==0&&after.Images.SequenceEqual(["blue-probe.png"]),"native_attachments_unconfirmed");
        const string probeText="蓝色测试图片，检查附件和文字。";
        after.Editor.SetFocus();
        TypeText(after,probeText,()=>Scope()==scope,true);
        for(int i=0;i<30;i++){
            var final=Read();
            Require(Scope()==scope&&final.Images.SequenceEqual(after.Images),"native_composer_changed");
            if(final.Text==probeText)return new{passed=true,imageCount=final.Images.Length,textMatched=true,submitted=false,openType,picker};
            Thread.Sleep(100);
        }
        throw new IOException("native_text_unconfirmed");
    }
    Task<ClaudeNativeSend.Result> IClaudeNativeSession.ExecuteAsync(ClaudeNativeRequest request,Func<ClaudeHistory.Value> history,Action dispatch,Func<bool> authorized,CancellationToken token)=>ExecuteAsync(request,history,dispatch,authorized,token);
    private async Task<ClaudeNativeSend.Result> ExecuteAsync(ClaudeNativeRequest request,Func<ClaudeHistory.Value> history,Action dispatch,Func<bool> authorized,CancellationToken token) {
        if(!authorized())return new(false,false,null,"unauthorized");
        if(!request.Current())return new(false,false,null,"turn_changed");
        bool idle=request.Operation=="send";
        var first=Read(idle);string target=string.Join(",",first.Frame.GetRuntimeId());
        bool Same(View view)=>string.Join(",",view.Frame.GetRuntimeId())==target&&view.Window==first.Window;
        if(request.Operation=="interrupt") {
            if(first.Button.Current.Name!="Stop"||!first.Button.Current.IsEnabled)return new(false,false,null,"turn_changed");
            string scope=Scope();bool attempted=false;
            try {
                if(!authorized()||!request.Current())return new(false,false,null,"turn_changed");
                var current=Read(false);Require(Same(current)&&current.Text==first.Text&&current.Images.SequenceEqual(first.Images),"native_composer_changed");
                dispatch();attempted=true;
                // Invoke only this original session's Stop control. No Escape,
                // confirmation answer, process signal or repeated stop action.
                ((InvokePattern)current.Button.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                int idleReads=0;
                for(int i=0;i<50;i++) {
                    token.ThrowIfCancellationRequested();Require(Scope()==scope&&request.Current(),"turn_changed");
                    var after=Read(false);Require(Same(after)&&after.Text==first.Text&&after.Images.SequenceEqual(first.Images),"native_composer_changed");
                    if(CheckNative()=="idle"&&after.Button.Current.Name!="Stop")idleReads++;else idleReads=0;
                    if(idleReads>=2)return new(false,true,null,"",Stopped:true);
                    await Task.Delay(100,token);
                }
            }catch(Exception e) when(e is IOException or InvalidOperationException or COMException or OperationCanceledException or ElementNotAvailableException){ }
            return new(false,attempted,null,"delivery_unknown");
        }
        if(request.Operation is not ("send" or "steer")||request.Text.Any(char.IsControl)||new UTF8Encoding(false,true).GetByteCount(request.Text)>2000||request.Images.Length>3||request.Images.Length!=request.ImageDigests.Length)
            return new(false,false,null,"native_input_unsupported");
        if(!first.Empty)return new(false,false,null,"desktop_draft_present");
        if(Held.Any(k=>(GetAsyncKeyState(k)&0x8000)!=0))return new(false,false,null,"desktop_input_active");
        for(int i=0;i<request.Images.Length;i++)Require(ClaudeNativeContent.FileDigest(request.Images[i])==request.ImageDigests[i],"native_image_changed");
        if(!authorized())return new(false,false,null,"unauthorized");
        var activation=Tab5DesktopActivation.Raise(first.Window);
        if(!activation.Foreground&&!activation.InputBlocked&&!activation.PartialTap){
            for(int i=0;i<6&&GetForegroundWindow()!=first.Window;i++){
                if(!authorized()||!request.Current()||Held.Any(k=>(GetAsyncKeyState(k)&0x8000)!=0))break;
                await Task.Delay(50,token);
            }
        }
        if(GetForegroundWindow()!=first.Window)return new(false,false,null,
            activation.InputBlocked?"native_foreground_input_blocked":"native_foreground_unconfirmed");
        // Chromium may acknowledge SetFocus before its webview actually receives
        // keyboard focus. Confirm the same empty composer before sealing a send.
        if(!Focused(first))first.Editor.SetFocus();
        bool focusReady=false;
        for(int attempt=0;attempt<10;attempt++){
            token.ThrowIfCancellationRequested();
            if(!authorized()||!request.Current())return new(false,false,null,"turn_changed");
            if(GetForegroundWindow()!=first.Window)return new(false,false,null,"desktop_foreground_unconfirmed");
            var current=Read(idle);
            if(!Same(current)||!current.Empty)return new(false,false,null,"composer_changed");
            if(Focused(current)){focusReady=true;break;}
            if(attempt<9)await Task.Delay(50,token);
        }
        if(!focusReady)return new(false,false,null,"desktop_foreground_unconfirmed");
        string[] names=request.Images.Select(Path.GetFileName).Select(n=>n!).ToArray();
        var calls=new ClaudeNativeSend.Calls(
            ()=>{var v=Read(idle);return new(_session,Scope(),string.Join(",",v.Frame.GetRuntimeId()),v.Text,v.Empty,
                idle?CheckNative()=="idle":CheckNative() is "busy" or "working",
                v.Button.Current.IsEnabled&&v.Button.Current.Name=="Send message"&&v.Images.SequenceEqual(names));},Scope,
            input=>{
                var v=Read(idle);
                Require(Same(v)&&v.Empty,"native_composer_changed");
                Require(request.Current()&&authorized(),"turn_changed");
                Require(Focused(v),"native_editor_focus_lost");
                if(request.Images.Length>0){
                    AttachFiles(v,request.Images,request.ImageDigests,()=>authorized()&&request.Current());
                    v=Read(idle);
                    Require(Same(v)&&v.Text.Length==0&&v.Images.SequenceEqual(names),"native_attachments_unconfirmed");
                    if(!Focused(v))v.Editor.SetFocus();
                }
                TypeText(v,input,()=>authorized()&&request.Current(),idle);
            },
            ()=>{var v=Read(idle);Require(Same(v)&&Focused(v)&&authorized()&&request.Current()&&v.Text==request.Text&&v.Images.SequenceEqual(names)&&v.Button.Current.Name=="Send message"&&v.Button.Current.IsEnabled,"native_composer_changed");
                // Native VS Code sends typed input to its existing queue while
                // busy; never invoke the separate Stop control for an append.
                ((InvokePattern)v.Button.GetCurrentPattern(InvokePattern.Pattern)).Invoke();},history,authorized,dispatch,ct=>Task.Delay(100,ct),request.ImageDigests,request.Current);
        return await ClaudeNativeSend.SendAsync(calls,request.Text,token);
    }
    private void TypeText(View expected,string text,Func<bool> current,bool idle){
        var view=Read(idle);
        Require(current(),"turn_changed");
        Require(Focused(view),"native_editor_focus_lost");
        Require(view.Window==expected.Window&&view.Text.Length==0&&
            view.Images.SequenceEqual(expected.Images)&&view.Frame.GetRuntimeId().SequenceEqual(expected.Frame.GetRuntimeId())&&
            !Held.Any(k=>(GetAsyncKeyState(k)&0x8000)!=0),"native_composer_changed");
        var keys=new List<Input>();
        foreach(char c in text){keys.Add(Key(c,false));keys.Add(Key(c,true));}
        if(keys.Count>0)Require(SendInput((uint)keys.Count,keys.ToArray(),Marshal.SizeOf<Input>())==keys.Count,"native_input_unconfirmed");
    }
    private string AttachFiles(View view,string[] paths,string[] digests,Func<bool> current,Action<object>? observe=null) {
        AutomationElement Button(AutomationElement root,string name,string stage) {
            var found=root.FindAll(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button),new PropertyCondition(AutomationElement.NameProperty,name))).Cast<AutomationElement>().Where(e=>!e.Current.IsOffscreen&&e.Current.IsEnabled).ToArray();
            Require(found.Length==1,stage);return found[0];
        }
        Require(current()&&GetForegroundWindow()==view.Window,"native_composer_changed");
        ((InvokePattern)Button(view.Composer,"Add","native_attachment_add_unavailable").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        AutomationElement? upload=null;
        for(int i=0;i<20&&upload is null;i++){try{upload=Button(view.Frame,"Upload from computer","native_attachment_upload_unavailable");}catch(IOException){Thread.Sleep(50);}}
        Require(upload is not null&&current(),"native_attachment_upload_unavailable");
        // A native common-dialog invocation may return only after the dialog.
        var invoke=Task.Run(()=>((InvokePattern)upload!.GetCurrentPattern(InvokePattern.Pattern)).Invoke());
        AutomationElement? dialog=null;nint hwnd=0;
        for(int i=0;i<50&&dialog is null;i++) {
            var candidates=new List<nint>();
            EnumWindows((w,_)=>{var cls=new StringBuilder(64);GetClassName(w,cls,cls.Capacity);if(IsWindowVisible(w)&&cls.ToString()=="#32770"&&GetWindow(w,4)==view.Window)candidates.Add(w);return true;},0);
            Require(candidates.Count<=1,"native_attachment_picker_ambiguous");
            if(candidates.Count==1){hwnd=candidates[0];dialog=AutomationElement.FromHandle(hwnd);}else Thread.Sleep(100);
        }
        Require(dialog is not null,"native_attachment_dialog_unavailable");
        Require(GetForegroundWindow()==hwnd&&current(),"native_attachment_dialog_not_active");
        observe?.Invoke(ClaudeNativePickerDiagnostic.Controls(dialog!));
        for(int i=0;i<paths.Length;i++)Require(Path.IsPathFullyQualified(paths[i])&&!paths[i].Contains('"')&&ClaudeNativeContent.FileDigest(paths[i])==digests[i],"native_image_changed");
        var edits=dialog!.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit)).Cast<AutomationElement>().Where(e=>e.Current.AutomationId=="1148"&&e.Current.IsEnabled).ToArray();
        if(edits.Length==0)edits=dialog.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"1148")).Cast<AutomationElement>().SelectMany(e=>e.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit)).Cast<AutomationElement>()).ToArray();
        if(edits.Length==0){
            string[] labels=["File name:","File name","文件名(N):","文件名(N)：","文件名:","文件名："];
            edits=dialog.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit))
                .Cast<AutomationElement>().Where(e=>e.Current.IsEnabled&&!e.Current.IsOffscreen&&
                    (labels.Contains(e.Current.Name)||e.Current.AutomationId=="FileNameControlHost")).ToArray();
        }
        Require(edits.Length==1,"native_attachment_filename_unavailable");
        string selected=string.Join(" ",paths.Select(p=>'"'+p+'"'));
        ((ValuePattern)edits[0].GetCurrentPattern(ValuePattern.Pattern)).SetValue(selected);
        Require(((ValuePattern)edits[0].GetCurrentPattern(ValuePattern.Pattern)).Current.Value==selected&&current()&&GetForegroundWindow()==hwnd,"native_attachment_picker_changed");
        observe?.Invoke(ClaudeNativePickerDiagnostic.Controls(AutomationElement.FromHandle(hwnd)));
        string[] openLabels=["Open","Open(O)","Open(&O)","打开","打开(O)","打开(&O)"];
        AutomationElement? open=null;
        long deadline=Environment.TickCount64+5000;
        do{
            Require(current()&&GetForegroundWindow()==hwnd,"native_attachment_picker_changed");
            dialog=AutomationElement.FromHandle(hwnd);
            var candidates=dialog.FindAll(TreeScope.Descendants,Condition.TrueCondition)
                .Cast<AutomationElement>().Where(e=>(e.Current.ControlType==ControlType.Button||e.Current.ControlType==ControlType.SplitButton)&&!e.Current.IsOffscreen&&
                    (e.Current.AutomationId=="1"||openLabels.Contains(e.Current.Name))).ToArray();
            Require(candidates.Length<=1,"native_attachment_open_ambiguous");
            if(candidates.Length==1&&candidates[0].Current.IsEnabled)open=candidates[0];
            if(open is null)Thread.Sleep(100);
        }while(open is null&&Environment.TickCount64<deadline);
        observe?.Invoke(ClaudeNativePickerDiagnostic.Controls(AutomationElement.FromHandle(hwnd)));
        Require(((ValuePattern)edits[0].GetCurrentPattern(ValuePattern.Pattern)).Current.Value==selected&&current(),"native_attachment_picker_changed");
        string openType;
        if(open is not null){
            openType=open.Current.ControlType.ProgrammaticName;
            ((InvokePattern)open.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        }else{
            // The live Win32 button can be omitted from this shell dialog's UIA tree.
            // Use its fixed dialog ID only after validating the visible foreground picker.
            nint native=GetDlgItem(hwnd,1);
            var caption=new StringBuilder(128);var cls=new StringBuilder(64);
            GetWindowText(native,caption,caption.Capacity);GetClassName(native,cls,cls.Capacity);
            Require(native!=0&&IsWindowVisible(native)&&IsWindowEnabled(native)&&cls.ToString()=="Button"&&
                openLabels.Contains(caption.ToString().Replace("&","",StringComparison.Ordinal))&&
                GetForegroundWindow()==hwnd&&current(),"native_attachment_open_unavailable");
            Require(PostMessage(native,0x00F5,0,0),"native_attachment_open_failed");
            openType="Win32.Button";
        }
        string[] names=paths.Select(Path.GetFileName).Select(n=>n!).ToArray();
        long imagesDeadline=Environment.TickCount64+20000;
        while(Environment.TickCount64<imagesDeadline){
            Require(current(),"turn_changed");
            try{
                var after=Read(false);
                Require(after.Text.Length==0,"native_composer_changed");
                if(after.Images.SequenceEqual(names)){
                    if(invoke.IsFaulted)throw new IOException("native_attachment_picker_unconfirmed");
                    return openType;
                }
                Require(after.Images.Length==0,"native_attachments_changed");
            }catch(IOException e) when(e.Message is "native_composer_unconfirmed" or "native_frame_ambiguous" or
                "native_composer_boundary_unconfirmed" or "native_attachment_label_unavailable"){
                // The native dialog closes before the webview publishes its attachment label.
            }
            Thread.Sleep(100);
        }
        throw new IOException("native_attachments_unconfirmed");
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetClassName(nint window,StringBuilder value,int capacity);
    [DllImport("user32.dll")]private static extern nint GetWindow(nint window,uint command);
    [DllImport("user32.dll")]private static extern nint GetDlgItem(nint window,int id);
    [DllImport("user32.dll")]private static extern bool IsWindowEnabled(nint window);
    [DllImport("user32.dll")]private static extern bool PostMessage(nint window,uint message,nint wParam,nint lParam);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetWindowText(nint window,StringBuilder value,int capacity);
}
