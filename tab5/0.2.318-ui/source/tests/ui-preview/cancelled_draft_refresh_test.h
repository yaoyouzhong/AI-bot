static void cancel_fixture(unsigned client,const char *value,const char *image) {
    workspace_client=client;codex_remote_set_client(workspace_name());
    const char *task="11111111-1111-4111-8111-111111111111";
    cJSON_Delete(shown);shown=cJSON_Parse("{\"epochMilliseconds\":1793758000000,\"pcInput\":{\"session\":\"cancel-fixture\"}}");assert(shown);
    cJSON *remote=cJSON_AddObjectToObject(shown,workspace_key());
    cJSON *projects=cJSON_AddArrayToObject(remote,"projects"),*p=cJSON_CreateObject();cJSON_AddItemToArray(projects,p);
    cJSON_AddStringToObject(p,"id","1111111111111111");cJSON_AddStringToObject(p,"name","取消恢复验收");
    cJSON *tasks=cJSON_AddArrayToObject(remote,"tasks"),*t=cJSON_CreateObject();cJSON_AddItemToArray(tasks,t);
    cJSON_AddStringToObject(t,"id",task);cJSON_AddStringToObject(t,"projectId","1111111111111111");cJSON_AddStringToObject(t,"title","取消后恢复文字与照片");
    cJSON *control=cJSON_AddObjectToObject(remote,"control");cJSON_AddStringToObject(control,"taskId",task);
    cJSON_AddStringToObject(control,"turnId","22222222-2222-4222-8222-222222222222");cJSON_AddBoolToObject(control,"running",true);
    preview_usb_connected=true;preview_submit_enabled=true;last_link=LINK_USB;last_received=preview_now_us/1000;
    page(8);snprintf(codex_selected,sizeof(codex_selected),"%s",task);render_codex();
    codex_draft *d=codex_draft_for(task,true);assert(codex_store_text(d,value,false));memset(d->images,0,sizeof(d->images));
    if(image)snprintf(d->images[0],37,"%s",image);
    d->request[0]=0;d->loaded=true;d->dirty=false;codex_load_draft();
    codex_waiting=codex_reading=codex_syncing=codex_load_waiting=codex_send_queued=false;
    codex_read_next=codex_sync_next=preview_now_us/1000+100000;preview_result_code=-1;
}
static void cancel_interrupt_success(void) {
    codex_interrupt(NULL);assert(codex_waiting&&!strcmp(preview_operation,"interrupt"));
    preview_result_code=202;tick(NULL);assert(!codex_waiting);
    codex_reading=false;codex_read_next=preview_now_us/1000+100000;
}
static void cancel_host_draft(const char *value) {
    cJSON *remote=item(shown,workspace_key());cJSON_DeleteItemFromObject(remote,"draftView");
    cJSON *v=cJSON_AddObjectToObject(remote,"draftView");cJSON_AddStringToObject(v,"taskId",codex_sync_task);
    cJSON_AddStringToObject(v,"requestId",codex_sync_request);cJSON_AddStringToObject(v,"text",value);
    cJSON *images=cJSON_AddArrayToObject(v,"images");cJSON_AddItemToArray(images,cJSON_CreateString("33333333-3333-4333-8333-333333333333"));
    preview_result_code=200;
}
static void check_cancelled_draft_refresh(void) {
    for(unsigned client=0;client<2;client++) {
        cancel_fixture(client,"",NULL);codex_draft *d=codex_draft_for(codex_selected,false);
        cancel_interrupt_success();assert(!d->loaded); // Fails on .313: no post-interrupt reload.
        codex_sync_next=0;codex_sync_tick(preview_now_us/1000);assert(codex_syncing&&!strcmp(preview_operation,"draft-load"));
        cancel_host_draft("取消后恢复的文字");codex_sync_tick(preview_now_us/1000+100);
        assert(d->loaded&&!strcmp(d->text,"取消后恢复的文字")&&d->images[0][0]);
        assert(!strcmp(lv_textarea_get_text(codex_input),d->text));
        capture(client?"cancel-claude-restored-text-photo":"cancel-codex-restored-text-photo");
        cancel_fixture(client,"后来输入的新草稿",NULL);d=codex_draft_for(codex_selected,false);
        cancel_interrupt_success();assert(d->loaded&&!strcmp(d->text,"后来输入的新草稿"));
        capture(client?"cancel-claude-preserved-newer":"cancel-codex-preserved-newer");
        cancel_fixture(client,"","44444444-4444-4444-8444-444444444444");d=codex_draft_for(codex_selected,false);
        cancel_interrupt_success();assert(d->loaded&&!strcmp(d->images[0],"44444444-4444-4444-8444-444444444444"));
        cancel_fixture(client,"",NULL);d=codex_draft_for(codex_selected,false);cancel_interrupt_success();
        codex_sync_next=0;codex_sync_tick(preview_now_us/1000);assert(codex_syncing);
        lv_textarea_set_text(codex_input,"刷新期间的新输入");cancel_host_draft("迟到的旧草稿");codex_sync_tick(preview_now_us/1000+100);
        assert(d->dirty&&!strcmp(d->text,"刷新期间的新输入")&&!d->images[0][0]);
    }
    puts("CANCELLED_DRAFT_REFRESH_PASS both_clients_text_photo_newer_text_photo_and_inflight_revision_guard");
}
