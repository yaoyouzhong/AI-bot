static void check_health_disconnected_redraw(void) {
    // Drain the transition frame, then ensure repeated offline samples leave
    // the settled page untouched instead of replaying cached healthy values.
    health_notice_tick(preview_now_us/1000);
    lv_obj_update_layout(lv_screen_active());lv_refr_now(lv_display_get_default());
    assert(!lv_display_get_default()->inv_p);
    for(int i=0;i<40;i++) {
        health_notice_tick(preview_now_us/1000);
        assert(!lv_display_get_default()->inv_p);
    }
}
static void check_runtime_health_ui(const char *path) {
    FILE *file=fopen(path,"rb");assert(file);fseek(file,0,SEEK_END);long size=ftell(file);assert(size>0&&size<524288);rewind(file);
    char *buffer=calloc(1,(size_t)size+1);assert(buffer&&fread(buffer,1,(size_t)size,file)==(size_t)size);fclose(file);
    cJSON *fixtures=cJSON_Parse(buffer);free(buffer);assert(fixtures);
    cJSON *scenario=NULL;int count=0;
    prefs.alerts=true;preview_now_us=20000000;last_received=20000;alert_until=0;
    unsigned reply_sounds=preview_chimes;
    cJSON_ArrayForEach(scenario,item(fixtures,"scenarios")) {
        cJSON_Delete(shown);shown=cJSON_Duplicate(item(scenario,"data"),true);assert(shown);
        health_close(NULL);health_detail_close(NULL);page(0);render();alert_visual_tick(20000);health_notice_tick(20000);
        const char *name=text(scenario,"name");bool abnormal=strcmp(name,"normal")!=0;
        assert(health_banner_active==abnormal);
        assert(lv_obj_has_flag(health_banner_icon,LV_OBJ_FLAG_HIDDEN)==!abnormal);
        if(abnormal){
            cJSON *alert=cJSON_GetArrayItem(item(item(shown,"runtimeHealth"),"alerts"),0);
            const char *title=!strcmp(name,"commit-pressure")?"提交量偏高":!strcmp(name,"paging-pressure")?"换页压力大":
                !strcmp(name,"codex-disconnect")?"Codex 断连":!strcmp(name,"codex-service")?"Codex 异常":
                !strcmp(name,"bridge-stall")?"采样停滞":text(alert,"title");
            char expected[192];snprintf(expected,sizeof(expected),"系统监控：%s",title);
            assert(!strcmp(lv_label_get_text(banner),expected));
            assert(lv_color_eq(lv_obj_get_style_text_color(health_banner_icon,0),lv_color_hex(health_color(text(alert,"state")))));
            lv_obj_update_layout(lv_screen_active());lv_point_t fit;
            lv_text_get_size(&fit,expected,lv_obj_get_style_text_font(banner,0),0,0,LV_COORD_MAX,LV_TEXT_FLAG_NONE);
            assert(fit.x<=lv_obj_get_content_width(banner));
        }
        else assert(!lv_label_get_text(banner)[0]);
        char output[80];snprintf(output,sizeof(output),"runtime-health-home-%s",name);capture(output);
        uint64_t sequence=health_notice_sequence;unsigned health_sounds=preview_health_chimes;
        health_notice_tick(20001);assert(health_notice_sequence==sequence&&preview_health_chimes==health_sounds);
        assert(preview_chimes==reply_sounds);if(abnormal)assert(health_sounds>0);
        if(abnormal){codex_notice_open(NULL);assert(current_page==4&&health_open);}else health_show(NULL);
        page(0);assert(!health_open);assert(lv_obj_has_flag(health_header_entry,LV_OBJ_FLAG_HIDDEN));
        page(4);assert(!strcmp(lv_label_get_text(workbench_title),"系统"));assert(!lv_obj_has_flag(health_header_entry,LV_OBJ_FLAG_HIDDEN));
        if(!strcmp(name,"normal"))capture("runtime-health-system-entry");
        lv_obj_send_event(health_header_entry,LV_EVENT_CLICKED,NULL);assert(health_open);assert(!lv_obj_has_flag(health_header_entry,LV_OBJ_FLAG_HIDDEN));
        assert(lv_obj_has_state(health_header_entry,LV_STATE_CHECKED));
        lv_obj_send_event(health_system_entry,LV_EVENT_CLICKED,NULL);assert(!health_open);
        assert(lv_obj_has_state(health_system_entry,LV_STATE_CHECKED));
        lv_obj_send_event(health_header_entry,LV_EVENT_CLICKED,NULL);assert(health_open);
        assert(!lv_obj_has_flag(health_layer,LV_OBJ_FLAG_HIDDEN));
        assert(!strstr(lv_label_get_text(health_vpn),"中断"));
        assert(health_trend_values[9]==(int32_t)num(cJSON_GetArrayItem(item(item(shown,"runtimeHealth"),"latencyHistory"),9),"milliseconds"));
        if(!strcmp(name,"pac-fault")){assert(!strcmp(lv_label_get_text(health_pac),"异常"));assert(strstr(lv_label_get_text(health_advice),"重启"));}
        snprintf(output,sizeof(output),"runtime-health-%s",name);capture(output);
        // Click cards into complete text; no diagnostic is lost to the compact page.
        lv_obj_send_event(health_network_card,LV_EVENT_CLICKED,NULL);
        assert(!lv_obj_has_flag(health_detail_layer,LV_OBJ_FLAG_HIDDEN));
        assert(!strstr(lv_label_get_text(health_detail_text),"Codex"));
        assert(!strstr(lv_label_get_text(health_detail_text),"HTTP"));
        if(!strcmp(name,"pac-fault")){
            assert(strstr(lv_label_get_text(health_detail_reason),"脚本不可用"));assert(strstr(lv_label_get_text(health_detail_action),"重启"));
            assert(strstr(lv_label_get_text(health_detail_text),"400 ms"));capture("runtime-health-network-details");
        }
        if(!strcmp(name,"vpn-slow")){assert(strstr(lv_label_get_text(health_detail_reason),"响应变慢"));capture("runtime-health-slow-details");}
        if(!strcmp(name,"normal")){assert(!strcmp(lv_label_get_text(health_detail_action),"无需处理"));capture("runtime-health-normal-details");}
        if(!strcmp(name,"chat-timeout")||!strcmp(name,"assets-connect")||!strcmp(name,"chat-http")) {
            const char *expected=!strcmp(name,"chat-timeout")?"ChatGPT 连接超时":!strcmp(name,"assets-connect")?"资源服务连接失败":"ChatGPT 响应异常";
            assert(!strcmp(lv_label_get_text(health_alarm_title),expected));
            assert(!strcmp(lv_label_get_text(health_detail_reason),expected));
            assert(!strcmp(lv_label_get_text(health_detail_action),!strcmp(name,"assets-connect")?"检查资源服务连接":"检查 ChatGPT 连接"));
            assert(strstr(lv_label_get_text(health_detail_text),!strcmp(name,"assets-connect")?"ChatGPT 仍可连接":"资源服务仍可连接"));
            snprintf(output,sizeof(output),"runtime-health-details-%s",name);capture(output);
        }
        lv_obj_send_event(workbench_back,LV_EVENT_CLICKED,NULL);assert(lv_obj_has_flag(health_detail_layer,LV_OBJ_FLAG_HIDDEN));assert(health_open);
        health_summary_detail(NULL);assert(!lv_obj_has_flag(health_detail_layer,LV_OBJ_FLAG_HIDDEN));
        bool connection=!strcmp(name,"codex-disconnect")||!strcmp(name,"codex-service")||!strcmp(name,"bridge-stall");
        bool pressure_case=!strcmp(name,"system-busy")||!strcmp(name,"commit-pressure")||!strcmp(name,"paging-pressure");
        assert(!strcmp(lv_label_get_text(health_detail_title),connection?"连接与监控详情":pressure_case?"系统负载详情":"VPN / PAC 详情"));
        if(!strcmp(name,"codex-disconnect")){assert(strstr(lv_label_get_text(health_detail_reason),"Codex"));assert(strstr(lv_label_get_text(health_detail_action),"VPN"));capture("runtime-health-codex-details");}
        if(!strcmp(name,"codex-service")){assert(strstr(lv_label_get_text(health_detail_reason),"服务"));assert(!strcmp(lv_label_get_text(health_detail_action),"稍后重试"));capture("runtime-health-service-details");}
        if(!strcmp(name,"bridge-stall")){assert(strstr(lv_label_get_text(health_detail_reason),"采样"));assert(strstr(lv_label_get_text(health_detail_action),"重启"));capture("runtime-health-bridge-details");}
        if(!strcmp(name,"commit-pressure")){assert(strstr(lv_label_get_text(health_detail_reason),"提交"));assert(strstr(lv_label_get_text(health_detail_text),"95%"));capture("runtime-health-commit-details");}
        if(!strcmp(name,"paging-pressure")){assert(strstr(lv_label_get_text(health_detail_reason),"换页"));capture("runtime-health-paging-details");}
        if(!strcmp(name,"system-busy")){
            assert(strstr(lv_label_get_text(health_detail_text),"97%"));assert(strstr(lv_label_get_text(health_detail_text),"95%"));
            assert(!strstr(lv_label_get_text(health_detail_text),"换页"));assert(strstr(lv_label_get_text(health_detail_action),"减少"));
            capture("runtime-health-system-details");
        }
        health_detail_close(NULL);
        lv_obj_send_event(workbench_back,LV_EVENT_CLICKED,NULL);assert(!health_open&&current_page==4);assert(!lv_obj_has_flag(health_header_entry,LV_OBJ_FLAG_HIDDEN));health_show(NULL);
        if(!strcmp(name,"normal")||!strcmp(name,"pac-fault")){
            health_close(NULL);assert(!lv_obj_has_flag(health_header_entry,LV_OBJ_FLAG_HIDDEN));render_system();
            capture(!strcmp(name,"normal")?"runtime-health-computer-status":"runtime-health-computer-pac-fault");health_show(NULL);
        }
        count++;
    }
    assert(count==12);
    // Full service/cause names fit without truncation, including DNS and TLS.
    const char *network_titles[]={"ChatGPT 连接超时","ChatGPT 域名解析失败","ChatGPT 安全连接失败","ChatGPT 连接失败","ChatGPT 响应异常",
        "资源服务连接超时","资源服务域名解析失败","资源服务安全连接失败","资源服务连接失败","资源服务响应异常"};
    cJSON *network_alert=cJSON_GetArrayItem(item(item(shown,"runtimeHealth"),"alerts"),0);assert(network_alert);health_close(NULL);page(0);
    for(unsigned n=0;n<10;n++) {
        cJSON_ReplaceItemInObject(network_alert,"key",cJSON_CreateString("vpn"));cJSON_ReplaceItemInObject(network_alert,"title",cJSON_CreateString(network_titles[n]));health_notice_tick(20000);lv_obj_update_layout(lv_screen_active());
        char full[192];snprintf(full,sizeof(full),"系统监控：%s",network_titles[n]);
        if(strcmp(lv_label_get_text(banner),full)){lv_point_t needed;lv_text_get_size(&needed,full,lv_obj_get_style_text_font(banner,0),0,0,LV_COORD_MAX,LV_TEXT_FLAG_NONE);fprintf(stderr,"MONITOR_TITLE_FIT n=%u needed=%d available=%d actual=%s\n",n,(int)needed.x,(int)lv_obj_get_content_width(banner),lv_label_get_text(banner));}
        assert(!strcmp(lv_label_get_text(banner),full));
        lv_point_t size;lv_text_get_size(&size,full,lv_obj_get_style_text_font(banner,0),0,0,LV_COORD_MAX,LV_TEXT_FLAG_NONE);assert(size.x<=lv_obj_get_content_width(banner));
    }
    page(4);lv_obj_update_layout(lv_screen_active());assert(lv_obj_get_x(health_segments)+lv_obj_get_width(health_segments)<lv_obj_get_x(banner));assert(lv_obj_get_x(banner)+lv_obj_get_width(banner)<lv_obj_get_x(link_buttons[0]));
    // Warning/error identity, readable short titles and task-banner priority.
    cJSON *notice_fixture=cJSON_Duplicate(shown,true);assert(notice_fixture);
    health_close(NULL);page(0);
    cJSON *first_alert=cJSON_GetArrayItem(item(item(shown,"runtimeHealth"),"alerts"),0);assert(first_alert);
    cJSON_ReplaceItemInObject(first_alert,"title",cJSON_CreateString("内存紧张"));
    cJSON_ReplaceItemInObject(first_alert,"key",cJSON_CreateString("memory"));
    cJSON_ReplaceItemInObject(first_alert,"state",cJSON_CreateString("warning"));
    cJSON_ReplaceItemInObject(item(shown,"systemMetrics"),"memoryPercent",cJSON_CreateNumber(95));render();
    alert_visual_tick(20000);health_notice_tick(20000);lv_obj_update_layout(lv_screen_active());
    assert(!strcmp(lv_label_get_text(banner),"系统监控：内存紧张"));
    lv_point_t title_size;lv_text_get_size(&title_size,lv_label_get_text(banner),lv_obj_get_style_text_font(banner,0),0,0,LV_COORD_MAX,LV_TEXT_FLAG_NONE);
    assert(title_size.x<=lv_obj_get_content_width(banner));
    assert(lv_obj_get_x(banner)+lv_obj_get_width(banner)<lv_obj_get_x(link_buttons[0]));
    assert(lv_color_eq(lv_obj_get_style_text_color(health_banner_icon,0),lv_color_hex(AMBER)));
    capture("runtime-health-home-memory-warning");
    cJSON_ReplaceItemInObject(first_alert,"state",cJSON_CreateString("error"));health_notice_tick(20000);
    assert(lv_color_eq(lv_obj_get_style_text_color(health_banner_icon,0),lv_color_hex(RED)));
    capture("runtime-health-home-memory-error");
    alert_until=21000;ui_text(banner,"Codex：等待你的操作");health_notice_tick(20000);
    assert(!health_banner_active&&lv_obj_has_flag(health_banner_icon,LV_OBJ_FLAG_HIDDEN));
    assert(!strcmp(lv_label_get_text(banner),"Codex：等待你的操作"));
    alert_until=0;health_notice_tick(20000);
    cJSON_Delete(shown);shown=notice_fixture;health_show(NULL);
    // Missing or disabled observations must never look healthy.
    cJSON *saved=cJSON_Duplicate(shown,true),*check=NULL;
    cJSON_ArrayForEach(check,item(item(shown,"runtimeHealth"),"checks"))cJSON_SetValuestring(item(check,"state"),"unknown");
    health_detail_group(1);assert(!strcmp(lv_label_get_text(health_detail_reason),"检测待确认"));assert(strcmp(lv_label_get_text(health_detail_action),"无需处理"));
    cJSON_ArrayForEach(check,item(item(shown,"runtimeHealth"),"checks"))cJSON_SetValuestring(item(check,"state"),"disabled");
    health_detail_refresh();assert(!strcmp(lv_label_get_text(health_detail_reason),"监控已关闭"));
    cJSON_Delete(shown);shown=saved;health_detail_close(NULL);
    // A live bridge with a stopped monitor must also lose its green state.
    cJSON_SetNumberValue(item(item(shown,"runtimeHealth"),"epochUtc"),num(shown,"epochUtc")-20);
    health_detail_group(1);health_render();assert(strstr(lv_label_get_text(health_vpn),"中断"));assert(health_trend_values[9]==LV_CHART_POINT_NONE);
    assert(strstr(lv_label_get_text(health_detail_reason),"中断"));assert(strstr(lv_label_get_text(health_detail_action),"桥接连接"));capture("runtime-health-stale-details");health_detail_close(NULL);
    cJSON_SetNumberValue(item(item(shown,"runtimeHealth"),"epochUtc"),num(shown,"epochUtc"));
    preview_now_us+=9000000;health_render();assert(strstr(lv_label_get_text(health_vpn),"中断"));capture("runtime-health-disconnected");
    check_health_disconnected_redraw();
    health_detail_group(1);check_health_disconnected_redraw();health_detail_close(NULL);
    // New authenticated data must refresh the same open page automatically.
    last_received=preview_now_us/1000;health_notice_tick(last_received);
    assert(!strstr(lv_label_get_text(health_vpn),"中断"));
    assert(health_trend_values[9]!=LV_CHART_POINT_NONE);
    preview_now_us+=9000000;health_notice_tick(preview_now_us/1000);
    check_health_disconnected_redraw();
    health_close(NULL);page(0);alert_visual_tick(29000);health_notice_tick(29000);assert(!strcmp(lv_label_get_text(banner),"系统监控：监控已中断"));capture("runtime-health-home-disconnected");
    cJSON_Delete(fixtures);
    puts("RUNTIME_HEALTH_UI_OK protocol_fixtures, compact_home, System_detail_entry, PAC_restart_advice, repeated_event, compact_cause_and_advice, hierarchical_back, unknown_disabled, stale_monitor, disconnected, offline_no_redraw, automatic_reconnect");
}
