using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5CodexPublicHistorySelfTest
{
    internal static async Task RunAsync()
    {
        static JsonElement Json(object value)=>JsonSerializer.SerializeToElement(value);
        foreach(string mode in new[]{"ok","native","legacy","foreign","cycle","too-large","unsupported","cancel"}) {
            int pages=0;using var cancel=new CancellationTokenSource();
            Task<JsonElement> Call(string method,object args,CancellationToken token) {
                var p=Json(args);
                if(method=="thread/read") {
                    if(p.GetProperty("includeTurns").GetBoolean())throw new Exception("Unbounded history requested");
                    return Task.FromResult(Json(new{result=new{thread=new{id="task",status=new{type="idle"},turns=Array.Empty<object>()}}}));
                }
                if(method=="thread/turns/list") {
                    if(p.GetProperty("itemsView").GetString()!="notLoaded"||p.GetProperty("limit").GetInt32()!=1)throw new Exception("Turn directory contains bodies");
                    return Task.FromResult(mode=="unsupported"?Json(new{error=new{code=-32601}}):Json(new{result=new{data=new[]{new{id="turn",status="completed",completedAt=1}}}}));
                }
                if(method=="thread/turns/items/list"&&mode=="legacy")return Task.FromResult(Json(new{error=new{code=-32601}}));
                if(method!="thread/turns/items/list"&&!(mode=="legacy"&&method=="thread/items/list"))throw new Exception("Unexpected operation");
                pages++;
                if(mode=="cancel"){cancel.Cancel();token.ThrowIfCancellationRequested();}
                string text=mode=="too-large"?new string('x',1024*1024+1):"public-"+pages;
                if(mode=="native")return Task.FromResult(Json(new{result=new{data=new[]{new{type="agentMessage",text},new{type="reasoning",text="SYNTHETIC_PRIVATE"}},nextCursor=pages==1?"next":null}}));
                return Task.FromResult(Json(new{result=new{data=new[]{
                    new{turnId=mode=="foreign"?"other":"turn",item=new{type="agentMessage",text}},
                    new{turnId="turn",item=new{type="reasoning",text="SYNTHETIC_PRIVATE"}},
                    new{turnId="turn",item=new{type="commandExecution",text="SYNTHETIC_TOOL"}}
                },nextCursor=mode=="cycle"||pages==1?"next":null}}));
            }
            bool rejected=false;
            try {
                var value=await Tab5CodexPublicHistory.ReadAsync("task",true,Call,cancel.Token);
                string json=value.GetRawText();
                if(mode is not ("ok" or "native" or "legacy")||pages!=2||!json.Contains("public-1")||!json.Contains("public-2")||json.Contains("SYNTHETIC_"))throw new Exception("Public history isolation");
            }catch(Exception ex) when(ex is IOException or OperationCanceledException){rejected=true;}
            if(rejected!=(mode is not ("ok" or "native" or "legacy")))throw new Exception("History boundary not enforced: "+mode);
        }
        string latest=Guid.NewGuid().ToString(),previous=Guid.NewGuid().ToString();
        Task<JsonElement> PageCall(string method,object args,CancellationToken token) {
            var p=Json(args);
            return Task.FromResult(method switch {
                "thread/read"=>Json(new{result=new{thread=new{id="task",status=new{type="notLoaded"}}}}),
                "thread/turns/list" when p.GetProperty("cursor").ValueKind==JsonValueKind.Null=>
                    Json(new{result=new{data=new[]{new{id=latest,status="interrupted"}},nextCursor="older"}}),
                "thread/turns/list"=>Json(new{result=new{data=new[]{new{id=previous,status="completed"}}}}),
                "thread/items/list"=>Json(new{result=new{data=new[]{new{turnId=p.GetProperty("turnId").GetString(),
                    item=new{type="agentMessage",phase="commentary",text="公开进度"}}}}}),
                _=>throw new Exception("Unexpected App Server operation")
            });
        }
        var current=await Tab5CodexPublicHistory.ReadPageAsync("task","",0,PageCall,CancellationToken.None);
        var older=await Tab5CodexPublicHistory.ReadPageAsync("task",latest,-1,PageCall,CancellationToken.None);
        if(current.TurnId!=latest||current.Status!="stored"||current.Runtime!="notLoaded"||current.PreviousTurnId!=previous||
           !Tab5CodexActivity.FromDesktop(current,-1).Text.Contains("公开进度")||
           older.TurnId!=previous||older.Status!="completed"||!older.IsHistory||older.NextTurnId!=latest)
            throw new Exception("App Server reply navigation and runtime isolation");
        var lines=new Tab5BoundedLines(new StringReader("one\r\ntwo\nthree"),10);
        if(await lines.ReadAsync()!="one"||await lines.ReadAsync()!="two"||await lines.ReadAsync()!="three"||await lines.ReadAsync() is not null)throw new Exception("Buffered line framing");
        foreach(string value in new[]{new string('x',11),"中文中文"}) {
            bool rejected=false;try{await new Tab5BoundedLines(new StringReader(value),10).ReadAsync();}catch(IOException){rejected=true;}
            if(!rejected)throw new Exception("Response line bound");
        }
        Console.WriteLine("TAB5_PUBLIC_HISTORY_OK bounded paging/public-only/ownership/cursor/size/cancel/unsupported/line-framing");
    }
}
