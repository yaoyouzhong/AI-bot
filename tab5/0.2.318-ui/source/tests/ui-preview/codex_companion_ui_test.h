// Exercise the actual production LVGL objects, including the optional saver.
static void check_codex_companion_ui(void) {
    assert(codex_companion_asset_available());
    const char *fixture="{\"codex\":{\"state\":\"working\"},\"codexTasks\":{\"overviewTask\":{\"id\":\"11111111-1111-1111-1111-111111111111\",\"title\":\"Codex 萌宠状态与屏保动作设计\",\"folder\":\"AI-bot\",\"state\":\"working\",\"companionState\":\"working\",\"turnId\":\"turn-one\"},\"tasks\":[]}}";
    cJSON_Delete(shown);shown=cJSON_Parse(fixture);assert(shown);
    cJSON *task=item(item(shown,"codexTasks"),"overviewTask");
    cJSON_AddItemToArray(item(item(shown,"codexTasks"),"tasks"),cJSON_Duplicate(task,true));
    last_link=LINK_USB;last_received=preview_now_us/1000;offline_since=-1;
    page(0);render_home_task();companion_tick(preview_now_us/1000,true,false);
    lv_obj_update_layout(lv_screen_active());
    assert(!lv_obj_has_flag(home_companion,LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_get_parent(home_companion)==lv_obj_get_parent(home_task_action));
    lv_area_t pet,button,title;lv_obj_get_coords(home_companion,&pet);lv_obj_get_coords(home_task_action,&button);lv_obj_get_coords(home_task_title,&title);
    assert(title.x2<pet.x1);
    lv_area_t project,status;lv_obj_get_coords(home_task_note,&project);lv_obj_get_coords(model_state[1],&status);
    assert(project.x1==title.x1&&project.x2<pet.x1&&project.y2<title.y1);
    const int scale=lv_image_get_scale_x(home_companion);
    int pet_width=(CODEX_COMPANION_WIDTH*scale+128)/256,pet_height=(CODEX_COMPANION_HEIGHT*scale+128)/256;
    assert(status.x1==pet.x1&&lv_obj_get_width(model_state[1])==pet_width&&lv_obj_has_flag(model_dot[1],LV_OBJ_FLAG_HIDDEN));
    assert(abs(2*pet.x1+pet_width-1316)<=1&&abs(2*pet.y1+pet_height-467)<=1);
    assert(abs((status.x1+status.x2+1)-(2*pet.x1+pet_width))<=1);
    assert(pet.y1+pet_height<button.y1);
    assert(lv_image_get_src(home_companion)==lv_image_get_src(companion_saver_image));
    uint8_t *allocation=companion_pixels;
    const char *names[]={"idle","working","confirm","answer","waiting","done","blocked","stopped","unknown"};
    for(unsigned i=0;i<9;i++) {
        codex_companion_value value={.state=(codex_companion_state)i};strcpy(value.task,"11111111-1111-1111-1111-111111111111");strcpy(value.turn,"turn-one");
        companion_set_value(value,1000);companion_tick(1250,true,false);
        ui_text(model_state[1],codex_companion_label(value.state));tint(model_state[1],companion_color(value.state));
        char name[80];snprintf(name,sizeof(name),"companion-overview-%s",names[i]);capture(name);
        int64_t start=companion_animation.started_at;
        saver_style=UI_SAVER_CODEX;saver_enter(false);companion_tick(1250,false,true);
        struct tm local={.tm_hour=10,.tm_min=15};companion_saver_time(&local);
        assert(!lv_obj_has_flag(companion_saver_layer,LV_OBJ_FLAG_HIDDEN));
        assert(companion_animation.value.state==value.state);
        assert(!lv_obj_has_flag(companion_saver_image,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(saver_claude.root,LV_OBJ_FLAG_HIDDEN));
        assert(lv_obj_has_flag(companion_saver_fallback,LV_OBJ_FLAG_HIDDEN));
        assert(lv_obj_get_x(companion_saver_clock)+lv_obj_get_width(companion_saver_clock)/2==640);
        assert(companion_animation.started_at==start&&companion_pixels==allocation);
        snprintf(name,sizeof(name),"companion-saver-%s",names[i]);capture(name);saver_leave();
    }
    // The existing completion action must retain the exact unread turn.
    if(!codex_notices)codex_notices=calloc(80,sizeof(*codex_notices));assert(codex_notices);
    strcpy(codex_notices[0].task,text(task,"id"));strcpy(codex_notices[0].turn,"unread-turn");codex_notices[0].unread=true;codex_notices[0].arrived=1;
    cJSON_ReplaceItemInObject(task,"state",cJSON_CreateString("waiting"));
    cJSON_ReplaceItemInObject(task,"companionState",cJSON_CreateString("waiting"));cJSON_AddStringToObject(task,"waitingKind","answer");
    render_home_task();assert(!home_task_unread&&companion_animation.value.state==CODEX_COMPANION_QUESTION);
    cJSON_ReplaceItemInObject(task,"state",cJSON_CreateString("idle"));cJSON_ReplaceItemInObject(task,"companionState",cJSON_CreateString("idle"));
    cJSON_ReplaceItemInArray(item(item(shown,"codexTasks"),"tasks"),0,cJSON_Duplicate(task,true));
    render_home_task();assert(home_task_unread&&!strcmp(home_task_turn,"unread-turn")&&companion_animation.value.state==CODEX_COMPANION_DONE);
    // Loss of telemetry keeps task/title/turn identity but changes the pose.
    offline_since=preview_now_us/1000;last_link=LINK_NONE;render_home_task();
    assert(companion_animation.value.state==CODEX_COMPANION_UNKNOWN&&!strcmp(home_task_id,text(task,"id")));
    assert(!strcmp(lv_label_get_text(home_task_title),text(task,"title")));
    // A new live task must take over even while the overview is hidden by saver.
    cJSON_ReplaceItemInObject(task,"state",cJSON_CreateString("waiting"));
    cJSON_ReplaceItemInObject(task,"companionState",cJSON_CreateString("waiting"));
    cJSON_ReplaceItemInObject(task,"turnId",cJSON_CreateString("turn-two"));
    cJSON_ReplaceItemInArray(item(item(shown,"codexTasks"),"tasks"),0,cJSON_Duplicate(task,true));
    codex_notices[0].unread=false;prefs.alerts=false;prefs.cycle=false;
    link_state_init(&device_links);preview_now_us+=250000;
    link_state_receive(&device_links,LINK_USB,"companion",1,preview_now_us/1000);
    cJSON_Delete(device_snapshots[LINK_USB]);device_snapshots[LINK_USB]=cJSON_Duplicate(shown,true);
    saver_style=UI_SAVER_CODEX;saver_enter(false);tick(NULL);
    assert(companion_animation.value.state==CODEX_COMPANION_QUESTION&&!strcmp(companion_animation.value.turn,"turn-two"));
    int64_t began=companion_animation.started_at;
    preview_now_us+=1000000;link_state_receive(&device_links,LINK_USB,"companion",2,preview_now_us/1000);tick(NULL);
    assert(companion_animation.started_at==began);
    preview_now_us+=9000000;tick(NULL);
    assert(companion_animation.value.state==CODEX_COMPANION_UNKNOWN);
    assert(companion_animation.value.state==CODEX_COMPANION_UNKNOWN);saver_leave();
    page(9);settings_select(1);
    assert(lv_dropdown_get_option_count(saver_selector)==UI_SAVER_OPTIONS);
    assert(strstr(lv_dropdown_get_options(saver_selector),"\nCodex萌宠"));
    saver_style=0;lv_dropdown_set_selected(saver_selector,ui_saver_option_from_style(UI_SAVER_CODEX));
    lv_obj_send_event(saver_selector,LV_EVENT_VALUE_CHANGED,NULL);
    assert(saver_style==UI_SAVER_CODEX&&preview_saved_saver==UI_SAVER_CODEX);
    lv_dropdown_open(saver_selector);capture("companion-saver-picker");lv_dropdown_close(saver_selector);
    // No mascot is attached to the Codex task page.
    page(8);assert(lv_obj_get_parent(home_companion)!=lv_obj_get_parent(codex_input));
    puts("CODEX_COMPANION_UI_OK nine_overview_and_saver_renders, approved_placement, shared_buffer_and_timeline, waiting_over_unread, exact_turn_retained, stale_keeps_content, live_saver_heartbeat_expiry, saved_picker");
}
