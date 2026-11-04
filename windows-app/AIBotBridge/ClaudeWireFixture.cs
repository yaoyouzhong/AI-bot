using System.Text.Json;

namespace AIBotBridge;

// Synthetic child process for transport tests. No files, network or credentials.
internal static class ClaudeWireFixture
{
    internal static async Task RunAsync(string[] args)
    {
        string session=args.Single(a=>a.StartsWith("--resume=",StringComparison.Ordinal))[9..];
        JsonElement? current=null;var queued=new List<JsonElement>();bool permission=false;
        async Task Emit(object value){await Console.Out.WriteLineAsync(JsonSerializer.Serialize(value));await Console.Out.FlushAsync();}
        async Task Result(JsonElement message,string reason="completed")=>await Emit(new{type="result",session_id=session,user_message_uuid=message.GetProperty("uuid").GetString(),terminal_reason=reason,is_error=reason!="completed",num_turns=1});
        async Task Finish(JsonElement message){
            await Emit(message);
            await Emit(new{type="assistant",session_id=session,message=new{content=new object[]{
                new{type="thinking",thinking="PRIVATE_REASONING_SENTINEL"},
                new{type="tool_use",name="Read",input=new{secret="PRIVATE_TOOL_SENTINEL"}},
                new{type="text",text="Synthetic public reply"}}}});
            await Result(message);
        }
        var reader=new Tab5BoundedLines(Console.In,12*1024*1024);
        while(await reader.ReadAsync() is {} line){
            using var json=JsonDocument.Parse(line);var frame=json.RootElement;
            switch(frame.GetProperty("type").GetString()){
                case "control_request":
                    bool interrupt=frame.GetProperty("request").GetProperty("subtype").GetString()=="interrupt";
                    await Emit(new{type="control_response",response=new{subtype="success",request_id=frame.GetProperty("request_id").GetString(),response=new{
                        cancelled=interrupt?queued.Select(m=>m.GetProperty("uuid").GetString()).ToArray():[],still_queued=Array.Empty<string>()}}});
                    if(interrupt){queued.Clear();if(current is {} active){await Emit(active);await Result(active,"interrupted");current=null;}permission=false;}
                    break;
                case "user":
                    if(current is not null){queued.Add(frame.Clone());await Emit(frame);break;}
                    current=frame.Clone();
                    string text=string.Concat(frame.GetProperty("message").GetProperty("content").EnumerateArray().Select(c=>c.TryGetProperty("text",out var t)?t.GetString():""));
                    if(text=="wait")break;
                    if(text=="hold"){await Emit(frame);break;}
                    if(text=="permission"){
                        permission=true;
                        for(int i=0;i<2;i++)await Emit(new{type="control_request",request_id="fixture-permission",request=new{subtype="can_use_tool",tool_name="Read",input=new{file_path="synthetic.txt"}}});
                        break;
                    }
                    await Finish(frame);current=null;break;
                case "control_response" when permission:
                    permission=false;
                    if(frame.GetProperty("response").GetProperty("response").GetProperty("behavior").GetString()=="allow")await Finish(current!.Value);
                    else{await Emit(current!.Value);await Result(current.Value,"denied");}
                    current=null;break;
            }
        }
    }
}
