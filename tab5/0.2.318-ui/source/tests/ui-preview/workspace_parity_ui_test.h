static void check_workspace_parity_ui(void) {
    const char *fixture="{\"epochMilliseconds\":1793758000000,\"pcInput\":{\"session\":\"workspace-fixture\"},\"codex\":{\"state\":\"idle\"},\"claude\":{\"state\":\"idle\"},\"codexTasks\":{\"projects\":[{\"id\":\"1111111111111111\",\"name\":\"AI-bot · Codex\"}],\"tasks\":[{\"id\":\"11111111-1111-4111-8111-111111111111\",\"projectId\":\"1111111111111111\",\"title\":\"验收会话 A · Codex\",\"updatedAt\":1793758000}]},\"claudeTasks\":{\"projects\":[{\"id\":\"2222222222222222\",\"name\":\"AI-bot · Claude\"}],\"tasks\":[{\"id\":\"22222222-2222-4222-8222-222222222222\",\"projectId\":\"2222222222222222\",\"title\":\"验收会话 A · Claude · 长标题与会话历史检查\",\"updatedAt\":1793758001},{\"id\":\"33333333-3333-4333-8333-333333333333\",\"projectId\":\"2222222222222222\",\"title\":\"验收会话 B · 语音与图片\",\"updatedAt\":1793757990}]}}";
    cJSON_Delete(shown);shown=cJSON_Parse(fixture);assert(shown);
    workspace_client=0;preview_usb_connected=true;last_link=LINK_USB;last_received=preview_now_us/1000;
    page(8);codex_default_pending=codex_default_all=true;render_codex();
    assert(!strcmp(codex_selected,"11111111-1111-4111-8111-111111111111"));
    codex_set_text("Codex 草稿仅在 Codex 恢复");codex_draft *a=codex_draft_for(codex_selected,true);
    assert(codex_store_text(a,lv_textarea_get_text(codex_input),false));a->loaded=true;a->dirty=false;
    capture("workspace-codex-draft");
    assert(workspace_choose(1));render_codex();
    assert(!strcmp(codex_selected,"22222222-2222-4222-8222-222222222222"));
    assert(!strcmp(lv_textarea_get_text(codex_input),""));
    codex_set_text("Claude 草稿：请查看图片，语音转写后可继续编辑。");codex_draft *b=codex_draft_for(codex_selected,true);
    assert(codex_store_text(b,lv_textarea_get_text(codex_input),false));b->loaded=true;b->dirty=false;
    capture("workspace-claude-draft");
    lv_obj_update_layout(lv_screen_active());lv_area_t first,second,picker,list;
    lv_obj_get_coords(workspace_buttons[0],&first);lv_obj_get_coords(workspace_buttons[1],&second);
    lv_obj_get_coords(codex_project_picker,&picker);lv_obj_get_coords(codex_list,&list);
    assert(first.x2<second.x1&&first.y2<picker.y1&&picker.y2<list.y1&&lv_obj_get_height(codex_list)==344);
    codex_voice_waiting=true;assert(!workspace_choose(0)&&workspace_client==1);codex_voice_waiting=false;
    photo_flow=true;assert(!workspace_choose(0)&&workspace_client==1);photo_flow=false;
    codex_control_style();
    assert(lv_obj_has_state(codex_send_button,LV_STATE_DISABLED));
    assert(workspace_choose(0));assert(!strcmp(lv_textarea_get_text(codex_input),a->text));
    lv_obj_update_layout(lv_screen_active());assert(lv_obj_get_height(codex_list)==328);
    codex_control_style();
    assert(!lv_obj_has_state(codex_send_button,LV_STATE_DISABLED));
    cJSON *cx=item(shown,"codexTasks");
    cJSON_AddItemToObject(cx,"control",cJSON_Parse("{\"taskId\":\"11111111-1111-4111-8111-111111111111\",\"running\":true,\"turnId\":\"55555555-5555-4555-8555-555555555555\"}"));
    codex_control_style();
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(codex_send_button,0)),"追加")&&!lv_obj_has_state(codex_send_button,LV_STATE_DISABLED));
    capture("workspace-codex-append-restored");
    cJSON_DeleteItemFromObject(cx,"control");
    codex_set_text("Codex 更新后的草稿");assert(!workspace_choose(1));assert(workspace_switch_pending==1&&workspace_client==0&&a->dirty);
    a->dirty=false;workspace_switch_tick();assert(workspace_client==1&&!strcmp(lv_textarea_get_text(codex_input),b->text));
    capture("workspace-claude-restored");
    // Single-client layouts keep their full-width logo tab; both clients restore
    // the two-button layout. Unsent composers were tested separately above.
    codex_set_text("");
    assert(codex_store_text(a,"",false)&&codex_store_text(b,"",false));a->dirty=b->dirty=false;
    cJSON *codex_saved=cJSON_DetachItemFromObject(shown,"codexTasks");
    preview_now_us+=9000000;last_received=preview_now_us/1000;
    render_codex();lv_obj_update_layout(lv_screen_active());assert(lv_obj_has_flag(workspace_buttons[0],LV_OBJ_FLAG_HIDDEN));
    assert(!lv_obj_has_flag(workspace_buttons[1],LV_OBJ_FLAG_HIDDEN)&&lv_obj_get_width(workspace_buttons[1])==342);
    capture("workspace-claude-only");
    cJSON_AddItemToObject(shown,"codexTasks",codex_saved);
    cJSON *claude_saved=cJSON_DetachItemFromObject(shown,"claudeTasks");
    preview_now_us+=9000000;last_received=preview_now_us/1000;
    render_codex();lv_obj_update_layout(lv_screen_active());assert(workspace_client==0&&lv_obj_has_flag(workspace_buttons[1],LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_get_width(workspace_buttons[0])==342);capture("workspace-codex-only");
    cJSON_AddItemToObject(shown,"claudeTasks",claude_saved);render_codex();lv_obj_update_layout(lv_screen_active());
    assert(!lv_obj_has_flag(workspace_buttons[0],LV_OBJ_FLAG_HIDDEN)&&!lv_obj_has_flag(workspace_buttons[1],LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_get_width(workspace_buttons[0])==166&&lv_obj_get_width(workspace_buttons[1])==166);
    // Overview navigation retains the exact provider AND session, including
    // the second, older Claude conversation instead of its newest default.
    cJSON_AddItemToObject(shown,"claudeSession",cJSON_Parse("{\"taskId\":\"33333333-3333-4333-8333-333333333333\"}"));
    page(0);home_companion_provider=0;
    lv_obj_send_event(home_task_action,LV_EVENT_CLICKED,NULL);
    assert(current_page==8&&workspace_client==1&&!strcmp(codex_selected,"33333333-3333-4333-8333-333333333333"));
    assert(!lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN)&&codex_reply_return==0);
    capture("workspace-overview-exact-claude-session");
    codex_nav_queued=codex_nav_pending=false;codex_reply_close(NULL);assert(current_page==0);
    codex_reading=true;workspace_open_task(0,"11111111-1111-4111-8111-111111111111",0);
    assert(workspace_client==1&&workspace_open_target[0]);codex_reading=false;workspace_open_tick();
    assert(workspace_client==0&&!workspace_open_target[0]&&!strcmp(codex_selected,"11111111-1111-4111-8111-111111111111"));
    assert(!lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN)&&codex_reply_return==0);
    codex_nav_queued=codex_nav_pending=false;codex_reply_close(NULL);assert(current_page==0);
    // Both overview cards share wording, exact navigation and unread opening.
    home_models=1;page(0);render_home_task();
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(home_task_action,0)),"查看对话"));
    capture("workspace-claude-overview-entry");
    home_models=2;render_home_task();
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(home_task_action,0)),"查看对话"));
    capture("workspace-codex-overview-entry");
    if(!codex_notices)codex_notices=calloc(80,sizeof(*codex_notices));
    for(unsigned client=0;client<2;client++) {
        const char *target=client?"33333333-3333-4333-8333-333333333333":"11111111-1111-4111-8111-111111111111";
        snprintf(codex_notices[0].task,37,"%s",target);snprintf(codex_notices[0].turn,64,"completed-turn");codex_notices[0].unread=true;
        home_models=client?1:2;page(0);render_home_task();
        assert(!strcmp(lv_label_get_text(lv_obj_get_child(home_task_action,0)),"查看回复"));
        codex_reading=false;codex_nav_queued=codex_nav_pending=false;
        lv_obj_send_event(home_task_action,LV_EVENT_CLICKED,NULL);
        assert(current_page==8&&workspace_client==client&&!strcmp(codex_selected,target));
        assert(!lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN)&&!strcmp(codex_reply_selected,"completed-turn"));
        assert(codex_notices[0].unread); // Navigation alone is never a read receipt.
        capture(client?"workspace-claude-overview-unread":"workspace-codex-overview-unread");
        codex_nav_queued=codex_nav_pending=codex_reading=false;codex_reply_close(NULL);assert(current_page==0);codex_notices[0].unread=false;
    }
    page(0);home_models=1;cJSON_AddNumberToObject(shown,"aiDialogueVersion",1);
    cJSON_DeleteItemFromObject(shown,"claudeTasks");render_home_task();
    assert(lv_obj_has_state(home_task_action,LV_STATE_DISABLED));
    lv_obj_send_event(home_task_action,LV_EVENT_CLICKED,NULL);assert(current_page==0);
    capture("workspace-modern-catalog-unavailable");
    puts("WORKSPACE_PARITY_UI_OK actual_LVGL_layout_provider_catalog_and_draft_isolation_busy_switch_guard_waits_durable_sync");
}
