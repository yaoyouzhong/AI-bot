static void check_session_source(void) {
    int sends=preview_submit_count,reads=preview_reads;
    reply_open_state_setup(1);preview_usb_connected=true;last_link=LINK_USB;
    const char *ids[]={"11111111-1111-4111-8111-111111111111","22222222-2222-4222-8222-222222222222","33333333-3333-4333-8333-333333333333","44444444-4444-4444-8444-444444444444"};
    const char *names[]={"VS Code终端","Claude Code原生终端","工作流示例","历史会话"};
    const char *labels[]={"VS Code","原生终端","Example Harness","来源未知"};
    const char *kinds[]={"ide","terminal","harness","unknown"};
    cJSON *remote=item(shown,"claudeTasks"),*tasks=item(remote,"tasks"),*first=cJSON_GetArrayItem(tasks,0);
    cJSON *controls=cJSON_AddObjectToObject(remote,"controls");
    for(int i=0;i<4;i++) {
        cJSON *task=i?cJSON_Duplicate(first,true):first;
        cJSON_ReplaceItemInObject(task,"id",cJSON_CreateString(ids[i]));cJSON_ReplaceItemInObject(task,"title",cJSON_CreateString(names[i]));
        cJSON_DeleteItemFromObject(task,"source");
        if(i<3){cJSON *source=cJSON_AddObjectToObject(task,"source");cJSON_AddStringToObject(source,"id",i==0?"claude.vscode":i==1?"claude.terminal":"example.workflow");cJSON_AddStringToObject(source,"label",labels[i]);cJSON_AddStringToObject(source,"kind",kinds[i]);}
        if(i)cJSON_AddItemToArray(tasks,task);
        cJSON *control=ready_control(ids[i],false);if(i>=2)cJSON_ReplaceItemInObject(control,"canSend",cJSON_CreateFalse());cJSON_AddItemToObject(controls,ids[i],control);
    }
    codex_select_task(ids[0]);render_codex();lv_obj_update_layout(lv_screen_active());
    for(int i=0;i<4;i++) {
        lv_area_t row,tag,list;lv_obj_get_coords(codex_task_rows[i],&row);lv_obj_get_coords(session_source_rows[i],&tag);lv_obj_get_coords(codex_list,&list);
        assert(tag.x1>=row.x1&&tag.x2<=row.x2&&tag.y1>=row.y1&&tag.y2<row.y2&&tag.y2<=list.y2);
        assert(!lv_obj_has_flag(session_source_rows[i],LV_OBJ_FLAG_CLICKABLE));
        assert(!strcmp(lv_label_get_text(lv_obj_get_child(session_source_rows[i],0)),labels[i]));
    }
    capture("source-vscode");codex_select_task(ids[1]);render_codex();capture("source-native");
    assert(!lv_obj_has_state(codex_send_button,LV_STATE_DISABLED));
    cJSON_ReplaceItemInObject(controls,ids[1],cJSON_CreateNull());render_codex();
    assert(lv_obj_has_state(codex_send_button,LV_STATE_DISABLED));assert(!lv_obj_has_flag(session_source_compose,LV_OBJ_FLAG_HIDDEN));
    codex_select_task(ids[2]);render_codex();assert(lv_obj_has_state(codex_send_button,LV_STATE_DISABLED));capture("source-harness");
    cJSON *custom=cJSON_GetArrayItem(tasks,2),*source=item(custom,"source");
    cJSON_ReplaceItemInObject(source,"label",cJSON_CreateString("自定义工作流程与第三方终端"));
    cJSON_ReplaceItemInObject(custom,"title",cJSON_CreateString("很长的工作流会话名称用于检验单行截断与按钮布局"));
    cJSON_AddStringToObject(item(remote,"receipts"),ids[2],"运行中");render_codex();lv_obj_update_layout(lv_screen_active());
    assert(lv_obj_get_x(session_source_compose)+lv_obj_get_width(session_source_compose)<=280);
    assert(lv_obj_get_height(session_source_rows[2])==32);capture("source-long-running");
    for(int i=0;i<24;i++)tab5_draw_cache_step(pages[8]);
    assert(tab5_draw_cache_is_ready(pages[8]));
    render_codex();assert(tab5_draw_cache_is_ready(pages[8]));
    codex_select_task(ids[0]);render_codex();
    cJSON *reply=cJSON_Parse("{\"taskId\":\"11111111-1111-4111-8111-111111111111\",\"receipt\":\"已完成\",\"text\":\"FOCUS_READY_1107\",\"replyView\":{\"taskId\":\"11111111-1111-4111-8111-111111111111\",\"turnId\":\"completed-source\",\"state\":\"completed\",\"page\":0,\"pageCount\":1,\"history\":false,\"hasText\":true}}");
    assert(preview_reads==reads);preview_usb_connected=false;
    codex_cache_store(reply);codex_open_latest=true;codex_reply_open(NULL);render_codex();
    assert(!lv_label_get_text(codex_reply_position)[0]);capture("source-completed-reply");cJSON_Delete(reply);
    // Cached completion opens without a foreground read; no send path is invoked.
    assert(preview_submit_count==sends&&preview_reads==reads);
    reply_open_state_setup(0);render_codex();assert(lv_obj_has_flag(session_source_compose,LV_OBJ_FLAG_HIDDEN));assert(lv_obj_get_height(codex_list)==328);
    puts("SESSION_SOURCE_OK source/capability independence; legacy unknown; long CJK; running header; fourth-row bounds; cached reply; Codex layout; zero reads/sends");
}
