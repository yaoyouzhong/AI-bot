#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include "link_state.h"
typedef struct {void *out,*in;int64_t peer_seen,confirmed_ms;char confirmed_session[40];} rpc_mailbox;
static rpc_mailbox mailboxes[3];
static link_state device_links;
static bool wifi_connected,usb_connected;
static int device_mutex,rpc_lock=1;
static int64_t clock_ms;
#define portMAX_DELAY 0
static void xSemaphoreTake(int lock,int timeout){(void)lock;(void)timeout;}
static void xSemaphoreGive(int lock){(void)lock;}
static bool tab5_usb_connected(void){return usb_connected;}
static int64_t esp_timer_get_time(void){return clock_ms*1000;}
static rpc_mailbox *box(int link){return &mailboxes[link];}
#include "rpc_library_route_under_test.inc"
static void reset(void) {
    clock_ms=101;wifi_connected=usb_connected=true;link_state_init(&device_links);
    for(int i=0;i<3;i++) {
        assert(link_state_receive(&device_links,i,"paired-session",1,100));
        mailboxes[i]=(rpc_mailbox){.out=(void *)1,.in=(void *)1,.peer_seen=100,.confirmed_ms=100};
        strcpy(mailboxes[i].confirmed_session,"paired-session");
    }
}
int main(void) {
    reset();assert(rpc_link_select()==LINK_USB&&rpc_read_link_select()==LINK_USB);
    assert(rpc_library_link_select()==LINK_WIFI&&device_links.mode==-1);
    wifi_connected=false;assert(rpc_library_link_select()==LINK_USB);
    usb_connected=false;assert(rpc_library_link_select()==LINK_BLE);
    reset();for(int mode=LINK_USB;mode<=LINK_BLE;mode++) {
        device_links.mode=mode;assert(rpc_library_link_select()==mode);
        assert(rpc_read_link_select()==mode&&rpc_link_select()==mode);
    }
    reset();clock_ms=8100;mailboxes[LINK_USB].peer_seen=mailboxes[LINK_USB].confirmed_ms=8099;
    assert(rpc_library_link_select()==LINK_USB); // Authenticated read freshness survives a slow bulk request.
    strcpy(mailboxes[LINK_USB].confirmed_session,"wrong-session");assert(rpc_library_link_select()==LINK_NONE);
    reset();device_links.received_ms[LINK_WIFI]=102;assert(rpc_library_link_select()==LINK_USB);
    reset();link_state_disconnect(&device_links,LINK_WIFI);assert(rpc_library_link_select()==LINK_USB);
    reset();wifi_connected=false;mailboxes[LINK_USB].peer_seen=102;assert(rpc_library_link_select()==LINK_BLE);
    reset();device_links.mode=LINK_WIFI;wifi_connected=false;assert(rpc_library_link_select()==LINK_NONE);
    reset();device_links.mode=LINK_USB;usb_connected=false;assert(rpc_library_link_select()==LINK_NONE);
    puts("GALLERY_BULK_ROUTE_OK actual production route; auto WiFi priority; USB fallback; fixed modes; fresh authenticated session; ordinary foreground unchanged");
}
