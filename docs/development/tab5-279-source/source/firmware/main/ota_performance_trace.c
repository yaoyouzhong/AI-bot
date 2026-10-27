#include "crash_record.h"
#include "esp_attr.h"
#include "freertos/FreeRTOS.h"
#include <stdatomic.h>
#include <string.h>

// Keep this object last in tab5_sources. Its new RTC section must not displace
// records owned by older objects. Source ordering still needs final ELF proof.
#define PERFORMANCE_MAGIC 0x4f504631U
// RTC must keep 4-byte alignment: an 8-byte member raises the entire noinit
// section alignment and can shift every older retained record before this one.
enum { PERFORMANCE_WORD_COUNT=(sizeof(ota_performance_t)+3)/4 };
typedef struct {uint32_t magic,words[PERFORMANCE_WORD_COUNT];} performance_trace;
_Static_assert(sizeof(uint32_t)==4,"RTC words must be 32-bit");
_Static_assert(_Alignof(performance_trace)==4,"preserve RTC section alignment");
_Static_assert(offsetof(performance_trace,words)==4,"RTC payload follows magic");
_Static_assert(sizeof(((performance_trace *)0)->words)>=sizeof(ota_performance_t),
               "RTC payload must fit the complete performance record");
RTC_NOINIT_ATTR static volatile performance_trace retained_performance;
static performance_trace previous_performance;
static bool performance_started;
static portMUX_TYPE performance_lock=portMUX_INITIALIZER_UNLOCKED;

void ota_performance_trace_init(bool retained_valid) {
    portENTER_CRITICAL(&performance_lock);
    previous_performance.magic=0;
    performance_started=false;
    if(retained_valid&&retained_performance.magic==PERFORMANCE_MAGIC)
        previous_performance=retained_performance;
    retained_performance.magic=0;
    portEXIT_CRITICAL(&performance_lock);
}
void ota_performance_trace_begin(void) {
    portENTER_CRITICAL(&performance_lock);
    performance_started=true;
    retained_performance.magic=0;
    portEXIT_CRITICAL(&performance_lock);
}
void ota_trace_performance(const ota_performance_t *performance) {
    if(!performance)return;
    portENTER_CRITICAL(&performance_lock);
    retained_performance.magic=0;
    memcpy((void *)retained_performance.words,performance,sizeof(*performance));
    atomic_thread_fence(memory_order_release);
    retained_performance.magic=PERFORMANCE_MAGIC;
    portEXIT_CRITICAL(&performance_lock);
}
void ota_trace_performance_format(char *out,size_t size) {
    if(!out||!size)return;
    out[0]=0;
    if(size<=6)return;
    performance_trace snapshot;
    portENTER_CRITICAL(&performance_lock);
    bool current=performance_started;
    snapshot=current?retained_performance:previous_performance;
    portEXIT_CRITICAL(&performance_lock);
    if(snapshot.magic!=PERFORMANCE_MAGIC)return;
    ota_performance_t value;
    memcpy(&value,snapshot.words,sizeof(value));
    if(!ota_performance_format(&value,out+6,size-6))return;
    memcpy(out,current?";op,0,":";op,1,",6);
}
