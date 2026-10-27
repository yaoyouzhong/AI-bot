// Exercise the production read-only retry path with transient, permanent and
// malformed replies. Failed requests must never advance the firmware offset.
#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <stdint.h>
#include <windows.h>
#include "cJSON.h"
#include "link_state.h"
#define RPC_BULK_DATA 49152
#define pdMS_TO_TICKS(n) (n)
static bool connected=true,ble_connected=true;
static bool tab5_usb_connected(void){return connected;}
static int calls,delays,scenario,expected_count,local_error;
static bool preflight;
static link_kind compressed_link=LINK_BLE;
static size_t rpc_data_limit(link_kind link){(void)link;return RPC_BULK_DATA;}
static void vTaskDelay(unsigned ms){assert(ms==100||ms==200);delays++;}
static const uint8_t compressed_zeros[]={120,156,237,193,49,1,0,0,0,194,160,245,79,109,12,31,160,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,224,111,192,0,0,1};
static cJSON *rpc_data_call(link_kind link,void *http,cJSON *request,int *status,char nonce[33],
    const uint8_t *upload,size_t upload_size,uint8_t *out,size_t count,size_t *size) {
    assert(link==(scenario>=7?compressed_link:LINK_USB)&&((link==LINK_WIFI)==(http!=NULL))&&!upload&&!upload_size&&out);
    assert(!strcmp(cJSON_GetStringValue(cJSON_GetObjectItem(request,"kind")),preflight?"ota_probe":"ota"));
    assert(!strcmp(cJSON_GetStringValue(cJSON_GetObjectItem(request,"sha256")),"immutable-sha"));
    assert(!strcmp(cJSON_GetStringValue(cJSON_GetObjectItem(request,"offerId")),"same-offer"));
    assert(cJSON_GetObjectItem(request,"offset")->valueint==196608);
    assert(cJSON_GetObjectItem(request,"count")->valueint==expected_count&&(int)count==expected_count);
    calls++;snprintf(nonce,33,"%d",calls);*size=0;
    if(scenario==12){*status=local_error;return NULL;}
    if(scenario==1&&calls<3||scenario==2||scenario==5){*status=0;return NULL;}
    if(scenario==3){*status=401;return cJSON_CreateObject();}
    cJSON *reply=cJSON_CreateObject();
    if(scenario==6&&count>8192){*status=400;cJSON_AddStringToObject(reply,"error","invalid_range");expected_count=8192;return reply;}
    if(scenario>=7&&scenario<=10) {
        assert(!strcmp(cJSON_GetStringValue(cJSON_GetObjectItem(request,"acceptEncoding")),"zlib"));
        *status=200;*size=sizeof(compressed_zeros);memcpy(out,compressed_zeros,*size);
        cJSON_AddNumberToObject(reply,"offset",196608);
        cJSON_AddStringToObject(reply,"encoding",scenario==8?"unknown":"zlib");
        cJSON_AddNumberToObject(reply,"decodedSize",scenario==9?count+1:count);
        if(scenario==10)out[(*size)++]=0;
        return reply;
    }
    *status=200;*size=count;memset(out,0x6c,count);
    cJSON_AddNumberToObject(reply,"offset",196608+(scenario==4?1:0));return reply;
}
#include "ota_zlib_host.h"
#define portMAX_DELAY 0
#define xSemaphoreTake(a,b) ((void)0)
#define xSemaphoreGive(a) ((void)0)
static cJSON *device_snapshots[3];
#include "ota_range_under_test.inc"
int main(void) { SetErrorMode(SEM_NOGPFAULTERRORBOX|SEM_FAILCRITICALERRORS);_set_error_mode(_OUT_TO_STDERR);_set_abort_behavior(0,_WRITE_ABORT_MSG|_CALL_REPORTFAULT);
    uint8_t bytes[49152];
    for(scenario=0;scenario<=5;scenario++) {
        calls=delays=0;expected_count=49152;connected=scenario!=5;
        int got=rpc_ota_read(LINK_USB,"immutable-sha","same-offer",196608,bytes,49152);
        if(scenario==0){assert(got==49152);for(unsigned i=0;i<sizeof(bytes);i++)assert(bytes[i]==0x6c);}
        if(scenario==1||scenario==2)assert(calls==1&&!delays);
        if(scenario==1||scenario==2||scenario==5)assert(got==-2);
        if(scenario==3)assert(got==-401&&calls==1&&!delays);
        if(scenario==4)assert(got==-3&&calls==1&&!delays);
        if(scenario==5)assert(calls==1&&!delays);
        if(scenario==6)assert(got==8192&&calls==2&&!delays);
    }
    connected=true;
    device_snapshots[LINK_WIFI]=cJSON_CreateObject();cJSON_AddNumberToObject(device_snapshots[LINK_WIFI],"rpcOtaZlib",1);
    device_snapshots[LINK_BLE]=cJSON_Parse("{\"rpcOtaZlib\":1}");
    device_snapshots[LINK_USB]=cJSON_Parse("{\"rpcOtaZlib\":1}");
    for(int route=0;route<3;route++) {
    compressed_link=(link_kind)route;
    for(scenario=7;scenario<=11;scenario++) {
        calls=delays=0;expected_count=49152;
        rpc_ota_reset_bytes(compressed_link);
        int got=compressed_link==LINK_WIFI?rpc_ota_http_read((void*)1,"immutable-sha","same-offer",196608,bytes,49152):rpc_ota_read(compressed_link,"immutable-sha","same-offer",196608,bytes,49152);
        assert(calls==1&&!delays&&!workspace);
        assert(rpc_ota_received_bytes(compressed_link)==(scenario==11?49152:sizeof(compressed_zeros)+(scenario==10)));
        if(scenario==7||scenario==11) {assert(got==49152);for(unsigned i=0;i<sizeof(bytes);i++)assert(bytes[i]==(scenario==7?0:0x6c));}
        else assert(got==-3);
    }
    }
    scenario=7;calls=delays=0;expected_count=49152;preflight=true;
    assert(rpc_ota_probe_read("immutable-sha","same-offer",196608,bytes,49152)==49152&&calls==1);
    scenario=6;calls=delays=0;expected_count=49152;preflight=false;
    assert(rpc_ota_read(LINK_USB,"immutable-sha","same-offer",196608,bytes,49152)==8192&&calls==2&&!delays);
    scenario=12;
    const int failures[]={-1001,-1002,-1003,-1004,-1005,-1006,-18};
    for(int route=0;route<2;route++){
    compressed_link=route?LINK_BLE:LINK_USB;expected_count=route?49152:8192; // USB has already negotiated its legacy fallback above.
    for(unsigned i=0;i<sizeof(failures)/sizeof(failures[0]);i++){
        calls=delays=0;local_error=failures[i];
        assert(rpc_ota_read(compressed_link,"immutable-sha","same-offer",196608,bytes,49152)==local_error&&calls==1&&!delays);
    }
    }
    cJSON_Delete(device_snapshots[LINK_WIFI]);
    cJSON_Delete(device_snapshots[LINK_BLE]);
    cJSON_Delete(device_snapshots[LINK_USB]);
    puts("OTA_LOCAL_FAILURE_OK decrypt, allocation, envelope and reconnect errors remain negative; zero data advancement and no retries");
    puts("OTA_RANGE_ZLIB_OK real_range_function_decoded_length_unknown_encoding_wrong_size_trailing_raw_fallback");
    puts("OTA_RANGE_OK exact-offset recovery; bounded retries; permanent rejection and malformed data stop; disconnect and legacy range fallback");
}
