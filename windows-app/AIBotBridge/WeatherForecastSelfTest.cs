using System.Text.Json;

namespace AIBotBridge;

internal static class WeatherForecastSelfTest
{
    internal static void Run() {
        static void Check(bool value){if(!value)throw new InvalidOperationException("Weather forecast regression");}
        using var q=JsonDocument.Parse("""
            {"hourly":[{"fxTime":"2026-09-28T23:00+08:00","text":"小雨","temp":"0","pop":"0"},{"fxTime":"2026-09-29T00:00+08:00","text":"阴","temp":"bad","pop":"101"},{"fxTime":"bad"}],"daily":[{"fxDate":"2026-09-28","textDay":"阴","tempMin":"-3","tempMax":"9","precip":"0.0","sunrise":"05:43","sunset":"18:16"}]}
            """);
        var hours=WeatherForecast.QHours(q.RootElement);var days=WeatherForecast.QDays(q.RootElement);
        Check(hours.Length==2&&hours[0].Temperature==0&&hours[0].RainProbability==0);
        Check(hours[1].Temperature is null&&hours[1].RainProbability is null);
        Check(days.Length==1&&days[0].Low==-3&&days[0].RainProbability is null&&days[0].PrecipitationMm==0);
        Check(days[0].Sunrise=="05:43"&&days[0].Sunset=="18:16"&&days[0].DaylightSeconds is null);
        using var meteo=JsonDocument.Parse("""
            {"hourly":{"time":["2026-09-28T01:00","2026-09-28T22:00","2026-09-28T23:00","2026-09-29T00:00"],"temperature_2m":[10,null,12,13],"weather_code":[1,3,61,2],"precipitation_probability":[0,null,80,0]},"daily":{"time":["2026-09-28","2026-09-29"],"temperature_2m_min":[9,null],"temperature_2m_max":[20],"weather_code":[1,2]}}
            """);
        hours=WeatherForecast.MeteoHours(meteo.RootElement,new DateTime(2026,9,28,22,30,0),_=>"多云");
        days=WeatherForecast.MeteoDays(meteo.RootElement,_=>"多云");
        Check(hours.Length==3&&hours[0].Temperature is null&&hours[1].RainProbability==80&&hours[2].Time.StartsWith("2026-09-29"));
        Check(days.Length==2&&days[1].Low is null&&days[1].High is null);
        Check(days[0].Sunrise is null&&days[0].Sunset is null&&days[0].DaylightSeconds is null);
        using var solar=JsonDocument.Parse("""
            {"daily":{"time":["2026-10-11","2026-10-12","2026-10-13","2026-10-14"],
            "sunrise":["2026-10-11T06:12","2026-10-11T06:13",null,"invalid"],
            "sunset":["2026-10-11T17:43:00","2026-10-12T25:00",null],"daylight_duration":[41460,-1,0,86400]}}
            """);
        days=WeatherForecast.MeteoDays(solar.RootElement,_=>"--");
        Check(days[0].Sunrise=="06:12"&&days[0].Sunset=="17:43"&&days[0].DaylightSeconds==41460);
        Check(days[1].Sunrise is null&&days[1].Sunset is null&&days[1].DaylightSeconds is null);
        Check(days[2].Sunrise is null&&days[2].DaylightSeconds==0&&days[3].DaylightSeconds==86400);
        using var old=JsonDocument.Parse("""{"date":"2026-10-11","condition":"晴","low":10,"high":20,"rainProbability":0,"precipitationMm":0}""");
        var restored=JsonSerializer.Deserialize<WeatherDay>(old.RootElement,JsonDefaults.Options);
        Check(restored?.Sunrise is null&&restored?.Sunset is null&&restored?.Date=="2026-10-11");
        var now=DateTimeOffset.UtcNow;
        var snapshot=new StatusSnapshot(1,now.ToString("HH:mm:ss"),now.ToUnixTimeSeconds(),28800,now,new("idle",null),new("idle",null)) {
            Weather=new("测试城市","晴",20,25,15,50,0,null,null,"open-meteo",now,false,UtcOffsetSeconds:28800){Daily=days}};
        var bytes=Tab5Protocol.Snapshot(snapshot,"0123456789ab","weather-solar",1);using var frame=JsonDocument.Parse(bytes);
        var weather=frame.RootElement.GetProperty("data").GetProperty("weather");var solarDay=weather.GetProperty("daily")[0];
        Check(solarDay.GetProperty("sunrise").GetString()=="06:12"&&solarDay.GetProperty("sunset").GetString()=="17:43"&&
            weather.GetProperty("utcOffsetSeconds").GetInt32()==28800&&weather.GetProperty("daily")[2].GetProperty("daylightSeconds").GetDouble()==0&&
            weather.GetProperty("daily")[3].GetProperty("daylightSeconds").GetDouble()==86400&&bytes.Length+28<Tab5Protocol.MaximumFrame);
        // The legacy USB heartbeat continues to omit TAB5-only forecast series.
        var legacy=DeviceStatusFrame.Create(snapshot);Check(legacy["data"]?["weather"]?["daily"] is null);
        Console.WriteLine("WEATHER_SOLAR_WIRE_OK shared_TAB5_USB_WiFi_BLE fields timezone polar_values bounded_frame legacy_unchanged");
        Console.WriteLine("WEATHER_SOLAR_PARSE_OK qweather_local meteo_dated_minutes cross_date_invalid_rejected polar_day_night old_cache");
        Console.WriteLine("WEATHER_FORECAST_PARSE_OK local midnight, missing arrays/numbers, zero values, probability vs amount");
    }
}
