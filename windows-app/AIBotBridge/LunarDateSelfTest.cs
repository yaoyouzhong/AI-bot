using System.Globalization;
namespace AIBotBridge;
internal static class LunarDateSelfTest
{
    internal static void Run()
    {
        foreach (var (date, expected) in new[] {
            ("2026-02-17", "农历正月初一"), ("2026-09-16", "农历八月初六"),
            ("2025-07-25", "农历闰六月初一"), ("2024-02-09", "农历腊月三十"),
            ("2024-02-10", "农历正月初一"), ("1901-02-18", ""), ("2101-01-29", "") })
            if (LunarDate.Format(DateTime.ParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture)) != expected)
                throw new InvalidOperationException("Lunar date mismatch: " + date);
        var utc = new DateTimeOffset(2026,2,16,16,0,0,TimeSpan.Zero);
        if (LunarDate.Format(utc.AddHours(8).Date) != "农历正月初一" ||
            LunarDate.Format(utc.AddHours(8).AddSeconds(-1).Date) == "农历正月初一")
            throw new InvalidOperationException("Lunar midnight did not follow the display offset.");
        Console.WriteLine("LUNAR_DATE_SELF_TEST_OK new year, leap month, timezone midnight, unsupported dates");
    }
}
