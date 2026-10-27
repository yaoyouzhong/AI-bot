"""Exercise production panic capture with simulated internal SRAM and MMIO.

Run the generated C file with a Windows host compiler. This verifies bounded
capture and RTC/JSON behavior; it does not simulate the ESP32-P4 stack guard.
"""
import argparse, re
from pathlib import Path

p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
root=Path(__file__).resolve().parents[1]
s=(root/'firmware/main/crash_record.c').read_text(encoding='utf-8')
def function(name):
    start=re.search(r'(?:static )?(?:void|bool) (?:IRAM_ATTR )?'+name+r'\(',s).start()
    opening=s.index('{',start);depth=1;end=opening+1
    while depth:
        depth+=(s[end]=='{')-(s[end]=='}');end+=1
    return s[start:end]
prefix=s[s.index('#define TRACE_MAGIC'):s.index('// Per-channel writers')]
functions='\n'.join(function(n) for n in ['crash_record_previous_runtime','boot_trace_update',
    'internal_span','code_address','capture_stack_guard','__wrap_esp_panic_handler','crash_record_init','crash_record_json'])
functions=functions.replace('__asm__ volatile("fence rw,rw" ::: "memory");','atomic_thread_fence(memory_order_seq_cst);')
header=r'''
#include <windows.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdatomic.h>
#include <stdio.h>
#include <string.h>
#define RTC_NOINIT_ATTR
#define IRAM_ATTR
#define SOC_DRAM_LOW 0x4ff00000U
#define SOC_DRAM_HIGH 0x4ffc0000U
#define SOC_IRAM_LOW SOC_DRAM_LOW
#define SOC_IRAM_HIGH SOC_DRAM_HIGH
#define SOC_IROM_LOW 0x40000000U
#define SOC_IROM_HIGH 0x48000000U
#define SOC_IROM_MASK_LOW 0x4fc00000U
#define SOC_IROM_MASK_HIGH 0x4fc20000U
#include "crash_record.h"
typedef unsigned UBaseType_t;
typedef uint32_t StackType_t;
#define configMAX_TASK_NAME_LEN 16
typedef struct {uint32_t saved;void *pxDummy6;uint8_t ucDummy7[16];void *pxDummy8;} StaticTask_t;
typedef struct {uint32_t mepc,ra,sp,gp,tp,t0,t1,t2,s0,s1,a0,a1,a2,a3,a4,a5,a6,a7,
    s2,s3,s4,s5,s6,s7,s8,s9,s10,s11,t3,t4,t5,t6,mstatus,mtvec,mcause,mtval,mhartid;} RvExcFrame;
typedef struct {unsigned core,exception;void *addr,*frame;} panic_info_t;
typedef int esp_reset_reason_t;
enum {ESP_RST_POWERON=1,ESP_RST_SW=3,ESP_RST_PANIC=4,ESP_RST_BROWNOUT=9};
static int reason=ESP_RST_POWERON;
static int esp_reset_reason(void){return reason;}
static unsigned esp_rom_get_reset_reason(int core){(void)core;return 12;}
static bool g_panic_abort;
static const char *g_panic_abort_details;
static unsigned real_calls;
static void __real_esp_panic_handler(panic_info_t *info){(void)info;real_calls++;}
static unsigned guard_data[2][4];
#define ASSIST_DEBUG_CORE_0_INTR_RAW_REG 0
#define ASSIST_DEBUG_CORE_1_INTR_RAW_REG 1
#define REG_READ(core) guard_data[core][0]
static unsigned assist_debug_ll_sp_spill_get_pc(unsigned i){return guard_data[i][1];}
static unsigned assist_debug_ll_sp_spill_get_min(unsigned i){return guard_data[i][2];}
static unsigned assist_debug_ll_sp_spill_get_max(unsigned i){return guard_data[i][3];}
void * volatile pxCurrentTCBs[2];
UBaseType_t port_uxInterruptNesting[2];
StackType_t *xIsrStackBottom[2],*xIsrStackTop[2];
#define REQUIRE(x) do{if(!(x)){fprintf(stderr,"FAIL: %s\n",#x);return 1;}}while(0)
'''
test=r'''
int main(void) {
    void *arena=VirtualAlloc((void *)(uintptr_t)SOC_DRAM_LOW,SOC_DRAM_HIGH-SOC_DRAM_LOW,MEM_RESERVE|MEM_COMMIT,PAGE_READWRITE);
    REQUIRE((uintptr_t)arena==SOC_DRAM_LOW);
    crash_record_init();
    RvExcFrame *frame=(RvExcFrame *)(uintptr_t)0x4ff10000;
    *frame=(RvExcFrame){.mepc=0x4ff002c2,.ra=0x4ff002b4,.sp=0x4ff1ad00,.mcause=27,.s1=5};
    RvExcFrame *interrupted=(RvExcFrame *)(uintptr_t)0x4ff24000;
    *interrupted=(RvExcFrame){.mepc=0x40001000,.ra=0x40002000,.sp=0x4ff24100};
    pxCurrentTCBs[0]=(void *)(uintptr_t)0x4ff22000;
    *(uint32_t *)pxCurrentTCBs[0]=(uint32_t)(uintptr_t)interrupted;
    StaticTask_t *task=(StaticTask_t *)pxCurrentTCBs[0];
    task->pxDummy6=(void *)(uintptr_t)0x4ff24000;task->pxDummy8=(void *)(uintptr_t)0x4ff26000;
    memcpy(task->ucDummy7,"taskLVGL",9);port_uxInterruptNesting[0]=1;
    xIsrStackBottom[0]=(StackType_t *)(uintptr_t)0x4ff1a700;xIsrStackTop[0]=(StackType_t *)(uintptr_t)0x4ff1ad00;
    guard_data[0][0]=512;guard_data[0][1]=0x4ff002b0;
    guard_data[0][2]=0x4ff1a700;guard_data[0][3]=0x4ff1ad00;
    guard_data[1][0]=256;guard_data[1][1]=0x40003000;
    guard_data[1][2]=0x4ff28000;guard_data[1][3]=0x4ff28800;
    panic_info_t info={.core=0,.exception=4,.frame=frame};
    __wrap_esp_panic_handler(&info);
    REQUIRE(real_calls==1&&retained_crash.magic==CRASH_MAGIC);
    REQUIRE(retained_crash.cause==27&&retained_crash.irq_cause==5);
    REQUIRE(retained_crash.guard[0].raw==512&&retained_crash.guard[1].raw==256);
    REQUIRE(retained_crash.guard[0].max==0x4ff1ad00);
    REQUIRE(retained_crash.task_min==0x4ff24000&&retained_crash.task_max==0x4ff26000);
    REQUIRE(!strcmp((const char *)retained_crash.task_name,"taskLVGL")&&retained_crash.irq_depth==1);
    REQUIRE(retained_crash.interrupted_pc==0x40001000&&retained_crash.interrupted_sp==0x4ff24100);
    reason=ESP_RST_PANIC;runtime_active=false;crash_record_init();
    REQUIRE(previous_crash.magic==CRASH_MAGIC&&previous_crash.guard[0].pc==0x4ff002b0);
    char json[3072];crash_record_json(json,sizeof(json));
    REQUIRE(strstr(json,"\"crash\":1")&&strstr(json,"\"stackGuard\":[[512,"));
    // Never dereference an external/unaligned TCB or saved frame.
    pxCurrentTCBs[0]=(void *)(uintptr_t)0x48000000;
    __wrap_esp_panic_handler(&info);REQUIRE(retained_crash.interrupted_pc==0);
    pxCurrentTCBs[0]=(void *)(uintptr_t)0x4ff22001;
    __wrap_esp_panic_handler(&info);REQUIRE(retained_crash.interrupted_pc==0);
    pxCurrentTCBs[0]=(void *)(uintptr_t)0x4ff22000;
    *(uint32_t *)pxCurrentTCBs[0]=SOC_DRAM_HIGH-4;
    __wrap_esp_panic_handler(&info);REQUIRE(retained_crash.interrupted_pc==0);
    info.core=3;__wrap_esp_panic_handler(&info);REQUIRE(retained_crash.interrupted_pc==0);
    info.frame=(void *)(uintptr_t)0x48000000;
    __wrap_esp_panic_handler(&info);REQUIRE(retained_crash.cause==0);
    reason=ESP_RST_POWERON;runtime_active=false;memset(&previous_crash,0,sizeof(previous_crash));
    crash_record_init();REQUIRE(previous_crash.magic==0);
    // Exercise the actual formatter with maximum numeric width, full code
    // candidates, and a fully escaped abort detail. Python validates the JSON.
    memset(&previous_crash,0xff,sizeof(previous_crash));previous_crash.magic=CRASH_MAGIC;
    memset(previous_crash.abort_detail,'"',159);previous_crash.abort_detail[159]=0;
    memset(previous_crash.task_name,'A',16);previous_crash.task_name[16]=0;
    memset(&previous_ota,0xff,sizeof(previous_ota));memset((void *)&retained_ota,0xff,sizeof(retained_ota));
    memset(&previous_boot,0xff,sizeof(previous_boot));memset((void *)&retained_boot,0xff,sizeof(retained_boot));
    memset(&previous_runtime,0xff,sizeof(previous_runtime));previous_runtime.magic=RUNTIME_MAGIC;
    memset(reset_raw,0xff,sizeof(reset_raw));
    crash_record_json(json,sizeof(json));
    REQUIRE(strlen(json)<sizeof(json)-1);
    FILE *out=NULL;REQUIRE(fopen_s(&out,"maximum-trace.json","wb")==0&&out);fwrite(json,1,strlen(json),out);fclose(out);
    printf("CRASH_PANIC_OK bounded MMIO/frame capture; RTC retention; unsafe pointer exclusion; JSON bytes=%zu\n",strlen(json));
    VirtualFree(arena,0,MEM_RELEASE);return 0;
}
'''
a.output.mkdir(parents=True,exist_ok=True)
(a.output/'panic_test.c').write_text(header+prefix+functions+test,encoding='utf-8')
print(a.output/'panic_test.c')
