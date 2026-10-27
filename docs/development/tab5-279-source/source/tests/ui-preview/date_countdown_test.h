static void check_date_countdown_editor(void) {
    ui_date_countdown saved=date_countdown,persisted=preview_date_countdown;
    date_editor_show(NULL);lv_textarea_set_text(date_title_input,"");
    lv_textarea_add_text(date_title_input,"一二三四五六七八九十");assert(lv_text_get_encoded_length(lv_textarea_get_text(date_title_input))==10);
    lv_textarea_add_text(date_title_input,"多");assert(lv_text_get_encoded_length(lv_textarea_get_text(date_title_input))==10);
    lv_textarea_set_text(date_title_input,"高考");lv_textarea_add_text(date_title_input,"一二三四五六七八九十");assert(!strcmp(lv_textarea_get_text(date_title_input),"高考"));
    lv_textarea_set_cursor_pos(date_title_input,LV_TEXTAREA_CURSOR_LAST);lv_textarea_delete_char(date_title_input);assert(!strcmp(lv_textarea_get_text(date_title_input),"高"));
    lv_textarea_set_text(date_title_input,"一二三四五六七八九十甲");date_editor_save(NULL);assert(date_editor_open()&&strstr(lv_label_get_text(date_feedback),"10"));
    lv_textarea_set_text(date_title_input,"高考");date_edit.year=2027;date_edit.month=6;date_edit.day=7;
    date_picker_show(NULL);assert(date_picker_draft.day==7);capture("date-picker-june");
    lv_dropdown_set_selected(date_picker_year,28);lv_dropdown_set_selected(date_picker_month,1);date_picker_change(NULL);
    date_picker_draft.day=29;date_picker_refresh();capture("date-picker-leap");
    lv_dropdown_set_selected(date_picker_year,27);date_picker_change(NULL);assert(date_picker_draft.day==28);
    date_header_clicked(NULL);assert(date_edit.year==2027&&date_edit.month==6&&date_edit.day==7&&date_editor_open());
    date_picker_show(NULL);date_picker_draft=(lv_calendar_date_t){2028,2,29};date_picker_refresh();date_picker_confirm(NULL);
    assert(date_edit.year==2028&&date_edit.day==29);date_keyboard_show(NULL);date_header_clicked(NULL);assert(date_editor_open()&&lv_obj_has_flag(date_keyboard,LV_OBJ_FLAG_HIDDEN));
    // Match the agenda service contract: owner, generation, switching, review and discard.
    if(!shown)shown=cJSON_CreateObject();cJSON *pc=cJSON_GetObjectItemCaseSensitive(shown,"pcInput");
    if(!pc){pc=cJSON_CreateObject();cJSON_AddItemToObject(shown,"pcInput",pc);}
    cJSON *old_session=cJSON_DetachItemFromObjectCaseSensitive(pc,"session");cJSON_AddStringToObject(pc,"session","date-voice-session");
    int64_t old_received=last_received;last_received=esp_timer_get_time()/1000;preview_voice_start_enabled=true;int submitted=preview_submit_count;
    lv_textarea_set_text(date_title_input,"高考");lv_textarea_set_cursor_pos(date_title_input,0);date_keyboard_show(NULL);
    lv_obj_send_event(date_editor_voice_button,LV_EVENT_CLICKED,NULL);assert(date_voice_waiting&&lv_obj_has_flag(date_keyboard,LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_has_state(date_editor_save_button,LV_STATE_DISABLED));
    strcpy(preview_voice.source,"dji");strcpy(preview_voice.phase,"recording");preview_voice.ready=true;preview_voice.level=65;date_editor_voice_tick();
    assert(!lv_obj_has_flag(date_editor_voice_switch,LV_OBJ_FLAG_HIDDEN));capture("date-editor-voice-recording");
    lv_obj_send_event(date_editor_voice_switch,LV_EVENT_CLICKED,NULL);assert(preview_voice_command==3);
    strcpy(preview_voice.source,"tab5");date_editor_voice_tick();assert(lv_obj_has_flag(date_editor_voice_switch,LV_OBJ_FLAG_HIDDEN));
    lv_obj_send_event(date_editor_voice_button,LV_EVENT_CLICKED,NULL);assert(preview_voice_command==1);
    strcpy(preview_voice.phase,"recognizing");preview_voice.ready=false;date_editor_voice_tick();capture("date-editor-voice-recognizing");
    assert(lv_obj_has_state(date_editor_voice_button,LV_STATE_DISABLED));
    preview_voice.busy=false;preview_voice.can_insert=true;strcpy(preview_voice.phase,"review");strcpy(preview_voice.text,"明年");date_editor_voice_tick();date_editor_voice_tick();
    assert(!strcmp(lv_textarea_get_text(date_title_input),"明年高考")&&!date_voice_waiting&&date_editor_open());
    lv_obj_send_event(date_editor_voice_button,LV_EVENT_CLICKED,NULL);assert(date_voice_waiting);
    preview_voice.busy=false;preview_voice.can_insert=true;strcpy(preview_voice.phase,"review");strcpy(preview_voice.text,"一二三四五六七八九十");date_editor_voice_tick();
    assert(!strcmp(lv_textarea_get_text(date_title_input),"明年一二三四五六七八九十高考")&&strstr(lv_label_get_text(date_feedback),"删减 4"));
    assert(lv_obj_has_state(date_editor_save_button,LV_STATE_DISABLED));capture("date-editor-voice-overlimit");
    date_editor_save(NULL);assert(!memcmp(&preview_date_countdown,&persisted,sizeof(persisted)));
    for(int n=0;n<4;n++)lv_textarea_delete_char(date_title_input);
    assert(lv_text_get_encoded_length(lv_textarea_get_text(date_title_input))==10&&!lv_obj_has_state(date_editor_save_button,LV_STATE_DISABLED));
    lv_textarea_set_text(date_title_input,"");lv_obj_send_event(date_editor_voice_button,LV_EVENT_CLICKED,NULL);
    preview_voice.busy=false;preview_voice.can_insert=true;strcpy(preview_voice.phase,"review");
    memset(preview_voice.text,'a',sizeof(preview_voice.text)-1);preview_voice.text[sizeof(preview_voice.text)-1]=0;date_editor_voice_tick();
    assert(strlen(lv_textarea_get_text(date_title_input))==2000&&lv_obj_has_state(date_editor_save_button,LV_STATE_DISABLED));
    lv_textarea_set_text(date_title_input,"明年高考");
    lv_obj_send_event(date_editor_voice_button,LV_EVENT_CLICKED,NULL);date_header_clicked(NULL);assert(date_voice_cancelled&&preview_voice_command==2&&date_editor_open());
    preview_voice.busy=false;preview_voice.can_insert=true;strcpy(preview_voice.phase,"review");strcpy(preview_voice.text,"不应插入");date_editor_voice_tick();assert(!strcmp(lv_textarea_get_text(date_title_input),"明年高考"));
    lv_obj_send_event(date_editor_voice_button,LV_EVENT_CLICKED,NULL);preview_voice.generation++;preview_voice.busy=false;strcpy(preview_voice.text,"别的会话");date_editor_voice_tick();assert(!strcmp(lv_textarea_get_text(date_title_input),"明年高考"));
    lv_obj_send_event(date_editor_voice_button,LV_EVENT_CLICKED,NULL);assert(date_voice_waiting);
    preview_voice.generation++;strcpy(preview_voice.task_id,"a1b07a65-0000-4000-8000-00000000a5ed");int command=preview_voice_command;
    lv_obj_send_event(date_editor_voice_cancel,LV_EVENT_CLICKED,NULL);assert(preview_voice_command==command&&!date_voice_waiting);preview_voice.busy=false;
    last_received-=10000;lv_obj_send_event(date_editor_voice_button,LV_EVENT_CLICKED,NULL);assert(!date_voice_waiting&&strstr(lv_label_get_text(date_feedback),"连接电脑"));
    assert(submitted==preview_submit_count);preview_voice_start_enabled=false;memset(&preview_voice,0,sizeof(preview_voice));last_received=old_received;
    cJSON_DeleteItemFromObjectCaseSensitive(pc,"session");if(old_session)cJSON_AddItemToObject(pc,"session",old_session);
    date_edit.year=2027;date_edit.month=6;date_edit.day=7;ui_text_fmt(date_selected_label,"2027 年 6 月 7 日");ui_text(date_feedback,"");capture("date-editor-final");date_header_clicked(NULL);assert(!date_editor_open());
    int old_saver=saver_style;saver_style=UI_SAVER_COUNTDOWN;saver_enter(false);
    date_countdown=(ui_date_countdown){.version=1,.year=2027,.month=6,.day=7};strcpy(date_countdown.title,"高考");
    struct tm local={.tm_year=126,.tm_mon=9,.tm_mday=16,.tm_hour=10,.tm_min=1};date_saver_update(&local);assert(!strcmp(lv_label_get_text(date_saver_days),"234"));capture("date-saver-day-short");
    local.tm_hour=21;date_saver_update(&local);capture("date-saver-night-short");
    const char *names[]={"项目上线","一二三四五六七八九十"};
    for(int i=0;i<2;i++){strcpy(date_countdown.title,names[i]);local.tm_hour=10;date_saver_update(&local);capture(i?"date-saver-day-ten":"date-saver-day-four");}
    strcpy(date_countdown.title,"全国硕士研究生入学考");date_saver_update(&local);capture("date-saver-day-ten-event");
    assert(lv_obj_get_style_text_font(date_saver_title,0)==date_vector_fonts[0]&&lv_obj_get_style_transform_scale_x(date_saver_title,0)==256);
    int stable_size=date_vector_sizes[0];for(int i=0;i<20;i++)date_saver_update(&local);assert(date_vector_sizes[0]==stable_size);
    strcpy(date_countdown.title,"龟");date_saver_update(&local);assert(lv_obj_get_style_text_font(date_saver_title,0)==&tab5_font_30);capture("date-saver-uncommon");
    strcpy(date_countdown.title,"全国硕士研究生入学考");date_saver_update(&local);assert(lv_obj_get_style_text_font(date_saver_title,0)==date_vector_fonts[0]);
    strcpy(date_countdown.title,"高考");
    for(int n=1;n<=5;n++){
        date_countdown.year=n==5?2199:2027;date_countdown.month=6;date_countdown.day=7;
        if(n==1)local=(struct tm){.tm_year=127,.tm_mon=5,.tm_mday=6,.tm_hour=10};
        if(n==2)local.tm_mon=4;
        if(n==3)local.tm_mon=0;
        if(n==4)local.tm_year=117;
        date_saver_update(&local);lv_obj_update_layout(date_saver_layer);
        int x=lv_obj_get_x(date_saver_days),right=lv_obj_get_x(date_saver_unit)+lv_obj_get_width(date_saver_unit)*lv_obj_get_style_transform_scale_x(date_saver_unit,0)/256;
        assert(x>=650&&right<=1200);char filename[64];snprintf(filename,sizeof(filename),"date-saver-%d-digits",n);capture(filename);
    }
    const char *stress_titles[]={"高考","项目上线","全国硕士研究生入学考"};
    for(int pass=0;pass<48;pass++){
        strcpy(date_countdown.title,stress_titles[pass%3]);date_saver_update(&local);lv_obj_update_layout(date_saver_layer);lv_refr_now(NULL);
        assert(lv_obj_get_style_text_font(date_saver_title,0)==date_vector_fonts[0]);
        assert(lv_obj_get_style_transform_scale_x(date_saver_title,0)==256);
    }
    puts("DATE_VECTOR_FONT_OK native_size_outlines stable_cache_key fallback_return 48_rendered_size_changes");
    local.tm_hour=6;date_saver_update(&local);lv_color_t night=lv_obj_get_style_bg_color(date_saver_layer,0);
    local.tm_hour=7;date_saver_update(&local);lv_color_t day=lv_obj_get_style_bg_color(date_saver_layer,0);assert(!lv_color_eq(night,day));
    local.tm_hour=18;date_saver_update(&local);assert(lv_color_eq(day,lv_obj_get_style_bg_color(date_saver_layer,0)));
    local.tm_hour=19;date_saver_update(&local);assert(lv_color_eq(night,lv_obj_get_style_bg_color(date_saver_layer,0)));
    saver_leave();saver_style=old_saver;date_countdown=saved;preview_date_countdown=persisted;page(0);
    puts("DATE_EDITOR_OK ten_char_atomic_paste legacy_save_guard calendar_leap_draft_cancel back_hierarchy voice_switch_stop_review_cancel_owner offline_guard day_night adaptive_digits");
}
