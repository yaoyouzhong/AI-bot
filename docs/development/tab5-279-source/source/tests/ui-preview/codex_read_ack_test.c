#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "cJSON.h"
#include "../../firmware/main/link_state.h"
#define portMAX_DELAY 0
#define MALLOC_CAP_SPIRAM 1
#define MALLOC_CAP_8BIT 2
#define pdPASS 1
#define HTTP_METHOD_POST 1
#define pdMS_TO_TICKS(ms) (ms)
#define vTaskDeleteWithCaps(p) return
static int device_mutex,held;
static void xSemaphoreTake(int m,int t){(void)m;(void)t;assert(!held);held=1;}
static void xSemaphoreGive(int m){(void)m;assert(held);held=0;}
static int64_t clock_ms;
static int64_t esp_timer_get_time(void){return clock_ms*1000;}
static void vTaskDelay(int ms){assert(!held);clock_ms+=ms;}
typedef struct {char host[16];int port;} device_config;
static device_config device_get_config(void){return (device_config){"192.168.1.2",18765};}
typedef void *esp_http_client_handle_t;
typedef struct {const char *url;int method,timeout_ms;bool disable_auto_redirect;int buffer_size;} esp_http_client_config_t;
static void *esp_http_client_init(const esp_http_client_config_t *o){assert(o->disable_auto_redirect&&o->timeout_ms==8000);return (void *)1;}
static void esp_http_client_cleanup(void *c){(void)c;}
static link_kind selected=LINK_USB;
static link_kind rpc_read_link_select(void){return selected;}
static int response_status=200,calls;
static cJSON *reply(cJSON *r,int *s){
    assert(!held);calls++;*s=response_status;
    assert(!strcmp(cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(r,"kind")),"codex"));
    assert(!strcmp(cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(r,"op")),"read-ack"));
    assert(!strcmp(cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(r,"message")),""));
    assert(!strcmp(cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(r,"session")),"bridge-session"));
    assert(strlen(cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(r,"turnId")))==36);
    assert(!cJSON_GetObjectItemCaseSensitive(r,"pin"));return cJSON_CreateObject();
}
static cJSON *rpc_call(link_kind l,cJSON *r,int *s,char n[33]){(void)n;assert(l==LINK_USB||l==LINK_BLE);return reply(r,s);}
static cJSON *rpc_http_call(void *c,cJSON *r,int *s,char n[33]){(void)c;(void)n;return reply(r,s);}
static void (*queued_fn)(void *);static void *queued_arg;static bool fail_task;
static int xTaskCreateWithCaps(void (*fn)(void *),const char *name,unsigned stack,void *arg,unsigned p,void *h,int caps){
    (void)name;(void)p;(void)h;(void)caps;assert(held&&stack==8192&&!queued_arg);
    if(fail_task)return 0;queued_fn=fn;queued_arg=arg;return pdPASS;
}
#include "codex_read_ack_under_test.inc"
static void finish(void){void *arg=queued_arg;queued_arg=NULL;queued_fn(arg);assert(!held&&!busy);}
int main(void){
    const char *task="11111111-1111-4111-8111-111111111111",*turn="22222222-2222-4222-8222-222222222222";
    const char *a="33333333-3333-4333-8333-333333333333",*b="44444444-4444-4444-8444-444444444444",*c="55555555-5555-4555-8555-555555555555";
    codex_read_acknowledge(task,turn,"","bridge-session");assert(!queued_arg&&!calls);
    codex_read_acknowledge(task,turn,a,"bridge-session");assert(queued_arg&&busy&&!calls);
    codex_read_acknowledge(task,turn,b,"bridge-session");finish();assert(calls==1);
    codex_read_acknowledge(task,turn,a,"bridge-session");assert(!queued_arg);
    selected=LINK_WIFI;response_status=503;codex_read_acknowledge(task,turn,b,"bridge-session");finish();assert(calls==5&&clock_ms==3000);
    fail_task=true;codex_read_acknowledge(task,turn,c,"bridge-session");assert(!busy&&!queued_arg);
    fail_task=false;selected=LINK_BLE;response_status=409;codex_read_acknowledge(task,turn,c,"bridge-session");finish();assert(calls==6);
    puts("CODEX_READ_ACK_WORKER_OK actual worker; USB Wi-Fi BLE; scoped empty-message receipt only; duplicate, busy, bounded retry, rejection and allocation failure");return 0;
}
