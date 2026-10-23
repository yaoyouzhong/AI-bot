#include "crash_record.h"
#include "esp_attr.h"
#include "esp_system.h"
#include "esp_timer.h"
#include "esp_rom_sys.h"
#include "esp_private/panic_internal.h"
#include "riscv/rvruntime-frames.h"
#include "soc/soc.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "hal/assist_debug_ll.h"
#include <stdio.h>
#include <string.h>

#define TRACE_MAGIC 0x4f544134U
#define PHASE_MAGIC 0x4f545031U
#define CRASH_MAGIC 0x43525334U
typedef struct {unsigned raw,pc,min,max;} stack_guard_trace;
typedef struct {unsigned magic,stage,offset,total,stack_free;int error;unsigned elapsed_ms,read_ms,write_ms;} ota_trace;
typedef struct {
    unsigned magic,core,exception,pc,ra,sp,cause,value,aborted,count;
    unsigned code[24];
    char abort_detail[160];
    stack_guard_trace guard[2];
    unsigned irq_cause,interrupted_pc,interrupted_ra,interrupted_sp;
    unsigned task_min,task_max,irq_depth,irq_min,irq_max;
    char task_name[17];
} crash_trace;
// Keep the original retained records at their RTC addresses across this OTA.
// GCC lays these RTC objects out in reverse declaration order; new data goes last.
typedef struct {unsigned magic;ota_phase_timing timing;} phase_trace;
RTC_NOINIT_ATTR static volatile phase_trace retained_phases;
RTC_NOINIT_ATTR static volatile ota_trace retained_ota;
RTC_NOINIT_ATTR static volatile crash_trace retained_crash;
static ota_trace previous_ota;
static crash_trace previous_crash;
typedef struct {unsigned magic,stage,healthy;int error;} boot_trace;
RTC_NOINIT_ATTR static volatile boot_trace retained_boot;
static boot_trace previous_boot;
#define BOOT_MAGIC 0x424f5431U
#define RUNTIME_MAGIC 0x52554e31U
typedef struct {unsigned stage,at_us,detail;} runtime_point;
typedef struct {unsigned magic;runtime_point points[RUNTIME_CHANNELS];} runtime_trace;
RTC_NOINIT_ATTR static volatile runtime_trace retained_runtime;
static runtime_trace previous_runtime;
static phase_trace previous_phases;
static unsigned reset_raw[2];
static volatile bool runtime_active;
// Per-channel writers use bounded numeric stores only, including cache-off
// Flash guards. A hardware watchdog need not reach the panic handler.
void IRAM_ATTR crash_record_checkpoint(unsigned channel,unsigned stage,unsigned detail) {
    // IDF may touch Flash before app_main. Preserve the old RTC record until
    // crash_record_init has copied it; BSS starts false after every reset.
    if(!runtime_active||channel>=RUNTIME_CHANNELS)return;
    retained_runtime.points[channel].at_us=(unsigned)esp_timer_get_time();
    retained_runtime.points[channel].detail=detail;
    __asm__ volatile("fence rw,rw" ::: "memory");
    retained_runtime.points[channel].stage=stage;
}
void crash_record_previous_runtime(char *out,size_t size) {
    int n=snprintf(out,size,"1,%u,%u,%u",previous_runtime.magic==RUNTIME_MAGIC,reset_raw[0],reset_raw[1]);
    if(n<0||(size_t)n>=size)return;
    size_t used=(size_t)n;
    for(unsigned i=0;i<RUNTIME_CHANNELS;i++) {
        runtime_point p=previous_runtime.points[i];
        n=snprintf(out+used,size-used,",%u,%u,%u",p.stage,p.at_us,p.detail);
        if(n<0||(size_t)n>=size-used)return;
        used+=(size_t)n;
    }
}
void boot_trace_update(unsigned stage,unsigned healthy,int error) {
    retained_boot.magic=0;retained_boot.stage=stage;
    retained_boot.healthy=healthy;retained_boot.error=error;retained_boot.magic=BOOT_MAGIC;
}

