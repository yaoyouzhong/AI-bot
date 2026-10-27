#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdatomic.h>
#include "cJSON.h"
#include "../../firmware/main/link_state.h"
#define TRUSTED_HOST_NAME 64
#define TAB5_LOCAL_ART 1
#define MALLOC_CAP_INTERNAL 1
#define MALLOC_CAP_8BIT 2
#define MALLOC_CAP_SPIRAM 4
#define portMAX_DELAY 0
#define pdPASS 1
#define pdMS_TO_TICKS(ms) (ms)
typedef struct{char id[16],host[16];int port;}device_config;
static device_config device_get_config(void){return(device_config){"001122aabbcc","192.0.2.1",18765};}
static unsigned epoch=1,calls,busy_calls,created;static bool changing,fail_task,fail_http;
static unsigned device_host_epoch(void){return epoch;}
static bool device_host_changing(void){return changing;}
static void device_host_identity(char *id,char *name){strcpy(id,"11111111111111111111111111111111");strcpy(name,"fixture");}
static int64_t now=123456;
static int64_t esp_timer_get_time(void){return now*1000+789;}
static void vTaskDelay(unsigned ms){now+=ms;}
static int esp_reset_reason(void){return 3;}
static size_t heap_caps_get_free_size(unsigned caps){return caps*1000;}
static unsigned diagnostic_largest_value=300,diagnostic_largest_calls;
static unsigned camera_viewport_diagnostic_largest(unsigned caps){assert(caps==(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT));diagnostic_largest_calls++;return diagnostic_largest_value;}
typedef struct{char version[32];}app_desc;
static const app_desc *esp_app_get_description(void){static app_desc d={"0.2.250-ui"};return &d;}
static void *device_mutex;static bool wifi_connected=true,ble_connected;
static unsigned ble_connect_count=4,ble_disconnect_reason=0;
static struct{int64_t received_ms[3];}device_links={{-1,123400,123300}};
static void xSemaphoreTake(void *p,unsigned t){(void)p;(void)t;}
static void xSemaphoreGive(void *p){(void)p;}
static unsigned assets_ready(void){return 7;}static unsigned assets_cached(void){return 3;}
volatile unsigned tab5_display_rotate_us,tab5_display_rotate_peak_us,tab5_display_flush_count=42,tab5_display_flush_errors;
volatile unsigned tab5_codex_notice_state,tab5_pc_wakes,tab5_backlight_changes,tab5_lcd_underruns;
volatile int tab5_ui_page=8,tab5_saver_active,tab5_backlight_percent=70;
volatile int tab5_ui_data_age_ms,tab5_ui_task_selected,tab5_ui_session_ready,tab5_ui_voice_check,tab5_ui_voice_age_ms;
volatile int tab5_voice_http_stage,tab5_voice_http_status,tab5_voice_request_ms,tab5_voice_last_seq;
unsigned tab5_ui_age_ms(void){return 9;}
#define formatter(name) static void name(char *s,unsigned n){snprintf(s,n,"1,2,3");}
formatter(camera_preview_diagnostics) formatter(display_diagnostics_format) formatter(photo_diagnostics)
formatter(codex_remote_upload_diagnostics) formatter(camera_preview_driver_error_hex) formatter(ota_update_diagnostics)
formatter(art_orientation_diagnostics) formatter(rpc_failure_diagnostics) formatter(tab5_wifi_numeric_diagnostics)
formatter(tab5_wifi_recovery_diagnostics) formatter(tab5_wifi_http_diagnostics) formatter(tab5_wifi_path_diagnostics)
formatter(tab5_wireless_boot_diagnostics) formatter(wifi_probe_diagnostics) formatter(gallery_diagnostics) formatter(gallery_transfer_diagnostics)
static void (*worker)(void *);static void *argument;
static int xTaskCreate(void (*fn)(void*),const char *name,unsigned stack,void *arg,unsigned priority,void *handle){
    (void)name;(void)priority;(void)handle;assert(stack==8192);if(fail_task)return 0;assert(!argument);worker=fn;argument=arg;created++;return pdPASS;
}
static void vTaskDelete(void *p){assert(!p);}
typedef void *esp_http_client_handle_t;
typedef struct{const char *url;int timeout_ms;bool disable_auto_redirect;}esp_http_client_config_t;
static void *esp_http_client_init(const esp_http_client_config_t *cfg){assert(cfg->disable_auto_redirect&&strstr(cfg->url,"192.0.2.1"));return fail_http?NULL:(void*)1;}
static void esp_http_client_cleanup(void *p){assert(p);}
static cJSON *respond(cJSON *request,int *status){
    calls++;assert(!strcmp(cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(request,"kind")),"runtime-diagnostics"));
    cJSON *s=cJSON_GetObjectItemCaseSensitive(request,"snapshot");assert(s);
    assert(cJSON_GetObjectItemCaseSensitive(s,"uptimeMs")->valuedouble==(double)123456);
    char *wire=cJSON_PrintUnformatted(s);assert(wire&&strlen(wire)<8192&&!strstr(wire,"fixture")&&!strstr(wire,"192.0.2.1"));free(wire);
    if(busy_calls){busy_calls--;*status=0;return NULL;}*status=200;return cJSON_CreateObject();
}
static cJSON *rpc_call(link_kind link,cJSON *r,int *s,char *n){assert(link==LINK_BLE);(void)n;return respond(r,s);}
static cJSON *rpc_http_call(void *http,cJSON *r,int *s,char *n){assert(http);(void)n;return respond(r,s);}
#include "runtime_diagnostics_under_test.inc"
static void finish(void){void *job=argument;argument=NULL;worker(job);assert(!atomic_load(&sending));}
static cJSON *item(cJSON *o,const char *key){return cJSON_GetObjectItemCaseSensitive(o,key);}
static bool remote_ota_hex(const char *s,unsigned n){if(!s||strlen(s)!=n)return false;for(unsigned i=0;i<n;i++)if(!strchr("0123456789abcdef",s[i]))return false;return true;}
#include "../../firmware/main/runtime_diagnostics.inc"
int main(void){
    // The real viewport test verifies scan gating; this checks the snapshot
    // preserves the helper's unknown value without substituting total free bytes.
    for(unsigned i=0;i<3;i++) {
        diagnostic_largest_value=i==1?0:300;unsigned before=diagnostic_largest_calls;
        cJSON *s=runtime_diagnostics_snapshot();assert(s);
        assert(item(s,"internalLargest")->valuedouble==diagnostic_largest_value);
        assert(item(s,"internalFree")->valuedouble==3000);
        assert(diagnostic_largest_calls==before+1);cJSON_Delete(s);
    }
    const char *id="22222222222222222222222222222222",*session="33333333333333333333333333333333";
    assert(runtime_diagnostics_report(id,session,LINK_WIFI));assert(!runtime_diagnostics_report(id,session,LINK_WIFI));finish();assert(calls==1);
    busy_calls=2;assert(runtime_diagnostics_report(id,session,LINK_BLE));finish();assert(calls==4);now=123456;
    assert(runtime_diagnostics_report(id,session,LINK_BLE));epoch++;finish();assert(calls==4);
    changing=true;assert(!runtime_diagnostics_report(id,session,LINK_BLE));changing=false;
    fail_task=true;assert(!runtime_diagnostics_report(id,session,LINK_WIFI)&&!atomic_load(&sending));fail_task=false;
    fail_http=true;assert(runtime_diagnostics_report(id,session,LINK_WIFI));finish();assert(calls==4);fail_http=false;
    cJSON *frame=cJSON_Parse("{\"epochMilliseconds\":1700000000000,\"pcInput\":{\"session\":\"33333333333333333333333333333333\"},\"connectionHealth\":{\"runtimeDiagnosticCommand\":{\"version\":1,\"requestId\":\"22222222222222222222222222222222\",\"transport\":\"Wi-Fi\",\"expiresAt\":1700000010000}}}");assert(frame);
    unsigned before=created;
    runtime_diagnostics_tick(frame,LINK_USB,1000,1000);runtime_diagnostics_tick(frame,LINK_BLE,1000,1000);
    runtime_diagnostics_tick(frame,LINK_WIFI,10000,1000);runtime_diagnostics_tick(frame,LINK_WIFI,999,1000);assert(created==before);
    cJSON *command=item(item(frame,"connectionHealth"),"runtimeDiagnosticCommand");
    cJSON_SetNumberValue(item(command,"expiresAt"),1700000000000.5);runtime_diagnostics_tick(frame,LINK_WIFI,1000,1000);assert(created==before);
    cJSON_SetNumberValue(item(command,"expiresAt"),1700000010000);runtime_diagnostics_tick(frame,LINK_WIFI,1000,1000);assert(created==before+1);finish();
    runtime_diagnostics_tick(frame,LINK_WIFI,1000,1000);assert(created==before+1);cJSON_Delete(frame);
    puts("RUNTIME_FIRMWARE_OK actual snapshot/worker/tick; Wi-Fi/BLE, bounded contention retry, integer uptime, host switch, failure cleanup, stale/channel/expiry/duplicate rejection, unknown largest value kept distinct from free");return 0;
}
