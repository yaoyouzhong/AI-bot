// Included by usb_link.c; proof comes only from an authenticated RPC response.
#include "ble_keyboard.h"
#include "ble_keyboard_flow.h"
#include "computer_remote.h"
static SemaphoreHandle_t uk_mutex;
static ble_keyboard_flow uk_flow;
static atomic_uchar uk_leds;
static unsigned uk_epoch,uk_authenticated_epoch;
static unsigned uk_host_epoch;
static unsigned uk_connection(void){return atomic_load(&cdc_epoch)^(device_host_epoch()*2654435761u);}
static int64_t uk_authenticated_at,uk_deadline,uk_last_digit;
static bool uk_wake,uk_display,uk_sending,uk_probe;
static char uk_request[37],uk_last_wake[37];
bool usb_keyboard_selected(void){int mode=device_get_config().mode;return mode==LINK_USB||(mode==-1&&device_host_usb_owned());}
static bool uk_ready(void) {
    int64_t now=esp_timer_get_time()/1000;
    return uk_host_epoch==device_host_epoch()&&device_host_usb_owned()&&!uk_probe&&tud_mounted()&&!tud_suspended()&&tab5_usb_connected()&&uk_authenticated_at>0&&
        uk_authenticated_epoch==atomic_load(&cdc_epoch)&&now>=uk_authenticated_at&&now-uk_authenticated_at<8000;
}
unsigned usb_keyboard_epoch(void){return atomic_load(&cdc_epoch);}
void usb_keyboard_authenticated(unsigned epoch,unsigned host_generation) {
    if(!uk_mutex)return;
    xSemaphoreTake(uk_mutex,portMAX_DELAY);
    if(epoch==atomic_load(&cdc_epoch)&&host_generation==device_host_epoch()&&!device_host_changing()){uk_authenticated_epoch=epoch;uk_host_epoch=host_generation;uk_authenticated_at=esp_timer_get_time()/1000;}
    xSemaphoreGive(uk_mutex);
}
void usb_keyboard_get(ble_keyboard_state *out) {
    memset(out,0,sizeof(*out));if(!uk_mutex)return;
    int mode=device_get_config().mode;if(mode!= -1&&mode!=LINK_USB)return;
    xSemaphoreTake(uk_mutex,portMAX_DELAY);bool ready=uk_ready();
    *out=(ble_keyboard_state){.connected=tud_mounted()&&tab5_usb_connected(),.secure=ready,.subscribed=tud_mounted(),
        .bridge_verified=ready,.ready=ready,.busy=uk_flow.busy||uk_sending,.finished=uk_flow.finished,.failed=uk_flow.failed,.connection=uk_connection()};
    xSemaphoreGive(uk_mutex);
}
static unsigned usb_keyboard_diagnostic(void){
    ble_keyboard_state s;usb_keyboard_get(&s);
    return (s.connected?1u:0u)|(s.secure?2u:0u)|(s.subscribed?4u:0u)|(s.bridge_verified?8u:0u)|(s.ready?16u:0u)|
        (tud_suspended()?32u:0u)|(s.busy?1u<<20:0u)|(s.failed?1u<<21:0u)|atomic_load(&tab5_unlock_diagnostic);
}
void usb_keyboard_cancel(void){if(!uk_mutex)return;xSemaphoreTake(uk_mutex,portMAX_DELAY);ble_keyboard_flow_cancel(&uk_flow);xSemaphoreGive(uk_mutex);}
bool usb_keyboard_probe_begin(void){if(!uk_mutex)return false;xSemaphoreTake(uk_mutex,portMAX_DELAY);bool ok=!uk_probe&&!uk_flow.busy&&!uk_sending;if(ok)uk_probe=true;xSemaphoreGive(uk_mutex);return ok;}
void usb_keyboard_probe_end(void){if(!uk_mutex)return;xSemaphoreTake(uk_mutex,portMAX_DELAY);uk_probe=false;xSemaphoreGive(uk_mutex);}
bool usb_keyboard_start(const char *pin,size_t n,unsigned connection,const char *id) {
    bool locked=false;int64_t updated=0;int display=-1;
    if(!uk_mutex||!id||strlen(id)!=36||!computer_remote_lock_state(&locked,&updated)||!locked||!computer_remote_display_state(id,&display)||display<1)return false;
    xSemaphoreTake(uk_mutex,portMAX_DELAY);
    bool ok=ble_keyboard_flow_start(&uk_flow,pin,n,!uk_sending&&connection==uk_connection()&&uk_ready());
    if(ok){strcpy(uk_request,id);uk_wake=uk_display=false;uk_deadline=esp_timer_get_time()/1000+10000;uk_last_digit=0;}
    xSemaphoreGive(uk_mutex);return ok;
}
bool usb_keyboard_wake(const char *id,unsigned connection,bool display) {
    bool locked=false,cover=false;int64_t updated=0;int screen=-1;
    if(!uk_mutex||!id||strlen(id)!=36||!computer_remote_lock_state(&locked,&updated)||!locked)return false;
    if(display){if(!computer_remote_display_state(id,&screen)||screen!=0)return false;}
    else if(!computer_remote_cover_state(id,&cover)||!cover)return false;
    xSemaphoreTake(uk_mutex,portMAX_DELAY);
    bool ready=!uk_sending&&connection==uk_connection()&&uk_ready()&&strcmp(id,uk_last_wake);
    bool ok=display?ble_keyboard_flow_display_wake(&uk_flow,ready):ble_keyboard_flow_wake(&uk_flow,ready);
    if(ok){strcpy(uk_request,id);strcpy(uk_last_wake,id);uk_wake=true;uk_display=display;uk_deadline=esp_timer_get_time()/1000+1000;}
    xSemaphoreGive(uk_mutex);return ok;
}
static void usb_keyboard_task(void *unused) {
    (void)unused;
    for(;;){
        bool locked=false,cover=false;int screen=-1;int mode=device_get_config().mode;int64_t updated=0,now=esp_timer_get_time()/1000;
        bool lock_ok=computer_remote_lock_state(&locked,&updated)&&locked;
        char request[37];xSemaphoreTake(uk_mutex,portMAX_DELAY);strcpy(request,uk_request);xSemaphoreGive(uk_mutex);
        computer_remote_cover_state(request,&cover);computer_remote_display_state(request,&screen);
        xSemaphoreTake(uk_mutex,portMAX_DELAY);
        unsigned epoch=atomic_load(&cdc_epoch);
        if(epoch!=uk_epoch||!tud_mounted()){
            ble_keyboard_flow_cancel(&uk_flow);uk_authenticated_at=0;uk_epoch=epoch;
            // On physical disconnect the host releases all keys; never replay on reconnect.
            if(!tud_mounted()){ble_keyboard_wipe(&uk_flow,sizeof(uk_flow));uk_flow.finished=uk_flow.failed=true;}
        }
        bool ready=uk_ready()&&(mode==-1||mode==LINK_USB)&&lock_ok&&now<uk_deadline;
        if(!uk_flow.release&&uk_wake)ready=ready&&!strcmp(request,uk_request)&&(uk_display?screen==0:cover);
        if(!uk_flow.release&&!uk_wake&&uk_flow.index==0)ready=ready&&!strcmp(request,uk_request)&&screen>0;
        if(!ready)ble_keyboard_flow_cancel(&uk_flow);
        bool waiting_enter=!uk_wake&&ready&&uk_flow.busy&&!uk_flow.release&&uk_flow.index+1==uk_flow.count&&updated<=uk_last_digit;
        uint8_t report[8]={0};
        if(!waiting_enter&&tud_hid_ready()&&ble_keyboard_flow_next(&uk_flow,ready,report)){
            ble_keyboard_caps_lock(report,(atomic_load(&uk_leds)&2)!=0);
            // Keep admission and queueing under the same lock; cancellation cannot race a key-down.
            uk_sending=true;
            bool sent=tud_hid_keyboard_report(0,report[0],report+2);
            if(sent&&!uk_wake&&report[2]&&report[2]!=0x28)uk_last_digit=now;
            ble_keyboard_flow_commit(&uk_flow,sent);uk_sending=false;
        }
        ble_keyboard_wipe(report,sizeof(report));xSemaphoreGive(uk_mutex);vTaskDelay(pdMS_TO_TICKS(40));
    }
}
static void usb_keyboard_init(void){uk_mutex=xSemaphoreCreateMutex();if(uk_mutex&&xTaskCreate(usb_keyboard_task,"usb-keyboard",4096,NULL,4,NULL)!=pdPASS){vSemaphoreDelete(uk_mutex);uk_mutex=NULL;}}
