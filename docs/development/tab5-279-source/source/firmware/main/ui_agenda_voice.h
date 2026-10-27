// The voice service treats taskId as an opaque owner; this owner never sends a Codex task.
#define AGENDA_VOICE_OWNER "a1b07a65-0000-4000-8000-00000000a5ed"
static char agenda_voice_base[97];
static unsigned agenda_voice_generation,agenda_voice_byte;
static int agenda_voice_cursor;
static int64_t agenda_voice_started;
static bool agenda_voice_switching;
static void agenda_voice_lock_keys(lv_obj_t *obj,bool locked) {
    if(locked)lv_obj_add_state(obj,LV_STATE_DISABLED);else lv_obj_remove_state(obj,LV_STATE_DISABLED);
    for(uint32_t n=0;n<lv_obj_get_child_count(obj);n++)agenda_voice_lock_keys(lv_obj_get_child(obj,n),locked);
}
static void agenda_editor_voice_action(lv_event_t *event) {
    int action=(int)(intptr_t)lv_event_get_user_data(event);
    if(agenda_voice_waiting){
        if(action==2)agenda_voice_cancelled=true;
        if(action==3){if(agenda_voice_switching)return;agenda_voice_switching=true;visible(agenda_editor_voice_switch,false);}
        voice_remote_command(action?action:1);return;
    }
    if(action||agenda_pending_request)return;
    voice_remote_state *voice=&ui_voice_work->agenda_action;voice_remote_get(voice);if(voice->busy){ui_text(agenda_editor_feedback,"语音正在使用，请稍后重试");return;}
    tab5_ime_finish_for(agenda_editor_ime);if(tab5_ime_pending_for(agenda_editor_ime)[0]){ui_text(agenda_editor_feedback,"请先完成名称输入");return;}
    int64_t age=esp_timer_get_time()/1000-last_received;const char *session=text(item(shown,"pcInput"),"session");
    if(age<0||age>6000||!session[0]||!strcmp(session,"--")){ui_text(agenda_editor_feedback,"语音识别需连接电脑；离线可使用键盘输入");return;}
    const char *base=lv_textarea_get_text(agenda_editor_name);if(strlen(base)>96)return;
    screen_preview_cache_reclaim(true);
    if(!voice_remote_start_tab5(AGENDA_VOICE_OWNER,session,(int64_t)num(shown,"epochMilliseconds")+age)){ui_text(agenda_editor_feedback,"语音未启动，请检查连接和电脑语音设置");return;}
    voice_remote_get(voice);agenda_voice_generation=voice->generation;agenda_voice_waiting=true;agenda_voice_cancelled=false;agenda_voice_started=0;agenda_voice_switching=false;
    agenda_editor_feedback_layout();
    strcpy(agenda_voice_base,base);agenda_voice_cursor=lv_textarea_get_cursor_pos(agenda_editor_name);agenda_voice_byte=lv_text_encoded_get_byte_id(base,agenda_voice_cursor);
    // Keep the input layout and stop target stationary throughout the take.
    agenda_voice_lock_keys(agenda_editor_keyboard,true);agenda_voice_lock_keys(agenda_editor_ime,true);lv_obj_add_state(agenda_editor_name,LV_STATE_DISABLED);lv_obj_add_state(agenda_editor_save_button,LV_STATE_DISABLED);tint(lv_obj_get_child(agenda_editor_save_button,0),MUTED);visible(agenda_editor_voice_cancel,true);lv_obj_add_state(agenda_editor_voice_button,LV_STATE_DISABLED);ui_bg_color(agenda_editor_voice_button,lv_color_hex(0x534C39),0);tint(agenda_editor_feedback,AMBER);ui_text(agenda_editor_feedback,"正在准备，请稍候…");
}
static void agenda_editor_voice_tick(void) {
    if(!agenda_voice_waiting)return;
    voice_remote_state *voice=&ui_voice_work->agenda_tick;voice_remote_get(voice);
    bool owner=voice->generation==agenda_voice_generation&&!strcmp(voice->task_id,AGENDA_VOICE_OWNER);
    if(!owner||!voice->busy||!strcmp(voice->source,"tab5"))agenda_voice_switching=false;
    bool recording=owner&&voice->busy&&voice->ready&&!strcmp(voice->phase,"recording")&&!agenda_voice_switching;
    visible(agenda_editor_voice_switch,recording&&!strcmp(voice->source,"dji"));
    visible(agenda_editor_mic_glyph,!recording);visible(agenda_editor_stop_glyph,recording);visible(agenda_editor_voice_meter,recording);lv_bar_set_value(agenda_editor_voice_meter,voice->level,LV_ANIM_OFF);
    ui_bg_color(agenda_editor_voice_button,lv_color_hex(recording?0x365D48:voice->busy&&owner?0x534C39:0x354247),0);
    if(voice->busy&&owner) {
        if(recording) {
            if(!agenda_voice_started)agenda_voice_started=esp_timer_get_time()/1000;
            int seconds=(int)((esp_timer_get_time()/1000-agenda_voice_started)/1000);
            const char *source=voice->source_name[0]?voice->source_name:!strcmp(voice->source,"dji")?"大疆":"TAB5 内置麦克风";
            ui_text_fmt(agenda_editor_feedback,voice->silent?"未检测到声音  %02d:%02d\n%s":"可以说话了  %02d:%02d\n%s",seconds/60,seconds%60,source);
        } else if(agenda_voice_switching)ui_text(agenda_editor_feedback,"正在切换 TAB5 内置麦克风…");
        else if(!strcmp(voice->phase,"draining"))ui_text(agenda_editor_feedback,"正在结束收音…");
        else if(!strcmp(voice->phase,"recognizing"))ui_text(agenda_editor_feedback,"正在识别，请稍候…");
        else ui_text(agenda_editor_feedback,voice->message[0]?voice->message:"正在准备，请稍候…");
        tint(agenda_editor_feedback,recording&&!voice->silent?GREEN:AMBER);
        if(recording)lv_obj_remove_state(agenda_editor_voice_button,LV_STATE_DISABLED);else lv_obj_add_state(agenda_editor_voice_button,LV_STATE_DISABLED);return;
    }
    tint(agenda_editor_feedback,owner&&!strcmp(voice->phase,"error")?AMBER:MUTED);
    agenda_voice_waiting=false;visible(agenda_editor_voice_cancel,false);agenda_voice_lock_keys(agenda_editor_keyboard,false);agenda_voice_lock_keys(agenda_editor_ime,false);lv_obj_remove_state(agenda_editor_voice_button,LV_STATE_DISABLED);lv_obj_remove_state(agenda_editor_name,LV_STATE_DISABLED);lv_obj_remove_state(agenda_editor_save_button,LV_STATE_DISABLED);tint(lv_obj_get_child(agenda_editor_save_button,0),0x173642);
    agenda_editor_feedback_layout();
    if(owner&&!agenda_voice_cancelled&&!strcmp(voice->phase,"review")&&voice->can_insert&&voice->text[0]) {
        size_t base=strlen(agenda_voice_base);
        unsigned base_chars=lv_text_get_encoded_length(agenda_voice_base),spoken=lv_text_get_encoded_length(voice->text);
        unsigned inserted=base_chars<32?LV_MIN(spoken,32-base_chars):0;
        size_t extra=lv_text_encoded_get_byte_id(voice->text,inserted);
        // Keep the existing prefix/suffix and cut only the new speech at a complete
        // UTF-8 character. The persisted title also has a 96-byte storage bound.
        while(inserted&&base+extra>96)extra=lv_text_encoded_get_byte_id(voice->text,--inserted);
        if(!inserted){ui_text(agenda_editor_feedback,"名称已满，请删减后再录音");return;}
        char joined[97];memcpy(joined,agenda_voice_base,agenda_voice_byte);memcpy(joined+agenda_voice_byte,voice->text,extra);memcpy(joined+agenda_voice_byte+extra,agenda_voice_base+agenda_voice_byte,base-agenda_voice_byte+1);
        lv_textarea_set_text(agenda_editor_name,joined);lv_textarea_set_cursor_pos(agenda_editor_name,agenda_voice_cursor+inserted);
        ui_text(agenda_editor_feedback,inserted==spoken?"已填入名称，请检查后保存":base_chars+inserted==32?"已保留 32 字，请检查后保存":"已截断并填入，请检查后保存");return;
    }
    ui_text(agenda_editor_feedback,agenda_voice_cancelled||!strcmp(voice->phase,"cancelled")?"已取消，原名称已保留":owner&&voice->message[0]?voice->message:"语音会话已变化，原名称已保留");
}
