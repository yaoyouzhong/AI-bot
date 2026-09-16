using System.Globalization;

namespace AIBotBridge;

internal static class LunarDate
{
    internal static string Format(DateTime localDate)
    {
        var calendar = new ChineseLunisolarCalendar();
        if (localDate < calendar.MinSupportedDateTime || localDate > calendar.MaxSupportedDateTime) return "";
        int year = calendar.GetYear(localDate), month = calendar.GetMonth(localDate);
        int leap = calendar.GetLeapMonth(year), day = calendar.GetDayOfMonth(localDate);
        bool isLeap = leap != 0 && month == leap;
        if (leap != 0 && month >= leap) month--;
        string[] months = ["正", "二", "三", "四", "五", "六", "七", "八", "九", "十", "冬", "腊"];
        const string digits = "一二三四五六七八九十";
        string date = day switch { 10 => "初十", 20 => "二十", 30 => "三十",
            _ => (day < 10 ? "初" : day < 20 ? "十" : "廿") + digits[(day - 1) % 10] };
        return "农历" + (isLeap ? "闰" : "") + months[month - 1] + "月" + date;
    }
}
