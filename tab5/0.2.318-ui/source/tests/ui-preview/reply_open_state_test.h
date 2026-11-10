static void reply_open_state_setup(unsigned client) {
    cJSON_Delete(shown);shown=cJSON_Parse("{\"epochMilliseconds\":1793758000000,\"pcInput\":{\"session\":\"reply-fixture\"},\"codex\":{\"state\":\"idle\"},\"claude\":{\"state\":\"idle\"}}");
    const char *task="11111111-1111-4111-8111-111111111111";
    cJSON *remote=cJSON_AddObjectToObject(shown,client?"claudeTasks":"codexTasks");
    cJSON *projects=cJSON_AddArrayToObject(remote,"projects"),*project=cJSON_CreateObject();cJSON_AddItemToArray(projects,project);
    cJSON_AddStringToObject(project,"id","1111111111111111");cJSON_AddStringToObject(project,"name","AI-bot");
    cJSON *tasks=cJSON_AddArrayToObject(remote,"tasks"),*entry=cJSON_CreateObject();cJSON_AddItemToArray(tasks,entry);
    cJSON_AddStringToObject(entry,"id",task);cJSON_AddStringToObject(entry,"projectId","1111111111111111");cJSON_AddStringToObject(entry,"title",client?"VS Code终端":"Codex 日常会话");
    cJSON_AddNumberToObject(entry,"updatedAt",1793758000);
    cJSON_AddStringToObject(cJSON_AddObjectToObject(remote,"receipts"),task,"已完成");
    workspace_client=client;workspace_switch_pending=-1;codex_selected[0]=codex_project_selected[0]=0;
    cJSON_Delete(codex_inline_reply);codex_inline_reply=NULL;
    for(int i=0;i<CODEX_CACHE_PAGES;i++){cJSON_Delete(codex_reply_cache[i].value);codex_reply_cache[i]=(codex_cached_page){0};}
    codex_showing_cache=codex_nav_pending=codex_nav_queued=codex_nav_timed_out=codex_turn_pending=false;
    codex_reading=codex_syncing=codex_load_waiting=codex_waiting=codex_send_queued=false;
    codex_reply_turn[0]=codex_reply_selected[0]=0;preview_now_us=30000000;last_received=30000;
    wifi_connected=ble_connected=preview_usb_connected=false;preview_read_recent=false;
    page(8);codex_default_pending=codex_default_all=true;render_codex();
    codex_draft *draft=codex_draft_for(codex_selected,true);draft->loaded=true;draft->dirty=false;codex_load_draft();
    codex_syncing=codex_load_waiting=false;preview_result_code=-1;
}
static void check_reply_open_state(void) {
    int sends=preview_submit_count;
    for(unsigned client=0;client<2;client++) {
        reply_open_state_setup(client);
        cJSON *reply=cJSON_Parse("{\"taskId\":\"11111111-1111-4111-8111-111111111111\",\"receipt\":\"已完成\",\"text\":\"这条回复已经完成。重新打开时，正文和完成状态应立即显示。\",\"replyView\":{\"taskId\":\"11111111-1111-4111-8111-111111111111\",\"turnId\":\"completed-turn\",\"state\":\"completed\",\"page\":0,\"pageCount\":1,\"history\":false,\"hasText\":true}}");
        codex_cache_store(reply);codex_open_latest=true;codex_reply_open(NULL);
        printf("REPLY_OPEN_INITIAL client=%u status=%s\n",client,lv_label_get_text(codex_reply_status));
        assert(!strcmp(lv_label_get_text(codex_reply_status),"已完成"));
        render_codex();assert(!strcmp(lv_label_get_text(codex_reply_status),"已完成"));
        assert(!lv_label_get_text(codex_reply_position)[0]);
        assert(codex_showing_cache&&!codex_reading&&!codex_nav_pending);
        for(int i=0;i<20;i++){lv_tick_inc(50);lv_anim_refr_now();}
        capture(client?"claude-completed-open":"codex-completed-open");
        preview_now_us+=4000000;codex_reply_refresh(NULL);render_codex();
        assert(!strcmp(lv_label_get_text(codex_reply_status),"已完成"));
        assert(!lv_label_get_text(codex_reply_position)[0]);
        capture(client?"claude-completed-wait":"codex-completed-wait");
        preview_usb_connected=true;last_received=preview_now_us/1000;codex_reply_refresh(NULL);assert(codex_reading);
        preview_result_code=503;codex_read_finish(preview_now_us/1000);
        assert(!strcmp(lv_label_get_text(codex_reply_status),"已完成"));
        preview_result_code=-1;codex_reply_refresh(NULL);assert(codex_reading);
        preview_read_view=cJSON_Duplicate(reply,true);preview_result_code=200;codex_read_finish(preview_now_us/1000);
        assert(!strcmp(lv_label_get_text(codex_reply_status),"已完成"));
        assert(!lv_label_get_text(codex_reply_position)[0]);
        assert(!codex_showing_cache&&!codex_nav_pending);
        // A periodic frame without a receipt must not erase the direct result
        // when bookmarking the visible page (the former Claude-only mismatch).
        cJSON_DeleteItemFromObject(item(shown,workspace_key()),"receipts");
        codex_cache_visible();cJSON *saved=codex_cache_find(true,"",-1);
        assert(saved&&!strcmp(text(saved,"receipt"),"已完成"));cJSON_Delete(saved);
        cJSON *older=cJSON_Duplicate(reply,true),*older_view=item(older,"replyView");
        cJSON_ReplaceItemInObject(older_view,"turnId",cJSON_CreateString("older-turn"));
        cJSON_ReplaceItemInObject(older_view,"history",cJSON_CreateTrue());
        cJSON_AddStringToObject(older_view,"startedLabel","11-07 10:30");
        cJSON_ReplaceItemInObject(older_view,"pageCount",cJSON_CreateNumber(2));codex_cache_store(older);
        snprintf(codex_reply_selected,sizeof(codex_reply_selected),"older-turn");
        codex_reply_follow=false;codex_requested_page=0;preview_usb_connected=false;
        int reads=preview_reads;codex_navigation_start();
        assert(preview_reads==reads&&!codex_reading&&!codex_nav_pending);
        assert(!strcmp(lv_label_get_text(codex_reply_status),"历史回复 · 11-07 10:30"));
        assert(!strcmp(lv_label_get_text(codex_reply_position),"1 / 2"));
        preview_now_us+=4000000;codex_reply_refresh(NULL);render_codex();
        assert(preview_reads==reads&&!strcmp(lv_label_get_text(codex_reply_position),"1 / 2"));
        cJSON_Delete(older);
        cJSON_Delete(reply);
        // Cold reads wait neutrally, still time out, and never invent a reply.
        reply_open_state_setup(client);codex_open_latest=true;codex_reply_open(NULL);
        assert(codex_nav_pending&&!codex_reading);
        assert(!strstr(lv_label_get_text(codex_reply_status),"连接")&&!strstr(lv_label_get_text(codex_reply_status),"缓存"));
        for(int i=0;i<20;i++){lv_tick_inc(50);lv_anim_refr_now();}
        capture(client?"claude-cold-read":"codex-cold-read");
        preview_now_us+=13000000;codex_navigation_timeout(preview_now_us/1000);
        assert(codex_nav_timed_out&&!codex_nav_pending&&strstr(lv_label_get_text(codex_reply_status),"读取超时"));
        capture(client?"claude-read-timeout":"codex-read-timeout");
        codex_reply_open(NULL);assert(!codex_nav_timed_out&&codex_nav_pending);
    }
    assert(preview_submit_count==sends);
    puts("REPLY_OPEN_STATE_OK both clients; cached completion; direct receipt preservation; history/date/page; cold read; delayed readiness; failed refresh; recovery; timeout/reopen; zero sends");
}

