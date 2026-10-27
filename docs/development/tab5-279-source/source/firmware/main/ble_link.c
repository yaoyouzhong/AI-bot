#include "device.h"
#include "assets.h"
#include "ble_voice.h"
#include "rpc_link.h"
#include "ble_mailbox_window.h"
#include "ble_mailbox_paced.h"
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <stdatomic.h>
#include "esp_log.h"
#include "esp_timer.h"
#include "esp_heap_caps.h"
#include "esp_app_desc.h"
#include "nimble/nimble_port.h"
#include "nimble/nimble_port_freertos.h"
#include "host/ble_hs.h"
#include "host/util/util.h"
#include "services/gap/ble_svc_gap.h"
#include "services/gatt/ble_svc_gatt.h"
#include "ble_keyboard.inc"
volatile bool ble_available;
volatile bool ble_connected;
volatile int ble_disconnect_reason;
volatile unsigned ble_connect_count;
static uint8_t own_address_type;
static char challenge[33];
static uint64_t acknowledged;
#define PACKET_CAPACITY (TAB5_MAX_FRAME+4)
// Reassembly runs on the NimBLE host task, not an ISR or DMA engine. Keep the
// 64 KiB payload out of internal RAM needed by CSI/ISP and USB descriptors.
static uint8_t *packet;
static bool packet_init(void) {
    if(!packet)packet=heap_caps_malloc(PACKET_CAPACITY,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    return packet!=NULL;
}
static size_t used;
static int64_t last_write;
#include "ble_performance.inc"
#include "ble_owner.inc"
void ble_host_changed(void) {
    // Disconnect the previous central, retaining all OS keyboard bonds.
    atomic_store(&ble_owner_verified,false);ble_connected=false;
    uint16_t conn=(uint16_t)atomic_load(&ble_owner_conn);
    if(ble_available&&conn!=BLE_HS_CONN_HANDLE_NONE)ble_gap_terminate(conn,0x13);
}
typedef struct {uint16_t conn,attr;} mailbox_notify_target;
// A complete notification plus packet/header headroom fits one MSYS_2 block.
// Reserve 32 RPC + 8 voice + 8 spare blocks; do not borrow the small packet pool.
_Static_assert(CONFIG_BT_NIMBLE_MSYS_2_BLOCK_SIZE>=sizeof(struct os_mbuf)+sizeof(struct os_mbuf_pkthdr)+16+488,"Mailbox notification must fit one mbuf");
_Static_assert(CONFIG_BT_NIMBLE_MSYS_2_BLOCK_COUNT>=48,"Mailbox windows require bounded pool capacity");
static unsigned mailbox_submitted,mailbox_alloc_failed,mailbox_submit_failed,mailbox_min_free=UINT16_MAX;
static int mailbox_last_rc;
static bool mailbox_notify(const uint8_t *data,size_t size,void *ctx) {
    mailbox_notify_target *target=ctx;
    ble_performance_bulk(target->conn);
    // Select the reserved large pool even at MTU 23 or for a short final piece.
    struct os_mbuf *om=os_msys_get_pkthdr(488+16,0);
    if(!om){mailbox_alloc_failed++;mailbox_last_rc=BLE_HS_ENOMEM;return false;}
    unsigned remaining=os_msys_num_free();if(remaining<mailbox_min_free)mailbox_min_free=remaining;
    om->om_data+=16;
    if(os_mbuf_append(om,data,size)!=0){os_mbuf_free_chain(om);mailbox_alloc_failed++;mailbox_last_rc=BLE_HS_ENOMEM;return false;}
    // NimBLE owns the mbuf even when notification submission fails.
    int rc=ble_gatts_notify_custom(target->conn,target->attr,om);
    if(rc!=0){mailbox_submit_failed++;mailbox_last_rc=rc;return false;}
    mailbox_submitted++;return true;
}
#include "ble_voice.inc"
// UUID bytes are in Bluetooth little-endian order.
static const ble_uuid128_t service_id=BLE_UUID128_INIT(0x01,0x01,0x32,0x19,0x7a,0x66,0x65,0xbc,0x91,0x4a,0x23,0x7f,0x01,0x00,0xf5,0x7a);
static const ble_uuid128_t info_id=BLE_UUID128_INIT(0x01,0x01,0x32,0x19,0x7a,0x66,0x65,0xbc,0x91,0x4a,0x23,0x7f,0x02,0x00,0xf5,0x7a);
static const ble_uuid128_t input_id=BLE_UUID128_INIT(0x01,0x01,0x32,0x19,0x7a,0x66,0x65,0xbc,0x91,0x4a,0x23,0x7f,0x03,0x00,0xf5,0x7a);
static const ble_uuid128_t host_id=BLE_UUID128_INIT(0x01,0x01,0x32,0x19,0x7a,0x66,0x65,0xbc,0x91,0x4a,0x23,0x7f,0x07,0x00,0xf5,0x7a);
static int host_info_access(uint16_t conn,uint16_t attr,struct ble_gatt_access_ctxt *ctx,void *arg) {
    (void)conn;(void)attr;(void)arg;if(ctx->op!=BLE_GATT_ACCESS_OP_READ_CHR)return BLE_ATT_ERR_UNLIKELY;
    char id[33],name[TRUSTED_HOST_NAME],json[112];device_host_identity(id,name);
    int n=snprintf(json,sizeof(json),"{\"version\":1,\"hostId\":\"%s\",\"bleProofRequired\":1}",id);
    return n>0&&os_mbuf_append(ctx->om,json,n)==0?0:BLE_ATT_ERR_INSUFFICIENT_RES;
}
static const ble_uuid128_t voice_id=BLE_UUID128_INIT(0x01,0x01,0x32,0x19,0x7a,0x66,0x65,0xbc,0x91,0x4a,0x23,0x7f,0x04,0x00,0xf5,0x7a);
static const ble_uuid128_t rpc_id=BLE_UUID128_INIT(0x01,0x01,0x32,0x19,0x7a,0x66,0x65,0xbc,0x91,0x4a,0x23,0x7f,0x05,0x00,0xf5,0x7a);
static size_t rpc_packet_read(uint8_t *data,size_t size,void *ctx){(void)ctx;return rpc_link_read(LINK_BLE,data,size);}
static bool rpc_packet_write(const uint8_t *data,size_t size,void *ctx){(void)ctx;return rpc_link_write(LINK_BLE,data,size);}
#include "ble_probe.inc"
static struct ble_npl_callout rpc_notify_callout;
static ble_mailbox_paced rpc_notify_state;
static mailbox_notify_target rpc_notify_target;
static int64_t rpc_notify_started;
static unsigned rpc_notify_yields,rpc_notify_aborts;
static bool rpc_notify_room(void *ctx) {
    (void)ctx;
    // Include both pools and leave all but eight blocks free. This bounds
    // outstanding queue memory instead of assuming notify_tx means air TX.
    return os_msys_num_free()>CONFIG_BT_NIMBLE_MSYS_1_BLOCK_COUNT+CONFIG_BT_NIMBLE_MSYS_2_BLOCK_COUNT-8;
}
static void rpc_notify_tick(struct ble_npl_event *event) {
    (void)event;if(!rpc_notify_state.remaining)return;
    if(!ble_connected||perf_connection!=rpc_notify_target.conn||esp_timer_get_time()/1000-rpc_notify_started>=3000) {
        rpc_notify_state.remaining=0;rpc_notify_aborts++;return;
    }
    int rc=ble_mailbox_paced_step(&rpc_notify_state,rpc_notify_room,rpc_packet_read,rpc_packet_write,mailbox_notify,&rpc_notify_target);
    if(rc<0){rpc_notify_aborts++;return;}
    if(rc>0){rpc_notify_yields++;ble_npl_callout_reset(&rpc_notify_callout,ble_npl_time_ms_to_ticks32(2));}
}
static int rpc_access(uint16_t conn,uint16_t attr,struct ble_gatt_access_ctxt *ctx,void *arg) {
    (void)conn;(void)attr;(void)arg;uint8_t data[488];
    if(!device_get_config().paired||!ble_host_authorized()||conn!=atomic_load(&ble_owner_conn))return BLE_ATT_ERR_INSUFFICIENT_AUTHEN;
    if(ctx->op==BLE_GATT_ACCESS_OP_READ_CHR) {
        size_t n=rpc_link_read(LINK_BLE,data,sizeof(data));
        return n&&os_mbuf_append(ctx->om,data,n)==0?0:BLE_ATT_ERR_INSUFFICIENT_RES;
    }
    unsigned n=OS_MBUF_PKTLEN(ctx->om);
    if(n>sizeof(data)||ble_hs_mbuf_to_flat(ctx->om,data,n,NULL))return BLE_ATT_ERR_INVALID_ATTR_VALUE_LEN;
    if(n==8&&data[0]==3) {
        ble_performance_bulk(conn);
        size_t capacity=ble_att_mtu(conn)-3;if(capacity>488)capacity=488;
        if(!ble_mailbox_paced_start(&rpc_notify_state,data,n,capacity,rpc_packet_write,NULL))return BLE_ATT_ERR_UNLIKELY;
        rpc_notify_target=(mailbox_notify_target){conn,attr};rpc_notify_started=esp_timer_get_time()/1000;
        ble_npl_callout_reset(&rpc_notify_callout,1);return 0;
    }
    uint32_t began=(uint32_t)esp_timer_get_time();
    if(n>9)ble_performance_bulk(conn);
    bool ok=rpc_link_write(LINK_BLE,data,n);
    if(n>9&&data[0]==2)ble_probe_received(n-9,began,(uint32_t)esp_timer_get_time(),ok);
    // Valid-size packets can still fail id/cursor/mailbox-state checks. Do
    // not report those rejections as an attribute-length error to Windows.
    return ok?0:BLE_ATT_ERR_UNLIKELY;
}
static int telemetry_info_access(uint16_t conn,uint16_t attr,struct ble_gatt_access_ctxt *ctx,void *arg) {
    (void)conn;(void)attr;(void)arg;
    device_config cfg=device_get_config();if(!cfg.paired)return BLE_ATT_ERR_INSUFFICIENT_AUTHEN;
    if(ctx->op==BLE_GATT_ACCESS_OP_READ_CHR) {
        // GATT callbacks run serially on nimble_host. Its 4096-byte task stack
        // must also accommodate newlib formatting and an interrupt frame.
        // Keep this host-owned workspace out of the callback's local frame.
        static char message[100],proof[65],json[513],ids[27],asset_proof[65],firmware_proof[65];
        snprintf(message,sizeof(message),"%s|%llu",challenge,(unsigned long long)acknowledged);device_proof(message,proof);
        xSemaphoreTake(device_mutex,portMAX_DELAY);assets_ids(ids);xSemaphoreGive(device_mutex);
        snprintf(message,sizeof(message),"ASSETS|%s|%s",challenge,ids);device_proof(message,asset_proof);
        const char *firmware=esp_app_get_description()->version;
        snprintf(message,sizeof(message),"FIRMWARE|%s|%s",challenge,firmware);device_proof(message,firmware_proof);
        // ATT attribute values are limited to 512 bytes, even with MTU 517.
        // rpcMailboxWindow defaults to mailboxWindow (8) in existing clients.
        // RPC writes copy directly into the existing bounded reassembly buffer;
        // they do not enqueue outgoing notifications. Negotiate that direction
        // separately; old bridges and voice keep eight-packet barriers. The
        // maximum-width identity including this field must still fit 512 bytes.
        int n=snprintf(json,sizeof(json),"{\"version\":1,\"telemetryVersion\":3,\"mailboxWindow\":8,\"rpcNotifyWindow\":32,\"rpcWriteWindow\":64,\"rpcReplay\":1,\"deviceId\":\"%s\",\"nonce\":\"%s\",\"ack\":%llu,\"proof\":\"%s\",\"assetIds\":\"%s\",\"assetProof\":\"%s\",\"firmware\":\"%s\",\"firmwareProof\":\"%s\"}",cfg.id,challenge,(unsigned long long)acknowledged,proof,ids,asset_proof,firmware,firmware_proof);
        if(n<0||(size_t)n>=sizeof(json))return BLE_ATT_ERR_INSUFFICIENT_RES;
        // Optional read-only state; preserve every identity field and the ATT
        // 512-byte ceiling. A maximum-width future identity can omit it.
        static char diagnostic[32];static bool failure_first;
        unsigned failure=0,failure_at=0;bool snapshot=unlock_failure_snapshot(&failure,&failure_at);
        // Alternate priority when only one optional field fits the ATT ceiling.
        // Both remain diagnostics, never identity proofs or input authorization.
        failure_first=!failure_first;
        for(int i=0;i<2;i++) {
            bool history=(i==0)==failure_first;if(history&&!snapshot)continue;
            int extra=history?snprintf(diagnostic,sizeof(diagnostic),",\"f\":\"%08X\"}",failure):
                snprintf(diagnostic,sizeof(diagnostic),",\"k\":%u}",ble_keyboard_diagnostic());
            n=(int)strlen(json);
            if(extra>0&&(size_t)extra<sizeof(diagnostic)&&(size_t)(n-1+extra)<sizeof(json))
                memcpy(json+n-1,diagnostic,(size_t)extra+1);
        }
        return os_mbuf_append(ctx->om,json,strlen(json))==0?0:BLE_ATT_ERR_INSUFFICIENT_RES;
    }
    return BLE_ATT_ERR_UNLIKELY;
}
// Separate callbacks keep the identity JSON buffers off the complete-frame
// decrypt/parse/delta call chain, even when compiled with size optimization.
#include "ble_owner_proof.inc"
static int telemetry_input_access(uint16_t conn,uint16_t attr,struct ble_gatt_access_ctxt *ctx,void *arg) {
    (void)conn;(void)attr;(void)arg;
    if(!device_get_config().paired||!ble_owner_possible(conn))return BLE_ATT_ERR_INSUFFICIENT_AUTHEN;
    if(ctx->op!=BLE_GATT_ACCESS_OP_WRITE_CHR)return BLE_ATT_ERR_UNLIKELY;
    int64_t now=esp_timer_get_time()/1000;if(now-last_write>5000)used=0;last_write=now;
    uint16_t length=OS_MBUF_PKTLEN(ctx->om);
    if(!packet){used=0;return BLE_ATT_ERR_INSUFFICIENT_RES;}
    if(used+length>PACKET_CAPACITY){used=0;return BLE_ATT_ERR_INVALID_ATTR_VALUE_LEN;}
    if(ble_hs_mbuf_to_flat(ctx->om,packet+used,length,NULL)!=0){used=0;return BLE_ATT_ERR_UNLIKELY;}
    used+=length;
    if(used>=4) {
        uint32_t size=(uint32_t)packet[0]|((uint32_t)packet[1]<<8)|((uint32_t)packet[2]<<16)|((uint32_t)packet[3]<<24);
        if(size<28||size>TAB5_MAX_FRAME||used>size+4){used=0;return BLE_ATT_ERR_INVALID_ATTR_VALUE_LEN;}
        if(used==size+4) {
            unsigned owner=device_host_epoch();char *clear=device_decrypt(packet+4,size,challenge);used=0;
            if(!clear)return BLE_ATT_ERR_INSUFFICIENT_AUTHEN;
            uint64_t sequence=0;bool ok=ble_owner_proof(clear,&sequence)||device_receive_for_host(clear,LINK_BLE,&sequence,owner);free(clear);
            if(ok&&ble_owner_authenticated(conn,owner)){acknowledged=sequence;keyboard_authenticated(owner);}
            else if(ok){ble_gap_terminate(conn,BLE_ERR_REM_USER_CONN_TERM);return BLE_ATT_ERR_INSUFFICIENT_AUTHEN;}
            else return BLE_ATT_ERR_UNLIKELY;
        }
    }
    return 0;
}
static const struct ble_gatt_svc_def services[]={
    {.type=BLE_GATT_SVC_TYPE_PRIMARY,.uuid=&service_id.u,.characteristics=(struct ble_gatt_chr_def[]){
        {.uuid=&info_id.u,.access_cb=telemetry_info_access,.flags=BLE_GATT_CHR_F_READ},
        {.uuid=&input_id.u,.access_cb=telemetry_input_access,.flags=BLE_GATT_CHR_F_WRITE|BLE_GATT_CHR_F_WRITE_NO_RSP},
        {.uuid=&host_id.u,.access_cb=host_info_access,.flags=BLE_GATT_CHR_F_READ},
        {.uuid=&voice_id.u,.access_cb=voice_access,.flags=BLE_GATT_CHR_F_READ|BLE_GATT_CHR_F_WRITE|BLE_GATT_CHR_F_WRITE_NO_RSP|BLE_GATT_CHR_F_NOTIFY},
        {.uuid=&rpc_id.u,.access_cb=rpc_access,.flags=BLE_GATT_CHR_F_READ|BLE_GATT_CHR_F_WRITE|BLE_GATT_CHR_F_WRITE_NO_RSP|BLE_GATT_CHR_F_NOTIFY},{0}}},{0}};
static void advertise(void);
static struct ble_npl_callout advertising_callout;
static int advertising_error;
static int gap_event(struct ble_gap_event *event,void *arg) {
    if(event->type==BLE_GAP_EVENT_CONNECT){
        atomic_store(&ble_owner_verified,false);ble_connected=false;
        ble_physical_connected=event->connect.status==0;
        atomic_store(&ble_owner_conn,ble_physical_connected?event->connect.conn_handle:BLE_HS_CONN_HANDLE_NONE);
        ble_owner_connected_at=esp_timer_get_time()/1000;
        if(ble_physical_connected&&!ble_owner_possible(event->connect.conn_handle)){ble_gap_terminate(event->connect.conn_handle,BLE_ERR_REM_USER_CONN_TERM);return 0;}
    }
    if(event->type==BLE_GAP_EVENT_DISCONNECT){atomic_store(&ble_owner_verified,false);atomic_store(&ble_owner_conn,BLE_HS_CONN_HANDLE_NONE);ble_physical_connected=false;ble_connected=false;}
    if(event->type==BLE_GAP_EVENT_ENC_CHANGE||event->type==BLE_GAP_EVENT_IDENTITY_RESOLVED)ble_owner_tick();
    int keyboard_result=keyboard_gap_event(event);
    if(event->type==BLE_GAP_EVENT_REPEAT_PAIRING)return keyboard_result;
    if(event->type==BLE_GAP_EVENT_CONNECT||event->type==BLE_GAP_EVENT_DISCONNECT){rpc_notify_state.remaining=0;ble_npl_callout_stop(&rpc_notify_callout);voice_reset();rpc_link_reset(LINK_BLE);}
    if(event->type==BLE_GAP_EVENT_CONNECT){used=0;acknowledged=0;device_nonce(challenge);if(ble_physical_connected){
        ble_connect_count++;
        perf_connection=event->connect.conn_handle;perf_bulk=false;perf_tx_octets=perf_rx_octets=0;
        perf_interval_rc=perf_phy_rc=perf_length_rc=0;
        memset(&perf_idle,0,sizeof(perf_idle));ble_gap_conn_find(perf_connection,&perf_idle);
        ble_probe_connection(0,&perf_idle);
        // A 488-byte mailbox fits one read at MTU 517, avoiding read-blob
        // round trips for every photo fragment. Smaller peers still negotiate.
        ble_gattc_exchange_mtu(event->connect.conn_handle,NULL,NULL);
    }else {ble_disconnect_reason=event->connect.status;advertise();}}
    if(event->type==BLE_GAP_EVENT_DATA_LEN_CHG){perf_tx_octets=event->data_len_chg.max_tx_octets;perf_rx_octets=event->data_len_chg.max_rx_octets;}
    if(event->type==BLE_GAP_EVENT_CONN_UPDATE) {
        struct ble_gap_conn_desc current={0};
        if(ble_gap_conn_find(event->conn_update.conn_handle,&current)==0)ble_probe_connection(event->conn_update.status,&current);
    }
    if(event->type==BLE_GAP_EVENT_DISCONNECT||event->type==BLE_GAP_EVENT_ADV_COMPLETE){if(event->type==BLE_GAP_EVENT_DISCONNECT) {ble_connected=false;ble_disconnect_reason=event->disconnect.reason;perf_connection=BLE_HS_CONN_HANDLE_NONE;perf_bulk=false;}used=0;acknowledged=0;device_nonce(challenge);advertise();}
    return 0;
}
#include "ble_advertising.inc"
static void advertising_tick(struct ble_npl_event *event) {
    (void)event;
    ble_owner_tick();ble_performance_idle();
    advertise_now();
    // Retry from the host queue after a failed start or a lost completion event.
    // Never restart an active advertisement or an established connection.
    ble_npl_callout_reset(&advertising_callout,ble_npl_time_ms_to_ticks32(2000));
}
static void advertise(void) {
    ble_npl_callout_reset(&advertising_callout,1);
}
static void sync_host(void) {
    if(ble_hs_util_ensure_addr(0)==0&&ble_hs_id_infer_auto(0,&own_address_type)==0) {
        // Existing OS bonds may cache the service layout from an older image.
        // Queue the standard indication before advertising; NimBLE retains it
        // for subscribed bonded peers until they reconnect and acknowledge it.
        ble_svc_gatt_changed(1,0xffff);
        ble_available=true;advertise();
    }
}
static void host_task(void *arg) {nimble_port_run();nimble_port_freertos_deinit();}
void ble_start(void) {
    if(!packet_init()){ESP_LOGE("tab5","BLE receive buffer allocation failed");return;}
    voice_lock=xSemaphoreCreateMutex();
    voice_out=heap_caps_malloc(VOICE_CAPACITY,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    voice_in=heap_caps_malloc(VOICE_CAPACITY,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    if(!voice_out||!voice_in||!voice_lock){free(voice_out);free(voice_in);if(voice_lock)vSemaphoreDelete(voice_lock);voice_lock=NULL;}
    esp_err_t ret=nimble_port_init();if(ret!=ESP_OK){ESP_LOGE("tab5","BLE init failed: %s",esp_err_to_name(ret));return;}
    if(ble_att_set_preferred_mtu(517)!=0)ESP_LOGW("tab5","Using configured BLE MTU fallback");
    if(ble_npl_callout_init(&advertising_callout,nimble_port_get_dflt_eventq(),advertising_tick,NULL)!=0){ESP_LOGE("tab5","BLE advertising recovery timer failed");return;}
    if(ble_npl_callout_init(&rpc_notify_callout,nimble_port_get_dflt_eventq(),rpc_notify_tick,NULL)!=0){ESP_LOGE("tab5","BLE mailbox timer failed");return;}
    ble_svc_gap_init();ble_svc_gatt_init();device_config cfg=device_get_config();char name[28];snprintf(name,sizeof(name),"AI-bot TAB5 %.6s",cfg.id+6);ble_svc_gap_device_name_set(name);
    if(ble_gatts_count_cfg(services)||ble_gatts_add_svcs(services)||!keyboard_init()){ESP_LOGE("tab5","BLE GATT registration failed");return;}
    device_nonce(challenge);ble_hs_cfg.sync_cb=sync_host;nimble_port_freertos_init(host_task);
}

