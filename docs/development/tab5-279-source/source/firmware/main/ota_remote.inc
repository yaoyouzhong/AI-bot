// Only LVGL calls this. The network worker never starts an upgrade or touches UI.
static char remote_ota_id[33],remote_ota_offer[33],remote_ota_sha[65],remote_ota_session[40];
static cJSON *remote_ota_reply;
static int remote_ota_stage;
static int64_t remote_ota_retry,remote_ota_until;
static bool remote_ota_hex(const char *s,size_t n) {
    if(!s||strlen(s)!=n)return false;
    for(size_t i=0;i<n;i++)if(!((s[i]>='0'&&s[i]<='9')||(s[i]>='a'&&s[i]<='f')))return false;
    return true;
}
static void remote_ota_response(const char *stage,const char *error) {
    cJSON_Delete(remote_ota_reply);remote_ota_reply=cJSON_CreateObject();
    cJSON_AddStringToObject(remote_ota_reply,"kind","ota-control");
    cJSON_AddStringToObject(remote_ota_reply,"requestId",remote_ota_id);
    cJSON_AddStringToObject(remote_ota_reply,"offerId",remote_ota_offer);
    cJSON_AddStringToObject(remote_ota_reply,"sha256",remote_ota_sha);
    cJSON_AddStringToObject(remote_ota_reply,"stage",stage);cJSON_AddStringToObject(remote_ota_reply,"error",error?error:"");
    remote_ota_retry=0;
}
static void ota_remote_tick(cJSON *frame,link_kind link,int64_t now,int64_t received) {
    cJSON *command=item(item(frame,"connectionHealth"),"otaCommand");
    cJSON *clock=item(frame,"epochMilliseconds"),*expires=item(command,"expiresAt"),*version=item(command,"version");
    const char *id=cJSON_GetStringValue(item(command,"requestId")),*offer=cJSON_GetStringValue(item(command,"offerId")),*sha=cJSON_GetStringValue(item(command,"sha256"));
    const char *session=cJSON_GetStringValue(item(item(frame,"pcInput"),"session"));
    bool fresh=link!=LINK_NONE&&now>=received&&now-received<6000&&session&&remote_ota_hex(session,32)&&
        cJSON_IsNumber(clock)&&clock->valuedouble>0&&cJSON_IsNumber(expires)&&expires->valuedouble>0&&expires->valuedouble<=9007199254740991.0&&
        expires->valuedouble==(int64_t)expires->valuedouble&&expires->valuedouble>=clock->valuedouble+now-received&&expires->valuedouble<=clock->valuedouble+45000&&
        cJSON_IsNumber(version)&&version->valuedouble==1&&remote_ota_hex(id,32)&&remote_ota_hex(offer,32)&&remote_ota_hex(sha,64);
    char result[33];bool commit,confirmed;
    if(ota_remote_result(result,&commit,&confirmed)&&!strcmp(result,remote_ota_id)) {
        if(remote_ota_stage==1&&commit) {
            const char *error=(!fresh||strcmp(session,remote_ota_session)||strcmp(id,remote_ota_id)||strcmp(offer,remote_ota_offer)||strcmp(sha,remote_ota_sha)||
                now>=remote_ota_until||!ota_update_offer_matches(remote_ota_offer,remote_ota_sha))?"offer_changed":ota_begin(remote_ota_offer,remote_ota_sha);
            remote_ota_response(error?"rejected":"started",error);remote_ota_stage=2;
        }else if(confirmed){cJSON_Delete(remote_ota_reply);remote_ota_reply=NULL;remote_ota_stage=3;}
    }
    if(fresh&&strcmp(id,remote_ota_id)&&(remote_ota_stage==0||remote_ota_stage==3||now>=remote_ota_until)) {
        snprintf(remote_ota_session,sizeof(remote_ota_session),"%s",session);snprintf(remote_ota_id,sizeof(remote_ota_id),"%s",id);snprintf(remote_ota_offer,sizeof(remote_ota_offer),"%s",offer);snprintf(remote_ota_sha,sizeof(remote_ota_sha),"%s",sha);
        remote_ota_until=now+(int64_t)(expires->valuedouble-clock->valuedouble)-(now-received);
        const char *error=ota_update_busy()?"device_busy":ota_update_offer_matches(offer,sha)?ota_prepare():"offer_changed";
        remote_ota_response(error?"rejected":"ready",error);remote_ota_stage=error?2:1;
    }
    if(now>=remote_ota_until||(!fresh&&remote_ota_stage==1)) {cJSON_Delete(remote_ota_reply);remote_ota_reply=NULL;remote_ota_stage=3;}
    if(remote_ota_reply&&fresh&&now>=remote_ota_retry) {
        cJSON *copy=cJSON_Duplicate(remote_ota_reply,true);
        if(copy){cJSON_AddStringToObject(copy,"session",remote_ota_session);if(!ota_remote_report(copy,link))cJSON_Delete(copy);}
        remote_ota_retry=now+1000;
    }
}
