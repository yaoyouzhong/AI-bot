using System.Text.Json;
namespace AIBotBridge;
internal static class UserPreferencesSelfTest
{
    internal static void Run() {
        AppPaths.BeginPublicSelfTest();
        void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        var settings=BridgeSettings.Load();Check(settings.SaveEditable(new Dictionary<string,string>{["device_host"]="private-host",["serial_port"]="COM91",["weather_city"]="PrivateCity",["stock_symbols"]="hk00700",["display_cycle_pages"]="codex,weather",["weather_animation"]="plant"},out _),"Fixture save");
        BirthdayStore.Save([new("示例",2,28,false,false,3)]);var archive=ConfigurationBackup.Capture();
        string json=JsonSerializer.Serialize(archive);Check(!json.Contains("private-host")&&!json.Contains("COM91")&&!json.Contains("PrivateCity")&&!json.Contains("serial_port"),"Backup leaked excluded settings");
        string path=UserPreferenceFile.PathFor("export.json");ConfigurationBackup.Export(path,archive);var loaded=ConfigurationBackup.Read(path);Check(loaded.Settings["stock_symbols"]=="hk00700","Archive roundtrip");
        var incoming=loaded with {Settings=new(loaded.Settings){["stock_symbols"]="sh000001",["weather_animation"]="house"},Birthdays=[new("替换示例",1,1,false,false,0)]};
        string before=ConfigurationBackup.Restore(incoming,["stock_symbols"],false);var actual=BridgeSettings.Load();Check(actual.Get("stock_symbols")=="sh000001"&&actual.Get("weather_animation")=="plant"&&actual.Get("device_host")=="private-host"&&BirthdayStore.Load()[0].Name=="示例","Selective restore altered unselected fields");
        // The stock service and the reopened editor must agree after restore,
        // even while the original process settings snapshot still has old data.
        Check(settings.Get("stock_symbols")=="hk00700","Fixture no longer exercises a stale process snapshot");
        using(var editor=new SettingsForm(includeDeviceSettings:false)) {
            IEnumerable<Control> Children(Control parent)=>parent.Controls.Cast<Control>().SelectMany(c=>new[]{c}.Concat(Children(c)));
            var fields=Children(editor).OfType<TextBox>().Select(c=>c.Text).ToArray();
            Check(fields.Contains("sh000001")&&!fields.Contains("hk00700"),"Reopened stock editor showed pre-restore settings");
            Check(fields.Contains("PrivateCity"),"Reopened stock editor changed an unselected field");
        }
        Check(ConfigurationBackup.Read(before).Settings["stock_symbols"]=="hk00700","Pre-restore backup missing original");
        ConfigurationBackup.Restore(incoming,[],true);Check(BirthdayStore.Load()[0].Name=="替换示例","Birthday selected restore failed");
        try{ConfigurationBackup.Validate(incoming with {Settings=new(){["token"]="never"}});throw new Exception("Unknown key accepted");}catch(InvalidDataException){}
        try{ConfigurationBackup.Validate(incoming with {Settings=new(){["display_cycle_interval_seconds"]="11"}});throw new Exception("Invalid cycle accepted");}catch(InvalidDataException){}
        string a=Guid.NewGuid().ToString(),b=Guid.NewGuid().ToString(),c=Guid.NewGuid().ToString();Tab5TaskPins.Save([b]);
        var catalog=new[]{new Tab5CodexTask(a,"Current","Project",30,"p"),new Tab5CodexTask(b,"Pinned","Project",10,"p"),new Tab5CodexTask(c,"Other","Other",20,"q")};
        var selected=Tab5TaskPins.Select(catalog,2,a,null,[]);Check(selected.Length==2&&selected[0].Id==b&&selected[0].Pinned&&selected.Any(t=>t.Id==a),"Pinned task or active overview lost under frame limit");
        Check(Tab5CodexTasks.SelectOverview(catalog,new Dictionary<string,CodexLifecycleTracker.TaskActivity>())?.Id==a,"Pin changed overview priority");
        Check(UserPreferenceFile.Read<string[]>("tab5-task-pins.json")!.SequenceEqual([b]),"Pins not persistent");
        var notifications=new BridgeNotifications();var noon=new DateTimeOffset(DateTime.Today.AddHours(12));
        Check(notifications.Evaluate("completion","one","完成",noon).Sound,"Default completion lost");Check(!notifications.Evaluate("completion","one","完成",noon.AddMinutes(1)).Show&&notifications.History.Length==1,"Duplicate not suppressed");
        Check(!notifications.Evaluate("completion","two","完成",noon.AddSeconds(2)).Show,"Burst not coalesced");
        notifications.Save(new(Quiet:true));Check(!notifications.Evaluate("attention","night","操作",new(DateTime.Today.AddHours(23))).Show,"Overnight quiet start failed");
        Check(!notifications.Evaluate("attention","morning","操作",new(DateTime.Today.AddDays(1).AddHours(7))).Show,"Overnight quiet end failed");
        Check(notifications.Evaluate("attention","awake","操作",new(DateTime.Today.AddDays(1).AddHours(8))).Show,"Quiet exact end failed");
        notifications.Save(new(Completion:false,Quiet:true,QuietStart:600,QuietEnd:660));Check(notifications.Options.IsQuiet(DateTime.Today.AddHours(10))&&!notifications.Options.IsQuiet(DateTime.Today.AddHours(11)),"Daytime quiet range failed");
        Check(!notifications.Evaluate("completion","disabled","完成",noon.AddDays(2)).Show,"Disabled type delivered");
        Check(!new BridgeNotifications().Options.Completion,"Notification policy did not persist");
        for(int i=0;i<130;i++)notifications.Evaluate("quota","q"+i,"额度",noon.AddDays(3).AddMinutes(i*6));Check(notifications.History.Length==100,"History unbounded");
        int shapes=0,shows=0,flags=0,count=-3;
        PointerVisibility.RestoreIfHidden(flags,()=>{shapes++;flags=1;},()=>flags,()=>{shows++;return ++count;});
        Check(shapes==1&&shows==0,"Null cursor repair changed display count unnecessarily");
        flags=0;shapes=0;
        PointerVisibility.RestoreIfHidden(flags,()=>shapes++,()=>flags,()=>{shows++;return ++count;});
        Check(shapes==1&&shows==3&&count==0,"Hidden cursor count was not restored to visible");
        foreach(int protectedState in new[]{1,2,-1})PointerVisibility.RestoreIfHidden(protectedState,()=>throw new Exception("Visible/touch cursor changed"),()=>protectedState,()=>throw new Exception("Cursor count inflated"));
        int attempts=0;PointerVisibility.RestoreIfHidden(0,()=>{},()=>0,()=>{attempts++;return -100;});
        Check(attempts==16,"Cursor recovery is unbounded");
        Console.WriteLine("POINTER_RECOVERY_OK null_handle, hidden_counter, visible_and_touch_untouched, bounded_recovery");
        Console.WriteLine("USER_PREFERENCES_OK backup allowlist, selective restore, recovery backup, pins, quiet boundaries, deduplication, history and persistence");
    }
}
