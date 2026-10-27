#pragma once
#include "battery_status.h"

// INA226 measures voltage/current, not state of charge. Start with a voltage
// estimate, then integrate current for the standard 2000 mAh TAB5 pack. In
// particular, charging voltage must never directly raise the displayed level.
typedef struct {
    bool initialized, anchored, charging, saw_charge;
    float percent;
    unsigned discharge_ms, full_ms;
} battery_estimate;

static inline int battery_estimate_sample(battery_estimate *s,int mv,int ma,unsigned elapsed_ms) {
    bool charging=s->charging;
    if(ma>15)charging=true;else if(ma<5)charging=false;
    if(!s->initialized) {
        s->initialized=true;
        s->percent=(float)battery_percent_from_mv(mv);
        // A cold boot on charge has no rested voltage anchor. Show unknown
        // until we can obtain one, rather than merely replacing a false 100 by 99.
        s->anchored=!charging;
        elapsed_ms=0;
    }
    // Never integrate unobserved time after an I2C outage or long scheduling gap.
    if(elapsed_ms>10000) {elapsed_ms=0;s->discharge_ms=0;s->full_ms=0;}
    if(charging!=s->charging)s->discharge_ms=0;
    s->charging=charging;
    if(charging)s->saw_charge=true;
    int current=(ma>-5&&ma<5)?0:ma;
    if(s->anchored)s->percent+=(float)current*elapsed_ms/72000000.0f;
    if(charging) {
        s->discharge_ms=0;
        if(s->percent>99)s->percent=99;
    } else {
        if(s->discharge_ms<60000)s->discharge_ms+=elapsed_ms;
        // After unplugging, allow voltage to settle. Correct drift slowly rather
        // than snapping down with the immediate charging-voltage collapse.
        if(s->discharge_ms>=60000&&ma<=-5) {
            float target=(float)battery_percent_from_mv(mv),step=elapsed_ms/60000.0f;
            if(!s->anchored){s->percent=target;s->anchored=true;}
            if(s->percent>target)s->percent-=s->percent-target<step?s->percent-target:step;
            // No upward voltage correction under load or after its removal.
        }
    }
    // Full is an estimate requiring a sustained high-voltage charge taper,
    // not the instant voltage rise at USB insertion. No charger-register guess.
    if(s->saw_charge&&mv>=8200&&ma>=-5&&ma<=50) {
        if(s->full_ms<600000)s->full_ms+=elapsed_ms;
        if(s->full_ms>=600000){s->percent=100;s->anchored=true;}
    } else s->full_ms=0;
    if(s->percent<0)s->percent=0;
    if(s->percent>100)s->percent=100;
    return s->anchored?(int)s->percent:-1;
}
