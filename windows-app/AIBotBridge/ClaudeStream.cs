using System.Diagnostics;
using System.Text;
using System.Text.Json;
namespace AIBotBridge;

// Local CLI transport. Consumers must whitelist public fields before export.
internal sealed class ClaudeStream : IDisposable
{
    private readonly Process _process;
    private readonly Guid _session;
    private readonly Action<JsonElement> _event;
    private readonly SemaphoreSlim _write=new(1,1);
    private readonly object _sync=new();
    private readonly Dictionary<string,TaskCompletionSource<JsonElement>> _waits=[];
    private bool _closed;
    internal ClaudeStream(string executable,string directory,Guid session,Action<JsonElement> receive,IReadOnlyDictionary<string,string>? environment=null,IEnumerable<string>? arguments=null,IEnumerable<string>? launcherArguments=null){
        _session=session;_event=receive;
        var info=new ProcessStartInfo(executable){WorkingDirectory=directory,UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8};
        foreach(string value in launcherArguments??[])info.ArgumentList.Add(value);
        foreach(string value in new[]{"--print","--input-format","stream-json","--output-format","stream-json","--verbose","--replay-user-messages","--permission-prompt-tool","stdio","--resume="+session})info.ArgumentList.Add(value);
        foreach(string value in arguments??[])info.ArgumentList.Add(value);
        if(environment is not null){info.Environment.Clear();foreach(var pair in environment)info.Environment[pair.Key]=pair.Value;}
        info.Environment.Remove("CLAUDECODE");info.Environment["CLAUDE_CODE_EMIT_SESSION_STATE_EVENTS"]="1";
        _process=new Process{StartInfo=info};
    }
    internal async Task StartAsync(CancellationToken token){
        if(!_process.Start())throw new IOException("Claude unavailable");
        _=Task.Run(ReadAsync);_=Task.Run(async()=>{try{var buffer=new char[4096];while(await _process.StandardError.ReadAsync(buffer)>0){} }catch(IOException){} });
        await ControlAsync("initialize",new{hooks=new Dictionary<string,object>{{"UserPromptSubmit",new[]{new{hookCallbackIds=new[]{"aibot-observe-prompt"}}}}}},token,TimeSpan.FromSeconds(30));
    }
    private async Task WriteAsync(object value,CancellationToken token=default){
        string json=JsonSerializer.Serialize(value);if(Encoding.UTF8.GetByteCount(json)>12*1024*1024)throw new IOException("Claude frame too large");
        await _write.WaitAsync(token);
        try{lock(_sync)if(_closed)throw new IOException("Claude unavailable");await _process.StandardInput.WriteLineAsync(json.AsMemory(),token);await _process.StandardInput.FlushAsync(token);}
        finally{_write.Release();}
    }
    private async Task ReadAsync(){
        try{
            var reader=new Tab5BoundedLines(_process.StandardOutput,12*1024*1024);
            while(await reader.ReadAsync() is {} line){
                using var doc=JsonDocument.Parse(line);var frame=doc.RootElement;
                if(Text(frame,"session_id") is {} raw&&(!Guid.TryParse(raw,out var id)||id!=_session)){Publish(new{type="bridge_protocol_error"});Dispose();break;}
                if(Text(frame,"type")=="control_response"&&frame.TryGetProperty("response",out var response)&&Text(response,"request_id") is {} request){
                    TaskCompletionSource<JsonElement>? waiter;lock(_sync)_waits.TryGetValue(request,out waiter);waiter?.TrySetResult(response.Clone());continue;
                }
                if(Text(frame,"type")=="control_request"&&frame.TryGetProperty("request",out var payload)&&Text(payload,"subtype")=="hook_callback"&&Text(frame,"request_id") is {} callback){
                    if(Text(payload,"callback_id")!="aibot-observe-prompt"){Publish(new{type="bridge_protocol_error"});Dispose();break;}
                    if(payload.TryGetProperty("input",out var input))Publish(new{type="bridge_prompt_submitted",input});
                    await RespondAsync(callback,new{});continue;
                }
                _event(frame.Clone());
            }
        }catch(Exception e) when(e is IOException or JsonException or InvalidOperationException){if(!_process.HasExited)_process.Kill(entireProcessTree:true);}
        finally{
            await _process.WaitForExitAsync();lock(_sync){_closed=true;foreach(var waiter in _waits.Values)waiter.TrySetException(new IOException("Claude closed"));_waits.Clear();}
            Publish(new{type="bridge_exit",status=_process.ExitCode});
        }
    }
    private void Publish(object value){using var doc=JsonDocument.Parse(JsonSerializer.Serialize(value));_event(doc.RootElement.Clone());}
    private async Task<JsonElement> ControlAsync(string subtype,object fields,CancellationToken token,TimeSpan? timeout=null){
        string id=Guid.NewGuid().ToString();var waiter=new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var data=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(JsonSerializer.Serialize(fields))!;data["subtype"]=JsonSerializer.SerializeToElement(subtype);
        lock(_sync)_waits[id]=waiter;
        try{await WriteAsync(new{type="control_request",request_id=id,request=data},token);
            var result=await waiter.Task.WaitAsync(timeout??TimeSpan.FromSeconds(5),token);
            if(Text(result,"subtype")!="success"||!result.TryGetProperty("response",out var response))throw new IOException("Claude control unconfirmed");return response.Clone();}
        finally{lock(_sync)_waits.Remove(id);}
    }
    internal Task SendAsync(Guid message,string text,IEnumerable<object> images,CancellationToken token){
        if(Encoding.UTF8.GetByteCount(text)>2000)throw new ArgumentException("message too long");
        var content=new List<object>();if(text.Length>0)content.Add(new{type="text",text});content.AddRange(images);
        if(content.Count==0||content.Count>4)throw new ArgumentException("invalid message");
        return WriteAsync(new{type="user",session_id=_session,uuid=message,parent_tool_use_id=(object?)null,client_composed=true,origin=new{kind="human"},message=new{role="user",content}},token);
    }
    internal Task<JsonElement> InterruptAsync(CancellationToken token)=>ControlAsync("interrupt",new{cancel_queued=true},token);
    internal Task RespondAsync(string id,object value,CancellationToken token=default)=>WriteAsync(new{type="control_response",response=new{subtype="success",request_id=id,response=value}},token);
    public void Dispose(){lock(_sync)_closed=true;try{if(!_process.HasExited)_process.Kill(entireProcessTree:true);}catch(InvalidOperationException){} }
    private static string? Text(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString():null;
}