// The standard IDF panic handler still prints, sets reset reason and reboots.
// Save only bounded internal-memory data before it runs. No heap, locks,
// flash writes, USB operations, PSRAM reads, or RTOS calls in this path.
static bool IRAM_ATTR internal_span(uintptr_t p,size_t n) {
    return p>=SOC_DRAM_LOW && p<SOC_DRAM_HIGH && n<=SOC_DRAM_HIGH-p;
}
static bool IRAM_ATTR code_address(uint32_t p) {
    return !(p&1)&&((p>=SOC_IROM_LOW&&p<SOC_IROM_HIGH)||
           (p>=SOC_IRAM_LOW&&p<SOC_IRAM_HIGH)||
           (p>=SOC_IROM_MASK_LOW&&p<SOC_IROM_MASK_HIGH));
}
// The IDF RISC-V port saves the interrupted frame pointer in the first TCB
// word (portasm.S). Read it only if both TCB and frame are in internal SRAM.
// The public task API is deliberately not called during a panic.
extern void * volatile pxCurrentTCBs[2];
extern UBaseType_t port_uxInterruptNesting[2];
extern StackType_t *xIsrStackBottom[2],*xIsrStackTop[2];
static void IRAM_ATTR capture_stack_guard(unsigned core) {
    for(unsigned i=0;i<2;i++) {
        retained_crash.guard[i].raw=REG_READ(i?ASSIST_DEBUG_CORE_1_INTR_RAW_REG:ASSIST_DEBUG_CORE_0_INTR_RAW_REG);
        retained_crash.guard[i].pc=assist_debug_ll_sp_spill_get_pc(i);
        retained_crash.guard[i].min=assist_debug_ll_sp_spill_get_min(i);
        retained_crash.guard[i].max=assist_debug_ll_sp_spill_get_max(i);
    }
    retained_crash.interrupted_pc=retained_crash.interrupted_ra=retained_crash.interrupted_sp=0;
    retained_crash.task_min=retained_crash.task_max=0;retained_crash.task_name[0]=0;
    retained_crash.irq_depth=retained_crash.irq_min=retained_crash.irq_max=0;
    if(core>=2)return;
    retained_crash.irq_depth=port_uxInterruptNesting[core];
    retained_crash.irq_min=(uintptr_t)xIsrStackBottom[core];
    retained_crash.irq_max=(uintptr_t)xIsrStackTop[core];
    uintptr_t tcb=(uintptr_t)pxCurrentTCBs[core];
    if((tcb&3)||!internal_span(tcb,sizeof(StaticTask_t)))return;
    // IDF port.c asserts these StaticTask_t offsets against the assembly TCB
    // stack-bound offsets; expression_with_stack.c uses the same fields.
    const volatile StaticTask_t *task=(const volatile StaticTask_t *)tcb;
    retained_crash.task_min=(uintptr_t)task->pxDummy6;
    retained_crash.task_max=(uintptr_t)task->pxDummy8;
    for(unsigned i=0;i<16&&i<configMAX_TASK_NAME_LEN;i++) {
        char c=task->ucDummy7[i];if(!c)break;
        // Only the fixed ASCII task identifier, never stack payload or user text.
        retained_crash.task_name[i]=(c>='a'&&c<='z')||(c>='A'&&c<='Z')||(c>='0'&&c<='9')||c=='_'?c:'?';
        retained_crash.task_name[i+1]=0;
    }
    uintptr_t saved=*(const volatile uint32_t *)tcb;
    if((saved&3)||!internal_span(saved,sizeof(RvExcFrame)))return;
    const volatile RvExcFrame *task_frame=(const volatile RvExcFrame *)saved;
    // These are diagnostic candidates when the panic interrupted an ISR;
    // they are not an unwound call stack or a task-context fault assertion.
    retained_crash.interrupted_pc=task_frame->mepc;
    retained_crash.interrupted_ra=task_frame->ra;
    retained_crash.interrupted_sp=task_frame->sp;
}
void __real_esp_panic_handler(panic_info_t *info);
void IRAM_ATTR __wrap_esp_panic_handler(panic_info_t *info) {
    retained_crash.magic=0;
    retained_crash.count=0;
    retained_crash.abort_detail[0]=0;
    retained_crash.aborted=g_panic_abort;
    retained_crash.core=info->core;
    retained_crash.exception=info->exception;
    retained_crash.pc=(uintptr_t)info->addr;
    retained_crash.ra=retained_crash.sp=retained_crash.cause=retained_crash.value=0;
    retained_crash.irq_cause=0;
    capture_stack_guard(info->core);
    const RvExcFrame *f=info->frame;
    if(internal_span((uintptr_t)f,sizeof(*f))) {
        retained_crash.pc=f->mepc;retained_crash.ra=f->ra;
        retained_crash.sp=f->sp;retained_crash.cause=f->mcause;retained_crash.value=f->mtval;
        retained_crash.irq_cause=f->s1;
        uintptr_t sp=(uintptr_t)f->sp;
        // Code-address candidates, not a claimed unwound backtrace. Discard all
        // non-code words so application data is never retained or exposed.
        if(!(sp&3))for(unsigned i=0;i<192&&retained_crash.count<24;i++) {
            uintptr_t at=sp+i*sizeof(uint32_t);
            if(!internal_span(at,sizeof(uint32_t)))break;
            uint32_t pc=*(const volatile uint32_t *)at;
            if(code_address(pc))retained_crash.code[retained_crash.count++]=pc;
        }
    }
    // IDF's formatted assertion/abort diagnostic is held in internal memory.
    // Do not dereference arbitrary external or flash strings during a panic.
    if(g_panic_abort)for(unsigned i=0;i+1<sizeof(retained_crash.abort_detail);i++) {
        uintptr_t at=(uintptr_t)g_panic_abort_details+i;
        if(!internal_span(at,1))break;
        char c=*(const volatile char *)at;
        retained_crash.abort_detail[i]=(c>=32&&c<127)?c:' ';
        retained_crash.abort_detail[i+1]=0;
        if(!c){retained_crash.abort_detail[i]=0;break;}
    }
    __asm__ volatile("fence rw,rw" ::: "memory");
    retained_crash.magic=CRASH_MAGIC;
    __real_esp_panic_handler(info);
}
void crash_record_init(void) {
    esp_reset_reason_t reason=esp_reset_reason();
    reset_raw[0]=esp_rom_get_reset_reason(0);reset_raw[1]=esp_rom_get_reset_reason(1);
    ota_performance_trace_init(reason!=ESP_RST_POWERON&&reason!=ESP_RST_BROWNOUT);
    if(reason!=ESP_RST_POWERON&&reason!=ESP_RST_BROWNOUT) {
        if(retained_ota.magic==TRACE_MAGIC)previous_ota=retained_ota;
        if(retained_crash.magic==CRASH_MAGIC)previous_crash=retained_crash;
        if(retained_boot.magic==BOOT_MAGIC)previous_boot=retained_boot;
        if(retained_runtime.magic==RUNTIME_MAGIC)previous_runtime=retained_runtime;
        if(retained_phases.magic==PHASE_MAGIC)previous_phases=retained_phases;
    }
    memset((void *)&retained_ota,0,sizeof(retained_ota));
    retained_phases.magic=0;
    retained_crash.magic=0;
    memset((void *)&retained_runtime,0,sizeof(retained_runtime));retained_runtime.magic=RUNTIME_MAGIC;
    boot_trace_update(1,0,0);
    runtime_active=true;
}
void ota_trace_begin(unsigned total) {
    ota_performance_trace_begin();
    retained_phases.magic=0;
    retained_ota.magic=0;
    retained_ota.total=total;retained_ota.offset=0;retained_ota.error=0;
    retained_ota.elapsed_ms=retained_ota.read_ms=retained_ota.write_ms=0;
    retained_ota.stage=OTA_TRACE_CHECK;
    retained_ota.stack_free=uxTaskGetStackHighWaterMark(NULL);
    retained_ota.magic=TRACE_MAGIC;
}
void ota_trace_update(unsigned stage,unsigned offset,int error) {
    retained_ota.offset=offset;retained_ota.error=error;
    unsigned free_stack=uxTaskGetStackHighWaterMark(NULL);
    if(free_stack<retained_ota.stack_free)retained_ota.stack_free=free_stack;
    retained_ota.stage=stage;
}
void ota_trace_timing(unsigned elapsed_ms,unsigned read_ms,unsigned write_ms) {
    retained_ota.elapsed_ms=elapsed_ms;retained_ota.read_ms=read_ms;retained_ota.write_ms=write_ms;
}
void ota_trace_phases(const ota_phase_timing *timing) {
    retained_phases.magic=0;retained_phases.timing=*timing;
    __asm__ volatile("fence rw,rw" ::: "memory");retained_phases.magic=PHASE_MAGIC;
}
static void phase_json(char *out,size_t size,const phase_trace *trace) {
    if(trace->magic!=PHASE_MAGIC){snprintf(out,size,"null");return;}
    const ota_phase_timing *p=&trace->timing;
    snprintf(out,size,"{\"transport\":%d,\"prepareMs\":%u,\"writeMs\":%u,\"freeWaitMs\":%u,"
        "\"readyWaitMs\":%u,\"verifyMs\":%u,\"installMs\":%u}",p->transport,p->prepare_ms,
        p->write_ms,p->free_wait_ms,p->ready_wait_ms,p->verify_ms,p->install_ms);
}
void crash_record_json(char *out,size_t size) {
    phase_trace current=retained_phases;
    char phases[240],old_phases[240];phase_json(phases,sizeof(phases),&current);phase_json(old_phases,sizeof(old_phases),&previous_phases);
    char runtime[256];crash_record_previous_runtime(runtime,sizeof(runtime));
    char code[24*12+1];size_t used=0;
    unsigned count=previous_crash.count;if(count>24)count=24;
    for(unsigned i=0;i<count;i++)used+=snprintf(code+used,sizeof(code)-used,"%s%u",i?",":"",previous_crash.code[i]);
    code[used]=0;
    // Abort diagnostic is restricted to printable ASCII above; escape JSON.
    char detail[321];used=0;
    for(unsigned i=0;i<sizeof(previous_crash.abort_detail)&&previous_crash.abort_detail[i];i++) {
        char c=previous_crash.abort_detail[i];if(c=='"'||c=='\\')detail[used++]='\\';detail[used++]=c;
    }
    detail[used]=0;
    snprintf(out,size,"{\"stage\":%u,\"offset\":%u,\"total\":%u,\"stackFree\":%u,\"error\":%d,"
        "\"previousStage\":%u,\"previousOffset\":%u,\"previousTotal\":%u,\"previousStackFree\":%u,\"previousError\":%d,"
        "\"crash\":%u,\"core\":%u,\"exception\":%u,\"pc\":%u,\"ra\":%u,\"sp\":%u,\"cause\":%u,\"value\":%u,"
        "\"aborted\":%u,\"abortDetail\":\"%s\",\"codeCandidates\":[%s],"
        "\"stackGuard\":[[%u,%u,%u,%u],[%u,%u,%u,%u]],"
        "\"irqCauseCandidate\":%u,\"interruptedContext\":[%u,%u,%u],"
        "\"taskName\":\"%s\",\"taskBounds\":[%u,%u],\"irqStack\":[%u,%u,%u],"
        "\"bootStage\":%u,\"healthySeconds\":%u,\"bootError\":%d,"
        "\"previousBootStage\":%u,\"previousHealthySeconds\":%u,\"previousBootError\":%d,"
        "\"otaMs\":[%u,%u,%u],\"previousOtaMs\":[%u,%u,%u],\"previousRuntime\":[%s],"
        "\"otaPhases\":%s,\"previousOtaPhases\":%s}",
        retained_ota.stage,retained_ota.offset,retained_ota.total,retained_ota.stack_free,retained_ota.error,
        previous_ota.stage,previous_ota.offset,previous_ota.total,previous_ota.stack_free,previous_ota.error,
        previous_crash.magic==CRASH_MAGIC,previous_crash.core,previous_crash.exception,previous_crash.pc,
        previous_crash.ra,previous_crash.sp,previous_crash.cause,previous_crash.value,previous_crash.aborted,detail,code,
        previous_crash.guard[0].raw,previous_crash.guard[0].pc,previous_crash.guard[0].min,previous_crash.guard[0].max,
        previous_crash.guard[1].raw,previous_crash.guard[1].pc,previous_crash.guard[1].min,previous_crash.guard[1].max,
        previous_crash.irq_cause,previous_crash.interrupted_pc,previous_crash.interrupted_ra,previous_crash.interrupted_sp,
        previous_crash.task_name,previous_crash.task_min,previous_crash.task_max,
        previous_crash.irq_depth,previous_crash.irq_min,previous_crash.irq_max,
        retained_boot.stage,retained_boot.healthy,retained_boot.error,previous_boot.stage,previous_boot.healthy,previous_boot.error,
        retained_ota.elapsed_ms,retained_ota.read_ms,retained_ota.write_ms,previous_ota.elapsed_ms,previous_ota.read_ms,previous_ota.write_ms,runtime,phases,old_phases);
}
