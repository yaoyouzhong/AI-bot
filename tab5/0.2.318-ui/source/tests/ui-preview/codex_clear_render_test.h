static uint16_t *clear_empty_frame;
static unsigned clear_checkpoint_frames;
static void clear_checkpoint_frame(const ui_codex_checkpoint *value) {
    if(value->text[0])return;
    lv_area_t area;lv_obj_get_coords(codex_input,&area);unsigned differences=0;
    // Exclude the border and the first caret column; compare actual placeholder
    // and erased text pixels before entering potentially slow storage code.
    for(int y=area.y1+4;y<area.y2-4;y++)for(int x=area.x1+22;x<area.x2-4;x++)
        differences+=camera_partial_pixels[y*1280+x]!=clear_empty_frame[y*1280+x];
    printf("CLEAR_BEFORE_STORAGE differences=%u\n",differences);assert(!differences);
    if(!clear_checkpoint_frames) {
        FILE *file=fopen("clear-empty-before-storage.ppm","wb");assert(file);fprintf(file,"P6\n1280 720\n255\n");
        for(int n=0;n<1280*720;n++){uint16_t v=camera_partial_pixels[n];uint8_t rgb[]={(uint8_t)(((v>>11)&31)*255/31),(uint8_t)(((v>>5)&63)*255/63),(uint8_t)((v&31)*255/31)};fwrite(rgb,1,3,file);}fclose(file);
    }
    clear_checkpoint_frames++;
}
// Incremental framebuffer validation: no forced redraw between the long press
// and the captured result. A later full render is only the reference.
static void check_codex_clear_render(void) {
    extern bool (*tab5_lvgl_cache_draw)(lv_layer_t *,lv_obj_t *);
    page(8);lv_anim_delete_all();
    strcpy(codex_selected,"00000000-0000-4000-8000-000000000111");
    codex_waiting=codex_voice_waiting=codex_send_queued=false;
    lv_obj_remove_state(codex_input,LV_STATE_DISABLED);
    lv_display_t *display=lv_display_get_default();
    lv_indev_t *touch=lv_indev_create();lv_indev_set_type(touch,LV_INDEV_TYPE_POINTER);
    lv_indev_set_display(touch,display);lv_indev_set_read_cb(touch,camera_touch_read);
    camera_partial_pixels=malloc(sizeof(preview_buffer));assert(camera_partial_pixels);
    clear_empty_frame=malloc(sizeof(preview_buffer));assert(clear_empty_frame);
    for(int voice=0;voice<2;voice++)for(int keyboard=0;keyboard<2;keyboard++)for(int long_text=0;long_text<2;long_text++)for(int cursor=0;cursor<2;cursor++) {
        tab5_draw_cache_drop();
        if(keyboard)codex_input_focus(NULL);else codex_keyboard_done(NULL);
        codex_set_text("");lv_obj_scroll_to(codex_input,0,0,LV_ANIM_OFF);
        lv_obj_remove_state(codex_input,LV_STATE_FOCUSED);lv_anim_delete_all();
        lv_display_set_flush_cb(display,preview_flush);
        lv_display_set_buffers(display,preview_buffer,NULL,sizeof(preview_buffer),LV_DISPLAY_RENDER_MODE_FULL);
        lv_obj_update_layout(lv_screen_active());lv_obj_invalidate(lv_screen_active());lv_refr_now(display);
        memcpy(clear_empty_frame,preview_buffer,sizeof(preview_buffer));
        char text[2001]="待清空的内容";
        if(long_text)for(int i=0;i<45;i++)strcat(text,"\n长段落滚动后清空");
        if(voice) {
            codex_voice_desktop=false;codex_voice_waiting=true;
            codex_voice_base[0]=0;codex_voice_cursor=codex_voice_insert_byte=0;
            preview_voice=(voice_remote_state){.busy=true,.ready=true};
            strcpy(preview_voice.phase,"recording");strcpy(preview_voice.task_id,codex_selected);
            strcpy(preview_voice.text,text);codex_voice_tick();
            preview_voice.busy=false;strcpy(preview_voice.phase,"done");codex_voice_tick();
        } else codex_set_text(text);
        assert(codex_save_draft());
        lv_textarea_set_cursor_pos(codex_input,cursor?0:LV_TEXTAREA_CURSOR_LAST);
        codex_control_style();ui_text(codex_voice_hint,"");lv_anim_delete_all();
        lv_display_set_flush_cb(display,preview_flush);
        lv_display_set_buffers(display,preview_buffer,NULL,sizeof(preview_buffer),LV_DISPLAY_RENDER_MODE_FULL);
        lv_obj_update_layout(lv_screen_active());lv_obj_invalidate(lv_screen_active());lv_refr_now(display);
        if(!keyboard)for(int n=0;n<30&&!tab5_draw_cache_is_ready(pages[8]);n++)tab5_draw_cache_step(pages[8]);
        memcpy(camera_partial_pixels,preview_buffer,sizeof(preview_buffer));
        lv_display_set_flush_cb(display,camera_partial_flush);
        lv_display_set_buffers(display,preview_buffer,NULL,TAB5_DRAW_BUFFER_PIXELS*2,LV_DISPLAY_RENDER_MODE_PARTIAL);
        lv_area_t button;lv_obj_get_coords(codex_clear_button,&button);
        camera_touch_point=(lv_point_t){(button.x1+button.x2)/2,(button.y1+button.y2)/2};
        preview_checkpoint_observer=clear_checkpoint_frame;
        camera_touch_state=LV_INDEV_STATE_PRESSED;lv_indev_read(touch);lv_timer_handler();
        for(int n=0;n<20;n++){lv_tick_inc(50);lv_indev_read(touch);lv_timer_handler();}
        camera_touch_state=LV_INDEV_STATE_RELEASED;lv_indev_read(touch);lv_tick_inc(40);lv_timer_handler();
        preview_checkpoint_observer=NULL;
        assert(!lv_textarea_get_text(codex_input)[0]);
        codex_draft *d=codex_draft_for(codex_selected,false);assert(!d->text||!d->text[0]);
        bool (*draw)(lv_layer_t *,lv_obj_t *)=tab5_lvgl_cache_draw;tab5_lvgl_cache_draw=NULL;
        lv_display_set_flush_cb(display,preview_flush);
        lv_display_set_buffers(display,preview_buffer,NULL,sizeof(preview_buffer),LV_DISPLAY_RENDER_MODE_FULL);
        lv_obj_invalidate(lv_screen_active());lv_refr_now(display);tab5_lvgl_cache_draw=draw;
        unsigned differences=0;lv_area_t input;lv_obj_get_coords(codex_input,&input);
        for(int y=input.y1+3;y<input.y2-3;y++)for(int x=input.x1+3;x<input.x2-3;x++)
            differences+=camera_partial_pixels[y*1280+x]!=preview_buffer[y*1280+x];
        printf("CLEAR_RENDER voice=%d keyboard=%d long=%d cursor_start=%d differences=%u\n",voice,keyboard,long_text,cursor,differences);
        assert(!differences);
    }
    assert(clear_checkpoint_frames==16);free(clear_empty_frame);clear_empty_frame=NULL;
    free(camera_partial_pixels);camera_partial_pixels=NULL;
    codex_input_focus(NULL);codex_set_text("清空前内容");assert(codex_save_draft());
    codex_draft *draft=codex_draft_for(codex_selected,false);assert(draft);
    strcpy(draft->images[0],"d0000000-0000-4000-8000-000000000001");
    type_spelling("nihao");unsigned revision=draft->revision;
    lv_obj_send_event(codex_clear_button,LV_EVENT_CLICKED,NULL);
    assert(!strcmp(lv_textarea_get_text(codex_input),"清空前内容")&&tab5_ime_pending()[0]&&draft->revision==revision);
    codex_voice_waiting=true;lv_obj_send_event(codex_clear_button,LV_EVENT_LONG_PRESSED,NULL);
    assert(lv_textarea_get_text(codex_input)[0]);codex_voice_waiting=false;
    codex_send_queued=true;lv_obj_send_event(codex_clear_button,LV_EVENT_LONG_PRESSED,NULL);
    assert(lv_textarea_get_text(codex_input)[0]);codex_send_queued=false;
    strcpy(draft->request,"d0000000-0000-4000-8000-000000000002");
    lv_obj_send_event(codex_clear_button,LV_EVENT_LONG_PRESSED,NULL);
    assert(lv_textarea_get_text(codex_input)[0]);draft->request[0]=0;
    lv_obj_send_event(codex_clear_button,LV_EVENT_LONG_PRESSED,NULL);
    assert(!lv_textarea_get_text(codex_input)[0]&&!tab5_ime_pending()[0]);
    assert(draft->dirty&&draft->loaded&&draft->revision>revision&&draft->images[0][0]&&!preview_checkpoint.text[0]);
    type_spelling("nihao");choose_word("你好");
    lv_obj_send_event(codex_clear_button,LV_EVENT_LONG_PRESSED_REPEAT,NULL);
    lv_obj_send_event(codex_clear_button,LV_EVENT_RELEASED,NULL);
    lv_obj_send_event(codex_clear_button,LV_EVENT_CLICKED,NULL);
    assert(!strcmp(lv_textarea_get_text(codex_input),"你好"));
    assert(codex_save_draft());preview_checkpoint_ok=false;lv_obj_send_event(codex_clear_button,LV_EVENT_LONG_PRESSED,NULL);
    assert(!lv_textarea_get_text(codex_input)[0]&&codex_local_dirty&&strstr(lv_label_get_text(codex_voice_hint),"保存失败"));
    preview_checkpoint_ok=true;assert(codex_checkpoint_write());
    puts("CODEX_CLEAR_GUARDS_OK short press, busy/send/receipt guards, IME, photos, revision, retype and failed persistence retry");

    lv_indev_reset(touch,NULL);lv_indev_delete(touch);
    puts("CODEX_CLEAR_RENDER_OK actual pointer long-press; incremental pixels match empty reference in 16 scenarios with normal timer refresh");
}
