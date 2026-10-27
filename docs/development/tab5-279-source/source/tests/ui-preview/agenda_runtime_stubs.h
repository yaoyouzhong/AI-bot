#pragma once
#include "agenda_core.h"
#include "countdown.h"
#include <stdio.h>
#include <string.h>
#include <time.h>
#include <stdlib.h>
typedef int SemaphoreHandle_t;
typedef int QueueHandle_t;
typedef int nvs_handle_t;
typedef int esp_err_t;
typedef int link_kind;
typedef void *esp_http_client_handle_t;
typedef struct{char host[16];int port;} device_config;
typedef struct{const char *url;int method,timeout_ms;bool disable_auto_redirect;int buffer_size;}esp_http_client_config_t;
#define ESP_OK 0
#define ESP_ERR_INVALID_ARG 2
#define ESP_ERR_NVS_NOT_FOUND 1
#define NVS_READONLY 1
#define NVS_READWRITE 2
#define pdPASS 1
#define portMAX_DELAY 0
#define LINK_NONE -1
#define LINK_USB 0
#define LINK_WIFI 1
#define LINK_BLE 2
#define HTTP_METHOD_POST 1
#define pdMS_TO_TICKS(x) (x)
#define MALLOC_CAP_SPIRAM 1
#define MALLOC_CAP_8BIT 2
static bool fail_allocation,fail_mutex,fail_queue,fail_task;
static unsigned allocation_calls,allocation_frees,allocation_caps,task_calls,mutex_deletes,queue_deletes;
static size_t allocation_bytes;
static void *tracked_allocation;
static void *heap_caps_calloc(size_t n,size_t size,unsigned caps){allocation_calls++;allocation_bytes=n*size;allocation_caps=caps;if(fail_allocation)return NULL;assert(!tracked_allocation);tracked_allocation=calloc(n,size);return tracked_allocation;}
static void agenda_test_free(void *p){if(p&&p==tracked_allocation){allocation_frees++;tracked_allocation=NULL;}free(p);}
#define free agenda_test_free
static void vSemaphoreDelete(int h){assert(h);mutex_deletes++;}
static void vQueueDelete(int h){assert(h);queue_deletes++;}
static int xSemaphoreTake(int a,int b){(void)a;(void)b;return 1;}
static int xSemaphoreGive(int a){(void)a;return 1;}
static int xSemaphoreCreateMutex(void){return fail_mutex?0:1;}
static int xQueueCreate(int a,size_t b){(void)a;(void)b;return fail_queue?0:1;}
static int xQueueSend(int a,const void *b,int c){(void)a;(void)b;(void)c;return 1;}
static int xQueueReceive(int a,void *b,int c){(void)a;(void)b;(void)c;return 0;}
static int xTaskCreate(void (*fn)(void*),const char *name,int stack,void *arg,int priority,void *handle){(void)fn;(void)name;(void)stack;(void)arg;(void)priority;(void)handle;task_calls++;return fail_task?0:1;}
static void vTaskDelay(int ms){(void)ms;}
static void vTaskDelete(void *task){(void)task;}
static int64_t test_clock=1792075800LL;
static void esp_fill_random(void *out,size_t length){static unsigned seed=1;unsigned char *bytes=out;for(size_t n=0;n<length;n++)bytes[n]=(unsigned char)(seed++);}
static int64_t esp_timer_get_time(void){return test_clock*1000000;}
static bool test_time_valid=true;
static bool gallery_clock_local(int64_t ms,struct tm *out){(void)ms;time_t t=(time_t)test_clock;return test_time_valid&&gmtime_s(out,&t)==0;}
static agenda_document disk,stage,host;
static agenda_workday_calendar calendar_disk,calendar_stage;
static bool calendar_present,calendar_staged;
static bool disk_present,fail_write;
static uint8_t local_disk[8192],local_stage[8192];static size_t local_size,local_stage_size;static bool local_staged;
static void test_commit_document(const void *value);
static int nvs_open(const char *ns,int mode,nvs_handle_t *h){(void)ns;(void)mode;*h=1;return 0;}
static int nvs_get_blob(int h,const char *key,void *out,size_t *size){(void)h;if(!strcmp(key,"agenda_days_v1")){if(!calendar_present)return ESP_ERR_NVS_NOT_FOUND;if(*size<sizeof(calendar_disk))return 2;memcpy(out,&calendar_disk,sizeof(calendar_disk));*size=sizeof(calendar_disk);return 0;}if(!strcmp(key,"agenda_v3")&&local_size){if(*size<local_size)return 2;memcpy(out,local_disk,local_size);*size=local_size;return 0;}if(!disk_present)return ESP_ERR_NVS_NOT_FOUND;if(*size<sizeof(disk))return 2;memcpy(out,&disk,sizeof(disk));*size=sizeof(disk);return 0;}
static int nvs_set_blob(int h,const char *key,const void *value,size_t size){(void)h;if(fail_write)return 2;calendar_staged=!strcmp(key,"agenda_days_v1");local_staged=!strcmp(key,"agenda_v3")&&size!=sizeof(stage);if(calendar_staged){if(size!=sizeof(calendar_stage))return 2;memcpy(&calendar_stage,value,size);}else if(local_staged){if(size>sizeof(local_stage))return 2;memcpy(local_stage,value,size);local_stage_size=size;}else{if(size!=sizeof(stage))return 2;memcpy(&stage,value,size);}return 0;}
static int nvs_commit(int h){(void)h;if(fail_write)return 2;if(calendar_staged){calendar_disk=calendar_stage;calendar_present=true;}else if(local_staged){memcpy(local_disk,local_stage,local_stage_size);local_size=local_stage_size;test_commit_document(local_stage);disk_present=true;}else{disk=stage;disk_present=true;}return 0;}
static void nvs_close(int h){(void)h;}
static device_config device_get_config(void){return (device_config){"127.0.0.1",8765};}
static link_kind test_link=LINK_USB;
static link_kind rpc_link_select(void){return test_link;}
static int response_status=200;
static bool host_support_edits=true;
static bool host_omit_device_ack;
static bool host_wrong_delete_ack;
static bool host_support_delete=true;
static bool host_support_retention=true;
static const cJSON *host_workday_years;
static cJSON *host_json(void) {
    cJSON *root=cJSON_CreateObject();cJSON_AddNumberToObject(root,"version",1);cJSON_AddStringToObject(root,"revision",host.revision);cJSON *items=cJSON_AddArrayToObject(root,"items");
    for(unsigned n=0;n<host.count;n++){const agenda_item *i=&host.items[n];cJSON *o=cJSON_CreateObject();cJSON_AddItemToArray(items,o);cJSON_AddStringToObject(o,"id",i->id);cJSON_AddStringToObject(o,"edit",i->edit);cJSON_AddStringToObject(o,"title",i->title);cJSON_AddNumberToObject(o,"due",i->due);cJSON_AddNumberToObject(o,"repeat",i->repeat);cJSON_AddNumberToObject(o,"lead",i->lead);cJSON_AddNumberToObject(o,"done",i->done);cJSON_AddNumberToObject(o,"snoozeOccurrence",i->snooze_occurrence);cJSON_AddNumberToObject(o,"snoozeUntil",i->snooze_until);}
    for(unsigned n=0;n<host.count;n++)cJSON_AddNumberToObject(cJSON_GetArrayItem(items,n),"end",host.items[n].end);
    if(host_support_edits)cJSON_AddNumberToObject(root,"editSupport",1);
    if(host_support_retention)cJSON_AddNumberToObject(root,"retentionSupport",2);
    if(host_workday_years)cJSON_AddItemToObject(root,"workdayYears",cJSON_Duplicate(host_workday_years,true));
    if(host_omit_device_ack)cJSON_DeleteItemFromArray(items,cJSON_GetArraySize(items)-1);
    return root;
}
static cJSON *rpc_call(link_kind link,cJSON *request,int *status,char nonce[33]) {
    (void)link;(void)nonce;*status=response_status;if(*status!=200)return NULL;
    const cJSON *op=cJSON_GetObjectItemCaseSensitive(request,"op");
    if(cJSON_IsString(op)&&!strcmp(op->valuestring,"delete")) {
        const char *id=cJSON_GetObjectItemCaseSensitive(request,"id")->valuestring,*edit=cJSON_GetObjectItemCaseSensitive(request,"edit")->valuestring;
        for(unsigned n=0;n<host.count;n++)if(!strcmp(host.items[n].id,id)) {
            if(strcmp(host.items[n].edit,edit)){*status=409;return NULL;}
            memmove(&host.items[n],&host.items[n+1],(host.count-n-1)*sizeof(host.items[0]));memset(&host.items[--host.count],0,sizeof(host.items[0]));break;
        }
        cJSON *reply=cJSON_CreateObject(),*removed=cJSON_AddObjectToObject(reply,"removed");cJSON_AddNumberToObject(reply,"version",1);cJSON_AddStringToObject(removed,"id",id);cJSON_AddStringToObject(removed,"edit",host_wrong_delete_ack?"wrong":edit);return reply;
    }
    if(host_support_delete) {
        const cJSON *d=NULL;cJSON_ArrayForEach(d,cJSON_GetObjectItemCaseSensitive(request,"deletions")) {
            const char *id=cJSON_GetObjectItemCaseSensitive(d,"id")->valuestring,*edit=cJSON_GetObjectItemCaseSensitive(d,"edit")->valuestring,*base=cJSON_GetObjectItemCaseSensitive(d,"baseEdit")->valuestring;
            for(unsigned n=0;n<host.count;n++)if(!strcmp(host.items[n].id,id)) {
                if(strcmp(host.items[n].edit,edit)&&strcmp(host.items[n].edit,base)){*status=409;return NULL;}
                memmove(&host.items[n],&host.items[n+1],(host.count-n-1)*sizeof(host.items[0]));memset(&host.items[--host.count],0,sizeof(host.items[0]));break;
            }
        }
    }
    if(host_support_edits) {
        const cJSON *change=NULL;cJSON_ArrayForEach(change,cJSON_GetObjectItemCaseSensitive(request,"edits")) {
            cJSON *value=cJSON_GetObjectItemCaseSensitive(change,"item");unsigned index=host.count;const char *id=cJSON_GetObjectItemCaseSensitive(value,"id")->valuestring;
            for(unsigned n=0;n<host.count;n++)if(!strcmp(host.items[n].id,id)){index=n;break;}
            if(index==host.count&&host.count==AGENDA_LIMIT){*status=409;return NULL;}
            cJSON *wrapper=cJSON_CreateObject(),*items=cJSON_AddArrayToObject(wrapper,"items");cJSON_AddNumberToObject(wrapper,"version",1);cJSON_AddStringToObject(wrapper,"revision",host.revision);cJSON_AddItemToArray(items,cJSON_Duplicate(value,true));agenda_document parsed;
            assert(agenda_parse(wrapper,&parsed));host.items[index]=parsed.items[0];if(index==host.count)host.count++;cJSON_Delete(wrapper);
        }
    }
    const cJSON *a=NULL;cJSON_ArrayForEach(a,cJSON_GetObjectItemCaseSensitive(request,"actions")) {
        for(unsigned n=0;n<host.count;n++) {agenda_item *i=&host.items[n];if(strcmp(i->id,cJSON_GetObjectItemCaseSensitive(a,"id")->valuestring)||strcmp(i->edit,cJSON_GetObjectItemCaseSensitive(a,"edit")->valuestring))continue;
            cJSON *until=cJSON_GetObjectItemCaseSensitive(a,"until");agenda_apply(i,cJSON_GetObjectItemCaseSensitive(a,"occurrence")->valuedouble,!strcmp(cJSON_GetObjectItemCaseSensitive(a,"op")->valuestring,"complete"),until?until->valuedouble:0);i->dirty=0;}
    }
    cJSON *reply=host_json();if(host_support_delete){cJSON_AddNumberToObject(reply,"deleteSupport",1);cJSON *receipts=cJSON_AddArrayToObject(reply,"deleted");const cJSON *d=NULL;cJSON_ArrayForEach(d,cJSON_GetObjectItemCaseSensitive(request,"deletions")){cJSON *receipt=cJSON_Duplicate(d,true);if(host_wrong_delete_ack){cJSON_DeleteItemFromObjectCaseSensitive(receipt,"edit");cJSON_AddStringToObject(receipt,"edit","wrong");}cJSON_AddItemToArray(receipts,receipt);}}return reply;
}
static void *esp_http_client_init(const esp_http_client_config_t *options){(void)options;return (void*)1;}
static void esp_http_client_cleanup(void *client){(void)client;}
static cJSON *rpc_http_call(void *client,cJSON *request,int *status,char nonce[33]){(void)client;return rpc_call(1,request,status,nonce);}
