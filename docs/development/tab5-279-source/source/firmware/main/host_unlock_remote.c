#include "host_unlock_remote.h"
#include "device.h"
#include "rpc_link.h"
#include "ble_keyboard.h"
#include "esp_timer.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>

typedef struct {
    char id[37],session[40];link_kind link;unsigned host,connection;bool cancelled;
} host_unlock_job;
static host_unlock_job *host_unlock_active,host_unlock_binding;
static host_unlock_state host_unlock_result;
extern unsigned usb_keyboard_epoch(void);

static unsigned host_unlock_connection(link_kind link){
    if(link==LINK_USB)return usb_keyboard_epoch();
    ble_keyboard_state state;ble_keyboard_get(&state);return state.connection;
}
static bool host_unlock_peer(link_kind link){
    if(device_host_changing())return false;
    if(link==LINK_USB)return tab5_usb_connected()&&device_host_usb_owned();
    return link==LINK_BLE&&ble_connected&&ble_host_authorized();
}
// Caller holds device_mutex. Capability is read from the selected transport's
// authenticated snapshot, never the last visible snapshot of another computer.
static bool host_unlock_snapshot(link_kind link){
    int64_t age=esp_timer_get_time()/1000-device_links.received_ms[link];
    cJSON *health=cJSON_GetObjectItemCaseSensitive(device_snapshots[link],"connectionHealth");
    cJSON *version=cJSON_GetObjectItemCaseSensitive(health,"hostUnlockVersion");
    return (device_links.mode==-1||device_links.mode==link)&&device_links.session[link][0]&&
        age>=0&&age<8000&&cJSON_IsNumber(version)&&version->valuedouble==1;
}
link_kind host_unlock_remote_link(void){
    const link_kind order[]={LINK_USB,LINK_BLE};
    for(unsigned i=0;i<2;i++){
        link_kind link=order[i];if(!host_unlock_peer(link))continue;
        xSemaphoreTake(device_mutex,portMAX_DELAY);bool ready=host_unlock_snapshot(link);
        bool selected=device_links.mode==-1||device_links.mode==link;xSemaphoreGive(device_mutex);
        if(ready)return link;
        // An attached selected USB host does not fall through to a stale BLE
        // capability. A later explicit click may choose a different connection.
        if(selected)return LINK_NONE;
    }
    return LINK_NONE;
}
static bool host_unlock_same(const host_unlock_job *job){
    if(!host_unlock_peer(job->link)||device_host_epoch()!=job->host||host_unlock_connection(job->link)!=job->connection)return false;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    bool same=host_unlock_snapshot(job->link)&&!strcmp(job->session,device_links.session[job->link]);
    xSemaphoreGive(device_mutex);return same;
}
static bool host_unlock_valid(host_unlock_job *job){
    if(!host_unlock_same(job))return false;
    xSemaphoreTake(device_mutex,portMAX_DELAY);bool valid=!job->cancelled;xSemaphoreGive(device_mutex);return valid;
}
void host_unlock_remote_get(host_unlock_state *out){
    xSemaphoreTake(device_mutex,portMAX_DELAY);*out=host_unlock_result;host_unlock_job binding=host_unlock_binding;xSemaphoreGive(device_mutex);
    out->current=binding.session[0]&&host_unlock_same(&binding);
}
void host_unlock_remote_cancel(void){
    xSemaphoreTake(device_mutex,portMAX_DELAY);if(host_unlock_active)host_unlock_active->cancelled=true;xSemaphoreGive(device_mutex);
}
static const char *host_unlock_text(cJSON *root,const char *key){
    const char *text=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(root,key));return text?text:"";
}
static bool host_unlock_offer(const char *text){
    if(strlen(text)!=32)return false;
    for(size_t i=0;i<32;i++)if(!((text[i]>='0'&&text[i]<='9')||(text[i]>='a'&&text[i]<='f')))return false;
    return true;
}
static cJSON *host_unlock_request(host_unlock_job *job,const char *op,const char *offer,int next){
    cJSON *root=cJSON_CreateObject();if(!root)return NULL;
    bool ok=cJSON_AddStringToObject(root,"kind","host-unlock")&&cJSON_AddStringToObject(root,"op",op)&&
        cJSON_AddStringToObject(root,"requestId",job->id)&&cJSON_AddStringToObject(root,"session",job->session);
    if(offer[0])ok=ok&&cJSON_AddStringToObject(root,"offer",offer);
    if(!strcmp(op,"poll"))ok=ok&&cJSON_AddNumberToObject(root,"next",next);
    if(!ok){cJSON_Delete(root);return NULL;}return root;
}
static void host_unlock_run(void *argument){
    host_unlock_job *job=argument;char offer[33]={0},nonce[33];int error=1,status=0,next=0,stage=0;bool unlocked=false;
    cJSON *request=NULL,*reply=NULL;int64_t until=esp_timer_get_time()/1000+40000;
    while(esp_timer_get_time()/1000<until&&host_unlock_valid(job)){
        request=host_unlock_request(job,offer[0]?"poll":"begin",offer,next);if(!request)break;
        // Exactly one send per sequence, bounded to 2 seconds by rpc_link.c.
        reply=rpc_call(job->link,request,&status,nonce);cJSON_Delete(request);request=NULL;
        if(!host_unlock_valid(job)||esp_timer_get_time()/1000>=until)break;
        if(status!=200||!cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(reply,"ok"))){
            error=!strcmp(host_unlock_text(reply,"error"),"host_unlock_disabled")?4:2;break;
        }
        if(cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(reply,"unlocked"))){
            // A begin must first establish an offer. Only a subsequent poll
            // can confirm the result of this exact request.
            if(offer[0]&&!strcmp(host_unlock_text(reply,"stage"),"unlocked")){unlocked=true;error=0;}
            else error=2;
            break;
        }
        const char *received=host_unlock_text(reply,"offer");cJSON *number=cJSON_GetObjectItemCaseSensitive(reply,"next");
        if(!host_unlock_offer(received)||(offer[0]&&strcmp(received,offer))||!cJSON_IsNumber(number)||
            number->valuedouble!=(double)next+(offer[0]?1:0)){error=2;break;}
        memcpy(offer,received,33);next=number->valueint;stage=!strcmp(host_unlock_text(reply,"stage"),"preparing")?0:1;
        xSemaphoreTake(device_mutex,portMAX_DELAY);host_unlock_result.stage=stage;xSemaphoreGive(device_mutex);
        cJSON_Delete(reply);reply=NULL;vTaskDelay(pdMS_TO_TICKS(100));
    }
    // Cancel is best-effort on the SAME binding even after UI cancellation.
    // A lost link expires the Mac lease; never send cleanup to a new host.
    if(offer[0]&&host_unlock_same(job)){
        request=host_unlock_request(job,"cancel",offer,0);
        if(request){cJSON *ack=rpc_call(job->link,request,&status,nonce);cJSON_Delete(ack);cJSON_Delete(request);request=NULL;}
    }
    cJSON_Delete(request);cJSON_Delete(reply);
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    if(job->cancelled){unlocked=false;error=1;}
    host_unlock_active=NULL;host_unlock_result=(host_unlock_state){.finished=true,.unlocked=unlocked,.error=error,.stage=stage};
    xSemaphoreGive(device_mutex);free(job);vTaskDelete(NULL);
}
bool host_unlock_remote_start(const char *id,link_kind link){
    if(!id||strlen(id)!=36||(link!=LINK_USB&&link!=LINK_BLE)||!host_unlock_peer(link))return false;
    host_unlock_job *job=calloc(1,sizeof(*job));if(!job)return false;
    snprintf(job->id,sizeof(job->id),"%s",id);job->link=link;job->host=device_host_epoch();job->connection=host_unlock_connection(link);
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    if(host_unlock_active||!host_unlock_snapshot(link)){xSemaphoreGive(device_mutex);free(job);return false;}
    snprintf(job->session,sizeof(job->session),"%s",device_links.session[link]);
    host_unlock_binding=*job;host_unlock_active=job;host_unlock_result=(host_unlock_state){.busy=true};xSemaphoreGive(device_mutex);
    if(xTaskCreate(host_unlock_run,"host-unlock",6144,job,4,NULL)!=pdPASS){
        xSemaphoreTake(device_mutex,portMAX_DELAY);host_unlock_active=NULL;host_unlock_result=(host_unlock_state){.finished=true,.error=2};xSemaphoreGive(device_mutex);free(job);return false;
    }
    return true;
}
