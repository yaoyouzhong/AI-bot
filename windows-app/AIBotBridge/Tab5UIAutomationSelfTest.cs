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
        Console.WriteLine("TAB5_UI_AUTOMATION_OK bounded command, exact acknowledgement, expiry and coordinate rejection");
        void Observe(string request,int status){using var doc=JsonDocument.Parse(JsonSerializer.Serialize(new{uiAutomation=$"1,{request},{status},1280,720,1"}));control.Observe(doc.RootElement);}
        void Check(bool value,string name){if(!value)throw new Exception("TAB5 automation: "+name);}
    }
}
