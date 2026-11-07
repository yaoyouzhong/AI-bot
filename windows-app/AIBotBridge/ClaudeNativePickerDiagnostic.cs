using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace AIBotBridge;

// Read-only OS control metadata. Does not access profiles, pairing or edit values.
internal static class ClaudeNativePickerDiagnostic
{
    internal static object Composer(int codePid){
        using var code=Process.GetProcessById(codePid);
        if(code.ProcessName!="Code")throw new ArgumentException("expected_code_process");
        var frames=new List<AutomationElement>();
        EnumWindows((window,_)=>{
            GetWindowThreadProcessId(window,out uint pid);
            if(pid==(uint)codePid&&IsWindowVisible(window)){
                frames.AddRange(AutomationElement.FromHandle(window).FindAll(TreeScope.Descendants,
                    new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Document),
                        new PropertyCondition(AutomationElement.AutomationIdProperty,"active-frame")))
                    .Cast<AutomationElement>().Where(e=>!e.Current.IsOffscreen));
            }
            return true;
        },0);
        if(frames.Count!=1)throw new IOException("native_frame_ambiguous");
        var editors=frames[0].FindAll(TreeScope.Descendants,new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit),
            new PropertyCondition(AutomationElement.NameProperty,"Message input")));
        if(editors.Count!=1)throw new IOException("native_composer_unconfirmed");
        var editor=editors[0];
        var parent=TreeWalker.ControlViewWalker.GetParent(editor);
        for(int depth=0;parent is not null&&depth<8;depth++,parent=TreeWalker.ControlViewWalker.GetParent(parent)){
            if(parent.GetRuntimeId().SequenceEqual(frames[0].GetRuntimeId()))break;
            if(parent.FindAll(TreeScope.Descendants,new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button),
                new PropertyCondition(AutomationElement.NameProperty,"Add"))).Count!=1)continue;
            var images=ClaudeNativeCode.AttachmentNames(parent);
            var remove=parent.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.NameProperty,"Remove attachment"));
            var chain=new List<object>();
            if(remove.Count==1){
                var node=TreeWalker.RawViewWalker.GetParent(remove[0]);
                for(int i=0;node is not null&&i<6;i++,node=TreeWalker.RawViewWalker.GetParent(node)){
                    if(node.GetRuntimeId().SequenceEqual(parent.GetRuntimeId()))break;
                    chain.Add(new{type=node.Current.ControlType.ProgrammaticName,
                        name=node.Current.Name.Length<=120?node.Current.Name:"omitted",
                        children=node.FindAll(TreeScope.Children,Condition.TrueCondition).Cast<AutomationElement>()
                            .Take(8).Select(child=>new{type=child.Current.ControlType.ProgrammaticName,
                                name=child.Current.Name.Length<=120?child.Current.Name:"omitted"}).ToArray()});
                }
            }
            string text=((ValuePattern)editor.GetCurrentPattern(ValuePattern.Pattern)).Current.Value;
            return new{readOnly=true,imageCount=images.Length,names=images,chain,blueProbe=images.SequenceEqual(["blue-probe.png"]),
                textEmpty=text.Length==0,placeholder=ClaudeNativeCode.KnownHint(text),
                editorFocused=editor.Current.HasKeyboardFocus,
                focusMatches=AutomationElement.FocusedElement is {} focused&&focused.GetRuntimeId().SequenceEqual(editor.GetRuntimeId()),
                width=editor.Current.BoundingRectangle.Width,height=editor.Current.BoundingRectangle.Height};
        }
        throw new IOException("native_composer_boundary_unconfirmed");
    }
    internal static object Controls(AutomationElement dialog){
        double bottom=dialog.Current.BoundingRectangle.Bottom;
        return dialog.FindAll(TreeScope.Descendants,Condition.TrueCondition).Cast<AutomationElement>()
            .Where(e=>(e.Current.BoundingRectangle.Top>bottom-180||e.Current.AutomationId=="1")&&
                e.Current.ControlType is {} type&&(type==ControlType.Button||type==ControlType.SplitButton||type==ControlType.Edit||type==ControlType.ComboBox))
            .Take(32).Select(e=>new{
                type=e.Current.ControlType.ProgrammaticName,id=e.Current.AutomationId,
                name=e.Current.Name.Length<=80&&!e.Current.Name.Contains('\\')&&!e.Current.Name.Contains('/')?e.Current.Name:"omitted",
                enabled=e.Current.IsEnabled,offscreen=e.Current.IsOffscreen,invoke=e.TryGetCurrentPattern(InvokePattern.Pattern,out _)
            }).ToArray();
    }
    internal static object Read(int codePid){
        var dialogs=Dialogs(codePid);
        if(dialogs.Count!=1)return new{readOnly=true,dialogs=dialogs.Count};
        var dialog=dialogs[0];
        var direct=dialog.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button));
        var exact=dialog.FindAll(TreeScope.Descendants,new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button),
            new PropertyCondition(AutomationElement.AutomationIdProperty,"1")));
        nint hwnd=dialog.Current.NativeWindowHandle;
        nint button=GetDlgItem(hwnd,1);
        var caption=new StringBuilder(128);var buttonClass=new StringBuilder(64);
        if(button!=0){GetWindowText(button,caption,caption.Capacity);GetClassName(button,buttonClass,buttonClass.Capacity);}
        return new{readOnly=true,dialogs=1,foreground=GetForegroundWindow()==hwnd,
            controls=Controls(dialog),directButtons=direct.Count,exactOpen=exact.Count,
            nativeOpen=new{exists=button!=0,visible=IsWindowVisible(button),enabled=IsWindowEnabled(button),type=buttonClass.ToString(),caption=caption.ToString()}};
    }
    private static List<AutomationElement> Dialogs(int codePid){
        using var code=Process.GetProcessById(codePid);
        if(code.ProcessName!="Code")throw new ArgumentException("expected_code_process");
        var dialogs=new List<AutomationElement>();
        EnumWindows((window,_)=>{
            var name=new StringBuilder(64);
            GetClassName(window,name,name.Capacity);
            GetWindowThreadProcessId(GetWindow(window,4),out uint owner);
            if(IsWindowVisible(window)&&name.ToString()=="#32770"&&owner==codePid)
                dialogs.Add(AutomationElement.FromHandle(window));
            return true;
        },0);
        return dialogs;
    }
    private delegate bool EnumCallback(nint window,nint parameter);
    [DllImport("user32.dll")]private static extern bool EnumWindows(EnumCallback callback,nint parameter);
    [DllImport("user32.dll")]private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(nint window,out uint pid);
    [DllImport("user32.dll")]private static extern nint GetWindow(nint window,uint command);
    [DllImport("user32.dll")]private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]private static extern nint GetDlgItem(nint window,int id);
    [DllImport("user32.dll")]private static extern bool IsWindowEnabled(nint window);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetWindowText(nint window,StringBuilder value,int capacity);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetClassName(nint window,StringBuilder value,int capacity);
}