static void reply_pointer_tap(lv_indev_t *pointer,lv_point_t point) {
    camera_touch_point=point;
    camera_touch_state=LV_INDEV_STATE_PRESSED;lv_indev_read(pointer);
    camera_touch_state=LV_INDEV_STATE_RELEASED;lv_indev_read(pointer);
}

static void check_reply_return_touch(void) {
    lv_indev_t *pointer=lv_indev_create();lv_indev_set_type(pointer,LV_INDEV_TYPE_POINTER);
    lv_indev_set_display(pointer,lv_display_get_default());lv_indev_set_read_cb(pointer,camera_touch_read);
    for(unsigned client=0;client<2;client++) {
        reply_open_state_setup(client);
        codex_reply_open(NULL);lv_obj_update_layout(lv_screen_active());
        lv_area_t open,back;lv_obj_get_coords(codex_reply_button,&open);
        lv_obj_t *back_button=NULL;
        for(uint32_t i=0;i<lv_obj_get_child_count(codex_reply_overlay);i++) {
            lv_obj_t *child=lv_obj_get_child(codex_reply_overlay,i);
            if(lv_obj_has_class(child,&lv_button_class)&&!strcmp(lv_label_get_text(lv_obj_get_child(child,0)),"返回")){back_button=child;break;}
        }
        assert(back_button);lv_obj_get_coords(back_button,&back);
        int left=LV_MAX(open.x1,back.x1),right=LV_MIN(open.x2,back.x2);
        int top=LV_MAX(open.y1,back.y1),bottom=LV_MIN(open.y2,back.y2);
        assert(right>left&&bottom>top);
        lv_point_t point={(left+right)/2,(top+bottom)/2};
        reply_pointer_tap(pointer,point);assert(lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN));
        lv_tick_inc(60);reply_pointer_tap(pointer,point);
        assert(lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN));
        capture(client?"claude-after-return":"codex-after-return");
        lv_tick_inc(300);reply_pointer_tap(pointer,point);
        assert(!lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN));
        lv_tick_inc(60);reply_pointer_tap(pointer,point);
        assert(!lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN));
        lv_tick_inc(300);reply_pointer_tap(pointer,point);
        assert(lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN));
        // A deliberate click outside the previous button remains immediate.
        assert(open.y1<back.y1);reply_pointer_tap(pointer,(lv_point_t){point.x,(open.y1+back.y1-1)/2});
        assert(!lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN));
        lv_tick_inc(300);reply_pointer_tap(pointer,point);
        assert(lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN));
        lv_tick_inc(300);
    }
    lv_indev_delete(pointer);puts("REPLY_RETURN_TOUCH_OK both clients; overlapping pointer taps; deliberate re-entry");
}

