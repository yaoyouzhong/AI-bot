// Exercise the actual LVGL event/refresh path. Transport worker tests are separate.
{
    // Capture the real partial display buffer; offscreen snapshots omit cached
    // background tiles and can falsely suggest corrupted pixels after a modal.
    camera_partial_pixels=calloc(1,sizeof(preview_buffer));assert(camera_partial_pixels);
    lv_display_t *host_display=lv_display_get_default();
    lv_display_set_flush_cb(host_display,camera_partial_flush);
    lv_display_set_buffers(host_display,preview_buffer,NULL,TAB5_DRAW_BUFFER_PIXELS*2,LV_DISPLAY_RENDER_MODE_PARTIAL);
    strcpy(preview_host_labels[preview_host_selected],"MacBook Pro");
    #define HOST_CAPTURE(name) do { \
        computer_refresh();lv_obj_update_layout(lv_screen_active()); \
        lv_obj_invalidate(lv_screen_active());lv_refr_now(host_display); \
        FILE *frame=fopen(name ".ppm","wb");assert(frame);fprintf(frame,"P6\n1280 720\n255\n"); \
        for(unsigned pixel_index=0;pixel_index<1280*720;pixel_index++){ \
            uint16_t pixel=camera_partial_pixels[pixel_index]; \
            uint8_t rgb[]={(uint8_t)(((pixel>>11)&31)*255/31),(uint8_t)(((pixel>>5)&63)*255/63),(uint8_t)((pixel&31)*255/31)}; \
            assert(fwrite(rgb,1,3,frame)==3); \
        } \
        fclose(frame); \
    }while(0)
    cJSON *health=item(shown,"connectionHealth");
    if(!health){health=cJSON_CreateObject();cJSON_AddItemToObject(shown,"connectionHealth",health);}
    cJSON_AddNumberToObject(health,"computerControlVersion",1);
    cJSON_AddNumberToObject(health,"wifiUnlockVersion",1);
    wifi_connected=true;last_received=preview_now_us/1000;
    unsigned hid=preview_unlock_submissions,grants=preview_oneclick_requests,wifi=preview_wifi_requests;
    page(14);
    for(int route=0;route<2;route++) {
        last_received=preview_now_us/1000;
        preview_host_link=route?LINK_BLE:LINK_USB;
        // A permitted USB host must not be blocked by Wi-Fi being disabled.
        preview_wifi_only=!route;cJSON_SetNumberValue(item(health,"wifiUnlockVersion"),0);
        preview_computer=(computer_remote_state){.ready=true,.locked=true,.lock_known=true,
            .updated=preview_now_us/1000,.lock_updated=preview_now_us/1000};
        // Host RPC must work without HID subscription or keyboard readiness.
        preview_keyboard=(ble_keyboard_state){0};preview_usb_available=false;
        preview_host_unlock=(host_unlock_state){0};
        unsigned requests=preview_host_requests;
        computer_refresh();lv_obj_send_event(computer_lock,LV_EVENT_CLICKED,NULL);
        assert(unlock_host&&unlock_auto&&!unlock_wifi&&unlock_overlay&&unlock_sent);
        assert(unlock_using_usb==!route&&preview_host_requests==requests+1);
        assert(lv_obj_has_flag(unlock_mask,LV_OBJ_FLAG_HIDDEN));
        assert(lv_obj_has_flag(unlock_text_keys,LV_OBJ_FLAG_HIDDEN));
        assert(strstr(lv_label_get_text(unlock_hint),route?"蓝牙":"USB"));
        if(route)HOST_CAPTURE("mac-ble-unlock-pending");else HOST_CAPTURE("mac-usb-unlock-pending");
        // Repeated clicks and ticks cannot create a second request.
        lv_obj_send_event(computer_lock,LV_EVENT_CLICKED,NULL);unlock_refresh();
        assert(preview_host_requests==requests+1);
        // Due status polls must not contend with the local unlock lease.
        unsigned status_reads=preview_status_reads;
        for(int tick=0;tick<4;tick++) {
            preview_now_us+=2000000;last_received=preview_now_us/1000;
            computer_refresh();assert(preview_status_reads==status_reads);
        }
        preview_host_unlock=(host_unlock_state){.finished=true,.unlocked=true,.current=true};
        // Terminal host reply releases polling for independent desktop proof.
        computer_refresh();assert(preview_status_reads==status_reads+1);
        unlock_refresh();assert(!unlock_success_until); // ordinary state still locked
        preview_computer.locked=false;preview_computer.lock_updated=unlock_submitted_at;unlock_refresh();assert(!unlock_success_until); // stale sample
        preview_now_us+=1000;last_received=preview_now_us/1000;
        preview_computer.lock_updated=preview_now_us/1000;unlock_refresh();
        assert(unlock_success_until&&!strcmp(lv_label_get_text(unlock_hint),"电脑已解锁"));
        if(route)HOST_CAPTURE("mac-ble-unlock-success");else HOST_CAPTURE("mac-usb-unlock-success");
        preview_now_us+=501000;unlock_refresh();assert(!unlock_overlay);
        assert(preview_unlock_submissions==hid&&preview_oneclick_requests==grants&&preview_wifi_requests==wifi);

        // Disconnect after a success reply may not display success for another host.
        preview_computer.locked=true;preview_computer.lock_updated=preview_now_us/1000;
        unlock_open();preview_host_unlock=(host_unlock_state){.finished=true,.unlocked=true,.current=false};
        preview_now_us+=1000;preview_computer.locked=false;preview_computer.lock_updated=preview_now_us/1000;
        unlock_refresh();assert(unlock_auto_failed&&!unlock_success_until);
        assert(strstr(lv_label_get_text(unlock_hint),"解锁未完成"));
        unlock_close();

        // Explicit cancel and lease timeout stop this route without HID/Wi-Fi fallback.
        preview_host_unlock=(host_unlock_state){0};unlock_open();unsigned cancels=preview_host_cancels;
        lv_obj_send_event(unlock_close_button,LV_EVENT_CLICKED,NULL);
        assert(!unlock_overlay&&preview_host_cancels==cancels+1);
        preview_host_unlock=(host_unlock_state){0};unlock_open();requests=preview_host_requests;
        preview_now_us+=40000000;unlock_refresh();
        assert(unlock_auto_failed&&!unlock_success_until&&!preview_host_unlock.busy);
        unlock_refresh();assert(preview_host_requests==requests);
        assert(preview_unlock_submissions==hid&&preview_oneclick_requests==grants&&preview_wifi_requests==wifi);
        unlock_close();
        preview_host_unlock=(host_unlock_state){0};unlock_open();
        preview_host_unlock=(host_unlock_state){.finished=true,.error=4,.current=true};unlock_refresh();
        assert(unlock_auto_failed&&strstr(lv_label_get_text(unlock_hint),"请在 Mac 开启"));
        if(route)HOST_CAPTURE("mac-ble-unlock-disabled");
        unlock_close();
    }
    preview_host_link=LINK_BLE;preview_wifi_only=true;preview_wifi_unlock=(unlock_remote_state){0};
    cJSON_SetNumberValue(item(health,"wifiUnlockVersion"),1);
    preview_computer.locked=true;preview_computer.lock_updated=preview_now_us/1000;
    unsigned requests=preview_host_requests;unlock_open();
    assert(unlock_wifi&&!unlock_host&&preview_wifi_requests==wifi+1&&preview_host_requests==requests);
    unlock_close();preview_wifi_only=false;preview_host_link=LINK_NONE;
    preview_keyboard=(ble_keyboard_state){.connected=true,.secure=true,.subscribed=true,.bridge_verified=true,.ready=true,.connection=1};
    preview_computer.locked=true;preview_computer.lock_updated=preview_now_us/1000;unlock_open();
    assert(!unlock_wifi&&!unlock_host&&!unlock_auto); // Windows manual credential UI retained.
    assert(!lv_obj_has_flag(unlock_mode_button,LV_OBJ_FLAG_HIDDEN));
    unlock_close();
    // Without a local capability, retain the existing Wi-Fi permission guidance.
    preview_wifi_only=true;cJSON_SetNumberValue(item(health,"wifiUnlockVersion"),0);
    last_received=preview_now_us/1000;preview_computer.updated=last_received;
    computer_refresh();lv_obj_send_event(computer_lock,LV_EVENT_CLICKED,NULL);
    assert(!unlock_overlay&&strstr(lv_label_get_text(computer_status),"Wi-Fi 一键解锁"));
    #undef HOST_CAPTURE
    puts("HOST_UNLOCK_UI_OK USB/BLE busy status suppression and terminal resumption, single request, no HID credential, fresh desktop confirmation, cancel, disconnect, deadline, Wi-Fi and Windows routing");
    return 0;
}
