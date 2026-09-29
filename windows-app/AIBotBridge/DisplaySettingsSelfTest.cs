using System.Collections.Concurrent;
using System.Reflection;

namespace AIBotBridge;

internal static class DisplaySettingsSelfTest
{
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static IEnumerable<Control> All(Control root)=>root.Controls.Cast<Control>().SelectMany(c=>new[]{c}.Concat(All(c)));
    private static void Commit(ComboBox combo,int index){combo.SelectedIndex=index;typeof(ComboBox).GetMethod("OnSelectionChangeCommitted",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(combo,[EventArgs.Empty]);}
    internal static void Run()
    {
        AppPaths.BeginPublicSelfTest();
        Check(BridgeSettings.Load().SaveEditable(new Dictionary<string,string>{["display_mode"]="auto",["display_cycle_enabled"]="1",["display_cycle_pages"]="weather,codex,stocks",["display_cycle_interval_seconds"]="15"},out _),"Fixture save failed");
        Exception? failure=null;
        using var runner=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-30000,-30000),Size=new(100,100)};
        runner.Shown+=async(_,_)=>{try{await VerifyAsync();await VerifyPreviewCompletionAsync();}catch(Exception ex){failure=ex;}finally{runner.Close();}};
        Application.Run(runner);if(failure is not null)throw failure;
        Console.WriteLine("DISPLAY_SETTINGS_OK no initial writes; debounced brightness, pending close, send failure; immediate mode/interval/selection/order persistence; preview synchronization");
    }
    private static async Task VerifyAsync()
    {
        var writes=new ConcurrentQueue<int>();var policies=new List<DisplayPolicy>();
        var read=new TaskCompletionSource<UsbDeviceInfo>();
        using var form=new CycleSettingsForm(()=>read.Task,level=>{writes.Enqueue(level);return true;},policies.Add){ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-30000,-30000)};
        form.Show();
        var slider=All(form).OfType<TrackBar>().Single();
        Check(!slider.Enabled,"Brightness changed before reading device");
        read.SetResult(new("ESP8266",1,"","auto",80,true,true,100,1,0));await Task.Delay(80);
        Check(slider.Enabled&&slider.Value==80&&writes.IsEmpty&&policies.Count==0,$"Opening display settings wrote to device or preferences: enabled={slider.Enabled}, level={slider.Value}, writes={writes.Count}, policies={policies.Count}");
        slider.Value=79;slider.Value=78;slider.Value=77;await Task.Delay(450);
        Check(writes.ToArray().SequenceEqual([77]),"Rapid brightness adjustments did not coalesce");
        var mode=All(form).OfType<ComboBox>().Single(c=>c.Name=="display-mode");
        var interval=All(form).OfType<ComboBox>().Single(c=>c.Name=="cycle-interval");
        var pages=All(form).OfType<ListView>().Single();
        Commit(mode,mode.Items.Cast<object>().Select((m,i)=>(m,i)).Single(m=>m.m.ToString()=="系统监控").i);
        var fixedPolicy=DisplayModes.Load(BridgeSettings.Load());
        Check(fixedPolicy.SelectedMode=="system"&&!fixedPolicy.CycleEnabled&&!pages.Enabled&&fixedPolicy.Pages.SequenceEqual(["weather","codex","stocks"]),"Fixed mode did not apply immediately or lost cycle selection");
        var completionRaw=new ToolState("idle",0);
        void Complete(){SessionActivityReader.Signals.Record("codex","TaskComplete");Check(SessionActivityReader.Signals.Apply("codex",completionRaw).CompletionActive,"Missing completion fixture");}
        Complete();Commit(mode,0);
        Check(!SessionActivityReader.Signals.Apply("codex",completionRaw).CompletionActive,"Display settings automatic mode retained completion hold");
        Complete();int applied=policies.Count;Commit(mode,0);
        Check(policies.Count==applied+1&&!SessionActivityReader.Signals.Apply("codex",completionRaw).CompletionActive,"Reselecting automatic mode did not resume cycling");
        Complete();interval.SelectedItem=30;
        Check(SessionActivityReader.Signals.Apply("codex",completionRaw).CompletionActive,"Changing interval acknowledged a completion without selecting automatic mode");
        SessionActivityReader.Signals.Acknowledge();
        pages.Items.Cast<ListViewItem>().Single(i=>i.Tag as string=="stocks").Checked=false;
        foreach(ListViewItem item in pages.Items)item.Selected=false;
        pages.Items[1].Selected=true;
        All(form).OfType<Button>().Single(b=>b.Text=="上移").PerformClick();
        var policy=DisplayModes.Load(BridgeSettings.Load());
        Check(policy.SelectedMode=="auto"&&policy.CycleEnabled&&policy.IntervalSeconds==30&&policy.Pages.SequenceEqual(["codex","weather"]),"Cycle change did not persist immediately");
        var snapshot=new StatusSnapshot(1,"12:00",0,0,DateTimeOffset.Now,new("idle",null),new("idle",null));
        using(var preview=new MirrorForm(()=>snapshot with {DisplayPolicy=DisplayModes.Load(BridgeSettings.Load())},()=>"auto")) {
            Check(preview.ShortcutPages.SequenceEqual(["codex","weather"]),"Preview ignored saved display selection");
            pages.Items.Cast<ListViewItem>().Single(i=>i.Tag as string=="weather").Checked=false;preview.SyncModes();
            Check(preview.ShortcutPages.SequenceEqual(["codex"]),"Preview retained unchecked page");
        }
        pages.Items.Cast<ListViewItem>().Single(i=>i.Tag as string=="codex").Checked=false;
        Check(pages.CheckedItems.Count==1&&DisplayModes.Load(BridgeSettings.Load()).Pages.SequenceEqual(["codex"]),"Last cycle page was lost");
        slider.Value=65;form.Close();await Task.Delay(200);
        Check(writes.ToArray().SequenceEqual([77,65]),"Closing lost final brightness change");
        Check(policies.Last().Pages.SequenceEqual(["codex"])&&DisplayModes.Load(BridgeSettings.Load()).Pages.SequenceEqual(["codex"]),"Closing discarded applied display choices");
        Check(DisplayModes.TrySelect("stocks",out var manual,out _)&&!manual.CycleEnabled&&manual.SelectedMode=="stocks","Preview selection did not persist manual mode");
        Check(DisplayModes.TrySelect("auto",out var automatic,out _)&&automatic.CycleEnabled&&automatic.SelectedMode=="auto"&&automatic.Pages.SequenceEqual(manual.Pages)&&automatic.IntervalSeconds==manual.IntervalSeconds,"Preview automatic did not restore saved cycle");
        using var failing=new CycleSettingsForm(()=>Task.FromException<UsbDeviceInfo>(new IOException()),_=>false){ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-30000,-30000)};
        failing.Show();await Task.Delay(80);All(failing).OfType<TrackBar>().Single().Value=40;await Task.Delay(400);
        Check(All(failing).OfType<Label>().Any(l=>l.Text.Contains("亮度未发送")),"Failed brightness send presented as success");failing.Close();
    }

    private static async Task VerifyPreviewCompletionAsync()
    {
        var now=DateTimeOffset.UtcNow;
        int clockAdvance=0;
        var raw=new ToolState("idle",0,CompletionSequence:100,CompletionAt:now.ToUnixTimeSeconds());
        var policy=new DisplayPolicy("auto",true,1,["codex","weather","stocks"],now.ToUnixTimeSeconds());
        StatusSnapshot Capture()=>DisplayModes.ExpireCompletionNotice(new(1,"12:00",DateTimeOffset.UtcNow.ToUnixTimeSeconds()+clockAdvance,0,DateTimeOffset.UtcNow,SessionActivityReader.Signals.Apply("codex",raw),new("idle",null),DisplayPolicy:policy));
        using var preview=new MirrorForm(Capture,()=>policy.SelectedMode,mode=>{
            Check(DisplayModes.TrySelect(mode,out var saved,out var error),error);
            // Accelerate only this isolated fixture; user settings and real tests keep 15 s.
            policy=policy with {SelectedMode=saved.SelectedMode,CycleEnabled=saved.CycleEnabled,CycleStartedAt=saved.CycleStartedAt};
            return true;
        }){StartPosition=FormStartPosition.Manual,PassiveTestWindow=true};
        var area=Screen.FromPoint(Cursor.Position).WorkingArea;preview.Location=new(area.Right-preview.Width-8,area.Bottom-preview.Height-8);
        async Task Until(Func<bool> ready){long end=Environment.TickCount64+5500;while(!ready()){if(Environment.TickCount64>end)throw new TimeoutException($"Preview timer/paint: frame={preview.DisplayedMode}, painted={preview.LastPaintedMode}, count={preview.PaintCount}, visible={preview.Visible}");await Task.Delay(30);}}
        preview.Show();
        var playback=All(preview).OfType<Label>().Single(l=>l.Name=="preview-playback");
        await Until(()=>preview.LastPaintedMode=="codex");
        Check(playback.Text.StartsWith("任务已完成")&&!playback.Text.Contains("秒后"),"Completion on scheduled Codex page falsely showed a countdown");
        await Task.Delay(1100);
        Check(preview.LastPaintedMode=="codex","Completion fixture did not reproduce occupied page");
        All(preview).OfType<Button>().Single(b=>b.Name=="preview-auto").PerformClick();
        Check(!Capture().Codex.CompletionActive,"Resuming cycling retained the old completion hold");
        Check(All(preview).OfType<Button>().Single(b=>b.Name=="preview-auto").Text=="已重新开始","Repeated auto click lacks confirmation");
        int paints=preview.PaintCount;
        using(var other=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-30000,-30000)}) {
            other.Show();other.Activate();
            typeof(Form).GetMethod("OnDeactivate",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(preview,[EventArgs.Empty]);
            Check(preview.Visible,"Preview hid on focus loss without a close action");
            await Until(()=>preview.LastPaintedMode=="weather");
            await Until(()=>preview.LastPaintedMode=="stocks");
            await Until(()=>preview.LastPaintedMode=="codex");
            Check(preview.Visible&&preview.PaintCount>paints+3,"Preview stopped painting after focus loss");
            Check(All(preview).OfType<Button>().Single(b=>b.Name=="preview-auto").Text=="轮播中","Automatic button did not settle into the running state");
        }
        preview.ShowPageMenu();preview.Controls.Find("preview-pages",false).Single().ContextMenuStrip!.Close(ToolStripDropDownCloseReason.AppClicked);
        Check(preview.Visible,"Closing page menu hid the whole preview");
        raw=raw with {CompletionSequence=101,CompletionAt=DateTimeOffset.UtcNow.ToUnixTimeSeconds()};
        await Until(()=>playback.Text.StartsWith("任务已完成"));
        Check(!playback.Text.Contains("秒后"),"New completion used a misleading countdown");
        long sequence=Capture().Codex.CompletionSequence;
        clockAdvance=DisplayModes.CompletionNoticeSeconds;
        await Until(()=>playback.Text.Contains("秒后"));
        Check(SessionActivityReader.Signals.Apply("codex",raw).CompletionSequence==sequence,"Notice expiry changed the task sequence");
        All(preview).OfType<Button>().Single(b=>b.AccessibleName=="收起预览").PerformClick();Check(!preview.Visible,"Explicit close stopped working");
        Console.WriteLine("PREVIEW_CONTINUOUS_OK completion hold reproduced/released; native timer+paint weather/stocks/codex; inactive window and menu dismissal remain visible; new completion notice retained");
    }

    // Explicit, exclusive hardware check. Retains the user's cycle list and brightness.
    internal static void RunHardware(int port)
    {
        if(System.Diagnostics.Process.GetProcessesByName("AIBotBridge").Any(p=>p.Id!=Environment.ProcessId))throw new InvalidOperationException("请先正常退出桥接。");
        Exception? failure=null;
        using var runner=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-30000,-30000),Size=new(100,100)};
        runner.Shown+=async(_,_)=>{try{await VerifyHardwareAsync(port);}catch(Exception ex){failure=ex;}finally{runner.Close();}};
        Application.Run(runner);
        if(failure is not null){Console.Error.WriteLine("ESP_DISPLAY_FAILED: "+failure.Message);Environment.ExitCode=1;}
    }
    private static async Task VerifyHardwareAsync(int port)
    {
        var registry=new DeviceRegistryStore().Snapshot;
        var esp=registry.Devices.Single(d=>d.Kind==HardwareKind.Esp8266&&d.Enabled);
        var original=DisplayModes.Load(BridgeSettings.Load());
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(120));
        using var runtime=new BridgeRuntime(startRefresh:false);
        runtime.SetDisplayPolicy(original);
        var manager=new DeviceServiceManager(runtime,port,stop.Token);
        int? brightness=null;
        async Task Until(Func<Task<bool>> ready) {
            long deadline=Environment.TickCount64+15000;
            while(!await ready()){if(Environment.TickCount64>deadline)throw new TimeoutException("实际设备未在预期时间内应用设置。");await Task.Delay(150,stop.Token);}
        }
        Task<UsbDeviceInfo> Read()=>Task.Run(manager.Serial.ReadDeviceInfo);
        try {
            await manager.ApplyAsync(registry with {Devices=[esp]});
            await Until(()=>Task.FromResult(manager.Serial.PortName is not null));
            var initial=await Read();brightness=initial.Brightness;
            using var form=new CycleSettingsForm(Read,manager.Serial.SendBrightness,p=>{runtime.SetDisplayPolicy(p with {CycleStartedAt=DateTimeOffset.UtcNow.ToUnixTimeSeconds()});manager.Serial.SendDisplayMode(p.SelectedMode);}){ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-30000,-30000)};
            form.Show();var slider=All(form).OfType<TrackBar>().Single();
            await Until(()=>Task.FromResult(slider.Enabled));
            Check(slider.Value==initial.Brightness,"Displayed brightness does not match the device");
            int changed=initial.Brightness>1?initial.Brightness-1:initial.Brightness+1;slider.Value=changed;
            await Until(async()=> (await Read()).Brightness==changed);
            Console.WriteLine($"ESP_BRIGHTNESS_UI_OK read={initial.Brightness} adjusted={changed} automatic=true");
            var mode=All(form).OfType<ComboBox>().Single(c=>c.Name=="display-mode");
            Commit(mode,mode.Items.Cast<object>().Select((m,i)=>(m,i)).Single(m=>m.m.ToString()=="系统监控").i);
            await Until(async()=>{var info=await Read();return info.Mode=="system"&&info.PageData is {} data&&data.TryGetProperty("rendered_page",out var rendered)&&rendered.GetString()=="system";});
            Console.WriteLine("ESP_DISPLAY_UI_OK mode=system rendered_page=system automatic=true");
            using(var preview=new MirrorForm(runtime.Capture,()=>DisplayModes.Load(BridgeSettings.Load()).SelectedMode,selected=>{
                Check(DisplayModes.TrySelect(selected,out var policy,out var error),error);runtime.SetDisplayPolicy(policy);manager.Serial.SendDisplayMode(selected);return true;
            }){ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-30000,-30000)}) {
                runtime.SetDisplayPolicy(original with {SelectedMode="auto",CycleEnabled=true,CycleStartedAt=DateTimeOffset.UtcNow.ToUnixTimeSeconds()});manager.Serial.SendDisplayMode("auto");
                SessionActivityReader.Signals.Record("codex","TaskComplete");
                Check(runtime.Capture().Codex.CompletionActive,"Hardware fixture did not create a completion hold");
                preview.Show();preview.Controls.OfType<Button>().Single(b=>b.Name=="preview-auto").PerformClick();
                Check(!runtime.Capture().Codex.CompletionActive,"Automatic click did not release completion hold");
                Check(DisplayModes.Load(BridgeSettings.Load()).CycleEnabled,"Preview did not enable cycling");
                string Page(string id)=>id.StartsWith("domestic_")?"domestic":id;
                await Until(async()=>{var info=await Read();return info.Mode=="auto"&&info.PageData?.GetProperty("rendered_page").GetString()==Page(original.Pages[0]);});
                string first=preview.DisplayedMode;
                await Task.Delay(TimeSpan.FromSeconds(original.IntervalSeconds+1),stop.Token);
                await Until(async()=>{var info=await Read();return info.Mode=="auto"&&info.PageData?.GetProperty("rendered_page").GetString()==Page(original.Pages[1%original.Pages.Length]);});
                Check(preview.DisplayedMode==original.Pages[1%original.Pages.Length],"Desktop preview did not advance with hardware");
                Console.WriteLine($"ESP_PREVIEW_AUTO_OK completion released, {first} -> {preview.DisplayedMode}; desktop and rendered hardware advanced");
                SessionActivityReader.Signals.Record("codex","TaskComplete");
                await Until(async()=>runtime.Capture().Codex.CompletionActive&&(await Read()).PageData?.GetProperty("rendered_page").GetString()=="codex");
                await Task.Delay(TimeSpan.FromSeconds(DisplayModes.CompletionNoticeSeconds+1),stop.Token);
                Check(!runtime.Capture().Codex.CompletionActive,"Completion display notice remained after its deadline");
                await Until(async()=>{var snapshot=runtime.Capture();string expected=DisplayModes.Resolve(snapshot,"auto");return expected!="codex"&&(await Read()).PageData?.GetProperty("rendered_page").GetString()==Page(expected);});
                Check(preview.Visible,"Preview hid during unattended cycling");
                Console.WriteLine("ESP_COMPLETION_TIMEOUT_OK new notice occupied Codex then expired; hardware resumed cycling without another click");preview.Close();
            }
            Commit(mode,0);slider.Value=initial.Brightness;await Until(async()=>{var info=await Read();return info.Mode=="auto"&&info.Brightness==initial.Brightness;});
            var restored=DisplayModes.Load(BridgeSettings.Load());
            Check(restored.CycleEnabled&&restored.Pages.SequenceEqual(original.Pages)&&restored.IntervalSeconds==original.IntervalSeconds,"Original cycle selection changed");
            form.Close();
            Console.WriteLine("ESP_DISPLAY_RESTORE_OK automatic cycle, original brightness/pages/order/interval");
        }finally {
            try {
                Check(BridgeSettings.Load().SaveEditable(new Dictionary<string,string>{["display_mode"]="auto",["display_cycle_enabled"]="1"},out _),"Could not restore automatic cycle");
                runtime.SetDisplayPolicy(original with {SelectedMode="auto",CycleEnabled=true});manager.Serial.SendDisplayMode("auto");
                if(brightness is int level) {
                    Check(manager.Serial.SendBrightness(level),"Could not restore device brightness");
                    Check((await Read()).Brightness==level,"Restored device brightness was not acknowledged");
                }
            }finally{stop.Cancel();await manager.StopAsync();}
        }
    }
}
