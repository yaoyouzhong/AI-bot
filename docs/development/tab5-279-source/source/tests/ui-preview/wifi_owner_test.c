#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#define LINK_WIFI 1
#define LINK_USB 0
#define portMAX_DELAY 0
typedef struct {int mode;} device_config;
typedef struct {bool ready;} ble_keyboard_state;
static bool wifi_connected=true,changing,usb_ready;
static int mode=-1,device_mutex,selected=1;
static int64_t now=1000;
static struct {int64_t received_ms[3];char session[3][40];} device_links;
static bool device_host_changing(void){return changing;}
static device_config device_get_config(void){return (device_config){mode};}
static void usb_keyboard_get(ble_keyboard_state *state){state->ready=usb_ready;}
static void xSemaphoreTake(int mutex,int wait){(void)mutex;(void)wait;}
static void xSemaphoreGive(int mutex){(void)mutex;}
static int64_t esp_timer_get_time(void){return now*1000;}
#include "wifi_owner_route_under_test.inc"
int main(void) {
    device_links.received_ms[LINK_WIFI]=now;device_links.session[LINK_WIFI][0]='A';
    assert(unlock_remote_wifi_only()&&selected==1); // No physical foreign USB/BLE flag can select another computer.
    usb_ready=true;assert(!unlock_remote_wifi_only());usb_ready=false;
    changing=true;assert(!unlock_remote_wifi_only()&&selected==1);changing=false;
    mode=LINK_USB;assert(!unlock_remote_wifi_only());mode=LINK_WIFI;assert(unlock_remote_wifi_only());mode=-1;
    now+=8000;assert(!unlock_remote_wifi_only()&&selected==1);
    device_links.received_ms[LINK_WIFI]=now+1;assert(!unlock_remote_wifi_only());
    device_links.received_ms[LINK_WIFI]=now;device_links.session[LINK_WIFI][0]=0;assert(!unlock_remote_wifi_only());
    device_links.session[LINK_WIFI][0]='A';wifi_connected=false;assert(!unlock_remote_wifi_only()&&selected==1);
    puts("WIFI_OWNER_TEST_OK selected-host Wi-Fi eligible; trusted USB priority; no foreign fallback; switching/stale/future/unauthenticated/offline closed");
}
