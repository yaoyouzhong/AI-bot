// All state and negotiation run on the NimBLE host queue.
static uint16_t perf_connection=BLE_HS_CONN_HANDLE_NONE;
static struct ble_gap_conn_desc perf_idle;
static bool perf_bulk,perf_pending;
static unsigned perf_level,perf_attempts;
static int64_t perf_last_bulk,perf_retry_at;
static atomic_int perf_interval_rc,perf_phy_rc,perf_length_rc;
static atomic_uint perf_tx_octets,perf_rx_octets;
static void ble_performance_request(void) {
    static const uint16_t intervals[]={6,12,24}; // 7.5, 15, 30 ms
    if(perf_pending||perf_level>=3||perf_attempts>=6)return;
    struct ble_gap_upd_params p={.itvl_min=intervals[perf_level],.itvl_max=perf_level==2?36:intervals[perf_level],
        .latency=0,.supervision_timeout=perf_level?600:400};
    perf_attempts++;
    int rc=ble_gap_update_params(perf_connection,&p);perf_interval_rc=rc;
    perf_pending=rc==0;
    // A collision is transient; await the asynchronous result before retrying.
    if(rc&&rc!=BLE_HS_EALREADY&&rc!=BLE_HS_HCI_ERR(BLE_ERR_DIFF_TRANS_COLL))perf_level++;
    perf_retry_at=esp_timer_get_time()/1000+2000;
}
static void ble_performance_updated(uint16_t conn,int status) {
    if(conn!=perf_connection||!perf_bulk||!perf_pending)return;
    perf_pending=false;
    if(!status)perf_level=3;
    else if(status!=BLE_HS_HCI_ERR(BLE_ERR_DIFF_TRANS_COLL))perf_level++;
    perf_retry_at=esp_timer_get_time()/1000+2000;
}
static void ble_performance_bulk(uint16_t conn) {
    perf_last_bulk=esp_timer_get_time()/1000;
    if(perf_connection!=conn||perf_bulk)return;
    // Restore the current idle policy, not the initial connection value.
    ble_gap_conn_find(conn,&perf_idle);
    perf_bulk=true;perf_pending=false;perf_level=perf_attempts=0;
    ble_performance_request();
    perf_length_rc=ble_gap_set_data_len(conn,251,2120);
    perf_phy_rc=ble_gap_set_prefered_le_phy(conn,BLE_GAP_LE_PHY_2M_MASK,BLE_GAP_LE_PHY_2M_MASK,0);
}
static void ble_performance_idle(void) {
    if(!perf_bulk||perf_connection==BLE_HS_CONN_HANDLE_NONE)return;
    int64_t now=esp_timer_get_time()/1000;
    if(now-perf_last_bulk<5000) {
        if(now>=perf_retry_at)ble_performance_request();
        return;
    }
    // NimBLE owns the pending procedure's timeout; never overlap idle restore.
    if(perf_pending)return;
    struct ble_gap_upd_params p={.itvl_min=perf_idle.conn_itvl,.itvl_max=perf_idle.conn_itvl,
        .latency=perf_idle.conn_latency,.supervision_timeout=perf_idle.supervision_timeout};
    if(p.itvl_min)perf_interval_rc=ble_gap_update_params(perf_connection,&p);
    perf_bulk=false;
}
