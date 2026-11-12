using System.Text.Json;

namespace AIBotBridge.MigratedWeather;

// Windows/CoreLocation store WGS-84. QWeather requires GCJ-02 in mainland
// China (https://dev.qweather.com/docs/resource/glossary/#坐标).
// Conversion is confined to the provider boundary; never rewrite OS coordinates.
internal static class QWeatherCoordinates
{
    internal static bool IsMainland(JsonElement location)
    {
        string Text(string key)=>location.TryGetProperty(key,out var value)?value.GetString()??"":"";
        return Text("country") is "中国" or "China" &&
            !new[]{"香港","澳门","台湾","Hong Kong","Macao","Macau","Taiwan"}.Any(p=>Text("adm1").StartsWith(p,StringComparison.OrdinalIgnoreCase));
    }

    internal static (double Latitude,double Longitude) FromWgs84(double latitude,double longitude)
    {
        if(!double.IsFinite(latitude)||!double.IsFinite(longitude)||latitude is <-90 or >90||longitude is <-180 or >180)
            throw new ArgumentOutOfRangeException(nameof(latitude));
        if(longitude is <72.004 or >137.8347 || latitude is <0.8293 or >55.8271)return (latitude,longitude);
        double x=longitude-105,y=latitude-35;
        double Wave(double argument)=>Math.Sin(argument*Math.PI);
        double shared=(20*Wave(6*x)+20*Wave(2*x))*2/3;
        double north=-100+2*x+3*y+0.2*y*y+0.1*x*y+0.2*Math.Sqrt(Math.Abs(x))
            +shared+(20*Wave(y)+40*Wave(y/3))*2/3+(160*Wave(y/12)+320*Wave(y/30))*2/3;
        double east=300+x+2*y+0.1*x*x+0.1*x*y+0.1*Math.Sqrt(Math.Abs(x))
            +shared+(20*Wave(x)+40*Wave(x/3))*2/3+(150*Wave(x/12)+300*Wave(x/30))*2/3;
        const double axis=6378245,eccentricity=0.006693421622965943;
        double radians=latitude*Math.PI/180,scale=1-eccentricity*Math.Pow(Math.Sin(radians),2),root=Math.Sqrt(scale);
        return (latitude+north*180/(axis*(1-eccentricity)/(scale*root)*Math.PI),
            longitude+east*180/(axis/root*Math.Cos(radians)*Math.PI));
    }
}
