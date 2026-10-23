// Include the actual RTC owner; only attributes and task locks are substituted.
#ifdef NDEBUG
#undef NDEBUG
#endif
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include "freertos/FreeRTOS.h"
#include "../firmware/main/crash_record.h"

static bool locked;
static unsigned enters,exits,format_calls;
void probe_test_enter(portMUX_TYPE *lock,bool isr) {
    (void)lock;assert(!isr&&!locked);locked=true;enters++;
}
void probe_test_exit(portMUX_TYPE *lock,bool isr) {
    (void)lock;assert(!isr&&locked);locked=false;exits++;
}
static size_t format_outside_lock(const ota_performance_t *p,char *out,size_t size) {
    assert(!locked);
    assert((uintptr_t)p%_Alignof(ota_performance_t)==0);
    format_calls++;
    return ota_performance_format(p,out,size);
}
#define RTC_NOINIT_ATTR
#define ota_performance_format format_outside_lock
#include "../firmware/main/ota_performance_trace.c"
#undef ota_performance_format

static const char sample_csv[]=
    "1,1,1048576,4096,7,10,32,1,0,0,0,0,0,0,0,0,1,99,1,0,0,0,0,0,0,0";
static ota_performance_t sample(void) {
    ota_performance_t p;
    ota_performance_reset(&p,1,1048576,4096);
    ota_performance_add_copy(&p,7);
    ota_performance_record_flash(&p,0,0,10,32,true);
    ota_performance_record_verify(&p,OTA_PERFORMANCE_VERIFY_END,1,0,99);
    return p;
}
static void expect_empty(void) {
    char out[OTA_PERFORMANCE_SUFFIX_CAPACITY];
    memset(out,'X',sizeof(out));
    ota_trace_performance_format(out,sizeof(out));
    assert(!out[0]&&!locked&&enters==exits);
}
static void expect_record(unsigned origin,const char *csv) {
    char expected[OTA_PERFORMANCE_SUFFIX_CAPACITY],out[OTA_PERFORMANCE_SUFFIX_CAPACITY];
    int length=snprintf(expected,sizeof(expected),";op,%u,%s",origin,csv);
    assert(length>0&&(size_t)length<sizeof(expected));
    ota_trace_performance_format(out,sizeof(out));
    assert(strcmp(expected,out)==0&&!locked&&enters==exits);
}
static void seed_retained(unsigned magic,const ota_performance_t *p) {
    assert(!locked);
    memcpy((void *)retained_performance.words,p,sizeof(*p));
    retained_performance.magic=magic;
}
static ota_performance_t retained_value(void) {
    ota_performance_t value;
    memcpy(&value,(const void *)retained_performance.words,sizeof(value));
    return value;
}
static void cold_and_invalid_boot(void) {
    ota_performance_t p=sample();
    seed_retained(PERFORMANCE_MAGIC,&p);
    ota_performance_trace_init(false); // Both power-on and brownout use false.
    assert(!retained_performance.magic&&!previous_performance.magic);
    assert(!performance_started);expect_empty();
    seed_retained(PERFORMANCE_MAGIC^1u,&p);
    ota_performance_trace_init(true);
    assert(!retained_performance.magic);expect_empty();
    seed_retained(0,&p); // Interrupted publication is never valid.
    ota_performance_trace_init(true);expect_empty();
}
static void publish_copy_and_new_begin(void) {
    ota_performance_t p=sample();
    ota_performance_trace_init(false);
    ota_performance_trace_begin();expect_empty();
    unsigned calls=enters;
    ota_trace_performance(NULL);
    assert(enters==calls);expect_empty();
    ota_trace_performance(&p);
    assert(retained_performance.magic==PERFORMANCE_MAGIC);
    ota_performance_t stored=retained_value();
    assert(stored.target_address==p.target_address);
    assert(stored.flash[0].successful_bytes==32);
    p.copy_us=12345; // Publishing owns a value copy, never the caller pointer.
    expect_record(0,sample_csv);
    ota_performance_trace_begin(); // Do not return the old current value.
    assert(!retained_performance.magic);expect_empty();
    ota_trace_performance(&p);
    stored=retained_value();assert(stored.copy_us==12345);
    p=sample();ota_trace_performance(&p);expect_record(0,sample_csv);
}
static void soft_boot_and_old_record_consumption(void) {
    ota_performance_t p=sample();
    ota_performance_trace_init(false);ota_performance_trace_begin();
    ota_trace_performance(&p);
    ota_performance_trace_init(true); // Model retained RTC and fresh boot state.
    assert(!performance_started&&!retained_performance.magic);
    assert(previous_performance.magic==PERFORMANCE_MAGIC);
    expect_record(1,sample_csv);
    // Copy is independent of retained memory, which no longer has a valid magic.
    ota_performance_t changed=retained_value();changed.copy_us=555;
    seed_retained(0,&changed);
    expect_record(1,sample_csv);
    ota_performance_trace_begin();expect_empty(); // No fallback to previous boot.
    ota_performance_trace_init(true);expect_empty(); // Nothing newly published.
    ota_performance_trace_begin();ota_trace_performance(&p);
    ota_performance_trace_init(true);expect_record(1,sample_csv);
    ota_performance_trace_init(true);expect_empty(); // Consumed only once.
    ota_performance_trace_begin();ota_trace_performance(&p);
    ota_performance_trace_init(false);expect_empty(); // Cold boot discards valid RTC.
}
static void maximum_record_and_short_buffers(void) {
    ota_performance_t p;
    ota_performance_reset(&p,INT32_MIN,UINT32_MAX,UINT32_MAX);
    p.copy_us=UINT64_MAX;
    for(unsigned i=0;i<OTA_PERFORMANCE_FLASH_BUCKETS;i++)
        p.flash[i]=(ota_performance_flash){UINT64_MAX,UINT64_MAX,UINT32_MAX};
    p.flash_different_endpoints=p.flash_unknown_endpoints=UINT32_MAX;
    for(unsigned i=0;i<OTA_PERFORMANCE_VERIFY_COUNT;i++)
        ota_performance_record_verify(&p,(ota_performance_verify_phase)i,-1,-1,UINT64_MAX);
    ota_performance_trace_init(false);ota_performance_trace_begin();
    ota_trace_performance(&p);
    char full[OTA_PERFORMANCE_SUFFIX_CAPACITY];
    ota_trace_performance_format(full,sizeof(full));
    size_t length=strlen(full);
    assert(length==OTA_PERFORMANCE_CSV_MAX_CHARS+6);
    assert(!strncmp(full,";op,0,",6));
    for(size_t capacity=0;capacity<=sizeof(full);capacity++) {
        char guarded[OTA_PERFORMANCE_SUFFIX_CAPACITY+2];
        memset(guarded,'X',sizeof(guarded));
        char *out=guarded+1;
        unsigned calls=enters;
        ota_trace_performance_format(out,capacity);
        assert(!locked&&enters==exits);
        if(capacity<=6)assert(enters==calls);
        if(!capacity)assert(out[0]=='X');
        else if(capacity<=length)assert(!out[0]);
        else assert(!strcmp(out,full));
        assert(guarded[0]=='X');
        for(size_t i=capacity+1;i<sizeof(guarded);i++)assert(guarded[i]=='X');
    }
    unsigned calls=enters;
    ota_trace_performance_format(NULL,100);
    assert(enters==calls);
    ota_performance_trace_init(true);
    ota_trace_performance_format(full,sizeof(full));
    assert(strlen(full)==length&&!strncmp(full,";op,1,",6));
}
int main(void) {
    assert(_Alignof(performance_trace)==4);
    assert(offsetof(performance_trace,words)==4);
    assert(sizeof(performance_trace)==4+4*PERFORMANCE_WORD_COUNT);
    cold_and_invalid_boot();
    publish_copy_and_new_begin();
    soft_boot_and_old_record_consumption();
    maximum_record_and_short_buffers();
    assert(enters==exits&&!locked&&format_calls>0);
    printf("OTA_PERFORMANCE_TRACE_OK actual source; cold/soft boot, publish copy, begin invalidation, one-boot retention, origins, bounded format, balanced locks, 4-byte RTC alignment; suffix=%u\n",
        (unsigned)(OTA_PERFORMANCE_SUFFIX_CAPACITY-1));
    return 0;
}
