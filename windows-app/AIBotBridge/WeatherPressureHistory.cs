namespace AIBotBridge;

internal sealed class WeatherPressureHistory
{
    private const long WindowSeconds=3*60*60, AllowanceSeconds=15*60;
    private readonly SortedDictionary<long,double> _samples=new();
    private string _identity="";

    internal double? Observe(string identity,long observed,double? pressure,long now)
    {
        if(identity!=_identity){_identity=identity;_samples.Clear();}
        if(identity.Length==0||pressure is not (>0 and <=1200)||observed<=0||observed>now+60||now-observed>1800)
            return null;
        if(_samples.Count>0&&observed<_samples.Last().Key)return null;
        long target=observed-WindowSeconds;
        foreach(var expired in _samples.Keys.TakeWhile(time=>time<target-AllowanceSeconds).ToArray())_samples.Remove(expired);
        _samples[observed]=pressure.Value;
        while(_samples.Count>64)_samples.Remove(_samples.First().Key);
        long distance=AllowanceSeconds+1;
        double? baseline=null;
        foreach(var sample in _samples) {
            long gap=Math.Abs(sample.Key-target);
            if(gap>=distance)continue;
            distance=gap;baseline=sample.Value;
        }
        return baseline is double prior?pressure.Value-prior:null;
    }
}