static void check_reply_unread_loading(void) {
    if(!codex_notices)codex_notices=calloc(80,sizeof(*codex_notices));
    assert(codex_notices);
    for(unsigned client=0;client<2;client++) {
        memset(codex_notices,0,80*sizeof(*codex_notices));
        reply_open_state_setup(client);
        cJSON *old=cJSON_Parse("{\"taskId\":\"11111111-1111-4111-8111-111111111111\",\"receipt\":\"已完成\",\"text\":\"这里是已经看过的上一条回复。\",\"replyView\":{\"taskId\":\"11111111-1111-4111-8111-111111111111\",\"turnId\":\"old-turn\",\"state\":\"completed\",\"page\":0,\"pageCount\":1,\"hasText\":true}}");
        cJSON *new_reply=cJSON_Duplicate(old,true);
        cJSON_ReplaceItemInObject(new_reply,"text",cJSON_CreateString("这里是本轮真正的新回复。"));
        cJSON_ReplaceItemInObject(item(new_reply,"replyView"),"turnId",cJSON_CreateString("unread-turn"));
        codex_cache_store(old);codex_open_latest=true;codex_reply_open(NULL);
        assert(!strcmp(lv_label_get_text(codex_reply_text),text(old,"text")));
        codex_notices[0]=(codex_notice){.task="11111111-1111-4111-8111-111111111111",.turn="unread-turn",.unread=true};
        codex_notices[1]=(codex_notice){.task="22222222-2222-4222-8222-222222222222",.turn="other-turn",.unread=true};
        codex_reply_close(NULL);render_codex();
        assert(!strcmp(lv_label_get_text(lv_obj_get_child(codex_reply_button,0)),"查看新回复"));
        preview_usb_connected=true;codex_reply_open(NULL);
        assert(codex_nav_pending&&codex_reading&&strcmp(lv_label_get_text(codex_reply_text),text(old,"text")));
        assert(!lv_label_get_text(codex_reply_position)[0]);
        capture(client?"claude-unread-loading":"codex-unread-loading");
        preview_read_view=cJSON_Duplicate(old,true);preview_result_code=200;codex_read_finish(30001);
        assert(codex_nav_pending&&codex_notices[0].unread&&strcmp(lv_label_get_text(codex_reply_text),text(old,"text")));
        codex_reply_refresh(NULL);assert(codex_reading);
        preview_read_view=cJSON_Duplicate(new_reply,true);preview_result_code=200;codex_read_finish(31501);
        assert(!codex_nav_pending&&!strcmp(lv_label_get_text(codex_reply_text),text(new_reply,"text")));
        assert(!lv_label_get_text(codex_reply_position)[0]);
        assert(!codex_notices[0].unread&&codex_notices[1].unread);
        capture(client?"claude-unread-correct":"codex-unread-correct");
        codex_reply_close(NULL);
        preview_usb_connected=false;codex_reply_open(NULL);
        assert(codex_reply_follow&&!codex_reply_selected[0]);
        codex_reply_close(NULL);preview_usb_connected=true;
        snprintf(codex_notices[0].turn,sizeof(codex_notices[0].turn),"next-uncached");codex_notices[0].unread=true;
        codex_reply_open(NULL);assert(codex_nav_pending&&codex_reading);
        codex_navigation_timeout(codex_nav_started+12001);
        assert(!codex_nav_pending&&codex_nav_timed_out&&codex_notices[0].unread);
        assert(!strcmp(lv_label_get_text(codex_reply_text),"正在读取会话动态…"));
        codex_reply_close(NULL);
        preview_read_view=cJSON_Duplicate(old,true);preview_result_code=200;codex_read_finish(45000);
        assert(lv_obj_has_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN)&&codex_notices[0].unread);
        cJSON_Delete(old);cJSON_Delete(new_reply);
    }
    puts("REPLY_UNREAD_LOADING_OK both clients; no stale body or read acknowledgement; exact reply");
}

