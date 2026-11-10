static void check_voice_source_labels(void) {
    page(8);lv_anim_delete_all();
    strcpy(codex_selected,"00000000-0000-4000-8000-000000000111");
    codex_voice_desktop=quick_visible=false;codex_voice_waiting=true;
    preview_voice=(voice_remote_state){.busy=true,.ready=true};
    strcpy(preview_voice.phase,"recording");strcpy(preview_voice.task_id,codex_selected);
    const char *sources[]={"tab5","pc","pc"};
    const char *names[]={"TAB5 内置麦克风 · 豆包输入法","Mac 麦克风（MacBook Pro 麦克风） · 豆包输入法","DJI 大疆麦克风（DJI MIC MINI） · 豆包输入法"};
    const char *captures[]={"voice-source-tab5","voice-source-mac","voice-source-dji"};
    for(unsigned i=0;i<3;i++) {
        strcpy(preview_voice.source,sources[i]);strcpy(preview_voice.source_name,names[i]);
        codex_voice_tick();assert(strstr(lv_label_get_text(codex_voice_hint),names[i]));
        assert(lv_obj_get_style_text_font(codex_voice_hint,0)==&tab5_font_24);
        capture(captures[i]);
    }
    preview_voice.source_name[0]=0;
    const char *legacy[]={"tab5","pc","dji"};
    const char *labels[]={"TAB5 内置麦克风","Mac 麦克风","DJI 大疆麦克风"};
    for(unsigned i=0;i<3;i++) {
        strcpy(preview_voice.source,legacy[i]);codex_voice_tick();
        assert(strstr(lv_label_get_text(codex_voice_hint),labels[i]));
    }
    preview_voice.silent=true;codex_voice_tick();assert(strstr(lv_label_get_text(codex_voice_hint),"未检测到声音"));
    preview_voice.ready=false;codex_voice_tick();assert(!strcmp(lv_label_get_text(codex_voice_hint),"正在准备，请稍候…"));
    puts("VOICE_SOURCE_LABEL_OK named TAB5/Mac/DJI, legacy fallback, silence and preparing; real LVGL");
}
