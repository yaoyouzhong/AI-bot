using System.Globalization;

namespace AIBotBridge;

internal static class Tab5Calendar
{
    // Gregorian dates (UTC+8), Hong Kong Observatory public calendar tables:
    // https://www.hko.gov.hk/tc/gts/time/calendar/text/files/T2026c.txt (also 2027/2028).
    private static readonly string[] Terms = ["小寒","大寒","立春","雨水","惊蛰","春分","清明","谷雨","立夏","小满","芒种","夏至","小暑","大暑","立秋","处暑","白露","秋分","寒露","霜降","立冬","小雪","大雪","冬至"];
    private static readonly Dictionary<int,int[]> TermDates = new()
    {
        [2026]=[105,120,204,218,305,320,405,420,505,521,605,621,707,723,807,823,907,923,1008,1023,1107,1122,1207,1222],
        [2027]=[105,120,204,219,306,321,405,420,506,521,606,621,707,723,808,823,908,923,1008,1023,1107,1122,1207,1222],
        [2028]=[106,120,204,219,305,320,404,419,505,520,605,621,706,722,807,822,907,922,1008,1023,1107,1122,1206,1221]
    };
    // State Council General Office notice, 2025-11-04, 国办发明电〔2025〕7号.
    // https://www.beijing.gov.cn/fuwu/bmfw/sy/jrts/202511/t20251104_4258838.html
    private static readonly (int Start,int End,string Name)[] Holidays2026 =
        [(101,103,"元旦"),(215,223,"春节"),(404,406,"清明节"),(501,505,"劳动节"),(619,621,"端午节"),(925,927,"中秋节"),(1001,1007,"国庆节")];
    private static readonly Dictionary<int,string> Workdays2026 = new()
        {[104]="元旦",[214]="春节",[228]="春节",[509]="劳动节",[920]="国庆",[1010]="国庆"};
    private static Tab5HolidayYear? Year(int year)=>Tab5HolidayStore.ApplyNextYear(Tab5HolidayStore.Get(year)??(year==2026?
        new(2026,Holidays2026.Select(h=>new Tab5HolidayRange(h.Start,h.End,h.Name)).ToArray(),Workdays2026.Keys.Order().ToArray(),[]):null));
    private static int[]? TermYear(int year)=>Tab5SolarTerms.Get(year)??TermDates.GetValueOrDefault(year);

    internal static string SolarTerm(DateTime date)
    {
        var dates=TermYear(date.Year);if(dates is null)return "节气待更新";
        var md=date.Month*100+date.Day;
        for(int i=0;i<dates.Length;i++)
        {
            if(dates[i]==md) return "今日"+Terms[i];
            if(dates[i]>md) return $"下个节气 {Terms[i]} {dates[i]/100:00}-{dates[i]%100:00}";
        }
        return TermYear(date.Year+1) is {} next?$"下个节气 小寒 01-{next[0]%100:00}":"节气待更新";
    }
    internal static string Holiday(DateTime date)
    {
        var schedule=Year(date.Year);
        if(schedule is null) return "节假日安排待更新";
        int md=date.Month*100+date.Day;
        if(schedule.Workdays.Contains(md)) return "调休 · 补班";
        foreach(var holiday in schedule.Holidays)
            if(md>=holiday.Start&&md<=holiday.End) return holiday.Name+"假期 · 休息";
        var label=date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday?"周末":"工作日";
        foreach(var holiday in schedule.Holidays)
        {
            var start=new DateTime(date.Year,holiday.Start/100,holiday.Start%100);
            var days=(start-date.Date).Days;
            if(days>0) return $"{label} · 距{holiday.Name}假期 {days} 天";
        }
        return label;
    }
    internal static object Snapshot(DateTime local)
    {
        Tab5HolidayStore.Schedule(local.Year);
        Tab5SolarTerms.Schedule(local.Year);
        var schedule=Year(local.Year);
        var next=local.AddDays(1);
        return new {date=local.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),weekday="星期"+"日一二三四五六"[(int)local.DayOfWeek],lunar=LunarDate.Format(local),
            solarTermLabel=SolarTerm(local),holidayLabel=Holiday(local),
            nextDate=next.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),nextLunar=LunarDate.Format(next),nextSolarTermLabel=SolarTerm(next),nextHolidayLabel=Holiday(next),
            detail=new {terms=Enumerable.Range(local.Year-1,5).Select(y=>new {year=y,dates=TermYear(y)}).Where(y=>y.dates is not null).ToArray(),holidayYear=schedule?.Year??0,
                holidays=schedule?.Holidays??[],workdays=schedule?.Workdays??[],
                holidayYears=Enumerable.Range(local.Year-1,3).Select(Year).OfType<Tab5HolidayYear>().Select(y=>new {y.Year,y.Holidays,y.Workdays}).ToArray(),
                holidayUpdate=Tab5HolidayStore.Status,
                birthdays=BirthdayStore.Snapshot(local)}};
    }
    internal static void SelfTest()
    {
        BirthdayStore.SelfTest();
        static DateTime D(string value)=>DateTime.ParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture);
        if(SolarTerm(D("2026-09-23"))!="今日秋分" || !SolarTerm(D("2026-09-24")).Contains("寒露 10-08") ||
           SolarTerm(D("2028-09-22"))!="今日秋分" || !SolarTerm(D("2026-12-31")).Contains("小寒 01-05") ||
           !Holiday(D("2026-09-20")).Contains("补班") || !Holiday(D("2026-09-25")).Contains("中秋节假期") ||
           !Holiday(D("2026-09-23")).Contains("2 天") || !Holiday(D("2027-01-01")).Contains("待更新"))
            throw new InvalidOperationException("TAB5 calendar boundary test failed");
        foreach(var year in TermDates) {
            if(year.Value.Length!=24 || !year.Value.SequenceEqual(year.Value.Order())) throw new InvalidOperationException("Solar term table invalid");
            foreach(var date in year.Value) _=new DateTime(year.Key,date/100,date%100);
        }
    }
}
