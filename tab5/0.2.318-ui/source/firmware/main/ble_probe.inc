// Only scalar diagnostics cross tasks. No additional identity bytes or data
// notifications; the existing USB benchmark response carries this snapshot.
static atomic_bool probe_capture;
static atomic_uint probe_fragments,probe_bytes,probe_callback_us,probe_first_us,probe_last_us,probe_gap_us,probe_rejected;
static atomic_int probe_interval_us,probe_latency,probe_update_status;
static void ble_probe_connection(int status,const struct ble_gap_conn_desc *desc) {
    atomic_store(&probe_interval_us,(int)desc->conn_itvl*1250);
    atomic_store(&probe_latency,desc->conn_latency);atomic_store(&probe_update_status,status);
}
void ble_probe_capture(bool active) {
    atomic_store(&probe_capture,false);
    if(active) {
        atomic_store(&probe_fragments,0);atomic_store(&probe_bytes,0);atomic_store(&probe_callback_us,0);
        atomic_store(&probe_first_us,0);atomic_store(&probe_last_us,0);atomic_store(&probe_gap_us,0);atomic_store(&probe_rejected,0);
    }
    atomic_store(&probe_capture,active);
}
static void ble_probe_received(unsigned bytes,uint32_t began,uint32_t ended,bool ok) {
    if(!atomic_load(&probe_capture))return;
    unsigned previous=atomic_fetch_add(&probe_fragments,1);
    if(!previous)atomic_store(&probe_first_us,began);
    else {
        uint32_t gap=began-atomic_load(&probe_last_us);
        if(gap>atomic_load(&probe_gap_us))atomic_store(&probe_gap_us,gap);
    }
    atomic_store(&probe_last_us,ended);atomic_fetch_add(&probe_bytes,bytes);
    atomic_fetch_add(&probe_callback_us,(uint32_t)(ended-began));
    if(!ok)atomic_fetch_add(&probe_rejected,1);
}
void ble_probe_diagnostics(char *out,size_t size) {
    uint32_t span=atomic_load(&probe_fragments)?atomic_load(&probe_last_us)-atomic_load(&probe_first_us):0;
    snprintf(out,size,"1,%d,%d,%d,%u,%u,%d,%d,%d,%d,%u,%u,%u,%u,%u,%u",
        ble_connected?1:0,atomic_load(&probe_interval_us),atomic_load(&probe_latency),
        (unsigned)perf_tx_octets,(unsigned)perf_rx_octets,perf_interval_rc,perf_length_rc,perf_phy_rc,atomic_load(&probe_update_status),
        atomic_load(&probe_fragments),atomic_load(&probe_bytes),atomic_load(&probe_callback_us),(unsigned)(span/1000),
        atomic_load(&probe_gap_us),atomic_load(&probe_rejected));
}
