#pragma once
#include <stdbool.h>
#include <stdint.h>

typedef struct { bool valid,charging; int percent,millivolts,milliamps; } battery_status;
void battery_status_start(void);
battery_status battery_status_get(void);
// TAB5 has voltage/current monitoring, not a coulomb-counting fuel gauge.
// Same 2S voltage estimate as M5Unified Power_Class::getBatteryLevel.
static inline int battery_percent_from_mv(int mv) {
    int percent=(mv-6600)/16;
    return percent<0?0:percent>100?100:percent;
}
