// Production RPC lifecycle compiled with real OS threads and semaphore waits.
#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdlib.h>
#include <stdio.h>
#include <string.h>
#include <stdatomic.h>
#include <windows.h>
#include "../../firmware/main/link_state.h"
#include "../../firmware/main/rpc_mailbox.inc"
#define portMAX_DELAY INFINITE
#define pdTRUE 1
#define pdMS_TO_TICKS(n) ((unsigned)(n))
#define MALLOC_CAP_SPIRAM 1
#define MALLOC_CAP_8BIT 2
typedef struct {HANDLE handle;bool mutex,allocated;} StaticSemaphore_t;
typedef StaticSemaphore_t *SemaphoreHandle_t;
static SemaphoreHandle_t ready_observed;
static HANDLE ready_read_entered;
static atomic_uint polls;
static atomic_bool force_timeout;
static atomic_bool fail_completion_alloc;
static atomic_uint completions_live;
static SemaphoreHandle_t xSemaphoreCreateMutex(void){SemaphoreHandle_t s=calloc(1,sizeof(*s));s->handle=CreateMutex(NULL,FALSE,NULL);s->mutex=s->allocated=true;return s;}
static SemaphoreHandle_t xSemaphoreCreateBinary(void){if(atomic_load(&fail_completion_alloc))return NULL;SemaphoreHandle_t s=calloc(1,sizeof(*s));assert(s);s->handle=CreateSemaphore(NULL,0,1,NULL);s->allocated=true;assert(s->handle);completions_live++;return s;}
static int xSemaphoreTake(SemaphoreHandle_t s,unsigned wait){if(!s->mutex&&wait&&atomic_load(&force_timeout))return 0;if(s==ready_observed&&wait&&ready_read_entered)SetEvent(ready_read_entered);return WaitForSingleObject(s->handle,wait)==WAIT_OBJECT_0;}
static int xSemaphoreGive(SemaphoreHandle_t s){return s->mutex?ReleaseMutex(s->handle):ReleaseSemaphore(s->handle,1,NULL);}
static void vSemaphoreDelete(SemaphoreHandle_t s){CloseHandle(s->handle);if(!s->mutex)completions_live--;if(s->allocated)free(s);}
static void vTaskDelay(unsigned ms){polls++;Sleep(ms);}
static int64_t esp_timer_get_time(void){return (int64_t)GetTickCount64()*1000;}
static void *heap_caps_malloc(size_t n,unsigned caps){(void)caps;return malloc(n);}
typedef struct {bool paired;} test_config;
static test_config device_get_config(void){return (test_config){true};}
static SemaphoreHandle_t device_mutex;
static link_state device_links;
static bool wifi_connected=true,ble_connected=true;
static atomic_bool usb_ready=true;
static bool tab5_usb_connected(void){return atomic_load(&usb_ready);}
static unsigned device_host_epoch(void){return 1;}
static bool device_host_changing(void){return false;}
#include "rpc_exchange_under_test.inc"
#include "usb_watch_under_test.inc"
typedef struct {link_kind link;uint8_t *reply;size_t size;unsigned timeout;} request;
static DWORD WINAPI run_request(void *arg){request *r=arg;char nonce[32],proof[64];uint8_t body[28];memset(nonce,'n',32);memset(proof,'p',64);memset(body,0x35,28);xSemaphoreTake(rpc_lock,portMAX_DELAY);uint32_t generation=box(r->link)->generation;xSemaphoreGive(rpc_lock);r->reply=exchange(r->link,nonce,proof,body,sizeof(body),&r->size,generation,r->timeout,0,1);return 0;}
static HANDLE start(request *r,link_kind link){*r=(request){.link=link,.timeout=75000};HANDLE thread=CreateThread(NULL,0,run_request,r,0,NULL);assert(thread);return thread;}
static uint32_t pending(link_kind link){uint8_t p[256];ULONGLONG end=GetTickCount64()+2000;do{size_t n=rpc_link_read(link,p,sizeof(p));if(n>8)return rpc_u32(p);Sleep(1);}while(GetTickCount64()<end);assert(!"request failed to publish");return 0;}
static void respond(link_kind link,uint32_t id){uint8_t packet[37]={2};rpc_put32(packet+1,id);rpc_put16(packet+5,0);rpc_put16(packet+7,28);memset(packet+9,0x6c,28);assert(rpc_link_write(link,packet,sizeof(packet)));}
static void join(HANDLE t,request *r,bool success){assert(WaitForSingleObject(t,2000)==WAIT_OBJECT_0);CloseHandle(t);assert((r->reply!=NULL)==success);if(success){assert(r->size==28);for(int i=0;i<28;i++)assert(r->reply[i]==0x6c);}else assert(r->size==0);free(r->reply);}
typedef struct {uint8_t bytes[256];size_t size;} ready_read;
static DWORD WINAPI read_ready(void *arg){ready_read *r=arg;r->size=rpc_link_read_wait(LINK_USB,r->bytes,sizeof(r->bytes),20);return 0;}
int main(void){
    device_mutex=xSemaphoreCreateMutex();link_state_init(&device_links);rpc_link_init();
    assert(rpc_usb_wait_supported()&&completions_live==1); // One process-lifetime readiness semaphore.
    ready_observed=rpc_ready_usb;ready_read_entered=CreateEvent(NULL,TRUE,FALSE,NULL);assert(ready_read_entered);
    ready_read incoming={0};HANDLE reader=CreateThread(NULL,0,read_ready,&incoming,0,NULL);assert(reader);
    assert(WaitForSingleObject(ready_read_entered,2000)==WAIT_OBJECT_0);
    request notified;HANDLE notified_thread=start(&notified,LINK_USB);
    assert(WaitForSingleObject(reader,2000)==WAIT_OBJECT_0);CloseHandle(reader);
    assert(incoming.size>8);respond(LINK_USB,rpc_u32(incoming.bytes));join(notified_thread,&notified,true);
    ResetEvent(ready_read_entered);incoming=(ready_read){0};reader=CreateThread(NULL,0,read_ready,&incoming,0,NULL);assert(reader);
    assert(WaitForSingleObject(ready_read_entered,2000)==WAIT_OBJECT_0);rpc_link_reset(LINK_USB);
    assert(WaitForSingleObject(reader,2000)==WAIT_OBJECT_0&&incoming.size==0);CloseHandle(reader);
    xSemaphoreGive(rpc_ready_usb);uint8_t idle[256];ULONGLONG idle_at=GetTickCount64();
    assert(rpc_link_read_wait(LINK_USB,idle,sizeof(idle),20)==8&&rpc_u32(idle)==0&&GetTickCount64()-idle_at<1000);
    SemaphoreHandle_t ready_saved=rpc_ready_usb;rpc_ready_usb=NULL;
    assert(!rpc_usb_wait_supported()&&rpc_link_read_wait(LINK_USB,idle,sizeof(idle),20)==8);rpc_ready_usb=ready_saved;
    assert(rpc_link_read_wait(LINK_BLE,idle,sizeof(idle),20)==8);
    CloseHandle(ready_read_entered);ready_read_entered=NULL;
    puts("USB_READY_WAIT_OK production wait; publication wakes request; reset invalidates generation; stale wake bounded; low-memory/legacy/BLE fallback");
    for(int i=0;i<100;i++){
        request r;HANDLE t=start(&r,LINK_USB);uint32_t id=pending(LINK_USB);respond(LINK_USB,id);join(t,&r,true);
    }
    for(int i=0;i<100;i++){
        request old,next;HANDLE a=start(&old,LINK_USB);uint32_t previous=pending(LINK_USB);rpc_link_reset(LINK_USB);
        HANDLE b=start(&next,LINK_USB);uint32_t id=pending(LINK_USB);assert(id!=previous);
        uint8_t stale[37]={2};rpc_put32(stale+1,previous);rpc_put16(stale+7,28);assert(!rpc_link_write(LINK_USB,stale,sizeof(stale)));
        respond(LINK_USB,id);join(b,&next,true);join(a,&old,false);
    }
    request usb,ble;HANDLE u=start(&usb,LINK_USB),b=start(&ble,LINK_BLE);
    uint32_t id=pending(LINK_USB);pending(LINK_BLE);rpc_link_reset(LINK_BLE);respond(LINK_USB,id);join(u,&usb,true);join(b,&ble,false);
    // Drive the exact CDC connection watcher with an in-flight USB request.
    bool was_ready=false;assert(usb_link_watch(&was_ready));
    int64_t now=esp_timer_get_time()/1000;
    assert(link_state_receive(&device_links,LINK_USB,"same-session",9,now));
    assert(link_state_receive(&device_links,LINK_WIFI,"same-session",9,now));
    assert(link_state_receive(&device_links,LINK_BLE,"same-session",9,now));
    request lost;HANDLE lost_thread=start(&lost,LINK_USB);uint32_t lost_id=pending(LINK_USB);
    uint8_t read_buf[256];rpc_link_read(LINK_BLE,read_buf,sizeof(read_buf));
    assert(rpc_link_select()==LINK_USB&&rpc_read_link_select()==LINK_USB);
    uint32_t old_generation=box(LINK_USB)->generation;
    atomic_store(&usb_ready,false);
    // Selection must bypass USB before the CDC task observes the edge.
    assert(rpc_link_select()==LINK_WIFI&&rpc_read_link_select()==LINK_WIFI);
    wifi_connected=false;assert(rpc_link_select()==LINK_BLE&&rpc_ota_link_select()==LINK_BLE);
    ble_connected=false;assert(rpc_ota_link_select()==LINK_NONE);ble_connected=true;
    device_links.mode=LINK_USB;assert(rpc_link_select()==LINK_NONE);device_links.mode=-1;
    assert(!usb_link_watch(&was_ready));join(lost_thread,&lost,false);
    assert(device_links.received_ms[LINK_USB]==-1&&device_links.sequence[LINK_USB]==9);
    assert(box(LINK_USB)->generation==old_generation+1&&!box(LINK_USB)->peer_seen);
    assert(!usb_link_watch(&was_ready)&&box(LINK_USB)->generation==old_generation+1);
    assert(link_state_active(&device_links,now)==LINK_WIFI); // State snapshot is invalidated independently of association.
    size_t reply_size=99;uint8_t body[28]={0};char nonce[32]={0},proof[64]={0};
    assert(!exchange(LINK_USB,nonce,proof,body,28,&reply_size,old_generation+1,75000,0,1)&&reply_size==0);
    atomic_store(&usb_ready,true);assert(usb_link_watch(&was_ready));
    rpc_link_read(LINK_USB,read_buf,sizeof(read_buf));
    assert(rpc_link_select()==LINK_BLE); // Polling alone cannot restore authenticated freshness.
    assert(!link_state_receive(&device_links,LINK_USB,"same-session",9,now));
    assert(!exchange(LINK_USB,nonce,proof,body,28,&reply_size,old_generation,75000,0,1));
    assert(link_state_receive(&device_links,LINK_USB,"same-session",10,now));
    assert(rpc_link_select()==LINK_USB&&rpc_read_link_select()==LINK_USB&&rpc_ota_link_select()==LINK_USB);
    request reconnected;HANDLE reconnected_thread=start(&reconnected,LINK_USB);uint32_t new_id=pending(LINK_USB);assert(new_id!=lost_id);
    uint8_t late[37]={2};rpc_put32(late+1,lost_id);rpc_put16(late+7,28);assert(!rpc_link_write(LINK_USB,late,sizeof(late)));
    respond(LINK_USB,new_id);join(reconnected_thread,&reconnected,true);wifi_connected=true;
    puts("USB_DISCONNECT_OK physical guard, in-flight wake, same-session anti-replay, reconnect, stale preparation/reply rejection, fixed mode and wireless fallback");
    atomic_store(&force_timeout,true);request timeout;HANDLE t=start(&timeout,LINK_USB);join(t,&timeout,false);atomic_store(&force_timeout,false);
    assert(!polls&&completions_live==1);
    request bounded={.link=LINK_USB,.timeout=20};ULONGLONG began=GetTickCount64();
    HANDLE bounded_thread=CreateThread(NULL,0,run_request,&bounded,0,NULL);assert(bounded_thread);
    join(bounded_thread,&bounded,false);assert(GetTickCount64()-began<1000&&!boxes[0].active&&completions_live==1);
    puts("RPC_EXCHANGE_OK 100 completion races, 100 reconnect/new-request races, cross-channel isolation, deadline exit; zero polling sleeps");
    atomic_store(&fail_completion_alloc,true);
    request low_memory;HANDLE low_thread=start(&low_memory,LINK_USB);uint32_t low_id=pending(LINK_USB);
    ULONGLONG end=GetTickCount64()+2000;while(!polls&&GetTickCount64()<end)Sleep(1);assert(polls);
    respond(LINK_USB,low_id);join(low_thread,&low_memory,true);
    request cancelled;HANDLE cancel_thread=start(&cancelled,LINK_USB);pending(LINK_USB);rpc_link_reset(LINK_USB);join(cancel_thread,&cancelled,false);
    atomic_store(&fail_completion_alloc,false);assert(completions_live==1);
    vSemaphoreDelete(rpc_ready_usb);rpc_ready_usb=NULL;assert(!completions_live);
    for(int i=0;i<2;i++){assert(!boxes[i].active&&!rpc_done[i]);free(boxes[i].out);free(boxes[i].in);}vSemaphoreDelete(rpc_lock);vSemaphoreDelete(device_mutex);
    puts("RPC_MEMORY_OK completion allocation failure preserves exact response and cancellation; no completion leaks");return 0;
}
