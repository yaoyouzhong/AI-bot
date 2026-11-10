static unsigned workspace_preview_clicks;
static void workspace_preview_tab_click(lv_event_t *event) {
    (void)event;workspace_preview_clicks++;
}

static void check_workspace_brand_tabs(void) {
    reply_open_state_setup(1);
    codex_draft *draft=codex_draft_for(codex_selected,false);
    assert(draft&&codex_store_text(draft,"",false));
    lv_obj_update_layout(lv_screen_active());
    assert(lv_obj_get_style_y(codex_list,0)==136);
    assert(lv_obj_get_height(workspace_buttons[1])==64&&lv_obj_get_width(workspace_buttons[1])==342);
    assert(lv_image_get_src(lv_obj_get_child(workspace_buttons[1],0))==&workspace_claude_wide_image);
    capture("provider-tabs-one");

    cJSON *codex=cJSON_Duplicate(item(shown,"claudeTasks"),true);
    cJSON_ReplaceItemInObject(cJSON_GetArrayItem(item(codex,"tasks"),0),"id",cJSON_CreateString("22222222-2222-4222-8222-222222222222"));
    cJSON_AddItemToObject(shown,"codexTasks",codex);
    render_codex();
    // Short missing snapshots do not hide either tab or clear the selected
    // conversation. A sustained disappearance is tested by parity coverage.
    char selected_before[37];snprintf(selected_before,sizeof(selected_before),"%s",codex_selected);
    cJSON *missing=cJSON_DetachItemFromObject(shown,"claudeTasks");
    preview_now_us+=1000000;last_received=preview_now_us/1000;render_codex();
    assert(!lv_obj_has_flag(workspace_buttons[1],LV_OBJ_FLAG_HIDDEN));
    assert(workspace_client==1&&!strcmp(codex_selected,selected_before));
    cJSON_AddItemToObject(shown,"claudeTasks",missing);
    missing=cJSON_DetachItemFromObject(shown,"codexTasks");
    preview_now_us+=1000000;last_received=preview_now_us/1000;render_codex();
    assert(!lv_obj_has_flag(workspace_buttons[0],LV_OBJ_FLAG_HIDDEN)&&workspace_client==1);
    cJSON_AddItemToObject(shown,"codexTasks",missing);render_codex();
    assert(lv_image_get_src(lv_obj_get_child(workspace_buttons[0],0))==&workspace_codex_wide_image);
    for(unsigned i=0;i<2;i++) {
        assert(lv_obj_get_child_count(workspace_buttons[i])==2);
        assert(!lv_obj_has_flag(lv_obj_get_child(workspace_buttons[i],0),LV_OBJ_FLAG_CLICKABLE));
    }
    visible(session_unread_clients[0],true);visible(session_unread_clients[1],true);
    ui_text(lv_obj_get_child(session_unread_clients[0],0),"1");
    ui_text(lv_obj_get_child(session_unread_clients[1],0),"9+");
    capture("provider-tabs-two-unread");
    lv_indev_t *touch=lv_indev_create();
    lv_indev_set_type(touch,LV_INDEV_TYPE_POINTER);lv_indev_set_display(touch,lv_display_get_default());
    lv_indev_set_read_cb(touch,camera_touch_read);
    lv_area_t bounds;lv_obj_get_coords(workspace_buttons[0],&bounds);
    reply_pointer_tap(touch,(lv_point_t){bounds.x1+10,bounds.y2-10});
    assert(workspace_client==0);

    // Exercise extra providers using the actual strip/layout, without
    // inventing additional firmware protocol clients.
    visible(workspace_buttons[0],false);visible(workspace_buttons[1],false);
    const lv_image_dsc_t *logos[]={&brand_codex_image,&brand_claude_image,&brand_deepseek_image,&brand_kimi_image,
        &brand_alibaba_image,&brand_zhipu_image,&brand_minimax_image};
    lv_obj_t *tabs[7],*badges[7];
    bool present[7]={true,true,false,true,false,false,false};
    for(int i=0;i<7;i++)tabs[i]=workspace_tab_create(workspace_tab_strip,logos[i],workspace_preview_tab_click,i,&badges[i]);
    // Check the actual two client controls in the denser layout as well as
    // generic future tabs; restore them before exercising the rest of the UI.
    lv_obj_t *mixed[]={workspace_buttons[0],workspace_buttons[1],tabs[2],tabs[3]};
    bool dense[]={true,true,true,true};
    workspace_tabs_layout(workspace_tab_strip,mixed,dense,4,0);
    assert(lv_image_get_src(lv_obj_get_child(workspace_buttons[0],0))==&workspace_codex_narrow_image);
    assert(lv_image_get_src(lv_obj_get_child(workspace_buttons[1],0))==&workspace_claude_narrow_image);
    assert(lv_image_get_scale(lv_obj_get_child(workspace_buttons[0],0))==LV_SCALE_NONE);
    assert(lv_image_get_scale(lv_obj_get_child(workspace_buttons[1],0))==LV_SCALE_NONE);
    bool two[]={true,true};workspace_tabs_layout(workspace_tab_strip,workspace_buttons,two,2,0);
    assert(lv_image_get_src(lv_obj_get_child(workspace_buttons[0],0))==&workspace_codex_wide_image);
    assert(lv_image_get_src(lv_obj_get_child(workspace_buttons[1],0))==&workspace_claude_wide_image);
    visible(workspace_buttons[0],false);visible(workspace_buttons[1],false);
    workspace_tabs_layout(workspace_tab_strip,tabs,present,7,1);
    lv_obj_update_layout(lv_screen_active());
    assert(lv_obj_get_x(tabs[3])+lv_obj_get_width(tabs[3])==342);
    assert(lv_obj_has_flag(tabs[2],LV_OBJ_FLAG_HIDDEN)&&!lv_obj_has_flag(workspace_tab_strip,LV_OBJ_FLAG_SCROLLABLE));
    capture("provider-tabs-three");
    present[2]=true;
    workspace_tabs_layout(workspace_tab_strip,tabs,present,7,1);
    for(int i=0;i<4;i++){visible(badges[i],true);ui_text(lv_obj_get_child(badges[i],0),"9+");}
    capture("provider-tabs-four");
    for(int i=0;i<4;i++) {
        lv_area_t icon,badge;
        lv_obj_get_coords(lv_obj_get_child(tabs[i],0),&icon);lv_obj_get_coords(badges[i],&badge);
        assert(icon.x2<badge.x1&&lv_obj_get_height(tabs[i])==64);
    }
    for(int i=4;i<7;i++)present[i]=true;
    workspace_tabs_layout(workspace_tab_strip,tabs,present,7,1);
    capture("provider-tabs-overflow");
    lv_obj_get_coords(workspace_tab_strip,&bounds);
    int scroll=lv_obj_get_scroll_right(workspace_tab_strip);
    assert(scroll>0&&lv_obj_get_scroll_bottom(workspace_tab_strip)==0);
    camera_touch_point=(lv_point_t){bounds.x1+280,bounds.y1+40};
    camera_touch_state=LV_INDEV_STATE_PRESSED;lv_indev_read(touch);
    for(int i=0;i<8;i++){camera_touch_point.x-=20;lv_tick_inc(18);lv_indev_read(touch);}
    camera_touch_state=LV_INDEV_STATE_RELEASED;lv_indev_read(touch);
    assert(lv_obj_get_scroll_x(workspace_tab_strip)>0&&workspace_preview_clicks==0);
    lv_obj_scroll_to_x(workspace_tab_strip,scroll,LV_ANIM_OFF);
    capture("provider-tabs-overflow-end");
    assert(lv_obj_get_scroll_right(workspace_tab_strip)==0);
    for(int i=0;i<7;i++)lv_obj_delete(tabs[i]);
    cJSON_DeleteItemFromObject(shown,"codexTasks");cJSON_DeleteItemFromObject(shown,"claudeTasks");
    preview_now_us+=9000000;last_received=preview_now_us/1000;
    render_codex();lv_obj_update_layout(lv_screen_active());
    assert(!lv_obj_has_flag(workspace_empty_label,LV_OBJ_FLAG_HIDDEN)&&lv_obj_get_scroll_x(workspace_tab_strip)==0);
    capture("provider-tabs-empty");
    lv_indev_delete(touch);
    puts("WORKSPACE_BRAND_TABS_OK actual zero/one/two providers; dense and overflow layout; pointer click and swipe; fixed list position");
}
