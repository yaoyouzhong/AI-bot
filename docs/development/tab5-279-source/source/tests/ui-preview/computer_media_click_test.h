{
    // A real LVGL tap overlaps an ordinary read; no command may be lost or replayed.
    computer_pending_click=(computer_click_intent){0};computer_action_notice[0]=0;
    preview_rpc_selection=LINK_USB;preview_usb_connected=preview_usb_available=true;
    wifi_connected=true;last_received=preview_now_us/1000;device_links.mode=LINK_USB;
    strcpy(device_links.session[LINK_USB],"media-click-fixture");preview_keyboard=(ble_keyboard_state){.connection=7};
    preview_computer=(computer_remote_state){.ready=true,.media_available=true,.can_play_pause=true,
        .can_previous=true,.can_next=true,.playing=true,.busy=true,.reading=true,
        .updated=last_received,.generation=10};
    page(5);computer_refresh();unsigned media_before=preview_computer_commands;
    assert(!lv_obj_has_state(music_control_buttons[1],LV_STATE_DISABLED));
    lv_obj_send_event(music_control_buttons[1],LV_EVENT_CLICKED,NULL);
    assert(preview_computer_commands==media_before);
    preview_now_us+=300000;last_received=preview_now_us/1000;
    preview_computer.busy=preview_computer.reading=false;preview_computer.updated=last_received;preview_computer.generation++;
    computer_refresh();
    if(getenv("TAB5_EXPECT_MEDIA_DROP")) {
        assert(preview_computer_commands==media_before);
        puts("MEDIA_CLICK_BASELINE_DROP_REPRODUCED no command after overlapping read completes");return 0;
    }
    assert(preview_computer_commands==media_before+1);
    assert(!strcmp(preview_computer_op,"media")&&!strcmp(preview_computer_target,"play-pause"));
    unsigned media_cases=1;
    #define MEDIA_SETUP(route) do { \
        CLICK_SETUP(route,false); \
        preview_computer.media_available=preview_computer.can_play_pause=preview_computer.can_previous=preview_computer.can_next=true; \
        preview_computer.playing=true;computer_poll_at=last_received; \
        cJSON_ReplaceItemInObject(item(shown,"music"),"title",cJSON_CreateString("Synthetic track")); \
        cJSON_ReplaceItemInObject(item(shown,"music"),"artist",cJSON_CreateString("Synthetic artist")); \
        cJSON_ReplaceItemInObject(item(shown,"music"),"album",cJSON_CreateString("Synthetic album")); \
        page(5);computer_refresh(); \
    }while(0)
    const char *targets[]={"previous","play-pause","next"};
    for(int route=0;route<3;route++)for(int action=0;action<3;action++)for(int stale=0;stale<2;stale++) {
        MEDIA_SETUP((link_kind)route);unsigned before=preview_computer_commands,reads=preview_status_reads;
        if(stale)computer_view->busy=computer_view->reading=false;
        assert(!lv_obj_has_state(music_control_buttons[action],LV_STATE_DISABLED));
        lv_obj_send_event(music_control_buttons[action],LV_EVENT_CLICKED,NULL);
        lv_obj_send_event(music_control_buttons[(action+1)%3],LV_EVENT_CLICKED,NULL);
        computer_refresh();assert(preview_computer_commands==before&&preview_status_reads==reads);
        assert(strstr(lv_label_get_text(music_control_hint),"随后"));
        CLICK_COMPLETE();computer_refresh();
        assert(preview_computer_commands==before+1&&!strcmp(preview_computer_op,"media"));
        assert(!strcmp(preview_computer_target,targets[action]));
        for(int tick=0;tick<3;tick++)computer_refresh();
        assert(preview_computer_commands==before+1);media_cases++;
    }
    const char *media_failures[]={"page","timeout","clock-back","host","session","mode","link","connection",
        "read-failed","no-generation","capability","playing-changed","title-changed","artist-changed","album-changed","disconnected","not-fresh"};
    for(int route=0;route<3;route++)for(int action=0;action<3;action++)for(unsigned f=0;f<sizeof(media_failures)/sizeof(*media_failures);f++) {
        MEDIA_SETUP((link_kind)route);unsigned before=preview_computer_commands;
        lv_obj_send_event(music_control_buttons[action],LV_EVENT_CLICKED,NULL);CLICK_COMPLETE();const char *failure=media_failures[f];
        if(!strcmp(failure,"page"))page(11);
        if(!strcmp(failure,"timeout"))preview_now_us+=3000000;
        if(!strcmp(failure,"clock-back"))preview_now_us-=4000000;
        if(!strcmp(failure,"host"))preview_computer_host_epoch++;
        if(!strcmp(failure,"session"))strcpy(device_links.session[route],"other-session");
        if(!strcmp(failure,"mode"))device_links.mode=-1;
        if(!strcmp(failure,"link"))preview_rpc_selection=route==LINK_USB?LINK_BLE:LINK_USB;
        if(!strcmp(failure,"connection")){if(route==LINK_WIFI)preview_wifi_connection++;else preview_keyboard.connection++;}
        if(!strcmp(failure,"read-failed"))preview_computer.ready=false;
        if(!strcmp(failure,"no-generation"))preview_computer.generation=10;
        if(!strcmp(failure,"capability"))preview_computer.media_available=false;
        if(!strcmp(failure,"playing-changed"))preview_computer.playing=false;
        if(!strcmp(failure,"title-changed"))cJSON_ReplaceItemInObject(item(shown,"music"),"title",cJSON_CreateString("Other track"));
        if(!strcmp(failure,"artist-changed"))cJSON_ReplaceItemInObject(item(shown,"music"),"artist",cJSON_CreateString("Other artist"));
        if(!strcmp(failure,"album-changed"))cJSON_ReplaceItemInObject(item(shown,"music"),"album",cJSON_CreateString("Other album"));
        if(!strcmp(failure,"disconnected")){preview_rpc_selection=LINK_NONE;preview_usb_connected=wifi_connected=ble_connected=false;}
        if(!strcmp(failure,"not-fresh"))preview_computer.updated-=7000;
        computer_refresh();if(preview_computer_commands!=before)fprintf(stderr,"MEDIA_CANCEL_FAILURE route=%d action=%d guard=%s\n",route,action,failure);assert(preview_computer_commands==before);
        computer_refresh();assert(preview_computer_commands==before);media_cases++;
    }
    for(int route=0;route<3;route++)for(int action=0;action<3;action++) {
        MEDIA_SETUP((link_kind)route);preview_computer.reading=false;computer_refresh();
        unsigned before=preview_computer_commands;lv_obj_send_event(music_control_buttons[action],LV_EVENT_CLICKED,NULL);
        CLICK_COMPLETE();computer_refresh();assert(preview_computer_commands==before);media_cases++;
        MEDIA_SETUP((link_kind)route);CLICK_COMPLETE();computer_poll_at=last_received;computer_refresh();
        before=preview_computer_commands;lv_obj_send_event(music_control_buttons[action],LV_EVENT_CLICKED,NULL);
        computer_refresh();assert(preview_computer_commands==before+1&&!strcmp(preview_computer_target,targets[action]));media_cases++;
    }
    #undef MEDIA_SETUP
    printf("MEDIA_CLICK_QUEUE_OK %u cases; USB/Wi-Fi/BLE; first tap, stale view, duplicate suppression, no poll starvation, bound/page/deadline/track/play-state cancellation, immediate action, no replay\n",media_cases);
}
