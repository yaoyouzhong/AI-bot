#include "computer_remote.h"
#include "device.h"
#include "rpc_link.h"
#include "esp_http_client.h"
#include "esp_timer.h"
#include "esp_heap_caps.h"
#include "mbedtls/base64.h"
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include "oneclick_remote.inc"
#include "unlock_history.h"

typedef struct {char id[37],operation[16],target[37],icon_revision[65];int value;link_kind link;} computer_job;
static computer_remote_state *state;
static char cover_request[37];
static bool cover_confirmed;
static int cover_display=-1;
static int64_t cover_started;
static bool state_init(void){if(!state)state=heap_caps_calloc(1,sizeof(*state),MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);return state!=NULL;}
static int display_value(cJSON *body) {
    cJSON *value=cJSON_GetObjectItemCaseSensitive(body,"displayState");
    return cJSON_IsNumber(value)&&value->valuedouble==value->valueint&&value->valueint>=0&&value->valueint<=2?value->valueint:-1;
}
static bool flag(cJSON *o,const char *key){return cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(o,key));}
static const char *string(cJSON *o,const char *key){cJSON *v=cJSON_GetObjectItemCaseSensitive(o,key);return cJSON_IsString(v)?v->valuestring:"";}
void computer_remote_get(computer_remote_state *out){xSemaphoreTake(device_mutex,portMAX_DELAY);if(state_init())*out=*state;else memset(out,0,sizeof(*out));xSemaphoreGive(device_mutex);}
bool computer_remote_busy(void){xSemaphoreTake(device_mutex,portMAX_DELAY);bool busy=state&&state->busy;xSemaphoreGive(device_mutex);return busy;}
bool computer_remote_oneclick_enabled(void){xSemaphoreTake(device_mutex,portMAX_DELAY);bool enabled=state&&state->ready&&state->one_click_unlock;xSemaphoreGive(device_mutex);return enabled;}
bool computer_remote_lock_state(bool *locked,int64_t *updated) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);int64_t now=esp_timer_get_time()/1000;
    bool valid=state&&state->lock_known&&now>=state->lock_updated&&now-state->lock_updated<6500;
    *locked=valid&&state->locked;*updated=state?state->lock_updated:0;xSemaphoreGive(device_mutex);return valid;
}
// Both authenticated status and lock-screen replies describe the same paired
// PC. A failed ordinary read must not erase an independently fresh lock sample.
// Unknown/malformed lock data invalidates it; delayed replies cannot renew or
// overwrite a newer sample. Never substitute response-receive time for started.
static void observe_lock(cJSON *body,bool valid,int64_t started) {
    if(!valid||started<state->lock_updated)return;
    cJSON *locked=cJSON_GetObjectItemCaseSensitive(body,"locked");
    state->lock_known=cJSON_IsBool(locked);state->locked=cJSON_IsTrue(locked);
    state->lock_updated=started;
}
void computer_remote_diagnostic(unsigned *operation,int *status,int64_t *started) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    *operation=state?state->diagnostic_operation:0;*status=state?state->diagnostic_status:0;*started=state?state->diagnostic_started:0;
    xSemaphoreGive(device_mutex);
}
bool computer_remote_screen_state(int *display) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);int64_t now=esp_timer_get_time()/1000;
    *display=state&&state->ready&&now>=state->display_updated&&now-state->display_updated<4000?state->display_state:-1;
    xSemaphoreGive(device_mutex);return *display>=0;
}
bool computer_remote_cover_state(const char *id,bool *cover) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);int64_t now=esp_timer_get_time()/1000;
    bool complete=id&&id[0]&&!strcmp(id,cover_request);
    *cover=complete&&cover_confirmed&&now>=cover_started&&now-cover_started<1500;
    xSemaphoreGive(device_mutex);return complete;
}
bool computer_remote_display_state(const char *id,int *display) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);int64_t now=esp_timer_get_time()/1000;
    bool complete=id&&id[0]&&!strcmp(id,cover_request);
    *display=complete&&now>=cover_started&&now-cover_started<1500?cover_display:-1;
    xSemaphoreGive(device_mutex);return complete;
}
static void run(void *argument) {
    computer_job *job=argument;int status=0;char nonce[33];
    int64_t started=esp_timer_get_time()/1000;
    char history[UNLOCK_HISTORY_SIZE];unsigned history_generation=0;
    cJSON *request=cJSON_CreateObject(),*reply=NULL;
    if(request) {
        cJSON_AddStringToObject(request,"kind","computer-control");cJSON_AddStringToObject(request,"op",job->operation);
        cJSON_AddStringToObject(request,"requestId",job->id);
        cJSON_AddStringToObject(request,"iconRevision",job->icon_revision);
        if(!strcmp(job->operation,"status")) {
            history_generation=unlock_history_snapshot(history);
            if(history_generation)cJSON_AddStringToObject(request,"unlockHistory",history);
        }
        if(!strcmp(job->operation,"volume"))cJSON_AddNumberToObject(request,"volume",job->value);
        if(!strcmp(job->operation,"mute"))cJSON_AddBoolToObject(request,"muted",job->value!=0);
        if(!strcmp(job->operation,"launch"))cJSON_AddStringToObject(request,"launcherId",job->target);
        if(!strcmp(job->operation,"media"))cJSON_AddStringToObject(request,"action",job->target);
        if(job->link==LINK_WIFI) {
            device_config cfg=device_get_config();char url[96];snprintf(url,sizeof(url),"http://%s:%d/tab5/v1/rpc",cfg.host,cfg.port);
            esp_http_client_config_t options={.url=url,.method=HTTP_METHOD_POST,.timeout_ms=8000,.disable_auto_redirect=true,.buffer_size=2048};
            esp_http_client_handle_t client=esp_http_client_init(&options);
            if(client){reply=rpc_http_call(client,request,&status,nonce);esp_http_client_cleanup(client);}
        }else reply=rpc_call(job->link,request,&status,nonce);
    }
    bool probing=!strcmp(job->operation,"lock-screen");
    bool reading=probing||!strcmp(job->operation,"status");
    cJSON *body=reading?reply:cJSON_GetObjectItemCaseSensitive(reply,"state");
    cJSON *version=cJSON_GetObjectItemCaseSensitive(body,"version");
    bool valid=status==200&&cJSON_IsNumber(version)&&version->valueint==1&&(reading||flag(reply,"ok"));
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    observe_lock(body,valid,started);
    if(reading){state->diagnostic_operation=probing?2:1;state->diagnostic_status=status;state->diagnostic_started=started;}
    if(valid&&history_generation)atomic_store(&tab5_unlock_history_received,history_generation);
    if(probing) {
        strcpy(cover_request,job->id);cover_started=started;
        cover_confirmed=valid&&flag(body,"locked")&&flag(body,"lockScreenCover");
        cover_display=valid&&flag(body,"locked")?display_value(body):-1;
        state->display_state=cover_display;state->display_updated=started;
        state->busy=false;state->reading=false;state->generation++;
        xSemaphoreGive(device_mutex);cJSON_Delete(reply);cJSON_Delete(request);free(job);vTaskDelete(NULL);return;
    }
    state->status=status;state->ready=valid;state->busy=false;state->reading=false;state->generation++;
    if(valid) {
        state->display_state=display_value(body);state->display_updated=started;
        state->one_click_unlock=flag(body,"oneClickUnlock");
        state->volume_available=flag(body,"volumeAvailable");state->muted=flag(body,"muted");state->can_lock=flag(body,"canLock");
        state->media_available=flag(body,"mediaAvailable");state->can_play_pause=flag(body,"canPlayPause");
        state->can_previous=flag(body,"canPrevious");state->can_next=flag(body,"canNext");state->playing=flag(body,"playing");
        cJSON *volume=cJSON_GetObjectItemCaseSensitive(body,"volume");state->volume=cJSON_IsNumber(volume)?volume->valueint:0;
        if(state->volume<0||state->volume>100){state->volume_available=false;state->volume=0;}
        const char *revision=string(body,"iconRevision");
        bool same_icons=strlen(revision)==64&&!strcmp(revision,state->icon_revision);
        state->count=0;
        if(!same_icons)memset(state->launchers,0,sizeof(state->launchers));
        snprintf(state->icon_revision,sizeof(state->icon_revision),"%s",strlen(revision)==64?revision:"");
        cJSON *launchers=cJSON_GetObjectItemCaseSensitive(body,"launchers");
        for(int i=0;cJSON_IsArray(launchers)&&i<cJSON_GetArraySize(launchers)&&state->count<4;i++) {
            cJSON *entry=cJSON_GetArrayItem(launchers,i);const char *id=string(entry,"id"),*name=string(entry,"name");
            if(strlen(id)!=36||strlen(name)>96||!name[0])continue;
            computer_launcher *slot=&state->launchers[state->count++];
            if(strcmp(slot->id,id))memset(slot,0,sizeof(*slot));
            strcpy(slot->id,id);strcpy(slot->name,name);slot->folder=!strcmp(string(entry,"kind"),"folder");
            const char *encoded=string(entry,"icon");
            if(encoded[0]) {
                size_t decoded=0;slot->has_icon=false;
                if(strlen(encoded)==5464&&mbedtls_base64_decode(slot->icon,sizeof(slot->icon),&decoded,(const unsigned char *)encoded,5464)==0&&decoded==sizeof(slot->icon))slot->has_icon=true;
            }
        }
        for(int i=state->count;i<4;i++)memset(&state->launchers[i],0,sizeof(state->launchers[i]));
        state->updated=esp_timer_get_time()/1000;
        snprintf(state->message,sizeof(state->message),"%s",reading?"电脑已连接":!strcmp(job->operation,"lock")?"已请求锁定电脑":!strcmp(job->operation,"launch")?"已请求打开":"已完成");
    }else {
        const char *error=string(reply,"error");
        snprintf(state->message,sizeof(state->message),"%s",!strcmp(error,"media_unavailable")?"当前播放器不支持控制":status==400||status==404?"请更新电脑桥接或检查启动入口":status==409?"操作未完成，请检查电脑状态":reading?"无法读取电脑状态，请检查连接":"未确认操作结果，请检查电脑");
    }
    xSemaphoreGive(device_mutex);cJSON_Delete(reply);cJSON_Delete(request);free(job);vTaskDelete(NULL);
}
bool computer_remote_start(const char *id,const char *operation,int value,const char *target) {
    if(!id||strlen(id)!=36||!operation||strlen(operation)>=16||(target&&strlen(target)>36))return false;
    if(strcmp(operation,"status")&&strcmp(operation,"lock-screen")&&strcmp(operation,"volume")&&strcmp(operation,"mute")&&strcmp(operation,"lock")&&strcmp(operation,"launch")&&strcmp(operation,"media"))return false;
    link_kind link=rpc_link_select();if(link==LINK_NONE)return false;
    computer_job *job=calloc(1,sizeof(*job));if(!job)return false;
    strcpy(job->id,id);strcpy(job->operation,operation);if(target)strcpy(job->target,target);job->value=value;job->link=link;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    if(!state_init()||state->busy){xSemaphoreGive(device_mutex);free(job);return false;}
    strcpy(job->icon_revision,state->icon_revision);
    state->busy=true;state->reading=!strcmp(operation,"status")||!strcmp(operation,"lock-screen");state->generation++;xSemaphoreGive(device_mutex);
    if(xTaskCreate(run,"computer-control",6144,job,4,NULL)!=pdPASS){xSemaphoreTake(device_mutex,portMAX_DELAY);state->busy=false;state->reading=false;state->ready=false;strcpy(state->message,"电脑控制暂时不可用，请重试");state->generation++;xSemaphoreGive(device_mutex);free(job);return false;}
    return true;
}
