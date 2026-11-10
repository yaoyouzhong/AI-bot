#pragma once
#include <stdint.h>
#include <stdio.h>
#include <string.h>

// Parse ISO 8601 provider timestamps without changing the device's global TZ.
// Fractional seconds are ignored; explicit offsets and Z are both supported.
inline int64_t quotaResetEpoch(const char* text) {
  if (!text || strlen(text) < 20) return -1;
  int year, month, day, hour, minute, second;
  if (sscanf(text, "%4d-%2d-%2dT%2d:%2d:%2d", &year, &month, &day,
             &hour, &minute, &second) != 6 || year < 1970 || year > 2200 ||
      month < 1 || month > 12 || day < 1 || hour < 0 || hour > 23 ||
      minute < 0 || minute > 59 || second < 0 || second > 59) return -1;
  const bool leap = year % 4 == 0 && (year % 100 != 0 || year % 400 == 0);
  const int daysInMonth[] = {31, leap ? 29 : 28, 31,30,31,30,31,31,30,31,30,31};
  if (day > daysInMonth[month - 1]) return -1;
  const char* zone = text + 19;
  if (*zone == '.') { ++zone; while (*zone >= '0' && *zone <= '9') ++zone; }
  int offset = 0;
  if (*zone == 'Z' && zone[1] == 0) { }
  else if ((*zone == '+' || *zone == '-') && strlen(zone) == 6 && zone[3] == ':') {
    int zh, zm;
    if (sscanf(zone + 1, "%2d:%2d", &zh, &zm) != 2 || zh < 0 || zh > 14 || zm < 0 || zm > 59 || (zh == 14 && zm != 0)) return -1;
    offset = (zh * 60 + zm) * 60 * (*zone == '-' ? -1 : 1);
  } else return -1;
  int64_t days = 0;
  for (int y = 1970; y < year; ++y)
    days += (y % 4 == 0 && (y % 100 != 0 || y % 400 == 0)) ? 366 : 365;
  for (int m = 1; m < month; ++m) days += daysInMonth[m - 1];
  return (days + day - 1) * 86400 + hour * 3600 + minute * 60 + second - offset;
}
