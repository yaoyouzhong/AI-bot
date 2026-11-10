static void workspace_switch_settle_animation(void) {
    for(int i=0;i<20;i++){lv_tick_inc(50);lv_anim_refr_now();}
}
static void check_workspace_switch_sync(void) {
    const char *fixture="{\"epochMilliseconds\":1793758000000,\"pcInput\":{\"session\":\"workspace-fixture\"},\"codex\":{\"state\":\"idle\"},\"claude\":{\"state\":\"idle\"},\"codexTasks\":{\"projects\":[{\"id\":\"1111111111111111\",\"name\":\"AI-bot · Codex\"}],\"tasks\":[{\"id\":\"11111111-1111-4111-8111-111111111111\",\"projectId\":\"1111111111111111\",\"title\":\"切换验收 · Codex\",\"updatedAt\":1793758000}]},\"claudeTasks\":{\"projects\":[{\"id\":\"2222222222222222\",\"name\":\"AI-bot · Claude\"}],\"tasks\":[{\"id\":\"22222222-2222-4222-8222-222222222222\",\"projectId\":\"2222222222222222\",\"title\":\"切换验收 · Claude\",\"updatedAt\":1793758001}]}}";
    cJSON_Delete(shown);shown=cJSON_Parse(fixture);assert(shown);
    workspace_client=0;workspace_switch_pending=-1;preview_usb_connected=true;last_link=LINK_USB;last_received=preview_now_us/1000;
    page(8);codex_default_pending=codex_default_all=true;render_codex();
    codex_draft *a=codex_draft_for(codex_selected,true);assert(codex_store_text(a,"Codex 原草稿",false));a->loaded=true;codex_load_draft();
    assert(workspace_choose(1));
    codex_draft *b=codex_draft_for(codex_selected,true);assert(codex_store_text(b,"Claude 原草稿",false));b->loaded=true;codex_load_draft();
    snprintf(a->request,sizeof(a->request),"%s","aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    snprintf(a->images[0],sizeof(a->images[0]),"%s","bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    snprintf(b->images[0],sizeof(b->images[0]),"%s","cccccccc-cccc-4ccc-8ccc-cccccccccccc");
    int submissions=preview_submit_count;
    // Local selection is immediate while the original read retains transport
    // ownership. Neither a client change nor a late reply changes any draft.
    for(int i=0;i<12;i++) {
        unsigned before=workspace_client,next=1-before;
        codex_reading=true;
        codex_read_client=before;
        assert(workspace_choose(next)&&workspace_client==next&&codex_reading);
        if(i==0){workspace_switch_settle_animation();capture("workspace-switch-reading-immediate");}
        assert(workspace_switch_pending==-1);
        codex_reading=false;workspace_switch_tick();
        assert(workspace_client==next&&workspace_switch_pending==-1);
        assert(!lv_label_get_text(codex_voice_hint)[0]);
        assert(!strcmp(lv_textarea_get_text(codex_input),next?b->text:a->text));
        assert(!strcmp(a->request,"aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
        assert(!strcmp(a->images[0],"bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));
        assert(!strcmp(b->images[0],"cccccccc-cccc-4ccc-8ccc-cccccccccccc"));
    }
    assert(!lv_label_get_text(codex_receipt)[0]);
    workspace_switch_settle_animation();
    capture("workspace-switch-completed");
    // A late old-client reply cannot paint the new client even if task IDs
    // collide. Returning before completion is additionally revision-guarded.
    unsigned current=workspace_client;
    codex_reading=true;codex_read_client=current;codex_read_revision=codex_nav_revision;
    snprintf(codex_read_session,sizeof(codex_read_session),"workspace-fixture");
    assert(workspace_choose(1-current));
    snprintf(codex_read_task,sizeof(codex_read_task),"%s",codex_selected);
    preview_read_view=cJSON_Parse("{\"text\":\"OLD CLIENT MUST NOT APPEAR\",\"replyView\":{}}");
    preview_result_code=200;codex_read_finish(preview_now_us/1000);
    assert(!codex_reading&&!preview_read_view&&!codex_inline_reply);
    assert(workspace_choose(current)&&workspace_switch_pending==-1);
    // Active input operations keep their old protection and never queue.
    bool *busy_flags[]={&workspace_stopping,&quick_visible,&photo_flow,&photo_open_pending,&codex_waiting,&codex_voice_waiting,&codex_voice_start_pending,&codex_send_queued};
    for(unsigned i=0;i<sizeof(busy_flags)/sizeof(busy_flags[0]);i++){
        *busy_flags[i]=true;assert(!workspace_choose(1-current));
        assert(workspace_switch_pending==-1&&workspace_client==current);*busy_flags[i]=false;
    }
    // A draft load finishes against the original client after switching.
    b->dirty=false;b->loaded=false;memset(b->images,0,sizeof(b->images));
    assert(codex_store_text(b,"",false));codex_load_draft();
    snprintf(codex_sync_task,sizeof(codex_sync_task),"%s",b->id);
    snprintf(codex_sync_request,sizeof(codex_sync_request),"%s","dddddddd-dddd-4ddd-8ddd-dddddddddddd");
    snprintf(codex_sync_op,sizeof(codex_sync_op),"%s","draft-load");
    codex_sync_revision=b->revision;codex_sync_client=1;codex_syncing=true;preview_result_code=200;
    int64_t original_deadline=codex_sync_next;
    assert(workspace_choose(0)&&workspace_switch_pending==-1&&codex_syncing);
    assert(codex_sync_next==original_deadline);
    codex_sync_tick(preview_now_us/1000);assert(codex_load_waiting&&workspace_client==0);
    cJSON *remote=item(shown,"claudeTasks");
    cJSON_AddItemToObject(remote,"draftView",cJSON_Parse("{\"taskId\":\"22222222-2222-4222-8222-222222222222\",\"requestId\":\"dddddddd-dddd-4ddd-8ddd-dddddddddddd\",\"text\":\"从电脑恢复的 Claude 草稿\",\"images\":[]}"));
    codex_sync_tick(preview_now_us/1000+100);workspace_switch_tick();
    assert(workspace_client==0&&b->loaded&&!b->dirty&&!strcmp(b->text,"从电脑恢复的 Claude 草稿"));
    assert(!strcmp(lv_textarea_get_text(codex_input),a->text));
    assert(workspace_choose(1));assert(!strcmp(lv_textarea_get_text(codex_input),b->text));
    // An edit made before the queued click is persisted before switching;
    // a short save has no hint and a longer save receives one after 350 ms.
    codex_set_text("Claude 保留新编辑");codex_reading=true;assert(!workspace_choose(0)&&b->dirty);
    assert(!lv_label_get_text(codex_voice_hint)[0]);
    preview_now_us+=300000;workspace_switch_tick();assert(!lv_label_get_text(codex_voice_hint)[0]);
    preview_now_us+=100000;workspace_switch_tick();assert(strstr(lv_label_get_text(codex_voice_hint),"保存草稿"));
    workspace_switch_settle_animation();capture("workspace-switch-long-save");
    codex_reading=false;workspace_switch_tick();assert(workspace_client==1);
    preview_submit_enabled=true;codex_sync_next=0;preview_result_code=-1;
    codex_sync_tick(preview_now_us/1000+200);assert(codex_syncing&&!strcmp(preview_operation,"draft-save"));
    assert(!strcmp(codex_sync_task,b->id));
    preview_result_code=200;codex_sync_tick(preview_now_us/1000+300);workspace_switch_tick();
    assert(workspace_client==0&&!b->dirty&&!strcmp(b->text,"Claude 保留新编辑"));
    // A timed-out old load cannot block or overwrite a new client's view.
    a->request[0]=0;memset(a->images,0,sizeof(a->images));
    assert(codex_store_text(a,"",false));a->dirty=false;a->loaded=false;codex_load_draft();
    codex_load_waiting=true;codex_sync_client=0;snprintf(codex_sync_task,sizeof(codex_sync_task),"%s",a->id);
    assert(workspace_choose(1)&&workspace_switch_pending==-1);
    int syncs=preview_sync_count;codex_sync_next=0;
    codex_sync_tick(preview_now_us/1000+500);workspace_switch_tick();
    assert(workspace_client==1&&!a->loaded&&preview_sync_count==syncs);
    assert(!lv_label_get_text(codex_voice_hint)[0]);
    workspace_switch_settle_animation();capture("workspace-switch-draft-preserved");
    assert(preview_submit_count==submissions);
    // A revision changed after loading started is never overwritten.
    assert(workspace_choose(0));
    codex_load_waiting=true;codex_sync_client=0;codex_sync_revision=a->revision;
    snprintf(codex_sync_task,sizeof(codex_sync_task),"%s",a->id);
    snprintf(codex_sync_request,sizeof(codex_sync_request),"dddddddd-dddd-4ddd-8ddd-dddddddddddd");
    cJSON_AddItemToObject(item(shown,"codexTasks"),"draftView",cJSON_Parse("{\"taskId\":\"11111111-1111-4111-8111-111111111111\",\"requestId\":\"dddddddd-dddd-4ddd-8ddd-dddddddddddd\",\"text\":\"stale draft\",\"images\":[]}"));
    codex_set_text("新的本机编辑");assert(codex_save_draft());
    codex_sync_tick(preview_now_us/1000+700);
    assert(!strcmp(a->text,"新的本机编辑")&&!strcmp(lv_textarea_get_text(codex_input),a->text));
    assert(preview_submit_count==submissions);
    puts("WORKSPACE_SWITCH_SYNC_OK immediate_bidirectional_reads_old_client_reply_discarded_load_client_binding_revision_guard_delayed_hint_input_guards_drafts_images_unknown_preserved_no_submission");
}
