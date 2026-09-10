namespace AIBotBridge;

internal static class AutoFollowSelfTest
{
    internal static void Run()
    {
        var tracker=new AutoFollowTracker();
        var idle=new StatusSnapshot(1,"",0,0,DateTimeOffset.UnixEpoch,new("idle",0),new("idle",0),DisplayPolicy:new("auto",false,15,["weather"]));
        void Check(StatusSnapshot s,long tick,string expected)
        {
            string app=tracker.Update(s,tick);
            if(app!=expected)throw new InvalidOperationException($"Follow timer at {tick}: {app}, expected {expected}");
            var live=s with{FollowApp=app};
            var json=DeviceStatusFrame.Create(live);
            if(json["data"]?["followApp"]?.GetValue<string>()!=app)throw new InvalidOperationException("USB lost authoritative follow state.");
        }
        Check(idle,100,"claude");Check(idle,6099,"claude");Check(idle,6100,"codex");
        var working=idle with{Codex=new("working",0),Claude=new("working",0)};
        Check(working,7000,"codex");Check(working,8099,"codex");Check(working,8100,"claude");
        var unique=idle with{Codex=new("working",0)};
        Check(unique,8500,"codex");Check(unique,9000,"codex");
        Check(working,10499,"codex");Check(working,10500,"claude");
        Check(idle,16499,"claude");Check(idle,16500,"codex");
        var hidden=idle with{DisplayPolicy=new("weather",false,15,["weather"])};
        Check(hidden,100000,"codex");Check(idle,100001,"claude");Check(idle,100001,"claude");
        var input=idle with{Codex=new("idle",0,NeedsInput:true)};
        Check(input,100100,"codex");Check(input,200000,"codex");
        Check(idle,200001,"claude"); // Holding the same tool does not restart the dwell timer.
        Check(idle with{DisplayPolicy=new("codex",false,15,[])},200100,"codex");
        Check(idle,206099,"codex");Check(idle,206100,"claude");
        var both=idle with{Codex=new("idle",0,NeedsInput:true),Claude=new("idle",0,NeedsInput:true),FollowApp="claude"};
        if(DisplayModes.Resolve(both,"weather")!="claude")throw new InvalidOperationException("Both input prompts must use the selected tool, not always Codex.");
        Console.WriteLine("AUTO_FOLLOW_SELF_TEST_OK dwell/unique/both/idle/hidden/manual/input/duplicate/USB");
    }
}
