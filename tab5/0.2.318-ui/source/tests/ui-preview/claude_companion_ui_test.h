static unsigned automation_clicks;
static void automation_test_click(lv_event_t *e){(void)e;automation_clicks++;}
static cJSON *automation_test_frame(unsigned request,const char *op){
    cJSON *frame=cJSON_CreateObject(),*health=cJSON_AddObjectToObject(frame,"connectionHealth"),*c=cJSON_AddObjectToObject(health,"uiAutomationCommand");
    char id[33];snprintf(id,sizeof(id),"%032x",request);
    cJSON_AddNumberToObject(frame,"epochMilliseconds",100000);cJSON_AddStringToObject(cJSON_AddObjectToObject(frame,"pcInput"),"session","11111111111111111111111111111111");
    cJSON_AddNumberToObject(c,"version",1);cJSON_AddStringToObject(c,"requestId",id);cJSON_AddStringToObject(c,"transport","USB");cJSON_AddStringToObject(c,"op",op);cJSON_AddNumberToObject(c,"expiresAt",105000);
    cJSON_AddNumberToObject(c,"width",lv_display_get_horizontal_resolution(NULL));cJSON_AddNumberToObject(c,"height",lv_display_get_vertical_resolution(NULL));cJSON_AddNumberToObject(c,"rotation",lv_display_get_rotation(NULL));
    cJSON_AddNumberToObject(c,"x",150);cJSON_AddNumberToObject(c,"y",150);cJSON_AddNumberToObject(c,"toX",150);cJSON_AddNumberToObject(c,"toY",150);cJSON_AddNumberToObject(c,"durationMs",80);return frame;
}
static void automation_test_real_pointer(void){
    lv_display_t *display=lv_display_get_default();lv_display_rotation_t original=lv_display_get_rotation(display);
    lv_obj_t *target=lv_button_create(lv_layer_top());lv_obj_set_pos(target,100,100);lv_obj_set_size(target,200,100);lv_obj_add_event_cb(target,automation_test_click,LV_EVENT_CLICKED,NULL);
    for(int rotation=0;rotation<4;rotation++){
        lv_display_set_rotation(display,(lv_display_rotation_t)rotation);lv_obj_update_layout(lv_layer_top());
        cJSON *f=automation_test_frame(100+rotation,"tap");ui_automation_tick(f,LINK_USB,100,100);assert(automation_pressed);
        lv_indev_read(automation_input);lv_tick_inc(96);lv_indev_read(automation_input);assert(automation_clicks==(unsigned)rotation+1);
        ui_automation_tick(f,LINK_USB,200,100);assert(!automation_pressed);cJSON_Delete(f);
    }
    lv_display_set_rotation(display,original);lv_obj_update_layout(lv_layer_top());
    cJSON *f=automation_test_frame(110,"tap"),*c=item(item(f,"connectionHealth"),"uiAutomationCommand");
    cJSON_SetNumberValue(item(c,"x"),1280);ui_automation_tick(f,LINK_USB,100,100);assert(!automation_pressed);assert(strstr(automation_report,",3,"));cJSON_Delete(f);
    f=automation_test_frame(111,"tap");ui_automation_tick(f,LINK_WIFI,100,100);assert(!automation_pressed);
    ui_automation_tick(f,LINK_USB,6000,100);assert(!automation_pressed);cJSON_Delete(f);
    f=automation_test_frame(112,"tap");ui_automation_tick(f,LINK_USB,100,100);lv_indev_read(automation_input);
    preview_computer_host_epoch++;lv_tick_inc(96);lv_indev_read(automation_input);assert(!automation_pressed&&automation_clicks==4);preview_computer_host_epoch--;cJSON_Delete(f);
    lv_obj_delete(target);
    lv_obj_t *scroll=lv_obj_create(lv_layer_top());lv_obj_set_pos(scroll,50,70);lv_obj_set_size(scroll,500,450);lv_obj_set_scroll_dir(scroll,LV_DIR_VER);
    lv_obj_t *content=lv_obj_create(scroll);lv_obj_set_size(content,420,1400);lv_obj_update_layout(lv_layer_top());
    f=automation_test_frame(113,"swipe");c=item(item(f,"connectionHealth"),"uiAutomationCommand");
    cJSON_SetNumberValue(item(c,"y"),380);cJSON_SetNumberValue(item(c,"toY"),150);cJSON_SetNumberValue(item(c,"durationMs"),320);
    ui_automation_tick(f,LINK_USB,100,100);lv_indev_read(automation_input);
    for(int i=0;i<24;i++){lv_tick_inc(16);lv_indev_read(automation_input);}
    assert(!automation_pressed&&lv_obj_get_scroll_y(scroll)>80);cJSON_Delete(f);lv_obj_delete(scroll);
    puts("TAB5_UI_AUTOMATION_REAL_POINTER_OK four rotations, real scrolling, bounds, replay, stale/link rejection and cancelled release");
}
// Use the actual widgets. Only input telemetry and time are fixtures.
static void check_claude_companion_ui(void) {
    assert(CLAUDE_PET_AVAILABLE);
    cJSON_Delete(shown);shown=cJSON_Parse("{\"epochUtc\":1793758000,\"codex\":{\"state\":\"idle\",\"ageSeconds\":30},\"claude\":{\"state\":\"working\",\"ageSeconds\":0},\"codexTasks\":{\"overviewTask\":{\"id\":\"11111111-1111-1111-1111-111111111111\",\"title\":\"Codex session must not appear under Claude\",\"folder\":\"Codex project\",\"state\":\"idle\"},\"tasks\":[]}}");assert(shown);
    cJSON *code=item(shown,"codex"),*claude=item(shown,"claude");
    home_companion_provider=1;
    assert(companion_provider_resolve(shown,(1u<<0)|(1u<<1)|(1u<<5)|(1u<<6))==0);
    cJSON_ReplaceItemInObject(code,"state",cJSON_CreateString("working"));
    cJSON_SetNumberValue(item(code,"ageSeconds"),0);cJSON_SetNumberValue(item(claude,"ageSeconds"),10);
    assert(companion_provider_resolve(shown,1u)==0);
    cJSON_ReplaceItemInObject(code,"state",cJSON_CreateString("idle"));
    cJSON_ReplaceItemInObject(claude,"state",cJSON_CreateString("offline"));
    cJSON_AddItemToObject(shown,"domesticActivity",cJSON_Parse("{\"activeProvider\":\"deepseek\",\"state\":\"working\",\"providers\":{\"deepseek\":{\"lastActivity\":1793758000,\"model\":\"deepseek-chat\"}}}"));
    assert(companion_provider_resolve(shown,(1u<<0)|(1u<<1)|(1u<<5)|(1u<<6))==0);
    assert(claude_client_mood(shown,true)==CODEX_COMPANION_WORKING);
    cJSON_ReplaceItemInObject(item(shown,"domesticActivity"),"activeProvider",cJSON_CreateString("zhipu"));
    assert(companion_provider_resolve(shown,1u<<1)==1);
    cJSON_DeleteItemFromObject(shown,"domesticActivity");
    // Both selected: compare the same sessions that will be shown on screen.
    cJSON_ReplaceItemInObject(code,"state",cJSON_CreateString("idle"));
    cJSON_ReplaceItemInObject(claude,"state",cJSON_CreateString("idle"));
    cJSON *codex_overview=item(item(shown,"codexTasks"),"overviewTask");
    cJSON_AddNumberToObject(codex_overview,"updatedAt",1793757880);
    cJSON_AddItemToObject(shown,"claudeSession",cJSON_Parse("{\"title\":\"Claude 会话\",\"lastActivity\":1793757940}"));
    assert(companion_provider_resolve(shown,3u)==0);
    cJSON_SetNumberValue(item(codex_overview,"updatedAt"),1793757990);
    assert(companion_provider_resolve(shown,3u)==1);
    cJSON_DeleteItemFromObject(shown,"claudeSession");
    home_models=(1u<<0)|(1u<<5)|(1u<<6);last_link=LINK_USB;offline_since=-1;
    const char *states[]={"working","idle","idle","idle","offline","unknown"};
    const char *names[]={"working","idle","waiting","done","stopped","unknown"};
    const codex_companion_state expected[]={CODEX_COMPANION_WORKING,CODEX_COMPANION_IDLE,CODEX_COMPANION_WAITING,CODEX_COMPANION_DONE,CODEX_COMPANION_STOPPED,CODEX_COMPANION_UNKNOWN};
    for(unsigned i=0;i<6;i++) {
        preview_now_us+=1000000;last_received=preview_now_us/1000;
        cJSON_ReplaceItemInObject(claude,"state",cJSON_CreateString(states[i]));
        cJSON_DeleteItemFromObject(claude,"needsInput");cJSON_AddBoolToObject(claude,"needsInput",i==2);
        cJSON_DeleteItemFromObject(claude,"completionActive");cJSON_AddBoolToObject(claude,"completionActive",i==3);
        page(0);render_home_task();
        preview_now_us+=3400000;last_received=preview_now_us/1000;
        claude_pet_tick(&home_claude,claude_mood,preview_now_us/1000,true);
        lv_obj_update_layout(lv_screen_active());
        assert(home_companion_provider==0&&claude_mood==expected[i]);
        assert(lv_obj_has_flag(home_companion,LV_OBJ_FLAG_HIDDEN)&&!lv_obj_has_flag(home_claude.root,LV_OBJ_FLAG_HIDDEN));
        assert(!strcmp(lv_label_get_text(home_task_title),"Claude Code")&&!strcmp(lv_label_get_text(home_task_note),"当前客户端"));
        assert(lv_obj_get_x(model_state[1])+lv_obj_get_width(model_state[1])/2==home_claude.x+152);
        lv_area_t box;lv_obj_get_coords(home_claude.root,&box);assert(box.x1==506&&box.y2<334);
        claude_companion_frame_info frame_info=claude_companion_frame(claude_mood,preview_now_us/1000-claude_mood_started,false);
        // Transparent atlas margins are not visible artwork or clipping.
        unsigned visible_right=0,visible_bottom=0,visible_left=frame_info.width,visible_top=frame_info.height;
        for(unsigned y=0;y<frame_info.height;y++)for(unsigned x=0;x<frame_info.width;x++)
            if(claude_pixels[frame_info.width*frame_info.height*2+y*frame_info.width+x]) {
                if(x+1>visible_right)visible_right=x+1;if(y+1>visible_bottom)visible_bottom=y+1;
                if(x<visible_left)visible_left=x;if(y<visible_top)visible_top=y;
            }
        assert(lv_obj_get_x(home_claude.image)+(int)(visible_left*frame_info.scale_q8/256)>=0);
        assert(lv_obj_get_y(home_claude.image)+(int)(visible_top*frame_info.scale_q8/256)>=0);
        assert(lv_obj_get_x(home_claude.image)+(int)((visible_right*frame_info.scale_q8+255)/256)<=home_claude.width);
        assert(lv_obj_get_y(home_claude.image)+(int)((visible_bottom*frame_info.scale_q8+255)/256)<=home_claude.height);
        if(i==1) {
            int64_t idle_at=claude_mood_started;
            claude_pet_tick(&home_claude,claude_mood,idle_at,true);int low=home_claude.last_scale;capture("claude-idle-breath-out");
            claude_pet_tick(&home_claude,claude_mood,idle_at+1800,true);assert(home_claude.last_scale>low);capture("claude-idle-breath-in");
            claude_pet_tick(&home_claude,claude_mood,idle_at+3600,true);assert(home_claude.last_scale==low);
        }
        char name[80];snprintf(name,sizeof(name),"claude-overview-%s",names[i]);capture(name);
        int64_t started=claude_mood_started;
        saver_style=UI_SAVER_CLAUDE;saver_enter(false);
        struct tm local={.tm_hour=10,.tm_min=15};companion_saver_time(&local);
        claude_pet_tick(&saver_claude,claude_mood,preview_now_us/1000,true);
        assert(!lv_obj_has_flag(companion_saver_layer,LV_OBJ_FLAG_HIDDEN));
        assert(lv_obj_has_flag(companion_saver_image,LV_OBJ_FLAG_HIDDEN)&&!lv_obj_has_flag(saver_claude.root,LV_OBJ_FLAG_HIDDEN));
        assert(lv_obj_has_flag(companion_saver_fallback,LV_OBJ_FLAG_HIDDEN)&&claude_mood_started==started);
        if(i==3) {
            preview_now_us+=7000000;last_received=preview_now_us/1000;
            claude_pet_tick(&saver_claude,claude_mood,preview_now_us/1000,true);
            unsigned first_frame=claude_rendered_frame;
            preview_now_us+=500000;last_received=preview_now_us/1000;
            claude_pet_tick(&saver_claude,claude_mood,preview_now_us/1000,true);
            assert(claude_rendered_frame!=first_frame&&claude_mood==CODEX_COMPANION_DONE&&claude_mood_started==started);
        }
        snprintf(name,sizeof(name),"claude-saver-%s",names[i]);capture(name);saver_leave();
        claude_pet_tick(&home_claude,claude_mood,preview_now_us/1000,true);
        if(i==3)assert(claude_rendered_frame==claude_companion_frame(CODEX_COMPANION_DONE,70000,false).frame);
    }
    // A single chosen client remains authoritative even while the other works.
    cJSON_ReplaceItemInObject(code,"state",cJSON_CreateString("working"));
    cJSON_AddItemToObject(shown,"claudeSession",cJSON_Parse("{\"title\":\"整理项目文档\",\"lastActivity\":1793757990}"));
    home_models=1u;page(0);render_home_task();lv_obj_update_layout(lv_screen_active());
    assert(home_companion_provider==0&&!strcmp(lv_label_get_text(home_task_title),"整理项目文档"));
    assert(!strcmp(lv_label_get_text(home_task_note),"最近会话"));capture("claude-overview-session-name");
    // The bridge now binds name and lifecycle to the same Claude prompt.
    // Exercise the production widgets with exactly that wire representation.
    const char *bound_states[]={"working","error","idle","idle"};
    const char *bound_names[]={"active","failed","interrupted","completed"};
    const codex_companion_state bound_moods[]={CODEX_COMPANION_WORKING,CODEX_COMPANION_BLOCKED,CODEX_COMPANION_IDLE,CODEX_COMPANION_DONE};
    for(unsigned i=0;i<4;i++) {
        cJSON_ReplaceItemInObject(claude,"state",cJSON_CreateString(bound_states[i]));
        cJSON_ReplaceItemInObject(claude,"completionActive",cJSON_CreateBool(i==3));
        cJSON_ReplaceItemInObject(claude,"needsInput",cJSON_CreateBool(false));
        render_home_task();companion_tick(preview_now_us/1000,true,false);lv_obj_update_layout(lv_screen_active());
        assert(home_companion_provider==0&&claude_mood==bound_moods[i]);
        assert(!strcmp(lv_label_get_text(home_task_title),"整理项目文档"));
        char shot[80];snprintf(shot,sizeof(shot),"claude-bound-%s",bound_names[i]);capture(shot);
    }
    cJSON_ReplaceItemInObject(item(shown,"claudeSession"),"title",cJSON_CreateString("这是一个很长的会话名称用于检查中文标题显示和末尾省略是否正确不会遮挡萌宠或按钮"));
    render_home_task();lv_obj_update_layout(lv_screen_active());
    assert(lv_obj_get_width(home_task_title)==480&&lv_obj_get_height(home_task_title)==82);
    capture("claude-overview-session-long");
    display_orientation_apply(2);render_home_task();lv_obj_update_layout(lv_screen_active());
    assert(home_companion_provider==0&&lv_obj_get_width(home_task_title)==480&&lv_obj_get_height(home_task_title)==82);
    capture("claude-overview-session-flipped");display_orientation_apply(0);
    cJSON_DeleteItemFromObject(shown,"claudeSession");render_home_task();
    assert(!strcmp(lv_label_get_text(home_task_title),"Claude Code"));
    assert(!strcmp(lv_label_get_text(home_task_note),"当前客户端"));
    home_models=2u;render_home_task();
    assert(home_companion_provider==1&&!strcmp(lv_label_get_text(home_task_title),"Codex session must not appear under Claude"));
    companion_tick(preview_now_us/1000,true,false);lv_obj_update_layout(lv_screen_active());
    capture("codex-overview-size-reference");
    cJSON_ReplaceItemInObject(code,"state",cJSON_CreateString("idle"));
    assert(claude_client_mood(shown,false)==CODEX_COMPANION_UNKNOWN);
    cJSON_ReplaceItemInObject(code,"state",cJSON_CreateString("working"));
    home_models=3u;render_home_task();companion_tick(preview_now_us/1000,true,false);
    assert(home_companion_provider==1&&!lv_obj_has_flag(home_companion,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(home_claude.root,LV_OBJ_FLAG_HIDDEN));
    page(9);settings_select(1);saver_style=0;lv_dropdown_set_selected(saver_selector,ui_saver_option_from_style(UI_SAVER_CLAUDE));
    lv_obj_send_event(saver_selector,LV_EVENT_VALUE_CHANGED,NULL);
    assert(saver_style==UI_SAVER_CLAUDE&&preview_saved_saver==UI_SAVER_CLAUDE);
    char selected[48];lv_dropdown_get_selected_str(saver_selector,selected,sizeof(selected));assert(!strcmp(selected,"Claude萌宠"));
    lv_dropdown_open(saver_selector);capture("claude-saver-picker");lv_dropdown_close(saver_selector);
    lv_dropdown_close(saver_selector);home_models=1u;page(0);
    cJSON_DeleteItemFromObject(shown,"claudeSession");
    cJSON_AddItemToObject(shown,"claudeSession",cJSON_Parse("{\"title\":\"整理项目文档\",\"readBinding\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"repliesEnabled\":true}"));
    cJSON_AddItemToObject(shown,"pcInput",cJSON_Parse("{\"session\":\"fixture-bridge-session\"}"));
    render_home_task();assert(!strcmp(lv_label_get_text(lv_obj_get_child(home_task_action,0)),"查看对话"));
    lv_obj_send_event(home_task_action,LV_EVENT_CLICKED,NULL);assert(claude_reader_opened&&current_page==0&&preview_claude_page==-1);
    claude_reader_tick();capture("claude-reader-loading");
    preview_claude_read.busy=false;preview_claude_read.status=200;preview_claude_read.page=0;preview_claude_read.total=3;
    strcpy(preview_claude_read.revision,"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
    strcpy(preview_claude_read.title,"整理项目文档");strcpy(preview_claude_read.text,"# Claude 公开回复\n\n");
    for(int i=0;i<16;i++)strcat(preview_claude_read.text,"这一段公开回复来自当前会话，支持中文、English 和 emoji 🙂。\n");
    preview_claude_read.generation++;claude_reader_tick();lv_obj_update_layout(lv_screen_active());
    assert(claude_reader_page==0&&claude_reader_total==3&&lv_obj_has_state(claude_reader_previous,LV_STATE_DISABLED));
    assert(!strcmp(lv_label_get_text(claude_reader_title),"Claude · 整理项目文档"));capture("claude-reader-public-reply");
    assert(lv_obj_get_scroll_bottom(claude_reader_scroll)>0);lv_obj_scroll_to_y(claude_reader_scroll,9999,LV_ANIM_OFF);capture("claude-reader-scrolled");
    lv_obj_send_event(claude_reader_next,LV_EVENT_CLICKED,NULL);assert(preview_claude_page==1);
    preview_claude_read.busy=false;preview_claude_read.page=1;strcpy(preview_claude_read.text,"第二页公开回复，不显示 Codex 会话内容。");preview_claude_read.generation++;claude_reader_tick();
    assert(claude_reader_page==1&&!lv_obj_has_state(claude_reader_previous,LV_STATE_DISABLED));capture("claude-reader-next-page");
    display_orientation_apply(2);capture("claude-reader-flipped");display_orientation_apply(0);
    strcpy(preview_claude_read.binding,"cccccccccccccccccccccccccccccccc");strcpy(preview_claude_read.title,"另一个会话禁止替换");preview_claude_read.generation++;claude_reader_tick();
    assert(!strcmp(lv_label_get_text(claude_reader_title),"Claude · 整理项目文档"));
    strcpy(preview_claude_read.binding,claude_reader_binding);preview_claude_read.status=403;preview_claude_read.generation++;claude_reader_tick();
    assert(lv_obj_get_child_count(claude_reader_body)==0);
    assert(lv_obj_has_state(claude_reader_previous,LV_STATE_DISABLED)&&lv_obj_has_state(claude_reader_next,LV_STATE_DISABLED)&&lv_obj_has_state(claude_reader_latest,LV_STATE_DISABLED));
    capture("claude-reader-source-stopped");
    claude_reader_close(NULL);assert(!claude_reader_opened&&lv_obj_has_flag(claude_reader_layer,LV_OBJ_FLAG_HIDDEN));
    automation_test_real_pointer();
    puts("CLAUDE_COMPANION_UI_OK supported_client_routing, active_over_quota_preferences, custom_domestic_models_keep_claude, six_states, exclusive_overview, no_codex_content_leak, exact_center, shared_saver_state, stale_unknown, existing_model_picker, saved_id10, readonly_claude_reader, paging, pinned_session, source_stop");
}
