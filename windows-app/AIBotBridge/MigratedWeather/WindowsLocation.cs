#nullable disable
using Windows.Devices.Geolocation;

namespace AIBotBridge.MigratedWeather;

static class WindowsLocation
{
    public sealed record Result(double Latitude, double Longitude, string Label,
        double AccuracyMeters=double.NaN,string Source="Unknown",DateTimeOffset Timestamp=default);

    internal static async Task<GeolocationAccessStatus?> RequestFromTray()
    {
        if(ForegroundObserver.CurrentProcessHasFocus())
            return await Geolocator.RequestAccessAsync();

        // The Windows consent UI needs a foreground application even when the
        // bridge normally lives in the tray. Nothing is drawn behind the prompt.
        using var owner=new Form {
            ShowInTaskbar=false, FormBorderStyle=FormBorderStyle.None,
            StartPosition=FormStartPosition.CenterScreen, Opacity=0,
            ClientSize=new System.Drawing.Size(1,1)
        };
        SettingsWindow.Present(owner);
        await Task.Yield();
        if(!ForegroundObserver.CurrentProcessHasFocus())return null;
        return await Geolocator.RequestAccessAsync();
    }

    public static async Task<Result> Locate()
    {
        var access = await Geolocator.RequestAccessAsync();
        if (access != GeolocationAccessStatus.Allowed)
            throw new UnauthorizedAccessException("尚未获得 AI-bot 的位置授权。若 Windows 未显示授权提示，请打开系统的“隐私和安全性 → 位置”，允许桌面应用访问位置。");

        return await LocateSilently();
    }

    public static async Task<Result> LocateSilently()
    {
        var locator = new Geolocator { DesiredAccuracyInMeters = 100 };
        var position = await locator.GetGeopositionAsync(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(12));
        var point = position.Coordinate.Point.Position;
        return new Result(point.Latitude, point.Longitude,
            "Windows 自动定位",position.Coordinate.Accuracy,
            position.Coordinate.PositionSource.ToString(),position.Coordinate.Timestamp);
    }
}
