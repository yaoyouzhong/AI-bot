#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdatomic.h>
#include <stdio.h>
#define BLE_HS_CONN_HANDLE_NONE 65535
#define BLE_HS_EALREADY 2
#define BLE_ERR_DIFF_TRANS_COLL 42
#define BLE_HS_HCI_ERR(n) (512+(n))
#define BLE_GAP_LE_PHY_2M_MASK 2
struct ble_gap_conn_desc {uint16_t conn_itvl,conn_latency,supervision_timeout;};
struct ble_gap_upd_params {uint16_t itvl_min,itvl_max,latency,supervision_timeout;};
static int64_t clock_us;
static int next_rc,calls;
static struct ble_gap_upd_params last;
static int64_t esp_timer_get_time(void){return clock_us;}
static int ble_gap_update_params(uint16_t conn,const struct ble_gap_upd_params *p){assert(conn==7);calls++;last=*p;return next_rc;}
static int ble_gap_conn_find(uint16_t conn,struct ble_gap_conn_desc *d){assert(conn==7);*d=(struct ble_gap_conn_desc){36,1,600};return 0;}
static int ble_gap_set_data_len(uint16_t c,int a,int b){assert(c==7&&a==251&&b==2120);return 0;}
static int ble_gap_set_prefered_le_phy(uint16_t c,int a,int b,int o){assert(c==7&&a==2&&b==2&&!o);return 0;}
#include "../../firmware/main/ble_performance.inc"
static void fresh(void){perf_bulk=false;perf_connection=7;clock_us=0;calls=0;next_rc=0;ble_performance_bulk(7);assert(calls==1&&last.itvl_min==6&&perf_pending);}
static void tick(int ms){clock_us=(int64_t)ms*1000;ble_performance_bulk(7);ble_performance_idle();}
int main(void){
 fresh();ble_performance_updated(7,0);tick(3000);assert(calls==1); // Windows keeps accepted 7.5 ms.
 clock_us=9000000;ble_performance_idle();assert(calls==2&&!perf_bulk&&last.itvl_min==36&&last.latency==1&&last.supervision_timeout==600);
 fresh();ble_performance_updated(8,571);assert(perf_pending); // Stale peer cannot advance.
 ble_performance_updated(7,571);tick(1999);assert(calls==1);tick(2000);assert(calls==2&&last.itvl_min==12&&last.itvl_max==12&&last.supervision_timeout==600);
 ble_performance_updated(7,0);tick(4000);assert(calls==2); // Apple accepted fallback, no request storm.
 fresh();ble_performance_updated(7,571);tick(2000);ble_performance_updated(7,571);tick(4000);assert(calls==3&&last.itvl_min==24&&last.itvl_max==36&&last.supervision_timeout==600);
 ble_performance_updated(7,571);tick(6000);assert(calls==3); // All profiles refused.
 fresh();ble_performance_updated(7,BLE_HS_HCI_ERR(BLE_ERR_DIFF_TRANS_COLL));next_rc=BLE_HS_EALREADY;
 for(int ms=2000;ms<=20000;ms+=2000)tick(ms);
 assert(calls==6&&last.itvl_min==6); // Bound busy retries without misclassifying collision.
 fresh();tick(4000);tick(8000);assert(calls==1&&perf_pending); // Host owns procedure timeout.
 perf_connection=BLE_HS_CONN_HANDLE_NONE;clock_us=20000000;ble_performance_idle();assert(calls==1);
 puts("BLE_PERFORMANCE_OK accepted_fallback_collision_bounded_idle_disconnect");
}
