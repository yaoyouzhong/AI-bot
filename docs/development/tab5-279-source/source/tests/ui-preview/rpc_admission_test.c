#ifdef NDEBUG
#undef NDEBUG
#endif
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../../firmware/main/rpc_mailbox.inc"
typedef enum {LINK_USB,LINK_WIFI,LINK_BLE} link_kind;
typedef struct {int kind;} semaphore;
typedef semaphore *SemaphoreHandle_t;
enum {portMAX_DELAY=0x7fffffff,pdTRUE=1,MALLOC_CAP_SPIRAM=1,MALLOC_CAP_8BIT=2};
#define pdMS_TO_TICKS(ms) (ms)
static semaphore mutex={1},ready={2};
static SemaphoreHandle_t rpc_lock=&mutex,rpc_ready_usb=&ready,rpc_done[2];
static rpc_mailbox boxes[2];
static uint8_t out[RPC_REQUEST_MAX],in[RPC_RESPONSE_MAX];
static unsigned now,host,drain,change,sends,wait_budget,created,deleted;
static bool locked,usb,changing,respond,ble_connected;
static bool ble_host_authorized(void){return true;}
static link_kind current_link;
static rpc_mailbox *box(link_kind link){return link==LINK_USB?&boxes[0]:link==LINK_BLE?&boxes[1]:NULL;}
static SemaphoreHandle_t completion(link_kind link){return rpc_done[link==LINK_USB?0:1];}
static int64_t esp_timer_get_time(void){return (int64_t)now*1000;}
static unsigned device_host_epoch(void){return host;}
static bool device_host_changing(void){return changing;}
static bool tab5_usb_connected(void){return usb;}
static void vTaskDelay(unsigned ticks){
    assert(!locked&&ticks>0);now+=ticks;
    rpc_mailbox *m=box(current_link);
    if(change==1&&now>=drain)m->generation++;
    if(change==2&&now>=drain)host++;
    if(change==3&&now>=drain){if(current_link==LINK_USB)usb=false;else ble_connected=false;}
    if(change==4&&now>=drain)changing=true;
    if(!change&&drain&&now>=drain)m->active=0;
}
static int xSemaphoreTake(SemaphoreHandle_t s,unsigned ticks){
    if(s==rpc_lock){assert(!locked);locked=true;return pdTRUE;}
    assert(!locked&&s&&s->kind==3);rpc_mailbox *m=box(current_link);
    sends++;wait_budget=ticks;assert(m->active==m->serial&&m->size==100&&m->out[96]==1);
    if(!respond){now+=ticks;return 0;}
    now+=20;m->expected=4;m->complete=true;memset(m->in,0xA3,4);return pdTRUE;
}
static void xSemaphoreGive(SemaphoreHandle_t s){if(s==rpc_lock){assert(locked);locked=false;}else assert(s==rpc_ready_usb);}
static SemaphoreHandle_t xSemaphoreCreateBinary(void){created++;semaphore *s=malloc(sizeof(*s));assert(s);s->kind=3;return s;}
static void vSemaphoreDelete(SemaphoreHandle_t s){deleted++;free(s);}
static void *heap_caps_malloc(size_t n,int caps){assert(caps==3);return malloc(n);}
#define RPC_STATUS_UNPUBLISHED_BUSY (-1011)
#include "rpc_admission_under_test.inc"
static void run(link_kind link,unsigned timeout,unsigned admission,unsigned drain_after,unsigned mutation,bool answer,bool expected){
    memset(boxes,0,sizeof(boxes));memset(rpc_done,0,sizeof(rpc_done));
    now=1000;host=7;usb=ble_connected=true;changing=locked=false;created=deleted=sends=wait_budget=0;
    drain=drain_after?now+drain_after:0;change=mutation;respond=answer;current_link=link;
    rpc_mailbox *m=box(link);m->out=out;m->in=in;m->generation=2;m->active=41;m->serial=41;
    uint8_t payload[4]={1,2,3,4};size_t length=99;int status=0;
    uint8_t *reply=exchange(link,"01234567890123456789012345678901",
        "0123456789012345678901234567890123456789012345678901234567890123",payload,4,&length,2,timeout,admission,7,&status);
    assert((reply!=NULL)==expected&&!locked&&created==deleted&&!completion(link));
    if(expected){assert(length==4&&reply[0]==0xA3&&m->serial==42&&sends==1&&wait_budget==timeout-drain_after);}
    else {assert(!length);if(!answer&&drain_after&&!mutation){assert(m->serial==42&&sends==1&&now==1000+timeout);}else assert(!created&&!sends&&m->serial==41);}
    free(reply);
}
int main(void){
    run(LINK_BLE,6000,0,40,0,true,false);
    run(LINK_BLE,6000,1000,40,0,true,true);
    run(LINK_USB,6000,1000,40,0,true,true);
    run(LINK_BLE,6000,1000,0,0,true,false);
    for(unsigned mutation=1;mutation<=4;mutation++)run(mutation==3?LINK_USB:LINK_BLE,6000,1000,40,mutation,true,false);
    run(LINK_BLE,6000,1000,40,0,false,false);
    for(int route=0;route<2;route++) {
        link_kind link=route?LINK_BLE:LINK_USB;
        // Reproduce the old immediate collision, then prove bounded admission.
        run(link,2000,0,40,0,true,false);
        run(link,2000,1000,40,0,true,true);
        run(link,2000,1000,990,0,true,true);
        run(link,2000,1000,0,0,true,false);
        run(link,2000,1000,40,0,false,false);
        for(unsigned mutation=1;mutation<=4;mutation++)
            run(link,2000,1000,40,mutation,true,false);
    }
    puts("RPC_ADMISSION_OK USB/BLE host unlock collision reproduced; two-second total budget; busy control precedes one credential read; 1s bounded admission; unchanged total deadline; disconnect/host/generation/switch abort; no replay");
}
