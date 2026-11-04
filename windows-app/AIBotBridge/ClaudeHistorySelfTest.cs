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
            Console.WriteLine("CLAUDE_HISTORY_SELF_TEST_OK selected_branch_public_text_opaque_aliases_exact_submission_proof_failed_scan_retention");
        }finally{Directory.Delete(root,true);}}
}
