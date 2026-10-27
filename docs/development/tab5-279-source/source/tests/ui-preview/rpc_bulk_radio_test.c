#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include "cJSON.h"
typedef enum {LINK_USB,LINK_WIFI,LINK_BLE,LINK_NONE} link_kind;
typedef struct {int mode;} device_config;
static int mode,begin_calls,ends,releases,wakes,waits;
static bool ble_connected,pending,ready,can_begin,becomes_ready,drop_link,change_mode;
static int64_t now;
static device_config device_get_config(void){return (device_config){mode};}
static bool wifi_probe_begin_rpc_bulk(void){begin_calls++;if(!can_begin)return false;pending=true;return true;}
static bool wifi_probe_pending(void){return pending;}
static bool wifi_probe_ready(void){return ready;}
static void wifi_probe_end(void){ends++;pending=ready=false;}
static void wifi_probe_release_rpc_bulk(bool succeeded){if(succeeded)releases++;else wifi_probe_end();}
static void tab5_wifi_wake(void){wakes++;}
static int64_t esp_timer_get_time(void){return now;}
#define pdMS_TO_TICKS(ms) (ms)
static void vTaskDelay(int ms){waits++;now+=(int64_t)ms*1000;if(becomes_ready)ready=true;if(drop_link)ble_connected=false;if(change_mode)mode=-1;}
#include "rpc_bulk_radio.inc"
static int rpc_calls;
static cJSON *rpc_call_impl(link_kind link,cJSON *request,int *status,char nonce[33],void *http,
    const uint8_t *upload,size_t upload_size,uint8_t *download,size_t capacity,size_t *received,void *progress,void *context) {
    (void)link;(void)request;(void)nonce;(void)http;(void)upload;(void)upload_size;
    (void)download;(void)capacity;(void)received;(void)progress;(void)context;
    rpc_calls++;*status=-1;return NULL;
}
#include "rpc_data_call_under_test.inc"
static void reset(void){mode=LINK_BLE;ble_connected=can_begin=true;pending=ready=becomes_ready=drop_link=change_mode=false;begin_calls=ends=releases=wakes=waits=0;now=0;}
int main(void) {
    cJSON *request=cJSON_CreateObject();cJSON_AddStringToObject(request,"kind","benchmark");
    reset();assert(!rpc_bulk_radio_begin(LINK_USB,request,49152,0)&&!begin_calls);
    assert(!rpc_bulk_radio_begin(LINK_WIFI,request,0,49152)&&!begin_calls);
    mode=-1;assert(!rpc_bulk_radio_begin(LINK_BLE,request,49152,0)&&!begin_calls);
    reset();assert(!rpc_bulk_radio_begin(LINK_BLE,request,12288,12288)&&!begin_calls);
    cJSON_AddBoolToObject(request,"otaProbe",true);assert(!rpc_bulk_radio_begin(LINK_BLE,request,0,49152)&&!begin_calls);
    cJSON_DeleteItemFromObjectCaseSensitive(request,"otaProbe");
    // The outer gallery or isolated preflight remains the only lease owner.
    reset();can_begin=false;pending=ready=true;
    bool owned=rpc_bulk_radio_begin(LINK_BLE,request,0,49152);rpc_bulk_radio_end(owned,true);
    assert(!owned&&!ends&&!releases&&!wakes&&pending&&ready);
    reset();becomes_ready=true;owned=rpc_bulk_radio_begin(LINK_BLE,request,49152,0);
    assert(owned&&waits==1&&!ends);rpc_bulk_radio_end(owned,true);assert(releases==1&&wakes==2);
    reset();ready=pending=true;assert(rpc_bulk_radio_begin(LINK_BLE,request,0,49152)&&!waits);
    rpc_bulk_radio_end(true,true);assert(releases==1);
    reset();assert(!rpc_bulk_radio_begin(LINK_BLE,request,0,49152)&&ends==1&&!releases&&now==500000);
    reset();drop_link=true;assert(!rpc_bulk_radio_begin(LINK_BLE,request,0,49152)&&waits==1&&ends==1);
    reset();change_mode=becomes_ready=true;assert(!rpc_bulk_radio_begin(LINK_BLE,request,0,49152)&&ends==1);
    // A completed or failed RPC releases only its own lease; the worker's
    // existing quiet period/hard deadline determines actual driver restoration.
    rpc_bulk_radio_end(false,true);assert(!releases);
    reset();becomes_ready=true;int status=0;char nonce[33]={0};uint8_t bytes[1]={0};
    assert(!rpc_data_call(LINK_BLE,NULL,request,&status,nonce,NULL,0,bytes,49152,NULL));
    assert(rpc_calls==1&&status==-1&&!releases&&ends==1);
    reset();mode=-1;
    assert(!rpc_data_call(LINK_BLE,NULL,request,&status,nonce,NULL,0,bytes,49152,NULL));
    assert(rpc_calls==2&&!begin_calls&&!releases);
    reset();can_begin=false;pending=ready=true;
    assert(!rpc_data_call(LINK_BLE,NULL,request,&status,nonce,NULL,0,bytes,49152,NULL));
    assert(rpc_calls==3&&!ends&&!releases&&ready);
    cJSON_Delete(request);
    puts("RPC_BULK_RADIO_OK actual_policy_auto_USB_probe_owner_timeout_mode_and_link_loss");
    return 0;
}
