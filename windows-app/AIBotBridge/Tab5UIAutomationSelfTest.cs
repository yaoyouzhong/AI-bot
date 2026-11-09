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
        MessageLoop();
        RunnerAsync().GetAwaiter().GetResult();
        string current=Guid.NewGuid().ToString(),following=Guid.NewGuid().ToString(),photoId=Guid.NewGuid().ToString();
        JsonElement Request(string text,string turn,params string[] photos)=>JsonSerializer.SerializeToElement(new{message=text,expectedTurn=turn,images=photos});
        var photo=new Tab5NativeAcceptance.Step("photo","send","blue","",[photoId]);
        Check(photo.Matches("send",Request("blue","",photoId))&&!photo.Matches("send",Request("blue","")),"missing hardware photo cannot pass");
        var append=new Tab5NativeAcceptance.Step("append","steer","w",current,[]);
        Check(append.Matches("steer",Request("w",current))&&!append.Matches("send",Request("w",current))&&!append.Matches("steer",Request("w",following)),"append must be bound to original turn");
        Check(append.AcceptedTurn(following)&&!append.AcceptedTurn(current),"append requires a distinct observed human turn");
        var stop=new Tab5NativeAcceptance.Step("stop","interrupt","",following,[]);
        Check(stop.AcceptedTurn(following)&&!stop.AcceptedTurn(current)&&!stop.Matches("interrupt",Request("",current)),"interrupt cannot acknowledge a different task");
        Console.WriteLine("TAB5_UI_AUTOMATION_OK bounded command, exact acknowledgement, expiry and coordinate rejection");
        void Observe(string request,int status){using var doc=JsonDocument.Parse(JsonSerializer.Serialize(new{uiAutomation=$"1,{request},{status},1280,720,1"}));control.Observe(doc.RootElement);}
        void Check(bool value,string name){if(!value)throw new Exception("TAB5 automation: "+name);}
    }
    private static void MessageLoop(){
        var previous=SynchronizationContext.Current;
        int owner=Environment.CurrentManagedThreadId,ticks=0;
        Tab5AutomationRunner.RunWithMessageLoop(async()=>{
            using var dispatcher=new Control();dispatcher.CreateControl();
            using var timer=new System.Windows.Forms.Timer{Interval=10};
            timer.Tick+=(_,_)=>ticks++;timer.Start();
            await Task.Delay(60);
            if(Environment.CurrentManagedThreadId!=owner||ticks==0)throw new Exception("Automation STA did not pump timers and continuations");
            var dispatched=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            dispatcher.BeginInvoke(()=>dispatched.SetResult());
            await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(2));
            if(Environment.CurrentManagedThreadId!=owner||Application.OpenForms.Count!=0)throw new Exception("Automation must remain on its hidden STA");
        });
        var expected=new IOException("diagnostic failure");Exception? observed=null;
        try{Tab5AutomationRunner.RunWithMessageLoop(()=>throw expected);}catch(Exception error){observed=error;}
        if(!ReferenceEquals(observed,expected)||!ReferenceEquals(previous,SynchronizationContext.Current))throw new Exception("Automation swallowed a failure or leaked its context");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        bool stopped=false;
        try{Tab5AutomationRunner.RunWithMessageLoop(()=>Task.FromCanceled(cancelled.Token));}catch(OperationCanceledException){stopped=true;}
        if(!stopped)throw new Exception("Automation swallowed cancellation");
        Tab5AutomationRunner.RunWithMessageLoop(()=>Task.CompletedTask);
        Console.WriteLine("TAB5_AUTOMATION_STA_OK hidden message loop, timer, dispatch, continuation, failure and cancellation");
    }
    private static async Task RunnerAsync(){
        string directory=Path.Combine(Path.GetTempPath(),"aibot-ui-protocol-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try{
            var settings=new Tab5DisplaySettings("1234abcd",80,50,false,-1,false,15,5,true,[1,1,1,1,1,1,1,1],[0,1,2,3,4,5,6,7]);
            await SettingsReadAsync(settings);
            long now=200,actual=100;int wakes=0;
            var activity=new Tab5AutomationRunner.InputActivity(()=>actual,()=>now);
            if(activity.Read()!=100)throw new Exception("Diagnostic input changed before wake");
            var actions=new List<string>();
            var backend=new Tab5AutomationRunner.Backend((command,_)=>{
                string op=command.GetProperty("op").GetString()!;
                if(op!="probe"){if(command.GetProperty("width").GetInt32()!=1280||command.GetProperty("height").GetInt32()!=720||command.GetProperty("rotation").GetInt32()!=1)throw new ArgumentException("Stale viewport");}
                var validator=new Tab5UIAutomation();validator.Begin(command);actions.Add(op);
                return Task.FromResult(new Tab5UIAutomation.Result("fixture",2,1280,720,1));
            },_=>Task.FromResult(new Tab5ScreenPreview.Frame(2,2,new byte[8],0)),_=>Task.FromResult(settings),_=>{wakes++;activity.Wake();return Task.CompletedTask;});
            var input=new StringReader("{\"op\":\"tap\",\"x\":1,\"y\":2}\n{\"op\":\"capture\",\"file\":\"../escape.png\"}\n{\"op\":\"swipe\",\"x\":1,\"y\":2,\"toX\":3,\"toY\":4,\"durationMs\":100}\n{\"op\":\"capture\",\"file\":\"frame.png\"}\n{\"op\":\"capture\",\"file\":\"frame.png\"}\n{\"op\":\"tap\",\"x\":1,\"y\":2,\"rotation\":2}\n{\"op\":\"wake\"}\n{\"op\":\"stop\"}\n");
            var output=new StringWriter();await Tab5AutomationRunner.CommandsAsync(input,output,directory,backend,default);
            var lines=output.ToString().Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(s=>JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(s)!).ToArray();
            if(!actions.SequenceEqual(["probe","tap","swipe"])||lines.Count(r=>r["status"].GetString()=="error")!=3||lines.Count(r=>r["status"].GetString()=="wake-requested")!=1||wakes!=1||!lines.Last()["settingsPreserved"].GetBoolean()||!File.Exists(Path.Combine(directory,"frame.png")))throw new Exception("Windows automation protocol failed");
            now=210;if(activity.Read()!=200)throw new Exception("Wake heartbeat must remain stable");
            var snapshot=new StatusSnapshot(1,"12:00",1000,0,DateTimeOffset.UtcNow,new("idle",0),new("idle",0));
            using(var frame=JsonDocument.Parse(Tab5Protocol.Snapshot(snapshot,"001122334455","fixture",1,inputClock:activity.Read)))
                if(frame.RootElement.GetProperty("data").GetProperty("pcInput").GetProperty("lastInputTickMs").GetInt64()!=200)throw new Exception("Diagnostic wake did not reach the status frame");
            actual=205;if(activity.Read()!=205||new Tab5AutomationRunner.InputActivity(()=>actual,()=>now).Read()!=205)throw new Exception("Real input or diagnostic isolation lost");
            settings=settings with{Brightness=60};int reads=0;
            var changed=backend with{Settings=_=>Task.FromResult(++reads==1?settings:settings with{Brightness=80})};
            bool detected=false;try{await Tab5AutomationRunner.CommandsAsync(new StringReader("{\"op\":\"stop\"}\n"),new StringWriter(),directory,changed,default);}catch(IOException){detected=true;}
            if(!detected)throw new Exception("Changed settings reported preserved");
            Console.WriteLine("WINDOWS_TAB5_AUTOMATION_JSONL_OK viewport, bounded gestures, capture traversal/overwrite rejection and settings verification; synthetic transport");
        }finally{Directory.Delete(directory,true);}
    }
    private static async Task SettingsReadAsync(Tab5DisplaySettings settings){
        int checks=0,reads=0;
        var result=await Tab5AutomationRunner.ReadSettingsAsync(_=>{reads++;return Task.FromResult(settings);},()=>++checks<3,default);
        if(reads!=1||checks<3||result!=settings)throw new Exception("Settings read must wait for an idle device");
        bool busy=false;checks=reads=0;
        bool Busy(){if(!busy)return false;if(++checks<3)return true;busy=false;return false;}
        result=await Tab5AutomationRunner.ReadSettingsAsync(_=>{
            if(++reads==1){busy=true;throw new InvalidOperationException("concurrent RPC");}
            return Task.FromResult(settings);
        },Busy,default);
        if(reads!=2||result!=settings)throw new Exception("A concurrent RPC must not lose the settings read");
        using var cancelled=new CancellationTokenSource(TimeSpan.FromMilliseconds(80));reads=0;
        bool stopped=false;
        try{await Tab5AutomationRunner.ReadSettingsAsync(_=>{reads++;return Task.FromResult(settings);},()=>true,cancelled.Token);}
        catch(OperationCanceledException) when(cancelled.IsCancellationRequested){stopped=true;}
        if(!stopped||reads!=0)throw new Exception("Cancelled busy wait must not access device settings");
        bool failed=false;reads=0;
        try{await Tab5AutomationRunner.ReadSettingsAsync(_=>{reads++;throw new IOException("read unavailable");},()=>false,default);}
        catch(IOException){failed=true;}
        if(!failed||reads!=1)throw new Exception("Settings IO failure must remain a failed read");
        Console.WriteLine("TAB5_AUTOMATION_SETTINGS_WAIT_OK idle, RPC race, cancellation and IO failure; settings comparison unchanged");
    }
}
