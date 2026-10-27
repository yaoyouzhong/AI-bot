// Reuse the worker-owned radio lease for real BLE bulk transfers. Never release
// a gallery/preflight owner's lease or stop Wi-Fi in automatic selection mode.
static bool rpc_bulk_radio_begin(link_kind link,cJSON *request,size_t upload_size,size_t capacity) {
    if(link!=LINK_BLE||(upload_size<=12288&&capacity<=12288)||device_get_config().mode!=LINK_BLE)return false;
    // Explicit diagnostic preflights retain their selected comparison condition.
    cJSON *kind=cJSON_GetObjectItemCaseSensitive(request,"kind");
    if(cJSON_IsString(kind)&&(!strcmp(kind->valuestring,"ota_probe")||
       (!strcmp(kind->valuestring,"benchmark")&&cJSON_IsTrue(cJSON_GetObjectItemCaseSensitive(request,"otaProbe")))))return false;
    if(!wifi_probe_begin_rpc_bulk())return false;
    int64_t until=esp_timer_get_time()+500000;
    tab5_wifi_wake();
    while(wifi_probe_pending()&&!wifi_probe_ready()&&device_get_config().mode==LINK_BLE&&ble_connected&&esp_timer_get_time()<until)
        vTaskDelay(pdMS_TO_TICKS(10));
    if(!wifi_probe_ready()||device_get_config().mode!=LINK_BLE||!ble_connected) {
        wifi_probe_end();tab5_wifi_wake();return false;
    }
    return true;
}
static void rpc_bulk_radio_end(bool owner,bool succeeded) {
    if(owner){wifi_probe_release_rpc_bulk(succeeded);tab5_wifi_wake();}
}
