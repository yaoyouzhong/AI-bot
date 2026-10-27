// Wi-Fi worker owns driver calls; USB only reads numeric atomic snapshots.
static atomic_int wifi_config_rc=-1,wifi_connect_rc=-1,wifi_scan_rc=-1,wifi_scan_matches=-1;
static atomic_int wifi_scan_rssi,wifi_scan_channel,wifi_get_config_rc=-1,wifi_ssid_matches=-1,wifi_station_channel,wifi_bssid_locked;
static atomic_uint wifi_connect_requests,wifi_scan_runs;
static int64_t wifi_next_scan_ms;
static atomic_int wifi_connect_phase; // 0 idle/failed, 1 associating, 2 awaiting DHCP
static atomic_int_fast64_t wifi_connect_until;
static atomic_int wifi_timeout_phase;
static atomic_uint wifi_timeouts,wifi_restarts;
static atomic_int wifi_restart_stop_rc=-1,wifi_restart_start_rc=-1;
static atomic_bool wifi_restart_pending;
static int64_t wifi_next_restart_ms,wifi_restart_retry_ms;
static int64_t wifi_offline_since_ms;
static void wifi_connection_phase(int phase) {
    if(phase==2)atomic_store(&wifi_timeout_phase,0);
    atomic_store(&wifi_connect_until,esp_timer_get_time()/1000+(phase==2?60000:30000));
    atomic_store(&wifi_connect_phase,phase);
}
static bool wifi_connection_waiting(void) {
    int phase=atomic_load(&wifi_connect_phase);if(!phase)return false;
    if(esp_timer_get_time()/1000<atomic_load(&wifi_connect_until))return true;
    if(atomic_compare_exchange_strong(&wifi_connect_phase,&phase,0)) {
        atomic_store(&wifi_timeout_phase,phase);atomic_fetch_add(&wifi_timeouts,1);
    }
    return false;
}
static void wifi_observe_configuration(const device_config *cfg) {
    wifi_config_t actual={0};int rc=esp_wifi_get_config(WIFI_IF_STA,&actual);
    atomic_store(&wifi_get_config_rc,rc);
    if(rc!=ESP_OK){atomic_store(&wifi_ssid_matches,-1);return;}
    size_t length=strnlen((const char *)actual.sta.ssid,sizeof(actual.sta.ssid));
    atomic_store(&wifi_ssid_matches,length==strlen(cfg->ssid)&&!memcmp(actual.sta.ssid,cfg->ssid,length));
    atomic_store(&wifi_station_channel,actual.sta.channel);
    atomic_store(&wifi_bssid_locked,actual.sta.bssid_set?1:0);
}
static void wifi_request_connection(const device_config *cfg) {
    atomic_fetch_add(&wifi_connect_requests,1);
    wifi_connection_phase(1);
    int rc=esp_wifi_connect();atomic_store(&wifi_connect_rc,rc);
    if(rc!=ESP_OK)wifi_connection_phase(0);
    wifi_observe_configuration(cfg);
}
static bool wifi_apply_configuration(const device_config *cfg) {
    atomic_store(&wifi_timeout_phase,0);
    wifi_connection_phase(0);esp_wifi_disconnect();wifi_config_t next={0};
    memcpy(next.sta.ssid,cfg->ssid,strlen(cfg->ssid));memcpy(next.sta.password,cfg->password,strlen(cfg->password));
    // Roaming SSIDs can have multiple APs: evaluate all channels and prefer
    // signal strength, instead of stopping at the first matching beacon.
    next.sta.scan_method=WIFI_ALL_CHANNEL_SCAN;next.sta.sort_method=WIFI_CONNECT_AP_BY_SIGNAL;
    int rc=esp_wifi_set_config(WIFI_IF_STA,&next);atomic_store(&wifi_config_rc,rc);
    atomic_store(&wifi_scan_matches,-1);atomic_store(&wifi_scan_rssi,0);atomic_store(&wifi_scan_channel,0);
    wifi_next_scan_ms=0;atomic_store(&disconnect_reason,0);
    if(rc!=ESP_OK){wifi_observe_configuration(cfg);return false;}
    wifi_request_connection(cfg);return true;
}
static bool wifi_recover_connection(const device_config *cfg,bool manual) {
    // Only the Wi-Fi worker calls this, after any BLE isolation lease is restored.
    // API success without association/IP progress is not a successful connection.
    int64_t now=esp_timer_get_time()/1000;
    if(manual&&cfg->ssid[0])atomic_store(&wifi_timeout_phase,3);
    else if(wifi_connected){atomic_store(&wifi_timeout_phase,0);wifi_offline_since_ms=0;return false;}
    if(!wifi_restart_pending){
        if(!cfg->ssid[0]){atomic_store(&wifi_timeout_phase,0);wifi_offline_since_ms=0;return false;}
        if(!wifi_offline_since_ms)wifi_offline_since_ms=now;
        wifi_connection_waiting();
        // Repeated completed no-AP scans across saved profiles can also indicate
        // a stuck station. Recover at most once per two minutes; a genuinely
        // absent AP still remains disconnected and retains its NO_AP evidence.
        if(now-wifi_offline_since_ms>=120000&&atomic_load(&disconnect_reason)==WIFI_REASON_NO_AP_FOUND&&
           atomic_load(&wifi_scan_rc)==ESP_OK&&atomic_load(&wifi_scan_matches)==0)atomic_store(&wifi_timeout_phase,4);
        if(!atomic_load(&wifi_timeout_phase))return false;
    }
    if(now<wifi_restart_retry_ms)return true;
    if(!wifi_restart_pending) {
        if(!manual&&now<wifi_next_restart_ms)return true;
        int rc=esp_wifi_stop();atomic_store(&wifi_restart_stop_rc,rc);
        if(rc!=ESP_OK){wifi_restart_retry_ms=now+10000;return true;}
        wifi_connected=false;wifi_restart_pending=true;atomic_fetch_add(&wifi_restarts,1);
        wifi_connection_phase(0);wifi_next_restart_ms=now+120000;
    }
    int rc=esp_wifi_start();atomic_store(&wifi_restart_start_rc,rc);
    if(rc!=ESP_OK){wifi_restart_retry_ms=now+2000;return true;}
    wifi_restart_pending=false;wifi_restart_retry_ms=0;
    wifi_offline_since_ms=now;
    atomic_store(&wifi_timeout_phase,0);
    if(cfg->ssid[0])wifi_apply_configuration(cfg);
    return true;
}
static void wifi_scan_missing_network(const device_config *cfg) {
    int64_t now=esp_timer_get_time()/1000;
    // Only diagnose an actual no-AP failure, at most once per 30 seconds.
    // Connected links and authentication failures retain their original path.
    if(!wifi_connected&&atomic_load(&disconnect_reason)==WIFI_REASON_NO_AP_FOUND&&now>=wifi_next_scan_ms) {
        wifi_next_scan_ms=now+30000;
        wifi_scan_config_t scan={.ssid=(uint8_t *)cfg->ssid,.show_hidden=true};
        atomic_fetch_add(&wifi_scan_runs,1);int rc=esp_wifi_scan_start(&scan,true);
        wifi_ap_record_t records[4];uint16_t count=4;int matches=-1,best=-128,channel=0;
        if(rc==ESP_OK) {
            rc=esp_wifi_scan_get_ap_records(&count,records);
            if(rc==ESP_OK) {
                matches=0;
                for(unsigned i=0;i<count;i++)if(!strcmp((const char *)records[i].ssid,cfg->ssid)) {
                    matches++;if(records[i].rssi>best){best=records[i].rssi;channel=records[i].primary;}
                }
            }
        }
        atomic_store(&wifi_scan_rc,rc);atomic_store(&wifi_scan_matches,matches);
        atomic_store(&wifi_scan_rssi,matches>0?best:0);atomic_store(&wifi_scan_channel,channel);
    }
}
static bool wifi_advance_profile(const device_config *cfg,int attempts,int count) {
    if(count<=1||wifi_connection_waiting())return false;
    int reason=atomic_load(&disconnect_reason);
    if(reason==WIFI_REASON_NO_AP_FOUND) {
        // Previously multiple saved profiles skipped this scan entirely and
        // rotated on a stale/no-AP reason, repeatedly interrupting recovery.
        wifi_scan_missing_network(cfg);
        return atomic_load(&wifi_scan_matches)==0;
    }
    return reason==202||reason==15||attempts>=15;
}
static void wifi_retry_connection(const device_config *cfg) {
    if(wifi_connected||wifi_connection_waiting())return;
    wifi_scan_missing_network(cfg);wifi_request_connection(cfg);
}
void tab5_wifi_numeric_diagnostics(char *out,size_t size) {
    snprintf(out,size,"1,%d,%d,%d,%d,%d,%u,%d,%u,%d,%d,%d,%d,%d,%d,%d",
        tab5_wifi_initialized?1:0,wifi_connected?1:0,atomic_load(&wifi_config_rc),atomic_load(&wifi_connect_rc),
        atomic_load(&disconnect_reason),atomic_load(&wifi_connect_requests),atomic_load(&wifi_scan_rc),atomic_load(&wifi_scan_runs),
        atomic_load(&wifi_scan_matches),atomic_load(&wifi_scan_rssi),atomic_load(&wifi_scan_channel),atomic_load(&wifi_get_config_rc),
        atomic_load(&wifi_ssid_matches),atomic_load(&wifi_station_channel),atomic_load(&wifi_bssid_locked));
}
void tab5_wifi_recovery_diagnostics(char *out,size_t size) {
    snprintf(out,size,"1,%d,%d,%u,%u,%d,%d,%d",atomic_load(&wifi_connect_phase),
        atomic_load(&wifi_timeout_phase),atomic_load(&wifi_timeouts),atomic_load(&wifi_restarts),
        atomic_load(&wifi_restart_stop_rc),atomic_load(&wifi_restart_start_rc),wifi_restart_pending?1:0);
}
