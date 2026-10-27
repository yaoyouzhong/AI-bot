#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stddef.h>
#include <stdatomic.h>
#include <stdio.h>
#include <string.h>
struct ble_gap_conn_desc {unsigned conn_itvl,conn_latency;};
static bool ble_connected=true;
static atomic_uint perf_tx_octets=251,perf_rx_octets=251;
static atomic_int perf_interval_rc,perf_length_rc,perf_phy_rc;
#include "../../firmware/main/ble_probe.inc"
int main(void){
    ble_probe_received(479,100,110,true);assert(atomic_load(&probe_fragments)==0);
    ble_probe_connection(0,&(struct ble_gap_conn_desc){12,0});ble_probe_capture(true);
    ble_probe_received(479,1000,1010,true);ble_probe_received(479,2000,2030,true);ble_probe_received(32,2200,2210,false);
    ble_probe_capture(false);ble_probe_received(99,2400,2450,true);
    assert(atomic_load(&probe_fragments)==3&&atomic_load(&probe_bytes)==990&&atomic_load(&probe_callback_us)==50);
    assert(atomic_load(&probe_gap_us)==990&&atomic_load(&probe_rejected)==1);
    char diag[256];ble_probe_diagnostics(diag,sizeof(diag));assert(!strcmp(diag,"1,1,15000,0,251,251,0,0,0,0,3,990,50,1,990,1"));
    ble_probe_capture(true);ble_probe_received(479,UINT32_MAX-10,UINT32_MAX-5,true);ble_probe_received(479,4,8,true);
    assert(atomic_load(&probe_gap_us)==10&&atomic_load(&probe_callback_us)==9&&atomic_load(&probe_rejected)==0);
    puts("BLE_RADIO_PROBE_OK negotiated_values_bounded_numeric_callback_timing_capture_reset_wrap");return 0;
}
