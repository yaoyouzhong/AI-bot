// This file is owned by ble_link.c's existing NimBLE host. No second host,
// privileged Windows helper, or network PIN message is introduced.
#include "ble_keyboard.h"
#include "ble_keyboard_flow.h"
#include "computer_remote.h"
#include "host/ble_sm.h"
#include "host/ble_store.h"
static SemaphoreHandle_t keyboard_mutex;
static struct ble_npl_callout keyboard_callout;
static ble_keyboard_flow keyboard_flow;
static uint16_t keyboard_conn=BLE_HS_CONN_HANDLE_NONE,keyboard_report,keyboard_boot;
static uint8_t keyboard_protocol=1;
static uint8_t keyboard_leds;
static bool keyboard_report_subscribed,keyboard_boot_subscribed,keyboard_secure,keyboard_suspended;
static bool keyboard_bridge_verified,keyboard_pair_pending,keyboard_pair_repair;
static bool keyboard_sending;
static bool keyboard_waking,keyboard_display_waking;
static char keyboard_pin_request[37];
static char keyboard_wake_request[37];
static int keyboard_pair_answer=-1;
static uint32_t keyboard_comparison;
static unsigned keyboard_connection;
static unsigned keyboard_host_epoch;
static unsigned keyboard_host_connection(void){return keyboard_connection^(device_host_epoch()*2654435761u);}
static int64_t keyboard_pair_deadline,keyboard_send_deadline,keyboard_bridge_at,keyboard_last_digit_at;
static const uint8_t keyboard_map[]={
    0x05,0x01,0x09,0x06,0xa1,0x01,0x85,0x01,
    0x05,0x07,0x19,0xe0,0x29,0xe7,0x15,0x00,0x25,0x01,
    0x75,0x01,0x95,0x08,0x81,0x02,0x95,0x01,0x75,0x08,0x81,0x01,
    0x95,0x05,0x75,0x01,0x05,0x08,0x19,0x01,0x29,0x05,0x91,0x02,
    0x95,0x01,0x75,0x03,0x91,0x01,
    0x95,0x06,0x75,0x08,0x15,0x00,0x25,0x65,0x05,0x07,0x19,0x00,0x29,0x65,0x81,0x00,0xc0
};
static bool keyboard_ready_locked(void) {
    return !device_host_changing()&&keyboard_host_epoch==device_host_epoch()&&keyboard_conn!=BLE_HS_CONN_HANDLE_NONE&&keyboard_secure&&keyboard_bridge_verified&&esp_timer_get_time()/1000-keyboard_bridge_at<8000&&!keyboard_suspended&&
        (keyboard_protocol?keyboard_report_subscribed:keyboard_boot_subscribed)&&!keyboard_pair_pending;
}
void ble_keyboard_get(ble_keyboard_state *out) {
    memset(out,0,sizeof(*out));if(!keyboard_mutex)return;
    xSemaphoreTake(keyboard_mutex,portMAX_DELAY);
    *out=(ble_keyboard_state){.connected=keyboard_conn!=BLE_HS_CONN_HANDLE_NONE,.secure=keyboard_secure,
        .subscribed=keyboard_protocol?keyboard_report_subscribed:keyboard_boot_subscribed,.bridge_verified=keyboard_bridge_verified&&esp_timer_get_time()/1000-keyboard_bridge_at<8000,
        .ready=keyboard_ready_locked(),.busy=keyboard_flow.busy||keyboard_sending,.finished=keyboard_flow.finished,.failed=keyboard_flow.failed,
        .pairing=keyboard_pair_pending&&keyboard_pair_answer<0,.repair_pairing=keyboard_pair_repair,.comparison=keyboard_comparison,.connection=keyboard_host_connection()};
    xSemaphoreGive(keyboard_mutex);
}

