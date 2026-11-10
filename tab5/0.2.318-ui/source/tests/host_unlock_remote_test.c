#include "device.h"
#include "ble_keyboard.h"
#include "host_unlock_remote.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>

int device_mutex;
bool ble_connected;
link_state device_links;
cJSON *device_snapshots[3];
static bool usb,owned,authorized,changing,task_failure;
static unsigned owner=1,usb_epoch=1,ble_epoch=1,requests,cancels,checks;
static int64_t now=1000;
static void (*worker)(void *);static void *argument;
static link_kind expected_link;
static int scenario;
enum {SUCCESS,HOST_CHANGE,USB_CHANGE,BLE_CHANGE,SESSION_CHANGE,MODE_CHANGE,DISABLED,
      BAD_SEQUENCE,BAD_OFFER,BEGIN_SUCCESS,RPC_FAILURE,CANCELLED,TOTAL_TIMEOUT,STALE};
#define CHECK(condition) do{assert(condition);checks++;}while(0)
unsigned device_host_epoch(void){return owner;}
bool device_host_changing(void){return changing;}
bool device_host_usb_owned(void){return owned;}
bool tab5_usb_connected(void){return usb;}
bool ble_host_authorized(void){return authorized;}
unsigned usb_keyboard_epoch(void){return usb_epoch;}
void ble_keyboard_get(ble_keyboard_state *state){state->connection=ble_epoch;}
void xSemaphoreTake(int mutex,int timeout){(void)mutex;(void)timeout;}
void xSemaphoreGive(int mutex){(void)mutex;}
int64_t esp_timer_get_time(void){return now*1000;}
void vTaskDelete(void *handle){(void)handle;}
int xTaskCreate(void (*run)(void *),const char *name,int stack,void *arg,int priority,void *handle){
    (void)name;(void)stack;(void)priority;(void)handle;
    if(task_failure)return 0;assert(!worker);worker=run;argument=arg;return 1;
}
void vTaskDelay(unsigned ms){now+=ms;if(scenario==CANCELLED)host_unlock_remote_cancel();}
static void capability(link_kind link,bool value){
    cJSON_Delete(device_snapshots[link]);device_snapshots[link]=cJSON_CreateObject();
    cJSON *health=cJSON_AddObjectToObject(device_snapshots[link],"connectionHealth");
    if(value)cJSON_AddNumberToObject(health,"hostUnlockVersion",1);
}
cJSON *rpc_call(link_kind link,cJSON *request,int *status,char nonce[33]){
    (void)nonce;CHECK(link==expected_link&&link!=LINK_WIFI);
    CHECK(!strcmp(cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(request,"kind")),"host-unlock"));
    CHECK(!strcmp(cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(request,"session")),"fixture-session"));
    for(cJSON *field=request->child;field;field=field->next)
        CHECK(!strcmp(field->string,"kind")||!strcmp(field->string,"op")||!strcmp(field->string,"requestId")||!strcmp(field->string,"session")||!strcmp(field->string,"offer")||!strcmp(field->string,"next"));
    const char *op=cJSON_GetStringValue(cJSON_GetObjectItemCaseSensitive(request,"op"));
    *status=200;
    if(!strcmp(op,"cancel")){cancels++;return cJSON_Parse("{\"ok\":true,\"cancelled\":true,\"unlocked\":false}");}
    requests++;
    if(requests==1){
        CHECK(!strcmp(op,"begin")&&!cJSON_HasObjectItem(request,"offer"));
        switch(scenario){
            case HOST_CHANGE:owner++;break;
            case USB_CHANGE:usb_epoch++;break;
            case BLE_CHANGE:ble_epoch++;break;
            case SESSION_CHANGE:strcpy(device_links.session[link],"restarted");break;
            case MODE_CHANGE:device_links.mode=LINK_WIFI;break;
            case DISABLED:capability(link,false);break;
            case RPC_FAILURE:*status=503;return NULL;
            case BEGIN_SUCCESS:return cJSON_Parse("{\"ok\":true,\"unlocked\":true,\"stage\":\"unlocked\"}");
            case TOTAL_TIMEOUT:now+=40000;break;
            case STALE:now+=8000;break;
        }
        return cJSON_Parse("{\"ok\":true,\"offer\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"next\":0,\"stage\":\"preparing\",\"unlocked\":false}");
    }
    CHECK(!strcmp(op,"poll")&&cJSON_GetObjectItemCaseSensitive(request,"next")->valueint==0);
    if(scenario==BAD_SEQUENCE)return cJSON_Parse("{\"ok\":true,\"offer\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"next\":2,\"stage\":\"preparing\"}");
    if(scenario==BAD_OFFER)return cJSON_Parse("{\"ok\":true,\"offer\":\"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\",\"next\":1,\"stage\":\"preparing\"}");
    return cJSON_Parse("{\"ok\":true,\"unlocked\":true,\"stage\":\"unlocked\"}");
}
static const char *id="00112233-4455-4677-8899-aabbccddeeff";
static void setup(link_kind link,int test){
    assert(!worker);usb=link==LINK_USB;ble_connected=link==LINK_BLE;owned=authorized=true;changing=task_failure=false;
    scenario=test;requests=cancels=0;expected_link=link;now=1000;owner++;usb_epoch++;ble_epoch++;
    memset(&device_links,0,sizeof(device_links));device_links.mode=-1;
    for(int i=0;i<3;i++){strcpy(device_links.session[i],"fixture-session");device_links.received_ms[i]=now;capability(i,true);}
}
static host_unlock_state run(void){
    assert(worker);void (*task)(void *)=worker;worker=NULL;task(argument);host_unlock_state state;host_unlock_remote_get(&state);return state;
}
int main(void){
    for(int i=0;i<2;i++){
        link_kind link=i?LINK_BLE:LINK_USB;setup(link,SUCCESS);
        CHECK(host_unlock_remote_link()==link);CHECK(!host_unlock_remote_start(id,LINK_WIFI));
        CHECK(host_unlock_remote_start(id,link));CHECK(!host_unlock_remote_start(id,link));
        host_unlock_state state=run();CHECK(state.finished&&state.unlocked&&state.current&&state.error==0&&requests==2&&cancels==1);
        // A changed host after the final response invalidates the UI result.
        owner++;host_unlock_remote_get(&state);CHECK(!state.current);
        const int cases[]={HOST_CHANGE,SESSION_CHANGE,MODE_CHANGE,DISABLED,BAD_SEQUENCE,BAD_OFFER,BEGIN_SUCCESS,RPC_FAILURE,CANCELLED,TOTAL_TIMEOUT,STALE};
        for(unsigned j=0;j<sizeof(cases)/sizeof(cases[0]);j++){
            setup(link,cases[j]);CHECK(host_unlock_remote_start(id,link));state=run();CHECK(state.finished&&!state.unlocked&&state.error!=0);
            CHECK(requests==((cases[j]==BAD_SEQUENCE||cases[j]==BAD_OFFER)?2u:1u));
            CHECK(cancels==((cases[j]==BAD_SEQUENCE||cases[j]==BAD_OFFER||cases[j]==CANCELLED)?1u:0u));
        }
        setup(link,i?BLE_CHANGE:USB_CHANGE);CHECK(host_unlock_remote_start(id,link));state=run();CHECK(!state.unlocked&&requests==1&&cancels==0);
        setup(link,SUCCESS);CHECK(host_unlock_remote_start(id,link));host_unlock_remote_cancel();state=run();CHECK(!state.unlocked&&requests==0&&cancels==0);
        setup(link,SUCCESS);task_failure=true;CHECK(!host_unlock_remote_start(id,link));task_failure=false;CHECK(host_unlock_remote_start(id,link));state=run();CHECK(state.unlocked);
        setup(link,SUCCESS);capability(link,false);CHECK(host_unlock_remote_link()==LINK_NONE&&!host_unlock_remote_start(id,link));
        capability(link,true);device_links.mode=LINK_WIFI;CHECK(host_unlock_remote_link()==LINK_NONE&&!host_unlock_remote_start(id,link));
        device_links.mode=-1;changing=true;CHECK(host_unlock_remote_link()==LINK_NONE&&!host_unlock_remote_start(id,link));
    }
    setup(LINK_USB,SUCCESS);ble_connected=true;CHECK(host_unlock_remote_link()==LINK_USB);
    capability(LINK_USB,false);CHECK(host_unlock_remote_link()==LINK_NONE);
    device_links.mode=LINK_BLE;CHECK(host_unlock_remote_link()==LINK_BLE);
    for(int i=0;i<3;i++)cJSON_Delete(device_snapshots[i]);
    printf("HOST_UNLOCK_REMOTE_TEST_OK %u assertions; fake RPC only, no credential or hardware\n",checks);
}
