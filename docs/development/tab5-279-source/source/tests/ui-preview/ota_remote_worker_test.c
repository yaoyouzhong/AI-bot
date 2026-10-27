#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdatomic.h>
#include "cJSON.h"
#include "../../firmware/main/link_state.h"
static bool fail_alloc,fail_task;static unsigned live,http_clients,rpc_calls;
static int reply_status;static bool reply_commit,reply_accepted;
typedef struct {char host[16];int port;} device_config;
static device_config device_get_config(void){return (device_config){"192.168.1.2",18765};}
typedef void *esp_http_client_handle_t;
typedef struct {const char *url;int timeout_ms;bool disable_auto_redirect;} esp_http_client_config_t;
static void *esp_http_client_init(const esp_http_client_config_t *options){assert(!strcmp(options->url,"http://192.168.1.2:18765/tab5/v1/rpc")&&options->timeout_ms==6000&&options->disable_auto_redirect);http_clients++;return (void *)1;}
static void esp_http_client_cleanup(void *client){assert(client==(void *)1&&http_clients==1);http_clients--;}
static cJSON *reply(cJSON *request,int *status){assert(cJSON_IsString(cJSON_GetObjectItemCaseSensitive(request,"requestId")));rpc_calls++;*status=reply_status;cJSON *body=cJSON_CreateObject();cJSON_AddBoolToObject(body,"commit",reply_commit);cJSON_AddBoolToObject(body,"accepted",reply_accepted);return body;}
static cJSON *rpc_call(link_kind link,cJSON *request,int *status,char nonce[33]){(void)nonce;assert(link==LINK_USB||link==LINK_BLE);return reply(request,status);}
static cJSON *rpc_http_call(void *client,cJSON *request,int *status,char nonce[33]){(void)nonce;assert(client==(void *)1);return reply(request,status);}
static void *job_malloc(size_t n){if(fail_alloc)return NULL;live++;return malloc(n);}
static void job_free(void *p){if(p){assert(live);live--;free(p);}}
#define pdPASS 1
#define vTaskDelete(p) return
static int xTaskCreate(void (*fn)(void *),const char *name,unsigned stack,void *arg,unsigned priority,void *handle){(void)handle;assert(!strcmp(name,"ota_control")&&stack==8192&&priority==3);if(fail_task)return 0;fn(arg);return 1;}
#define malloc job_malloc
#define free job_free
#include "ota_remote_under_test.inc"
#undef malloc
#undef free
int main(void) {
    for(int link=0;link<3;link++)for(int mode=0;mode<5;mode++) {
        reply_status=mode==3?409:mode==4?401:200;reply_commit=mode==0;reply_accepted=mode==1;
        cJSON *request=cJSON_Parse("{\"requestId\":\"11111111111111111111111111111111\"}");assert(ota_remote_report(request,link));
        cJSON *blocked=cJSON_Parse("{\"requestId\":\"22222222222222222222222222222222\"}");assert(!ota_remote_report(blocked,link));cJSON_Delete(blocked);
        char id[33];bool commit,confirmed;assert(ota_remote_result(id,&commit,&confirmed));
        assert(!strcmp(id,"11111111111111111111111111111111")&&commit==(mode==0)&&confirmed==(mode==0||mode==1||mode==3));
        assert(!ota_remote_result(id,&commit,&confirmed)&&!live&&!http_clients);
    }
    for(int mode=0;mode<3;mode++) {
        cJSON *request=cJSON_Parse("{\"requestId\":\"11111111111111111111111111111111\"}");fail_alloc=mode==0;fail_task=mode==1;
        assert(!ota_remote_report(request,mode==2?LINK_NONE:LINK_USB));cJSON_Delete(request);assert(!live&&!atomic_load(&sending));
    }
    assert(rpc_calls==15);puts("OTA_REMOTE_WORKER_OK production worker USB/Wi-Fi/BLE; commit/auth rejection; single unconsumed result; allocation/task failure retains caller ownership; zero leaked clients/jobs");
}
