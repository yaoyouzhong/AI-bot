using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class ClaudeWorkspaceSelfTest
{
    internal static async Task RunAsync()
    {
        string root=Path.Combine(Path.GetTempPath(),"aibot-claude-workspace-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string python=(Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator)
            .Select(p=>Path.Combine(p.Trim('"'),"python.exe")).FirstOrDefault(File.Exists)??throw new IOException("Python required for isolated wire test");
        string fixture=Path.GetFullPath("tools/claude_stream_fixture.py");
        Check(File.Exists(fixture),"synthetic CLI peer missing");
        var session=Guid.NewGuid();
        File.WriteAllText(Path.Combine(root,session+".jsonl"),JsonSerializer.Serialize(new{type="user",sessionId=session,cwd=root,timestamp=DateTimeOffset.UtcNow,message=new{role="user",content="Synthetic initial prompt"}})+"\n"+
            JsonSerializer.Serialize(new{type="custom-title",sessionId=session,customTitle="合成验收会话"})+"\n");
        try
        {
            using var workspace=new ClaudeWorkspace(()=>true,root,Path.Combine(root,"sealed"),live:_=>Task.FromResult(JsonSerializer.SerializeToElement(Array.Empty<object>())),
                streamFactory:(entry,receive)=>new ClaudeStream(python,entry.Directory,entry.Session,receive,launcherArguments:["-I","-u",fixture]));
            workspace.Start();
            await Until(()=>workspace.Alias(session) is not null);
            string task=workspace.Alias(session)!;
            Check(task!=session.ToString(),"raw session ID exposed");
            async Task<(int Status,JsonElement Body)> Rpc(string op,string text="",string expected="",string? id=null,string device="device-a",string[]? images=null,string turn="")
            {
                var request=JsonSerializer.SerializeToElement(new{op,taskId=task,requestId=id??Guid.NewGuid().ToString(),message=text,expectedTurn=expected,images=images??[],turnId=turn});
                var reply=await workspace.HandleAsync(request,device,default);
                return(reply.Status,JsonSerializer.SerializeToElement(reply.Body));
            }
            JsonElement Snapshot(string device="device-a")=>JsonSerializer.SerializeToElement(workspace.Snapshot(device:device));
            Check((await Rpc("draft-save","私有合成草稿")).Status==200,"draft save");
            Check((await Rpc("draft-load")).Status==200&&Snapshot().GetProperty("draftView").GetProperty("text").GetString()=="私有合成草稿","draft restore");
            Check(Snapshot("device-b").GetProperty("draftView").ValueKind==JsonValueKind.Null,"draft leaked across devices");
            string id=Guid.NewGuid().ToString();
            Check((await Rpc("send","Synthetic message",id:id)).Status==202,"send delivery");
            await Until(()=>workspace.Selection(null,DateTimeOffset.UtcNow)?.Outcome=="completed");
            Check((await Rpc("send","Synthetic message",id:id)).Status==202,"duplicate send not reconciled");
            Check((await Rpc("send","different",id:id)).Status==409,"conflicting request accepted");
            Check((await Rpc("receipt",id:id,device:"device-b")).Status==404,"receipt leaked across devices");
            Check((await Rpc("read")).Status==200,"public reply read");
            string wire=Snapshot().GetRawText();
            Check(wire.Contains("Synthetic public reply")&&!wire.Contains("PRIVATE_REASONING_SENTINEL")&&!wire.Contains("PRIVATE_TOOL_SENTINEL")&&!wire.Contains(session.ToString()),"public field filtering");
            var view=Snapshot().GetProperty("replyView");string turn=view.GetProperty("turnId").GetString()!,receipt=view.GetProperty("readReceipt").GetString()!;
            Check((await Rpc("read-ack",id:receipt,device:"device-b",turn:turn)).Status==409,"cross-device read acknowledgement");
            Check((await Rpc("read-ack",id:receipt,turn:turn)).Status==200,"exact read acknowledgement");
            Check(workspace.Selection(null,DateTimeOffset.UtcNow)?.CompletionAt==0,"read completion reminder retained");
            using var image=new Bitmap(2,2);using var png=new MemoryStream();image.Save(png,System.Drawing.Imaging.ImageFormat.Png);
            string imageId=Guid.NewGuid().ToString();
            Check(workspace.Upload("device-a",task,imageId,png.ToArray()).Status==200,"image upload");
            Check((await Rpc("draft-save","picture",images:[imageId],device:"device-b")).Status==503,"cross-device image reference accepted");
            Check((await Rpc("send","image",images:[imageId])).Status==202,"image send");
            await Until(()=>workspace.Selection(null,DateTimeOffset.UtcNow)?.Outcome=="completed");
            string permissionId=Guid.NewGuid().ToString();
            Check((await Rpc("send","permission",id:permissionId)).Status==504,"pending permission auto-approved");
            Check(workspace.Approvals().Length==1&&workspace.Selection(null,DateTimeOffset.UtcNow)?.NeedsInput==true,"duplicate approval or waiting state lost");
            var approval=workspace.Approvals().Single();
            await workspace.DecideAsync(approval,false);
            await Until(()=>workspace.Selection(null,DateTimeOffset.UtcNow)?.Outcome=="failed");
            Check((await Rpc("receipt",id:permissionId)).Status==202,"confirmed delivery lost after refusal");
            bool expired=false;try{await workspace.DecideAsync(approval,true);}catch(IOException){expired=true;}
            Check(expired,"expired approval accepted");
            string allowedId=Guid.NewGuid().ToString();
            Check((await Rpc("send","permission",id:allowedId)).Status==504,"second permission missing");
            await workspace.DecideAsync(workspace.Approvals().Single(),true);
            await Until(()=>workspace.Selection(null,DateTimeOffset.UtcNow)?.Outcome=="completed");
            Check((await Rpc("receipt",id:allowedId)).Status==202,"approved delivery receipt");
            string unknown=Guid.NewGuid().ToString();
            Check((await Rpc("send","wait",id:unknown)).Status==504,"unconfirmed delivery treated as success");
            Check((await Rpc("receipt",id:unknown)).Status==504&&(await Rpc("send","must not resend")).Status==409,"unknown delivery did not block resending");
            Check((await Rpc("read")).Status==200,"active read");
            string active=Snapshot().GetProperty("control").GetProperty("turnId").GetString()!;
            Check((await Rpc("interrupt",expected:active)).Status==202&&(await Rpc("receipt",id:unknown)).Status==202,"unknown request was not reconciled after interruption");
            Check((await Rpc("send","hold")).Status==202,"accepted running turn");
            Check((await Rpc("read")).Status==200,"running control read");
            active=Snapshot().GetProperty("control").GetProperty("turnId").GetString()!;
            string queued=Guid.NewGuid().ToString();
            Check((await Rpc("steer","queued draft",active,queued,images:[imageId])).Status==202,"queued append delivery");
            Check((await Rpc("read")).Status==200,"queued control refresh");
            active=Snapshot().GetProperty("control").GetProperty("turnId").GetString()!;
            Check((await Rpc("interrupt",expected:active)).Status==202,"interrupt acknowledgement");
            var cancelled=await Rpc("receipt",id:queued);
            Check(cancelled.Status==409&&cancelled.Body.GetProperty("deliveryFinal").GetBoolean(),"queue cancellation not final");
            Check((await Rpc("draft-load")).Status==200,"draft restore after queue cancellation");
            var draft=Snapshot().GetProperty("draftView");
            Check(draft.GetProperty("text").GetString()=="queued draft"&&draft.GetProperty("images").GetArrayLength()==1,"cancelled queue draft or image erased");
            Check((await Rpc("send","hold")).Status==202&&(await Rpc("read")).Status==200,"second running turn");
            active=Snapshot().GetProperty("control").GetProperty("turnId").GetString()!;
            Check((await Rpc("steer","older queued input",active)).Status==202,"second queued input");
            Check((await Rpc("draft-save","newer input")).Status==200&&(await Rpc("read")).Status==200,"newer draft save");
            active=Snapshot().GetProperty("control").GetProperty("turnId").GetString()!;
            Check((await Rpc("interrupt",expected:active)).Status==202&&(await Rpc("draft-load")).Status==200,"second interrupt");
            Check(Snapshot().GetProperty("draftView").GetProperty("text").GetString()=="newer input","queue cancellation overwrote newer draft");
            string journal=Directory.GetFiles(Path.Combine(root,"sealed","devices"),"drafts.dat",SearchOption.AllDirectories).Single();
            Check(!Encoding.UTF8.GetString(File.ReadAllBytes(journal)).Contains("queued draft"),"journal contains plaintext");
            workspace.StopOwners();
            using var restored=new ClaudeWorkspace(()=>true,root,Path.Combine(root,"sealed"));restored.Start();await Until(()=>restored.Alias(session)==task);
            Check(restored.Alias(session)==task,"DPAPI alias identity changed on restart");
            await restored.HandleAsync(JsonSerializer.SerializeToElement(new{op="draft-load",taskId=task,requestId=Guid.NewGuid().ToString()}),"device-a",default);
            Check(JsonSerializer.SerializeToElement(restored.Snapshot(device:"device-a")).GetProperty("draftView").GetProperty("text").GetString()=="newer input","encrypted draft was not restored after restart");
            Console.WriteLine("CLAUDE_WORKSPACE_NATIVE_OK DPAPI, exact delivery, dedup, public replies, device/image isolation, approval refusal/allow/expiry, queue cancel and restart; synthetic CLI peer only");
        }
        finally{Directory.Delete(root,true);}
    }
    private static void Check(bool value,string label){if(!value)throw new InvalidOperationException("Claude workspace: "+label);}
    private static async Task Until(Func<bool> condition){for(int i=0;i<150;i++){if(condition())return;await Task.Delay(20);}throw new TimeoutException("Claude synthetic state did not settle");}
    internal static async Task RunNativeAsync(string directory)
    {
        string root=Path.GetFullPath(directory);
        Check(File.Exists(Path.Combine(root,".aibot-synthetic-claude")),"synthetic marker missing");
        var environment=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllBytes(Path.Combine(root,"environment.json")))!;
        var arguments=JsonSerializer.Deserialize<string[]>(File.ReadAllBytes(Path.Combine(root,"arguments.json")))!;
        Check(environment.GetValueOrDefault("ANTHROPIC_API_KEY")=="synthetic-key"&&Uri.TryCreate(environment.GetValueOrDefault("ANTHROPIC_BASE_URL"),UriKind.Absolute,out var endpoint)&&endpoint.IsLoopback&&endpoint.Scheme=="http","loopback fixture required");
        foreach(string key in new[]{"HOME","USERPROFILE"})Check(Path.GetFullPath(environment[key]).TrimEnd(Path.DirectorySeparatorChar)==root.TrimEnd(Path.DirectorySeparatorChar),"isolated home required");
        string config=Path.GetFullPath(environment["CLAUDE_CONFIG_DIR"]);Check(config.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"isolated config required");
        string cli=File.ReadAllText(Path.Combine(root,"cli-path.txt")).Trim();Check(Path.IsPathFullyQualified(cli)&&File.Exists(cli),"installed CLI unavailable");
        using var workspace=new ClaudeWorkspace(()=>true,Path.Combine(config,"projects"),Path.Combine(root,"sealed-native"),live:_=>Task.FromResult(JsonSerializer.SerializeToElement(Array.Empty<object>())),
            streamFactory:(entry,receive)=>new ClaudeStream(cli,entry.Directory,entry.Session,receive,environment,arguments));
        workspace.Start();
        string? task=null;await Until(()=>{using var doc=JsonDocument.Parse(JsonSerializer.Serialize(workspace.Snapshot()));var rows=doc.RootElement.GetProperty("tasks").EnumerateArray().ToArray();task=rows.Length>0?rows[0].GetProperty("id").GetString():null;return task is not null;});
        async Task<(int Status,JsonElement Body)> Rpc(string op,string text="",string expected="",string? id=null,string[]? images=null){
            var result=await workspace.HandleAsync(JsonSerializer.SerializeToElement(new{op,taskId=task,requestId=id??Guid.NewGuid().ToString(),message=text,expectedTurn=expected,images=images??[]}),"native-fixture",default);
            return(result.Status,JsonSerializer.SerializeToElement(result.Body));
        }
        async Task Completed(){for(int i=0;i<150;i++){var r=await Rpc("read");if(r.Status==200&&r.Body.GetProperty("replyView").GetProperty("state").GetString()=="completed")return;await Task.Delay(100);}throw new TimeoutException("Native CLI result did not settle");}
        Check((await Rpc("send","NATIVE_TEXT")).Status==202,"native text delivery");await Completed();
        using var bitmap=new Bitmap(2,2);using var png=new MemoryStream();bitmap.Save(png,System.Drawing.Imaging.ImageFormat.Png);string image=Guid.NewGuid().ToString();
        Check(workspace.Upload("native-fixture",task!,image,png.ToArray()).Status==200&&(await Rpc("send","NATIVE_IMAGE",images:[image])).Status==202,"native image delivery");await Completed();
        Check((await Rpc("send","NATIVE_SLOW")).Status==202,"native slow turn delivery");
        await Rpc("read");var snapshot=JsonSerializer.SerializeToElement(workspace.Snapshot());string active=snapshot.GetProperty("control").GetProperty("turnId").GetString()!;
        string queued=Guid.NewGuid().ToString();var appended=await Rpc("steer","NATIVE_QUEUED",active,queued,images:[image]);Check(appended.Status is 202 or 504,"native queue append");
        await Rpc("read");snapshot=JsonSerializer.SerializeToElement(workspace.Snapshot());active=snapshot.GetProperty("control").GetProperty("turnId").GetString()!;
        Check((await Rpc("interrupt",expected:active)).Status==202,"native interrupt");
        var cancelled=await Rpc("receipt",id:queued);Check(cancelled.Status==409&&cancelled.Body.GetProperty("deliveryFinal").GetBoolean(),"native queued cancellation");
        await Rpc("draft-load");var draft=JsonSerializer.SerializeToElement(workspace.Snapshot(device:"native-fixture")).GetProperty("draftView");
        Check(draft.GetProperty("text").GetString()=="NATIVE_QUEUED"&&draft.GetProperty("images").GetArrayLength()==1,"native cancelled input recovery");
        Console.WriteLine("WINDOWS_INSTALLED_CLAUDE_LOOPBACK_OK exact resume, text, PNG, queued input and interrupt; isolated home/config, synthetic API only");
    }
}
