using System.Text;
using System.Text.Json;
namespace AIBotBridge;
internal static class ClaudeHistorySelfTest
{
    internal static void Run(){string root=Path.Combine(Path.GetTempPath(),"aibot-claude-history-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try{Guid session=Guid.NewGuid(),first=Guid.NewGuid(),discarded=Guid.NewGuid(),tool=Guid.NewGuid(),answer=Guid.NewGuid(),second=Guid.NewGuid(),final=Guid.NewGuid();
            object Row(string type,Guid id,Guid? parent,object content)=>new{type,uuid=id,sessionId=session,parentUuid=parent,message=new{role=type,content},timestamp=DateTimeOffset.UtcNow.ToString("O"),cwd=root};
            object[] rows=[Row("user",first,null,"Excluded private prompt"),Row("assistant",discarded,first,new[]{new{type="text",text="Excluded discarded branch"}}),Row("user",tool,first,new[]{new{type="tool_result",content="Excluded tool output"}}),Row("assistant",answer,tool,new object[]{new{type="thinking",thinking="Excluded private reasoning"},new{type="text",text="第一轮公开回复"}}),Row("user",second,answer,"Excluded private followup"),Row("assistant",final,second,new[]{new{type="text",text="第二轮公开回复"}})];
            File.WriteAllText(Path.Combine(root,session+".jsonl"),string.Join("\n",rows.Select(r=>JsonSerializer.Serialize(r)))+"\n",new UTF8Encoding(false));
            byte[] key=Enumerable.Range(0,32).Select(i=>(byte)i).ToArray();var value=ClaudeHistory.Read(root,session,key);string json=JsonSerializer.Serialize(value.Turns);
            if(value.Turns.Count!=2||json.Contains("Excluded")||json.Contains(session.ToString())||!json.Contains("\\u7B2C")&&!json.Contains("第一轮公开回复")||!value.Accepted.Contains(second.ToString()))throw new InvalidOperationException("Claude public branch isolation failed");
            var catalog=new ClaudeCatalog(root,()=>key);catalog.Refresh();var entries=catalog.Snapshot();if(entries.Count!=1||entries[0].Session!=session||entries[0].Alias==session.ToString()||entries[0].Directory!=root)throw new InvalidOperationException("Claude opaque catalog failed");
            Directory.Move(root,root+"-moved");try{catalog.Refresh();throw new InvalidOperationException("Failed scan was accepted");}catch(DirectoryNotFoundException){}finally{Directory.Move(root+"-moved",root);}
            if(catalog.Snapshot().Count!=1)throw new InvalidOperationException("Claude catalog failure cleared data");
            var grouped=new ClaudeCatalog(root,()=>key,()=>[
                new(Guid.NewGuid(),DateTimeOffset.UtcNow,"Same directory",root.ToUpperInvariant()+Path.DirectorySeparatorChar),
                new(Guid.NewGuid(),DateTimeOffset.UtcNow,"Other workspace",Path.Combine(root,"other","workspace")),
                new(Guid.NewGuid(),DateTimeOffset.UtcNow,"Another workspace",Path.Combine(root,"another","workspace"))]);
            grouped.Refresh();var groupedEntries=grouped.Snapshot();
            if(groupedEntries.Count!=4||groupedEntries.Select(e=>e.Project).Distinct().Count()!=3||
                groupedEntries.Single(e=>e.Session==session).Project!=groupedEntries.Single(e=>e.Title=="Same directory").Project)
                throw new InvalidOperationException("Windows project path identity failed");
            Guid blank=Guid.NewGuid();
            File.WriteAllText(Path.Combine(root,blank+".jsonl"),JsonSerializer.Serialize(new{type="custom-title",sessionId=blank,customTitle="VS Code终端"})+"\n");
            catalog.Refresh();if(catalog.Snapshot().Count!=1)throw new InvalidOperationException("Title metadata invented a conversation");
            var namedBlank=new ClaudeCatalog(root,()=>key,()=>[new(blank,DateTimeOffset.UtcNow,"Claude Code",root)]);
            namedBlank.Refresh();if(namedBlank.Snapshot().Single(e=>e.Session==blank).Title!="VS Code终端")throw new InvalidOperationException("Verified blank panel lost its custom title");
            var blankHistory=ClaudeHistory.Read(root,blank,key,()=>true);
            if(blankHistory.Limited||blankHistory.Turns.Count!=0||blankHistory.Accepted.Count!=0)throw new InvalidOperationException("Title-only panel invented a turn");
            bool blankRejected=false;try{ClaudeHistory.Read(root,blank,key);}catch(IOException){blankRejected=true;}
            if(!blankRejected)throw new InvalidOperationException("Title-only history trusted without its live panel");
            File.AppendAllText(Path.Combine(root,blank+".jsonl"),"malformed tail");
            blankRejected=false;try{ClaudeHistory.Read(root,blank,key,()=>true);}catch(IOException){blankRejected=true;}
            if(!blankRejected)throw new InvalidOperationException("Damaged transcript treated as a blank panel");
            Guid nativeUser=Guid.NewGuid(),context=Guid.NewGuid(),context2=Guid.NewGuid(),peer=Guid.NewGuid(),nativeReply=Guid.NewGuid();
            object attachment(Guid id,Guid parent)=>Row("attachment",id,parent,new[]{new{type="text",text="Excluded internal context"}});
            object[] nativeRows=[Row("user",nativeUser,null,"Excluded native prompt"),attachment(context,nativeUser),
                new{type="user",uuid=peer,sessionId=session,parentUuid=context,isMeta=false,origin=new{kind="peer"},message=new{role="user",content="Excluded peer message"}},
                attachment(context2,peer),Row("assistant",nativeReply,context2,new[]{new{type="text",text="原生公开回复"}}),
                attachment(Guid.NewGuid(),nativeReply),attachment(Guid.NewGuid(),context)];
            File.WriteAllText(Path.Combine(root,session+".jsonl"),string.Join("\n",nativeRows.Select(r=>JsonSerializer.Serialize(r)))+"\n",new UTF8Encoding(false));
            var native=ClaudeHistory.Read(root,session,key);string visible=JsonSerializer.Serialize(native.Turns);
            if(native.Limited||native.Turns.Count!=1||!native.Accepted.SetEquals([nativeUser.ToString()])||
                (string)native.Turns[0]["id"]!=ClaudeCatalog.Alias("claude:turn:"+session+":"+nativeUser,key)||
                visible.Contains("Excluded")||!visible.Contains("\\u539F")&&!visible.Contains("原生公开回复"))throw new InvalidOperationException("Native context graph or peer receipt isolation failed");
            if(native.InputDigests.Count!=1||!native.InputDigests.ContainsKey(nativeUser.ToString())||native.InputDigests.ContainsKey(peer.ToString()))throw new InvalidOperationException("Native prompt digest or peer isolation failed");
            object End(Guid id,Guid parent,string kind,string body)=>new{type="assistant",uuid=id,sessionId=session,parentUuid=parent,message=new{role="assistant",stop_reason="end_turn",content=new[]{new{type=kind,text=kind=="text"?body:"",thinking=kind=="thinking"?body:""}}},timestamp="2026-11-05T13:00:00Z"};
            Guid thinking=Guid.NewGuid(),finished=Guid.NewGuid();string historyPath=Path.Combine(root,session+".jsonl");
            File.WriteAllText(historyPath,JsonSerializer.Serialize(Row("user",nativeUser,null,"Excluded native prompt"))+"\n"+JsonSerializer.Serialize(End(thinking,nativeUser,"thinking","Excluded private reasoning"))+"\n",new UTF8Encoding(false));
            var pending=ClaudeHistory.Read(root,session,key);
            if((string)pending.Turns.Single()["status"]!="unknown"||(long)pending.Turns[0]["endedAt"]!=0||JsonSerializer.Serialize(pending.Turns).Contains("Excluded"))throw new InvalidOperationException("Thinking-only end_turn falsely completed native turn");
            File.AppendAllText(historyPath,JsonSerializer.Serialize(End(finished,thinking,"text","公开完成回复"))+"\n",new UTF8Encoding(false));
            var completed=ClaudeHistory.Read(root,session,key);
            if((string)completed.Turns.Single()["status"]!="completed"||(long)completed.Turns[0]["endedAt"]!=DateTimeOffset.Parse("2026-11-05T13:00:00Z").ToUnixTimeSeconds())throw new InvalidOperationException("Native public end_turn did not complete its original turn");
            Guid duration=Guid.NewGuid(),nextUser=Guid.NewGuid();
            object Link(Guid id,Guid parent,string subtype)=>new{type="system",subtype,uuid=id,parentUuid=parent,sessionId=session,message=new{role="assistant",content="Excluded duration details"}};
            string prefix=File.ReadAllText(historyPath);
            void AppendLinked(string subtype){File.WriteAllText(historyPath,prefix+JsonSerializer.Serialize(Link(duration,finished,subtype))+"\n"+JsonSerializer.Serialize(Row("user",nextUser,duration,"Excluded next prompt"))+"\n"+JsonSerializer.Serialize(Link(Guid.NewGuid(),thinking,"turn_duration"))+"\n");}
            AppendLinked("turn_duration");var linked=ClaudeHistory.Read(root,session,key);
            if(linked.Limited||!linked.Accepted.SetEquals([nativeUser.ToString(),nextUser.ToString()])||linked.Turns.Count!=2||JsonSerializer.Serialize(linked.Turns).Contains("Excluded"))throw new InvalidOperationException("Terminal duration metadata broke selected ancestry or leaked content");
            AppendLinked("away_summary");var resumed=ClaudeHistory.Read(root,session,key);
            if(resumed.Limited||!resumed.Accepted.SetEquals([nativeUser.ToString(),nextUser.ToString()])||JsonSerializer.Serialize(resumed.Turns).Contains("Excluded"))throw new InvalidOperationException("Idle summary broke native turn ancestry");
            AppendLinked("informational");var fallback=ClaudeHistory.Read(root,session,key);
            if(fallback.Limited||!fallback.Accepted.SetEquals([nativeUser.ToString(),nextUser.ToString()])||JsonSerializer.Serialize(fallback.Turns).Contains("Excluded"))throw new InvalidOperationException("Native informational link broke ancestry or leaked content");
            AppendLinked("api_error");var retried=ClaudeHistory.Read(root,session,key);
            if(retried.Limited||!retried.Accepted.SetEquals([nativeUser.ToString(),nextUser.ToString()])||JsonSerializer.Serialize(retried.Turns).Contains("Excluded"))throw new InvalidOperationException("Native API retry metadata broke ancestry or leaked content");
            Guid retryOne=Guid.NewGuid(),retryTwo=Guid.NewGuid();
            File.WriteAllText(historyPath,JsonSerializer.Serialize(Row("user",nativeUser,null,"Excluded original prompt"))+"\n"+
                JsonSerializer.Serialize(Link(retryOne,nativeUser,"api_error"))+"\n"+JsonSerializer.Serialize(Link(retryTwo,retryOne,"api_error"))+"\n"+
                JsonSerializer.Serialize(End(finished,retryTwo,"text","Recovered public reply"))+"\n");
            var recovered=ClaudeHistory.Read(root,session,key);
            if(recovered.Limited||recovered.Turns.Count!=1||(string)recovered.Turns[0]["status"]!="completed"||
                !recovered.Accepted.SetEquals([nativeUser.ToString()])||recovered.InputDigests.Count!=1||JsonSerializer.Serialize(recovered.Turns).Contains("Excluded"))
                throw new InvalidOperationException("API retries severed the completed original user turn");
            AppendLinked("unknown_subtype");if(!ClaudeHistory.Read(root,session,key).Limited)throw new InvalidOperationException("Unverified system node counted as a verified link");
            Guid prompt=Guid.NewGuid(),notice=Guid.NewGuid();
            object User(Guid id,Guid? parent,Guid promptId,string value)=>new{type="user",uuid=id,parentUuid=parent,sessionId=session,promptId,message=new{role="user",content=new[]{new{type="text",text=value}}}};
            foreach(string marker in new[]{"[Request interrupted by user]","[Request interrupted by user for tool use]"}){
                object[] stoppedRows=[User(nativeUser,null,prompt,"w"),Row("assistant",nativeReply,nativeUser,new[]{new{type="text",text="公开的部分回复"}}),User(notice,nativeReply,prompt,marker)];
                File.WriteAllText(historyPath,string.Join("\n",stoppedRows.Select(row=>JsonSerializer.Serialize(row)))+"\n");
                var stopped=ClaudeHistory.Read(root,session,key);
                if((string)stopped.Turns.Single()["status"]!="interrupted"||!stopped.Accepted.SetEquals([nativeUser.ToString()])||stopped.InputDigests.ContainsKey(notice.ToString()))throw new InvalidOperationException("Native interrupt notice invented a user turn");
                File.AppendAllText(historyPath,JsonSerializer.Serialize(User(nextUser,notice,Guid.NewGuid(),marker))+"\n");
                if(!ClaudeHistory.Read(root,session,key).Accepted.Contains(nextUser.ToString()))throw new InvalidOperationException("Literal prompt was mistaken for a stop notice");
            }
            // The terminal supervisor accepts an @file reference in a text
            // message. Only the corresponding decoded native file attachment
            // on this branch proves an image submission.
            string materialized=Path.Combine(root,"private images",Guid.NewGuid()+".png");
            using var blue=new Bitmap(2,2);
            blue.SetPixel(0,0,Color.Blue);
            using var png=new MemoryStream();blue.Save(png,System.Drawing.Imaging.ImageFormat.Png);
            var bytes=png.ToArray();
            string imagePrompt="@\""+materialized.Replace('\\','/')+"\" check";
            string expected=ClaudeNativeContent.Digest("check",[ClaudeNativeContent.ImageDigest(bytes)]);
            string photoUser=JsonSerializer.Serialize(User(nativeUser,null,prompt,imagePrompt))+"\n";
            string ImageRow(string file)=>JsonSerializer.Serialize(new{type="attachment",uuid=context,parentUuid=nativeUser,sessionId=session,
                attachment=new{type="file",filename=file,content=new{type="image",file=new{type="image/png",base64=Convert.ToBase64String(bytes)}}}})+"\n";
            string photoReply=JsonSerializer.Serialize(End(nativeReply,context,"text","Blue"))+"\n";
            File.WriteAllText(historyPath,photoUser+ImageRow(materialized)+photoReply);
            if(ClaudeHistory.Read(root,session,key,backgroundImages:true).InputDigests[nativeUser.ToString()]!=expected)throw new InvalidOperationException("Native terminal image not bound to its user row");
            File.WriteAllText(historyPath,photoUser+ImageRow(Path.Combine(root,"different.png"))+photoReply);
            if(ClaudeHistory.Read(root,session,key,backgroundImages:true).InputDigests[nativeUser.ToString()]==expected)throw new InvalidOperationException("Wrong terminal image counted as delivered");
            Console.WriteLine("CLAUDE_HISTORY_SELF_TEST_OK selected_branch_public_text_opaque_aliases_exact_submission_proof_failed_scan_retention_native_context_links_peer_isolation_public_completion_only");
        }finally{Directory.Delete(root,true);}}
}
