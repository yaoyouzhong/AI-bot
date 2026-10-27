#include "host_ble_peers.h"
static atomic_bool ble_owner_verified;
static atomic_uint ble_owner_epoch,ble_owner_conn=BLE_HS_CONN_HANDLE_NONE;
static int64_t ble_owner_connected_at;
static bool ble_physical_connected;
bool ble_host_authorized(void) {
    return atomic_load(&ble_owner_verified)&&atomic_load(&ble_owner_epoch)==device_host_epoch()&&!device_host_changing();
}
static bool ble_owner_possible(uint16_t conn) {
    struct ble_gap_conn_desc peer={0};
    return ble_gap_conn_find(conn,&peer)==0&&device_host_ble_possible(peer.peer_id_addr.type,peer.peer_id_addr.val);
}
static bool ble_owner_authenticated(uint16_t conn,unsigned epoch) {
    struct ble_gap_conn_desc peer={0};
    if(conn!=atomic_load(&ble_owner_conn)||ble_gap_conn_find(conn,&peer)!=0||
       !device_host_ble_bind(epoch,peer.peer_id_addr.type,peer.peer_id_addr.val))return false;
    atomic_store(&ble_owner_epoch,epoch);atomic_store(&ble_owner_verified,true);ble_connected=true;return true;
}
static void ble_owner_tick(void) {
    uint16_t conn=(uint16_t)atomic_load(&ble_owner_conn);
    if(!ble_physical_connected||conn==BLE_HS_CONN_HANDLE_NONE)return;
    if(!ble_owner_possible(conn)||(atomic_load(&ble_owner_verified)&&!ble_host_authorized())||
       (!atomic_load(&ble_owner_verified)&&esp_timer_get_time()/1000-ble_owner_connected_at>=20000))
        ble_gap_terminate(conn,BLE_ERR_REM_USER_CONN_TERM);
    else if(ble_host_authorized()&&!ble_owner_authenticated(conn,device_host_epoch()))
        ble_gap_terminate(conn,BLE_ERR_REM_USER_CONN_TERM); // Persist a newly resolved identity only after this connection's proof.
}
static bool ble_owner_advertising(uint8_t *filter) {
    static bool configured;static int last_state=-2;static ble_addr_t last;
    ble_addr_t peer={0};int state=device_host_ble_target(&peer.type,peer.val);
    if(state<0){if(ble_gap_adv_active())ble_gap_adv_stop();configured=false;return false;}
    *filter=state==1?BLE_HCI_ADV_FILT_CONN:0;
    if(!configured||state!=last_state||(state==1&&memcmp(&last,&peer,sizeof(peer)))) {
        if(ble_gap_adv_active()&&ble_gap_adv_stop()!=0)return false;
        if(ble_gap_wl_set(state==1?&peer:NULL,state==1?1:0)!=0)return false;
        last=peer;last_state=state;configured=true;
    }
    return true;
}
