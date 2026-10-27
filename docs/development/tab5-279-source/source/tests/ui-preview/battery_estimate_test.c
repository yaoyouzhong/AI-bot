#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include "../../firmware/main/battery_estimate.h"
int main(void) {
    battery_estimate s={0};
    assert(battery_estimate_sample(&s,7400,-300,0)==50);
    // USB immediately raises terminal voltage above the old 100% threshold.
    for(int i=0;i<30;i++)battery_estimate_sample(&s,8350,1000,2000);
    assert(s.percent>50&&s.percent<51&&!s.full_ms&&s.charging);
    // An hour at 1 A adds 50 percentage points to the nominal 2 Ah pack.
    for(int i=0;i<1800;i++)battery_estimate_sample(&s,8350,1000,2000);
    assert(s.percent==99);
    for(int i=0;i<299;i++)assert(battery_estimate_sample(&s,8300,30,2000)==99);
    assert(battery_estimate_sample(&s,8300,30,2000)==100);
    assert(battery_estimate_sample(&s,7600,-300,2000)>=99);
    assert(!s.charging&&s.full_ms==0);
    float previous=s.percent;
    battery_estimate_sample(&s,7600,-300,3600000);
    assert(s.percent==previous); // no fabricated charge across missing samples
    s=(battery_estimate){0};
    assert(battery_estimate_sample(&s,8400,1000,0)==-1&&s.charging);
    for(int i=0;i<29;i++)assert(battery_estimate_sample(&s,7400,-300,2000)==-1);
    assert(battery_estimate_sample(&s,7400,-300,2000)==50);
    s=(battery_estimate){0};
    assert(battery_estimate_sample(&s,6600,-300,0)==0);
    assert(battery_estimate_sample(&s,6500,-300,2000)==0);
    puts("BATTERY_ESTIMATE_OK plug_unplug_charge_taper_missing_samples_bounds");
}
