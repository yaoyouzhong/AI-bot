using System.Windows.Automation;

namespace AIBotBridge;

internal static partial class Tab5CodexComposer
{
    // Inspect every descendant, including nested controls. A toolbar icon is
    // decorative only inside a simple button; a nested removal button is not.
    internal sealed record LayoutNode(string Kind,string Name,string Classes,LayoutNode[] Children);
    internal static bool AttachmentFree(LayoutNode root) {
        int editors=0,bodies=0,count=0;
        bool EmptySlot(LayoutNode node,int depth) {
            if(++count>300||depth>48||node.Kind is not ("Group" or "Custom" or "Text")||!string.IsNullOrWhiteSpace(node.Name))return false;
            return node.Children.All(c=>EmptySlot(c,depth+1));
        }
        bool Visit(LayoutNode node,bool control,int depth) {
            if(++count>300||depth>48)return false;
            if(IsComposerBodyClass(node.Classes)&&++bodies>1)return false;
            if(HasClass(node.Classes,"@container/request-card"))return false;
            if(node.Classes.Split(' ').Any(c=>c.StartsWith("_ComposerLayoutAttachments_",StringComparison.Ordinal)||
                c.StartsWith("_ComposerLayoutReplyContext_",StringComparison.Ordinal)))return EmptySlot(node,depth);
            if(node.Kind is "Button" or "ComboBox"&&(node.Name.Contains("remove",StringComparison.OrdinalIgnoreCase)||node.Name.Contains("移除")||node.Name.Contains("删除附件")))return false;
            if(node.Kind is "Document" or "Hyperlink" or "Window" or "ListItem")return false;
            bool button=node.Kind is "Button" or "ComboBox";
            if(button&&control||node.Kind=="Image"&&!control)return false;
            if(node.Kind=="Edit"&&(control||!HasClass(node.Classes,"ProseMirror")||++editors>1))return false;
            return node.Children.All(c=>Visit(c,control||button,depth+1));
        }
        return Visit(root,false,0)&&editors==1&&bodies==1;
    }
    internal static string NativeLayoutGuard(string task,string identity) {
        if(!ForegroundObserver.CodexVisible())return "desktop_foreground_unconfirmed";
        string? title=UniqueTitle(task);if(title is null)return "target_title_missing_or_duplicate";
        var target=FindTarget(title);
        if(target is null||target.Question||target.Key!=identity)return "composer_changed";
        try {
            var cache=new CacheRequest {TreeScope=TreeScope.Subtree,TreeFilter=Automation.RawViewCondition};
            cache.Add(AutomationElement.NameProperty);cache.Add(AutomationElement.ClassNameProperty);cache.Add(AutomationElement.ControlTypeProperty);
            var parent=TreeWalker.RawViewWalker.GetParent(target.Container);
            if(parent is null||parent.Current.ControlType!=ControlType.Group&&
                !parent.Current.ClassName.Split(' ').Any(c=>c.StartsWith("_ComposerLayoutRoot_",StringComparison.Ordinal)))return "desktop_attachments_present";
            var root=parent.GetUpdatedCache(cache);int count=0;long began=Environment.TickCount64;
            LayoutNode Convert(AutomationElement node,int depth) {
                if(++count>300||depth>48||Environment.TickCount64-began>500)throw new InvalidOperationException("uia_scan_limit");
                var properties=node.Cached;
                return new(properties.ControlType.ProgrammaticName.Replace("ControlType.",""),properties.Name,properties.ClassName,
                    node.CachedChildren.Cast<AutomationElement>().Select(c=>Convert(c,depth+1)).ToArray());
            }
            return AttachmentFree(Convert(root,0))?"":"desktop_attachments_present";
        }catch(Exception ex) when(IsUnavailable(ex)){return "desktop_attachments_present";}
    }
    internal static async Task<string> AppendTaskAsync(string task,string text,string turn,Action dispatch,
        Func<CancellationToken,Task<bool>> current,Action<Tab5CodexDeliveryReceipt> capture,CancellationToken token,Func<bool>? authorized=null) {
        // This is an explicit user send, not a diagnostic/background navigation.
        if(authorized?.Invoke()==false)throw new Tab5CodexDesktop.Rejected("unauthorized");
        // Overlap read-only evidence with navigation and target validation. Do
        // not write a draft or click Send without a complete pre-action baseline.
        using var observation=CancellationTokenSource.CreateLinkedTokenSource(token);
        var baselineTask=Tab5CodexDeliveryReceipt.ReadAsync(task,text,observation.Token);
        try {
        if(!await Tab5QuickConsole.OpenAsync(task,token))throw new Tab5CodexDesktop.Rejected("desktop_foreground_unconfirmed");
        var before=Capture(task);
        if(before is null||!before.Identity.StartsWith("main:",StringComparison.Ordinal))throw new Tab5CodexDesktop.Rejected("composer_changed");
        if(before.Text.Length>0)throw new Tab5CodexDesktop.Rejected("desktop_draft_present");
        string Guard()=>authorized?.Invoke()==false?"unauthorized":NativeLayoutGuard(task,before.Identity);
        string guard=Guard();if(guard.Length>0)throw new Tab5CodexDesktop.Rejected(guard);
        if(!await current(token))throw new Tab5CodexDesktop.Rejected("turn_changed");
        var baseline=await baselineTask;
        if(!HasBaseline(baseline,task,text))throw new Tab5CodexDesktop.Rejected("delivery_baseline_unavailable");
        // Native submit invokes a button immediately after verified staging;
        // selection/caret positioning is only needed for a draft left for editing.
        var staged=await StageBoundAsync(task,text,before.Identity,token,true,Guard,dispatch,positionCaret:false);
        if(!staged.Staged||staged.Binding is null) {
            if(staged.Attempted)throw new IOException("Native staging unconfirmed");
            throw new Tab5CodexDesktop.Rejected(staged.Error);
        }
        // Revalidate latest turn after staging; never switch to IPC on failure.
        if(!await current(token))throw new Tab5CodexDesktop.Rejected("turn_changed");
        var result=await SubmitAsync(task,staged.Binding,token,capture,true,Guard,baseline);
        if(result.Submitted)return Guid.TryParse(result.TurnId,out _)?result.TurnId:turn;
        if(!result.Attempted)throw new Tab5CodexDesktop.Rejected(Guard() is {Length:>0} reason?reason:result.Error);
        throw new IOException("Native delivery unconfirmed");
        }finally{observation.Cancel();}
    }
}

// Bounded, monotonic-time evidence, never persisted/reconstructed on restart.
internal sealed class Tab5NativeReceiptCache(Func<long>? clock=null)
{
    private readonly object _gate=new();
    private readonly Dictionary<string,(long At,Tab5CodexDeliveryReceipt Receipt)> _items=[];
    private long Now=>clock?.Invoke()??Environment.TickCount64;
    private void Expire(){foreach(var item in _items.Where(p=>Now-p.Value.At>=60000).ToArray())_items.Remove(item.Key);}
    internal void Put(string id,Tab5CodexDeliveryReceipt receipt) {
        lock(_gate){Expire();if(receipt.Local is null||receipt.Server is null)return;
            if(_items.Count>=64&&!_items.ContainsKey(id))_items.Remove(_items.MinBy(p=>p.Value.At).Key);
            _items[id]=(Now,receipt);}
    }
    internal Tab5CodexDeliveryReceipt? Get(string id){lock(_gate){Expire();return _items.TryGetValue(id,out var value)?value.Receipt:null;}}
    internal void Remove(string id){lock(_gate)_items.Remove(id);}
    internal int Count {get{lock(_gate){Expire();return _items.Count;}}}
}
