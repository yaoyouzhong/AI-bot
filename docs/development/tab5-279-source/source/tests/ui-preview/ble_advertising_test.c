#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#define BLE_HS_ADV_F_DISC_GEN 1
#define BLE_HS_ADV_F_BREDR_UNSUP 2
#define BLE_GAP_CONN_MODE_UND 1
#define BLE_GAP_DISC_MODE_GEN 1
#define BLE_HS_FOREVER -1
#define ESP_LOGW(...) ((void)0)
#define ESP_LOGI(...) ((void)0)
typedef int ble_uuid128_t;
typedef uint16_t ble_uuid16_t;
#define BLE_UUID16_INIT(v) (v)
struct ble_hs_adv_fields {ble_uuid16_t *uuids16;int num_uuids16,uuids16_is_complete,appearance,appearance_is_present;int flags;ble_uuid128_t *uuids128;int num_uuids128,uuids128_is_complete;uint8_t *name;int name_len,name_is_complete;};
struct ble_gap_adv_params {int conn_mode,disc_mode,filter_policy;};
static bool ble_connected,synced=true,active;
#define ble_physical_connected ble_connected
static bool ble_owner_advertising(uint8_t *filter){*filter=0;return true;}
static int advertising_error,service_id,own_address_type;
static int failure,fields_calls,response_calls,start_calls;
static int ble_hs_synced(void){return synced;}
static int ble_gap_adv_active(void){return active;}
static int ble_gap_adv_set_fields(struct ble_hs_adv_fields *f){assert(f->num_uuids128==1);assert(f->num_uuids16==1&&*f->uuids16==0x1812&&f->appearance==0x03c1&&f->appearance_is_present);assert(3+18+4+4<=31);fields_calls++;return failure==1?11:0;}
static const char *ble_svc_gap_device_name(void){return "AI-bot TAB5 test";}
static int ble_gap_adv_rsp_set_fields(struct ble_hs_adv_fields *f){assert(f->name_len==(int)strlen(ble_svc_gap_device_name()));response_calls++;return failure==2?12:0;}
static int gap_event(void *event,void *arg){return 0;}
static int ble_gap_adv_start(int type,void *address,int duration,struct ble_gap_adv_params *params,int (*callback)(void*,void*),void *arg){start_calls++;if(failure==3)return 13;active=true;return 0;}
#include "../../firmware/main/ble_advertising.inc"
int main(void) {
    for(int stage=1;stage<=3;stage++) {
        active=false;failure=stage;fields_calls=response_calls=start_calls=0;
        advertise_now();assert(!active&&advertising_error==10+stage);
        assert(fields_calls==1&&response_calls==(stage>=2)&&start_calls==(stage>=3));
        failure=0;advertise_now();assert(active&&advertising_error==0);
        int calls=start_calls;advertise_now();assert(start_calls==calls);
    }
    active=false;ble_connected=true;int calls=start_calls;
    advertise_now();assert(start_calls==calls);
    ble_connected=false;synced=false;advertise_now();assert(start_calls==calls);
    synced=true;advertise_now();assert(active&&start_calls==calls+1);
    // A disconnected device can resume even when a completion callback was lost.
    active=false;advertise_now();assert(active&&start_calls==calls+2);
    puts("BLE_ADVERTISING_OK fields/response/start failures recover; active/connected/unsynced guarded; lost completion recovers");
}
