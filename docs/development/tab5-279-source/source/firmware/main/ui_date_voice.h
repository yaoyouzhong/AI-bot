// The voice service treats taskId as an opaque owner; this owner never sends a Codex task.
#define DATE_VOICE_OWNER "a1b07a65-0000-4000-8000-00000000da7e"
static char date_voice_base[97];
static unsigned date_voice_generation,date_voice_byte;
static int date_voice_cursor;
static int64_t date_voice_started;
static bool date_voice_switching;
static void date_voice_lock_keys(lv_obj_t *obj,bool locked) {
    if(locked)lv_obj_add_state(obj,LV_STATE_DISABLED);else lv_obj_remove_state(obj,LV_STATE_DISABLED);
    for(uint32_t n=0;n<lv_obj_get_child_count(obj);n++)date_voice_lock_keys(lv_obj_get_child(obj,n),locked);
}
static void date_editor_voice_tick(void);
static void date_editor_voice_action(lv_event_t *event) {
    int action=(int)(intptr_t)lv_event_get_user_data(event);
    if(date_voice_waiting){
        voice_remote_state *active=&ui_voice_work->date_active;voice_remote_get(active);
        if(active->generation!=date_voice_generation||strcmp(active->task_id,DATE_VOICE_OWNER)){date_editor_voice_tick();return;}
        if(action==2)date_voice_cancelled=true;
        if(action==3){if(date_voice_switching)return;date_voice_switching=true;visible(date_editor_voice_switch,false);}
        voice_remote_command(action?action:1);return;
    }
    if(action)return;
    voice_remote_state *voice=&ui_voice_work->date_action;voice_remote_get(voice);if(voice->busy){ui_text(date_feedback,"语音正在使用，请稍后重试");return;}
    tab5_ime_finish_for(date_ime);if(tab5_ime_pending_for(date_ime)[0]){ui_text(date_feedback,"请先完成名称输入");return;}
    int64_t age=esp_timer_get_time()/1000-last_received;const char *session=text(item(shown,"pcInput"),"session");
    if(age<0||age>6000||!session[0]||!strcmp(session,"--")){ui_text(date_feedback,"语音识别需连接电脑；离线可使用键盘输入");return;}
    const char *base=lv_textarea_get_text(date_title_input);if(strlen(base)>96){ui_text(date_feedback,"请先缩短当前名称，再继续录音");return;}
    screen_preview_cache_reclaim(true);
    if(!voice_remote_start_tab5(DATE_VOICE_OWNER,session,(int64_t)num(shown,"epochMilliseconds")+age)){ui_text(date_feedback,"语音未启动，请检查连接和电脑语音设置");return;}
    voice_remote_get(voice);date_voice_generation=voice->generation;date_voice_waiting=true;date_voice_cancelled=false;date_voice_started=0;date_voice_switching=false;
    strcpy(date_voice_base,base);date_voice_cursor=lv_textarea_get_cursor_pos(date_title_input);date_voice_byte=lv_text_encoded_get_byte_id(base,date_voice_cursor);
    date_keyboard_hide(NULL);
    // Keep the input layout and stop target stationary throughout the take.
    date_voice_lock_keys(date_keyboard,true);date_voice_lock_keys(date_ime,true);lv_obj_add_state(date_title_input,LV_STATE_DISABLED);lv_obj_add_state(date_editor_save_button,LV_STATE_DISABLED);tint(lv_obj_get_child(date_editor_save_button,0),MUTED);visible(date_editor_voice_cancel,true);lv_obj_add_state(date_editor_voice_button,LV_STATE_DISABLED);ui_bg_color(date_editor_voice_button,lv_color_hex(0x534C39),0);tint(date_feedback,AMBER);ui_text(date_feedback,"正在准备，请稍候…");
}
static void date_editor_voice_tick(void) {
    if(!date_voice_waiting)return;
    voice_remote_state *voice=&ui_voice_work->date_tick;voice_remote_get(voice);
    bool owner=voice->generation==date_voice_generation&&!strcmp(voice->task_id,DATE_VOICE_OWNER);
    if(!owner||!voice->busy||!strcmp(voice->source,"tab5"))date_voice_switching=false;
    bool recording=owner&&voice->busy&&voice->ready&&!strcmp(voice->phase,"recording")&&!date_voice_switching;
    visible(date_editor_voice_switch,recording&&!strcmp(voice->source,"dji"));
    visible(date_editor_mic_glyph,!recording);visible(date_editor_stop_glyph,recording);visible(date_editor_voice_meter,recording);lv_bar_set_value(date_editor_voice_meter,voice->level,LV_ANIM_OFF);
    ui_bg_color(date_editor_voice_button,lv_color_hex(recording?0x365D48:voice->busy&&owner?0x534C39:0x354247),0);
    if(voice->busy&&owner) {
        if(recording) {
            if(!date_voice_started)date_voice_started=esp_timer_get_time()/1000;
            int seconds=(int)((esp_timer_get_time()/1000-date_voice_started)/1000);
            const char *source=voice->source_name[0]?voice->source_name:!strcmp(voice->source,"dji")?"大疆":"TAB5 内置麦克风";
            ui_text_fmt(date_feedback,voice->silent?"未检测到声音  %02d:%02d\n%s":"可以说话了  %02d:%02d\n%s",seconds/60,seconds%60,source);
        } else if(date_voice_switching)ui_text(date_feedback,"正在切换 TAB5 内置麦克风…");
        else if(!strcmp(voice->phase,"draining"))ui_text(date_feedback,"正在结束收音…");
        else if(!strcmp(voice->phase,"recognizing"))ui_text(date_feedback,"正在识别，请稍候…");
        else ui_text(date_feedback,voice->message[0]?voice->message:"正在准备，请稍候…");
        tint(date_feedback,recording&&!voice->silent?GREEN:AMBER);
        if(recording)lv_obj_remove_state(date_editor_voice_button,LV_STATE_DISABLED);else lv_obj_add_state(date_editor_voice_button,LV_STATE_DISABLED);return;
    }
    tint(date_feedback,owner&&!strcmp(voice->phase,"error")?AMBER:MUTED);
    date_voice_waiting=false;visible(date_editor_voice_cancel,false);date_voice_lock_keys(date_keyboard,false);date_voice_lock_keys(date_ime,false);lv_obj_remove_state(date_editor_voice_button,LV_STATE_DISABLED);lv_obj_remove_state(date_title_input,LV_STATE_DISABLED);lv_obj_remove_state(date_editor_save_button,LV_STATE_DISABLED);tint(lv_obj_get_child(date_editor_save_button,0),0x173642);
    if(owner&&!date_voice_cancelled&&!strcmp(voice->phase,"review")&&voice->can_insert&&voice->text[0]) {
        size_t base=strlen(date_voice_base),extra=strlen(voice->text);
        // Keep the entire recognized draft for editing; only persisted titles are limited to 10 characters.
        char *joined=ui_voice_work->date_joined;
        memcpy(joined,date_voice_base,date_voice_byte);memcpy(joined+date_voice_byte,voice->text,extra);memcpy(joined+date_voice_byte+extra,date_voice_base+date_voice_byte,base-date_voice_byte+1);
        lv_textarea_set_text(date_title_input,joined);lv_textarea_set_cursor_pos(date_title_input,date_voice_cursor+lv_text_get_encoded_length(voice->text));
        date_title_changed(NULL);
        if(lv_text_get_encoded_length(joined)<=10)ui_text(date_feedback,"已填入名称，请检查后保存");
        return;
    }
    ui_text(date_feedback,date_voice_cancelled||!strcmp(voice->phase,"cancelled")?"已取消，原名称已保留":owner&&voice->message[0]?voice->message:"语音会话已变化，原名称已保留");
    date_title_changed(NULL);
}
