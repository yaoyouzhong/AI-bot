// Execute the production OTA task with deterministic HTTP/flash failure injection.
#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdatomic.h>
#include <windows.h>
#include "cJSON.h"
#include "../../firmware/main/link_state.h"
static link_kind test_link=LINK_WIFI;
static link_kind rpc_ota_link_select(void){return test_link;}
static bool benchmark_busy;
static bool flash_testing;
static bool flash_benchmark_busy(void){return flash_testing;}
static int wifi_users;
static void wifi_performance_bulk(bool active){wifi_users+=active?1:-1;assert(wifi_users>=0&&wifi_users<=1);}
bool transport_benchmark_busy(void){return benchmark_busy;}
static int rpc_ota_read(link_kind link,const char *sha,const char *offer,int offset,uint8_t *out,int count);
#include "../../firmware/main/crash_record.h"
#define ESP_OK 0
#define ESP_FAIL -1
#define ESP_ERR_NO_MEM 0x101
#define OTA_WITH_SEQUENTIAL_WRITES ((size_t)-2)
typedef int esp_err_t;
#define portMAX_DELAY INFINITE
#define pdTRUE 1
#define pdPASS 1
#define pdMS_TO_TICKS(n) (n)
typedef HANDLE SemaphoreHandle_t;
static SemaphoreHandle_t device_mutex;
static SemaphoreHandle_t xSemaphoreCreateBinary(void){return CreateSemaphore(NULL,0,1,NULL);}
static int xSemaphoreTake(SemaphoreHandle_t s,unsigned ms){return !s||WaitForSingleObject(s,ms)==WAIT_OBJECT_0;}
static int xSemaphoreGive(SemaphoreHandle_t s){return !s||ReleaseSemaphore(s,1,NULL);}
static void vSemaphoreDelete(SemaphoreHandle_t s){CloseHandle(s);}
typedef struct {CRITICAL_SECTION lock;HANDLE empty,full;int head,tail,count,size;unsigned char *data;} *QueueHandle_t;
static QueueHandle_t xQueueCreate(int count,int size){
    QueueHandle_t q=calloc(1,sizeof(*q));assert(q);q->data=malloc(count*size);q->count=count;q->size=size;
    InitializeCriticalSection(&q->lock);q->empty=CreateSemaphore(NULL,count,count,NULL);q->full=CreateSemaphore(NULL,0,count,NULL);return q;
}
static int xQueueSend(QueueHandle_t q,const void *value,unsigned ms){
    if(WaitForSingleObject(q->empty,ms)!=WAIT_OBJECT_0)return 0;
    EnterCriticalSection(&q->lock);memcpy(q->data+q->tail*q->size,value,q->size);q->tail=(q->tail+1)%q->count;LeaveCriticalSection(&q->lock);
    ReleaseSemaphore(q->full,1,NULL);return 1;
}
static int xQueueReceive(QueueHandle_t q,void *value,unsigned ms){
    if(WaitForSingleObject(q->full,ms)!=WAIT_OBJECT_0)return 0;
    EnterCriticalSection(&q->lock);memcpy(value,q->data+q->head*q->size,q->size);q->head=(q->head+1)%q->count;LeaveCriticalSection(&q->lock);
    ReleaseSemaphore(q->empty,1,NULL);return 1;
}
static void vQueueDelete(QueueHandle_t q){CloseHandle(q->empty);CloseHandle(q->full);DeleteCriticalSection(&q->lock);free(q->data);free(q);}
static atomic_int yields;
static atomic_uint elapsed_ms;
static unsigned preparation_ack_at,completion_ack_at,backlight_off_at;
volatile unsigned tab5_lcd_underruns;
static void simulated_delay(unsigned ms);
#define vTaskDelay(n) simulated_delay(n)
#define vTaskDelete(p) return
#define vTaskDeleteWithCaps(p) return
typedef struct {char id[13],host[16];bool paired;int port,mode;} device_config;
static atomic_uint host_epoch=1;
static unsigned device_host_epoch(void){return host_epoch;}
static bool device_host_changing(void){return false;}
static cJSON *device_snapshots[3];
static atomic_int recovery_fault,recovery_failures,range_calls,first_resume_offset=-1;
static bool recovery_injected;
static void recovery_range_failed(void);
static atomic_bool recovery_event_claimed,recovery_event_pending,radio_ready=true;
static atomic_uint receiver_thread_id;
static atomic_uint reconnect_due;
static bool rpc_ota_link_connected(link_kind link){assert(link==test_link);return radio_ready;}
enum {RECOVER_ONCE=1,RECOVER_EXHAUST,RECOVER_REJECT,RECOVER_STOP,RECOVER_OWNER,RECOVER_GENERATION,RECOVER_CORRUPT,RECOVER_OPEN,RECOVER_WAIT};
static device_config device_get_config(void){return (device_config){.id="001122334455",.host="192.168.1.2",.paired=true,.port=18765,.mode=-1};}
static void device_nonce(char out[33]){memset(out,'1',32);out[32]=0;}
static void device_proof(const char *in,char out[65]){(void)in;memset(out,'0',64);out[64]=0;}
typedef struct {unsigned address,size;} esp_partition_t;
static esp_partition_t running_partition={0x20000,0x6e0000},next_partition={0x700000,0x6e0000};
static const esp_partition_t *esp_ota_get_running_partition(void){return &running_partition;}
static const esp_partition_t *esp_ota_get_next_update_partition(void *p){(void)p;return &next_partition;}
static int esp_partition_read(const esp_partition_t *p,int at,void *out,int size){(void)p;(void)at;memset(out,0,size);return ESP_OK;}
typedef unsigned esp_ota_handle_t;
typedef struct {char project_name[32],version[32];} esp_app_desc_t;
static esp_app_desc_t running_description={"aibot_tab5","previous"};
static const esp_app_desc_t *esp_app_get_description(void){return &running_description;}
enum {SUCCESS,OPEN_FAIL,SHORT_READ,WRITE_FAIL,HASH_FAIL,END_FAIL,IDENTITY_FAIL,BOOT_FAIL,BEGIN_FAIL,UI_NO_RENDER,BUFFER_FAIL,SECOND_BUFFER_FAIL,READER_START_FAIL,FLASH_BUFFER_FAIL,FLASH_LOW_MEMORY,FLASH_FRAGMENTED_16,FLASH_FRAGMENTED_8,PREFETCH_LOW_MEMORY,PREFETCH_EXTRA_FAIL,UI_PREPARE_NO_RENDER};
static int allocations;
static int fault,aborts,boot_changes,restarts,transfer_size,write_at;
static bool compressed_http;
static uint8_t *http_payload;
static int http_size,http_at,block_ends[160],decoded_ends[160],http_blocks;
static atomic_int read_at,write_calls;
static bool early_feedback_seen;
static HANDLE prefetch_ready,receive_complete;static unsigned overlap_cases;
static void check_download_feedback(void);
static void check_prepare_feedback(void);
static void check_write_feedback(void);
static void check_verification_feedback(bool advanced);
#define MALLOC_CAP_SPIRAM 1
#define MALLOC_CAP_8BIT 2
#define MALLOC_CAP_INTERNAL 4
static size_t heap_caps_get_largest_free_block(unsigned caps){return caps& MALLOC_CAP_INTERNAL?4096:1048576;}
static void *internal_buffer;static size_t internal_capacity;
static unsigned long long external_write_bytes;
static bool flash_input_internal(const void *p);
static size_t heap_caps_get_free_size(unsigned caps){assert(caps==3||caps==6);if(caps==3)return (fault==PREFETCH_LOW_MEMORY?4:12)*1024*1024-(size_t)allocations*49152;return fault==FLASH_LOW_MEMORY?38603:200*1024;}
static void *heap_caps_malloc(size_t size,unsigned caps){
    assert(caps==3||caps==6);
    if(caps==6){
        assert(fault!=FLASH_LOW_MEMORY&&(size==49152||size==16384||size==8192));
        if(fault==FLASH_BUFFER_FAIL||(fault==FLASH_FRAGMENTED_16&&size>16384)||(fault==FLASH_FRAGMENTED_8&&size>8192))return NULL;
        internal_capacity=size;return internal_buffer=malloc(size);
    }
    if(size!=49152){assert(compressed_http);return malloc(size);}
    allocations++;
    return fault==BUFFER_FAIL||(fault==SECOND_BUFFER_FAIL&&allocations==2)||(fault==PREFETCH_EXTRA_FAIL&&allocations==3)?NULL:malloc(size);
}
static int64_t esp_timer_get_time(void){return (int64_t)elapsed_ms*1000;}
static int previous_progress;
static unsigned trace_stage,trace_offset;
static void ota_trace_begin_mock(unsigned total){assert(total==(unsigned)transfer_size);trace_stage=OTA_TRACE_CHECK;trace_offset=0;}
static void ota_trace_update_mock(unsigned stage,unsigned offset,int error){(void)error;assert(offset<=(unsigned)transfer_size);trace_stage=stage;trace_offset=offset;}
#define ota_trace_begin ota_trace_begin_mock
#define ota_trace_update ota_trace_update_mock
static void ota_trace_timing(unsigned elapsed,unsigned read,unsigned write){assert(elapsed>=read&&elapsed>=write&&elapsed>=250&&write>=250);}
static ota_phase_timing measured_phases;
static ota_performance_t measured_performance;
// Exercise both starting cores in the real install loop without changing its
// decisions. Pure helper tests separately cover unequal/unknown endpoints.
static int xPortGetCoreID(void){return write_calls&1;}
static void ota_trace_performance_mock(const ota_performance_t *value){measured_performance=*value;}
#define ota_trace_performance ota_trace_performance_mock
static void ota_trace_phases_mock(const ota_phase_timing *timing){measured_phases=*timing;}
#define ota_trace_phases ota_trace_phases_mock
static bool erased;
static int esp_ota_begin(const esp_partition_t *p,size_t n,esp_ota_handle_t *out){assert(p==&next_partition&&p!=&running_partition&&n==(size_t)transfer_size&&n<=p->size);assert(read_at>=(int)(n<49152?n:49152)&&!write_calls);check_prepare_feedback();*out=1;if(fault==SUCCESS&&n>49152){assert(WaitForSingleObject(prefetch_ready,2000)==WAIT_OBJECT_0);assert(read_at>=98304&&!write_calls);}elapsed_ms+=250;if(fault==BEGIN_FAIL)return -1;erased=true;return ESP_OK;}
static int esp_ota_write(esp_ota_handle_t h,const void *p,int n){(void)h;assert(erased);write_calls++;if(!flash_input_internal(p))external_write_bytes+=n;assert(flash_input_internal(p));if(fault==SUCCESS&&transfer_size>49152&&!write_at){assert(WaitForSingleObject(receive_complete,2000)==WAIT_OBJECT_0);assert(read_at==transfer_size);overlap_cases++;}check_write_feedback();assert(n>0&&n<=(int)(internal_capacity?internal_capacity:4096));assert(trace_stage==OTA_TRACE_WRITE&&trace_offset==(unsigned)(write_at/49152*49152));const uint8_t *bytes=p;for(int i=0;i<n;i++)assert(bytes[i]==((write_at+i)==0?(fault==HASH_FAIL?2:1):0));if(fault==WRITE_FAIL&&write_at+n>transfer_size/2)return -1;write_at+=n;return ESP_OK;}
static int esp_ota_end(esp_ota_handle_t h){(void)h;check_verification_feedback(false);elapsed_ms+=1207;check_verification_feedback(true);return fault==END_FAIL?-1:ESP_OK;}
static void esp_ota_abort(esp_ota_handle_t h){(void)h;aborts++;}
static int esp_ota_get_partition_description(const esp_partition_t *p,esp_app_desc_t *out){(void)p;strcpy(out->project_name,fault==IDENTITY_FAIL?"other":"aibot_tab5");strcpy(out->version,"test");return ESP_OK;}
static int esp_ota_set_boot_partition(const esp_partition_t *p){assert(p==&next_partition);check_verification_feedback(true);if(fault==BOOT_FAIL)return -1;boot_changes++;return ESP_OK;}
static int bsp_display_backlight_off(void){assert(completion_ack_at&&elapsed_ms-completion_ack_at>=3500);backlight_off_at=elapsed_ms;return ESP_OK;}
static void esp_restart(void){assert(backlight_off_at&&elapsed_ms-backlight_off_at>=50);restarts++;}
enum {HTTP_EVENT_ON_HEADER};
typedef struct {int event_id;void *user_data;const char *header_key,*header_value;} esp_http_client_event_t;
typedef struct {const char *url;int timeout_ms;bool disable_auto_redirect;int buffer_size;esp_err_t (*event_handler)(esp_http_client_event_t *);void *user_data;} esp_http_client_config_t;
static esp_http_client_config_t http_options;
typedef void *esp_http_client_handle_t;
static void *esp_http_client_init(const esp_http_client_config_t *c){assert(c->disable_auto_redirect);http_options=*c;return (void *)1;}
static void esp_http_client_set_header(void *c,const char *k,const char *v){
    (void)c;if(!strcmp(k,"X-AIBot-OTA-Flow"))assert(!strcmp(v,"tcp-v2"));
}
static int esp_http_client_open(void *c,int n){(void)c;(void)n;return fault==OPEN_FAIL||(recovery_fault==RECOVER_OPEN&&!range_calls)?-1:ESP_OK;}
static int64_t esp_http_client_fetch_headers(void *c){
    (void)c;if(!compressed_http)return transfer_size;
    esp_http_client_event_t e={HTTP_EVENT_ON_HEADER,http_options.user_data,"x-aibot-ota-encoding","aibot-zlib-blocks-v1"};
    assert(http_options.event_handler(&e)==ESP_OK);return http_size;
}
static int esp_http_client_get_status_code(void *c){(void)c;return 200;}
static int esp_http_client_read(void *c,char *out,int n){
    (void)c;check_download_feedback();
    if(recovery_fault&&test_link==LINK_WIFI&&!recovery_injected&&
       (compressed_http?http_at>=block_ends[0]+16:read_at>=65536)) {
        recovery_injected=true;if(recovery_fault==RECOVER_WAIT){radio_ready=false;reconnect_due=elapsed_ms+30000;}return 0;
    }
    if(fault==SHORT_READ&&(compressed_http?http_at>=http_size/2:read_at>=transfer_size/2))return 0;if(n>137)n=137;
    if(compressed_http){
        if(fault==SHORT_READ&&n>http_size/2-http_at)n=http_size/2-http_at;
        if(n>http_size-http_at)n=http_size-http_at;memcpy(out,http_payload+http_at,n);http_at+=n;
        for(int i=0;i<http_blocks;i++)if(http_at>=block_ends[i])read_at=decoded_ends[i];
    }else{memset(out,0,n);if(!read_at)out[0]=fault==HASH_FAIL?2:1;read_at+=n;}
    if(read_at>=98304)SetEvent(prefetch_ready);return n;
}
static void rpc_ota_reset_bytes(link_kind link){(void)link;}
static unsigned rpc_ota_received_bytes(link_kind link){(void)link;return (unsigned)read_at;}
static int rpc_ota_read(link_kind link,const char *sha,const char *offer,int offset,uint8_t *out,int count) {
    assert(link==test_link&&strlen(sha)==64&&strlen(offer)==32&&offset==read_at);
    if(recovery_fault&&offset>=49152) {
        range_calls++;
        if(first_resume_offset<0)first_resume_offset=offset;
        if(!recovery_injected||recovery_fault==RECOVER_EXHAUST||recovery_fault==RECOVER_STOP||recovery_fault==RECOVER_OWNER||
           recovery_fault==RECOVER_REJECT||recovery_fault==RECOVER_CORRUPT) {
            recovery_injected=true;recovery_failures++;
            if(recovery_fault==RECOVER_WAIT){radio_ready=false;reconnect_due=elapsed_ms+30000;}
            int result=recovery_fault==RECOVER_REJECT?-401:recovery_fault==RECOVER_CORRUPT?-3:recovery_fault==RECOVER_GENERATION?-1005:-2;
            recovery_range_failed();return result;
        }
    }
    if(fault==OPEN_FAIL)return -1;
    if(count>8192)count=8192;
    return esp_http_client_read(NULL,(char *)out,count);
}
static int rpc_ota_http_read(void *http,const char *sha,const char *offer,int offset,uint8_t *out,int count) {
    assert(http&&test_link==LINK_WIFI&&strlen(sha)==64&&strlen(offer)==32);
    if(first_resume_offset<0)first_resume_offset=offset;
    range_calls++;
    if(recovery_fault==RECOVER_EXHAUST||recovery_fault==RECOVER_STOP||recovery_fault==RECOVER_OWNER){recovery_failures++;recovery_range_failed();return -2;}
    if(recovery_fault==RECOVER_REJECT){recovery_failures++;return -401;}
    if(recovery_fault==RECOVER_CORRUPT){recovery_failures++;return -3;}
    // The incomplete stream block is deliberately overwritten at its boundary.
    memset(out,0,count);if(!offset)out[0]=1;read_at=offset+count;
    check_download_feedback();return count;
}
static void esp_http_client_close(void *c){(void)c;}
static void esp_http_client_cleanup(void *c){(void)c;}
// This facade tests integrity-failure control flow, not the platform SHA primitive.
typedef struct {uint8_t sum;} mbedtls_sha256_context;
static void mbedtls_sha256_init(mbedtls_sha256_context *c){c->sum=0;}
static void mbedtls_sha256_starts(mbedtls_sha256_context *c,int n){(void)n;c->sum=0;}
static void mbedtls_sha256_update(mbedtls_sha256_context *c,const void *p,size_t n){const uint8_t *b=p;for(size_t i=0;i<n;i++)c->sum^=b[i];}
static void mbedtls_sha256_finish(mbedtls_sha256_context *c,uint8_t out[32]){memset(out,c->sum,32);}
static void mbedtls_sha256_free(mbedtls_sha256_context *c){(void)c;}
static void (*worker)(void *);static void *worker_arg;
typedef struct {void (*fn)(void *);void *arg;} test_thread;
static DWORD WINAPI launch_thread(void *p){test_thread t=*(test_thread*)p;free(p);atomic_store(&receiver_thread_id,(unsigned)GetCurrentThreadId());t.fn(t.arg);return 0;}
static int xTaskCreate(void (*fn)(void *),const char *name,int stack,void *arg,int priority,void *handle){
    (void)stack;(void)priority;(void)handle;
    if(!strcmp(name,"tab5_ota_rx")){if(fault==READER_START_FAIL)return 0;test_thread *t=malloc(sizeof(*t));*t=(test_thread){fn,arg};HANDLE thread=CreateThread(NULL,0,launch_thread,t,0,NULL);assert(thread);CloseHandle(thread);return pdPASS;}
    worker=fn;worker_arg=arg;return pdPASS;
}
static int xTaskCreateWithCaps(void (*fn)(void *),const char *name,int stack,void *arg,int priority,void *handle,unsigned caps){
    assert(caps==(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT));
    assert(stack==8192&&!strcmp(name,"tab5_ota_rx"));
    return xTaskCreate(fn,name,stack,arg,priority,handle);
}
#define TAB5_TEST_EXTERNAL_HEAP
#include "ota_zlib_host.h"
#define strcasecmp _stricmp
#include "../../firmware/main/ota_stream.h"
#include "../../firmware/main/ota_resume.h"
#include "../../firmware/main/gallery_remote.h"
bool ota_update_request_checked(const char *,const char *);
#define DRAM_ATTR
#include "ota_update_under_test.inc"
static bool flash_input_internal(const void *p){return (internal_buffer&&p==internal_buffer)||p==flash_fallback;}
static void prepare_http_payload(void) {
    free(http_payload);http_payload=NULL;http_at=http_size=http_blocks=0;
    if(!compressed_http)return;
    typedef int (__cdecl *compress_fn)(void*,unsigned long*,const void*,unsigned long,int);
    HMODULE dll=LoadLibraryA(getenv("TAB5_TEST_ZLIB")?getenv("TAB5_TEST_ZLIB"):"C:\\Program Files\\Git\\ucrt64\\bin\\zlib1.dll");assert(dll);
    compress_fn encode=(compress_fn)GetProcAddress(dll,"compress2");assert(encode);
    http_payload=malloc(transfer_size+160*16);
    for(int at=0;at<transfer_size;at+=49152) {
        int count=transfer_size-at;if(count>49152)count=49152;
        uint8_t raw[49152]={0};if(!at)raw[0]=fault==HASH_FAIL?2:1;
        unsigned long length=49152;uint8_t packed[49152];assert(encode(packed,&length,raw,count,6)==0&&length<(unsigned)count);
        uint32_t header[4]={(unsigned)at,(unsigned)count,length,1};
        memcpy(http_payload+http_size,header,16);http_size+=16;memcpy(http_payload+http_size,packed,length);http_size+=length;
        block_ends[http_blocks]=http_size;decoded_ends[http_blocks++]=at+count;
    }
    FreeLibrary(dll);
}
static void check_prepare_feedback(void){check_download_feedback();char status[128];ota_update_status(status,sizeof(status));assert(trace_stage==OTA_TRACE_BEGIN&&strstr(status,"正在准备")&&strstr(status,"已接收"));assert(preparation_ack_at&&atomic_load(&preparation_presented));if(transfer_size>49152)assert(atomic_load(&buffer_slots)==(unsigned)((fault==PREFETCH_LOW_MEMORY||fault==PREFETCH_EXTRA_FAIL)?2:(transfer_size+49151)/49152));}
static void check_download_feedback(void) {
    if(read_at>0&&!write_calls) {
        assert(ota_update_progress()==0); // prefetched bytes are not committed flash
        // Observe the same public status consumed by the device UI. During
        // preparation it derives byte counts from atomics; the internal generic
        // phase label intentionally has no KB and is not the rendered status.
        char status[128];ota_update_status(status,sizeof(status));
        if(strstr(status,"已接收")&&strstr(status,"KB"))early_feedback_seen=true;
    }
}
static void check_write_feedback(void) {
    assert(!ota_update_verifying()&&!ota_update_completed());
    assert(ota_update_progress()<99&&ota_update_progress()<=(int)((int64_t)write_at*99/transfer_size));
    if(fault==SUCCESS&&transfer_size>49152&&!write_at) {
        char status[128];ota_update_status(status,sizeof(status));
        assert(ota_update_progress()==0&&strstr(status,"下载完成")&&strstr(status,"正在写入 0 /"));
    }
}
static void check_verification_feedback(bool advanced) {
    assert(ota_update_verifying()&&!ota_update_completed()&&ota_update_progress()==99);
    assert(write_at==transfer_size&&!boot_changes&&!completion_ack_at);
    char status[128];ota_update_status(status,sizeof(status));
    assert(strstr(status,"正在校验固件")&&strstr(status,advanced?"1.2 秒":"0.0 秒"));
}
// Arm only after the mock has recorded a real failed range attempt. Consume
// on the receiver's next retry delay, after the retry-loop ownership check;
// the concurrent writer's delays and status messages cannot deliver it early.
static void recovery_range_failed(void) {
    int event=atomic_load(&recovery_fault);
    if(event==RECOVER_STOP||event==RECOVER_OWNER)atomic_store(&recovery_event_pending,true);
}
static void simulated_delay(unsigned ms) {
    elapsed_ms+=ms;yields++;
    if(!radio_ready&&reconnect_due&&elapsed_ms>=reconnect_due)radio_ready=true;
    if((unsigned)GetCurrentThreadId()==atomic_load(&receiver_thread_id)&&
       atomic_exchange(&recovery_event_pending,false)&&!atomic_exchange(&recovery_event_claimed,true)) {
        assert(ms==50&&atomic_load(&range_calls)==1&&atomic_load(&recovery_failures)==1);
        int event=atomic_load(&recovery_fault);assert(event==RECOVER_STOP||event==RECOVER_OWNER);
        if(event==RECOVER_OWNER)host_epoch++;
        else {
            // An older cross-channel withdrawal must not cancel the new offer.
            ota_update_consider("{\"data\":{\"epochMilliseconds\":0}}");
            assert(!atomic_load(&offer_withdrawn));
            ota_update_consider("{\"data\":{\"epochMilliseconds\":3}}");
            assert(atomic_load(&offer_withdrawn));
        }
        atomic_store(&recovery_fault,0);
    }
    if(atomic_load(&download_bytes)==transfer_size)SetEvent(receive_complete);
    Sleep(0);
    int percent=ota_update_progress();assert(percent>=0&&percent<=100);
    // Production progress is atomic; the receiver and writer now overlap.
    if(ota_update_completed())assert(percent==100&&boot_changes==1);
    else assert(percent<100);
    if(!ota_update_completed()&&!preparation_ack_at&&fault!=UI_PREPARE_NO_RENDER) {
        ota_update_preparation_presented();preparation_ack_at=elapsed_ms;
    }
    // Simulate delayed rendering, not just receiving a completion message.
    if(ota_update_completed()&&!completion_ack_at&&fault!=UI_NO_RENDER&&ms==50) {
        ota_update_completion_presented();completion_ack_at=elapsed_ms;
    }
}
int main(void) { SetErrorMode(SEM_NOGPFAULTERRORBOX|SEM_FAILCRITICALERRORS);_set_error_mode(_OUT_TO_STDERR);_set_abort_behavior(0,_WRITE_ABORT_MSG|_CALL_REPORTFAULT);
    ota_resume limits={.started_ms=10,.gap_ms=20,.in_gap=true};
    assert(ota_resume_wait_budget(&limits,30020));
    assert(!ota_resume_wait_budget(&limits,90020));
    assert(!ota_resume_wait_budget(&limits,9));
    limits.started_ms=0;limits.gap_ms=899999;
    assert(!ota_resume_wait_budget(&limits,900000));
    prefetch_ready=CreateEvent(NULL,TRUE,FALSE,NULL);receive_complete=CreateEvent(NULL,TRUE,FALSE,NULL);assert(prefetch_ready&&receive_complete);
    char offer[512];
    int sizes[]={1024,6464048};
    for(int mode=0;mode<4;mode++)
    for(unsigned s=0;s<sizeof(sizes)/sizeof(sizes[0]);s++) {
      test_link=mode==3?LINK_WIFI:(link_kind)mode;compressed_http=mode==3;
      transfer_size=sizes[s];
      snprintf(offer,sizeof(offer),"{\"data\":{\"ota\":{\"sha256\":\"0101010101010101010101010101010101010101010101010101010101010101\",\"size\":%d,\"version\":\"test\",\"offerId\":\"11111111111111111111111111111111\"}}}",transfer_size);
      for(fault=SUCCESS;fault<=UI_PREPARE_NO_RENDER;fault++) {
        prepare_http_payload();
        last_offer[0]=0;pending_offer.size=0;atomic_store(&busy,false);worker=NULL;
        read_at=write_calls=write_at=aborts=boot_changes=restarts=yields=0;
        elapsed_ms=preparation_ack_at=completion_ack_at=backlight_off_at=0;
        previous_progress=0;allocations=0;internal_buffer=NULL;internal_capacity=0;
        memset(&measured_phases,0,sizeof(measured_phases));
        early_feedback_seen=false;erased=false;ResetEvent(prefetch_ready);ResetEvent(receive_complete);
        ota_update_consider(offer);assert(ota_update_available()&&!worker&&!ota_update_busy());
        assert(!ota_update_request_checked("ffffffffffffffffffffffffffffffff",pending_offer.sha)&&ota_update_available()&&!worker&&!ota_update_busy());
        assert(ota_update_request()&&worker&&ota_update_busy());
        assert(ota_update_progress()==0);
        char target[32];ota_update_target_version(target,sizeof(target));assert(!strcmp(target,"test"));
        assert(!ota_update_request());worker(worker_arg);assert(wifi_users==0);
        assert(measured_performance.transport==(int)test_link);
        assert(measured_performance.target_address==next_partition.address);
        uint64_t measured_bytes=0;uint32_t measured_calls=0;
        for(unsigned core=0;core<OTA_PERFORMANCE_FLASH_BUCKETS;core++) {
            measured_bytes+=measured_performance.flash[core].successful_bytes;
            measured_calls+=measured_performance.flash[core].calls;
        }
        assert(measured_bytes==(uint64_t)write_at&&measured_calls==(uint32_t)write_calls);
        bool success=fault==SUCCESS||fault==FLASH_BUFFER_FAIL||fault==FLASH_LOW_MEMORY||fault==FLASH_FRAGMENTED_16||fault==FLASH_FRAGMENTED_8||fault==PREFETCH_LOW_MEMORY||fault==PREFETCH_EXTRA_FAIL;
        if(success){int n=internal_capacity?(int)internal_capacity:4096;assert(boot_changes==1&&restarts==1&&write_at==transfer_size&&yields>=(transfer_size+49151)/49152&&trace_stage==OTA_TRACE_DONE&&write_calls==(transfer_size+n-1)/n);
            assert(measured_phases.transport==(int)test_link&&measured_phases.prepare_ms>=250&&measured_phases.verify_ms==1207);
            assert(measured_phases.install_ms>=measured_phases.prepare_ms+measured_phases.write_ms+measured_phases.verify_ms);
            assert(elapsed_ms-measured_phases.install_ms>=3500); // success dwell/reboot are separate
            assert(measured_performance.staging_bytes==(uint32_t)n);
            assert(measured_performance.verify_valid_mask==7);
            assert(measured_performance.verify[OTA_PERFORMANCE_VERIFY_END].elapsed_us==1207000);
        }
        else if(fault==UI_NO_RENDER)assert(boot_changes==1&&!restarts&&!backlight_off_at&&ota_update_busy()&&strstr(message,"请手动重启"));
        else assert(!boot_changes&&!restarts&&!ota_update_busy());
        if(!success&&fault!=UI_NO_RENDER)assert(ota_update_progress()<100);
        if(fault==SHORT_READ||fault==WRITE_FAIL||fault==HASH_FAIL||fault==BEGIN_FAIL)assert(aborts==((erased||fault==BEGIN_FAIL)?1:0));
        if(fault==SHORT_READ)assert((compressed_http?http_at==http_size/2:read_at>=transfer_size/2)&&write_at<=read_at&&read_at-write_at<=(int)atomic_load(&buffer_slots)*49152&&trace_offset==(unsigned)write_at);
        if(success&&compressed_http)assert(atomic_load(&transfer_bytes)==http_size&&http_size<transfer_size);
        if(fault==WRITE_FAIL)assert(write_at==(transfer_size/2/49152)*49152&&trace_offset==(unsigned)write_at);
        if(fault==BUFFER_FAIL||fault==SECOND_BUFFER_FAIL)assert(!read_at&&!write_calls&&!aborts);
        if(fault==READER_START_FAIL||fault==OPEN_FAIL||fault==UI_PREPARE_NO_RENDER)assert(!erased&&!write_calls&&!aborts);
        if(fault==END_FAIL)assert(measured_performance.verify_valid_mask==1);
        if(fault==IDENTITY_FAIL)assert(measured_performance.verify_valid_mask==3);
        if(fault==BOOT_FAIL)assert(measured_performance.verify_valid_mask==7);
        if(fault==HASH_FAIL||fault==WRITE_FAIL||fault==BEGIN_FAIL)assert(!measured_performance.verify_valid_mask);
        if(fault==SUCCESS&&(!compressed_http||transfer_size>49152))assert(early_feedback_seen);
      }
    }
    test_link=LINK_NONE;fault=SUCCESS;write_calls=boot_changes=restarts=0;
    atomic_store(&busy,false);last_offer[0]=0;ota_update_consider(offer);
    assert(ota_update_request());worker(worker_arg);
    assert(!write_calls&&!boot_changes&&!restarts&&!ota_update_busy()&&strstr(message,"所选连接"));
    benchmark_busy=true;assert(!ota_update_request());benchmark_busy=false;
    flash_testing=true;assert(!ota_update_request()&&!ota_update_busy());flash_testing=false;
    atomic_store(&busy,false);last_offer[0]=0;ota_update_consider(offer);assert(ota_update_available());
    char version[32],notes[1025];ota_update_offer_details(version,sizeof(version),notes,sizeof(notes));
    assert(!strcmp(version,"test")&&!notes[0]); // legacy offer has no notes
    cJSON *root=cJSON_Parse(offer),*ota=cJSON_GetObjectItem(cJSON_GetObjectItem(root,"data"),"ota");
    cJSON_AddStringToObject(ota,"notes","修复升级崩溃\n新增更新说明");
    char *json=cJSON_PrintUnformatted(root);ota_update_consider(json);free(json);
    ota_update_offer_details(version,sizeof(version),notes,sizeof(notes));assert(!strcmp(notes,"修复升级崩溃\n新增更新说明"));
    char large[1026];memset(large,'x',1025);large[1025]=0;
    cJSON_ReplaceItemInObject(ota,"notes",cJSON_CreateString(large));
    json=cJSON_PrintUnformatted(root);ota_update_consider(json);free(json);cJSON_Delete(root);
    ota_update_offer_details(version,sizeof(version),notes,sizeof(notes));assert(!notes[0]&&ota_update_available());
    ota_update_consider("{\"data\":{}}");assert(!ota_update_available());
    ota_update_offer_details(version,sizeof(version),notes,sizeof(notes));assert(!version[0]&&!notes[0]);
    // Reboot loses last_offer, but a PC that keeps offering the installed
    // version must not resurrect the badge/button, even with a new offer ID.
    strcpy(running_description.version,"test");last_offer[0]=0;
    ota_update_consider(offer);assert(!ota_update_available());
    assert(!strcmp(message,"已是最新版本")&&!ota_update_request());
    ota_update_offer_details(version,sizeof(version),notes,sizeof(notes));assert(!version[0]&&!notes[0]);
    strcpy(running_description.version,"previous");ota_update_consider(offer);assert(ota_update_available());
    assert(overlap_cases==4);
    if(external_write_bytes){fprintf(stderr,"OTA_INTERNAL_WRITE_REGRESSION externalBytes=%llu\n",external_write_bytes);return 42;}
    strcpy(running_description.version,"previous");
    for(int route=0;route<4;route++)for(int test=RECOVER_ONCE;test<=RECOVER_WAIT;test++) {
        if(test==RECOVER_OPEN&&route!=LINK_WIFI&&route!=3)continue;
        test_link=route==3?LINK_WIFI:(link_kind)route;compressed_http=route==3;
        transfer_size=196625;fault=FLASH_BUFFER_FAIL;recovery_fault=test;
        recovery_failures=range_calls=0;first_resume_offset=-1;recovery_injected=false;atomic_store(&recovery_event_claimed,false);atomic_store(&recovery_event_pending,false);atomic_store(&receiver_thread_id,0);host_epoch=1;radio_ready=true;reconnect_due=0;
        read_at=write_calls=write_at=aborts=boot_changes=restarts=yields=0;
        elapsed_ms=preparation_ack_at=completion_ack_at=backlight_off_at=0;
        allocations=0;internal_buffer=NULL;internal_capacity=0;erased=false;
        last_offer[0]=0;pending_offer.size=0;pending_offer_epoch=0;atomic_store(&busy,false);worker=NULL;
        ResetEvent(prefetch_ready);ResetEvent(receive_complete);
        prepare_http_payload();
        device_snapshots[LINK_WIFI]=cJSON_Parse("{\"rpcOtaResume\":1}");
        snprintf(offer,sizeof(offer),"{\"data\":{\"epochMilliseconds\":1,\"ota\":{\"sha256\":\"0101010101010101010101010101010101010101010101010101010101010101\",\"size\":%d,\"version\":\"test\",\"offerId\":\"11111111111111111111111111111111\"}}}",transfer_size);
        ota_update_consider(offer);assert(ota_update_request());worker(worker_arg);
        bool success=test==RECOVER_ONCE||test==RECOVER_GENERATION||test==RECOVER_OPEN||test==RECOVER_WAIT;
        if(success)assert(restarts==1&&boot_changes==1&&write_at==transfer_size&&ota_update_progress()==100);
        else assert(!restarts&&!boot_changes&&write_at<transfer_size&&!ota_update_busy());
        assert(range_calls>0&&first_resume_offset==(test==RECOVER_OPEN?0:49152));
        if(test==RECOVER_WAIT)assert(elapsed_ms>=30000&&radio_ready);
        if(test==RECOVER_EXHAUST)assert(recovery_failures==(test_link==LINK_WIFI?8:9));
        if(test==RECOVER_REJECT||test==RECOVER_CORRUPT)assert(recovery_failures==1);
        if(test==RECOVER_STOP)assert(atomic_load(&offer_withdrawn));
        if(test==RECOVER_STOP||test==RECOVER_OWNER)assert(range_calls==1&&recovery_failures==1);
        cJSON_Delete(device_snapshots[LINK_WIFI]);device_snapshots[LINK_WIFI]=NULL;
        recovery_fault=0;
    }
    free(http_payload);CloseHandle(prefetch_ready);CloseHandle(receive_complete);
    puts("OTA_RECOVERY_TASK_OK USB_WIFI_BLE resumed offset; raw/zlib stream fallback; disconnect, generation, exhaustion, stop, stale withdrawal, owner change, malformed range and authorization rejection");
    puts("OTA_TASK_OK USB_WIFI_BLE_fault_matrix authenticated_prefetch_before_erase rendered_preparation_required bounded_PSRAM_pool_internal_staging_even_at_38KB_or_allocation_failure receive_complete_before_write_not_99 verification_elapsed_feedback completion_and_failure_paths");return 0;
}
