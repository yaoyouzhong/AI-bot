using System.Text.Json;
namespace AIBotBridge;
internal static class Tab5UIAutomationSelfTest
{
    internal static void Run(){
        var control=new Tab5UIAutomation();const long now=100000;
        using var tap=JsonDocument.Parse("{\"op\":\"tap\",\"width\":1280,\"height\":720,\"rotation\":1,\"x\":100,\"y\":200}");
        var id=control.Begin(tap.RootElement,now);
        using var command=JsonDocument.Parse(JsonSerializer.Serialize(control.CommandForFrame(now-2000,now)));
        Check(command.RootElement.GetProperty("expiresAt").GetInt64()==now+3000,"lease bound");
        Observe("wrong",2);Check(control.Snapshot() is null,"wrong request");Observe(id,1);Check(control.Snapshot()?.Status==1,"running");
        Observe(id,2);Check(control.Snapshot()?.Status==2&&control.CommandForFrame(now,now) is null,"complete");
        using var probe=JsonDocument.Parse("{\"op\":\"probe\"}");control.Begin(probe.RootElement,now);Observe(id,2);Check(control.Snapshot() is null,"late result");
        Check(control.CommandForFrame(now,now+5000) is null,"expiry");control.Cancel();
        using var invalid=JsonDocument.Parse("{\"op\":\"tap\",\"width\":1280,\"height\":720,\"rotation\":1,\"x\":1280,\"y\":0}");
        bool rejected=false;try{control.Begin(invalid.RootElement);}catch(ArgumentException){rejected=true;}Check(rejected,"bounds");
        RunnerAsync().GetAwaiter().GetResult();
        Console.WriteLine("TAB5_UI_AUTOMATION_OK bounded command, exact acknowledgement, expiry and coordinate rejection");
        void Observe(string request,int status){using var doc=JsonDocument.Parse(JsonSerializer.Serialize(new{uiAutomation=$"1,{request},{status},1280,720,1"}));control.Observe(doc.RootElement);}
        void Check(bool value,string name){if(!value)throw new Exception("TAB5 automation: "+name);}
    }
    private static async Task RunnerAsync(){
        string directory=Path.Combine(Path.GetTempPath(),"aibot-ui-protocol-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try{
            var settings=new Tab5DisplaySettings("1234abcd",80,50,false,-1,false,15,5,true,[1,1,1,1,1,1,1,1],[0,1,2,3,4,5,6,7]);
            var actions=new List<string>();
            var backend=new Tab5AutomationRunner.Backend((command,_)=>{
                string op=command.GetProperty("op").GetString()!;
                if(op!="probe"){if(command.GetProperty("width").GetInt32()!=1280||command.GetProperty("height").GetInt32()!=720||command.GetProperty("rotation").GetInt32()!=1)throw new ArgumentException("Stale viewport");}
                var validator=new Tab5UIAutomation();validator.Begin(command);actions.Add(op);
                return Task.FromResult(new Tab5UIAutomation.Result("fixture",2,1280,720,1));
            },_=>Task.FromResult(new Tab5ScreenPreview.Frame(2,2,new byte[8],0)),_=>Task.FromResult(settings));
            var input=new StringReader("{\"op\":\"tap\",\"x\":1,\"y\":2}\n{\"op\":\"capture\",\"file\":\"../escape.png\"}\n{\"op\":\"swipe\",\"x\":1,\"y\":2,\"toX\":3,\"toY\":4,\"durationMs\":100}\n{\"op\":\"capture\",\"file\":\"frame.png\"}\n{\"op\":\"capture\",\"file\":\"frame.png\"}\n{\"op\":\"tap\",\"x\":1,\"y\":2,\"rotation\":2}\n{\"op\":\"stop\"}\n");
            var output=new StringWriter();await Tab5AutomationRunner.CommandsAsync(input,output,directory,backend,default);
            var lines=output.ToString().Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(s=>JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(s)!).ToArray();
            if(!actions.SequenceEqual(["probe","tap","swipe"])||lines.Count(r=>r["status"].GetString()=="error")!=3||!lines.Last()["settingsPreserved"].GetBoolean()||!File.Exists(Path.Combine(directory,"frame.png")))throw new Exception("Windows automation protocol failed");
            settings=settings with{Brightness=60};int reads=0;
            var changed=backend with{Settings=_=>Task.FromResult(++reads==1?settings:settings with{Brightness=80})};
            bool detected=false;try{await Tab5AutomationRunner.CommandsAsync(new StringReader("{\"op\":\"stop\"}\n"),new StringWriter(),directory,changed,default);}catch(IOException){detected=true;}
            if(!detected)throw new Exception("Changed settings reported preserved");
            Console.WriteLine("WINDOWS_TAB5_AUTOMATION_JSONL_OK viewport, bounded gestures, capture traversal/overwrite rejection and settings verification; synthetic transport");
        }finally{Directory.Delete(directory,true);}
    }
}
