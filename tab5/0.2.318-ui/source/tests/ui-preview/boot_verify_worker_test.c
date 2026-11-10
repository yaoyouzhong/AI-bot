#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdatomic.h>
#include "cJSON.h"

#undef assert
#define assert(value) do {if(!(value)){fprintf(stderr,"BOOT_WORKER_ASSERT %s:%d: %s\n",__FILE__,__LINE__,#value);exit(1);}}while(0)
typedef enum {LINK_USB,LINK_WIFI,LINK_BLE} link_kind;
typedef struct {char id[16],host[16];int port;} device_config;
typedef struct {char label[16];unsigned address;} esp_partition_t;
typedef struct {uint8_t app_elf_sha256[32];char version[32];} esp_app_desc_t;
typedef int esp_ota_img_states_t;
typedef int esp_err_t;
#define ESP_OTA_IMG_UNDEFINED 0
#define ESP_ERR_NOT_FOUND -1
#define pdPASS 1
#define pdMS_TO_TICKS(ms) (ms)
static int64_t clock_ms=1000;
static int64_t clock_tail_us;
static bool inspect_uptime;
static const char *wire_sample_path;
static int64_t esp_timer_get_time(void){return clock_ms*1000+clock_tail_us;}
static void vTaskDelay(unsigned ms){clock_ms+=ms;}
static unsigned tab5_ui_age_ms(void){return 5;}
volatile unsigned tab5_display_flush_count=100,tab5_display_flush_errors=0;
static device_config device_get_config(void){return (device_config){"aabbccddeeff","192.168.1.2",18765};}
static const esp_partition_t *esp_ota_get_running_partition(void){static esp_partition_t p={"ota_1",0x680000};return &p;}
static int esp_ota_get_state_partition(const esp_partition_t *p,esp_ota_img_states_t *state){(void)p;*state=2;return 0;}
static const esp_app_desc_t *esp_app_get_description(void){static esp_app_desc_t d={{0},"test-ui"};return &d;}
typedef void *esp_http_client_handle_t;
typedef struct {const char *url;int timeout_ms;bool disable_auto_redirect;} esp_http_client_config_t;
static void *esp_http_client_init(const esp_http_client_config_t *c){assert(c->disable_auto_redirect);return (void *)1;}
static void esp_http_client_cleanup(void *c){(void)c;}
static unsigned calls,successes,busy_calls;
static int64_t occupied_until;
static bool simulate_timeout,fail_task;
static int response_status=200;
#include "rpc_timeout_under_test.inc"
static cJSON *respond(cJSON *request,int *status){
    calls++;
    assert(!strcmp(cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(request,"kind")),"boot-verify"));
    assert(cJSON_IsObject(cJSON_GetObjectItemCaseSensitive(request,"hello")));
    assert(cJSON_IsObject(cJSON_GetObjectItemCaseSensitive(request,"diagnostic")));
    cJSON *hello=cJSON_GetObjectItemCaseSensitive(request,"hello");
    if(inspect_uptime) {
        char *wire=cJSON_PrintUnformatted(hello);assert(wire);
        if(wire_sample_path){FILE *file=fopen(wire_sample_path,"ab");assert(file);fputs(wire,file);fputc('\n',file);fclose(file);}
        double uptime=cJSON_GetObjectItemCaseSensitive(hello,"uptimeMs")->valuedouble;
        if(uptime!=(double)clock_ms){fprintf(stderr,"BOOT_UPTIME_FRACTIONAL_WIRE actual=%.17g expected_integer=%lld\n",uptime,(long long)clock_ms);free(wire);exit(1);}
        free(wire);
    }
    assert(cJSON_GetObjectItemCaseSensitive(hello,"uptimeMs")->valuedouble==clock_ms);
    if(clock_ms<occupied_until){*status=0;return NULL;}
    if(busy_calls){busy_calls--;if(simulate_timeout)clock_ms+=rpc_exchange_timeout("boot-verify",false);*status=0;return NULL;}
    *status=response_status;if(response_status==200)successes++;return cJSON_CreateObject();
}
static cJSON *rpc_call(link_kind link,cJSON *request,int *status,char nonce[33]){assert(link==LINK_BLE);(void)nonce;return respond(request,status);}
static cJSON *rpc_http_call(void *client,cJSON *request,int *status,char nonce[33]){(void)client;(void)nonce;return respond(request,status);}
static void (*queued_worker)(void *);static void *queued_job;
static int xTaskCreate(void (*fn)(void *),const char *name,unsigned stack,void *arg,unsigned priority,void *handle){
    (void)name;(void)stack;(void)priority;(void)handle;assert(!queued_job);if(fail_task)return 0;queued_worker=fn;queued_job=arg;return pdPASS;
}
static void vTaskDelete(void *p){(void)p;}
#include "boot_verify_worker_under_test.inc"
static void finish(void){void *job=queued_job;queued_job=NULL;queued_worker(job);assert(!atomic_load(&boot_verify_sending));}
int main(void){
    const char *id="11111111111111111111111111111111",*session="22222222222222222222222222222222";
    assert(boot_verify_report(id,session,LINK_BLE));finish();assert(calls==1&&successes==1);
    calls=successes=0;busy_calls=1;
    assert(boot_verify_report(id,session,LINK_BLE));finish();
    printf("BOOT_WORKER_CONTENTION calls=%u successes=%u busyRemaining=%u\n",calls,successes,busy_calls);
    if(successes!=1){puts("BOOT_WORKER_CONTENTION_FAILED: a transient occupied BLE mailbox loses the one-shot report");return 1;}
    assert(calls==2);
    calls=successes=0;occupied_until=clock_ms+500;
    assert(boot_verify_report(id,session,LINK_BLE));finish();assert(calls==3&&successes==1);
    calls=successes=0;occupied_until=clock_ms+2000;
    assert(boot_verify_report(id,session,LINK_BLE));finish();assert(calls==9&&successes==1);
    occupied_until=0;
    calls=successes=0;busy_calls=10;simulate_timeout=true;int64_t began=clock_ms;
    assert(boot_verify_report(id,session,LINK_BLE));assert(!boot_verify_report(id,session,LINK_BLE));finish();
    assert(calls==3&&successes==0&&clock_ms-began==6500);
    busy_calls=100;simulate_timeout=false;calls=successes=0;began=clock_ms;
    assert(boot_verify_report(id,session,LINK_BLE));finish();assert(calls==16&&successes==0&&clock_ms-began==3750);
    simulate_timeout=false;busy_calls=0;response_status=409;calls=0;
    assert(boot_verify_report(id,session,LINK_BLE));finish();assert(calls==1);
    response_status=200;calls=successes=0;
    assert(boot_verify_report(id,session,LINK_WIFI));finish();assert(calls==1&&successes==1);
    fail_task=true;assert(!boot_verify_report(id,session,LINK_BLE));assert(!atomic_load(&boot_verify_sending)&&!queued_job);
    assert(!boot_verify_report("invalid",session,LINK_BLE));
    assert(rpc_exchange_timeout("boot-verify",false)==2000);
    assert(rpc_exchange_timeout("gallery",false)==8000&&rpc_exchange_timeout("ota",false)==10000);
    assert(rpc_exchange_timeout("benchmark",true)==5000&&rpc_exchange_timeout("unlock-pin",false)==75000);
    assert(rpc_exchange_timeout(NULL,false)==75000);
    fail_task=false;inspect_uptime=true;wire_sample_path=getenv("TAB5_BOOT_UPTIME_WIRE");
    const int tails[]={830,1,83,500,999,0};
    for(unsigned i=0;i<sizeof(tails)/sizeof(tails[0]);i++) {
        clock_ms=562752+(int64_t)i*1200;clock_tail_us=tails[i];calls=successes=0;
        assert(boot_verify_report(id,session,LINK_BLE));finish();assert(calls==1&&successes==1);
    }
    clock_ms=570000;clock_tail_us=830;busy_calls=1;calls=successes=0;
    assert(boot_verify_report(id,session,LINK_BLE));finish();assert(calls==2&&successes==1);
    clock_ms=571450;calls=successes=0;
    assert(boot_verify_report(id,session,LINK_WIFI));finish();assert(calls==1&&successes==1);
    puts("BOOT_WORKER_UPTIME_WIRE_OK actual cJSON macro and worker; fractional microsecond clock tails; integer milliseconds on first attempt and retry; serialized samples available for unchanged bridge validation");
    puts("BOOT_WORKER_CONTENTION_OK actual worker; 500/2000ms occupied mailbox recovery; bounded timeout/attempts; refreshed samples; rejection/no overlap/allocation; unchanged Wi-Fi and unrelated RPC limits");return 0;
}
