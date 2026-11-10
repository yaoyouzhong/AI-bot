#include "oneclick_remote.h"
#include "mbedtls/gcm.h"
#include "ble_keyboard_flow.h"
typedef struct{char id[37];link_kind link;bool extended;unsigned epoch;int64_t started;} oneclick_job;
static unsigned oc_epoch;
static int oc_state;
static bool oc_running;
static int64_t oc_until;
static char oc_pin[64];static size_t oc_length;
static void oneclick_run(void *argument){
    oneclick_job *job=argument;int status=0;char nonce[33];
    uint8_t packet[92]={0},pin[64]={0};size_t bytes=0;bool valid=false;int failure=-6;
    cJSON *request=cJSON_CreateObject(),*reply=NULL;
    if(request){
        cJSON_AddStringToObject(request,"kind","unlock-pin");cJSON_AddStringToObject(request,"op","read");cJSON_AddStringToObject(request,"requestId",job->id);
        if(job->extended)cJSON_AddNumberToObject(request,"credentialVersion",2);
        reply=rpc_call(job->link,request,&status,nonce);
        const char *error=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(reply,"error"));
        failure=status==200?-7:status==404&&error&&!strcmp(error,"pin_not_configured")?-2:
            status==503&&error&&!strcmp(error,"pin_unavailable")?-3:
            status==409&&error&&!strcmp(error,"screen_not_ready")?-4:
            status==409&&error&&!strcmp(error,"pin_request_consumed")?-5:-6;
        const char *id=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(reply,"requestId"));
        const char *encoded=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(reply,"packet"));
        if(status==200&&id&&!strcmp(id,job->id)&&encoded&&strlen(encoded)<=124&&
           !mbedtls_base64_decode(packet,sizeof(packet),&bytes,(const unsigned char*)encoded,strlen(encoded))&&bytes>=29&&bytes<=92){
            char aad[33]={0};size_t a=0;for(size_t i=0;i<36;i++)if(id[i]!='-'&&a<32)aad[a++]=id[i];
            device_config cfg=device_get_config();mbedtls_gcm_context gcm;mbedtls_gcm_init(&gcm);
            valid=cfg.paired&&!mbedtls_gcm_setkey(&gcm,MBEDTLS_CIPHER_ID_AES,cfg.key,256)&&
                a==32&&!mbedtls_gcm_auth_decrypt(&gcm,bytes-28,packet,12,(const unsigned char*)aad,32,packet+12,16,packet+28,pin);
            mbedtls_gcm_free(&gcm);ble_keyboard_wipe(&cfg,sizeof(cfg));
            for(size_t i=0;valid&&i<bytes-28;i++)if(pin[i]<0x20||pin[i]>0x7e)valid=false;
        }
    }
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    oc_running=false;
    if(job->epoch==oc_epoch&&oc_state==1){
        int64_t age=esp_timer_get_time()/1000-job->started;
        if(valid&&age>=0&&age<6000){oc_length=bytes-28;memcpy(oc_pin,pin,oc_length);oc_state=2;}
        else oc_state=age<0||age>=6000?-8:failure;
    }
    xSemaphoreGive(device_mutex);
    ble_keyboard_wipe(pin,sizeof(pin));ble_keyboard_wipe(packet,sizeof(packet));cJSON_Delete(reply);cJSON_Delete(request);free(job);vTaskDelete(NULL);
}
void oneclick_cancel(void){xSemaphoreTake(device_mutex,portMAX_DELAY);oc_epoch++;oc_state=0;oc_length=0;ble_keyboard_wipe(oc_pin,sizeof(oc_pin));xSemaphoreGive(device_mutex);}
bool oneclick_start(const char *id,link_kind link,bool extended){
    if(!id||strlen(id)!=36||(link!=LINK_USB&&link!=LINK_BLE))return false;
    oneclick_job *job=calloc(1,sizeof(*job));if(!job)return false;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    if(oc_state!=0||oc_running){xSemaphoreGive(device_mutex);free(job);return false;}
    strcpy(job->id,id);job->link=link;job->extended=extended;job->epoch=++oc_epoch;job->started=esp_timer_get_time()/1000;oc_until=job->started+6000;oc_state=1;oc_running=true;xSemaphoreGive(device_mutex);
    if(xTaskCreate(oneclick_run,"unlock-pin",6144,job,4,NULL)!=pdPASS){free(job);xSemaphoreTake(device_mutex,portMAX_DELAY);oc_running=false;xSemaphoreGive(device_mutex);oneclick_cancel();return false;}return true;
}
int oneclick_take(char *pin,size_t capacity,size_t *length){
    *length=0;xSemaphoreTake(device_mutex,portMAX_DELAY);
    if(oc_state==1&&esp_timer_get_time()/1000>=oc_until){oc_epoch++;oc_state=-8;}
    int result=oc_state;
    if(oc_state==2){
        if(!pin||capacity<oc_length)result=-1;else{memcpy(pin,oc_pin,oc_length);*length=oc_length;}
        oc_length=0;ble_keyboard_wipe(oc_pin,sizeof(oc_pin));oc_state=0;
    }
    xSemaphoreGive(device_mutex);return result;
}
