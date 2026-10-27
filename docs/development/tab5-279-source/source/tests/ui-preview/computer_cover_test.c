#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "cJSON.h"
#include "../../firmware/main/computer_remote.h"
#include "../../firmware/main/unlock_history.h"
atomic_uint tab5_unlock_history[2+UNLOCK_HISTORY_COUNT*UNLOCK_HISTORY_WORDS];
atomic_uint tab5_unlock_history_received;
#include "../../firmware/main/link_state.h"
#define portMAX_DELAY 0
#define MALLOC_CAP_SPIRAM 1
#define MALLOC_CAP_8BIT 2
#define pdPASS 1
#define ESP_HTTP_METHOD_POST 1
#define HTTP_METHOD_POST 1
#define vTaskDelete(p) return
static int device_mutex,held;
static void xSemaphoreTake(int m,int wait){(void)m;(void)wait;assert(!held);held=1;}
static void xSemaphoreGive(int m){(void)m;assert(held);held=0;}
static void *heap_caps_calloc(size_t n,size_t size,int caps){(void)caps;return calloc(n,size);}
static int64_t clock_ms=1000,reply_delay;
static int64_t esp_timer_get_time(void){return clock_ms*1000;}
typedef struct {char host[16];int port;} device_config;
static device_config device_get_config(void){return (device_config){"192.168.1.2",18765};}
typedef void *esp_http_client_handle_t;
typedef struct {const char *url;int method,timeout_ms;bool disable_auto_redirect;int buffer_size;} esp_http_client_config_t;
static void *esp_http_client_init(const esp_http_client_config_t *o){assert(o->disable_auto_redirect);return (void *)1;}
static void esp_http_client_cleanup(void *c){(void)c;}
static link_kind selected=LINK_BLE;
static link_kind rpc_link_select(void){return selected;}
static int response_status=200;static const char *response;
static unsigned probes;
static bool expect_history;
static cJSON *reply(cJSON *request,int *status){
    const char *op=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(request,"op"));
    assert(!strcmp(cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(request,"kind")),"computer-control"));
    if(!strcmp(op,"lock-screen"))probes++;
    assert(!cJSON_GetObjectItemCaseSensitive(request,"pin"));
    if(expect_history){const char *history=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(request,"unlockHistory"));assert(history&&!strncmp(history,"1;00000001",10)&&strlen(history)==74);}
    clock_ms+=reply_delay;*status=response_status;return cJSON_Parse(response);
}
static cJSON *rpc_call(link_kind link,cJSON *r,int *s,char n[33]){(void)n;assert(link==selected&&(link==LINK_USB||link==LINK_BLE));return reply(r,s);}
static cJSON *rpc_http_call(void *http,cJSON *r,int *s,char n[33]){(void)http;(void)n;return reply(r,s);}
static int mbedtls_base64_decode(unsigned char *d,size_t cap,size_t *n,const unsigned char *s,size_t len){(void)d;(void)cap;(void)n;(void)s;(void)len;return -1;}
static bool fail_task;
static void check_job_flags(void *argument,bool finished);
static int xTaskCreate(void (*fn)(void *),const char *name,unsigned stack,void *arg,unsigned priority,void *handle){
    (void)name;(void)stack;(void)priority;(void)handle;check_job_flags(arg,false);if(fail_task)return 0;fn(arg);check_job_flags(NULL,true);return pdPASS;
}
#include "computer_remote_under_test.inc"
static void check_job_flags(void *argument,bool finished){
    assert(state);if(finished){assert(!state->busy&&!state->reading);return;}
    computer_job *job=argument;assert(state->busy);
    assert(state->reading==(!strcmp(job->operation,"status")||!strcmp(job->operation,"lock-screen")));
}
int main(void){
    const char *id="11111111-1111-4111-8111-111111111111";
    bool cover=true;
    response="{\"version\":1,\"locked\":true,\"volumeAvailable\":true,\"volume\":42}";
    assert(computer_remote_start(id,"status",0,NULL));assert(state->ready&&state->volume==42);
    assert(!computer_remote_cover_state(id,&cover)&&!cover);
    response="{\"version\":1,\"locked\":true,\"lockScreenCover\":true}";
    assert(computer_remote_start(id,"lock-screen",0,NULL));
    assert(computer_remote_cover_state(id,&cover)&&cover&&state->ready&&state->volume==42&&!state->busy);
    assert(!computer_remote_cover_state("other",&cover)&&!cover);
    clock_ms+=1500;assert(computer_remote_cover_state(id,&cover)&&!cover);
    reply_delay=1500;assert(computer_remote_start(id,"lock-screen",0,NULL));assert(computer_remote_cover_state(id,&cover)&&!cover);reply_delay=0;
    response="{\"version\":1,\"locked\":true}";assert(computer_remote_start(id,"lock-screen",0,NULL));assert(computer_remote_cover_state(id,&cover)&&!cover);
    response="{\"version\":1,\"locked\":false,\"lockScreenCover\":true}";assert(computer_remote_start(id,"lock-screen",0,NULL));assert(computer_remote_cover_state(id,&cover)&&!cover);
    response="{\"error\":\"invalid_operation\"}";response_status=400;
    assert(computer_remote_start(id,"lock-screen",0,NULL));assert(computer_remote_cover_state(id,&cover)&&!cover&&state->ready&&state->volume==42);
    response_status=0;response="null";assert(computer_remote_start(id,"lock-screen",0,NULL));assert(computer_remote_cover_state(id,&cover)&&!cover);
    response_status=200;response="{\"version\":1,\"locked\":true,\"lockScreenCover\":true}";
    selected=LINK_WIFI;assert(computer_remote_start(id,"lock-screen",0,NULL));assert(computer_remote_cover_state(id,&cover)&&cover);
    selected=LINK_NONE;unsigned before=probes;assert(!computer_remote_start(id,"lock-screen",0,NULL)&&probes==before);
    selected=LINK_BLE;fail_task=true;assert(!computer_remote_start(id,"lock-screen",0,NULL)&&!state->busy&&!state->reading);
    fail_task=false;selected=LINK_BLE;response_status=200;reply_delay=0;
    int display=99;
    assert(!computer_remote_display_state("other",&display)&&display==-1);
    const char *values[]={"0","1","2","null","-1","3","1.5","true","\"1\""};
    for(int i=0;i<9;i++) {
        char body[160];snprintf(body,sizeof(body),"{\"version\":1,\"locked\":true,\"displayState\":%s}",values[i]);response=body;
        assert(computer_remote_start(id,"lock-screen",0,NULL));
        assert(computer_remote_display_state(id,&display)&&display==(i<3?i:-1));
    }
    response="{\"version\":1,\"locked\":true,\"displayState\":1}";
    reply_delay=1500;assert(computer_remote_start(id,"lock-screen",0,NULL));assert(computer_remote_display_state(id,&display)&&display==-1);reply_delay=0;
    assert(computer_remote_start(id,"lock-screen",0,NULL));clock_ms+=1500;assert(computer_remote_display_state(id,&display)&&display==-1);
    response="{\"version\":1,\"locked\":false,\"displayState\":1}";assert(computer_remote_start(id,"lock-screen",0,NULL));assert(computer_remote_display_state(id,&display)&&display==-1);
    // The normal UI report does not count as request-bound HID evidence.
    response="{\"version\":1,\"locked\":true,\"displayState\":1}";clock_ms+=1500;
    assert(computer_remote_start(id,"status",0,NULL));assert(computer_remote_screen_state(&display)&&display==1);
    assert(computer_remote_display_state(id,&display)&&display==-1);
    clock_ms+=3999;assert(computer_remote_screen_state(&display)&&display==1);
    clock_ms++;assert(!computer_remote_screen_state(&display)&&display==-1);
    const char *ui_values[]={"0","1","2","null","3","1.5","true","\"1\""};
    for(int i=0;i<8;i++) {
        char body[128];snprintf(body,sizeof(body),"{\"version\":1,\"locked\":true,\"displayState\":%s}",ui_values[i]);response=body;
        assert(computer_remote_start(id,"status",0,NULL));
        assert(computer_remote_screen_state(&display)==(i<3)&&display==(i<3?i:-1));
    }
    response="{\"version\":1,\"locked\":true}";assert(computer_remote_start(id,"status",0,NULL));assert(!computer_remote_screen_state(&display)&&display==-1);
    response="{\"version\":1,\"locked\":true,\"displayState\":0}";reply_delay=4000;
    assert(computer_remote_start(id,"status",0,NULL));assert(!computer_remote_screen_state(&display)&&display==-1);reply_delay=0;
    assert(computer_remote_start(id,"lock-screen",0,NULL));assert(computer_remote_screen_state(&display)&&display==0);
    clock_ms+=1500;assert(computer_remote_screen_state(&display)&&display==0);assert(computer_remote_display_state(id,&display)&&display==-1);
    puts("COMPUTER_SCREEN_CACHE_OK UI_only_4s_including_rpc malformed_legacy_unknown no_new_rpc no_HID_proof_widening");
    for(int channel=0;channel<3;channel++) {
        selected=(link_kind)channel;clock_ms+=10000;response_status=200;reply_delay=0;
        response="{\"version\":1,\"locked\":true,\"displayState\":1}";
        assert(computer_remote_start(id,"status",0,NULL));
        bool locked=false;int64_t sampled=0;assert(computer_remote_lock_state(&locked,&sampled)&&locked);
        int64_t ordinary=state->updated;
        clock_ms+=3000;assert(computer_remote_start(id,"lock-screen",0,NULL));
        assert(state->updated==ordinary&&state->lock_updated==clock_ms);
        clock_ms+=4000;assert(computer_remote_lock_state(&locked,&sampled)&&locked); // Ordinary snapshot is now >6500ms old.
        int64_t lock_sample=sampled;
        response_status=0;response="null";assert(computer_remote_start(id,"status",0,NULL));
        assert(!state->ready&&computer_remote_lock_state(&locked,&sampled)&&locked&&sampled==lock_sample);
        clock_ms+=2499;assert(computer_remote_lock_state(&locked,&sampled)&&locked);
        clock_ms++;assert(!computer_remote_lock_state(&locked,&sampled)&&!locked); // Failure did not renew the sample.
        response_status=200;response="{\"version\":1,\"locked\":false}";
        assert(computer_remote_start(id,"lock-screen",0,NULL));
        assert(computer_remote_lock_state(&locked,&sampled)&&!locked&&!state->ready);
        response="{\"version\":1,\"locked\":true}";clock_ms++;
        assert(computer_remote_start(id,"lock-screen",0,NULL));
        cJSON *older=cJSON_Parse("{\"locked\":false}");observe_lock(older,true,clock_ms-1);cJSON_Delete(older);
        assert(computer_remote_lock_state(&locked,&sampled)&&locked&&sampled==clock_ms);
        response="{\"version\":1,\"locked\":null}";clock_ms++;
        assert(computer_remote_start(id,"lock-screen",0,NULL));
        assert(!computer_remote_lock_state(&locked,&sampled)&&!locked); // Explicit unknown invalidates a positive cache.
        response="{\"version\":1,\"locked\":true}";reply_delay=6500;
        assert(computer_remote_start(id,"lock-screen",0,NULL));reply_delay=0;
        assert(!computer_remote_lock_state(&locked,&sampled)&&!locked); // Age includes RPC time.
    }
    puts("COMMON_LOCK_CACHE_OK USB_WIFI_BLE probe_refreshes_independent_timestamp ordinary_failure_does_not_erase_or_renew 6500ms_expiry unknown_invalidates delayed_reply_no_renew older_sample_no_overwrite");
    unsigned words[UNLOCK_HISTORY_WORDS]={0,8,10000,1200,50,400,800,0x10000,300};unlock_history_append(words);expect_history=true;
    for(int link=0;link<3;link++) {
        selected=(link_kind)link;atomic_store(&tab5_unlock_history_received,0);
        response_status=503;response="null";assert(computer_remote_start(id,"status",0,NULL)&&unlock_history_pending());
        unsigned op=0;int status=0;int64_t started=0;computer_remote_diagnostic(&op,&status,&started);assert(op==1&&status==503&&started==clock_ms);
        response_status=200;response="{\"version\":1,\"locked\":true}";
        assert(computer_remote_start(id,"status",0,NULL)&&!unlock_history_pending());
    }
    puts("UNLOCK_HISTORY_DELIVERY_OK actual_worker_USB_WIFI_BLE_failure_retains_pending_authenticated_status_ack_operation_status_age");
    expect_history=false;
    const char *effects[]={"lock","volume","mute","launch","media"};
    response_status=200;response="{\"ok\":true,\"state\":{\"version\":1,\"locked\":true}}";
    for(int channel=0;channel<3;channel++)for(unsigned op=0;op<sizeof(effects)/sizeof(*effects);op++){
        selected=(link_kind)channel;assert(computer_remote_start(id,effects[op],0,NULL));
        assert(!state->busy&&!state->reading);
    }
    for(int i=0;i<2;i++){
        fail_task=true;assert(!computer_remote_start(id,i?"lock":"status",0,NULL));
        assert(!state->busy&&!state->reading);fail_task=false;
    }
    puts("COMPUTER_BUSY_KIND_OK actual start and every terminal path classify status/lock-screen as read-only; task failure clears busy/read flags");
    free(state);puts("COMPUTER_COVER_OK actual worker: explicit id, bounded freshness, delayed/unknown/legacy/failure skip, ordinary state retained; no hardware input");return 0;
}
