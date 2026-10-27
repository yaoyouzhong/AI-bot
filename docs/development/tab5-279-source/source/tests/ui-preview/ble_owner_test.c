#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <string.h>
#include <stdatomic.h>
#include <stdio.h>
#define BLE_HS_CONN_HANDLE_NONE 65535
#define BLE_ERR_REM_USER_CONN_TERM 19
#define BLE_HCI_ADV_FILT_CONN 2
typedef struct {uint8_t type,val[6];} ble_addr_t;
struct ble_gap_conn_desc {ble_addr_t peer_id_addr;};
static unsigned epoch=1;static bool changing,ble_connected,active,wl_failed;
static int target,terminated,stops,whitelist_count;static int64_t now;
static ble_addr_t selected={0,{1,2,3,4,5,6}},central,whitelist;
static unsigned device_host_epoch(void){return epoch;}
static bool device_host_changing(void){return changing;}
static int64_t esp_timer_get_time(void){return now*1000;}
static int device_host_ble_target(uint8_t *type,uint8_t addr[6]){*type=selected.type;memcpy(addr,selected.val,6);return changing?-1:target;}
static bool device_host_ble_possible(uint8_t type,const uint8_t addr[6]){return target>=0&&!changing&&(target==0||(type==selected.type&&!memcmp(addr,selected.val,6)));}
static bool device_host_ble_bind(unsigned request_epoch,uint8_t type,const uint8_t addr[6]){return request_epoch==epoch&&device_host_ble_possible(type,addr);}
static int ble_gap_conn_find(uint16_t conn,struct ble_gap_conn_desc *out){(void)conn;out->peer_id_addr=central;return 0;}
static int ble_gap_terminate(uint16_t conn,int reason){assert(conn==7&&reason==19);terminated++;return 0;}
static bool ble_gap_adv_active(void){return active;}
static int ble_gap_adv_stop(void){stops++;active=false;return 0;}
static int ble_gap_wl_set(const ble_addr_t *addresses,uint8_t count){if(wl_failed)return 1;whitelist_count=count;if(count)whitelist=*addresses;return 0;}
#include "../../firmware/main/ble_owner.inc"
int main(void) {
    uint8_t filter=99;target=-1;active=true;assert(!ble_owner_advertising(&filter)&&!active);
    target=0;assert(ble_owner_advertising(&filter)&&filter==0&&whitelist_count==0);
    target=1;active=true;assert(ble_owner_advertising(&filter)&&filter==2&&whitelist_count==1&&!memcmp(&whitelist,&selected,sizeof(selected))&&!active);
    central=selected;ble_physical_connected=true;atomic_store(&ble_owner_conn,7);ble_owner_connected_at=0;
    assert(!ble_host_authorized());now=19999;ble_owner_tick();assert(!terminated);
    now=20000;ble_owner_tick();assert(terminated==1&&!ble_host_authorized());
    assert(!ble_owner_authenticated(7,epoch-1));
    assert(ble_owner_authenticated(7,epoch)&&ble_host_authorized()&&ble_connected);
    now=90000;ble_owner_tick();assert(terminated==1); // Same owner may remain idle.
    epoch++;assert(!ble_host_authorized());ble_owner_tick();assert(terminated==2);
    selected.val[0]=99;assert(!ble_owner_authenticated(7,epoch));
    active=true;assert(ble_owner_advertising(&filter)&&!active&&whitelist.val[0]==99);
    central=selected;assert(ble_owner_authenticated(7,epoch));
    changing=true;assert(!ble_host_authorized()&&!ble_owner_advertising(&filter));changing=false;
    selected.val[0]=88;wl_failed=true;assert(!ble_owner_advertising(&filter));
    wl_failed=false;assert(ble_owner_advertising(&filter)&&whitelist.val[0]==88);
    target=-1;assert(!ble_owner_advertising(&filter));
    puts("BLE_OWNER_TEST_OK unknown peer gets no authority; authentication deadline; selected peer filter; old epoch closed; no foreign takeover; filter failure closed");
}
