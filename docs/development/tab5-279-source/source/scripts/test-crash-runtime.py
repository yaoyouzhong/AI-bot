"""Compile the production RTC lifecycle functions with simulated reset/timer I/O.

Compile and run the generated C file with a host C11 compiler.
Only the RISC-V fence is substituted by a C atomic fence for the host compiler.
"""
import argparse, re
from pathlib import Path

p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
root=Path(__file__).resolve().parents[1]
s=(root/'firmware/main/crash_record.c').read_text(encoding='utf-8')
def function(name):
    start=re.search(r'void (?:IRAM_ATTR )?'+name+r'\(',s).start()
    opening=s.index('{',start);depth=1;end=opening+1
    while depth:
        depth += (s[end]=='{')-(s[end]=='}');end+=1
    return s[start:end]
prefix=s[s.index('#define TRACE_MAGIC'):s.index('// Per-channel writers')]
functions='\n'.join(function(n) for n in ['crash_record_checkpoint','crash_record_previous_runtime','boot_trace_update','crash_record_init'])
functions=functions.replace('__asm__ volatile("fence rw,rw" ::: "memory");','atomic_thread_fence(memory_order_seq_cst);')
header=r'''
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdatomic.h>
#include <stdio.h>
#include <string.h>
#undef assert
#define assert(x) do {if(!(x)){fprintf(stderr,"FAILED: %s\n",#x);return 1;}} while(0)
#define RTC_NOINIT_ATTR
#define IRAM_ATTR
#include "crash_record.h"
typedef int esp_reset_reason_t;
enum {ESP_RST_POWERON=1,ESP_RST_SW=3,ESP_RST_WDT=7,ESP_RST_BROWNOUT=9};
static int reason=ESP_RST_POWERON;
static uint64_t timer_us;
static int esp_reset_reason(void){return reason;}
static unsigned esp_rom_get_reset_reason(int core){(void)core;return 12;}
static uint64_t esp_timer_get_time(void){return timer_us;}
'''
test=r'''
int main(void) {
    char value[256];
    crash_record_init();
    assert(runtime_active);
    timer_us=520000000;
    crash_record_checkpoint(RUNTIME_FLASH,1,0);
    crash_record_checkpoint(RUNTIME_UI,1,5);
    // Simulate BSS reset while RTC survives. Early IDF Flash operations must
    // not replace evidence from the preceding run before app_main copies it.
    runtime_active=false;memset(&previous_runtime,0,sizeof(previous_runtime));
    reason=ESP_RST_WDT;timer_us=13549;
    crash_record_checkpoint(RUNTIME_FLASH,0,0);
    crash_record_init();
    assert(previous_runtime.magic==RUNTIME_MAGIC);
    assert(previous_runtime.points[RUNTIME_FLASH].stage==1);
    assert(previous_runtime.points[RUNTIME_FLASH].at_us==520000000);
    assert(previous_runtime.points[RUNTIME_UI].detail==5);
    timer_us=20000;crash_record_checkpoint(RUNTIME_FLASH,0,0);
    assert(retained_runtime.points[RUNTIME_FLASH].at_us==20000);
    assert(previous_runtime.points[RUNTIME_FLASH].stage==1);
    crash_record_previous_runtime(value,sizeof(value));
    assert(strstr(value,"1,520000000,0"));
    // Cold/brownout boots must not claim stale RTC evidence is valid.
    for(int i=0;i<2;i++) {
        runtime_active=false;memset(&previous_runtime,0,sizeof(previous_runtime));
        reason=i?ESP_RST_BROWNOUT:ESP_RST_POWERON;crash_record_init();
        assert(previous_runtime.magic==0);
    }
    puts("CRASH_RUNTIME_OK early Flash preserves prior watchdog evidence; current writes and cold-boot exclusion pass");
}
'''
a.output.mkdir(parents=True,exist_ok=True)
(a.output/'runtime_test.c').write_text(header+prefix+functions+test,encoding='utf-8')
print(a.output/'runtime_test.c')
