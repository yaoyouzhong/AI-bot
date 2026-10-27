// Called only on the NimBLE host queue; connection callbacks schedule this work.
static void advertise_now(void) {
    if(ble_physical_connected||!ble_hs_synced())return;
    uint8_t filter=0;if(!ble_owner_advertising(&filter)||ble_gap_adv_active())return;
    struct ble_hs_adv_fields fields={0};fields.flags=BLE_HS_ADV_F_DISC_GEN|BLE_HS_ADV_F_BREDR_UNSUP;
    fields.uuids128=(ble_uuid128_t*)&service_id;fields.num_uuids128=1;fields.uuids128_is_complete=1;
    static const ble_uuid16_t hid=BLE_UUID16_INIT(0x1812);
    fields.uuids16=(ble_uuid16_t*)&hid;fields.num_uuids16=1;fields.uuids16_is_complete=0;
    fields.appearance=0x03c1;fields.appearance_is_present=1;
    const char *stage="fields";
    int error=ble_gap_adv_set_fields(&fields);
    if(!error) {
        struct ble_hs_adv_fields response={0};const char *name=ble_svc_gap_device_name();
        response.name=(uint8_t*)name;response.name_len=strlen(name);response.name_is_complete=1;
        stage="scan response";error=ble_gap_adv_rsp_set_fields(&response);
    }
    if(!error) {
        struct ble_gap_adv_params params={0};params.conn_mode=BLE_GAP_CONN_MODE_UND;params.disc_mode=BLE_GAP_DISC_MODE_GEN;
        params.filter_policy=filter;
        stage="start";error=ble_gap_adv_start(own_address_type,NULL,BLE_HS_FOREVER,&params,gap_event,NULL);
    }
    if(error&&error!=advertising_error)ESP_LOGW("tab5","BLE advertising %s failed: %d; retry scheduled",stage,error);
    if(!error&&advertising_error)ESP_LOGI("tab5","BLE advertising recovered");
    advertising_error=error;
}
