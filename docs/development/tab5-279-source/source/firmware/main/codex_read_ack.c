#include "codex_read_ack.h"
#include "device.h"
#include "rpc_link.h"
#include "voice_remote.h"
#include "esp_http_client.h"
#include "esp_timer.h"
#include "esp_heap_caps.h"
#include "freertos/idf_additions.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

typedef struct {char task[37],turn[37],receipt[37],session[40];int64_t queued;} read_ack;
static read_ack recent[8];
static unsigned next_slot;
static bool busy;
// Keep the voice snapshot off the RPC/crypto call stack.
static __attribute__((noinline)) bool read_ack_voice_idle(int64_t deadline) {
    while(esp_timer_get_time()/1000<deadline) {
        voice_remote_state voice;voice_remote_get(&voice);
        if(!voice.busy)return true;
        // This background receipt owns no mailbox while voice needs it.
        vTaskDelay(pdMS_TO_TICKS(250));
    }
    return false;
}
static void send_ack(void *argument) {
    read_ack *job=argument;
    // Only this idempotent read receipt may retry. No message/input command is
    // sent here, and the bridge rechecks its identity and latest turn each time.
    for(int attempt=0;attempt<4;attempt++) {
        if(!read_ack_voice_idle(job->queued+90000))break;
        int status=0;char nonce[33];cJSON *reply=NULL;
        cJSON *request=cJSON_CreateObject();link_kind link=rpc_read_link_select();
        if(request&&link!=LINK_NONE) {
            cJSON_AddStringToObject(request,"kind","codex");cJSON_AddStringToObject(request,"op","read-ack");
            cJSON_AddStringToObject(request,"taskId",job->task);cJSON_AddStringToObject(request,"turnId",job->turn);
            cJSON_AddStringToObject(request,"requestId",job->receipt);cJSON_AddStringToObject(request,"session",job->session);
            cJSON_AddStringToObject(request,"message","");
            if(link==LINK_WIFI) {
                device_config cfg=device_get_config();char url[96];snprintf(url,sizeof(url),"http://%s:%d/tab5/v1/rpc",cfg.host,cfg.port);
                esp_http_client_config_t options={.url=url,.method=HTTP_METHOD_POST,.timeout_ms=8000,.disable_auto_redirect=true,.buffer_size=2048};
                esp_http_client_handle_t client=esp_http_client_init(&options);
                if(client){reply=rpc_http_call(client,request,&status,nonce);esp_http_client_cleanup(client);}
            }else reply=rpc_call(link,request,&status,nonce);
        }
        cJSON_Delete(reply);cJSON_Delete(request);
        if(status==200||status==400||status==401||status==404||status==409)break;
        if(attempt<3)vTaskDelay(pdMS_TO_TICKS(1000));
    }
    xSemaphoreTake(device_mutex,portMAX_DELAY);busy=false;xSemaphoreGive(device_mutex);
    free(job);vTaskDeleteWithCaps(NULL);
}
void codex_read_acknowledge(const char *task,const char *turn,const char *receipt,const char *session) {
    if(!task||strlen(task)!=36||!turn||strlen(turn)!=36||!receipt||strlen(receipt)!=36||!session||!session[0]||strlen(session)>=40)return;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    for(int i=0;i<8;i++)if(!strcmp(recent[i].receipt,receipt)) {xSemaphoreGive(device_mutex);return;}
    read_ack *job=calloc(1,sizeof(*job));
    if(job) {
        strcpy(job->task,task);strcpy(job->turn,turn);strcpy(job->receipt,receipt);strcpy(job->session,session);
        job->queued=esp_timer_get_time()/1000;
        // Keep at most one live receipt task; subsequent render ticks retry
        // starting a different receipt after it finishes.
    }
    if(!job||busy){free(job);xSemaphoreGive(device_mutex);return;}
    busy=true;
    unsigned slot=next_slot++%8;recent[slot]=*job;
    if(xTaskCreateWithCaps(send_ack,"codex_read_ack",8192,job,4,NULL,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT)!=pdPASS) {
        busy=false;memset(&recent[slot],0,sizeof(recent[slot]));free(job);
    }
    xSemaphoreGive(device_mutex);
}
