using System.Text.Json;
namespace AIBotBridge;

internal sealed class Tab5UIAutomation
{
    internal sealed record Result(string Request,int Status,int Width,int Height,int Rotation);
    private readonly object _sync=new();
    private Dictionary<string,object>? _pending;
    private Result? _result;
    internal string Begin(JsonElement action,long? clock=null)
    {
        string op=action.TryGetProperty("op",out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()!:"";
        if(op is not ("probe" or "tap" or "swipe"))throw new ArgumentException("Invalid TAB5 UI action");
        string id=Guid.NewGuid().ToString("N");
        var p=new Dictionary<string,object>{{"version",1},{"requestId",id},{"transport","USB"},{"op",op},{"expiresAt",(clock??DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())+5000}};
        if(op!="probe"){
            int Number(string key,int low,int high){if(!action.TryGetProperty(key,out var n)||n.ValueKind!=JsonValueKind.Number||!n.TryGetInt32(out int value)||value<low||value>high)throw new ArgumentException("Invalid TAB5 coordinate");return value;}
            int w=Number("width",1,1280),h=Number("height",1,1280),x=Number("x",0,w-1),y=Number("y",0,h-1);
            p["width"]=w;p["height"]=h;p["rotation"]=Number("rotation",0,3);p["x"]=x;p["y"]=y;
            p["toX"]=op=="tap"?x:Number("toX",0,w-1);p["toY"]=op=="tap"?y:Number("toY",0,h-1);p["durationMs"]=op=="tap"?80:Number("durationMs",100,1200);
        }
        lock(_sync){if(_pending is not null)throw new InvalidOperationException("TAB5 UI action pending");_pending=p;_result=null;return id;}
    }
    internal object? CommandForFrame(long frameEpoch,long? clock=null)
    {
        lock(_sync){if(_pending is null||frameEpoch<=0||frameEpoch>long.MaxValue-5000||(clock??DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())>=(long)_pending["expiresAt"])return null;
            return new Dictionary<string,object>(_pending){["expiresAt"]=Math.Min((long)_pending["expiresAt"],frameEpoch+5000)};}
    }
    internal void Observe(JsonElement root)
    {
        if(!root.TryGetProperty("uiAutomation",out var v)||v.ValueKind!=JsonValueKind.String)return;
        var p=v.GetString()!.Split(',');
        if(p.Length!=6||p[0]!="1"||!int.TryParse(p[2],out int status)||status is <1 or >4||!int.TryParse(p[3],out int w)||w is <1 or >1280||!int.TryParse(p[4],out int h)||h is <1 or >1280||!int.TryParse(p[5],out int r)||r is <0 or >3)return;
        lock(_sync){if(_pending?.GetValueOrDefault("requestId") as string!=p[1])return;_result=new(p[1],status,w,h,r);if(status!=1)_pending=null;}
    }
    internal Result? Snapshot(){lock(_sync)return _result;}
    internal void Cancel(){lock(_sync){_pending=null;_result=null;}}
}
