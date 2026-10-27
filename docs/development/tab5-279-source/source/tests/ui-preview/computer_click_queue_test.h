{
    cJSON *health=item(shown,"connectionHealth");
    if(!health){health=cJSON_CreateObject();cJSON_AddItemToObject(shown,"connectionHealth",health);}
    cJSON_AddNumberToObject(health,"computerControlVersion",1);
    cJSON_AddNumberToObject(health,"hostUnlockVersion",1);
    cJSON_AddNumberToObject(health,"wifiUnlockVersion",1);
    unsigned cases=0;
    #define CLICK_EFFECTS (preview_computer_commands+preview_host_requests+preview_wifi_requests)
    #define CLICK_SETUP(route,locked_value) do { \
        if(unlock_overlay)unlock_close();computer_pending_click=(computer_click_intent){0};computer_action_notice[0]=0; \
        preview_rpc_selection=route;preview_usb_connected=route==LINK_USB;preview_usb_available=route==LINK_USB; \
        wifi_connected=true;last_received=preview_now_us/1000;device_links.mode=route; \
        strcpy(device_links.session[route],"click-fixture");preview_keyboard=(ble_keyboard_state){.connection=7}; \
        preview_host_link=route==LINK_WIFI?LINK_NONE:route;preview_wifi_only=route==LINK_WIFI; \
        preview_host_unlock=(host_unlock_state){0};preview_wifi_unlock=(unlock_remote_state){0}; \
        preview_computer=(computer_remote_state){.ready=true,.lock_known=true,.locked=locked_value,.can_lock=true, \
            .updated=last_received,.lock_updated=last_received,.busy=true,.reading=true,.generation=10}; \
        page(14);computer_refresh(); \
    }while(0)
    #define CLICK_COMPLETE() do { \
        preview_now_us+=300000;last_received=preview_now_us/1000;preview_computer.busy=preview_computer.reading=false; \
        preview_computer.generation++;preview_computer.updated=preview_computer.lock_updated=last_received; \
    }while(0)
    for(int route=0;route<3;route++)for(int locked=0;locked<2;locked++)for(int stale_view=0;stale_view<2;stale_view++) {
        CLICK_SETUP((link_kind)route,locked);
        unsigned before=CLICK_EFFECTS,hid=preview_unlock_submissions,reads=preview_status_reads,commands=preview_computer_commands;
        if(stale_view)computer_view->busy=computer_view->reading=false;
        assert(!lv_obj_has_state(computer_lock,LV_STATE_DISABLED));
        lv_obj_send_event(computer_lock,LV_EVENT_CLICKED,NULL);
        lv_obj_send_event(computer_lock,LV_EVENT_CLICKED,NULL);
        assert(computer_pending_click.active&&CLICK_EFFECTS==before);
        computer_refresh();assert(strstr(lv_label_get_text(computer_status),"随后"));
        assert(preview_status_reads==reads); // No new poll can starve the explicit tap.
        CLICK_COMPLETE();computer_refresh();
        assert(!computer_pending_click.active&&CLICK_EFFECTS==before+1&&preview_unlock_submissions==hid);
        for(int tick=0;tick<3;tick++)computer_refresh();
        assert(CLICK_EFFECTS==before+1);cases++;
        if(locked)assert(unlock_overlay);else assert(!unlock_overlay&&preview_computer_commands==commands+1);
    }
    const char *failures[]={"page","timeout","host","session","mode","link","connection","read-failed","unknown","state-changed","no-new-generation","stale-lock"};
    for(int route=0;route<3;route++)for(int locked=0;locked<2;locked++)for(unsigned failure=0;failure<sizeof(failures)/sizeof(*failures);failure++) {
        CLICK_SETUP((link_kind)route,locked);unsigned before=CLICK_EFFECTS;
        lv_obj_send_event(computer_lock,LV_EVENT_CLICKED,NULL);assert(computer_pending_click.active);
        CLICK_COMPLETE();const char *name=failures[failure];
        if(!strcmp(name,"page"))page(11);
        if(!strcmp(name,"timeout"))preview_now_us+=3000000;
        if(!strcmp(name,"host"))preview_computer_host_epoch++;
        if(!strcmp(name,"session"))strcpy(device_links.session[route],"changed-session");
        if(!strcmp(name,"mode"))device_links.mode=-1;
        if(!strcmp(name,"link"))preview_rpc_selection=route==LINK_USB?LINK_BLE:LINK_USB;
        if(!strcmp(name,"connection")){if(route==LINK_WIFI)preview_wifi_connection++;else preview_keyboard.connection++;}
        if(!strcmp(name,"read-failed"))preview_computer.ready=false;
        if(!strcmp(name,"unknown"))preview_computer.lock_known=false;
        if(!strcmp(name,"state-changed"))preview_computer.locked=!locked;
        if(!strcmp(name,"no-new-generation"))preview_computer.generation=10;
        if(!strcmp(name,"stale-lock"))preview_computer.lock_updated-=7000;
        computer_refresh();assert(!computer_pending_click.active&&CLICK_EFFECTS==before&&!unlock_overlay);cases++;
    }
    for(int route=0;route<3;route++) {
        CLICK_SETUP((link_kind)route,false);preview_computer.reading=false;computer_refresh();
        unsigned before=CLICK_EFFECTS;lv_obj_send_event(computer_lock,LV_EVENT_CLICKED,NULL);
        CLICK_COMPLETE();computer_refresh();assert(CLICK_EFFECTS==before&&!computer_pending_click.active);cases++;
        // A normal lock immediately schedules state refresh, rather than waiting 3.5s.
        CLICK_SETUP((link_kind)route,false);CLICK_COMPLETE();computer_refresh();
        before=preview_status_reads;lv_obj_send_event(computer_lock,LV_EVENT_CLICKED,NULL);computer_refresh();
        assert(preview_status_reads>before);cases++;
    }
    #include "computer_media_click_test.h"
    #undef CLICK_SETUP
    #undef CLICK_COMPLETE
    #undef CLICK_EFFECTS
    printf("COMPUTER_CLICK_QUEUE_OK %u cases; USB/Wi-Fi/BLE lock/unlock first tap; duplicate suppression; read completion; binding/page/deadline/state cancellation; no HID; immediate post-lock status\n",cases);
    return 0;
}
