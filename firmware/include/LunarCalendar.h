#pragma once
#include "LunarCalendarData.h"

namespace LunarCalendar {
struct Date { int month = 0; int day = 0; bool leap = false; };
inline bool gregorianLeap(int year) { return year % 4 == 0 && (year % 100 != 0 || year % 400 == 0); }
inline Date convert(int year, int month, int day) {
    if (year < 1901 || year > 2101 || month < 1 || month > 12) return {};
    const int lengths[] = {31,28,31,30,31,30,31,31,30,31,30,31};
    if (day < 1 || day > lengths[month-1] + (month == 2 && gregorianLeap(year))) return {};
    int offset = day - 1;
    for (int m = 1; m < month; m++) offset += lengths[m-1] + (m == 2 && gregorianLeap(year));
    if (year == 2101 || offset < int(Years[year-1901] >> 17)) {
        if (year == 1901) return {};
        year--;
        offset += 365 + gregorianLeap(year);
    }
    const uint32_t data = Years[year-1901];
    offset -= data >> 17;
    const int leap = data & 15;
    for (int m = 1; m <= (leap ? 13 : 12); m++) {
        const int days = 29 + ((data >> (m+3)) & 1);
        if (offset < days) return {m - (leap && m >= leap), offset+1, leap && m == leap};
        offset -= days;
    }
    return {};
}
}
