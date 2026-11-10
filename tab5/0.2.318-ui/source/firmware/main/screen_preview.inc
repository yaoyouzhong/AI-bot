static char screen_preview_last[33];
static int64_t screen_preview_until;
static bool screen_preview_cache_paused(int64_t now) {
    return screen_preview_busy()||now<screen_preview_until;
}
static void screen_preview_tick(cJSON *frame,link_kind link,int64_t now,int64_t received) {
    screen_preview_cache_reclaim(false);
    cJSON *command=item(item(frame,"connectionHealth"),"screenPreviewCommand");
    cJSON *clock=item(frame,"epochMilliseconds"),*expires=item(command,"expiresAt"),*version=item(command,"version");
    cJSON *width=item(command,"width"),*height=item(command,"height");
    const char *id=cJSON_GetStringValue(item(command,"requestId")),*stream=cJSON_GetStringValue(item(command,"streamId"));
    const char *session=cJSON_GetStringValue(item(item(frame,"pcInput"),"session"));
    const char *transport=cJSON_GetStringValue(item(command,"transport")),*encoding=cJSON_GetStringValue(item(command,"encoding"));
    if((link!=LINK_USB&&link!=LINK_WIFI&&link!=LINK_BLE)||!transport||
       strcmp(transport,link==LINK_USB?"USB":link==LINK_WIFI?"Wi-Fi":"BLE")||
       now<received||now-received>=6000||!remote_ota_hex(id,32)||!remote_ota_hex(stream,32)||!remote_ota_hex(session,32)||
       !encoding||!cJSON_IsNumber(version)||!((version->valuedouble==1&&!strcmp(encoding,"rgb565-rle1"))||
       (version->valuedouble==2&&(!strcmp(encoding,"rgb565-rle1")||!strcmp(encoding,"jpeg"))))||
       !cJSON_IsNumber(width)||!cJSON_IsNumber(height)||
       !((!strcmp(encoding,"rgb565-rle1")&&((width->valuedouble==640&&height->valuedouble==360)||(width->valuedouble==320&&height->valuedouble==180)))||
       (version->valuedouble==2&&!strcmp(encoding,"jpeg")&&((width->valuedouble==1280&&height->valuedouble==720)||(width->valuedouble==720&&height->valuedouble==1280))))||
       !cJSON_IsNumber(clock)||clock->valuedouble<=0||!cJSON_IsNumber(expires)||expires->valuedouble<=0||
       expires->valuedouble>9007199254740991.0||expires->valuedouble!=(int64_t)expires->valuedouble||
       expires->valuedouble<clock->valuedouble+now-received||expires->valuedouble>clock->valuedouble+30000||
       !strcmp(id,screen_preview_last))return;
    // UI-thread-only, between render passes. Drop only recreatable caches;
    // displayed/gesture snapshots, photo attachments and drafts keep ownership.
    screen_preview_until=now+5000;
    if(!screen_preview_busy()) {
        gallery_cache_reclaim();
        tab5_draw_cache_reclaim_staging();
        tab5_draw_cache_trim_hidden(3u*1024u*1024u);
    }
    int64_t deadline=now+(int64_t)(expires->valuedouble-clock->valuedouble)-(now-received);
    if(screen_preview_request(stream,id,session,link,width->valueint,height->valueint,(int)version->valuedouble,encoding,deadline))
        snprintf(screen_preview_last,sizeof(screen_preview_last),"%s",id);
}
