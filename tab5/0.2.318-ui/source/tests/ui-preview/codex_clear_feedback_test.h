static void check_codex_clear_feedback(void) {
    lv_obj_t *outlined[]={codex_clear_button,codex_reply_button,codex_interrupt_button,
        codex_reply_latest,codex_reply_older,codex_reply_newer,codex_turn_older,codex_turn_newer,quick_cancel_button,
        composer_remove[0],composer_remove[1],composer_remove[2]};
    for(unsigned i=0;i<sizeof(outlined)/sizeof(outlined[0]);i++) {
        lv_obj_t *b=outlined[i];assert(b);lv_state_t state=lv_obj_get_state(b);
        lv_obj_remove_state(b,LV_STATE_PRESSED|LV_STATE_DISABLED);
        lv_opa_t normal=lv_obj_get_style_bg_opa(b,0);
        lv_obj_add_state(b,LV_STATE_PRESSED);fprintf(stderr,"button=%u alpha=%u\n",i,lv_obj_get_style_bg_opa(b,0));assert(lv_obj_get_style_bg_opa(b,0)==LV_OPA_COVER);
        lv_obj_remove_state(b,LV_STATE_PRESSED);assert(lv_obj_get_style_bg_opa(b,0)==normal);
        lv_obj_add_state(b,LV_STATE_DISABLED);lv_opa_t disabled=lv_obj_get_style_bg_opa(b,0);
        lv_obj_add_state(b,LV_STATE_PRESSED);fprintf(stderr,"button=%u alpha=%u\n",i,lv_obj_get_style_bg_opa(b,0));assert(lv_obj_get_style_bg_opa(b,0)==disabled);
        lv_obj_remove_state(b,LV_STATE_PRESSED|LV_STATE_DISABLED);lv_obj_add_state(b,state);
    }
    puts("OUTLINED_BUTTON_FEEDBACK_OK twelve production controls; press, release and disabled precedence");
    page(8);lv_anim_delete_all();
    strcpy(codex_selected,"00000000-0000-4000-8000-000000000111");
    codex_waiting=codex_voice_waiting=codex_send_queued=false;
    lv_obj_remove_state(codex_input,LV_STATE_DISABLED);
    codex_set_text("测试清空按钮反馈");assert(codex_save_draft());codex_control_style();
    lv_obj_update_layout(lv_screen_active());
    lv_indev_t *touch=lv_indev_create();lv_indev_set_type(touch,LV_INDEV_TYPE_POINTER);
    lv_indev_set_display(touch,lv_display_get_default());lv_indev_set_read_cb(touch,camera_touch_read);
    lv_area_t area;lv_obj_get_coords(codex_clear_button,&area);
    camera_touch_point=(lv_point_t){(area.x1+area.x2)/2,(area.y1+area.y2)/2};
    assert(lv_obj_get_style_bg_opa(codex_clear_button,0)==LV_OPA_TRANSP);
    capture("clear-feedback-idle");
    camera_touch_state=LV_INDEV_STATE_PRESSED;lv_indev_read(touch);lv_timer_handler();
    lv_tick_inc(100);lv_indev_read(touch);lv_timer_handler();
    assert(lv_obj_has_state(codex_clear_button,LV_STATE_PRESSED));
    printf("CLEAR_FEEDBACK_PRESSED alpha=%u\n",lv_obj_get_style_bg_opa(codex_clear_button,0));
    assert(lv_obj_get_style_bg_opa(codex_clear_button,0)==LV_OPA_COVER);
    assert(lv_textarea_get_text(codex_input)[0]);capture("clear-feedback-pressed");
    camera_touch_state=LV_INDEV_STATE_RELEASED;lv_indev_read(touch);lv_timer_handler();
    assert(lv_obj_get_style_bg_opa(codex_clear_button,0)==LV_OPA_TRANSP&&lv_textarea_get_text(codex_input)[0]);
    camera_touch_state=LV_INDEV_STATE_PRESSED;lv_indev_read(touch);lv_timer_handler();
    for(int i=0;i<20;i++){lv_tick_inc(50);lv_indev_read(touch);lv_timer_handler();}
    assert(!lv_textarea_get_text(codex_input)[0]);
    camera_touch_state=LV_INDEV_STATE_RELEASED;lv_indev_read(touch);lv_timer_handler();
    assert(lv_obj_get_style_bg_opa(codex_clear_button,0)==LV_OPA_TRANSP);
    assert(!strcmp(lv_label_get_text(codex_voice_hint),"文字已清空"));capture("clear-feedback-cleared");
    lv_indev_reset(touch,NULL);lv_indev_delete(touch);
    puts("CODEX_CLEAR_FEEDBACK_OK visible hold, short press preserves, long press clears, release restores outline");
}
