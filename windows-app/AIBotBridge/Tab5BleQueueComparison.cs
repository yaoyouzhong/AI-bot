using System.Text.Json;

namespace AIBotBridge;

internal sealed record Tab5BleQueueProbe(string Result,string Radio,string WifiIsolation,string Trace,int FirmwareBytes);

// Explicit, RAM-only ABA comparison; never selects a production setting.
internal sealed class Tab5BleQueueComparison
{
    private int _running,_nativeLimit=7,_writeWindow=Tab5BleMailboxWindow.DefaultBulkWriteCredits;
    private volatile string _json="尚未对照",_summary="尚未对照";
    internal int NativeLimit=>Volatile.Read(ref _nativeLimit);
    internal int WriteWindow=>Volatile.Read(ref _writeWindow);
    internal string Json=>_json;
    internal string Summary=>_summary;
    internal static bool Complete(Tab5BleQueueProbe data) {
        if(Tab5BleOtaEstimate.Seconds(data.Result,1024) is null)return false;
        var p=data.WifiIsolation.Split(',');
        return p.Length==7&&p[0]=="1"&&p[1]=="0"&&p[2]=="0"&&p[3]=="0"&&
            long.TryParse(p[4],out long held)&&held>0&&p[5]=="1"&&p[6]=="0";
    }
    internal async Task<string> RunAsync(Func<int,CancellationToken,Task<Tab5BleQueueProbe>> run,CancellationToken token,bool compareWindow=false) {
        if(Interlocked.CompareExchange(ref _running,1,0)!=0)throw new InvalidOperationException("蓝牙对照正在进行。");
        int[] limits=compareWindow?[32,64,32]:[7,31,7];string label=compareWindow?"确认窗口":"排队";
        var entries=new List<(int Limit,Tab5BleQueueProbe Data)>();string state="running",last="unavailable";int group=0;
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(120));
        void Publish() {
            _json=JsonSerializer.Serialize(new{schema=2,kind=compareWindow?"writeWindow":"nativeQueue",state,group,activeNativeLimit=NativeLimit,activeWriteWindow=WriteWindow,plannedLimits=limits,
                samples=entries.Select(x=>new {nativeLimit=compareWindow?7:x.Limit,writeWindow=compareWindow?x.Limit:32,result=x.Data.Result,radio=x.Data.Radio,wifiIsolation=x.Data.WifiIsolation,
                    trace=x.Data.Trace,firmwareBytes=x.Data.FirmwareBytes,estimateSeconds=Tab5BleOtaEstimate.Seconds(x.Data.Result,x.Data.FirmwareBytes),complete=Complete(x.Data)})});
            var lines=entries.Select((x,i)=> {
                if(!Complete(x.Data))return $"第 {i+1} 组（{label} {x.Limit}）：未完整通过，已停止对照。";
                double ms=x.Data.Result.Split(';',StringSplitOptions.RemoveEmptyEntries).Skip(1).Sum(r=>double.Parse(r.Split(',')[4],System.Globalization.CultureInfo.InvariantCulture));
                string estimate=Tab5BleOtaEstimate.Seconds(x.Data.Result,x.Data.FirmwareBytes) is {} seconds?$"，整包估算 {seconds:F0} 秒":"";
                return $"第 {i+1} 组（{label} {x.Limit}）：{ms/1000:F2} 秒{estimate}";
            }).ToList();
            lines.Add(state=="running"?$"正在进行第 {group}/3 组，{label}上限 {(compareWindow?WriteWindow:NativeLimit)}。":
                state=="complete"?"对照完成，已恢复标准参数。整包耗时仅为估算，不自动刷机。":
                "对照已结束，已恢复标准参数；保留已完成结果。");
            _summary=string.Join(Environment.NewLine,lines);
        }
        try {
            foreach(int limit in limits) {
                deadline.Token.ThrowIfCancellationRequested();group++;
                Volatile.Write(ref _writeWindow,compareWindow?limit:32);
                if(!compareWindow)Volatile.Write(ref _nativeLimit,limit);Publish();
                var data=await run(limit,deadline.Token);last=data.Result;entries.Add((limit,data));
                if(!Complete(data)){state="incomplete";return last;}
            }
            state="complete";return last;
        }catch(OperationCanceledException){state="cancelled";throw;}
        catch{state="failed";throw;}
        finally {
            Volatile.Write(ref _nativeLimit,7);Volatile.Write(ref _writeWindow,Tab5BleMailboxWindow.DefaultBulkWriteCredits);Publish();Interlocked.Exchange(ref _running,0);
        }
    }
}
