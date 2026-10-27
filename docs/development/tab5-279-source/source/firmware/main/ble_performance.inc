// Called only on the NimBLE host queue. Bulk mode expires automatically; retain
// the central's original idle interval rather than inventing an idle policy.
static uint16_t perf_connection=BLE_HS_CONN_HANDLE_NONE;
static struct ble_gap_conn_desc perf_idle;
static bool perf_bulk;
static int64_t perf_last_bulk;
static atomic_int perf_interval_rc,perf_phy_rc,perf_length_rc;
static atomic_uint perf_tx_octets,perf_rx_octets;
static void ble_performance_bulk(uint16_t conn) {
    perf_last_bulk=esp_timer_get_time()/1000;
    if(perf_connection!=conn||perf_bulk)return;
    perf_bulk=true;
    // Ask for 7.5 ms during bulk transfer. A 6..12 range lets the central pick
    // 15 ms; it may still reject this request, in which case the link continues
    // with its negotiated interval and the diagnostic reports the actual value.
    struct ble_gap_upd_params p={.itvl_min=6,.itvl_max=6,.latency=0,.supervision_timeout=400};
    perf_interval_rc=ble_gap_update_params(conn,&p);
    perf_length_rc=ble_gap_set_data_len(conn,251,2120);
    perf_phy_rc=ble_gap_set_prefered_le_phy(conn,BLE_GAP_LE_PHY_2M_MASK,BLE_GAP_LE_PHY_2M_MASK,0);
}
static void ble_performance_idle(void) {
    if(!perf_bulk||perf_connection==BLE_HS_CONN_HANDLE_NONE||esp_timer_get_time()/1000-perf_last_bulk<5000)return;
    struct ble_gap_upd_params p={.itvl_min=perf_idle.conn_itvl,.itvl_max=perf_idle.conn_itvl,
        .latency=perf_idle.conn_latency,.supervision_timeout=perf_idle.supervision_timeout};
    if(p.itvl_min)perf_interval_rc=ble_gap_update_params(perf_connection,&p);
    perf_bulk=false;
}
