static void check_remote_ota_ui(void) {
    cJSON *frame=cJSON_Parse("{\"epochMilliseconds\":1000000,\"pcInput\":{\"session\":\"33333333333333333333333333333333\"},\"connectionHealth\":{\"otaCommand\":{\"version\":1,\"requestId\":\"44444444444444444444444444444444\",\"offerId\":\"11111111111111111111111111111111\",\"sha256\":\"2222222222222222222222222222222222222222222222222222222222222222\",\"expiresAt\":1030000}}}");assert(frame);
    cJSON *command=item(item(frame,"connectionHealth"),"otaCommand");
    char selected[80];snprintf(selected,sizeof(selected),"%s",codex_selected);
    char *saved_text=strdup(lv_textarea_get_text(codex_input));assert(saved_text);
    bool available=preview_ota_available;unsigned started=preview_ota_starts;
    // Real manual and remote paths share save/busy guards and the overlay.
    for(int fault=0;fault<12;fault++) {
        cJSON *input=cJSON_Duplicate(frame,true),*c=item(item(input,"connectionHealth"),"otaCommand");
        remote_ota_id[0]=0;remote_ota_stage=0;cJSON_Delete(remote_ota_reply);remote_ota_reply=NULL;
        preview_ota_available=true;preview_ota_busy=false;preview_remote_result_ready=false;
        preview_remote_commit=true;preview_remote_confirmed=true;preview_checkpoint_ok=true;
        codex_voice_waiting=codex_waiting=codex_reading=codex_send_queued=codex_syncing=codex_load_waiting=photo_flow=false;
        for(int i=0;i<80;i++){codex_drafts[i].request[0]=0;codex_drafts[i].dirty=false;}
        assert(strlen(codex_drafts[0].id)==36);snprintf(codex_selected,sizeof(codex_selected),"%s",codex_drafts[0].id);char test_text[80];snprintf(test_text,sizeof(test_text),"电脑升级测试保留草稿 %d",fault);codex_set_text(test_text);
        codex_draft *draft=codex_draft_for(codex_selected,true);assert(draft);draft->loaded=true;draft->dirty=false;
        unsigned before=preview_ota_starts;
        if(fault==1)codex_voice_waiting=true;
        if(fault==2){snprintf(codex_drafts[0].request,sizeof(codex_drafts[0].request),"unconfirmed");}
        if(fault==3)preview_checkpoint_ok=false;
        if(fault==4)cJSON_SetNumberValue(item(c,"expiresAt"),999999);
        if(fault==5)cJSON_SetValuestring(item(c,"requestId"),"invalid");
        if(fault==6)cJSON_SetNumberValue(item(c,"version"),2);
        if(fault==7)cJSON_SetValuestring(item(c,"sha256"),"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        if(fault==8){preview_remote_commit=false;preview_remote_confirmed=false;}
        if(fault==9)cJSON_SetNumberValue(item(c,"expiresAt"),1e100);
        ota_remote_tick(input,LINK_USB,1000,1000);
        assert(preview_ota_starts==before); // No flash before an authenticated commit.
        if(fault==10)codex_voice_waiting=true; // Guard changes during the roundtrip.
        if(fault==11)cJSON_DeleteItemFromObjectCaseSensitive(item(input,"connectionHealth"),"otaCommand");
        ota_remote_tick(input,LINK_USB,1001,1000);
        if(fault==0){assert(preview_ota_starts==before+1&&preview_ota_busy&&!lv_obj_has_flag(ota_layer,LV_OBJ_FLAG_HIDDEN));}
        else {if(preview_ota_starts!=before||preview_ota_busy)fprintf(stderr,"REMOTE_OTA_FAILED fault=%d stage=%d starts=%u before=%u\n",fault,remote_ota_stage,preview_ota_starts,before);assert(preview_ota_starts==before&&!preview_ota_busy);}
        ota_remote_tick(input,LINK_USB,1002,1000);ota_remote_tick(input,LINK_USB,1003,1000);
        assert(preview_ota_starts==before+(fault==0)); // Duplicate telemetry/results cannot restart.
        preview_ota_busy=false;ota_remote_tick(input,LINK_USB,40000,1000);
        assert(!remote_ota_reply);cJSON_Delete(input);
    }
    assert(preview_ota_starts==started+1);
    (void)command;preview_checkpoint_ok=true;preview_ota_available=available;preview_ota_busy=false;
    codex_voice_waiting=false;for(int i=0;i<80;i++){codex_drafts[i].request[0]=0;codex_drafts[i].dirty=false;}
    snprintf(codex_selected,sizeof(codex_selected),"%s",selected);codex_set_text(saved_text);free(saved_text);visible(ota_layer,false);cJSON_Delete(frame);
    puts("REMOTE_OTA_UI_OK production shared save/busy/unconfirmed-message guards; authenticated commit before start; stale/malformed/hash/withdrawal rejection; no duplicate installation; preparation overlay");
}
