#pragma once
#include <stdbool.h>
#include <stdint.h>
#include <time.h>

// Local civil time, calibrated only from an authenticated bridge snapshot.
// I2C and RTC writes run outside the LVGL thread; no image/network dependency.
void gallery_clock_start(void);
void gallery_clock_sync(int64_t local_ms,int64_t now_ms);
bool gallery_clock_local(int64_t now_ms,struct tm *out);
