#include "unlock_remote.h"
#include "device.h"
#include "rpc_link.h"
#include "computer_remote.h"
#include "unlock_keyboard.h"
#include "esp_http_client.h"
#include "esp_timer.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>

// This worker never receives, requests or retains a Windows PIN.
typedef struct {char id[37],session[40];unsigned connection;bool cancelled;} wifi_unlock_job;
static wifi_unlock_job *wifi_unlock_active;
static unlock_remote_state wifi_unlock_state;
static void wifi_unlock_wipe(void *p,size_t n){volatile unsigned char *b=p;while(n--)*b++=0;}
bool unlock_remote_wifi_only(void){
    // Wi-Fi belongs to the selected computer. Unrelated physical connections
    // must neither receive keys nor block this computer's Wi-Fi input path.
    if(!wifi_connected||device_host_changing())return false;
    device_config cfg=device_get_config();if(cfg.mode!=-1&&cfg.mode!=LINK_WIFI)return false;
    ble_keyboard_state usb={0};usb_keyboard_get(&usb);if(usb.ready)return false;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    int64_t age=esp_timer_get_time()/1000-device_links.received_ms[LINK_WIFI];
    bool fresh=device_links.session[LINK_WIFI][0]&&age>=0&&age<8000;
    xSemaphoreGive(device_mutex);return fresh;
}
unsigned unlock_remote_connection(void){
    unsigned hash=2166136261u^device_host_epoch();xSemaphoreTake(device_mutex,portMAX_DELAY);
    const char *session=device_links.session[LINK_WIFI];
    for(size_t i=0;session[i];i++)hash=(hash^(unsigned char)session[i])*16777619u;
    xSemaphoreGive(device_mutex);return hash;
}
void unlock_remote_get(unlock_remote_state *out){xSemaphoreTake(device_mutex,portMAX_DELAY);*out=wifi_unlock_state;xSemaphoreGive(device_mutex);}
void unlock_remote_cancel(void){xSemaphoreTake(device_mutex,portMAX_DELAY);if(wifi_unlock_active)wifi_unlock_active->cancelled=true;xSemaphoreGive(device_mutex);}
static bool wifi_unlock_valid(wifi_unlock_job *job){
    if(!unlock_remote_wifi_only()||unlock_remote_connection()!=job->connection)return false;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    bool valid=!job->cancelled&&!strcmp(job->session,device_links.session[LINK_WIFI]);
    xSemaphoreGive(device_mutex);return valid;
}
static const char *wifi_unlock_text(cJSON *root,const char *key){const char *s=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(root,key));return s?s:"";}
static cJSON *wifi_unlock_request(wifi_unlock_job *job,const char *op,const char *offer,int next){
    cJSON *root=cJSON_CreateObject();if(!root)return NULL;
    cJSON_AddStringToObject(root,"kind","wifi-unlock");cJSON_AddStringToObject(root,"op",op);
    cJSON_AddStringToObject(root,"requestId",job->id);cJSON_AddStringToObject(root,"session",job->session);
    if(offer[0])cJSON_AddStringToObject(root,"offer",offer);
    if(!strcmp(op,"poll"))cJSON_AddNumberToObject(root,"next",next);
    return root;
}
static void wifi_unlock_run(void *argument){
    wifi_unlock_job *job=argument;char offer[33]={0},nonce[33];int error=1,status=0,next=0;bool unlocked=false;
    esp_http_client_handle_t client=NULL;cJSON *request=NULL,*reply=NULL;
    device_config cfg=device_get_config();char url[96];
    if(!cfg.paired||!wifi_unlock_valid(job))goto done;
    snprintf(url,sizeof(url),"http://%s:%d/tab5/v1/rpc",cfg.host,cfg.port);
    esp_http_client_config_t options={.url=url,.method=HTTP_METHOD_POST,.timeout_ms=2000,.disable_auto_redirect=true,.buffer_size=2048};
    client=esp_http_client_init(&options);if(!client)goto done;
    int64_t until=esp_timer_get_time()/1000+40000;
    while(esp_timer_get_time()/1000<until&&wifi_unlock_valid(job)){
        request=wifi_unlock_request(job,offer[0]?"poll":"begin",offer,next);if(!request)goto done;
        // One attempt per sequence number. No HTTP, USB or BLE fallback/retry.
        reply=rpc_http_call(client,request,&status,nonce);cJSON_Delete(request);request=NULL;
        if(!wifi_unlock_valid(job))goto done;
        if(status!=200||!cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(reply,"ok"))){
            const char *e=wifi_unlock_text(reply,"error");
            error=!strcmp(e,"pin_not_configured")?3:!strcmp(e,"wifi_unlock_disabled")?4:
                !strcmp(e,"screen_not_ready")||!strcmp(e,"screen_changed")?5:!strcmp(e,"unlock_not_confirmed")?6:2;
            goto done;
        }
        if(cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(reply,"unlocked"))){unlocked=true;error=0;goto done;}
        const char *received=wifi_unlock_text(reply,"offer");cJSON *number=cJSON_GetObjectItemCaseSensitive(reply,"next");
        if(strlen(received)!=32||(offer[0]&&strcmp(received,offer))||!cJSON_IsNumber(number)||number->valuedouble!=(double)next+(offer[0]?1:0)){error=2;goto done;}
        memcpy(offer,received,32);next=number->valueint;
        const char *stage=wifi_unlock_text(reply,"stage");
        xSemaphoreTake(device_mutex,portMAX_DELAY);wifi_unlock_state.stage=!strcmp(stage,"preparing")?0:1;xSemaphoreGive(device_mutex);
        cJSON_Delete(reply);reply=NULL;vTaskDelay(pdMS_TO_TICKS(100));
    }
