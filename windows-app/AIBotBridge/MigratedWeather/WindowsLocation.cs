#nullable disable
using Windows.Devices.Geolocation;

namespace AIBotBridge.MigratedWeather;

static class WindowsLocation
{
    public sealed record Result(double Latitude, double Longitude, string Label,
        double AccuracyMeters=double.NaN,string Source="Unknown",DateTimeOffset Timestamp=default);

    public static async Task<Result> Locate()
    {
        var access = await Geolocator.RequestAccessAsync();
        if (access != GeolocationAccessStatus.Allowed)
            throw new UnauthorizedAccessException("尚未获得 AI-bot 的位置授权，请允许 Windows 提示；若已拒绝，请在系统“位置”设置中允许 AI-bot 访问位置。");

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