atomic_uint tab5_unlock_diagnostic;
atomic_uint tab5_unlock_failure[3];
atomic_uint tab5_unlock_history[2+UNLOCK_HISTORY_COUNT*UNLOCK_HISTORY_WORDS];
atomic_uint tab5_unlock_history_received;
// Called only from the serialized NimBLE identity-read callback. Keep the
// host-stack workspace static, as with that callback's JSON/proof buffers.
unsigned ble_keyboard_diagnostic(void) {
    unsigned bits=0;uint16_t conn=BLE_HS_CONN_HANDLE_NONE;
    if(!keyboard_mutex)return atomic_load(&tab5_unlock_diagnostic);
    xSemaphoreTake(keyboard_mutex,portMAX_DELAY);
    conn=keyboard_conn;
    bool fresh=keyboard_bridge_verified&&esp_timer_get_time()/1000-keyboard_bridge_at<8000;
    bits=(conn!=BLE_HS_CONN_HANDLE_NONE?1u:0u)|(keyboard_secure?2u:0u)|
        ((keyboard_protocol?keyboard_report_subscribed:keyboard_boot_subscribed)?4u:0u)|
        (fresh?8u:0u)|(keyboard_ready_locked()?16u:0u)|(keyboard_suspended?32u:0u)|
        (keyboard_protocol?64u:0u)|(keyboard_report_subscribed?128u:0u)|
        (keyboard_boot_subscribed?256u:0u)|(keyboard_pair_pending?512u:0u)|
        (keyboard_flow.busy||keyboard_sending?1u<<20:0u)|(keyboard_flow.failed?1u<<21:0u)|
        (keyboard_bridge_verified?1u<<23:0u);
    xSemaphoreGive(keyboard_mutex);
    static struct ble_gap_conn_desc live;
    memset(&live,0,sizeof(live));
    if(conn!=BLE_HS_CONN_HANDLE_NONE&&ble_gap_conn_find(conn,&live)==0) {
        bits|=1u<<27;
        if(live.sec_state.encrypted)bits|=1u<<24;
        if(live.sec_state.authenticated)bits|=1u<<25;
        if(live.sec_state.bonded)bits|=1u<<26;
    }
    return bits|atomic_load(&tab5_unlock_diagnostic);
}
bool ble_keyboard_start(const char *pin,size_t n,unsigned connection,const char *id) {
    bool locked=false;int64_t updated=0;int display=-1;
    if(!keyboard_mutex||!id||strlen(id)!=36||!computer_remote_lock_state(&locked,&updated)||!locked||
       !computer_remote_display_state(id,&display)||display<1)return false;
    xSemaphoreTake(keyboard_mutex,portMAX_DELAY);
    bool ok=ble_keyboard_flow_start(&keyboard_flow,pin,n,!keyboard_sending&&connection==keyboard_host_connection()&&keyboard_ready_locked());
    if(ok){strcpy(keyboard_pin_request,id);keyboard_waking=keyboard_display_waking=false;keyboard_send_deadline=esp_timer_get_time()/1000+10000;keyboard_last_digit_at=0;}
    xSemaphoreGive(keyboard_mutex);return ok;
}
bool ble_keyboard_wake(const char *id,unsigned connection) {
    bool locked=false,cover=false;int64_t updated=0;
    if(!keyboard_mutex||!id||strlen(id)!=36||!computer_remote_lock_state(&locked,&updated)||!locked||
        !computer_remote_cover_state(id,&cover)||!cover)return false;
    xSemaphoreTake(keyboard_mutex,portMAX_DELAY);
    bool ok=ble_keyboard_flow_wake(&keyboard_flow,!keyboard_sending&&strcmp(id,keyboard_wake_request)&&connection==keyboard_host_connection()&&keyboard_ready_locked());
    if(ok){strcpy(keyboard_wake_request,id);keyboard_waking=true;keyboard_display_waking=false;keyboard_send_deadline=esp_timer_get_time()/1000+500;}
    xSemaphoreGive(keyboard_mutex);return ok;
}
bool ble_keyboard_display_wake(const char *id,unsigned connection) {
    bool locked=false;int64_t updated=0;int display=-1;
    if(!keyboard_mutex||!id||strlen(id)!=36||!computer_remote_lock_state(&locked,&updated)||!locked||
        !computer_remote_display_state(id,&display)||display!=0)return false;
    xSemaphoreTake(keyboard_mutex,portMAX_DELAY);
    bool ok=ble_keyboard_flow_display_wake(&keyboard_flow,!keyboard_sending&&strcmp(id,keyboard_wake_request)&&connection==keyboard_host_connection()&&keyboard_ready_locked());
    if(ok){strcpy(keyboard_wake_request,id);keyboard_waking=keyboard_display_waking=true;keyboard_send_deadline=esp_timer_get_time()/1000+500;}
    xSemaphoreGive(keyboard_mutex);return ok;
}
void ble_keyboard_cancel(void) {
    if(!keyboard_mutex)return;
    xSemaphoreTake(keyboard_mutex,portMAX_DELAY);ble_keyboard_flow_cancel(&keyboard_flow);xSemaphoreGive(keyboard_mutex);
}
void ble_keyboard_pair_confirm(bool accept) {
    if(!keyboard_mutex)return;
    xSemaphoreTake(keyboard_mutex,portMAX_DELAY);if(keyboard_pair_pending)keyboard_pair_answer=accept?1:0;xSemaphoreGive(keyboard_mutex);
}
static void keyboard_authenticated(unsigned host_generation) {
    xSemaphoreTake(keyboard_mutex,portMAX_DELAY);keyboard_host_epoch=host_generation;keyboard_bridge_verified=host_generation==device_host_epoch()&&!device_host_changing();keyboard_bridge_at=esp_timer_get_time()/1000;xSemaphoreGive(keyboard_mutex);
}
static int keyboard_access(uint16_t conn,uint16_t attr,struct ble_gatt_access_ctxt *ctx,void *arg) {
    (void)conn;(void)attr;uintptr_t which=(uintptr_t)arg;
    static const uint8_t info[]={0x11,0x01,0,0x02},input_ref[]={1,1},output_ref[]={1,2};
    // Device Information PnP ID uses the existing project's Espressif USB VID,
    // with a distinct product identity; no claim of Bluetooth qualification.
    static const uint8_t pnp[]={2,0x3a,0x30,0x51,0x40,0x00,0x01};
    uint8_t value=0,zeros[8]={0};const void *data=zeros;size_t length=8;
    if(ctx->op==BLE_GATT_ACCESS_OP_READ_DSC){data=which==5?input_ref:output_ref;length=2;}
    else if(ctx->op==BLE_GATT_ACCESS_OP_READ_CHR) {
        if(which==1){data=info;length=sizeof(info);}
        else if(which==2){data=keyboard_map;length=sizeof(keyboard_map);}
        else if(which==3){value=keyboard_protocol;data=&value;length=1;}
        else if(which==6){xSemaphoreTake(keyboard_mutex,portMAX_DELAY);value=keyboard_leds;xSemaphoreGive(keyboard_mutex);data=&value;length=1;}
        else if(which==7){data=pnp;length=sizeof(pnp);}
    }else if(ctx->op==BLE_GATT_ACCESS_OP_WRITE_CHR) {
        if(OS_MBUF_PKTLEN(ctx->om)!=1||os_mbuf_copydata(ctx->om,0,1,&value))return BLE_ATT_ERR_INVALID_ATTR_VALUE_LEN;
        if((which==3||which==4)&&value>1)return BLE_ATT_ERR_VALUE_NOT_ALLOWED;
        xSemaphoreTake(keyboard_mutex,portMAX_DELAY);
        if(which==3){keyboard_protocol=value;ble_keyboard_flow_cancel(&keyboard_flow);}
        if(which==4){keyboard_suspended=value==0;if(keyboard_suspended)ble_keyboard_flow_cancel(&keyboard_flow);}
        if(which==6)keyboard_leds=value;
        xSemaphoreGive(keyboard_mutex);return 0;
    }else return BLE_ATT_ERR_UNLIKELY;
    return os_mbuf_append(ctx->om,data,length)?BLE_ATT_ERR_INSUFFICIENT_RES:0;
}
#define KREAD (BLE_GATT_CHR_F_READ|BLE_GATT_CHR_F_READ_ENC|BLE_GATT_CHR_F_READ_AUTHEN)
#define KWRITE (BLE_GATT_CHR_F_WRITE|BLE_GATT_CHR_F_WRITE_NO_RSP|BLE_GATT_CHR_F_WRITE_ENC|BLE_GATT_CHR_F_WRITE_AUTHEN)
static const struct ble_gatt_svc_def keyboard_services[]={
 {.type=BLE_GATT_SVC_TYPE_PRIMARY,.uuid=BLE_UUID16_DECLARE(0x1812),.characteristics=(struct ble_gatt_chr_def[]){
  {.uuid=BLE_UUID16_DECLARE(0x2a4a),.access_cb=keyboard_access,.arg=(void*)1,.flags=KREAD},
  {.uuid=BLE_UUID16_DECLARE(0x2a4b),.access_cb=keyboard_access,.arg=(void*)2,.flags=KREAD},
  {.uuid=BLE_UUID16_DECLARE(0x2a4e),.access_cb=keyboard_access,.arg=(void*)3,.flags=KREAD|KWRITE},
  {.uuid=BLE_UUID16_DECLARE(0x2a4c),.access_cb=keyboard_access,.arg=(void*)4,.flags=KWRITE},
  {.uuid=BLE_UUID16_DECLARE(0x2a4d),.access_cb=keyboard_access,.flags=KREAD|BLE_GATT_CHR_F_NOTIFY,.val_handle=&keyboard_report,
    .descriptors=(struct ble_gatt_dsc_def[]){{.uuid=BLE_UUID16_DECLARE(0x2908),.att_flags=BLE_ATT_F_READ,.access_cb=keyboard_access,.arg=(void*)5},{0}}},
  {.uuid=BLE_UUID16_DECLARE(0x2a4d),.access_cb=keyboard_access,.arg=(void*)6,.flags=KREAD|KWRITE,
    .descriptors=(struct ble_gatt_dsc_def[]){{.uuid=BLE_UUID16_DECLARE(0x2908),.att_flags=BLE_ATT_F_READ,.access_cb=keyboard_access,.arg=(void*)6},{0}}},
  {.uuid=BLE_UUID16_DECLARE(0x2a22),.access_cb=keyboard_access,.flags=KREAD|BLE_GATT_CHR_F_NOTIFY,.val_handle=&keyboard_boot},
  {.uuid=BLE_UUID16_DECLARE(0x2a32),.access_cb=keyboard_access,.arg=(void*)6,.flags=KREAD|KWRITE},{0}}},
 {.type=BLE_GATT_SVC_TYPE_PRIMARY,.uuid=BLE_UUID16_DECLARE(0x180a),.characteristics=(struct ble_gatt_chr_def[]){
  {.uuid=BLE_UUID16_DECLARE(0x2a50),.access_cb=keyboard_access,.arg=(void*)7,.flags=BLE_GATT_CHR_F_READ},{0}}},{0}};
