using System.Diagnostics;
using System.IO.MemoryMappedFiles;

namespace AIBotBridge;

// One ephemeral child per tray session. No service, scheduled task, automatic
// restart or persistent system setting is installed. Only heartbeat integers
// cross this same-user mapping; it survives a crashed parent until reporting.
internal sealed class RuntimeHealthGuard : IDisposable
{
    private const int Ui=0,Monitor=8,Expected=16,Enabled=24,Exit=32,Faults=40,Code=48,GuardAt=56,Handled=64,Displayed=72,AlarmSequence=80;
    private readonly MemoryMappedFile _map;
    private readonly MemoryMappedViewAccessor _view;
    private readonly Process? _child;
    private readonly long _started=Environment.TickCount64;
    private RuntimeHealthGuard(MemoryMappedFile map,MemoryMappedViewAccessor view,Process? child){_map=map;_view=view;_child=child;}
    internal int ChildId=>_child?.Id??0;
    internal bool ChildExited=>_child?.HasExited??true;
    internal static RuntimeHealthGuard Start(string? testReport=null) {
        string name="Local\\AIBotBridge.Health."+Environment.ProcessId+"."+Guid.NewGuid().ToString("N");
        var map=MemoryMappedFile.CreateNew(name,128);var view=map.CreateViewAccessor();
        view.Write(Ui,Environment.TickCount64);view.Write(Enabled,1L);
        try {
            string executable=Path.ChangeExtension(typeof(Program).Assembly.Location,".exe");
            var launch=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
            launch.ArgumentList.Add("--runtime-health-guard");launch.ArgumentList.Add(Environment.ProcessId.ToString());launch.ArgumentList.Add(name);
            if(testReport is not null){launch.ArgumentList.Add("--self-test");launch.ArgumentList.Add(testReport);}
            var child=Process.Start(launch)??throw new IOException("监控守护未启动。");
            long began=Environment.TickCount64;
            while(view.ReadInt64(GuardAt)==0&&!child.HasExited&&Environment.TickCount64-began<5000)Thread.Sleep(25);
            if(view.ReadInt64(GuardAt)==0){view.Write(Exit,1L);child.Dispose();throw new IOException("监控守护未确认启动。");}
            return new(map,view,child);
        }catch{view.Write(Exit,1L);view.Dispose();map.Dispose();throw;}
    }
    internal void Pulse(RuntimeHealthSnapshot? sample,bool expected,bool enabled,int faults) {
        long now=Environment.TickCount64;
        _view.Write(Expected,expected?1L:0L);_view.Write(Enabled,enabled?1L:0L);_view.Write(Faults,(long)faults);
        _view.Write(Monitor,sample is null?0L:now-Math.Max(0,(long)(DateTimeOffset.UtcNow-sample.UpdatedAt).TotalMilliseconds));
        _view.Write(Ui,now);
    }
    internal RuntimeHealthCheck Capture(DateTimeOffset now) {
        bool ready=_view.ReadInt64(GuardAt)>0&&Environment.TickCount64-_view.ReadInt64(GuardAt)<10000;
        int code=(int)_view.ReadInt64(Code);
        if(!ready)code=Environment.TickCount64-_started<20000?0:5;
        var check=Check(code,now,_view.ReadInt64(Handled)>0);
        return !ready&&code==0?check with{State="observing",Summary="守护正在连接"}:check;
    }
    internal static RuntimeHealthCheck Check(int code,DateTimeOffset at,bool external=false) {
        string title=code switch{1=>"桥接界面无响应",2=>"监控采样已停滞",3=>"桥接服务异常",4=>"桥接异常退出",5=>"监控守护已中断",_=>"桥接心跳正常"};
        return new("bridge","桥接与采样",code==0?"ok":"error",title,code==0?"界面心跳、采样和服务任务受到独立守护。":"独立守护确认心跳或服务异常。",code==0?"":"重启桥接程序。",at){HandledExternally=external};
    }
    internal void BeginExit()=>_view.Write(Exit,1L);
    public void Dispose(){BeginExit();_child?.Dispose();_view.Dispose();_map.Dispose();}

