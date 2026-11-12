using System.Text.Json;
namespace AIBotBridge;

internal sealed class Tab5UIAutomation
{
    internal sealed record Result(string Request,int Status,int Width,int Height,int Rotation,bool WifiTestActive=false);
    private readonly object _sync=new();
    private Dictionary<string,object>? _pending;
    private Result? _result;
    private string? _pendingOwner;
    internal bool Pending {get{lock(_sync)return _pending is not null;}}
    private string? _wifiOwner;
    private long _wifiSeenAt;
    internal bool WifiAvailable(string owner,long? clock=null)
    {
        lock(_sync) {
            long age=(clock??Environment.TickCount64)-_wifiSeenAt;
            return _wifiOwner==owner&&age is >=0 and <8000;
        }
    }
    internal string Begin(JsonElement action,long? clock=null,string transport="USB",string? owner=null)
    {
        string op=action.TryGetProperty("op",out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()!:"";
        if(transport is not ("USB" or "Wi-Fi")||(op switch {
            "probe" or "tap" or "swipe"=>false,
            "wifi-test"=>transport!="USB",
            "restore"=>transport!="Wi-Fi",
            _=>true
        }))throw new ArgumentException("Invalid TAB5 UI action or transport");
        string id=Guid.NewGuid().ToString("N");
        var p=new Dictionary<string,object>{{"version",1},{"requestId",id},{"transport",transport},{"op",op},{"expiresAt",(clock??DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())+5000}};
        if(op is "tap" or "swipe"){
            int Number(string key,int low,int high){if(!action.TryGetProperty(key,out var n)||n.ValueKind!=JsonValueKind.Number||!n.TryGetInt32(out int value)||value<low||value>high)throw new ArgumentException("Invalid TAB5 coordinate");return value;}
            int w=Number("width",1,1280),h=Number("height",1,1280),x=Number("x",0,w-1),y=Number("y",0,h-1);
            p["width"]=w;p["height"]=h;p["rotation"]=Number("rotation",0,3);p["x"]=x;p["y"]=y;
            p["toX"]=op=="tap"?x:Number("toX",0,w-1);p["toY"]=op=="tap"?y:Number("toY",0,h-1);p["durationMs"]=op=="tap"?80:Number("durationMs",100,1200);
        }
        lock(_sync){if(_pending is not null)throw new InvalidOperationException("TAB5 UI action pending");_pending=p;_pendingOwner=owner;_result=null;return id;}
    }
    internal object? CommandForFrame(long frameEpoch,long? clock=null,string? owner=null)
    {
        lock(_sync){if(_pending is null||(_pendingOwner is not null&&_pendingOwner!=owner)||frameEpoch<=0||frameEpoch>long.MaxValue-5000||(clock??DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())>=(long)_pending["expiresAt"])return null;
            return new Dictionary<string,object>(_pending){["expiresAt"]=Math.Min((long)_pending["expiresAt"],frameEpoch+5000)};}
    }
    internal void Observe(JsonElement root,string? owner=null)
    {
        if(!root.TryGetProperty("uiAutomation",out var v)||v.ValueKind!=JsonValueKind.String)return;
        lock(_sync)ObserveLocked(v.GetString()!.Split(','),"USB",false,owner);
    }
    internal void ObserveWifi(string owner,string report,long? clock=null)
    {
        var fields=report.Split(',');
        if(fields.Length!=7||fields[6] is not ("0" or "1"))return;
        lock(_sync) {
            bool validIdle=fields[0]=="1"&&fields[1].Length==0&&fields[2]=="0"&&
                fields[3]=="0"&&fields[4]=="0"&&fields[5]=="0";
            if(!validIdle&&!ObserveLocked(fields,"Wi-Fi",fields[6]=="1",owner))return;
            _wifiOwner=owner;
            _wifiSeenAt=clock??Environment.TickCount64;
        }
    }
    private bool ObserveLocked(string[] fields,string transport,bool wifiTest,string? owner)
    {
        if(fields.Length!=(transport=="USB"?6:7)||fields[0]!="1"||
            !int.TryParse(fields[2],out int status)||status is <1 or >4||
            !int.TryParse(fields[3],out int width)||width is <1 or >1280||
            !int.TryParse(fields[4],out int height)||height is <1 or >1280||
            !int.TryParse(fields[5],out int rotation)||rotation is <0 or >3)return false;
        if(!Tab5Protocol.ValidNonce(fields[1]))return false;
        if((_pendingOwner is null||_pendingOwner==owner)&&
            _pending?.GetValueOrDefault("requestId") as string==fields[1]&&
            _pending.GetValueOrDefault("transport") as string==transport) {
            _result=new(fields[1],status,width,height,rotation,wifiTest);
            if(status!=1)_pending=null;
        }
        return true;
    }
    internal Result? Snapshot(){lock(_sync)return _result;}
    internal void Cancel(){lock(_sync){_pending=null;_pendingOwner=null;_result=null;}}
}