static void check_claude_reply_refresh_rate(void) {
    reply_open_state_setup(1);
    codex_open_latest=true;codex_reply_open(NULL);
    preview_usb_connected=true;last_received=preview_now_us/1000;
    preview_result_code=-1;codex_reply_refresh(NULL);assert(codex_reading);
    preview_read_view=cJSON_Parse("{\"taskId\":\"11111111-1111-4111-8111-111111111111\",\"receipt\":\"运行中\",\"text\":\"正在生成的公开回复\",\"replyView\":{\"taskId\":\"11111111-1111-4111-8111-111111111111\",\"turnId\":\"running-turn\",\"state\":\"inProgress\",\"page\":0,\"pageCount\":1,\"history\":false,\"hasText\":true}}");
    preview_result_code=200;codex_read_finish(1000);
    assert(codex_read_next==1750&&!strcmp(lv_label_get_text(codex_reply_text),"正在生成的公开回复"));
    capture("claude-fast-running-reply");
    assert(codex_success_read_interval(false)==3000);
    lv_obj_add_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN);assert(codex_success_read_interval(true)==3000);
    lv_obj_remove_flag(codex_reply_overlay,LV_OBJ_FLAG_HIDDEN);
    workspace_client=0;assert(codex_success_read_interval(true)==3000);workspace_client=1;
    snprintf(codex_reply_selected,sizeof(codex_reply_selected),"older-turn");codex_reply_follow=false;
    assert(codex_success_read_interval(true)==15000);
    codex_reply_selected[0]=0;codex_reply_follow=true;
    preview_result_code=-1;codex_reply_refresh(NULL);assert(codex_reading);
    preview_read_view=cJSON_Parse("{\"taskId\":\"11111111-1111-4111-8111-111111111111\",\"receipt\":\"已完成\",\"text\":\"最终公开回复\",\"replyView\":{\"taskId\":\"11111111-1111-4111-8111-111111111111\",\"turnId\":\"running-turn\",\"state\":\"completed\",\"page\":0,\"pageCount\":1,\"history\":false,\"hasText\":true}}");
    preview_result_code=200;codex_read_finish(2000);
    assert(codex_read_next==5000&&!strcmp(lv_label_get_text(codex_reply_text),"最终公开回复"));
    capture("claude-fast-completed-reply");
    preview_result_code=-1;codex_reply_refresh(NULL);assert(codex_reading);
    preview_result_code=429;codex_read_finish(3000);assert(codex_read_next==3200);
    puts("CLAUDE_REPLY_RATE_OK running=750 completed=3000 history=15000 hidden=3000 codex=3000 retry429=200");
}
