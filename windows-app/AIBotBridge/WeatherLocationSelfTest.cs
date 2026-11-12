using AIBotBridge.MigratedWeather;

namespace AIBotBridge;

internal static class WeatherLocationSelfTest
{
    internal static void Run()
    {
        static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
        var beijing=QWeatherCoordinates.FromWgs84(39.908722,116.397389);
        Check(Math.Abs(beijing.Latitude-39.91012548)<0.0000001&&Math.Abs(beijing.Longitude-116.40363255)<0.0000001,"WGS84 to GCJ02 reference point changed");
        Check(QWeatherCoordinates.FromWgs84(48.85,2.35)==(48.85,2.35),"Overseas coordinates shifted");
        using(var places=System.Text.Json.JsonDocument.Parse("""[{"country":"中国","adm1":"江苏省"},{"country":"中国","adm1":"香港特别行政区"},{"country":"中国","adm1":"澳门特别行政区"},{"country":"中国","adm1":"台湾省"},{"country":"France","adm1":"Paris"}]""")) {
            int index=0;foreach(var place in places.RootElement.EnumerateArray())Check(QWeatherCoordinates.IsMainland(place)==(index++==0),"Provider mainland coordinate boundary incorrect");
        }
        var now=DateTimeOffset.Parse("2026-11-12T08:00:00Z");
        var query=new WeatherRefreshSchedule.Query(true,"fixture","open-meteo","",32,119);
        var schedule=new WeatherRefreshSchedule();
        Check(schedule.ShouldFetch(query,false,false,now),"First weather fetch skipped");
        schedule.Succeeded(query,false,now);
        Check(!schedule.ShouldFetch(query,false,false,now.AddMinutes(1)),"Old location fetched every minute");
        Check(schedule.ShouldFetch(query,true,false,now.AddSeconds(2)),"Permission recovery waited for weather cadence");
        schedule.Succeeded(query,true,now);
        Check(!schedule.ShouldFetch(query,true,false,now.AddMinutes(14)),"Stationary cadence lost");
        Check(schedule.ShouldFetch(query,true,false,now.AddMinutes(15)),"Stationary weather never refreshed");
        schedule.Succeeded(query,true,now);
        Check(!schedule.ShouldFetch(query,false,false,now.AddMinutes(1)),"Temporary location loss discarded weather cadence");
        Check(schedule.ShouldFetch(query,true,false,now.AddMinutes(2)),"Location recovery with unchanged coordinates reused stale weather");
        foreach(var changed in new[]{query with{Latitude=33},query with{Longitude=120},query with{City="other"},
            query with{Automatic=false},query with{Provider="qweather"},query with{Host="example.qweatherapi.com"}})
            Check(schedule.ShouldFetch(changed,true,false,now.AddSeconds(1)),"Updated setting kept old-location weather");
        Check(schedule.ShouldFetch(query,true,true,now),"Explicit refresh ignored");
        schedule.Failed();
        Check(schedule.ShouldFetch(query,true,false,now.AddSeconds(1)),"Failed fetch suppressed retry");

        var settingsPath=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData),"AI-bot","settings.json");
        var before=File.Exists(settingsPath)?File.ReadAllBytes(settingsPath):null;
        int requested=0;
        using(var form=new WeatherSettingsForm(new WeatherMonitor(),()=>{requested++;return Task.FromException<WindowsLocation.Result>(new UnauthorizedAccessException("fixture denied"));})) {
            form.UsePreviewData();
            form.SaveSettings().GetAwaiter().GetResult();
            Check(requested==1,"Saved coordinates bypassed current executable consent");
            Check(form.DialogResult!=DialogResult.OK,"Denied auto-location save reported success");
            Check(form.Controls.Find("weather-result",true).Single().Text.Contains("尚未保存"),"Denied save not explained");
        }
        var after=File.Exists(settingsPath)?File.ReadAllBytes(settingsPath):null;
        Check(before is null?after is null:after is not null&&before.SequenceEqual(after),"Denied consent changed settings");
        using(var form=new WeatherSettingsForm(new WeatherMonitor(),()=>Task.FromResult(new WindowsLocation.Result(32,119,"fixture")))) {
            form.UsePreviewData();form.Locate().GetAwaiter().GetResult();
            Check(form.Controls.Find("weather-result",true).Single().Text.Contains("定位成功"),"Granted lookup did not complete");
        }
        Console.WriteLine("WEATHER_LOCATION_OK coordinate_system mainland_only permission_recovery changed_settings cadence failure_retry denied_save_preserves_settings granted_lookup");
    }
}
