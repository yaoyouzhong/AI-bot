using System.Text;
using System.Text.Json;
using System.Windows.Automation;

namespace AIBotBridge;

internal static partial class Tab5CodexComposer
{
    private static readonly Lazy<Task> ReaderPreparation=new(()=>Task.Run(()=> {
        long began=Environment.TickCount64;
        try {
            // Initialize accessibility without reading/caching drafts, activating
            // windows or assuming that the eventual recording target is current.
            _=AutomationElement.RootElement.GetCurrentPropertyValue(AutomationElement.ControlTypeProperty);
            if(Tab5DesktopActivation.IsInteractive()&&ForegroundObserver.CodexVisible())
                _=AutomationElement.FromHandle(GetForegroundWindow()).GetCurrentPropertyValue(AutomationElement.ControlTypeProperty);
        }catch(Exception ex) when(IsUnavailable(ex)){ }
        finally {Tab5VoiceTiming.Log("reader-prepare",began);}
    }));
    internal static Task PrepareReaderAsync()=>AppPaths.IsPublicSelfTest?Task.CompletedTask:ReaderPreparation.Value;
    internal sealed record Target(string Key, AutomationElement Editor, AutomationElement Container, bool Question);
    internal sealed record QuestionSignature(string Text, string Action);
    internal static bool IsQuestionAction(string name)=>name is "Next" or "Send" or "Send update" or "Submit" or "下一步" or "发送" or "发送更新" or "提交";
    internal static bool IsSubmitAction(string name)=>name is "Send" or "Steer" or "Queue" or "发送" or "引导" or "排队";
    internal static bool QuestionActionAcknowledged(string before,string after)=>
        before=="Send"&&after=="Send update"||before=="发送"&&after=="发送更新";
    internal static QuestionSignature? QuestionInfo(string[] text,string[] buttons,int editors) {
        var actions=buttons.Where(IsQuestionAction).ToArray();
        if(editors!=1||actions.Length>1||actions.Length==0&&!buttons.Any(s=>s is "Skip" or "跳过")||text.Length<2||!text.Any(s=>s is "Question" or "问题"||s.StartsWith("Question ",StringComparison.Ordinal)||s.StartsWith("问题 ",StringComparison.Ordinal))||
            text.Sum(Encoding.UTF8.GetByteCount)>8192)return null;
        return new(JsonSerializer.Serialize(text),actions.FirstOrDefault()??"");
    }
    private static AutomationElement? Container(AutomationElement editor,bool cached=false) {
        AutomationElement? Parent(AutomationElement node)=>cached?node.CachedParent:TreeWalker.RawViewWalker.GetParent(node);
        var node=Parent(editor);
        AutomationElement? body=null;
        for(int i=0;i<32&&node is not null;i++,node=Parent(node)) {
            // A question may reuse the composer layout internally. Its outer
            // card still owns the answer and the Next/Submit action.
            var properties=cached?node.Cached:node.Current;
            if(HasClass(properties.ClassName,"@container/request-card"))return node;
            if(IsComposerBodyClass(properties.ClassName))body??=node;
            if(properties.ControlType==ControlType.Document||properties.ControlType==ControlType.Window)return body;
        }
        return null;
    }
    private static bool BelongsToDocument(AutomationElement editor,string title,bool cached=false) {
        var node=editor;
        for(int i=0;i<32&&node is not null;i++,node=cached?node.CachedParent:TreeWalker.RawViewWalker.GetParent(node)) {
            if((cached?node.Cached:node.Current).ControlType==ControlType.Document)
                return node.Current.AutomationId=="RootWebArea"&&node.Current.Name==title;
        }
        return false;
    }
    // Stop on an incomplete scan: a partial list cannot prove uniqueness. Do not
    // descend into answers when computing question identity (typing must not change it).
    private static List<AutomationElement> Scan(AutomationElement root,int limit,int milliseconds,bool skipInputs=false) {
        var result=new List<AutomationElement>();var pending=new Stack<(AutomationElement,int)>();
        pending.Push((root,0));long started=Environment.TickCount64;
        while(pending.TryPop(out var entry)) {
            if(result.Count>=limit||entry.Item2>48||Environment.TickCount64-started>milliseconds)
                throw new InvalidOperationException("uia_scan_limit");
            var node=entry.Item1;result.Add(node);
            if(skipInputs&&(node.Current.ControlType==ControlType.Edit||node.Current.ControlType==ControlType.Button))continue;
            var child=TreeWalker.RawViewWalker.GetFirstChild(node);int siblings=0;
            while(child is not null) {
                if(++siblings>limit||pending.Count>=limit||Environment.TickCount64-started>milliseconds)
                    throw new InvalidOperationException("uia_scan_limit");
                pending.Push((child,entry.Item2+1));child=TreeWalker.RawViewWalker.GetNextSibling(child);
            }
        }
        return result;
    }
    private static (QuestionSignature Signature,AutomationElement? Action)? QuestionInfo(AutomationElement card) {
        var nodes=Scan(card,300,500,true).Where(n=>!n.Current.IsOffscreen).ToArray();
        if(nodes.Any(n=>n.Current.ControlType==ControlType.Document||n.Current.IsPassword))return null;
        var buttons=nodes.Where(n=>n.Current.ControlType==ControlType.Button).ToArray();
        var signature=QuestionInfo(nodes.Where(n=>n.Current.ControlType==ControlType.Text).Select(n=>n.Current.Name).Where(n=>n.Length>0).ToArray(),
            buttons.Select(n=>n.Current.Name).ToArray(),nodes.Count(n=>n.Current.ControlType==ControlType.Edit));
        return signature is null?null:(signature,buttons.SingleOrDefault(b=>b.Current.Name==signature.Action));
    }
    private static Target? FindTarget(string title,bool cachedAncestry=true) {
        if(!ForegroundObserver.CodexVisible()){Volatile.Write(ref _editorDiagnostic,"foreground_not_codex");return null;}
        nint handle=GetForegroundWindow();
        if(!WindowMatches(handle,title)){Volatile.Write(ref _editorDiagnostic,"editor_document_mismatch");return null;}
        try {
            var window=AutomationElement.FromHandle(handle);
            // One native snapshot avoids thousands of cross-process property
            // calls. Identity and text are still read live before every mutation.
            var cache=new CacheRequest();
            cache.Add(AutomationElement.ClassNameProperty);cache.Add(AutomationElement.ControlTypeProperty);
            cache.Add(AutomationElement.IsOffscreenProperty);cache.Add(AutomationElement.IsEnabledProperty);
            AutomationElement[] nodes;
            if(cachedAncestry) {
                // One fresh raw-tree snapshot per observation, not a cache
                // across observations. Parent traversal then stays in-process.
                cache.TreeScope=TreeScope.Subtree;cache.TreeFilter=Automation.RawViewCondition;
                var root=window.GetUpdatedCache(cache);
                var found=new List<AutomationElement>();var pending=new Stack<(AutomationElement Node,int Depth)>();
                pending.Push((root,0));
                while(pending.TryPop(out var entry)) {
                    if(found.Count>=8000||entry.Depth>128)throw new InvalidOperationException("uia_scan_limit");
                    found.Add(entry.Node);
                    foreach(AutomationElement child in entry.Node.CachedChildren)pending.Push((child,entry.Depth+1));
                }
                nodes=found.ToArray();
            } else {
                // Retained only for explicit read-only before/after measurement.
                window.FindAll(TreeScope.Children,Condition.TrueCondition);
                AutomationElementCollection snapshot;
                using(cache.Activate())snapshot=window.FindAll(TreeScope.Descendants,Condition.TrueCondition);
                if(snapshot.Count>8000)throw new InvalidOperationException("uia_scan_limit");
                nodes=snapshot.Cast<AutomationElement>().ToArray();
            }
            var cards=nodes.Where(n=>HasClass(n.Cached.ClassName,"@container/request-card")&&!n.Cached.IsOffscreen).ToArray();
            var editors=nodes.Where(n=>n.Cached.ControlType==ControlType.Edit&&HasClass(n.Cached.ClassName,"ProseMirror")&&n.Cached.IsEnabled&&!n.Cached.IsOffscreen).ToArray();
            var candidates=new List<Target>();
            foreach(var editor in editors) {
                if(!BelongsToDocument(editor,title,cachedAncestry))continue;
                var container=Container(editor,cachedAncestry);if(container is null)continue;
                bool question=HasClass(container.Current.ClassName,"@container/request-card");
                if(cards.Length>0&&!question)continue;
                string key="main:"+Tab5CodexDeliveryReceipt.Digest(JsonSerializer.Serialize(new {
                    window=(long)handle,editor=editor.GetRuntimeId(),container=container.GetRuntimeId()}));
                if(question) {
                    var info=QuestionInfo(container);
                    if(info is null){Volatile.Write(ref _editorDiagnostic,"question_structure_unconfirmed");return null;}
                    key="question:"+Tab5CodexDeliveryReceipt.Digest(JsonSerializer.Serialize(new {
                        window=(long)handle,card=container.GetRuntimeId(),editor=editor.GetRuntimeId(),info.Value.Signature.Text}));
                }
                candidates.Add(new(key,editor,container,question));
            }
            if(candidates.Count!=1||cards.Length>1||cards.Length>0&&!candidates[0].Question) {
                Volatile.Write(ref _editorDiagnostic,cards.Length>0?"question_structure_unconfirmed":$"main_editor_count={candidates.Count}");return null;
            }
            if(GetForegroundWindow()!=handle||!WindowMatches(handle,title)){Volatile.Write(ref _editorDiagnostic,"foreground_or_document_changed");return null;}
            Volatile.Write(ref _editorDiagnostic,candidates[0].Question?"question_editor_verified":"main_editor_verified");
            return candidates[0];
        }catch(Exception ex) when(IsUnavailable(ex)) {
            Volatile.Write(ref _editorDiagnostic,ex.Message=="uia_scan_limit"?"uia_scan_limit":"composer_unavailable");return null;
        }
    }
    internal static DraftSnapshot? Capture(string task) {
        long began=Environment.TickCount64;
        string? title=UniqueTitle(task);
        Tab5VoiceTiming.Log("binding-title",began);
        if(title is null){Volatile.Write(ref _editorDiagnostic,"target_title_missing_or_duplicate");return null;}
        try {
            began=Environment.TickCount64;var target=FindTarget(title);
            Tab5VoiceTiming.Log("binding-target",began);
            if(target is null)return null;
            began=Environment.TickCount64;var result=new DraftSnapshot(target.Key,IsEmpty(target.Editor)?"":Read(target.Editor));
            Tab5VoiceTiming.Log("binding-text",began);return result;
        }
        catch(Exception ex) when(IsUnavailable(ex)){Volatile.Write(ref _editorDiagnostic,"composer_read_unavailable");return null;}
    }
    internal static object InspectDraftReadPerformance(string task) {
        var clock=System.Diagnostics.Stopwatch.StartNew();string? title=UniqueTitle(task);long titleMs=clock.ElapsedMilliseconds;
        if(title is null)return new{available=false,titleMs};
        var rows=new List<object>();
        for(int sample=0;sample<3;sample++) {
            clock.Restart();var first=FindTarget(title,cachedAncestry:false);
            if(first is null)return new{available=false,titleMs,diagnostic=EditorDiagnostic};
            var legacy=TargetDraft(first);
            long legacyMs=clock.ElapsedMilliseconds;
            clock.Restart();var single=ReadTargetDraft(title,null);long singleMs=clock.ElapsedMilliseconds;
            rows.Add(new{sample,legacyMs,singleMs,same=legacy is not null&&legacy==single});
        }
        return new{available=true,titleMs,samples=rows};
    }
    private static DraftSnapshot? ReadTargetDraft(string title,string? expectedTarget) {
        var target=FindTarget(title);
        if(target is null||expectedTarget is not null&&target.Key!=expectedTarget)return null;
        return TargetDraft(target);
    }
    private static DraftSnapshot TargetDraft(Target target)=>new(target.Key,IsEmpty(target.Editor)?"":Read(target.Editor));
    private static AutomationElement? FindAction(Target target) {
        if(target.Question)return QuestionInfo(target.Container)?.Action;
        var buttons=Scan(target.Container,700,500,true).Where(n=>n.Current.ControlType==ControlType.Button&&
            !n.Current.IsOffscreen&&IsSubmitAction(n.Current.Name)).ToArray();
        return buttons.Length==1?buttons[0]:null;
    }
    internal static object InspectIdentity(string task) {
        int windows=0,matchingTitles=0,documents=0,exactUrls=0;
        string? title=UniqueTitle(task);
        foreach(var process in System.Diagnostics.Process.GetProcessesByName("ChatGPT").Concat(System.Diagnostics.Process.GetProcessesByName("Codex")))using(process) {
            try {
                if(process.MainModule?.FileName.Contains("OpenAI.Codex_",StringComparison.OrdinalIgnoreCase)!=true||process.MainWindowHandle==0)continue;
                windows++;var window=AutomationElement.FromHandle(process.MainWindowHandle);
                var roots=window.FindAll(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Document),new PropertyCondition(AutomationElement.AutomationIdProperty,"RootWebArea")));
                foreach(AutomationElement root in roots) {
                    documents++;if(root.Current.Name==title)matchingTitles++;
                    string value=root.TryGetCurrentPattern(ValuePattern.Pattern,out var pattern)?((ValuePattern)pattern).Current.Value:"";
                    if(value==Tab5CodexDesktop.ThreadUri(task))exactUrls++;
                }
            }catch(Exception ex) when(IsUnavailable(ex)){}
        }
        return new{windows,documents,matchingTitles,exactThreadUrls=exactUrls,uniqueCatalogTitle=title is not null};
    }
}
