#undef NDEBUG
#include <assert.h>
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <string.h>
#include <stdatomic.h>
#include "../../firmware/main/usb_receive_profile.h"
static bool bulk_selected;
static bool drop_bulk;
static void usb_bulk_receive_select(bool enabled){bulk_selected=enabled;}
static void usb_bulk_receive_close(void){bulk_selected=false;}
static bool usb_bulk_receive_discard(void){return drop_bulk;}
#define TINYUSB_CDC_ACM_0 0
#define pdMS_TO_TICKS(n) (n)
typedef struct {struct {bool dtr;} line_state_changed_data;} cdcacm_event_t;
typedef struct {int id;} tinyusb_event_t;
enum {TINYUSB_EVENT_ATTACHED=1,TINYUSB_EVENT_DETACHED=2};
typedef void *TaskHandle_t;
static unsigned wakes;
static void xTaskNotifyGive(TaskHandle_t task){assert(task);wakes++;}
static bool connected=true;
static bool port_open=true;
static int scenario;static int64_t now;static size_t sent;
static unsigned rx_buffered,tx_buffered,flushes;
static void tud_cdc_read_flush(void){rx_buffered=0;flushes++;}
static void tud_cdc_write_clear(void){tx_buffered=0;}
static void close_port(void);
static void open_port(void);
static bool tud_ready(void){return connected;}
static bool tud_cdc_connected(void){return connected&&port_open;}
static int64_t esp_timer_get_time(void){return now;}
static void vTaskDelay(unsigned ms){now+=(int64_t)ms*1000;}
static void tinyusb_cdcacm_write_flush(int itf,unsigned wait){assert(itf==0&&wait==20);now+=20000;}
static size_t tinyusb_cdcacm_write_queue(int itf,const uint8_t *bytes,size_t size) {
    assert(itf==0);if(scenario==1)return 0;
    size_t n=size>1024?1024:size;
    for(size_t i=0;i<n;i++)assert(bytes[i]==(uint8_t)(sent+i));
    sent+=n;if(scenario==2)close_port();return n;
}
#include "usb_cdc_under_test.inc"
#include "../../firmware/main/usb_binary.h"
#include "../../firmware/main/link_state.h"
static link_state device_links;
static int device_mutex=1;
#define portMAX_DELAY 0xffffffff
static void xSemaphoreTake(int mutex,unsigned wait){assert(mutex==1&&wait==portMAX_DELAY);}
static void xSemaphoreGive(int mutex){assert(mutex==1);}
static unsigned mailbox_resets;
static void rpc_link_reset(int link){assert(link==LINK_USB);mailbox_resets++;}
static unsigned sync_receiver(unsigned epoch) {
    size_t used=5;bool overflow=true;usb_binary binary={.used=3,.want=8,.failed=true};
#include "usb_cdc_sync_under_test.inc"
    assert(!used&&!overflow&&!binary.used&&!binary.want&&!binary.failed);
    return epoch;
}
static void close_port(void){port_open=false;cdcacm_event_t event={0};usb_cdc_line_state(0,&event);}
static void open_port(void){port_open=true;cdcacm_event_t event={.line_state_changed_data.dtr=true};usb_cdc_line_state(0,&event);}
int main(void) {
    unsigned before_bus=atomic_load(&cdc_epoch);tinyusb_event_t bus={.id=TINYUSB_EVENT_DETACHED};
    usb_keyboard_bus_event(&bus,NULL);bus.id=TINYUSB_EVENT_ATTACHED;usb_keyboard_bus_event(&bus,NULL);
    assert(atomic_load(&cdc_epoch)==before_bus+2);
    cdc_task=(void*)1;usb_cdc_rx(0,NULL);assert(wakes==1);
    usb_cdc_rx(1,NULL);assert(wakes==1);
    drop_bulk=true;rx_buffered=129;usb_cdc_rx(0,NULL);
    assert(wakes==1&&!rx_buffered);drop_bulk=false;
    // Only active binary reception is timed; real RX callback wakes remain
    // unchanged. Timer wrap and disconnect must not create bogus long waits.
    usb_rx_reset();now=100;usb_cdc_rx(0,NULL);
    assert(!atomic_load(&usb_rx_values[5]));
    usb_rx_enable(true);now=0xfffffff0u;usb_cdc_rx(0,NULL);
    usb_rx_span(8192,1500,2100,0x20u);
    assert(atomic_load(&usb_rx_values[6])==48);
    // RX task callback may publish after the caller sampled its clock.
    usb_rx_notify(100);usb_rx_span(0,0,0,79);
    assert(atomic_load(&usb_rx_values[6])==48);
    usb_rx_enable(false);now=900;usb_cdc_rx(0,NULL);
    usb_rx_span(64,500,900,1000);
    char receive[200];usb_rx_format(receive,sizeof(receive));
    assert(!strcmp(receive,";usb_rx,8256,3,8192,2,3,2,48,0,0,0,0"));
    usb_rx_reset();usb_rx_format(receive,sizeof(receive));
    assert(!strcmp(receive,";usb_rx,0,0,0,0,0,0,0,0,0,0,0"));
    puts("USB_RECEIVE_PROFILE_OK active notifications, FIFO spans/units, timer wrap, disconnect/reset; native hardware counters are stubs");
    uint8_t bytes[65535];for(unsigned i=0;i<sizeof(bytes);i++)bytes[i]=(uint8_t)i;
    unsigned epoch=atomic_load(&cdc_epoch);
    open_port();assert(atomic_load(&cdc_epoch)==epoch);
    close_port();open_port();assert(atomic_load(&cdc_epoch)==epoch+1);
    // Close/open can finish while the receiver task is blocked. Clear old
    // bytes at the close edge, before a new host command can enter the FIFO.
    unsigned receiver_epoch=atomic_load(&cdc_epoch);
    rx_buffered=17;tx_buffered=19;bulk_selected=true;close_port();assert(!bulk_selected);
    assert(!rx_buffered&&!tx_buffered&&flushes>0);
    open_port();rx_buffered=31; // first new command
    link_state_init(&device_links);
    assert(link_state_receive(&device_links,LINK_USB,"old-bridge",42,100));
    assert(link_state_receive(&device_links,LINK_WIFI,"wifi-bridge",43,100));
    receiver_epoch=sync_receiver(receiver_epoch);
    assert(rx_buffered==31&&mailbox_resets==1&&receiver_epoch==atomic_load(&cdc_epoch));
    assert(device_links.received_ms[LINK_USB]<0&&device_links.sequence[LINK_USB]==42);
    assert(!strcmp(device_links.session[LINK_USB],"old-bridge"));
    assert(link_state_active(&device_links,101)==LINK_WIFI);
    assert(!link_state_receive(&device_links,LINK_USB,"old-bridge",41,101));
    assert(link_state_receive(&device_links,LINK_USB,"new-bridge",1,102));
    assert(link_state_active(&device_links,102)==LINK_USB);
    cdc_receive_epoch=atomic_load(&cdc_epoch);
    for(scenario=0;scenario<3;scenario++) {
        cdc_receive_epoch=atomic_load(&cdc_epoch);
        sent=0;now=0;bool success=usb_write((char*)bytes,sizeof(bytes));
        if(scenario==0)assert(success&&sent==sizeof(bytes)&&now>500000&&now<3000000);
        if(scenario==1)assert(!success&&!sent&&now>=3000000&&now<3050000);
        if(scenario==2)assert(!success&&sent==1024&&now<50000);
    }
    // A command parsed before close must not emit even its first reply byte
    // into a reopened connection, not just stop halfway through a reply.
    scenario=0;sent=0;close_port();open_port();
    assert(!usb_write((char*)bytes,sizeof(bytes))&&!sent);
    // A configured bus with a closed serial session is unavailable as well.
    port_open=false;sent=0;cdc_receive_epoch=atomic_load(&cdc_epoch);
    assert(!tab5_usb_connected()&&!usb_write((char*)bytes,sizeof(bytes))&&!sent);
    port_open=true;
    connected=false;sent=0;assert(!usb_write((char*)bytes,sizeof(bytes))&&!sent);
    puts("USB_CDC_OK bounded writes; close clears old FIFO; delayed receiver preserves new command; old-generation replies blocked; mid-write close and disconnect stop sending");
    puts("USB_CDC_RECONNECT_OK close/open between polls invalidates old USB heartbeat, preserves replay fence and Wi-Fi fallback, new bridge restores USB");
}
