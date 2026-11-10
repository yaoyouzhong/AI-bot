static void check_session_unread(void) {
    int sends=preview_submit_count;
    reply_open_state_setup(1);
    codex_draft *original_draft=codex_draft_for(codex_selected,true);
    assert(codex_store_text(original_draft,"保留这段尚未发送的草稿",false));
    original_draft->loaded=true;original_draft->dirty=false;codex_load_draft();
    if(!codex_notices)codex_notices=calloc(80,sizeof(*codex_notices));
    assert(codex_notices);memset(codex_notices,0,80*sizeof(*codex_notices));
    const char *ids[]={"11111111-1111-4111-8111-111111111111","22222222-2222-4222-8222-222222222222","33333333-3333-4333-8333-333333333333","99999999-9999-4999-8999-999999999999"};
    const char *titles[]={"AI-bot-原生终端","很长的中文会话名称用于检查截断和来源标签","其他项目的新回复"};
    cJSON *remote=item(shown,"claudeTasks"),*tasks=item(remote,"tasks"),*first=cJSON_GetArrayItem(tasks,0);
    for(int i=0;i<3;i++) {
        cJSON *task=i?cJSON_Duplicate(first,true):first;
        cJSON_ReplaceItemInObject(task,"id",cJSON_CreateString(ids[i]));
        cJSON_ReplaceItemInObject(task,"title",cJSON_CreateString(titles[i]));
        cJSON_DeleteItemFromObject(task,"source");
        cJSON *source=cJSON_AddObjectToObject(task,"source");
        cJSON_AddStringToObject(source,"kind","terminal");cJSON_AddStringToObject(source,"label","原生终端");
        if(i==2)cJSON_ReplaceItemInObject(task,"projectId",cJSON_CreateString("2222222222222222"));
        if(i)cJSON_AddItemToArray(tasks,task);
    }
    cJSON *project=cJSON_CreateObject();cJSON_AddStringToObject(project,"id","2222222222222222");cJSON_AddStringToObject(project,"name","另一个项目");cJSON_AddItemToArray(item(remote,"projects"),project);
    cJSON *other=cJSON_Duplicate(remote,true);cJSON_AddItemToObject(shown,"codexTasks",other);
    cJSON *otherTasks=item(other,"tasks");while(cJSON_GetArraySize(otherTasks)>1)cJSON_DeleteItemFromArray(otherTasks,1);
    cJSON *otherTask=cJSON_GetArrayItem(otherTasks,0);cJSON_ReplaceItemInObject(otherTask,"id",cJSON_CreateString(ids[3]));cJSON_ReplaceItemInObject(otherTask,"title",cJSON_CreateString("Codex 日常会话"));
    int order[]={10,20,30,15};
    for(int i=0;i<4;i++){snprintf(codex_notices[i].task,37,"%s",ids[i]);snprintf(codex_notices[i].turn,64,"unread-%d",i);codex_notices[i].unread=true;codex_notices[i].arrived=order[i];}
    codex_notices[4]=(codex_notice){.task="88888888-8888-4888-8888-888888888888",.turn="catalog-absent",.unread=true,.arrived=99};
    render_codex();lv_obj_update_layout(lv_screen_active());
    assert(session_unread_count(1,NULL)==3&&session_unread_count(0,NULL)==1);
    assert((tab5_codex_notice_state&255)==4&&strstr(lv_dropdown_get_options(codex_project_picker),"[2] AI-bot")&&strstr(lv_dropdown_get_options(codex_project_picker),"[1] 另一个项目"));
    for(int i=0;i<2;i++) {
        lv_area_t row,title,badge,source;lv_obj_get_coords(codex_task_rows[i],&row);lv_obj_get_coords(codex_task_titles[i],&title);
        lv_obj_get_coords(session_unread_rows[i],&badge);lv_obj_get_coords(session_source_rows[i],&source);
        assert(!lv_obj_has_flag(session_unread_rows[i],LV_OBJ_FLAG_HIDDEN));
        assert(title.x2<badge.x1&&badge.x2<row.x2&&badge.y1>=row.y1&&badge.y2<source.y1);
    }
    capture("unread-claude-sessions");
    for(int i=0;i<24;i++)tab5_draw_cache_step(pages[8]);
    assert(tab5_draw_cache_is_ready(pages[8]));
    render_codex();
    assert(tab5_draw_cache_is_ready(pages[8]));
    cJSON *missing=cJSON_DetachItemFromArray(tasks,2);render_codex();
    assert(session_unread_count(1,NULL)==2&&codex_notices[2].unread&&codex_notices[4].unread);
    cJSON_AddItemToArray(tasks,missing);render_codex();assert(session_unread_count(1,NULL)==3);
    // A stale acknowledgement cannot clear a newer unseen reply.
    cJSON *read=cJSON_AddObjectToObject(remote,"repliesRead");cJSON_AddStringToObject(read,ids[2],"old-turn");
    cJSON *ready=cJSON_AddObjectToObject(remote,"repliesReady");cJSON_AddStringToObject(ready,ids[2],"unread-2");
    codex_notices_ready=true;workspace_notice_tick(30000,true,1);assert(codex_notices[2].unread);
    // Opening a different project uses the existing cached exact-turn path.
    cJSON *reply=cJSON_Parse("{\"taskId\":\"33333333-3333-4333-8333-333333333333\",\"receipt\":\"已完成\",\"text\":\"这是你尚未查看的新回复。\",\"replyView\":{\"taskId\":\"33333333-3333-4333-8333-333333333333\",\"turnId\":\"unread-2\",\"state\":\"completed\",\"page\":0,\"pageCount\":1,\"history\":false,\"hasText\":true}}");
    codex_cache_store(reply);cJSON_Delete(reply);workspace_open_task(1,ids[2],8);render_codex();
    assert(!strcmp(codex_selected,ids[2])&&!strcmp(codex_project_selected,"2222222222222222")&&codex_reply_return==8);
    assert(!codex_notices[2].unread&&codex_notices[0].unread&&codex_notices[1].unread&&codex_notices[3].unread);
    assert(!strcmp(original_draft->text,"保留这段尚未发送的草稿"));
    capture("unread-opened-reply");visible(codex_reply_overlay,false);
    assert(workspace_choose(0));render_codex();capture("unread-codex-session");
    assert(session_unread_task(ids[3])&&!lv_obj_has_flag(session_unread_rows[0],LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_get_y(session_unread_rows[0])==lv_obj_get_y(codex_task_titles[0])+2);
    // Claude-only unread must not mark the Codex client or its project label.
    codex_notices[3].unread=false;render_codex();lv_obj_update_layout(lv_screen_active());
    assert(lv_obj_has_flag(session_unread_clients[0],LV_OBJ_FLAG_HIDDEN));
    assert(!lv_obj_has_flag(session_unread_clients[1],LV_OBJ_FLAG_HIDDEN));
    assert(!strcmp(lv_label_get_text(session_project_label),"项目"));
    assert(!lv_obj_has_flag(session_project_label,LV_OBJ_FLAG_CLICKABLE));
    assert(lv_obj_get_width(session_unread_clients[1])==20);
    assert(lv_obj_get_style_text_font(lv_obj_get_child(session_unread_clients[1],0),0)==&lv_font_montserrat_14);
    for(int i=0;i<2;i++){
        lv_area_t button,badge;lv_obj_get_coords(workspace_buttons[i],&button);lv_obj_get_coords(session_unread_clients[i],&badge);
        assert(button.x2-badge.x2>=8&&badge.x1>button.x1&&badge.y1>button.y1&&badge.y2<button.y2);
    }
    capture("unread-claude-only-on-codex");
    // The real 'view new reply' action must target unread, not a newer empty turn.
    assert(workspace_choose(1));
    snprintf(codex_project_selected,sizeof(codex_project_selected),"1111111111111111");
    codex_restore_project=codex_default_pending=false;codex_select_task(ids[0]);render_codex();
    reply=cJSON_Parse("{\"taskId\":\"11111111-1111-4111-8111-111111111111\",\"receipt\":\"状态未知\",\"text\":\"本轮尚无公开文字。\",\"replyView\":{\"taskId\":\"11111111-1111-4111-8111-111111111111\",\"turnId\":\"new-empty-turn\",\"state\":\"unknown\",\"page\":0,\"pageCount\":1,\"history\":false,\"hasText\":false}}");
    codex_cache_store(reply);cJSON_Delete(reply);
    codex_reply_open(NULL);
    assert(!strcmp(codex_reply_selected,"unread-0")&&!codex_reply_follow&&codex_nav_pending);
    assert(codex_notices[0].unread); // cold read/empty latest does not clear unread
    reply=cJSON_Parse("{\"taskId\":\"11111111-1111-4111-8111-111111111111\",\"receipt\":\"已完成\",\"text\":\"这是对应未读标识的回复，不是后面尚无文字的一轮。\",\"replyView\":{\"taskId\":\"11111111-1111-4111-8111-111111111111\",\"turnId\":\"unread-0\",\"state\":\"completed\",\"page\":0,\"pageCount\":1,\"history\":true,\"hasText\":true}}");
    codex_cache_store(reply);cJSON_Delete(reply);codex_reply_open(NULL);render_codex();
    assert(!strcmp(codex_reply_turn,"unread-0")&&!codex_notices[0].unread&&codex_notices[1].unread);
    assert(strstr(lv_label_get_text(codex_reply_text),"对应未读标识"));
    capture("unread-exact-reply-before-empty-turn");
    // The compact label and its largest supported count fit without clipping.
    lv_font_glyph_dsc_t glyph;assert(lv_font_get_glyph_dsc(&tab5_source_20,&glyph,0x8bfb,0));
    ui_text(lv_obj_get_child(session_unread_clients[1],0),"9+");session_unread_center(lv_obj_get_child(session_unread_clients[1],0));
    lv_obj_update_layout(lv_screen_active());
    lv_area_t badge,count;lv_obj_get_coords(session_unread_clients[1],&badge);lv_obj_get_coords(lv_obj_get_child(session_unread_clients[1],0),&count);
    assert(count.x1>=badge.x1&&count.x2<=badge.x2&&count.y1>=badge.y1&&count.y2<=badge.y2);
    visible(codex_reply_overlay,false);capture("unread-small-count-9plus");
    assert(preview_submit_count==sends);
    puts("SESSION_UNREAD_OK row/client/project markers; smaller client count; client isolation; exact unread before empty latest; long CJK bounds; cache stability; reduced catalog retention; no sends");
}
