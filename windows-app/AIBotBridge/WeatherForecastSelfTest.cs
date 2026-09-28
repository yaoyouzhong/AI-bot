using System.Text.Json;

namespace AIBotBridge;

internal static class WeatherForecastSelfTest
{
    internal static void Run() {
        static void Check(bool value){if(!value)throw new InvalidOperationException("Weather forecast regression");}
        using var q=JsonDocument.Parse("""
            {"hourly":[{"fxTime":"2026-09-28T23:00+08:00","text":"小雨","temp":"0","pop":"0"},{"fxTime":"2026-09-29T00:00+08:00","text":"阴","temp":"bad","pop":"101"},{"fxTime":"bad"}],"daily":[{"fxDate":"2026-09-28","textDay":"阴","tempMin":"-3","tempMax":"9","precip":"0.0"}]}
            """);
        var hours=WeatherForecast.QHours(q.RootElement);var days=WeatherForecast.QDays(q.RootElement);
        Check(hours.Length==2&&hours[0].Temperature==0&&hours[0].RainProbability==0);
        Check(hours[1].Temperature is null&&hours[1].RainProbability is null);
        Check(days.Length==1&&days[0].Low==-3&&days[0].RainProbability is null&&days[0].PrecipitationMm==0);
        using var meteo=JsonDocument.Parse("""
            {"hourly":{"time":["2026-09-28T01:00","2026-09-28T22:00","2026-09-28T23:00","2026-09-29T00:00"],"temperature_2m":[10,null,12,13],"weather_code":[1,3,61,2],"precipitation_probability":[0,null,80,0]},"daily":{"time":["2026-09-28","2026-09-29"],"temperature_2m_min":[9,null],"temperature_2m_max":[20],"weather_code":[1,2]}}
            """);
        hours=WeatherForecast.MeteoHours(meteo.RootElement,new DateTime(2026,9,28,22,30,0),_=>"多云");
        days=WeatherForecast.MeteoDays(meteo.RootElement,_=>"多云");
        Check(hours.Length==3&&hours[0].Temperature is null&&hours[1].RainProbability==80&&hours[2].Time.StartsWith("2026-09-29"));
        Check(days.Length==2&&days[1].Low is null&&days[1].High is null);
        Console.WriteLine("WEATHER_FORECAST_PARSE_OK local midnight, missing arrays/numbers, zero values, probability vs amount");
    }
}
