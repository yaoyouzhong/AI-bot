static void agenda_test_tap(lv_indev_t *touch,lv_obj_t *obj) {
    lv_obj_update_layout(obj);lv_area_t area;lv_obj_get_coords(obj,&area);gallery_pointer_click(touch,(lv_point_t){(area.x1+area.x2)/2,(area.y1+area.y2)/2});
}
static void agenda_test_fonts(lv_obj_t *obj) {
    if(lv_obj_check_type(obj,&lv_label_class)) {
        const char *value=lv_label_get_text(obj);const lv_font_t *font=lv_obj_get_style_text_font(obj,0);
        for(uint32_t at=0;value[at];){uint32_t cp=lv_text_encoded_next(value,&at);lv_font_glyph_dsc_t glyph;
            if(cp!='\n'&&cp!='\r')assert(lv_font_get_glyph_dsc(font,&glyph,cp,0)&&!glyph.is_placeholder);}
    }
    for(uint32_t n=0;n<lv_obj_get_child_count(obj);n++)agenda_test_fonts(lv_obj_get_child(obj,n));
}
static void check_agenda_ui(const char *fixture) {
    FILE *file=fopen(fixture,"rb");assert(file);fseek(file,0,SEEK_END);long length=ftell(file);rewind(file);char *body=calloc(length+1,1);assert(body&&fread(body,1,length,file)==(size_t)length);fclose(file);
    cJSON *root=cJSON_Parse(body);free(body);assert(agenda_parse(root,&preview_agenda.document));cJSON_Delete(root);
    // Select stable examples from the actual RPC response for the visual clock.
    agenda_document source=preview_agenda.document;preview_agenda.document.count=0;
    const char *samples[]={"客户会议","改期会议","整理客户资料"};
    for(unsigned n=0;n<3;n++)for(unsigned j=0;j<source.count;j++)if(!strcmp(source.items[j].title,samples[n]))preview_agenda.document.items[preview_agenda.document.count++]=source.items[j];
    assert(preview_agenda.document.count==3);preview_agenda.ready=true;strcpy(preview_agenda.message,"已同步，可离线提醒");
    preview_gallery_clock_valid=true;preview_gallery_clock_time=(struct tm){.tm_year=126,.tm_mon=9,.tm_mday=15,.tm_hour=14,.tm_min=50};
    page(11);assert(!strcmp(names[13],"日程")&&!strcmp(lv_label_get_text(lv_obj_get_child(lv_obj_get_child(pages[11],0),0)),"日程"));capture("agenda-app-entry");page(13);agenda_today=true;agenda_render();capture("agenda-today");
    assert(!strcmp(agenda_selected,preview_agenda.document.items[0].id));agenda_today=false;agenda_render();
    lv_obj_send_event(agenda_rows[0],LV_EVENT_CLICKED,NULL);capture("agenda-detail");
    if(getenv("TAB5_AGENDA_RENDER_AUDIT")){tab5_draw_cache_drop();capture("agenda-detail-uncached");printf("AGENDA_RENDER_TEXT time=%s detail=%s\n",lv_label_get_text(agenda_row_times[0]),lv_label_get_text(agenda_repeat));}
    assert(!strcmp(lv_label_get_text(agenda_when),"15:00")&&!strstr(lv_label_get_text(agenda_repeat),"16:00")&&!lv_label_get_text(agenda_row_dates[0])[0]);
    assert(!lv_label_get_text(agenda_status)[0]);
    // The homepage exposes five whole rows, long selected titles and contextual actions.
    agenda_document homepage_document=preview_agenda.document;char homepage_selected[33];strcpy(homepage_selected,agenda_selected);
    for(unsigned n=0;n<8;n++) {
        preview_agenda.document.items[n]=homepage_document.items[0];agenda_item *sample=&preview_agenda.document.items[n];
        snprintf(sample->id,sizeof(sample->id),"%032x",n+100);sample->due+=n*3600;sample->end+=n*3600;
    }
    preview_agenda.document.count=8;strcpy(preview_agenda.document.items[0].title,"客户会议客户会议客户会议客户会议客户会议客户会议客户会议客户会议");
    agenda_selected[0]=0;agenda_render();lv_obj_update_layout(pages[13]);capture("agenda-home-five-rows-long-title");
    lv_area_t list_area,row_area,title_area,repeat_area;
    lv_obj_get_coords(agenda_list,&list_area);lv_obj_get_coords(agenda_rows[4],&row_area);lv_obj_get_coords(agenda_title,&title_area);lv_obj_get_coords(agenda_repeat,&repeat_area);
    assert(row_area.y2<=list_area.y2&&title_area.x1>list_area.x2&&title_area.y2<repeat_area.y1);
    assert(!strcmp(lv_label_get_text(agenda_title),preview_agenda.document.items[0].title));
    lv_point_t title_size;lv_text_get_size(&title_size,preview_agenda.document.items[0].title,lv_obj_get_style_text_font(agenda_title,0),lv_obj_get_style_text_letter_space(agenda_title,0),lv_obj_get_style_text_line_space(agenda_title,0),lv_obj_get_content_width(agenda_title),LV_TEXT_FLAG_NONE);
    assert(title_size.y<=lv_obj_get_content_height(agenda_title));
    lv_obj_scroll_to_view(agenda_rows[7],LV_ANIM_OFF);lv_obj_update_layout(agenda_list);lv_obj_get_coords(agenda_rows[7],&row_area);assert(row_area.y1>=list_area.y1&&row_area.y2<=list_area.y2);
    lv_obj_send_event(agenda_rows[7],LV_EVENT_CLICKED,NULL);assert(!strcmp(agenda_selected,preview_agenda.document.items[7].id));capture("agenda-home-scrolled-selection");
    agenda_test_fonts(pages[13]);preview_agenda.document=homepage_document;strcpy(agenda_selected,homepage_selected);lv_obj_scroll_to_y(agenda_list,0,LV_ANIM_OFF);agenda_render();
    puts("AGENDA_HOME_LAYOUT_OK five full rows, long title fits, last row scroll/selection, normal status silent");
    // Hit-test the regrouped toolbar; surfaces must not intercept a real touch.
    lv_indev_t *toolbar_touch=lv_indev_create();lv_indev_set_type(toolbar_touch,LV_INDEV_TYPE_POINTER);lv_indev_set_display(toolbar_touch,lv_display_get_default());lv_indev_set_read_cb(toolbar_touch,camera_touch_read);
    agenda_test_tap(toolbar_touch,agenda_filters[0]);assert(agenda_today);
    agenda_test_tap(toolbar_touch,agenda_filters[1]);assert(!agenda_today);
    unsigned toolbar_count=preview_agenda.document.count;
    agenda_test_tap(toolbar_touch,agenda_new);assert(agenda_editor_open());agenda_editor_close(NULL);
    assert(!agenda_editor_open()&&preview_agenda.document.count==toolbar_count);
    lv_indev_delete(toolbar_touch);agenda_render();
    puts("AGENDA_TOOLBAR_TOUCH_OK Today/All filter and populated New/cancel preserve items");
    assert(!lv_obj_has_flag(agenda_snooze,LV_OBJ_FLAG_HIDDEN));lv_obj_send_event(agenda_snooze,LV_EVENT_CLICKED,NULL);
    assert(preview_agenda.document.items[0].snooze_until==agenda_civil_now()+600);agenda_render();capture("agenda-snoozed");
    // Save failures keep the popup visible until a matching successful operation result.
    unsigned agenda_sound_before=preview_agenda_chimes,timer_sound_before=preview_timer_chimes;
    preview_agenda_notice=true;agenda_ui_next=0;agenda_tick(esp_timer_get_time()/1000+1000);assert(!lv_obj_has_flag(agenda_popup,LV_OBJ_FLAG_HIDDEN));capture("agenda-reminder");
    assert(preview_agenda_chimes==agenda_sound_before+1&&preview_timer_chimes==timer_sound_before);
    puts("AGENDA_CHIME_ROUTE_OK agenda cue does not use countdown notification");
    ui_text(agenda_popup_title,"客户会议客户会议客户会议客户会议客户会议客户会议客户会议客户会议");capture("agenda-reminder-long-title");agenda_test_fonts(agenda_popup);
    lv_text_get_size(&title_size,lv_label_get_text(agenda_popup_title),lv_obj_get_style_text_font(agenda_popup_title,0),lv_obj_get_style_text_letter_space(agenda_popup_title,0),lv_obj_get_style_text_line_space(agenda_popup_title,0),lv_obj_get_content_width(agenda_popup_title),LV_TEXT_FLAG_NONE);
    lv_area_t popup_title_area,popup_status_area,popup_button_area;
    lv_obj_get_coords(agenda_popup_title,&popup_title_area);lv_obj_get_coords(agenda_popup_status,&popup_status_area);lv_obj_get_coords(agenda_popup_snooze,&popup_button_area);
    assert(title_size.y<=lv_obj_get_content_height(agenda_popup_title)&&popup_title_area.y2<popup_status_area.y1&&popup_status_area.y2<popup_button_area.y1);
    assert(lv_obj_get_height(agenda_popup_snooze)>=64&&lv_obj_get_height(agenda_popup_dismiss)>=64);
    assert(lv_obj_get_width(agenda_popup_snooze)>lv_obj_get_width(agenda_popup_dismiss)&&strstr(lv_label_get_text(agenda_popup_time),"15:00"));
    lv_indev_t *popup_touch=lv_indev_create();lv_indev_set_type(popup_touch,LV_INDEV_TYPE_POINTER);lv_indev_set_display(popup_touch,lv_display_get_default());lv_indev_set_read_cb(popup_touch,camera_touch_read);
    unsigned popup_requests=preview_agenda.completed_request;
    agenda_test_tap(popup_touch,agenda_new);assert(!agenda_editor_open()&&!lv_obj_has_flag(agenda_popup,LV_OBJ_FLAG_HIDDEN)&&preview_agenda.completed_request==popup_requests);
    agenda_test_tap(popup_touch,agenda_popup_dismiss);assert(lv_obj_has_flag(agenda_popup,LV_OBJ_FLAG_HIDDEN)&&preview_agenda.completed_request==popup_requests);
    preview_agenda_notice=true;agenda_ui_next=0;agenda_tick(esp_timer_get_time()/1000+1500);
    assert(!lv_obj_has_flag(agenda_popup,LV_OBJ_FLAG_HIDDEN));
    ui_text(agenda_popup_title,preview_agenda.document.items[0].title);
    preview_agenda_save_fail=true;agenda_test_tap(popup_touch,agenda_popup_snooze);
    assert(lv_obj_has_state(agenda_popup_snooze,LV_STATE_DISABLED)&&lv_obj_has_state(agenda_popup_dismiss,LV_STATE_DISABLED));
    popup_requests=preview_agenda.completed_request;agenda_test_tap(popup_touch,agenda_popup_snooze);agenda_test_tap(popup_touch,agenda_popup_dismiss);
    assert(preview_agenda.completed_request==popup_requests&&!lv_obj_has_flag(agenda_popup,LV_OBJ_FLAG_HIDDEN));
    agenda_ui_next=0;agenda_tick(esp_timer_get_time()/1000+2000);
    assert(!lv_obj_has_flag(agenda_popup,LV_OBJ_FLAG_HIDDEN)&&strstr(lv_label_get_text(agenda_popup_status),"保存失败")&&!lv_obj_has_state(agenda_popup_snooze,LV_STATE_DISABLED)&&!lv_obj_has_state(agenda_popup_dismiss,LV_STATE_DISABLED));capture("agenda-reminder-save-failed");agenda_test_fonts(agenda_popup);
    preview_agenda_save_fail=false;agenda_test_tap(popup_touch,agenda_popup_snooze);assert(!lv_obj_has_flag(agenda_popup,LV_OBJ_FLAG_HIDDEN));
    agenda_ui_next=0;agenda_tick(esp_timer_get_time()/1000+3000);assert(lv_obj_has_flag(agenda_popup,LV_OBJ_FLAG_HIDDEN));
    lv_indev_delete(popup_touch);
    puts("AGENDA_REMINDER_UI_OK long title fits, modal blocks background, pointer dismiss/snooze, pending prevents duplicate input, save failure recovers");
    preview_gallery_clock_time.tm_hour=15;preview_gallery_clock_time.tm_min=20;agenda_render();assert(strstr(lv_label_get_text(agenda_repeat),"进行中"));capture("agenda-running");
    preview_gallery_clock_time.tm_min=55;agenda_render();assert(lv_obj_has_flag(agenda_snooze,LV_OBJ_FLAG_HIDDEN));
    preview_agenda_notice=true;agenda_ui_next=0;agenda_tick(esp_timer_get_time()/1000+4000);
    assert(!lv_obj_has_flag(agenda_popup,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(agenda_popup_snooze,LV_OBJ_FLAG_HIDDEN));capture("agenda-near-end");agenda_test_fonts(agenda_popup);
    preview_gallery_clock_time.tm_hour=16;preview_gallery_clock_time.tm_min=0;agenda_ui_next=0;agenda_tick(esp_timer_get_time()/1000+5000);
    assert(lv_obj_has_flag(agenda_popup,LV_OBJ_FLAG_HIDDEN)&&!strcmp(agenda_selected,preview_agenda.document.items[0].id)&&!strcmp(lv_label_get_text(agenda_repeat),"已结束"));capture("agenda-ended");
    assert(sizeof(agenda_filters)/sizeof(agenda_filters[0])==2);
    lv_obj_send_event(agenda_filters[0],LV_EVENT_CLICKED,NULL);assert(agenda_today&&strcmp(agenda_selected,preview_agenda.document.items[0].id));
    lv_obj_send_event(agenda_filters[1],LV_EVENT_CLICKED,NULL);assert(!agenda_today);bool expired_visible=false;
    for(unsigned n=0;n<AGENDA_LIMIT;n++)if(!lv_obj_has_flag(agenda_rows[n],LV_OBJ_FLAG_HIDDEN)&&!strcmp(lv_label_get_text(agenda_row_titles[n]),preview_agenda.document.items[0].title)){expired_visible=true;assert(!strcmp(lv_label_get_text(agenda_row_times[n]),"已结束"));}
    assert(expired_visible);capture("agenda-expired-retained");agenda_test_fonts(pages[13]);
    // A notice queued before the end must not reopen the popup afterward.
    preview_agenda_notice=true;agenda_ui_next=0;agenda_tick(esp_timer_get_time()/1000+6000);assert(lv_obj_has_flag(agenda_popup,LV_OBJ_FLAG_HIDDEN));
    // Local creation uses its own Chinese composer and waits for persistence acknowledgement.
    lv_obj_send_event(agenda_new,LV_EVENT_CLICKED,NULL);assert(agenda_editor_open());capture("agenda-editor-new");
    assert(lv_obj_get_x(agenda_editor_times)==748&&lv_obj_get_y(agenda_editor_times)==136&&lv_obj_get_width(agenda_editor_times)==468&&lv_obj_get_height(agenda_editor_times)==484);
    assert(lv_obj_get_x(agenda_editor_voice_button)>lv_obj_get_x(agenda_editor_name)+lv_obj_get_width(agenda_editor_name));
    assert(lv_obj_get_y(agenda_editor_voice_button)==lv_obj_get_y(agenda_editor_name)+(lv_obj_get_height(agenda_editor_name)-72)/2);
    lv_area_t feedback_area,name_feedback_area;lv_obj_get_coords(agenda_editor_feedback,&feedback_area);lv_obj_get_coords(agenda_editor_name,&name_feedback_area);
    assert(feedback_area.x1==name_feedback_area.x1&&feedback_area.y1>name_feedback_area.y2);
    assert(agenda_editor_has_time);
    assert(lv_obj_get_y(agenda_editor_durations[0])==lv_obj_get_y(agenda_editor_endpoints[1]));
    assert(lv_obj_get_style_text_font(agenda_editor_clocks[0],0)==&tab5_digits_72&&agenda_editor_clocks[1]==NULL);
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(agenda_editor_endpoints[1],0)),"自定义"));
    assert(strstr(lv_dropdown_get_options(agenda_editor_repeat),"周一至周五")&&strstr(lv_dropdown_get_options(agenda_editor_repeat),"工作日"));
    assert(strstr(lv_dropdown_get_options(agenda_editor_lead),"提前 1 分钟")&&strstr(lv_dropdown_get_options(agenda_editor_lead),"提前 3 分钟"));
    assert(!lv_obj_has_flag(agenda_editor_mic_glyph,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(agenda_editor_stop_glyph,LV_OBJ_FLAG_HIDDEN));
    // A calendar and two rollers edit a private draft, preserving duration when start moves.
    int64_t initial_start=agenda_editor_date(0),initial_end=agenda_editor_date(1);
    lv_indev_t *agenda_touch=lv_indev_create();lv_indev_set_type(agenda_touch,LV_INDEV_TYPE_POINTER);lv_indev_set_display(agenda_touch,lv_display_get_default());lv_indev_set_read_cb(agenda_touch,camera_touch_read);
    agenda_test_tap(agenda_touch,agenda_editor_endpoints[0]);assert(agenda_picker_open());capture("agenda-time-picker");agenda_test_fonts(agenda_editor);
    lv_roller_set_selected(agenda_picker_minutes,8,LV_ANIM_OFF);lv_obj_update_layout(agenda_picker_minutes);lv_area_t roller;lv_obj_get_coords(agenda_picker_minutes,&roller);
    camera_touch_point=(lv_point_t){(roller.x1+roller.x2)/2,(roller.y1+roller.y2)/2};camera_touch_state=LV_INDEV_STATE_PRESSED;lv_indev_read(agenda_touch);
    camera_touch_point.y-=40;lv_indev_read(agenda_touch);camera_touch_point.y-=40;lv_indev_read(agenda_touch);camera_touch_state=LV_INDEV_STATE_RELEASED;lv_indev_read(agenda_touch);lv_tick_inc(400);lv_timer_handler();
    assert(lv_roller_get_selected(agenda_picker_minutes)!=8); // Actual drag input, not only programmatic selection.
    agenda_test_tap(agenda_touch,agenda_picker_days[1]);assert(agenda_picker_day.day==16);
    lv_roller_set_selected(agenda_picker_hours,9,LV_ANIM_OFF);lv_roller_set_selected(agenda_picker_minutes,30,LV_ANIM_OFF);
    agenda_picker_close(NULL);assert(agenda_editor_date(0)==initial_start&&agenda_editor_date(1)==initial_end);
    lv_obj_send_event(agenda_editor_endpoints[0],LV_EVENT_CLICKED,NULL);lv_obj_send_event(agenda_picker_days[1],LV_EVENT_CLICKED,NULL);
    lv_roller_set_selected(agenda_picker_hours,9,LV_ANIM_OFF);lv_roller_set_selected(agenda_picker_minutes,30,LV_ANIM_OFF);
    lv_obj_send_event(agenda_picker_confirm,LV_EVENT_CLICKED,NULL);assert(!agenda_picker_open()&&agenda_editor_date(0)==1792143000LL&&agenda_editor_date(1)==1792146600LL);
    lv_obj_send_event(agenda_editor_durations[0],LV_EVENT_CLICKED,NULL);assert(agenda_editor_date(1)-agenda_editor_date(0)==1800);
    lv_obj_send_event(agenda_editor_endpoints[1],LV_EVENT_CLICKED,NULL);lv_obj_send_event(agenda_picker_days[0],LV_EVENT_CLICKED,NULL);
    lv_obj_send_event(agenda_picker_confirm,LV_EVENT_CLICKED,NULL);assert(agenda_picker_open()&&strstr(lv_label_get_text(agenda_picker_feedback),"晚于"));
    lv_obj_send_event(agenda_picker_days[1],LV_EVENT_CLICKED,NULL);agenda_test_tap(agenda_touch,agenda_picker_dates[20]);assert(agenda_picker_day.day==17);
    lv_obj_send_event(agenda_picker_confirm,LV_EVENT_CLICKED,NULL);assert(agenda_picker_open()); // Longer than 24 hours.
    lv_obj_send_event(agenda_picker_days[1],LV_EVENT_CLICKED,NULL);lv_roller_set_selected(agenda_picker_hours,10,LV_ANIM_OFF);lv_roller_set_selected(agenda_picker_minutes,0,LV_ANIM_OFF);
    lv_obj_send_event(agenda_picker_confirm,LV_EVENT_CLICKED,NULL);assert(!agenda_picker_open()&&agenda_editor_date(1)-agenda_editor_date(0)==1800);
    agenda_editor_set_date(0,initial_start);agenda_editor_set_date(1,initial_end);lv_indev_delete(agenda_touch);
    lv_obj_send_event(agenda_editor_name,LV_EVENT_CLICKED,NULL);assert(!lv_obj_has_flag(agenda_editor_keyboard,LV_OBJ_FLAG_HIDDEN));
    for(const char *spelling="nihao";*spelling;spelling++){char key[2]={*spelling,0};assert(press_text(agenda_editor_keyboard,key));}
    assert(!strcmp(tab5_ime_pending_for(agenda_editor_ime),"nihao")&&!tab5_ime_pending()[0]);
    assert(press_text(lv_obj_get_child(agenda_editor_ime,2),"你好"));assert(!strcmp(lv_textarea_get_text(agenda_editor_name),"你好"));capture("agenda-editor-keyboard");agenda_editor_keyboard_hide(NULL);
    lv_textarea_set_text(agenda_editor_name,"设备日程");agenda_editor_set_date(0,1792108200LL);agenda_editor_set_date(1,1792111800LL);capture("agenda-editor-edit");
    lv_dropdown_set_selected(agenda_editor_repeat,4);lv_dropdown_set_selected(agenda_editor_lead,1);lv_dropdown_open(agenda_editor_repeat);capture("agenda-editor-repeat-workday");lv_dropdown_close(agenda_editor_repeat);
    lv_dropdown_open(agenda_editor_lead);capture("agenda-editor-reminder-options");agenda_test_fonts(lv_dropdown_get_list(agenda_editor_lead));lv_dropdown_close(agenda_editor_lead);
    // An invalid end never enters the save queue.
    unsigned requests=preview_agenda.completed_request;agenda_editor_set_date(1,1792108200LL);lv_obj_send_event(agenda_editor_save_button,LV_EVENT_CLICKED,NULL);assert(preview_agenda.completed_request==requests&&agenda_editor_open());agenda_editor_set_date(1,1792111800LL);
    preview_agenda_save_fail=true;lv_obj_send_event(agenda_editor_save_button,LV_EVENT_CLICKED,NULL);agenda_render();assert(agenda_editor_open()&&!strcmp(lv_textarea_get_text(agenda_editor_name),"设备日程")&&strstr(lv_label_get_text(agenda_editor_feedback),"保存失败"));capture("agenda-editor-save-failed");
    preview_agenda_save_fail=false;lv_obj_send_event(agenda_editor_save_button,LV_EVENT_CLICKED,NULL);agenda_render();assert(!agenda_editor_open()&&preview_agenda.document.count==4&&preview_agenda.pending);
    assert(preview_agenda.document.items[3].repeat==4&&preview_agenda.document.items[3].lead==1);
    agenda_today=false;agenda_render();lv_obj_send_event(agenda_edit_button,LV_EVENT_CLICKED,NULL);assert(agenda_editor_open());
    assert(lv_dropdown_get_selected(agenda_editor_repeat)==4&&lv_dropdown_get_selected(agenda_editor_lead)==1);
    lv_dropdown_set_selected(agenda_editor_lead,2);lv_obj_send_event(agenda_editor_save_button,LV_EVENT_CLICKED,NULL);agenda_render();assert(preview_agenda.document.items[3].lead==3);
    lv_obj_send_event(agenda_edit_button,LV_EVENT_CLICKED,NULL);assert(agenda_editor_open()&&lv_dropdown_get_selected(agenda_editor_lead)==2);
    // Voice owns the name field; it inserts only an explicitly stopped, final result once.
    if(!shown)shown=cJSON_CreateObject();cJSON *pc=cJSON_GetObjectItemCaseSensitive(shown,"pcInput");if(!pc){pc=cJSON_CreateObject();cJSON_AddItemToObject(shown,"pcInput",pc);}assert(cJSON_IsObject(pc));cJSON_DeleteItemFromObjectCaseSensitive(pc,"session");cJSON_AddStringToObject(pc,"session","agenda-voice-session");last_received=esp_timer_get_time()/1000;preview_voice_start_enabled=true;
    lv_textarea_set_text(codex_input,"保留 Codex 草稿");int submissions=preview_submit_count;
    lv_textarea_set_text(agenda_editor_name,"");agenda_editor_keyboard_show(NULL);lv_obj_update_layout(agenda_editor);
    lv_area_t voice_area,name_area,ime_area;lv_obj_get_coords(agenda_editor_voice_button,&voice_area);lv_obj_get_coords(agenda_editor_name,&name_area);lv_obj_get_coords(agenda_editor_ime,&ime_area);
    assert(lv_area_get_height(&voice_area)>=64&&voice_area.x1>name_area.x2&&voice_area.y1>=name_area.y1&&voice_area.y2<=name_area.y2&&voice_area.y2<ime_area.y1);
    agenda_touch=lv_indev_create();lv_indev_set_type(agenda_touch,LV_INDEV_TYPE_POINTER);lv_indev_set_display(agenda_touch,lv_display_get_default());lv_indev_set_read_cb(agenda_touch,camera_touch_read);
    gallery_pointer_click(agenda_touch,(lv_point_t){(voice_area.x1+voice_area.x2)/2,voice_area.y2-8});
    if(!agenda_voice_waiting)fprintf(stderr,"AGENDA_VOICE_START_DIAGNOSTIC busy=%d pending=%u hint=%s\n",preview_voice.busy,agenda_pending_request,lv_label_get_text(agenda_editor_feedback));
    assert(agenda_voice_waiting&&!strcmp(preview_voice.task_id,AGENDA_VOICE_OWNER));
    preview_voice.ready=true;preview_voice.level=62;strcpy(preview_voice.source,"dji");strcpy(preview_voice.source_name,"麦克风 (Wireless Mic Rx)");strcpy(preview_voice.phase,"recording");agenda_editor_voice_tick();capture("agenda-editor-voice-recording");agenda_test_fonts(agenda_editor);
    assert(lv_obj_has_flag(agenda_editor_mic_glyph,LV_OBJ_FLAG_HIDDEN)&&!lv_obj_has_flag(agenda_editor_stop_glyph,LV_OBJ_FLAG_HIDDEN));
    assert(!lv_obj_has_flag(agenda_editor_voice_meter,LV_OBJ_FLAG_HIDDEN)&&lv_bar_get_value(agenda_editor_voice_meter)==62&&strstr(lv_label_get_text(agenda_editor_feedback),"Wireless Mic Rx"));
    lv_obj_get_coords(agenda_editor_feedback,&feedback_area);lv_obj_get_coords(agenda_editor_name,&name_feedback_area);assert(feedback_area.x1==name_feedback_area.x1);
    lv_area_t recording_area;lv_obj_get_coords(agenda_editor_voice_button,&recording_area);assert(!memcmp(&voice_area,&recording_area,sizeof(voice_area))&&!lv_obj_has_flag(agenda_editor_keyboard,LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_has_state(agenda_editor_keyboard,LV_STATE_DISABLED)&&lv_obj_has_state(lv_obj_get_child(agenda_editor_ime,2),LV_STATE_DISABLED));
    agenda_test_tap(agenda_touch,agenda_editor_keyboard);assert(!lv_textarea_get_text(agenda_editor_name)[0]);
    // Reuse Codex command 3; switching never stops/inserts/saves this draft.
    assert(!lv_obj_has_flag(agenda_editor_voice_switch,LV_OBJ_FLAG_HIDDEN));agenda_test_tap(agenda_touch,agenda_editor_voice_switch);
    assert(preview_voice_command==3&&agenda_voice_switching&&agenda_voice_waiting);agenda_editor_voice_tick();capture("agenda-editor-voice-switching");
    assert(lv_obj_has_flag(agenda_editor_voice_switch,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_state(agenda_editor_voice_button,LV_STATE_DISABLED)&&!lv_textarea_get_text(agenda_editor_name)[0]);
    strcpy(preview_voice.source,"tab5");strcpy(preview_voice.source_name,"TAB5 内置麦克风");agenda_editor_voice_tick();capture("agenda-editor-voice-tab5");agenda_test_fonts(agenda_editor);
    assert(!agenda_voice_switching&&lv_obj_has_flag(agenda_editor_voice_switch,LV_OBJ_FLAG_HIDDEN)&&!lv_obj_has_state(agenda_editor_voice_button,LV_STATE_DISABLED)&&strstr(lv_label_get_text(agenda_editor_feedback),"内置麦克风"));
    gallery_pointer_click(agenda_touch,(lv_point_t){(voice_area.x1+voice_area.x2)/2,voice_area.y2-8});assert(preview_voice_command==1);lv_indev_delete(agenda_touch);
    preview_voice.busy=false;preview_voice.can_insert=true;strcpy(preview_voice.phase,"review");strcpy(preview_voice.text,"语音会议");agenda_editor_voice_tick();assert(!agenda_voice_waiting&&!strcmp(lv_textarea_get_text(agenda_editor_name),"语音会议"));assert(!lv_obj_has_state(agenda_editor_keyboard,LV_STATE_DISABLED)&&!lv_obj_has_state(lv_obj_get_child(agenda_editor_ime,2),LV_STATE_DISABLED));agenda_editor_voice_tick();assert(!strcmp(lv_textarea_get_text(agenda_editor_name),"语音会议"));capture("agenda-editor-voice-result");agenda_editor_keyboard_hide(NULL);
    lv_obj_send_event(agenda_editor_voice_button,LV_EVENT_CLICKED,NULL);assert(agenda_voice_waiting);
    preview_voice.ready=true;preview_voice.level=48;strcpy(preview_voice.source,"dji");strcpy(preview_voice.source_name,"麦克风 (Wireless Mic Rx)");strcpy(preview_voice.phase,"recording");agenda_editor_voice_tick();capture("agenda-editor-voice-dji");
    preview_voice.silent=true;agenda_editor_voice_tick();assert(strstr(lv_label_get_text(agenda_editor_feedback),"未检测到声音"));preview_voice.silent=false;
    strcpy(preview_voice.phase,"recognizing");preview_voice.ready=false;agenda_editor_voice_tick();capture("agenda-editor-voice-recognizing");
    assert(lv_obj_has_state(agenda_editor_voice_button,LV_STATE_DISABLED)&&!lv_obj_has_flag(agenda_editor_voice_cancel,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(agenda_editor_voice_meter,LV_OBJ_FLAG_HIDDEN));
    assert(strstr(lv_label_get_text(agenda_editor_feedback),"正在识别"));agenda_test_fonts(agenda_editor);
    lv_obj_send_event(agenda_editor_voice_cancel,LV_EVENT_CLICKED,NULL);assert(preview_voice_command==2);preview_voice.busy=false;strcpy(preview_voice.phase,"cancelled");agenda_editor_voice_tick();assert(!strcmp(lv_textarea_get_text(agenda_editor_name),"语音会议"));
    assert(!lv_obj_has_flag(agenda_editor_mic_glyph,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(agenda_editor_stop_glyph,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(agenda_editor_voice_meter,LV_OBJ_FLAG_HIDDEN));
    lv_obj_send_event(agenda_editor_voice_button,LV_EVENT_CLICKED,NULL);preview_voice.busy=false;strcpy(preview_voice.phase,"error");strcpy(preview_voice.message,"连接中断");agenda_editor_voice_tick();assert(!strcmp(lv_textarea_get_text(agenda_editor_name),"语音会议"));
    // A result from another owner must never replace the agenda name.
    lv_obj_send_event(agenda_editor_voice_button,LV_EVENT_CLICKED,NULL);preview_voice.busy=false;strcpy(preview_voice.task_id,"00000000-0000-4000-8000-000000000000");strcpy(preview_voice.phase,"review");strcpy(preview_voice.text,"其他会话");preview_voice.can_insert=true;agenda_editor_voice_tick();assert(!strcmp(lv_textarea_get_text(agenda_editor_name),"语音会议"));
    // Real completion path: return a usable prefix instead of throwing away speech.
    for(unsigned test=0;test<5;test++) {
        const char *base=test==2?"已有名称":test==4?"12345678901234567890123456789012":"";
        const char *unit=test==3?"\xF0\x9F\x98\x80":test==2?"x":"会";
        unsigned count=test==0?32:40;
        lv_textarea_set_text(agenda_editor_name,base);lv_textarea_set_cursor_pos(agenda_editor_name,test==2?2:LV_TEXTAREA_CURSOR_LAST);
        lv_obj_send_event(agenda_editor_voice_button,LV_EVENT_CLICKED,NULL);assert(agenda_voice_waiting);
        preview_voice.busy=false;strcpy(preview_voice.phase,"review");preview_voice.text[0]=0;
        for(unsigned n=0;n<count;n++)strcat(preview_voice.text,unit);
        preview_voice.can_insert=true;agenda_editor_voice_tick();assert(!agenda_voice_waiting);
        const char *result=lv_textarea_get_text(agenda_editor_name);unsigned kept=test==3?24:test==4?0:test==2?28:32;
        char expected[97]="";if(test==2)strcpy(expected,"已有");if(test==4)strcpy(expected,base);
        for(unsigned n=0;n<kept;n++)strcat(expected,unit);if(test==2)strcat(expected,"名称");
        assert(!strcmp(result,expected)&&strlen(result)<=96&&lv_text_get_encoded_length(result)<=32);
        assert(!lv_obj_has_state(agenda_editor_save_button,LV_STATE_DISABLED)&&!lv_obj_has_state(agenda_editor_name,LV_STATE_DISABLED));
        assert(test==4?strstr(lv_label_get_text(agenda_editor_feedback),"已满")!=NULL:test==0?strstr(lv_label_get_text(agenda_editor_feedback),"已填入")!=NULL:strstr(lv_label_get_text(agenda_editor_feedback),test==3?"已截断":"已保留 32 字")!=NULL);
        agenda_editor_voice_tick();assert(!strcmp(lv_textarea_get_text(agenda_editor_name),expected));
        if(test==1){capture("agenda-editor-voice-truncated");agenda_test_fonts(agenda_editor);}
    }
    printf("AGENDA_VOICE_TRUNCATION_OK exact32/over32/cursor-suffix/utf8-byte-limit/full-title; no auto-save\n");
    last_received=esp_timer_get_time()/1000-7000;lv_obj_send_event(agenda_editor_voice_button,LV_EVENT_CLICKED,NULL);assert(!agenda_voice_waiting&&strstr(lv_label_get_text(agenda_editor_feedback),"连接电脑"));
    agenda_editor_close(NULL);assert(!agenda_editor_open());preview_voice_start_enabled=false;
    assert(preview_submit_count==submissions&&!strcmp(lv_textarea_get_text(codex_input),"保留 Codex 草稿"));
    assert(!strcmp(preview_agenda.document.items[3].title,"设备日程")); // Voice edits remain unsaved until explicit Save.
    // Newly entering a past one-off is rejected; existing elapsed entries remain visible.
    lv_obj_send_event(agenda_new,LV_EVENT_CLICKED,NULL);lv_textarea_set_text(agenda_editor_name,"已结束事项");agenda_editor_set_date(0,1792069200LL);agenda_editor_set_date(1,1792071000LL);
    lv_obj_send_event(agenda_editor_save_button,LV_EVENT_CLICKED,NULL);agenda_render();assert(agenda_editor_open()&&!strcmp(lv_textarea_get_text(agenda_editor_name),"已结束事项")&&strstr(lv_label_get_text(agenda_editor_feedback),"时间已过"));capture("agenda-past-rejected");agenda_test_fonts(agenda_editor);
    lv_textarea_set_text(agenda_editor_name,"自动清理规则");agenda_editor_set_date(0,agenda_civil_now()+3600);agenda_editor_set_date(1,agenda_civil_now()+7200);
    lv_obj_send_event(agenda_editor_save_button,LV_EVENT_CLICKED,NULL);agenda_render();assert(!agenda_editor_open()&&!strcmp(lv_label_get_text(agenda_title),"自动清理规则"));capture("agenda-future-saved");
    agenda_item *past=&preview_agenda.document.items[preview_agenda.document.count-1];past->dirty=4;char removed_id[33];strcpy(removed_id,past->id);unsigned retained_count=preview_agenda.document.count;
    lv_indev_t *delete_touch=lv_indev_create();lv_indev_set_type(delete_touch,LV_INDEV_TYPE_POINTER);lv_indev_set_display(delete_touch,lv_display_get_default());lv_indev_set_read_cb(delete_touch,camera_touch_read);
    agenda_test_tap(delete_touch,agenda_delete_button);assert(agenda_delete_open());agenda_test_fonts(agenda_delete_layer);capture("agenda-delete-confirmation");
    agenda_test_tap(delete_touch,agenda_new);assert(agenda_delete_open()&&!agenda_editor_open()&&preview_agenda.document.count==retained_count);
    ui_text(agenda_delete_title,"客户会议客户会议客户会议客户会议客户会议客户会议客户会议客户会议");capture("agenda-delete-long-title");agenda_test_fonts(agenda_delete_layer);
    lv_text_get_size(&title_size,lv_label_get_text(agenda_delete_title),lv_obj_get_style_text_font(agenda_delete_title,0),lv_obj_get_style_text_letter_space(agenda_delete_title,0),lv_obj_get_style_text_line_space(agenda_delete_title,0),lv_obj_get_content_width(agenda_delete_title),LV_TEXT_FLAG_NONE);
    lv_obj_get_coords(agenda_delete_title,&popup_title_area);lv_obj_get_coords(agenda_delete_status,&popup_status_area);lv_obj_get_coords(agenda_delete_yes,&popup_button_area);
    assert(title_size.y<=lv_obj_get_content_height(agenda_delete_title)&&popup_title_area.y2<popup_status_area.y1&&popup_status_area.y2<popup_button_area.y1);
    assert(lv_obj_get_height(agenda_delete_no)>=64&&lv_obj_get_height(agenda_delete_yes)>=64&&lv_obj_get_width(agenda_delete_no)==lv_obj_get_width(agenda_delete_yes));
    agenda_test_tap(delete_touch,agenda_delete_no);assert(!agenda_delete_open()&&preview_agenda.document.count==retained_count);
    agenda_test_tap(delete_touch,agenda_delete_button);preview_agenda_save_fail=true;agenda_test_tap(delete_touch,agenda_delete_yes);
    assert(lv_obj_has_state(agenda_delete_no,LV_STATE_DISABLED)&&lv_obj_has_state(agenda_delete_yes,LV_STATE_DISABLED));
    unsigned delete_request=preview_agenda.completed_request;agenda_test_tap(delete_touch,agenda_delete_no);agenda_test_tap(delete_touch,agenda_delete_yes);
    assert(agenda_delete_open()&&preview_agenda.completed_request==delete_request);
    agenda_render();assert(agenda_delete_open()&&preview_agenda.document.count==retained_count&&strstr(lv_label_get_text(agenda_delete_status),"仍保留")&&!lv_obj_has_state(agenda_delete_no,LV_STATE_DISABLED)&&!lv_obj_has_state(agenda_delete_yes,LV_STATE_DISABLED));capture("agenda-delete-unconfirmed");agenda_test_fonts(agenda_delete_layer);
    preview_agenda_save_fail=false;agenda_test_tap(delete_touch,agenda_delete_yes);agenda_render();assert(!agenda_delete_open()&&preview_agenda.document.count==retained_count-1&&!agenda_selected_item(removed_id));capture("agenda-delete-complete");lv_indev_delete(delete_touch);puts("AGENDA_DELETE_UI_OK pointer confirmation/cancellation, modal background block, long-title fit, pending duplicate-input guard, failed local write preserves record and recovers; unsynced item deletes locally");
    lv_obj_send_event(agenda_filters[1],LV_EVENT_CLICKED,NULL);assert(!agenda_today);puts("AGENDA_RETENTION_UI_OK today hides ended entries, all retains and labels ended entries, past new input preserved and future entry selectable");
    strcpy(preview_agenda.message,"本机操作已保存，连接后自动同步");preview_agenda.pending=true;agenda_render();agenda_test_fonts(pages[13]);capture("agenda-sync-retrying");
    assert(strstr(lv_label_get_text(agenda_status),"待同步"));
    strcpy(preview_agenda.message,"事项已同步，工作日历更新失败，将自动重试");preview_agenda.pending=false;agenda_render();assert(strstr(lv_label_get_text(agenda_status),"失败"));agenda_test_fonts(pages[13]);capture("agenda-calendar-retrying");strcpy(preview_agenda.message,"");
    agenda_document empty_test_document=preview_agenda.document;
    preview_agenda.document.count=1;preview_agenda.document.items[0]=homepage_document.items[1];agenda_today=true;agenda_render();capture("agenda-today-empty-other-dates");
    assert(!strcmp(lv_label_get_text(agenda_empty),"今天没有事项")&&lv_obj_has_flag(agenda_filters[0],LV_OBJ_FLAG_HIDDEN)&&!lv_obj_has_flag(agenda_filters[1],LV_OBJ_FLAG_HIDDEN));
    lv_indev_t *empty_touch=lv_indev_create();lv_indev_set_type(empty_touch,LV_INDEV_TYPE_POINTER);lv_indev_set_display(empty_touch,lv_display_get_default());lv_indev_set_read_cb(empty_touch,camera_touch_read);
    agenda_test_tap(empty_touch,agenda_filters[1]);assert(!agenda_today&&!lv_obj_has_flag(agenda_list,LV_OBJ_FLAG_HIDDEN)&&!lv_obj_has_flag(agenda_filters[0],LV_OBJ_FLAG_HIDDEN));
    lv_obj_update_layout(pages[13]);assert(lv_obj_get_y(agenda_new)<lv_obj_get_y(agenda_list));capture("agenda-empty-all-restored");
    preview_agenda.document=empty_test_document;memset(&preview_agenda.document,0,sizeof(preview_agenda.document));agenda_render();capture("agenda-empty");
    assert(!strcmp(lv_label_get_text(agenda_empty),"暂无事项")&&!lv_label_get_text(agenda_status)[0]);
    assert(lv_obj_has_flag(agenda_title,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(agenda_edit_button,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(agenda_delete_button,LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_has_flag(agenda_filters[0],LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(agenda_filters[1],LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(agenda_list,LV_OBJ_FLAG_HIDDEN));
    lv_obj_update_layout(pages[13]);assert(lv_obj_get_x(agenda_new)*2+lv_obj_get_width(agenda_new)==lv_obj_get_width(pages[13]));
    agenda_test_tap(empty_touch,agenda_new);assert(agenda_editor_open());agenda_editor_close(NULL);lv_indev_delete(empty_touch);
    agenda_today=true;agenda_render();assert(!strcmp(lv_label_get_text(agenda_empty),"暂无事项"));capture("agenda-today-empty");
    puts("AGENDA_EMPTY_ACTIONS_OK empty document has centered New only; empty Today retains All when other dates exist; pointer New/All work; populated toolbar restored");
    preview_gallery_clock_valid=false;agenda_render();capture("agenda-clock-unset");
    puts("AGENDA_UI_OK start-only timeline, automatic end, bounded snooze, local editor, calendar/roller pointer input, duration-preserving start, picker cancel/end guards, glyph coverage, independent Chinese IME, persistent save acknowledgement/failure, Codex source-switch command with stationary controls, voice stop/cancel/error/owner isolation without auto-save, empty/unset clock");
}
