// UI task only: validate a fresh authenticated snapshot, then schedule a bounded
// read-only report. The worker never touches UI, NVS, keys or OTA state.
static char boot_verify_last[33];
static void boot_verify_tick(cJSON *frame,link_kind link,int64_t now,int64_t received) {
    cJSON *command=item(item(frame,"connectionHealth"),"bootVerifyCommand");
    cJSON *clock=item(frame,"epochMilliseconds"),*expires=item(command,"expiresAt");
    const char *id=cJSON_GetStringValue(item(command,"requestId"));
    const char *session=cJSON_GetStringValue(item(item(frame,"pcInput"),"session"));
    const char *transport=cJSON_GetStringValue(item(command,"transport"));
    cJSON *version=item(command,"version");
    if((link!=LINK_WIFI&&link!=LINK_BLE)||!transport||strcmp(transport,link==LINK_WIFI?"Wi-Fi":"BLE")||
        now<received||now-received>=6000||!remote_ota_hex(id,32)||!remote_ota_hex(session,32)||
        !cJSON_IsNumber(version)||version->valuedouble!=1||!cJSON_IsNumber(clock)||clock->valuedouble<=0||
        !cJSON_IsNumber(expires)||expires->valuedouble<=0||expires->valuedouble>9007199254740991.0||
        expires->valuedouble!=(int64_t)expires->valuedouble||expires->valuedouble<clock->valuedouble+now-received||
        expires->valuedouble>clock->valuedouble+15000||!strcmp(id,boot_verify_last))return;
    if(boot_verify_report(id,session,link))snprintf(boot_verify_last,sizeof(boot_verify_last),"%s",id);
}