done:
    // Best-effort revocation carries no input; a lost link expires the PC lease.
    if(client&&offer[0]&&unlock_remote_wifi_only()&&unlock_remote_connection()==job->connection){
        request=wifi_unlock_request(job,"cancel",offer,0);
        if(request){cJSON *ack=rpc_http_call(client,request,&status,nonce);cJSON_Delete(ack);cJSON_Delete(request);request=NULL;}
    }
    cJSON_Delete(request);cJSON_Delete(reply);if(client)esp_http_client_cleanup(client);wifi_unlock_wipe(&cfg,sizeof(cfg));
    xSemaphoreTake(device_mutex,portMAX_DELAY);wifi_unlock_active=NULL;wifi_unlock_state=(unlock_remote_state){.finished=true,.unlocked=unlocked,.error=error,.stage=wifi_unlock_state.stage};xSemaphoreGive(device_mutex);
    wifi_unlock_wipe(job,sizeof(*job));free(job);vTaskDelete(NULL);
}
bool unlock_remote_start(const char *id,unsigned connection){
    if(!id||strlen(id)!=36||!unlock_remote_wifi_only()||unlock_remote_connection()!=connection)return false;
    bool locked=false;int64_t updated=0;if(!computer_remote_lock_state(&locked,&updated)||!locked)return false;
    wifi_unlock_job *job=calloc(1,sizeof(*job));if(!job)return false;
    memcpy(job->id,id,36);job->connection=connection;xSemaphoreTake(device_mutex,portMAX_DELAY);
    if(wifi_unlock_active){xSemaphoreGive(device_mutex);free(job);return false;}
    snprintf(job->session,sizeof(job->session),"%s",device_links.session[LINK_WIFI]);wifi_unlock_active=job;wifi_unlock_state=(unlock_remote_state){.busy=true};xSemaphoreGive(device_mutex);
    if(xTaskCreate(wifi_unlock_run,"wifi-unlock",6144,job,4,NULL)!=pdPASS){xSemaphoreTake(device_mutex,portMAX_DELAY);wifi_unlock_active=NULL;wifi_unlock_state=(unlock_remote_state){.finished=true,.error=2};xSemaphoreGive(device_mutex);free(job);return false;}
    return true;
}
