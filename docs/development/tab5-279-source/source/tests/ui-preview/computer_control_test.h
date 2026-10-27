{
    int64_t preparation_saved_clock=preview_now_us;
    // A fake clock plus independent fresh status models normal background polls.
    #define PREP_ADVANCE(ms) do {preview_now_us+=(int64_t)(ms)*1000;preview_computer.updated=preview_computer.lock_updated=preview_now_us/1000;last_received=preview_now_us/1000;} while(0)
    #define PREP_READ() do {unlock_refresh();unlock_refresh();} while(0)
    #define PREP_READY() do {if(preview_display>0&&preview_cover_complete&&preview_keyboard.ready)for(int ready_step=0;ready_step<8&&unlock_preparing&&!preview_keyboard.busy;ready_step++){PREP_ADVANCE(200);PREP_READ();}} while(0)
    int64_t saved_received=last_received;bool saved_wifi=wifi_connected;
    cJSON *health=item(shown,"connectionHealth");bool created=!health;
    if(created){health=cJSON_CreateObject();cJSON_AddItemToObject(shown,"connectionHealth",health);}
    cJSON_AddNumberToObject(health,"computerControlVersion",1);
    cJSON_AddNumberToObject(health,"unlockTrialVersion",1);
    wifi_connected=true;last_received=esp_timer_get_time()/1000;
    preview_computer=(computer_remote_state){.ready=true,.lock_known=true,.volume_available=true,.can_lock=true,.media_available=true,.can_play_pause=true,.can_previous=true,.can_next=true,.playing=true,.volume=38,.count=2,.updated=last_received,.lock_updated=last_received};
    strcpy(preview_computer.launchers[0].id,"11111111-1111-4111-8111-111111111111");strcpy(preview_computer.launchers[0].name,"记事本");
    strcpy(preview_computer.launchers[1].id,"22222222-2222-4222-8222-222222222222");strcpy(preview_computer.launchers[1].name,"工作文件夹");
    preview_computer.launchers[1].folder=true;
    const char *icon_directory=getenv("TAB5_LAUNCHER_ICON_DIR");
    if(icon_directory)for(int i=0;i<2;i++) {
        char filename[768];snprintf(filename,sizeof(filename),"%s/launcher-icon-%d.bgra",icon_directory,i);
        FILE *file=fopen(filename,"rb");assert(file);
        assert(fread(preview_computer.launchers[i].icon,1,4096,file)==4096);fclose(file);preview_computer.launchers[i].has_icon=true;
    }

    const char *visual_state=getenv("TAB5_UNLOCK_VISUAL_STATE");
    if(visual_state) {
        // The device flushes partial strips and consumes native cached tiles.
        // FULL preview_buffer alone omits those tiles after a state change.
        camera_partial_pixels=calloc(1,sizeof(preview_buffer));assert(camera_partial_pixels);
        lv_display_t *visual_display=lv_display_get_default();
        lv_display_set_flush_cb(visual_display,camera_partial_flush);
        lv_display_set_buffers(visual_display,preview_buffer,NULL,TAB5_DRAW_BUFFER_PIXELS*2,LV_DISPLAY_RENDER_MODE_PARTIAL);
        preview_computer.locked=true;preview_computer.can_lock=false;page(14);computer_refresh();unlock_open();PREP_READY();
        if(!strcmp(visual_state,"closed")) {preview_computer.locked=false;PREP_ADVANCE(1);unlock_refresh();assert(unlock_overlay&&unlock_success_until);PREP_ADVANCE(500);unlock_refresh();assert(!unlock_overlay);computer_refresh();}
        else if(!strcmp(visual_state,"off"))preview_display=0;
        else if(!strcmp(visual_state,"dim"))preview_display=2;
        else if(!strcmp(visual_state,"unknown"))preview_display=-1;
        else if(!strcmp(visual_state,"submit")||!strcmp(visual_state,"retry")) {
            for(int i=0;i<4;i++)lv_obj_send_event(unlock_keys[i],LV_EVENT_CLICKED,NULL);
            if(!strcmp(visual_state,"retry"))preview_display=-1;else preview_cover_complete=false;
            lv_obj_send_event(unlock_confirm,LV_EVENT_CLICKED,NULL);
        }
        unlock_refresh();lv_tick_inc(400);lv_timer_handler();
        char visual_name[80];snprintf(visual_name,sizeof(visual_name),!strcmp(visual_state,"closed")?"computer-auto-close-%s":"computer-unlock-state-%s",visual_state);capture(visual_name);
        puts("UNLOCK_STATE_FRESH_PROCESS_OK actual_display_buffer_simulation");return 0;
    }
    page(11);capture("apps-with-control");page(14);computer_refresh();capture("computer-control");
    assert(!lv_obj_has_state(computer_slider,LV_STATE_DISABLED));
    assert(!strcmp(lv_label_get_text(computer_volume),"38%"));
    preview_computer.volume=0;computer_refresh();capture("computer-volume-zero");
    preview_computer.volume=100;computer_refresh();capture("computer-volume-full");
    preview_computer.volume=38;computer_refresh();
    assert(lv_obj_has_state(computer_launch[2],LV_STATE_DISABLED));
    lv_tick_inc(400);lv_timer_handler();computer_refresh();lv_obj_update_layout(lv_screen_active());lv_refr_now(NULL);
    computer_preview_damage_count=0;
    lv_display_add_event_cb(lv_display_get_default(),computer_preview_damage,LV_EVENT_INVALIDATE_AREA,NULL);
    for(int i=0;i<8;i++){preview_computer.busy=(i%2)!=0;computer_refresh();lv_obj_update_layout(lv_screen_active());}
    assert(computer_preview_damage_count==0); // Read-only polls must not flash enabled/disabled or relayout unchanged rows.
    lv_display_remove_event_cb_with_user_data(lv_display_get_default(),computer_preview_damage,NULL);
    preview_computer.busy=false;computer_refresh();
    // A status timeout while a pointer owns the slider disables it. LVGL then
    // omits RELEASED; control polling must recover without a page/device reset.
    {
        int64_t saved_now=preview_now_us;
        lv_indev_t *touch=lv_indev_create();lv_indev_set_type(touch,LV_INDEV_TYPE_POINTER);
        lv_indev_set_display(touch,lv_display_get_default());lv_indev_set_read_cb(touch,camera_touch_read);
        lv_obj_update_layout(lv_screen_active());lv_area_t area;lv_obj_get_coords(computer_slider,&area);
        camera_touch_point=(lv_point_t){(area.x1+area.x2)/2,(area.y1+area.y2)/2};
        camera_touch_state=LV_INDEV_STATE_PRESSED;lv_indev_read(touch);assert(computer_adjusting);
        preview_now_us+=7000000;last_received=esp_timer_get_time()/1000;
        computer_message_until=0;computer_poll_at=last_received-2001;
        unsigned before=preview_computer_commands;computer_refresh();
        camera_touch_state=LV_INDEV_STATE_RELEASED;lv_indev_read(touch);computer_refresh();
        printf("COMPUTER_STALE_DRAG adjusting=%d disabled=%d fresh=%d\n",computer_adjusting,lv_obj_has_state(computer_slider,LV_STATE_DISABLED),computer_ready());fflush(stdout);
        assert(!computer_adjusting&&!lv_obj_has_state(computer_slider,LV_STATE_DISABLED));
        assert(preview_computer_commands==before); // Recovery never replays volume.
        lv_indev_delete(touch);preview_now_us=saved_now;last_received=saved_now/1000;
        preview_computer.updated=last_received;computer_poll_at=0;computer_refresh();
        unsigned cancelled=preview_computer_commands;
        lv_obj_send_event(computer_slider,LV_EVENT_PRESSED,NULL);page(11);computer_refresh();
        lv_obj_send_event(computer_slider,LV_EVENT_RELEASED,NULL);assert(!computer_adjusting&&preview_computer_commands==cancelled);
        page(14);computer_refresh();
    }
    unsigned commands=preview_computer_commands;
    lv_obj_send_event(computer_slider,LV_EVENT_PRESSED,NULL);lv_slider_set_value(computer_slider,45,LV_ANIM_OFF);
    lv_obj_send_event(computer_slider,LV_EVENT_VALUE_CHANGED,NULL);assert(preview_computer_commands==commands);
    lv_obj_send_event(computer_slider,LV_EVENT_RELEASED,NULL);assert(preview_computer.volume==45&&preview_computer_commands==commands+1);
    computer_refresh();lv_obj_send_event(computer_mute,LV_EVENT_CLICKED,NULL);assert(preview_computer.muted);computer_refresh();
    assert(!strcmp(lv_label_get_text(computer_volume),LV_SYMBOL_MUTE));
    assert(!lv_obj_has_flag(computer_volume_slash,LV_OBJ_FLAG_HIDDEN));capture("computer-control-muted");
    lv_obj_send_event(computer_slider,LV_EVENT_PRESSED,NULL);lv_slider_set_value(computer_slider,50,LV_ANIM_OFF);
    lv_obj_send_event(computer_slider,LV_EVENT_VALUE_CHANGED,NULL);
    assert(!strcmp(lv_label_get_text(computer_volume),LV_SYMBOL_MUTE));
    lv_obj_send_event(computer_slider,LV_EVENT_RELEASED,NULL);computer_refresh();
    lv_obj_send_event(computer_mute,LV_EVENT_CLICKED,NULL);computer_refresh();
    assert(!preview_computer.muted&&!strcmp(lv_label_get_text(computer_volume),"50%"));
    assert(lv_obj_has_flag(computer_volume_slash,LV_OBJ_FLAG_HIDDEN));
    lv_obj_send_event(computer_launch[1],LV_EVENT_CLICKED,NULL);assert(!strcmp(preview_computer_op,"launch")&&!strcmp(preview_computer_target,preview_computer.launchers[1].id));
    lv_obj_send_event(computer_lock,LV_EVENT_CLICKED,NULL);assert(!strcmp(preview_computer_op,"lock"));
    preview_computer.locked=true;preview_computer.can_lock=false;computer_refresh();
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(computer_lock,0)),"解锁"));
    assert(strstr(lv_label_get_text(computer_status),"已锁屏")&&!lv_obj_has_state(computer_lock,LV_STATE_DISABLED));capture("computer-control-locked");
    lv_obj_send_event(computer_lock,LV_EVENT_CLICKED,NULL);assert(unlock_overlay&&unlock_length==0);PREP_READY();
    for(int i=0;i<4;i++)lv_obj_send_event(unlock_keys[i],LV_EVENT_CLICKED,NULL);
    assert(unlock_length==4&&!strcmp(lv_label_get_text(unlock_mask),"****"));capture("computer-unlock-pin");
    lv_obj_send_event(unlock_close_button,LV_EVENT_CLICKED,NULL);assert(!unlock_overlay&&unlock_length==0);
    unlock_open();PREP_READY();
    lv_obj_send_event(unlock_keys[0],LV_EVENT_CLICKED,NULL);
    assert(unlock_length==1&&!lv_obj_has_state(unlock_confirm,LV_STATE_DISABLED));
    unlock_input_key(-2);
    lv_obj_send_event(unlock_mode_button,LV_EVENT_CLICKED,NULL);assert(unlock_text_mode==1&&!lv_obj_has_flag(unlock_text_keys,LV_OBJ_FLAG_HIDDEN));
    lv_buttonmatrix_set_selected_button(unlock_text_keys,0);lv_obj_send_event(unlock_text_keys,LV_EVENT_VALUE_CHANGED,NULL);
    assert(unlock_length==1&&unlock_pin[0]=='q');capture("computer-unlock-lowercase");
    lv_obj_send_event(unlock_mode_button,LV_EVENT_CLICKED,NULL);assert(unlock_text_mode==2);
    lv_buttonmatrix_set_selected_button(unlock_text_keys,0);lv_obj_send_event(unlock_text_keys,LV_EVENT_VALUE_CHANGED,NULL);
    assert(unlock_length==2&&!memcmp(unlock_pin,"qQ",2));capture("computer-unlock-uppercase");
    lv_obj_send_event(unlock_mode_button,LV_EVENT_CLICKED,NULL);assert(unlock_text_mode==3);
    lv_buttonmatrix_set_selected_button(unlock_text_keys,0);lv_obj_send_event(unlock_text_keys,LV_EVENT_VALUE_CHANGED,NULL);
    lv_buttonmatrix_set_selected_button(unlock_text_keys,32);lv_obj_send_event(unlock_text_keys,LV_EVENT_VALUE_CHANGED,NULL);
    assert(unlock_length==4&&!memcmp(unlock_pin,"qQ! ",4)&&!strcmp(lv_label_get_text(unlock_mask),"****"));capture("computer-unlock-symbols");
    lv_buttonmatrix_set_selected_button(unlock_text_keys,34);lv_obj_send_event(unlock_text_keys,LV_EVENT_VALUE_CHANGED,NULL);assert(unlock_length==3);
    lv_buttonmatrix_set_selected_button(unlock_text_keys,33);lv_obj_send_event(unlock_text_keys,LV_EVENT_VALUE_CHANGED,NULL);assert(unlock_length==0);
    lv_obj_send_event(unlock_mode_button,LV_EVENT_CLICKED,NULL);assert(unlock_text_mode==0&&lv_obj_has_flag(unlock_text_keys,LV_OBJ_FLAG_HIDDEN));
    lv_obj_send_event(unlock_mode_button,LV_EVENT_CLICKED,NULL);assert(unlock_text_mode==1);
    lv_obj_send_event(unlock_close_button,LV_EVENT_CLICKED,NULL);assert(!unlock_overlay&&unlock_length==0);
    unlock_open();PREP_READY();assert(unlock_text_mode==0&&lv_obj_has_flag(unlock_text_keys,LV_OBJ_FLAG_HIDDEN)&&!lv_obj_has_flag(unlock_keys[0],LV_OBJ_FLAG_HIDDEN));capture("computer-unlock-default-numeric");
    lv_obj_send_event(unlock_close_button,LV_EVENT_CLICKED,NULL);assert(!unlock_overlay&&unlock_length==0);
    puts("UNLOCK_TEXT_UI_OK single_character_confirmation_lower_upper_symbols_space_backspace_clear_mode_cycle_and_mask");
    for(size_t i=0;i<sizeof(unlock_pin);i++)assert(unlock_pin[i]==0);
    unlock_open();PREP_READY();for(int i=0;i<4;i++)lv_obj_send_event(unlock_keys[i],LV_EVENT_CLICKED,NULL);
    unsigned submitted=preview_unlock_submissions;lv_obj_send_event(unlock_confirm,LV_EVENT_CLICKED,NULL);PREP_READY();
    assert(unlock_sent&&unlock_length==0&&preview_unlock_submissions==submitted+1);
    lv_obj_send_event(unlock_confirm,LV_EVENT_CLICKED,NULL);assert(preview_unlock_submissions==submitted+1);
    preview_keyboard.busy=false;preview_keyboard.finished=true;unlock_refresh();assert(!strcmp(lv_label_get_text(unlock_hint),"正在解锁，请稍候"));capture("computer-unlock-awaiting");preview_computer.locked=false;PREP_ADVANCE(1);unlock_refresh();assert(unlock_overlay&&unlock_success_until&&!unlock_length);PREP_ADVANCE(500);unlock_refresh();assert(!unlock_overlay);capture("computer-auto-close-success");unlock_close();preview_computer.locked=true;
    unlock_open();PREP_READY();lv_obj_send_event(unlock_keys[0],LV_EVENT_CLICKED,NULL);preview_keyboard.ready=false;unlock_refresh();
    assert(unlock_length==0&&lv_obj_has_state(unlock_confirm,LV_STATE_DISABLED));capture("computer-unlock-bluetooth-required");unlock_close();preview_keyboard.ready=true;
    unlock_open();PREP_READY();lv_obj_send_event(unlock_keys[0],LV_EVENT_CLICKED,NULL);unlock_deadline=esp_timer_get_time()/1000-1;unlock_refresh();assert(!unlock_overlay&&unlock_length==0);
    preview_keyboard.pairing=true;preview_keyboard.comparison=482617;unlock_refresh();assert(unlock_overlay&&unlock_pairing_mode);capture("computer-bluetooth-pairing");lv_obj_send_event(unlock_confirm,LV_EVENT_CLICKED,NULL);assert(!unlock_overlay&&!preview_keyboard.pairing);
    preview_keyboard.pairing=true;preview_keyboard.repair_pairing=true;unlock_refresh();capture("computer-bluetooth-repair");lv_obj_send_event(unlock_close_button,LV_EVENT_CLICKED,NULL);assert(!unlock_overlay);preview_keyboard.repair_pairing=false;
    // Legacy / unknown / expired display evidence never enables PIN submission.
    preview_keyboard.connected=preview_keyboard.secure=preview_keyboard.subscribed=preview_keyboard.bridge_verified=preview_keyboard.ready=true;
    unsigned wakes=preview_wake_submissions,display_wakes=preview_display_wakes,reads=preview_cover_reads;
    preview_cover=false;preview_display=1;unlock_open();PREP_READY();assert(unlock_prepared&&!unlock_preparing&&preview_wake_submissions==wakes&&preview_cover_reads==reads+4);
    for(int i=0;i<5;i++)unlock_refresh();assert(preview_cover_reads==reads+4&&preview_wake_submissions==wakes);unlock_close();
    preview_display=-1;unlock_open();PREP_READY();assert(unlock_preparing&&!unlock_prepared&&!unlock_prepare_failed&&preview_display_wakes==display_wakes);
    PREP_ADVANCE(12000);unlock_refresh();assert(unlock_prepare_failed&&unlock_failure==1&&!unlock_prepared&&preview_display_wakes==display_wakes);unlock_close();preview_display=1;
    preview_cover=true;preview_cover_complete=false;unlock_open();PREP_READY();assert(unlock_preparing==1);lv_tick_inc(400);lv_timer_handler();unlock_refresh();capture("computer-unlock-checking");
    lv_obj_send_event(unlock_keys[0],LV_EVENT_CLICKED,NULL);assert(!unlock_length);
    unlock_prepare_until=esp_timer_get_time()/1000-1;unlock_refresh();assert(unlock_preparing==4&&!unlock_prepared&&!unlock_prepare_failed&&preview_wake_submissions==wakes);
    PREP_ADVANCE(12000);unlock_refresh();assert(!unlock_preparing&&unlock_prepare_failed&&unlock_failure==1);
    preview_cover_complete=true;unlock_refresh();assert(preview_wake_submissions==wakes);unlock_close();
    // Off -> modifier-only wake -> On AND cover -> one Space -> fresh On -> editable.
    preview_cover=false;preview_display=0;unlock_open();PREP_READY();assert(unlock_preparing==2&&preview_display_wakes==display_wakes+1&&preview_wake_submissions==wakes);
    lv_tick_inc(400);lv_timer_handler();unlock_refresh();capture("computer-unlock-screen-waking");
    for(int i=0;i<5;i++)unlock_refresh();assert(preview_display_wakes==display_wakes+1);
    preview_keyboard.busy=false;preview_keyboard.finished=true;unlock_refresh();assert(unlock_preparing==3);
    PREP_ADVANCE(350);PREP_READ();assert(unlock_preparing==3&&preview_display_wakes==display_wakes+1); // No second Shift before the 1.5s settle.
    preview_display=1;preview_cover=true;unlock_prepare_until=esp_timer_get_time()/1000-1;unlock_refresh();unlock_refresh();
    assert(unlock_preparing==2&&preview_wake_submissions==wakes+1);capture("computer-unlock-waking");
    preview_cover=false;preview_keyboard.busy=false;preview_keyboard.finished=true;unlock_refresh();assert(unlock_preparing==3);
    unlock_prepare_until=esp_timer_get_time()/1000-1;unlock_refresh();unlock_refresh();assert(!unlock_preparing&&unlock_prepared&&unlock_woke);
    lv_tick_inc(400);lv_timer_handler();unlock_refresh();capture("computer-unlock-cover-dismissed");unlock_close();
    // An already lit PIN page does not receive either wake key.
    wakes=preview_wake_submissions;display_wakes=preview_display_wakes;
    unlock_open();PREP_READY();assert(unlock_prepared&&preview_wake_submissions==wakes&&preview_display_wakes==display_wakes);unlock_close();
    // Screen goes off after panel opening: confirm prepares first, sends PIN once only after a fresh On.
    unlock_open();PREP_READY();for(int i=0;i<4;i++)lv_obj_send_event(unlock_keys[i],LV_EVENT_CLICKED,NULL);
    submitted=preview_unlock_submissions;preview_display=0;lv_obj_send_event(unlock_confirm,LV_EVENT_CLICKED,NULL);
    assert(unlock_submit_pending&&!unlock_sent&&unlock_length==4&&preview_unlock_submissions==submitted&&preview_display_wakes==display_wakes+1);
    lv_obj_send_event(unlock_confirm,LV_EVENT_CLICKED,NULL);assert(preview_display_wakes==display_wakes+1);
    preview_keyboard.busy=false;preview_keyboard.finished=true;unlock_refresh();preview_display=1;
    unlock_prepare_until=esp_timer_get_time()/1000-1;unlock_refresh();unlock_refresh();
    assert(unlock_sent&&!unlock_submit_pending&&unlock_length==0&&preview_unlock_submissions==submitted+1);
    for(int i=0;i<5;i++)unlock_refresh();assert(preview_unlock_submissions==submitted+1);unlock_close();
    // Cancel / expiry / connection change while preparing must clear pending PIN and never replay it.
    preview_keyboard.busy=false;unlock_open();PREP_READY();for(int i=0;i<4;i++)lv_obj_send_event(unlock_keys[i],LV_EVENT_CLICKED,NULL);
    preview_cover_complete=false;lv_obj_send_event(unlock_confirm,LV_EVENT_CLICKED,NULL);assert(unlock_submit_pending);unlock_close();
    preview_cover_complete=true;unlock_refresh();assert(preview_unlock_submissions==submitted+1&&!unlock_length);
    unlock_open();PREP_READY();for(int i=0;i<4;i++)lv_obj_send_event(unlock_keys[i],LV_EVENT_CLICKED,NULL);
    preview_cover_complete=false;lv_obj_send_event(unlock_confirm,LV_EVENT_CLICKED,NULL);unlock_prepare_deadline=esp_timer_get_time()/1000-1;unlock_refresh();
    assert(unlock_prepare_failed&&!unlock_submit_pending&&!unlock_length);preview_cover_complete=true;unlock_refresh();assert(preview_unlock_submissions==submitted+1);unlock_close();
    preview_cover_complete=false;unlock_open();PREP_READY();unsigned before_cancel=preview_wake_submissions;preview_keyboard.connection++;unlock_refresh();
    assert(!unlock_preparing&&!unlock_submit_pending&&!unlock_length);preview_cover_complete=true;unlock_refresh();assert(preview_wake_submissions==before_cancel);unlock_close();
    preview_cover=false;preview_display=1;preview_keyboard.finished=false;

    // One On reply and repeated UI ticks cannot authorize input. Fresh,
    // consistent replies spanning 500ms may finish without the old six-second pause.
    wakes=preview_wake_submissions;display_wakes=preview_display_wakes;submitted=preview_unlock_submissions;
    preview_display=1;preview_cover=false;int64_t fast_open_at=preview_now_us/1000;unlock_open();
    assert(unlock_preparing==4&&!unlock_prepared&&lv_obj_has_state(unlock_keys[0],LV_STATE_DISABLED));
    assert(unlock_prepare_deadline-preview_now_us/1000==12000);
    for(int i=0;i<20;i++)unlock_refresh();assert(!unlock_prepared);
    PREP_ADVANCE(499);PREP_READ();assert(!unlock_prepared&&preview_display_wakes==display_wakes&&preview_wake_submissions==wakes);
    PREP_ADVANCE(1);unlock_refresh();assert(!unlock_prepared); // No new reply yet.
    PREP_ADVANCE(199);PREP_READ();assert(unlock_prepared&&!unlock_preparing&&preview_unlock_submissions==submitted);
    assert(preview_now_us/1000-fast_open_at<1500);unlock_close();
    // On -> Off during observation wakes without waiting for the whole grace.
    unlock_open();PREP_ADVANCE(1000);preview_display=0;PREP_READ();
    assert(unlock_preparing==2&&preview_display_wakes==display_wakes+1&&preview_wake_submissions==wakes);
    for(int i=0;i<5;i++)unlock_refresh();assert(preview_display_wakes==display_wakes+1);
    preview_keyboard.busy=false;preview_keyboard.finished=true;unlock_refresh();
    PREP_ADVANCE(1499);PREP_READ();assert(unlock_preparing==3&&preview_display_wakes==display_wakes+1);
    preview_display=1;preview_cover=true;PREP_ADVANCE(1);PREP_READ();
    assert(unlock_preparing==2&&preview_wake_submissions==wakes+1);
    preview_keyboard.busy=false;preview_keyboard.finished=true;unlock_refresh();
    PREP_ADVANCE(350);PREP_READ();assert(unlock_preparing==4&&preview_wake_submissions==wakes+1); // Cover still visible: never second Space.
    preview_cover=false;PREP_ADVANCE(200);PREP_READ();assert(unlock_prepared&&!unlock_length);unlock_close();
    // Opening during transient HID suspension must not skip preparation forever.
    preview_keyboard.ready=false;unlock_open();assert(unlock_preparing&&!unlock_prepared);
    PREP_ADVANCE(500);PREP_READ();assert(!unlock_prepared);
    preview_keyboard.ready=true;preview_display=0;PREP_ADVANCE(200);PREP_READ();assert(unlock_preparing==2);
    unlock_close();assert(!preview_keyboard.busy&&!unlock_length);
    // Unknown reply, then a read timeout, then positive Off: only reads repeat.
    display_wakes=preview_display_wakes;wakes=preview_wake_submissions;
    preview_display=-1;unlock_open();assert(unlock_preparing==4);
    preview_cover_complete=false;PREP_ADVANCE(200);unlock_refresh();assert(unlock_preparing==1);
    PREP_ADVANCE(2000);unlock_refresh();assert(unlock_preparing==4&&!unlock_prepare_failed);
    preview_cover_complete=true;preview_display=0;PREP_ADVANCE(200);PREP_READ();
    assert(unlock_preparing==2&&preview_display_wakes==display_wakes+1&&preview_wake_submissions==wakes);unlock_close();
    // Rejected BEFORE admission is harmless; retry requires a new read proof.
    display_wakes=preview_display_wakes;preview_wake_accept=false;unlock_open();
    assert(unlock_preparing==4&&unlock_display_attempts==0&&preview_display_wakes==display_wakes);
    preview_wake_accept=true;PREP_ADVANCE(200);PREP_READ();assert(unlock_display_attempts==1&&preview_display_wakes==display_wakes+1);
    // Once accepted, uncertainty stops, clears volatile PIN, and never repeats.
    preview_keyboard.ready=false;unlock_refresh();assert(unlock_prepare_failed&&unlock_failure==2&&!unlock_preparing&&!unlock_length);
    preview_keyboard.ready=true;for(int i=0;i<5;i++){PREP_ADVANCE(200);unlock_refresh();}
    assert(preview_display_wakes==display_wakes+1);unlock_close();
    for(int loss=0;loss<3;loss++) {
        preview_display=-1;unlock_open();assert(unlock_preparing);
        if(loss==0)preview_keyboard.connected=false;
        if(loss==1)preview_keyboard.secure=false;
        if(loss==2)preview_keyboard.connection++;
        unlock_refresh();assert(unlock_prepare_failed&&unlock_failure==3&&!unlock_preparing&&!unlock_length);
        preview_keyboard.connected=preview_keyboard.secure=true;unlock_close();
    }
    // Explicit submit on lit PIN page also observes before sending; cancel during it clears PIN.
    preview_display=1;preview_cover=false;unlock_open();PREP_READY();assert(unlock_prepared);
    for(int i=0;i<4;i++)lv_obj_send_event(unlock_keys[i],LV_EVENT_CLICKED,NULL);
    submitted=preview_unlock_submissions;preview_cover_complete=false;lv_obj_send_event(unlock_confirm,LV_EVENT_CLICKED,NULL);
    assert(unlock_submit_pending&&unlock_preparing&&!unlock_sent&&unlock_length==4);
    PREP_ADVANCE(1000);PREP_READ();assert(preview_unlock_submissions==submitted);
    lv_obj_send_event(unlock_close_button,LV_EVENT_CLICKED,NULL);assert(!unlock_overlay&&!unlock_length&&!unlock_submit_pending);
    preview_cover_complete=true;
    for(size_t i=0;i<sizeof(unlock_pin);i++)assert(!unlock_pin[i]);
    // Expired normal lock status is not proof even with a display reply; recovery may only reread.
    unlock_open();preview_computer.lock_updated-=7000;unlock_refresh();assert(!unlock_prepared&&unlock_preparing);
    PREP_READY();assert(unlock_prepared);unlock_close();
    // Fresh Off AFTER a completed Shift plus 1.5s settling permits ONE extra Shift.
    preview_keyboard.finished=false;preview_keyboard.busy=false;preview_display=0;preview_cover=false;
    display_wakes=preview_display_wakes;wakes=preview_wake_submissions;submitted=preview_unlock_submissions;
    unlock_open();assert(unlock_display_attempts==1&&preview_display_wakes==display_wakes+1);
    preview_keyboard.busy=false;preview_keyboard.finished=true;unlock_refresh();
    assert(unlock_preparing==3&&unlock_prepare_until-preview_now_us/1000==1500);
    unsigned retry_reads=preview_cover_reads;
    PREP_ADVANCE(1499);PREP_READ();assert(unlock_preparing==3&&preview_display_wakes==display_wakes+1&&preview_cover_reads==retry_reads);
    PREP_ADVANCE(1);PREP_READ();assert(unlock_preparing==2&&unlock_display_attempts==2&&preview_display_wakes==display_wakes+2&&preview_cover_reads==retry_reads+1);
    preview_keyboard.busy=false;preview_keyboard.finished=true;unlock_refresh();
    PREP_ADVANCE(1500);PREP_READ();assert(unlock_preparing==4&&preview_display_wakes==display_wakes+2);
    // Even if every subsequent response is Off there is no third Shift or any PIN/Space.
    for(int i=0;i<46;i++){PREP_ADVANCE(200);PREP_READ();}
    assert(unlock_prepare_failed&&unlock_failure==1&&!unlock_length&&preview_display_wakes==display_wakes+2&&preview_wake_submissions==wakes&&preview_unlock_submissions==submitted);unlock_close();
    // On after the wait skips the retry; unknown/late reply cannot authorize it.
    display_wakes=preview_display_wakes;unlock_open();preview_keyboard.busy=false;preview_keyboard.finished=true;unlock_refresh();
    preview_cover_complete=false;PREP_ADVANCE(1500);PREP_READ();assert(unlock_preparing==1&&preview_display_wakes==display_wakes+1);
    PREP_ADVANCE(2000);unlock_refresh();assert(unlock_preparing==4&&preview_display_wakes==display_wakes+1);
    preview_cover_complete=true;preview_display=-1;PREP_ADVANCE(200);PREP_READ();assert(unlock_preparing==4&&preview_display_wakes==display_wakes+1);
    preview_display=1;PREP_ADVANCE(200);PREP_READ();assert(unlock_prepared&&preview_display_wakes==display_wakes+1);unlock_close();
    // Failed/uncertain first transaction never gets the extra Shift.
    preview_display=0;display_wakes=preview_display_wakes;unlock_open();preview_keyboard.busy=false;preview_keyboard.finished=true;preview_keyboard.failed=true;unlock_refresh();
    assert(unlock_prepare_failed&&unlock_failure==2);PREP_ADVANCE(2000);PREP_READ();assert(preview_display_wakes==display_wakes+1);unlock_close();preview_keyboard.failed=false;
    // Cancellation during settling drops the second Shift; it cannot arrive later.
    display_wakes=preview_display_wakes;unlock_open();preview_keyboard.busy=false;preview_keyboard.finished=true;unlock_refresh();
    lv_obj_send_event(unlock_close_button,LV_EVENT_CLICKED,NULL);PREP_ADVANCE(2000);PREP_READ();assert(!unlock_overlay&&preview_display_wakes==display_wakes+1);
    // Explicit PIN submission waits through both Shift transactions, then sends once.
    preview_display=1;unlock_open();PREP_READY();for(int i=0;i<4;i++)lv_obj_send_event(unlock_keys[i],LV_EVENT_CLICKED,NULL);
    submitted=preview_unlock_submissions;display_wakes=preview_display_wakes;preview_display=0;lv_obj_send_event(unlock_confirm,LV_EVENT_CLICKED,NULL);
    assert(unlock_submit_pending&&!unlock_sent&&preview_display_wakes==display_wakes+1);
    preview_keyboard.busy=false;preview_keyboard.finished=true;unlock_refresh();PREP_ADVANCE(1500);PREP_READ();
    assert(unlock_submit_pending&&!unlock_sent&&preview_display_wakes==display_wakes+2&&preview_unlock_submissions==submitted);
    preview_keyboard.busy=false;preview_keyboard.finished=true;unlock_refresh();preview_display=1;PREP_ADVANCE(1500);PREP_READ();
    assert(unlock_sent&&!unlock_submit_pending&&!unlock_length&&preview_unlock_submissions==submitted+1);
    for(int i=0;i<5;i++)unlock_refresh();assert(preview_unlock_submissions==submitted+1);unlock_close();
    // Completed preparation must survive only transient readiness/freshness gaps.
    preview_keyboard.finished=false;preview_keyboard.busy=false;preview_display=1;preview_cover=false;
    unlock_open();PREP_READY();assert(unlock_prepared&&!lv_obj_has_state(unlock_keys[0],LV_STATE_DISABLED));
    display_wakes=preview_display_wakes;wakes=preview_wake_submissions;submitted=preview_unlock_submissions;
    lv_obj_send_event(unlock_keys[0],LV_EVENT_CLICKED,NULL);assert(unlock_length==1);
    preview_keyboard.ready=false;unlock_refresh();assert(!unlock_length&&lv_obj_has_state(unlock_keys[0],LV_STATE_DISABLED));
    lv_obj_send_event(unlock_keys[0],LV_EVENT_CLICKED,NULL);assert(!unlock_length);
    preview_keyboard.ready=true;unlock_refresh();
    bool readiness_recovered=unlock_prepared&&!lv_obj_has_state(unlock_keys[0],LV_STATE_DISABLED);
    if(getenv("TAB5_READINESS_EXPECT_BUG")) {
        assert(!readiness_recovered);
        puts("BASELINE_READINESS_LATCH_REPRODUCED actual_220_ui keyboard_and_lock_ready_again_but_digits_stay_disabled no_user_pin");
        unlock_close();
    }else {
        assert(readiness_recovered&&preview_display_wakes==display_wakes&&preview_wake_submissions==wakes&&preview_unlock_submissions==submitted);
        // Production lock-state freshness also gates local taps, independently of display RPCs.
        lv_obj_send_event(unlock_keys[0],LV_EVENT_CLICKED,NULL);assert(unlock_length==1);
        preview_computer.updated=preview_computer.lock_updated=preview_now_us/1000-6500;unlock_refresh();
        assert(!unlock_length&&lv_obj_has_state(unlock_keys[0],LV_STATE_DISABLED));
        lv_obj_send_event(unlock_keys[0],LV_EVENT_CLICKED,NULL);assert(!unlock_length);
        preview_computer.updated=preview_computer.lock_updated=preview_now_us/1000;unlock_refresh();
        assert(unlock_prepared&&!lv_obj_has_state(unlock_keys[0],LV_STATE_DISABLED));
        for(int gap=0;gap<3;gap++) {
            preview_keyboard.ready=false;
            if(gap==0)preview_keyboard.subscribed=false;
            if(gap==1)preview_keyboard.bridge_verified=false;
            unlock_refresh();assert(!unlock_length&&lv_obj_has_state(unlock_keys[0],LV_STATE_DISABLED));
            preview_keyboard.ready=preview_keyboard.subscribed=preview_keyboard.bridge_verified=true;unlock_refresh();
            assert(unlock_prepared&&!lv_obj_has_state(unlock_keys[0],LV_STATE_DISABLED));
        }
        assert(preview_display_wakes==display_wakes&&preview_wake_submissions==wakes&&preview_unlock_submissions==submitted);
        // Recovering local entry is not HID authorization: confirmation starts a new check.
        for(int i=0;i<4;i++)lv_obj_send_event(unlock_keys[i],LV_EVENT_CLICKED,NULL);
        preview_display=-1;lv_obj_send_event(unlock_confirm,LV_EVENT_CLICKED,NULL);
        assert(unlock_prepare_failed&&!unlock_submit_pending&&!unlock_sent&&!unlock_length&&preview_unlock_submissions==submitted);
        PREP_ADVANCE(4000);unlock_refresh();assert(unlock_prepare_failed&&!unlock_length&&!unlock_submit_pending&&preview_unlock_submissions==submitted);unlock_close();
        // Critical peer changes / a positive unlocked state never auto-recover local editing.
        for(int loss=0;loss<4;loss++) {
            preview_display=1;unlock_open();PREP_READY();assert(unlock_prepared);
            lv_obj_send_event(unlock_keys[0],LV_EVENT_CLICKED,NULL);assert(unlock_length==1);
            if(loss==0){preview_keyboard.connected=false;preview_keyboard.ready=false;}
            if(loss==1){preview_keyboard.secure=false;preview_keyboard.ready=false;}
            if(loss==2)preview_keyboard.connection++;
            if(loss==3)preview_computer.locked=false;
            unlock_refresh();assert(!unlock_prepared&&!unlock_length);
            preview_keyboard.connected=preview_keyboard.secure=preview_keyboard.ready=true;preview_computer.locked=true;unlock_refresh();
            if(loss==3){assert(unlock_overlay&&unlock_success_until&&!unlock_length);PREP_ADVANCE(500);unlock_refresh();assert(!unlock_overlay);}
            else assert(!unlock_prepared&&lv_obj_has_state(unlock_keys[0],LV_STATE_DISABLED));unlock_close();
        }
        assert(preview_display_wakes==display_wakes&&preview_wake_submissions==wakes&&preview_unlock_submissions==submitted);
        puts("BLE_READINESS_RECOVERY_OK local_digits_recover_same_secure_peer pin_cleared_while_unready every_tap_gated no_auto_keys_or_pin confirm_rechecks critical_changes_require_reopen");
    }
    // State copy is explicit and fresh; merely displaying Off never sends a key.
    preview_display=1;preview_cover=false;preview_keyboard.busy=false;preview_keyboard.finished=false;
    unlock_open();PREP_READY();assert(unlock_prepared);
    wakes=preview_wake_submissions;display_wakes=preview_display_wakes;submitted=preview_unlock_submissions;
    const int samples[]={1,0,2,-1};const char *sample_words[]={"请输入 PIN 或密码","尚未就绪","请输入 PIN 或密码","无法确认"};
    const char *sample_captures[]={"computer-unlock-screen-on","computer-unlock-screen-off","computer-unlock-screen-dim","computer-unlock-screen-unknown"};
    for(int i=0;i<4;i++) {
        preview_display=samples[i];unlock_refresh();assert(strstr(lv_label_get_text(unlock_hint),sample_words[i]));
        assert(lv_obj_has_state(unlock_keys[0],LV_STATE_DISABLED)==(samples[i]<=0));
        if(samples[i]>0)assert(strstr(lv_label_get_text(unlock_hint),"（不保存）")&&!strchr(lv_label_get_text(unlock_hint),'\n')&&!strstr(lv_label_get_text(unlock_hint),"键盘布局")&&!strstr(lv_label_get_text(unlock_hint),"报告")&&!strstr(lv_label_get_text(unlock_hint),"亮屏"));
        else assert(strstr(lv_label_get_text(unlock_hint),"请关闭面板后重试"));
        lv_tick_inc(400);lv_timer_handler();capture(sample_captures[i]);
    }
    preview_display=1;preview_computer.updated=preview_computer.lock_updated=preview_now_us/1000-4000;unlock_refresh();
    assert(strstr(lv_label_get_text(unlock_hint),"无法确认")&&lv_obj_has_state(unlock_keys[0],LV_STATE_DISABLED));
    preview_computer.updated=preview_computer.lock_updated=preview_now_us/1000;unlock_refresh();
    assert(preview_display_wakes==display_wakes&&preview_wake_submissions==wakes&&preview_unlock_submissions==submitted);
    for(int i=0;i<4;i++)lv_obj_send_event(unlock_keys[i],LV_EVENT_CLICKED,NULL);
    preview_cover_complete=false;int64_t confirm_at=preview_now_us/1000;
    lv_obj_send_event(unlock_confirm,LV_EVENT_CLICKED,NULL);
    assert(unlock_submit_pending&&!unlock_sent&&!unlock_observing&&unlock_prepare_deadline-confirm_at==4000);
    assert(strstr(lv_label_get_text(unlock_hint),"正在解锁"));capture("computer-unlock-submit-checking");
    lv_obj_send_event(unlock_confirm,LV_EVENT_CLICKED,NULL);assert(preview_unlock_submissions==submitted);
    PREP_ADVANCE(500);preview_cover_complete=true;unlock_refresh();
    assert(preview_now_us/1000-confirm_at==500&&unlock_sent&&!unlock_length&&preview_unlock_submissions==submitted+1);
    for(int i=0;i<5;i++)unlock_refresh();assert(preview_unlock_submissions==submitted+1);unlock_close();
    // Missing/late evidence stops the confirmed action; reopening is explicit.
    for(int missing=0;missing<2;missing++) {
        preview_display=1;preview_keyboard.busy=false;unlock_open();PREP_READY();
        for(int i=0;i<4;i++)lv_obj_send_event(unlock_keys[i],LV_EVENT_CLICKED,NULL);
        if(missing==0)preview_display=-1;else preview_cover_complete=false;
        submitted=preview_unlock_submissions;lv_obj_send_event(unlock_confirm,LV_EVENT_CLICKED,NULL);
        if(missing==1){PREP_ADVANCE(2000);unlock_refresh();}
        assert(unlock_prepare_failed&&unlock_failure==4&&!unlock_submit_pending&&!unlock_length&&preview_unlock_submissions==submitted);
        assert(strstr(lv_label_get_text(unlock_hint),"关闭面板后重试"));capture("computer-unlock-reopen-required");
        unsigned failed_reads=preview_cover_reads;preview_display=1;preview_cover_complete=true;PREP_ADVANCE(4000);unlock_refresh();
        assert(preview_cover_reads==failed_reads&&preview_unlock_submissions==submitted&&!unlock_length);unlock_close();
    }
    preview_display=1;unlock_open();PREP_READY();assert(unlock_prepared&&!unlock_length);unlock_close();

    // Success is a new unlocked state, not HID completion or an older cache.
    preview_display=1;preview_keyboard.ready=preview_keyboard.secure=preview_keyboard.connected=preview_keyboard.bridge_verified=true;
    preview_keyboard.busy=false;preview_keyboard.finished=false;preview_computer.locked=true;
    unlock_open();PREP_READY();for(int i=0;i<4;i++)lv_obj_send_event(unlock_keys[i],LV_EVENT_CLICKED,NULL);
    submitted=preview_unlock_submissions;lv_obj_send_event(unlock_confirm,LV_EVENT_CLICKED,NULL);
    assert(unlock_sent&&unlock_overlay&&!unlock_length&&preview_unlock_submissions==submitted+1);
    preview_keyboard.busy=false;preview_keyboard.finished=true;unlock_refresh();assert(unlock_overlay&&!unlock_success_until); // Sent is not success.
    preview_computer.locked=false;preview_computer.updated=preview_computer.lock_updated=unlock_submitted_at;unlock_refresh();assert(unlock_overlay&&!unlock_success_until); // Same-age cache.
    preview_computer.updated=preview_computer.lock_updated=preview_now_us/1000-6500;unlock_refresh();assert(unlock_overlay&&!unlock_success_until); // Expired unlocked cache.
    preview_computer.locked=true;PREP_ADVANCE(1);unlock_refresh();assert(unlock_overlay); // New locked sample.
    preview_computer.locked=false;PREP_ADVANCE(1);unlock_refresh();
    assert(unlock_overlay&&unlock_success_until&&!unlock_length&&!unlock_preparing&&!unlock_submit_pending);
    assert(!strcmp(lv_label_get_text(unlock_hint),"电脑已解锁"));
    assert(lv_obj_has_flag(unlock_keys[0],LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(unlock_close_button,LV_OBJ_FLAG_HIDDEN));
    assert(!unlock_spinning&&!lv_obj_has_flag(unlock_status_face,LV_OBJ_FLAG_HIDDEN));
    lv_obj_send_event(unlock_keys[0],LV_EVENT_CLICKED,NULL);assert(!unlock_length);
    capture("manual-unlock-success");
    PREP_ADVANCE(499);unlock_refresh();assert(unlock_overlay&&!strcmp(lv_label_get_text(unlock_hint),"电脑已解锁"));
    PREP_ADVANCE(1);unlock_refresh();assert(!unlock_overlay);
    for(size_t i=0;i<sizeof(unlock_pin);i++)assert(!unlock_pin[i]);
    for(int i=0;i<5;i++)unlock_refresh();assert(!unlock_overlay&&preview_unlock_submissions==submitted+1);
    capture("computer-auto-close-confirmed");
    // Computer-keyboard unlock also dismisses an open panel, clearing unsent local digits.
    preview_computer.locked=true;unlock_open();PREP_READY();lv_obj_send_event(unlock_keys[0],LV_EVENT_CLICKED,NULL);
    submitted=preview_unlock_submissions;display_wakes=preview_display_wakes;wakes=preview_wake_submissions;
    preview_computer.locked=false;PREP_ADVANCE(1);unlock_refresh();assert(unlock_overlay&&unlock_success_until&&!unlock_length&&preview_unlock_submissions==submitted);PREP_ADVANCE(500);unlock_refresh();assert(!unlock_overlay);
    assert(preview_display_wakes==display_wakes&&preview_wake_submissions==wakes);
    // A different or insecure peer must not cause a success dismissal.
    for(int loss=0;loss<3;loss++) {
        preview_computer.locked=true;unlock_open();PREP_READY();preview_computer.locked=false;PREP_ADVANCE(1);
        if(loss==0)preview_keyboard.connection++;
        if(loss==1)preview_keyboard.secure=false;
        if(loss==2)preview_keyboard.bridge_verified=false;
        unlock_refresh();assert(unlock_overlay&&!unlock_success_until);unlock_close();
        preview_keyboard.secure=preview_keyboard.bridge_verified=true;
    }
    // The pairing consent panel is independent even if the computer is unlocked.
    preview_keyboard.pairing=true;preview_keyboard.comparison=482617;unlock_refresh();
    PREP_ADVANCE(1);unlock_refresh();assert(unlock_overlay&&unlock_pairing_mode);
    unlock_close();preview_keyboard.pairing=false;preview_computer.locked=true;
    // Closing does not need to reset last-transaction UI flags: verify the
    // actual user contract, a fresh editable panel with an empty masked value.
    preview_computer.locked=true;PREP_ADVANCE(1);preview_keyboard.busy=false;
    preview_keyboard.connected=preview_keyboard.secure=preview_keyboard.subscribed=preview_keyboard.bridge_verified=preview_keyboard.ready=true;
    submitted=preview_unlock_submissions;unlock_open();PREP_READY();
    assert(unlock_overlay&&!unlock_length&&!unlock_sent&&!unlock_submit_pending);
    assert(!strcmp(lv_label_get_text(unlock_mask),"PIN")&&!lv_obj_has_state(unlock_keys[0],LV_STATE_DISABLED));
    for(size_t i=0;i<sizeof(unlock_pin);i++)assert(!unlock_pin[i]);
    assert(preview_unlock_submissions==submitted);unlock_close();
    puts("BLE_AUTO_CLOSE_OK existing_success_branches_fresh_unlocked_same_secure_peer only_hid_finished_not_success stale_unknown_locked_do_not_close manual_pc_unlock_clears_unsent_pin peer_loss_no_success pairing_unchanged no_replay");
    puts("BLE_FAST_SUBMIT_UI_OK new_reply_before_pin no_fixed_six_second_submit_wait 500ms_fixture_reply unknown_timeout_stop_clear manual_reopen no_automatic_pin_replay simple_pin_prompt_only_normal error_close_retry states_expiry_gate_no_keys");
    printf("BLE_PREPARATION_REGRESSION_OK fresh_stable_on_observation_500ms bounded_12000ms readiness_recovery readonly_retry cancel_expiry_peer_loss_no_replay\n");
    printf("BLE_SHIFT_RETRY_OK 1499ms_no_send 1500ms_new_read_fresh_off two_max on_skips unknown_no_key failed_first_no_retry cancel_no_late_send pending_pin_once\n");
    const char *cover_words="正在确认电脑屏幕和登录页前不会输入PIN未状态开始请检查关闭后重新请求显示已本次不保存唤醒超时停止报告亮屏幕变暗检测展开页稍候更新立即仅本次使用必要关闭重新打开暂时无法就绪准备面板后重试尚未能解锁完成（）";
    for(uint32_t pos=0;cover_words[pos];){uint32_t cp=lv_text_encoded_next(cover_words,&pos);lv_font_glyph_dsc_t glyph={0};assert(lv_font_get_glyph_dsc(&tab5_font_24,&glyph,cp,0)&&!glyph.is_placeholder);}
    const char *keyboard_titles="蓝牙配对重新配对解锁电脑";
    for(uint32_t pos=0;keyboard_titles[pos];){uint32_t cp=lv_text_encoded_next(keyboard_titles,&pos);lv_font_glyph_dsc_t glyph={0};assert(lv_font_get_glyph_dsc(&tab5_font_30,&glyph,cp,0)&&!glyph.is_placeholder);}
    preview_computer.locked=false;preview_computer.can_lock=true;computer_refresh();
    assert(strstr(lv_label_get_text(computer_status),"未锁屏")&&!lv_obj_has_state(computer_lock,LV_STATE_DISABLED));
    lv_obj_send_event(workbench_back,LV_EVENT_CLICKED,NULL);assert(current_page==11);
    page(5);computer_refresh();capture("music-playback-controls");
    lv_obj_send_event(music_control_buttons[0],LV_EVENT_CLICKED,NULL);assert(!strcmp(preview_computer_target,"previous"));
    lv_obj_send_event(music_control_buttons[1],LV_EVENT_CLICKED,NULL);assert(!strcmp(preview_computer_target,"play-pause"));
    lv_obj_send_event(music_control_buttons[2],LV_EVENT_CLICKED,NULL);assert(!strcmp(preview_computer_target,"next"));
    preview_computer.can_next=false;computer_refresh();assert(lv_obj_has_state(music_control_buttons[2],LV_STATE_DISABLED));
    commands=preview_computer_commands;lv_obj_send_event(music_control_buttons[2],LV_EVENT_CLICKED,NULL);assert(preview_computer_commands==commands);
    last_received-=9000;computer_refresh();assert(lv_obj_has_state(music_control_buttons[1],LV_STATE_DISABLED));
    lv_obj_send_event(music_control_buttons[1],LV_EVENT_CLICKED,NULL);assert(preview_computer_commands==commands);
    page(14);capture("computer-disconnected");
    preview_computer.launchers[0].has_icon=false;computer_refresh();
    assert(!lv_obj_has_flag(computer_launch_symbol[0],LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(computer_launch_icon[0],LV_OBJ_FLAG_HIDDEN));
    preview_computer.count=0;computer_refresh();
    assert(lv_obj_has_flag(computer_launch_symbol[0],LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(computer_launch_icon[0],LV_OBJ_FLAG_HIDDEN));
    last_received=esp_timer_get_time()/1000;cJSON_DeleteItemFromObject(health,"computerControlVersion");computer_refresh();
    assert(lv_obj_has_state(computer_lock,LV_STATE_DISABLED));capture("computer-unsupported");
    const char *words="正在通过 USB Wi-Fi 蓝牙解锁：一键修改控制电脑音量锁屏常用启动离开座位静音工作文件夹播放切歌解锁确认清空取消输入仅用于本次不保存请先在登录页断蓝牙连接正在稍候已请求失效重新开始未检查会自动试临时程序";
    for(uint32_t i=0;words[i];){uint32_t cp=lv_text_encoded_next(words,&i);lv_font_glyph_dsc_t g;assert(lv_font_get_glyph_dsc(&tab5_font_24,&g,cp,0)&&!g.is_placeholder);}
    const char *title_words="解锁电脑";
    for(uint32_t i=0;title_words[i];){uint32_t cp=lv_text_encoded_next(title_words,&i);lv_font_glyph_dsc_t g;assert(lv_font_get_glyph_dsc(&tab5_font_30,&g,cp,0)&&!g.is_placeholder);}
    // Exercise the real modal through both latched transports with synthetic PINs.
    cJSON_AddNumberToObject(health,"computerControlVersion",1);
    for(int usb=0;usb<2;usb++) {
        preview_usb_available=usb;preview_keyboard=(ble_keyboard_state){.connected=true,.secure=true,.subscribed=true,.bridge_verified=true,.ready=true,.connection=9};
        preview_display=1;preview_cover=false;preview_cover_complete=true;preview_wake_accept=true;
        preview_computer=(computer_remote_state){.ready=true,.lock_known=true,.locked=true,.one_click_unlock=true,.updated=preview_now_us/1000,.lock_updated=preview_now_us/1000};
        last_received=preview_now_us/1000;page(14);computer_refresh();
        unsigned sent_before=preview_unlock_submissions,requests_before=preview_oneclick_requests;
        unlock_open();assert(unlock_auto&&unlock_using_usb==(bool)usb);assert(lv_obj_has_flag(unlock_keys[0],LV_OBJ_FLAG_HIDDEN));
        PREP_READY();PREP_READ();PREP_READ();
        assert(unlock_sent&&preview_unlock_submissions==sent_before+1&&preview_oneclick_requests==requests_before+1&&unlock_length==0);
        for(int i=0;i<5;i++)unlock_refresh();assert(preview_oneclick_requests==requests_before+1&&preview_unlock_submissions==sent_before+1);
        assert(unlock_spinning&&!lv_obj_has_flag(unlock_close_button,LV_OBJ_FLAG_HIDDEN));
        capture(usb?"usb-oneclick-waiting":"ble-oneclick-waiting");
        preview_computer.locked=false;PREP_ADVANCE(1);unlock_refresh();assert(unlock_overlay&&unlock_success_until&&!strcmp(lv_label_get_text(unlock_hint),"电脑已解锁"));
        assert(!unlock_spinning&&lv_obj_has_flag(unlock_close_button,LV_OBJ_FLAG_HIDDEN)&&lv_obj_has_flag(unlock_title,LV_OBJ_FLAG_HIDDEN));
        capture(usb?"usb-oneclick-success":"ble-oneclick-success");
        PREP_ADVANCE(499);unlock_refresh();assert(unlock_overlay&&!unlock_length);
        PREP_ADVANCE(1);unlock_refresh();assert(!unlock_overlay&&preview_unlock_submissions==sent_before+1);
        preview_keyboard.busy=false;preview_keyboard.finished=true;preview_computer.locked=true;PREP_ADVANCE(1);
        unlock_open();PREP_READY();assert(unlock_auto_requested);
        preview_oneclick_state=1;sent_before=preview_unlock_submissions;unlock_close();assert(!preview_oneclick_state&&unlock_length==0);unlock_refresh();assert(preview_unlock_submissions==sent_before);
        preview_keyboard.busy=false;unlock_open();preview_cover_complete=false;requests_before=preview_oneclick_requests;sent_before=preview_unlock_submissions;
        PREP_ADVANCE(12001);unlock_refresh();assert(unlock_auto_failed&&unlock_prepare_failed&&!preview_oneclick_state);
        assert(preview_oneclick_requests==requests_before&&preview_unlock_submissions==sent_before);
        assert(strstr(lv_label_get_text(unlock_hint),"未完成"));
        assert(!unlock_spinning&&!lv_obj_has_flag(unlock_close_button,LV_OBJ_FLAG_HIDDEN));
        capture(usb?"usb-oneclick-error":"ble-oneclick-error");
        lv_obj_update_layout(unlock_overlay);
        assert(lv_obj_get_height(unlock_hint)>=lv_font_get_line_height(&tab5_font_24)*2);
        lv_obj_send_event(unlock_close_button,LV_EVENT_CLICKED,NULL);assert(!unlock_overlay);preview_cover_complete=true;
        preview_computer.one_click_unlock=false;preview_keyboard.busy=false;unlock_open();PREP_READY();assert(!unlock_auto&&!lv_obj_has_flag(unlock_keys[0],LV_OBJ_FLAG_HIDDEN));
        lv_obj_send_event(unlock_keys[0],LV_EVENT_CLICKED,NULL);assert(unlock_length==1);unlock_close();assert(!unlock_length);
    }
    preview_usb_available=false;preview_computer.one_click_unlock=false;
    puts("UNLOCK_SUCCESS_FEEDBACK_OK manual_ble_usb_confirmed_only_499ms_visible_500ms_closes_no_more_digits");
    puts("UNLOCK_STATUS_CARD_OK progress_cancel_error_close_success_no_controls_hidden_spinner_stopped_manual_pairing_preserved");
    puts("USB_BLE_ONECLICK_UI_OK latched_transport_single_request_single_send_cancel_clears_manual_fallback_autoclose");
    // Opening readiness gaps must recover without reopening or sending extra keys.
    // Exercise both latched transports, every independent gate, and terminal paths.
    for(int usb=0;usb<2;usb++) {
        preview_usb_available=usb;preview_oneclick_result=2;
        for(int gap=0;gap<7;gap++) {
            preview_keyboard=(ble_keyboard_state){.connected=true,.secure=true,.subscribed=true,.bridge_verified=true,.ready=true,.connection=77};
            preview_computer=(computer_remote_state){.ready=true,.lock_known=true,.locked=true,.one_click_unlock=true,.updated=preview_now_us/1000,.lock_updated=preview_now_us/1000};
            preview_display=1;preview_cover=false;preview_cover_complete=true;page(14);computer_refresh();
            if(gap==0)preview_keyboard.connected=false;
            if(gap==1)preview_keyboard.secure=false;
            if(gap==2)preview_keyboard.subscribed=false;
            if(gap==3)preview_keyboard.bridge_verified=false;
            if(gap<5)preview_keyboard.ready=false;
            if(gap==6)preview_keyboard.busy=true;
            unsigned before_send=preview_unlock_submissions,before_grant=preview_oneclick_requests;
            unlock_open();if(gap==5)preview_computer.lock_updated-=7000;unlock_refresh();
            assert(!unlock_auto_failed&&!unlock_failure_recorded&&!unlock_sent&&unlock_preparing);
            assert(preview_unlock_submissions==before_send&&preview_oneclick_requests==before_grant);
            preview_keyboard=(ble_keyboard_state){.connected=true,.secure=true,.subscribed=true,.bridge_verified=true,.ready=true,.connection=77};
            PREP_READY();for(int i=0;i<6;i++)unlock_refresh();
            assert(unlock_sent&&preview_unlock_submissions==before_send+1&&preview_oneclick_requests==before_grant+1);
            preview_computer.locked=false;PREP_ADVANCE(1);unlock_refresh();PREP_ADVANCE(500);unlock_refresh();assert(!unlock_overlay);
        }
        // No unbounded retry, no rebind, and no history loss after modal close.
        preview_keyboard=(ble_keyboard_state){.connected=true,.subscribed=true,.connection=77};
        preview_computer=(computer_remote_state){.ready=true,.lock_known=true,.locked=true,.one_click_unlock=true,.updated=preview_now_us/1000,.lock_updated=preview_now_us/1000};
        unsigned before_send=preview_unlock_submissions,before_grant=preview_oneclick_requests;
        unlock_open();PREP_ADVANCE(12001);unlock_refresh();assert(unlock_prepare_failed&&unlock_auto_failed);
        unsigned failure=0,failed_at=0;assert(unlock_failure_snapshot(&failure,&failed_at)&&((failure&31)==1));
        assert(((failure>>5)&511)&2);assert(((failure>>17)&1)==(unsigned)usb);
        unlock_close();unsigned retained=0,retained_at=0;assert(unlock_failure_snapshot(&retained,&retained_at)&&retained==failure&&retained_at==failed_at);
        preview_keyboard=(ble_keyboard_state){.connected=true,.secure=true,.subscribed=true,.bridge_verified=true,.ready=true,.connection=77};
        unlock_open();preview_keyboard.connection++;unlock_refresh();assert(unlock_auto_failed&&!unlock_sent);unlock_close();
        assert(preview_unlock_submissions==before_send&&preview_oneclick_requests==before_grant);
        for(int error=-2;error>=-8;error--) {
            preview_oneclick_result=error;PREP_ADVANCE(1);unlock_open();PREP_READY();for(int i=0;i<6;i++)unlock_refresh();
            assert(unlock_auto_failed&&!unlock_sent&&!unlock_length);assert(unlock_failure_snapshot(&failure,&failed_at)&&(failure&31)==(unsigned)(10-error));
            unlock_close();assert(preview_unlock_submissions==before_send);
        }
        preview_oneclick_result=2;preview_keyboard.busy=false;PREP_ADVANCE(1);unlock_open();PREP_READY();for(int i=0;i<6;i++)unlock_refresh();
        assert(unlock_sent);before_send=preview_unlock_submissions;before_grant=preview_oneclick_requests;
        preview_keyboard.busy=false;preview_keyboard.failed=true;unlock_refresh();
        assert(unlock_failure_snapshot(&failure,&failed_at)&&(failure&31)==8);
        for(int i=0;i<5;i++){PREP_ADVANCE(3000);unlock_refresh();}
        assert(preview_unlock_submissions==before_send&&preview_oneclick_requests==before_grant);unlock_close();
        // A later successful unlock must retain the previous failure for diagnosis.
        preview_keyboard.failed=false;PREP_ADVANCE(1);unlock_open();PREP_READY();for(int i=0;i<6;i++)unlock_refresh();
        preview_computer.locked=false;PREP_ADVANCE(1);unlock_refresh();PREP_ADVANCE(500);unlock_refresh();
        assert(!unlock_overlay&&unlock_failure_snapshot(&retained,&retained_at)&&retained==failure);
    }
    preview_usb_available=false;preview_oneclick_result=2;
    puts("UNLOCK_STABILITY_OK USB_BLE_7_opening_gaps_recover_bounded_timeout_epoch_change_no_rebind_7_credential_errors_no_PIN_retry_failure_survives_close_success");
    cJSON_AddNumberToObject(health,"computerControlVersion",1);
    preview_keyboard=(ble_keyboard_state){.connected=true,.secure=true,.subscribed=true,.bridge_verified=true,.ready=true,.connection=90};
    preview_computer=(computer_remote_state){.ready=true,.lock_known=true,.locked=true,.one_click_unlock=true,.updated=preview_now_us/1000,.lock_updated=preview_now_us/1000};
    unlock_open();PREP_READY();for(int i=0;i<6;i++)unlock_refresh();assert(unlock_sent);
    computer_poll_at=preview_now_us/1000;computer_message_until=computer_poll_at+3500;
    unsigned polls=preview_status_reads;
    PREP_ADVANCE(499);computer_refresh();assert(preview_status_reads==polls);
    PREP_ADVANCE(1);computer_refresh();assert(preview_status_reads==polls+1);
    unlock_close();computer_poll_at=preview_now_us/1000;computer_message_until=0;
    PREP_ADVANCE(500);computer_refresh();assert(preview_status_reads==polls+1);
    PREP_ADVANCE(1500);computer_refresh();assert(preview_status_reads==polls+2);
    puts("UNLOCK_CONFIRM_POLL_OK 499ms_no_poll_500ms_poll_bypasses_old_action_message_normal_2000ms_no_key_retry");
    // Wi-Fi uses the same modal; there is no device PIN fetch or HID send.
    cJSON_AddNumberToObject(health,"wifiUnlockVersion",1);preview_wifi_only=true;preview_usb_available=false;
    preview_computer=(computer_remote_state){.ready=true,.lock_known=true,.locked=true,.updated=preview_now_us/1000,.lock_updated=preview_now_us/1000};
    unsigned wifi_requests=preview_wifi_requests,pin_requests=preview_oneclick_requests,hid_requests=preview_unlock_submissions;
    page(14);computer_refresh();unlock_open();assert(unlock_overlay&&unlock_wifi&&unlock_auto&&unlock_sent);
    assert(preview_wifi_requests==wifi_requests+1&&preview_oneclick_requests==pin_requests&&preview_unlock_submissions==hid_requests&&!unlock_length);
    assert(lv_obj_has_flag(unlock_keys[0],LV_OBJ_FLAG_HIDDEN));unlock_open();assert(preview_wifi_requests==wifi_requests+1);
    capture("computer-wifi-unlock-preparing");preview_wifi_unlock.stage=1;unlock_refresh();capture("computer-wifi-unlock-sending");
    preview_wifi_unlock=(unlock_remote_state){.finished=true,.unlocked=true};unlock_refresh();assert(!unlock_success_until);
    preview_computer.locked=false;PREP_ADVANCE(1);unlock_refresh();assert(unlock_success_until);capture("computer-wifi-unlock-success");
    PREP_ADVANCE(500);unlock_refresh();assert(!unlock_overlay);
    preview_computer.locked=true;PREP_ADVANCE(1);unlock_open();preview_wifi_unlock=(unlock_remote_state){.finished=true,.error=2};unlock_refresh();
    assert(!unlock_success_until);capture("computer-wifi-unlock-failed");unlock_close();
    PREP_ADVANCE(1);unlock_open();unsigned wifi_cancels=preview_wifi_cancels;unlock_close();assert(preview_wifi_cancels==wifi_cancels+1);
    PREP_ADVANCE(1);unlock_open();preview_wifi_connection++;unlock_refresh();assert(unlock_auto_failed&&!preview_wifi_unlock.busy);unlock_close();
    cJSON_DeleteItemFromObjectCaseSensitive(health,"wifiUnlockVersion");computer_refresh();lv_obj_send_event(computer_lock,LV_EVENT_CLICKED,NULL);assert(!unlock_overlay);
    assert(strstr(lv_label_get_text(computer_status),"Wi-Fi"));
    preview_wifi_only=false;assert(preview_oneclick_requests==pin_requests&&preview_unlock_submissions==hid_requests);
    puts("WIFI_UNLOCK_UI_OK existing_button_no_PIN_no_HID_one_start_cancel_epoch_no_failover_fresh_unlock_500ms_close");
    // Ring snapshots remain bounded, ordered and retain failure + success.
    for(unsigned i=0;i<2+UNLOCK_HISTORY_COUNT*UNLOCK_HISTORY_WORDS;i++)atomic_store(&tab5_unlock_history[i],0);
    atomic_store(&tab5_unlock_history_received,0);
    unsigned history_words[UNLOCK_HISTORY_WORDS]={0,8,10000,1200,50,400,800,0x200C8,100};
    char ring[UNLOCK_HISTORY_SIZE];
    for(unsigned i=0;i<10;i++){history_words[1]=i==8?8:4;unlock_history_append(history_words);}
    assert(unlock_history_snapshot(ring)==10&&strlen(ring)==585&&unlock_history_pending());
    assert(!strncmp(ring,"1;00000003",10)&&strstr(ring,"0000000900000008")&&strstr(ring,"0000000A00000004"));
    atomic_store(&tab5_unlock_history[0],21);assert(!unlock_history_snapshot(ring)&&!ring[0]);atomic_store(&tab5_unlock_history[0],20);
    atomic_store(&tab5_unlock_history_received,10);assert(!unlock_history_pending());
    // Visible channel is latched at open, including after live link changes.
    preview_computer=(computer_remote_state){.ready=true,.lock_known=true,.locked=true,.one_click_unlock=true,.lock_updated=preview_now_us/1000};
    preview_keyboard=(ble_keyboard_state){.connected=true,.secure=true,.subscribed=true,.bridge_verified=true,.ready=true,.connection=123};
    preview_usb_available=true;unlock_open();assert(!strcmp(unlock_transport_name(),"USB"));
    preview_usb_available=false;assert(!strcmp(unlock_transport_name(),"USB"));capture("unlock-channel-usb");unlock_close();
    PREP_ADVANCE(1);unlock_open();assert(!strcmp(unlock_transport_name(),"蓝牙"));capture("unlock-channel-ble");unlock_close();
    preview_wifi_only=true;cJSON_AddNumberToObject(health,"wifiUnlockVersion",1);PREP_ADVANCE(1);unlock_open();
    assert(!strcmp(unlock_transport_name(),"Wi-Fi"));preview_wifi_unlock=(unlock_remote_state){.finished=true,.error=5,.stage=1};unlock_refresh();
    unsigned terminal=atomic_load(&tab5_unlock_history[1]);assert(unlock_history_snapshot(ring)==terminal);unlock_refresh();unlock_close();assert(atomic_load(&tab5_unlock_history[1])==terminal);
    preview_wifi_only=false;puts("UNLOCK_HISTORY_OK wrap8_order_failure_then_success_bounded_concurrent_reader_cancel_dedupe_wifi_failure_latched_channels");
    if(created)cJSON_DeleteItemFromObject(shown,"connectionHealth");
    preview_computer=(computer_remote_state){0};wifi_connected=saved_wifi;last_received=saved_received;computer_refresh();page(5);
    // Finish the pressed/disabled theme transitions from the simulated taps.
    lv_tick_inc(400);lv_timer_handler();
    preview_now_us=preparation_saved_clock;
    #undef PREP_ADVANCE
    #undef PREP_READ
    #undef PREP_READY
    puts("COMPUTER_CONTROL_UI_OK volume_release_mute_launch_lock_return_playback_capabilities_offline_legacy_fonts");
}
