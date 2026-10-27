static void check_voice_start_pending(void) {
    page(8);
    const char *task="00000000-0000-4000-8000-000000000111";
    snprintf(codex_selected,sizeof(codex_selected),"%s",task);
    codex_draft *d=codex_draft_for(task,true);assert(d);
    assert(codex_store_text(d,"retained text",true));codex_set_text(d->text);d->loaded=true;
    for(int i=0;i<3;i++)codex_uuid(d->images[i]);
    char images[3][37];memcpy(images,d->images,sizeof(images));
    if(!shown)shown=cJSON_CreateObject();
    cJSON *pc=cJSON_CreateObject();cJSON_AddStringToObject(pc,"session","synthetic-session");
    cJSON_DeleteItemFromObjectCaseSensitive(shown,"pcInput");cJSON_AddItemToObject(shown,"pcInput",pc);
    preview_voice_start_enabled=true;last_received=esp_timer_get_time()/1000;
    codex_waiting=codex_voice_waiting=codex_send_queued=codex_syncing=codex_load_waiting=false;
    codex_reading=true;unsigned starts=preview_voice_starts;int sends=preview_submit_count;
    lv_obj_remove_state(codex_input,LV_STATE_DISABLED);
    lv_obj_send_event(codex_voice_button,LV_EVENT_CLICKED,NULL);
    assert(codex_voice_start_pending&&!codex_voice_waiting&&preview_voice_starts==starts);
    assert(strstr(lv_label_get_text(codex_voice_hint),"等待")&&!lv_obj_has_flag(codex_voice_cancel,LV_OBJ_FLAG_HIDDEN));
    if(getenv("TAB5_VOICE_START_CAPTURE"))capture("voice-start-waiting");
    codex_voice_request(0);codex_voice_pending_tick(esp_timer_get_time()/1000);
    assert(preview_voice_starts==starts&&codex_voice_start_pending);
    codex_reading=false;codex_reply_refresh(NULL);codex_sync_tick(esp_timer_get_time()/1000);
    assert(!codex_reading&&!codex_syncing&&preview_submit_count==sends);
    codex_send(NULL);assert(!codex_send_queued&&preview_submit_count==sends);
    codex_voice_pending_tick(esp_timer_get_time()/1000);codex_voice_pending_tick(esp_timer_get_time()/1000);
    if(!codex_voice_waiting)fprintf(stderr,"pending=%d starts=%u check=%d hint=%s\n",codex_voice_start_pending,preview_voice_starts,tab5_ui_voice_check,lv_label_get_text(codex_voice_hint));
    assert(!codex_voice_start_pending&&codex_voice_waiting&&preview_voice_starts==starts+1&&!codex_voice_started);
    assert(!strcmp(lv_textarea_get_text(codex_input),"retained text")&&!memcmp(images,d->images,sizeof(images)));
    preview_voice.busy=false;strcpy(preview_voice.phase,"cancelled");codex_voice_tick();
    // Each explicit cancellation/context change must prevent a later start.
    for(int scenario=0;scenario<10;scenario++) {
        codex_voice_waiting=false;codex_reading=true;quick_visible=false;current_page=8;saver=false;
        preview_ota_busy=codex_waiting=codex_send_queued=photo_flow=false;
        snprintf(codex_selected,sizeof(codex_selected),"%s",task);d->request[0]=0;
        cJSON_ReplaceItemInObjectCaseSensitive(pc,"session",cJSON_CreateString("synthetic-session"));
        lv_obj_remove_state(codex_input,LV_STATE_DISABLED);last_received=esp_timer_get_time()/1000;
        codex_voice_request(0);assert(codex_voice_start_pending);starts=preview_voice_starts;
        switch(scenario) {
        case 0:lv_obj_send_event(codex_voice_cancel,LV_EVENT_CLICKED,NULL);break;
        case 1:current_page=0;break;
        case 2:codex_selected[35]='2';break;
        case 3:cJSON_ReplaceItemInObjectCaseSensitive(pc,"session",cJSON_CreateString("other-session"));break;
        case 4:saver=true;break;
        case 5:preview_ota_busy=true;break;
        case 6:strcpy(d->request,"00000000-0000-4000-8000-000000000222");break;
        case 7:codex_send_queued=true;break;
        case 8:preview_now_us+=20000000;break;
        case 9:quick_visible=true;break;
        }
        codex_voice_pending_tick(esp_timer_get_time()/1000);codex_reading=false;
        codex_voice_pending_tick(esp_timer_get_time()/1000);
        assert(!codex_voice_start_pending&&!codex_voice_waiting&&preview_voice_starts==starts);
    }
    quick_visible=true;current_page=8;preview_ota_busy=codex_send_queued=saver=false;
    snprintf(codex_selected,sizeof(codex_selected),"%s",task);d->request[0]=0;
    cJSON_ReplaceItemInObjectCaseSensitive(pc,"session",cJSON_CreateString("synthetic-session"));
    quick_count=1;quick_index=0;snprintf(quick_tasks[0].id,sizeof(quick_tasks[0].id),"%s",task);
    snprintf(quick_opened,sizeof(quick_opened),"%s",task);
    quick_opening=quick_recording=quick_filling=quick_sending=quick_clearing=quick_ready=quick_resetting=quick_failed=false;
    codex_reading=true;last_received=esp_timer_get_time()/1000;starts=preview_voice_starts;
    lv_obj_send_event(quick_voice_button,LV_EVENT_CLICKED,NULL);
    assert(codex_voice_start_pending&&!quick_recording&&preview_voice_starts==starts);
    quick_tick();assert(codex_voice_start_pending&&!quick_recording&&!quick_failed);
    assert(strstr(lv_label_get_text(quick_status),"等待"));
    codex_reading=false;codex_voice_pending_tick(esp_timer_get_time()/1000);
    assert(quick_recording&&codex_voice_waiting&&preview_voice_starts==starts+1);
    assert(!strcmp(lv_textarea_get_text(codex_input),"retained text")&&!memcmp(images,d->images,sizeof(images)));
    assert(preview_submit_count==sends);
    puts("VOICE_START_UI_OK single click waits; duplicate coalesced; draft/photos retained; timer starts after capture; cancel/context/timeout abort; background work blocked; Quick Console starts once without stale take");
}