    internal sealed class Policy {
        private long _last,_resumeUntil,_candidateAt;
        private int _candidate;
        internal int Update(long now,long ui,long monitor,bool expected,bool enabled,int faults,bool alive,bool intentional) {
            if(intentional||!enabled)return 0;
            if(!alive)return 4;
            if(_last==0||now-_last>10000){_resumeUntil=now+20000;_candidate=0;}
            _last=now;
            int next=now<_resumeUntil?0:faults>0?3:ui>0&&now-ui>15000?1:expected&&(monitor==0||now-monitor>10000)?2:0;
            if(next!=_candidate){_candidate=next;_candidateAt=now;}
            return next!=0&&now-_candidateAt>=30000?next:0;
        }
    }
    [STAThread]
    internal static void Run(int parentId,string name,string? testReport=null) {
        if(!name.StartsWith("Local\\AIBotBridge.Health."+parentId+".",StringComparison.Ordinal))throw new ArgumentException("Invalid guard session");
        using var parent=Process.GetProcessById(parentId);
        // Do not attach to a reused PID or unrelated executable.
        if(!string.Equals(parent.MainModule?.FileName,Path.ChangeExtension(typeof(Program).Assembly.Location,".exe"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Guard parent mismatch");
        using var map=MemoryMappedFile.OpenExisting(name);using var view=map.CreateViewAccessor();
        if(testReport is not null)AppPaths.BeginPublicSelfTest();
        ApplicationConfiguration.Initialize();
        using var context=new ApplicationContext();using var icon=new NotifyIcon{Icon=AppIcon.Load(),Text="AI-bot · 运行监控"};
        using var timer=new System.Windows.Forms.Timer{Interval=2000};
        var notices=new BridgeNotifications();var policy=new Policy();int previous=0;long sequence=0,hideAt=0,exitAt=0;
        void Evidence()=>File.WriteAllText(testReport!,System.Text.Json.JsonSerializer.Serialize(new{code=previous,sequence,shown=view.ReadInt64(Displayed),intentional=view.ReadInt64(Exit)!=0,parentExited=parent.HasExited},JsonDefaults.Options));
        icon.BalloonTipShown+=(_,_)=>{notices.BalloonShown();view.Write(Displayed,view.ReadInt64(Displayed)+1);if(testReport is not null)Evidence();};
        timer.Tick+=(_,_)=> {
            long now=Environment.TickCount64;bool intentional=view.ReadInt64(Exit)!=0,alive=!parent.HasExited;
            if(intentional){context.ExitThread();return;}
            int code=policy.Update(now,view.ReadInt64(Ui),view.ReadInt64(Monitor),view.ReadInt64(Expected)!=0,view.ReadInt64(Enabled)!=0,(int)view.ReadInt64(Faults),alive,intentional);
            view.Write(GuardAt,now);view.Write(Code,(long)code);
            if(code!=previous) {
                previous=code;view.Write(Handled,0L);
                if(code!=0) {
                    var check=Check(code,DateTimeOffset.UtcNow);string title="系统监控："+check.Summary;
                    var decision=notices.Evaluate("health",name+":"+(++sequence),title,DateTimeOffset.Now);
                    view.Write(AlarmSequence,sequence);
                    if(decision.Show){icon.Visible=true;icon.ShowBalloonTip(10000,(testReport is null?"":"监控自测 · ")+title,"建议重启桥接程序",check.State=="error"?ToolTipIcon.Error:ToolTipIcon.Warning);hideAt=now+15000;}
                    // A suppressed alert must not be shown again by the parent.
                    view.Write(Handled,1L);
                    if(decision.Sound)_=Task.Run(CompletionChime.Play);
                }
            }
            if(icon.Visible&&now>=hideAt)icon.Visible=false;
            if(!alive){if(exitAt==0)exitAt=now+16000;if(now>=exitAt)context.ExitThread();}
        };
        view.Write(GuardAt,Environment.TickCount64);
        timer.Start();Application.Run(context);if(testReport is not null)Evidence();icon.Visible=false;icon.Icon?.Dispose();
    }
}
