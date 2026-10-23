#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "cJSON.h"
typedef enum {LINK_USB,LINK_WIFI,LINK_BLE,LINK_NONE} link_kind;
typedef struct {uint8_t *image;size_t image_size;link_kind transport;unsigned host_generation;int mode;char session[40];} codex_job;
typedef struct {int mode;} device_config;
#define MALLOC_CAP_SPIRAM 1
#define MALLOC_CAP_8BIT 2
#define heap_caps_malloc(n,c) malloc(n)
#define pdMS_TO_TICKS(ms) (ms)
#define portMAX_DELAY 0
static int device_mutex;
static struct {char session[3][40];} device_links;
static cJSON *device_snapshots[3];
static void xSemaphoreTake(int unused,int wait){(void)unused;(void)wait;}
static void xSemaphoreGive(int unused){(void)unused;}
static uint8_t photo[161111],accepted[sizeof(photo)];
static size_t received;
static unsigned now,host=7;
static int connection_mode=LINK_BLE,mode,calls,commits,waits,expected_link=LINK_BLE;
static bool ble_connected=true,authorized=true,changing;
static unsigned device_host_epoch(void){return host;}
static bool device_host_changing(void){return changing;}
static device_config device_get_config(void){return (device_config){connection_mode};}
static bool ble_host_authorized(void){return authorized;}
static int64_t esp_timer_get_time(void){return (int64_t)now*1000;}
static link_kind rpc_image_link_select(const char *session){
    assert(!strcmp(session,"same-session"));
    return mode==11?LINK_NONE:ble_connected&&authorized?LINK_BLE:LINK_NONE;
}
static void vTaskDelay(unsigned ms){
    assert(ms==50);now+=ms;waits++;
    if(mode==9)host++;
    else if(mode==10)connection_mode=-1;
    else if(mode==11)strcpy(device_links.session[LINK_BLE],"different-session");
    else if(mode!=2&&mode!=11&&now>=1250){ble_connected=true;authorized=true;}
}
#include "photo_upload_retry.h"
static size_t rpc_data_limit(int link){assert(link==expected_link);return mode>=4?49152:8192;}
static const char *link_name(int link){return link==LINK_BLE?"BLE":"USB";}
static void set_result(const char *text,int code){assert(code==-1&&strstr(text,"%"));}
static int mbedtls_sha256(const void *p,size_t n,uint8_t hash[32],int flags){assert(p==photo&&n==sizeof(photo));memset(hash,0,32);return 0;}
static int photo_tx_sha256(const uint8_t *p,size_t n,uint8_t hash[32]){return mbedtls_sha256(p,n,hash,0);}
static void photo_tx_diagnostics(char *out,unsigned size){snprintf(out,size,"c,1,0,0,0,0,0,0,1024,0");}
static cJSON *rpc_call(int link,cJSON *json,int *status,char nonce[33]){
    assert(link==expected_link&&mode==13);calls++;*status=0;return NULL;
}
static cJSON *rpc_data_call(int link,void *http,cJSON *json,int *status,char nonce[33],const uint8_t *raw,size_t count,uint8_t *out,size_t capacity,size_t *size) {
    assert(link==expected_link&&!http&&!out&&!size&&capacity==0&&raw>=photo&&raw+count<=photo+sizeof(photo));
    assert(!cJSON_HasObjectItem(json,"data"));calls++;
    assert(!strcmp(cJSON_GetObjectItemCaseSensitive(json,"requestId")->valuestring,"same-image-id"));
    assert(!strcmp(cJSON_GetObjectItemCaseSensitive(json,"sha256")->valuestring,"0000000000000000000000000000000000000000000000000000000000000000"));
    size_t offset=(size_t)cJSON_GetObjectItemCaseSensitive(json,"offset")->valuedouble;
    assert(offset==(size_t)(raw-photo));
    cJSON *reply=cJSON_CreateObject();
    if(mode==1&&count>6144){*status=400;cJSON_AddStringToObject(reply,"error","invalid_range");return reply;}
    if(mode==8||mode==14){*status=mode==8?401:403;return reply;}
    if(mode>=17&&mode<=20){*status=mode==17?400:mode==18?409:mode==19?500:-18;return reply;}
    if(mode==2||mode==16||((mode==5||mode==9||mode==10||mode==11||mode==12||mode==15||mode>=21)&&calls==1)) {
        ble_connected=false;if(mode==12){ble_connected=true;authorized=false;}
        *status=0;cJSON_Delete(reply);return NULL;
    }
    // Accepted-prefix/final-ID transport contract: a lost reply must not append
    // duplicate bytes or save the image twice when the same chunk is repeated.
    if(offset==received){memcpy(accepted+offset,raw,count);received+=count;if(received==sizeof(photo))commits++;}
    else {assert(offset+count<=received&&!memcmp(accepted+offset,raw,count));}
    if((mode==6&&calls==2)||(mode==7&&received==sizeof(photo)&&calls==4)){
        ble_connected=false;*status=0;cJSON_Delete(reply);return NULL;
    }
    *status=200;cJSON_AddNumberToObject(reply,"offset",mode==3?received+1:offset+count);return reply;
}
#include "photo_upload_chunks_under_test.inc"
static void reset(int scenario){
    mode=scenario;now=1000;host=7;connection_mode=LINK_BLE;changing=false;
    ble_connected=authorized=true;received=calls=commits=waits=0;expected_link=LINK_BLE;
    strcpy(device_links.session[LINK_BLE],"same-session");
    cJSON_Delete(device_snapshots[LINK_BLE]);device_snapshots[LINK_BLE]=cJSON_CreateObject();
    if(mode==22)cJSON_AddNumberToObject(device_snapshots[LINK_BLE],"imageChunkRetry",2);
    else if(mode==23)cJSON_AddBoolToObject(device_snapshots[LINK_BLE],"imageChunkRetry",true);
    else if(mode!=21)cJSON_AddNumberToObject(device_snapshots[LINK_BLE],"imageChunkRetry",1);
    memset(accepted,0,sizeof(accepted));
}
static int upload(bool command){
    cJSON *json=cJSON_CreateObject();cJSON_AddStringToObject(json,"session","same-session");
    cJSON_AddStringToObject(json,"taskId","same-task-id");cJSON_AddStringToObject(json,"requestId","same-image-id");
    cJSON_AddStringToObject(json,"op",command?"send":"image-upload");
    codex_job job={command?NULL:photo,sizeof(photo),(link_kind)expected_link,7,LINK_BLE,"same-session"};
    int status=-1;char nonce[33];cJSON *reply=rpc_job(&job,json,&status,nonce);
    upload_mark(&job,0,status);cJSON_Delete(reply);cJSON_Delete(json);return status;
}
int main(void){
    for(unsigned i=0;i<sizeof(photo);i++)photo[i]=(uint8_t)(i*17u);
    for(int scenario=0;scenario<=23;scenario++){
        reset(scenario);if(mode==15)expected_link=LINK_USB;
        int status=upload(mode==13);
        if(mode==0||mode==1||mode==4||mode==5||mode==6||mode==7||mode==12){
            assert(status==200&&received==sizeof(photo)&&commits==1&&!memcmp(photo,accepted,sizeof(photo)));
            assert(calls==(mode==0?20:mode==1?28:mode==4?4:5));
        }else {
            assert(status!=200);
            if(mode==2)assert(calls==1&&now==1000+PHOTO_BLE_RECOVERY_MS);
            if(mode==3)assert(status==502&&calls==1&&received==8192);
            if(mode==8||mode==14)assert(calls==1&&!waits);
            if(mode>=17)assert(calls==1&&!waits);
            if(mode==9||mode==10||mode==11)assert(calls==1&&waits==1&&now==1050);
            if(mode==13||mode==15)assert(status==0&&calls==1&&!waits);
            if(mode==16)assert(calls==2&&upload_retries>0);
        }
    }
    unsigned first=upload_first_offset,first_ms=upload_first_ms,first_phase=upload_first_phase;
    int first_status=upload_first_status;unsigned failures=upload_failures;
    reset(4);assert(upload(false)==200);char diagnostic[96];codex_remote_upload_diagnostics(diagnostic,sizeof(diagnostic));
    assert(upload_failures==failures&&upload_first_offset==first&&upload_first_status==first_status&&
        upload_first_ms==first_ms&&upload_first_phase==first_phase&&strstr(diagnostic,";f,")&&strstr(diagnostic,";r,"));
    cJSON_Delete(device_snapshots[LINK_BLE]);
    puts("PHOTO_UPLOAD_CHUNKS_OK legacy/bulk; one BLE-only retry; first/middle/final lost replies; exact bytes and one final save; offline/host/mode/session/auth bounds; commands and USB not retried; first failure retained");
}
