using System.Text.Json;

namespace AIBotBridge;

internal static class RuntimeHealthSelfTest
{
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private static RuntimeHealthCheck Check(string state,DateTimeOffset at,string key="cpu")=>new(key,key,state,"系统繁忙","测试观察","检查持续负载。",at);
    internal static async Task RunAsync(string directory) {
        Directory.CreateDirectory(directory);var start=DateTimeOffset.UtcNow;var policy=new RuntimeHealthPolicy();
        for(int i=0;i<15;i++)policy.Apply(Check("warning",start.AddSeconds(i*2)),2,TimeSpan.FromSeconds(30));
        Require(policy.Alerts.Length==0,"brief load created alarm");
        policy.Apply(Check("warning",start.AddSeconds(30)),2,TimeSpan.FromSeconds(30));
        Require(policy.Alerts.Length==1&&policy.Sequence==1,"sustained load did not alarm");
        string eventId=policy.Alerts[0].Id;
        policy.Apply(Check("warning",start.AddSeconds(32)),2,TimeSpan.FromSeconds(30));
        Require(policy.Alerts[0].Id==eventId&&policy.Sequence==1,"same issue repeated event");
        var recovering=policy.Apply(Check("ok",start.AddSeconds(34)) with {Summary="CPU 正常",Advice=""},2,TimeSpan.FromSeconds(30));
        Require(policy.Alerts.Length==1&&policy.Alerts[0].Title=="系统繁忙"&&recovering.Summary=="系统繁忙","one healthy observation cleared or relabeled alarm");
        policy.Apply(Check("ok",start.AddSeconds(36)),2,TimeSpan.FromSeconds(30));Require(policy.Alerts.Length==0,"recovery did not clear alarm");
        policy.Apply(Check("warning",start.AddSeconds(38)),2,TimeSpan.FromSeconds(30));
        policy.Apply(Check("warning",start.AddMinutes(10)),2,TimeSpan.FromSeconds(30));Require(policy.Alerts.Length==0,"sleep gap counted as sustained pressure");
        var pressure=new SystemPressure(100,0,1,0,8000,16,start);
        var system=RuntimeHealthService.SystemChecks(new(30,60,0,0,start),pressure,new(),start);
        Require(system.Single(c=>c.Key=="queue").State=="ok","CPU queue alone falsely classified backlog");
        system=RuntimeHealthService.SystemChecks(new(99,95,0,0,start),pressure with {DiskQueue=10,DiskLatencyMs=100},new(),start);
        Require(system.All(c=>c.State=="warning"),"pressure combination not detected");
        Require(RuntimeHealthService.SystemChecks(new(0,0,0,0,start.AddMinutes(-1)),pressure,new(),start).First().State=="unknown","stale metrics green");
        var commitPressure=pressure with{CommitPercent=95,CommittedMemoryMb=9500,CommitLimitMb=10000};
        Require(RuntimeHealthService.SystemChecks(new(20,60,0,0,start),commitPressure,new(),start).Single(c=>c.Key=="memory").Summary.Contains("提交"),"commit exhaustion missed at low physical use");
        var pagingPressure=pressure with{PagesInputPerSecond=2000,AvailableMemoryMb=900,DiskLatencyMs=30,CommitPercent=80};
        Require(RuntimeHealthService.SystemChecks(new(20,60,0,0,start),pagingPressure,new(),start).Single(c=>c.Key=="memory").Summary.Contains("换页"),"sustained paging combination missed");
        Require(RuntimeHealthService.SystemChecks(new(20,60,0,0,start),pagingPressure with{AvailableMemoryMb=8000,CommitPercent=50},new(),start).Single(c=>c.Key=="memory").State=="ok","mapped file input falsely became paging pressure");
        var connections=new CodexConnectionHealth();
        Require(connections.Capture(start).State=="unknown","no business observation declared healthy");
        for(int i=0;i<3;i++)connections.Observe("model","network",start.AddSeconds(i));
        connections.Observe("usage","ok",start.AddSeconds(3));
        Require(connections.Capture(start.AddSeconds(4)).Summary=="Codex 连接不稳","usage success masked model connection errors");
        connections.Observe("model","ok",start.AddSeconds(5));Require(connections.Capture(start.AddSeconds(5)).State=="warning","one success cleared connection alarm");
        connections.Observe("model","ok",start.AddSeconds(6));Require(connections.Capture(start.AddSeconds(6)).State=="ok","two actual successes did not recover");
        connections.ObserveHttp(401,start.AddSeconds(7));Require(connections.Capture(start.AddSeconds(7)).State=="observing","single authenticated failure alerted");
        connections.ObserveHttp(401,start.AddSeconds(8));Require(connections.Capture(start.AddSeconds(8)).Summary.Contains("登录"),"authentication failure unclassified");
        Require(connections.Capture(start.AddMinutes(6)).State=="unknown","expired connection evidence retained healthy or failure state");
        var flapping=new CodexConnectionHealth();
        for(int i=0;i<3;i++){flapping.Observe("model","network",start.AddSeconds(i*3));if(i<2){flapping.Observe("model","ok",start.AddSeconds(i*3+1));flapping.Observe("model","ok",start.AddSeconds(i*3+2));}}
        Require(flapping.Capture(start.AddSeconds(7)).State=="warning","frequent reconnects masked by intermittent successful requests");
        var serverFailure=new CodexConnectionHealth();for(int i=0;i<3;i++)serverFailure.ObserveHttp(503,start.AddSeconds(i));
        var serverCheck=serverFailure.Capture(start.AddSeconds(4));
        Require(serverCheck.Summary.Contains("服务")&&new RuntimeHealthAlert("fixture","codex","warning",serverCheck.Summary,serverCheck.Advice,start).ShortAdvice=="稍后重试","service error incorrectly advised changing network settings");
        var model=new CodexConnectionHealth();
        for(int i=0;i<3;i++)model.ObserveModelEvent(JsonSerializer.Serialize(new{type="event_msg",timestamp=start.AddSeconds(i),payload=new{type="error",codex_error_info="ResponseStreamDisconnected",message="private-fixture"}}),start.AddSeconds(i));
        Require(model.Capture(start.AddSeconds(3)).State=="warning"&&!JsonSerializer.Serialize(model.Capture(start.AddSeconds(3))).Contains("private-fixture"),"model metadata classification or privacy failed");
        model.ObserveModelEvent(JsonSerializer.Serialize(new{type="event_msg",timestamp=start.AddSeconds(4),payload=new{type="turn_aborted"}}),start.AddSeconds(4));
        Require(model.Capture(start.AddSeconds(4)).State=="warning","user cancellation relabeled connection failure");
        var guardPolicy=new RuntimeHealthGuard.Policy();
        for(long now=1000;now<=70000;now+=2000)Require(guardPolicy.Update(now,now,now,true,true,0,true,false)==0,"fresh guard warned");
        int guardCode=0;for(long now=72000;now<=120000;now+=2000)guardCode=guardPolicy.Update(now,70000,now,true,true,0,true,false);
        Require(guardCode==1,"stalled UI not detected");
        Require(guardPolicy.Update(122000,122000,122000,true,true,0,true,false)==0,"guard did not recover");
        Require(guardPolicy.Update(124000,0,0,true,true,0,false,true)==0,"normal exit alerted");
        Require(guardPolicy.Update(126000,0,0,true,true,0,false,false)==4,"unexpected process exit missed");
        Require(guardPolicy.Update(128000,0,0,true,false,0,false,false)==0,"disabled monitor alerted");
        Require(guardPolicy.Update(500000,70000,70000,true,true,0,true,false)==0,"sleep gap caused immediate stale alarm");
        var stalledMonitor=new RuntimeHealthGuard.Policy();
        for(long now=1000;now<=70000;now+=2000)guardCode=stalledMonitor.Update(now,now,1000,true,true,0,true,false);
        Require(guardCode==2,"collector stopped without alarm");
        Require(WindowsPacRoute.ParseProxy("http://user:secret@localhost:7890")==null,"credential URI accepted");
        Require(WindowsPacRoute.ParseProxy("http=localhost:7890;https=localhost:7891")?.Port==7891,"HTTPS proxy selection");
        var proxy=new Uri("http://127.0.0.1:7890");
        RuntimeNetworkProbe Probe(bool pacAvailable,bool routeProxy=true,bool reachable=true,double ms=400)=>new() {
            Configuration=()=>new("http://127.0.0.1:7893/pac?private-value","127.0.0.1:7890",false),ProcessProxy=_=>null,
            Resolve=(_,_)=>new(true,routeProxy?proxy:null,routeProxy?"代理":"直连"),
            FetchPac=(_,_)=>Task.FromResult(new RuntimeProbeResult(pacAvailable,pacAvailable?200:null,10,pacAvailable?"":"连接失败")),
            Send=(_,_,_)=>Task.FromResult(new RuntimeProbeResult(reachable,reachable?401:null,reachable?ms:null,reachable?"":"超时"))
        };
        var options=new RuntimeHealthOptions(Throughput:false);
        var normal=await Probe(true).CaptureAsync(options,CancellationToken.None);
        Require(normal.Checks.All(c=>c.State=="ok"),"expected 401 incorrectly declared service outage");
        var brokenPac=await Probe(false).CaptureAsync(options,CancellationToken.None);
        Require(brokenPac.Checks.Single(c=>c.Key=="vpn").State=="ok"&&brokenPac.Checks.Single(c=>c.Key=="pac").Advice.Contains("重启"),"PAC and healthy exit conflated");
        var wrongRoute=await Probe(true,false).CaptureAsync(options,CancellationToken.None);
        Require(wrongRoute.Checks.Single(c=>c.Key=="pac").State=="error","wrong PAC route missed");
        var unavailable=await Probe(true,true,false).CaptureAsync(options,CancellationToken.None);
        Require(unavailable.Checks.Single(c=>c.Key=="vpn").State=="error","network loss missed");
        var slow=await Probe(true,true,true,4200).CaptureAsync(options,CancellationToken.None);
        Require(slow.Checks.Single(c=>c.Key=="vpn").State=="warning","slow responses missed");
        var networkPolicy=new RuntimeHealthPolicy();
        for(int i=0;i<3;i++)networkPolicy.Apply(brokenPac.Checks[1] with {UpdatedAt=start.AddSeconds(i*30)},3,TimeSpan.Zero,TimeSpan.FromSeconds(90));
        Require(networkPolicy.Alerts.Length==1,"three failed rounds did not alarm");
        int collectorStarts=0,collectorStops=0;
        using(var runtime=new BridgeRuntime(false,async (_,ct)=>{Interlocked.Increment(ref collectorStarts);try{await Task.Delay(Timeout.Infinite,ct);}finally{Interlocked.Increment(ref collectorStops);}})) {
            await runtime.ApplyDemandAsync(new(["system"],[]));await Task.Delay(50);
            Require(runtime.ActiveCollectors.SequenceEqual(new[]{"health","system"}),"system demand did not start both collectors");
            await runtime.ApplyDemandAsync(new([],[]));Require(collectorStarts==2&&collectorStops==2,"removing system demand left a monitor running");
        }
        var fixtures=new List<object>();
        void Fixture(string name,RuntimeNetworkRound net,SystemMetricsSnapshot metrics,SystemPressure pressure,bool systemBusy=false,RuntimeHealthCheck? extra=null) {
            var engine=new RuntimeHealthPolicy();RuntimeHealthCheck[] checks=[];
            for(int i=0;i<3;i++) {
                var at=start.AddSeconds(i*30);
                checks=net.Checks.Concat(RuntimeHealthService.SystemChecks(metrics with {UpdatedAt=at},pressure with {UpdatedAt=at},options,at)).Concat(extra is null?[]:new[]{extra})
                    .Select(c=>engine.Apply(c with {UpdatedAt=at},2,TimeSpan.Zero,TimeSpan.FromSeconds(90))).ToArray();
            }
            var atNow=start.AddSeconds(60);
            var latency=(net.Checks.Single(c=>c.Key=="vpn").LatencyMs??400);
            var trend=Enumerable.Range(0,10).Select(i=>new RuntimeLatencyPoint(atNow.AddSeconds((i-9)*30).ToUnixTimeSeconds(),
                name=="vpn-slow"?700+i*i*45:350+(i%4)*55,true)).ToArray();
            var health=new RuntimeHealthSnapshot(engine.Session,engine.Sequence,atNow,checks,engine.Alerts,pressure with {UpdatedAt=atNow},LatencyHistory:trend);
            var snapshot=new StatusSnapshot(1,"10:30:00",atNow.ToUnixTimeSeconds(),28800,atNow,new("working",2),new("idle",10),SystemMetrics:metrics,RuntimeHealth:health);
            using var frame=JsonDocument.Parse(Tab5Protocol.Snapshot(snapshot,"001122aabbcc","fixture-session",1));
            Require(frame.RootElement.GetProperty("data").GetProperty("runtimeHealth").GetProperty("alerts").GetArrayLength()==engine.Alerts.Length,"TAB5 health transport lost alerts");
            fixtures.Add(new{name,data=frame.RootElement.GetProperty("data").Clone()});
        }
        var healthyMetrics=new SystemMetricsSnapshot(34,62,162816,3952640,start) {SampleSession="monitor-preview",SampleSequence=64,
            Samples=Enumerable.Range(0,64).Select(i=>new NetworkSample(162816+(i%6)*3000,3952640+(i%12)*200000)).ToArray()};
        var healthyPressure=new SystemPressure(1,0,4,0,12500,16,start);
        Fixture("normal",normal,healthyMetrics,healthyPressure);
        Fixture("pac-fault",brokenPac,healthyMetrics,healthyPressure);
        Fixture("vpn-slow",slow,healthyMetrics,healthyPressure);
        Fixture("system-busy",normal,healthyMetrics with {CpuPercent=97,MemoryPercent=95},healthyPressure with {ProcessorQueue=48,DiskQueue=5,DiskLatencyMs=85,AvailableMemoryMb=450},true);
        Fixture("commit-pressure",normal,healthyMetrics,healthyPressure with{CommitPercent=95,CommittedMemoryMb=9500,CommitLimitMb=10000});
        Fixture("paging-pressure",normal,healthyMetrics,healthyPressure with{PagesInputPerSecond=2000,AvailableMemoryMb=900,DiskLatencyMs=30,CommitPercent=80});
        Fixture("codex-disconnect",normal,healthyMetrics,healthyPressure,extra:model.Capture(start.AddSeconds(4)));
        Fixture("bridge-stall",normal,healthyMetrics,healthyPressure,extra:RuntimeHealthGuard.Check(2,start,true));
        Fixture("codex-service",normal,healthyMetrics,healthyPressure,extra:serverCheck);
        File.WriteAllText(Path.Combine(directory,"preview-fixtures.json"),JsonSerializer.Serialize(new{simulated=true,scenarios=fixtures},JsonDefaults.Options));
        var encoded=File.ReadAllText(Path.Combine(directory,"preview-fixtures.json"));Require(!encoded.Contains("private-value"),"PAC URL query leaked");
        var notifications=new BridgeNotifications();var decision=notifications.Evaluate("health","same","PAC 异常",start);
        Require(decision.Show&&!notifications.Evaluate("health","same","PAC 异常",start.AddSeconds(30)).Show,"notification duplication");
        notifications.BalloonShown();Require(notifications.History.Single().Outcome=="系统已显示","native notification receipt was not recorded");
        var muted=new BridgeNotifications();muted.Save(new(Health:false));muted.Evaluate("health","muted","PAC 异常",start);muted.BalloonShown();
        Require(muted.History.Single().Outcome=="已关闭","suppressed notification became shown");
        Console.WriteLine("RUNTIME_HEALTH_SELF_TEST_OK sustained_pressure, commit_pressure, paging_combination, model_connection, auth_classification, independent_recovery, guard_heartbeat_exit_sleep, privacy, protocol, notification_dedup");
    }
    internal static async Task LiveAsync(string path) {
        var metricsService=new SystemMetricsService();
        using var pressure=new WindowsPressureSampler();await Task.Delay(2100);var sample=pressure.Capture(DateTimeOffset.UtcNow);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var net=await new RuntimeNetworkProbe().CaptureAsync(new(),timeout.Token);
        var metrics=await metricsService.CaptureForSelfTestAsync();
        var now=DateTimeOffset.UtcNow;
        var checks=RuntimeHealthService.SystemChecks(metrics with {UpdatedAt=metrics.UpdatedAt},sample,new(),now).Concat(net.Checks).ToArray();
        var health=new RuntimeHealthSnapshot("live-diagnostic",0,now,checks,[],sample,net.ThroughputMbps,net.ThroughputAt,LatencyHistory:net.LatencyHistory);
        string data=JsonSerializer.Serialize(new{live=true,hardwareVerified=false,runtimeHealth=health},JsonDefaults.Options);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);File.WriteAllText(path,data);
        Console.WriteLine(data);
    }
    internal static async Task WatchAsync(string path,int seconds) {
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        var metrics=new SystemMetricsService();var service=new RuntimeHealthService(()=>metrics.Snapshot);
        var metricsTask=metrics.RunAsync(stop.Token);var healthTask=service.RunAsync(stop.Token);
        var history=new List<RuntimeHealthSnapshot>();
        try{using var timer=new PeriodicTimer(TimeSpan.FromSeconds(2));while(await timer.WaitForNextTickAsync(stop.Token))if(service.Snapshot is {} sample)history.Add(sample);}
        catch(OperationCanceledException) when(stop.IsCancellationRequested){}
        finally{stop.Cancel();try{await Task.WhenAll(metricsTask,healthTask);}catch(OperationCanceledException) when(stop.IsCancellationRequested){}}
        Require(history.Count>=seconds/3,"monitor did not keep sampling");
        Require(history[^1].Checks.Any(c=>c.Key=="pac")&&history[^1].Pressure?.ProcessorQueue.HasValue==true,"production monitor did not collect PAC and scheduler pressure");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);File.WriteAllText(path,JsonSerializer.Serialize(new{live=true,hardwareVerified=false,samples=history},JsonDefaults.Options));
        Console.WriteLine($"RUNTIME_HEALTH_LIVE_WATCH_OK seconds={seconds} samples={history.Count} checks={history[^1].Checks.Length} alerts={history[^1].Alerts.Length}");
    }
    internal static void PreviewForm(string fixtures,string directory) {
        Directory.CreateDirectory(directory);using var document=JsonDocument.Parse(File.ReadAllText(fixtures));
        var health=document.RootElement.GetProperty("scenarios")[1].GetProperty("data").GetProperty("runtimeHealth").Deserialize<RuntimeHealthSnapshot>(JsonDefaults.Options)!;
        var fakeMetrics=document.RootElement.GetProperty("scenarios")[1].GetProperty("data").GetProperty("systemMetrics").Deserialize<SystemMetricsSnapshot>(JsonDefaults.Options)!;
        var service=new RuntimeHealthService(()=>fakeMetrics with {UpdatedAt=DateTimeOffset.UtcNow});
        foreach(float scale in new[]{1f,1.5f,2f})foreach(var size in new[]{new Size(880,650),new Size(680,500)}) {
            using var form=new RuntimeHealthForm(service,()=>health with {UpdatedAt=DateTimeOffset.UtcNow}){Opacity=0};form.ClientSize=size;form.Show();
            if(scale!=1)form.Scale(new SizeF(scale,scale));form.PerformLayout();Application.DoEvents();
            string suffix=scale==1?"":$"-{scale*100:0}pct";
            void Verify(Control parent) {
                foreach(Control control in parent.Controls) {
                    if(!control.Visible)continue;
                    if(control is Label or Button or CheckBox or NumericUpDown) {
                        Require(control.Left>=-2&&control.Right<=parent.ClientSize.Width+2,"monitor horizontal clipping: "+control.Text);
                        Require(control.Top>=-2&&control.Bottom<=parent.ClientSize.Height+2,"monitor vertical clipping: "+control.Text);
                    }
                    Verify(control);
                }
            }
            Verify(form);
            using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));
            bitmap.Save(Path.Combine(directory,$"bridge-monitor-{size.Width}{suffix}.png"),System.Drawing.Imaging.ImageFormat.Png);
            var tabs=(TabControl)form.Controls.Find("monitor-tabs",true).Single();tabs.SelectedIndex=1;Application.DoEvents();
            Verify(form);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(directory,$"bridge-monitor-settings-{size.Width}{suffix}.png"),System.Drawing.Imaging.ImageFormat.Png);form.Close();
        }
        Console.WriteLine("RUNTIME_HEALTH_FORM_PREVIEW_OK normal_and_minimum 100_150_200pct_layout native_WinForms_render; no_settings_written");
    }
}