#undef KREAD
#undef KWRITE
static void keyboard_tick(struct ble_npl_event *event) {
    (void)event;bool locked=false;int64_t updated=0,now=esp_timer_get_time()/1000;
    bool lock_ok=computer_remote_lock_state(&locked,&updated)&&locked;
    bool cover=false;
    // Read outside keyboard_mutex, keeping the established lock order. Recheck
    // immediately before Space key-down; key-up is always best effort.
    char wake_request[37];xSemaphoreTake(keyboard_mutex,portMAX_DELAY);strcpy(wake_request,keyboard_wake_request);xSemaphoreGive(keyboard_mutex);
    bool cover_ok=computer_remote_cover_state(wake_request,&cover)&&cover;
    int display=-1;bool display_off=computer_remote_display_state(wake_request,&display)&&display==0;
    char pin_request[37];xSemaphoreTake(keyboard_mutex,portMAX_DELAY);strcpy(pin_request,keyboard_pin_request);xSemaphoreGive(keyboard_mutex);
    int pin_display=-1;bool display_on=computer_remote_display_state(pin_request,&pin_display)&&pin_display>=1;
    xSemaphoreTake(keyboard_mutex,portMAX_DELAY);
    if(keyboard_pair_pending&&(keyboard_pair_answer>=0||now>=keyboard_pair_deadline)) {
        struct ble_sm_io io={.action=BLE_SM_IOACT_NUMCMP,.numcmp_accept=keyboard_pair_answer==1&&now<keyboard_pair_deadline};
        uint16_t conn=keyboard_conn;bool repair=keyboard_pair_repair;keyboard_pair_pending=false;keyboard_pair_repair=false;keyboard_comparison=0;keyboard_pair_answer=-1;
        xSemaphoreGive(keyboard_mutex);
        if(repair){struct ble_gap_conn_desc d={0};if(io.numcmp_accept&&ble_gap_conn_find(conn,&d)==0)ble_store_util_delete_peer(&d.peer_id_addr);ble_gap_terminate(conn,BLE_ERR_REM_USER_CONN_TERM);}
        else ble_sm_inject_io(conn,&io);
        xSemaphoreTake(keyboard_mutex,portMAX_DELAY);
    }
    uint8_t report[8];bool ready=keyboard_ready_locked()&&lock_ok&&now<keyboard_send_deadline;
    if(keyboard_waking&&!keyboard_flow.release)ready=ready&&(keyboard_display_waking?display_off:cover_ok)&&!strcmp(wake_request,keyboard_wake_request);
    if(!keyboard_waking&&keyboard_flow.index==0&&!keyboard_flow.release)ready=ready&&display_on&&!strcmp(pin_request,keyboard_pin_request);
    // Numeric Windows Hello can submit itself on the last digit. Only emit
    // Enter after a newer status still says locked, not onto an unlocked app.
    bool waiting_enter=!keyboard_waking&&ready&&keyboard_flow.busy&&!keyboard_flow.release&&keyboard_flow.index+1==keyboard_flow.count&&updated<=keyboard_last_digit_at;
    if(!waiting_enter&&ble_keyboard_flow_next(&keyboard_flow,ready,report)) {
        ble_keyboard_caps_lock(report,(keyboard_leds&2)!=0);
        // Even an aborted transaction can send a zero report to release a key.
        uint16_t attr=keyboard_protocol?keyboard_report:keyboard_boot,conn=keyboard_conn;
        unsigned epoch=keyboard_connection;size_t index=keyboard_flow.index;bool release=keyboard_flow.release;
        bool connected=conn!=BLE_HS_CONN_HANDLE_NONE&&keyboard_secure;keyboard_sending=true;
        xSemaphoreGive(keyboard_mutex);
        struct os_mbuf *om=connected?ble_hs_mbuf_from_flat(report,8):NULL;
        bool sent=om&&ble_gatts_notify_custom(conn,attr,om)==0;
        xSemaphoreTake(keyboard_mutex,portMAX_DELAY);keyboard_sending=false;
        if(epoch==keyboard_connection) {
            if(sent&&!keyboard_waking&&report[2]&&report[2]!=0x28)keyboard_last_digit_at=now;
            if(!sent){ble_keyboard_wipe(&keyboard_flow,sizeof(keyboard_flow));keyboard_flow.finished=true;keyboard_flow.failed=true;}
            else if(keyboard_flow.index==index&&keyboard_flow.release==release&&(!keyboard_flow.failed||release))ble_keyboard_flow_commit(&keyboard_flow,true);
            else {ble_keyboard_flow_cancel(&keyboard_flow);keyboard_flow.release=report[0]!=0||report[2]!=0;keyboard_flow.busy=keyboard_flow.release;keyboard_flow.finished=!keyboard_flow.busy;}
        }
        if(!sent&&connected){xSemaphoreGive(keyboard_mutex);ble_gap_terminate(conn,BLE_ERR_REM_USER_CONN_TERM);xSemaphoreTake(keyboard_mutex,portMAX_DELAY);}
    }
    ble_keyboard_wipe(report,sizeof(report));xSemaphoreGive(keyboard_mutex);
    ble_npl_callout_reset(&keyboard_callout,ble_npl_time_ms_to_ticks32(40));
}
static int keyboard_gap_event(struct ble_gap_event *event) {
    xSemaphoreTake(keyboard_mutex,portMAX_DELAY);
    if(event->type==BLE_GAP_EVENT_CONNECT||event->type==BLE_GAP_EVENT_DISCONNECT) {
        ble_keyboard_wipe(&keyboard_flow,sizeof(keyboard_flow));keyboard_flow.finished=true;keyboard_flow.failed=true;
        keyboard_conn=event->type==BLE_GAP_EVENT_CONNECT&&event->connect.status==0?event->connect.conn_handle:BLE_HS_CONN_HANDLE_NONE;
        keyboard_connection++;keyboard_secure=false;keyboard_report_subscribed=keyboard_boot_subscribed=false;
        keyboard_bridge_verified=false;keyboard_pair_pending=false;keyboard_pair_repair=false;keyboard_comparison=0;keyboard_pair_answer=-1;keyboard_protocol=1;keyboard_suspended=false;
    }
    if(event->type==BLE_GAP_EVENT_ENC_CHANGE){struct ble_gap_conn_desc d={0};keyboard_secure=event->enc_change.status==0&&
        ble_gap_conn_find(event->enc_change.conn_handle,&d)==0&&d.sec_state.encrypted&&d.sec_state.authenticated&&d.sec_state.bonded;
        if(!keyboard_secure)ble_keyboard_flow_cancel(&keyboard_flow);}
    if(event->type==BLE_GAP_EVENT_SUBSCRIBE){if(event->subscribe.attr_handle==keyboard_report)keyboard_report_subscribed=event->subscribe.cur_notify;
        if(event->subscribe.attr_handle==keyboard_boot)keyboard_boot_subscribed=event->subscribe.cur_notify;}
    if(event->type==BLE_GAP_EVENT_NOTIFY_TX&&event->notify_tx.status!=0&&
       (event->notify_tx.attr_handle==keyboard_report||event->notify_tx.attr_handle==keyboard_boot))ble_keyboard_flow_cancel(&keyboard_flow);
    if(event->type==BLE_GAP_EVENT_PASSKEY_ACTION) {
        ble_keyboard_flow_cancel(&keyboard_flow);
        if(event->passkey.params.action==BLE_SM_IOACT_NUMCMP){keyboard_pair_pending=true;keyboard_pair_repair=false;keyboard_pair_answer=-1;
            keyboard_comparison=event->passkey.params.numcmp;keyboard_pair_deadline=esp_timer_get_time()/1000+30000;}
        else {uint16_t conn=event->passkey.conn_handle;xSemaphoreGive(keyboard_mutex);ble_gap_terminate(conn,BLE_ERR_AUTH_FAIL);return 0;}
    }
    if(event->type==BLE_GAP_EVENT_REPEAT_PAIRING){ble_keyboard_flow_cancel(&keyboard_flow);keyboard_pair_pending=true;keyboard_pair_repair=true;
        keyboard_pair_answer=-1;keyboard_comparison=0;keyboard_pair_deadline=esp_timer_get_time()/1000+30000;}
    xSemaphoreGive(keyboard_mutex);
    // A repeat request is ignored until a user explicitly clears that peer's
    // old bond in the TAB5 confirmation dialog, then pairs again in Windows.
    return event->type==BLE_GAP_EVENT_REPEAT_PAIRING?BLE_GAP_REPEAT_PAIRING_IGNORE:0;
}
void ble_store_config_init(void);
static bool keyboard_init(void) {
    keyboard_mutex=xSemaphoreCreateMutex();if(!keyboard_mutex)return false;
    if(ble_npl_callout_init(&keyboard_callout,nimble_port_get_dflt_eventq(),keyboard_tick,NULL))return false;
    ble_hs_cfg.sm_io_cap=BLE_SM_IO_CAP_DISP_YES_NO;ble_hs_cfg.sm_bonding=1;ble_hs_cfg.sm_mitm=1;ble_hs_cfg.sm_sc=1;
    ble_hs_cfg.sm_our_key_dist=BLE_SM_PAIR_KEY_DIST_ENC|BLE_SM_PAIR_KEY_DIST_ID;
    ble_hs_cfg.sm_their_key_dist=BLE_SM_PAIR_KEY_DIST_ENC|BLE_SM_PAIR_KEY_DIST_ID;
    ble_store_config_init();
    if(ble_gatts_count_cfg(keyboard_services)||ble_gatts_add_svcs(keyboard_services))return false;
    ble_npl_callout_reset(&keyboard_callout,ble_npl_time_ms_to_ticks32(40));return true;
}
