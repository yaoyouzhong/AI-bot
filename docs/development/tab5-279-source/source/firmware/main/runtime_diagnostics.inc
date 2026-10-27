// Same bound and freshness checks as the authenticated boot probe. A distinct
// command avoids coupling ordinary diagnostics to an OTA offer or verification.
static char runtime_diagnostics_last[33];
static void runtime_diagnostics_tick(cJSON *frame,link_kind link,int64_t now,int64_t received) {
    cJSON *command=item(item(frame,"connectionHealth"),"runtimeDiagnosticCommand");
    cJSON *clock=item(frame,"epochMilliseconds"),*expires=item(command,"expiresAt"),*version=item(command,"version");
    const char *id=cJSON_GetStringValue(item(command,"requestId"));
    const char *session=cJSON_GetStringValue(item(item(frame,"pcInput"),"session"));
    const char *transport=cJSON_GetStringValue(item(command,"transport"));
    if((link!=LINK_WIFI&&link!=LINK_BLE)||!transport||strcmp(transport,link==LINK_WIFI?"Wi-Fi":"BLE")||
        now<received||now-received>=6000||!remote_ota_hex(id,32)||!remote_ota_hex(session,32)||
        !cJSON_IsNumber(version)||version->valuedouble!=1||!cJSON_IsNumber(clock)||clock->valuedouble<=0||
        !cJSON_IsNumber(expires)||expires->valuedouble<=0||expires->valuedouble>9007199254740991.0||
        expires->valuedouble!=(int64_t)expires->valuedouble||expires->valuedouble<clock->valuedouble+now-received||
        expires->valuedouble>clock->valuedouble+15000||!strcmp(id,runtime_diagnostics_last))return;
    if(runtime_diagnostics_report(id,session,link))snprintf(runtime_diagnostics_last,sizeof(runtime_diagnostics_last),"%s",id);
}
