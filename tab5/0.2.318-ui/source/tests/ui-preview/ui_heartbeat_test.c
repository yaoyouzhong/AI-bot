#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdatomic.h>
#include <stdio.h>
static int64_t esp_timer_get_time(void);
#include "ui_heartbeat_under_test.inc"
static uint64_t time_us;
static bool interleave;
static int64_t esp_timer_get_time(void) {
    uint64_t captured=time_us;
    if(interleave){atomic_store(&ui_heartbeat_ms,1001);interleave=false;}
    return (int64_t)captured;
}
int main(void) {
    time_us=1234000;atomic_store(&ui_heartbeat_ms,1200);assert(tab5_ui_age_ms()==34);
    time_us=1000000;atomic_store(&ui_heartbeat_ms,1000);interleave=true;
    unsigned age=tab5_ui_age_ms();
    printf("UI_AGE_INTERLEAVE age=%u heartbeat=%u\n",age,atomic_load(&ui_heartbeat_ms));
    if(age>1){puts("UI_AGE_INTERLEAVE_FAILED: a new UI heartbeat between timer capture and heartbeat load wraps age");return 1;}
    time_us=5000000;atomic_store(&ui_heartbeat_ms,2000);assert(tab5_ui_age_ms()==3000);
    time_us=1000000;atomic_store(&ui_heartbeat_ms,1001);assert(tab5_ui_age_ms()==UINT32_MAX);
    time_us=UINT64_C(0x100000005)*1000;atomic_store(&ui_heartbeat_ms,0xfffffff0u);assert(tab5_ui_age_ms()==21);
    time_us=0;atomic_store(&ui_heartbeat_ms,0);assert(tab5_ui_age_ms()==0);
    puts("UI_AGE_SNAPSHOT_OK actual function; concurrent new heartbeat does not wrap; genuine stale heartbeat remains stale; uint32 clock wrap preserved");
    return 0;
}
