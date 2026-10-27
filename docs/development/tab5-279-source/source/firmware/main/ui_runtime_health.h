// Detailed monitoring stays under System. The shared banner carries only a
// short actionable title; stale host data can never retain a green status.
static lv_obj_t *health_layer,*health_network_card,*health_vpn,*health_latency,*health_pac;
static lv_obj_t *health_cpu,*health_memory,*health_queue,*health_disk,*health_system_state,*health_bars[2];
static lv_obj_t *health_trend,*health_trend_scale,*health_trend_note;
static lv_chart_series_t *health_trend_series;
static int32_t health_trend_values[10];
static bool health_trend_ready;
static lv_obj_t *health_alarm_title,*health_advice,*health_status_dot;
static lv_obj_t *health_detail_layer,*health_detail_title,*health_detail_reason_caption,*health_detail_reason,*health_detail_text,*health_detail_action,*health_detail_advice;
static int health_detail_selected;
static void health_detail_refresh(void);
static bool health_banner_active;
static lv_obj_t *health_banner_icon;
static char health_notice_session[40];
static uint64_t health_notice_sequence;
static uint32_t health_color(const char *state) {
    return !strcmp(state,"error")?RED:!strcmp(state,"warning")?AMBER:!strcmp(state,"ok")?GREEN:MUTED;
}
static cJSON *health_check(const char *key) {
    cJSON *check=NULL;
    cJSON_ArrayForEach(check,item(item(shown,"runtimeHealth"),"checks"))if(!strcmp(text(check,"key"),key))return check;
    return NULL;
}
static bool health_fresh(void) {
    cJSON *h=item(shown,"runtimeHealth");
    return cJSON_IsObject(h)&&last_received>0&&esp_timer_get_time()/1000-last_received<8000&&
        num(h,"epochUtc")>0&&num(shown,"epochUtc")-num(h,"epochUtc")<10;
}
static void health_number(cJSON *obj,const char *key,char *out,size_t size,const char *unit) {
    cJSON *value=item(obj,key);if(cJSON_IsNumber(value))snprintf(out,size,"%.0f%s",value->valuedouble,unit);else snprintf(out,size,"--");
}
static void health_compact(lv_obj_t *label,cJSON *check) {
    const char *state=text(check,"state");
    ui_text(label,!strcmp(state,"ok")?"正常":!strcmp(state,"warning")?"变慢":!strcmp(state,"error")?"异常":
        !strcmp(state,"observing")?"观察中":!strcmp(state,"disabled")?"未启用":"待确认");tint(label,health_color(state));
}
static void health_chart(cJSON *health,bool fresh) {
    int32_t points[10];for(int i=0;i<10;i++)points[i]=LV_CHART_POINT_NONE;
    cJSON *history=fresh?item(health,"latencyHistory"):NULL;int count=cJSON_GetArraySize(history),take=count>10?10:count;
    int32_t maximum=1000;
    for(int i=0;i<take;i++) {
        cJSON *point=cJSON_GetArrayItem(history,count-take+i),*value=item(point,"milliseconds");
        if(yes(point,"reachable")&&cJSON_IsNumber(value)){points[10-take+i]=(int32_t)LV_CLAMP(0,value->valuedouble,15000);if(points[10-take+i]>maximum)maximum=points[10-take+i];}
    }
    ui_text(health_trend_note,take?"最近 10 次":"等待采样");ui_text_fmt(health_trend_scale,"%ld ms",(long)maximum);
    if(health_trend_ready&&!memcmp(points,health_trend_values,sizeof(points)))return;
    health_trend_ready=true;memcpy(health_trend_values,points,sizeof(points));
    lv_chart_set_range(health_trend,LV_CHART_AXIS_PRIMARY_Y,0,maximum);
    for(int i=0;i<10;i++)lv_chart_set_value_by_id(health_trend,health_trend_series,i,points[i]);
    lv_chart_refresh(health_trend);
}
static void health_render(void) {
    if(!health_layer)return;
    cJSON *h=item(shown,"runtimeHealth"),*vpn=health_check("vpn"),*pac=health_check("pac"),*p=item(h,"pressure"),*m=item(shown,"systemMetrics");
    bool fresh=health_fresh();
    // Choose the final offline presentation before touching LVGL objects.
    // Replaying cached healthy values and then overwriting them invalidated
    // the left card every 100 ms even while the bridge remained disconnected.
    if(!fresh) {
        ui_text(health_vpn,h?"已中断":"待连接");tint(health_vpn,MUTED);
        ui_text(health_pac,"待确认");tint(health_pac,MUTED);
        ui_text(health_latency,"-- ms");ui_text(health_cpu,"--");ui_text(health_memory,"--");ui_text(health_queue,"--");ui_text(health_disk,"-- ms");
        tint(health_queue,MUTED);tint(health_disk,MUTED);ui_text(health_system_state,"待连接");tint(health_system_state,MUTED);
        lv_bar_set_value(health_bars[0],0,LV_ANIM_OFF);lv_bar_set_value(health_bars[1],0,LV_ANIM_OFF);
        ui_text(health_alarm_title,"监控已中断");tint(health_alarm_title,MUTED);ui_text(health_advice,"检查桥接连接");tint(health_advice,MUTED);
        ui_bg_color(health_status_dot,lv_color_hex(MUTED),0);
        health_chart(h,false);
        if(health_detail_layer&&!lv_obj_has_flag(health_detail_layer,LV_OBJ_FLAG_HIDDEN))health_detail_refresh();
        return;
    }
    health_compact(health_vpn,vpn);health_compact(health_pac,pac);
    char latency[32],cpu[24],memory[24],queue[24],disk[24];
    health_number(vpn,"latencyMs",latency,sizeof(latency)," ms");health_number(m,"cpuPercent",cpu,sizeof(cpu),"%");
    health_number(m,"memoryPercent",memory,sizeof(memory),"%");health_number(p,"processorQueue",queue,sizeof(queue),"");
    health_number(p,"diskLatencyMs",disk,sizeof(disk)," ms");
    ui_text(health_latency,latency);ui_text(health_cpu,cpu);ui_text(health_memory,memory);ui_text(health_queue,queue);ui_text(health_disk,disk);
    cJSON *cpu_check=health_check("cpu"),*memory_check=health_check("memory"),*queue_check=health_check("queue"),*disk_check=health_check("disk");
    cJSON *bottleneck=NULL; cJSON *checks[]={queue_check,disk_check,memory_check,cpu_check};bool system_known=true,observing=false;
    for(int i=0;i<4;i++) {
        const char *state=text(checks[i],"state");
        if(!strcmp(state,"warning")||!strcmp(state,"error")){if(!bottleneck)bottleneck=checks[i];}
        if(!strcmp(state,"observing"))observing=true;
        if(!strcmp(state,"unknown")||!strcmp(state,"--")||!strcmp(state,"disabled"))system_known=false;
    }
    ui_text(health_system_state,bottleneck?"负载偏高":!system_known?"待确认":observing?"观察中":"正常");
    tint(health_system_state,bottleneck?AMBER:system_known&&!observing?GREEN:MUTED);
    lv_bar_set_value(health_bars[0],fresh?percentage(m,"cpuPercent"):0,LV_ANIM_OFF);
    lv_bar_set_value(health_bars[1],fresh?percentage(m,"memoryPercent"):0,LV_ANIM_OFF);
    ui_bg_color(health_bars[0],lv_color_hex(!strcmp(text(cpu_check,"state"),"warning")?AMBER:BLUE),LV_PART_INDICATOR);
    ui_bg_color(health_bars[1],lv_color_hex(!strcmp(text(memory_check,"state"),"warning")?AMBER:GREEN),LV_PART_INDICATOR);
    tint(health_queue,health_color(text(queue_check,"state")));tint(health_disk,health_color(text(disk_check,"state")));
    cJSON *alert=cJSON_GetArrayItem(item(h,"alerts"),0);
    const char *action=text(alert,"shortAdvice");
    if(!strcmp(action,"--"))action=text(alert,"advice");
    bool healthy=system_known&&!observing&&vpn&&!strcmp(text(vpn,"state"),"ok")&&
        (!strcmp(text(pac,"state"),"ok")||!strcmp(text(pac,"state"),"disabled"));
    ui_text(health_alarm_title,alert?text(alert,"title"):healthy?"运行正常":"等待检测确认");tint(health_alarm_title,alert?health_color(text(alert,"state")):healthy?GREEN:MUTED);
    ui_bg_color(health_status_dot,lv_color_hex(alert?health_color(text(alert,"state")):healthy?GREEN:MUTED),0);
    if(alert)ui_text(health_advice,action);else ui_text_fmt(health_advice,"数据更新  %s",text(shown,"time"));tint(health_advice,MUTED);
    health_chart(h,fresh);
    if(health_detail_layer&&!lv_obj_has_flag(health_detail_layer,LV_OBJ_FLAG_HIDDEN))health_detail_refresh();
}
static void health_segments_refresh(void) {
    lv_obj_t *buttons[]={health_system_entry,health_header_entry};
    for(int i=0;i<2;i++)if(buttons[i]) {
        bool selected=(i==1)==health_open;
        lv_obj_set_style_radius(buttons[i],12,0);
        ui_bg_color(buttons[i],lv_color_hex(selected?BLUE:SURFACE),0);
        ui_text_color(buttons[i],lv_color_hex(selected?0x173642:MUTED),0);
        if(selected)lv_obj_add_state(buttons[i],LV_STATE_CHECKED);else lv_obj_remove_state(buttons[i],LV_STATE_CHECKED);
    }
}
static void health_show(lv_event_t *event) {
    (void)event;health_open=true;page(4);visible(health_layer,true);health_render();ui_text(workbench_title,"运行监控");
}
static void health_close(lv_event_t *event) {
    (void)event;health_open=false;visible(health_layer,false);visible(health_detail_layer,false);ui_text(workbench_title,"系统");
    visible(health_header_entry,current_page==4);health_segments_refresh();
}
static void health_leave_system(void) {
    health_open=false;if(health_layer)visible(health_layer,false);if(health_detail_layer)visible(health_detail_layer,false);
}
static void health_detail_close(lv_event_t *event) {(void)event;visible(health_detail_layer,false);}
static bool health_issue(cJSON *check) {
    const char *state=text(check,"state");return !strcmp(state,"warning")||!strcmp(state,"error");
}
static void health_detail_refresh(void) {
    char evidence[640]={0},suggestions[512]={0};
    const char *reason="检测待确认",*action="等待下一轮检测",*caption="检测结果";uint32_t color=MUTED;
    if(!health_fresh()) {
        reason="监控已中断";action="检查电脑桥接连接";
        snprintf(evidence,sizeof(evidence),"当前数据已过期");
        snprintf(suggestions,sizeof(suggestions),"确认桥接程序正在运行");
    } else if(!strcmp(text(health_check("cpu"),"state"),"disabled")&&!strcmp(text(health_check("memory"),"state"),"disabled")) {
        reason="监控已关闭";action="需要时启用运行监控";
    } else if(health_detail_selected==2) {
        cJSON *codex=health_check("codex"),*bridge=health_check("bridge");
        cJSON *check=health_issue(bridge)?bridge:health_issue(codex)?codex:NULL;
        if(check) {
            caption="异常原因";reason=text(check,"summary");color=health_color(text(check,"state"));
            bool local=check==bridge;
            action=local?"重启桥接程序":strstr(reason,"登录")?"检查登录状态":strstr(reason,"受限")||strstr(reason,"服务")?"稍后重试":"检查 VPN / PAC";
            snprintf(evidence,sizeof(evidence),local?"心跳或后台服务异常":"实际请求连续异常");
            snprintf(suggestions,sizeof(suggestions),local?"启动后等待监控恢复":"调整后等待实际连接恢复");
        } else {reason="连接状态待确认";action="等待实际连接结果";}
    } else if(health_detail_selected==0) {
        cJSON *vpn=health_check("vpn"),*pac=health_check("pac");
        const char *vpn_state=text(vpn,"state"),*pac_state=text(pac,"state"),*detail=text(pac,"detail"),*advice=text(pac,"advice");
        const char *summary=text(vpn,"summary");
        bool chat=!strncmp(summary,"ChatGPT ",strlen("ChatGPT ")),assets=!strncmp(summary,"资源服务",strlen("资源服务"));
        if(health_issue(pac)&&!(health_issue(vpn)&&(chat||assets))) {
            caption="异常原因";color=health_color(pac_state);
            reason=strstr(detail,"脚本服务不可用")?"PAC 脚本不可用":strstr(detail,"脚本选路失败")?"PAC 脚本选路失败":
                strstr(advice,"未按预期走代理")?"关键域名未走代理":"PAC 路径检测失败";
            bool exit_ok=!strcmp(vpn_state,"ok")||!strcmp(vpn_state,"warning");
            if(exit_ok) {
                char ms[24];health_number(vpn,"latencyMs",ms,sizeof(ms)," ms");
                snprintf(evidence,sizeof(evidence),"代理出口可响应 · %s",ms);
            } else snprintf(evidence,sizeof(evidence),"代理出口也需要检查");
            action=strstr(advice,"重启")?"重启 VPN 客户端":"检查 VPN 客户端";
            snprintf(suggestions,sizeof(suggestions),exit_ok?"等待下一轮检测\n仍异常时检查 PAC 规则":"复查节点和 PAC\n等待下一轮检测");
        } else if(health_issue(vpn)) {
            caption="异常原因";color=health_color(vpn_state);
            bool slow=!strcmp(vpn_state,"warning"),target=chat||assets||!strcmp(summary,"目标响应异常");
            reason=slow?"代理出口响应变慢":chat||assets?summary:target?"目标服务响应异常":"代理出口连接失败";
            if(slow) {
                char ms[24];health_number(vpn,"latencyMs",ms,sizeof(ms)," ms");snprintf(evidence,sizeof(evidence),"响应时间 %s",ms);
            } else snprintf(evidence,sizeof(evidence),"%s",chat?"资源服务仍可连接":assets?"ChatGPT 仍可连接":target?"部分检测目标未正常响应":"检测目标未正常响应");
            action=chat?"检查 ChatGPT 连接":assets?"检查资源服务连接":target?"检查对应服务":"检查或更换 VPN 节点";
            snprintf(suggestions,sizeof(suggestions),target?"稍后重试，等待下一轮检测":"调整后等待下一轮检测");
        } else if(!strcmp(vpn_state,"ok")&&(!strcmp(pac_state,"ok")||!strcmp(pac_state,"disabled"))) {
            reason="未发现网络异常";action="无需处理";color=GREEN;
            char ms[24];health_number(vpn,"latencyMs",ms,sizeof(ms)," ms");snprintf(evidence,sizeof(evidence),"出口响应 %s",ms);
            snprintf(suggestions,sizeof(suggestions),"后台继续监控");
        } else if(!strcmp(vpn_state,"disabled")) {
            reason="网络检测已关闭";action="需要时启用网络检测";
        } else {
            if(!strcmp(text(vpn,"summary"),"目标限制访问")) {
                reason="目标限制访问";action="稍后复查目标服务";
            }
            snprintf(evidence,sizeof(evidence),"当前检测尚未确认");
        }
    } else {
        cJSON *h=item(shown,"runtimeHealth"),*p=item(h,"pressure"),*m=item(shown,"systemMetrics");
        cJSON *cpu=health_check("cpu"),*memory=health_check("memory"),*queue=health_check("queue"),*disk=health_check("disk");
        bool cpu_bad=health_issue(cpu),memory_bad=health_issue(memory),queue_bad=health_issue(queue),disk_bad=health_issue(disk);
        if(cpu_bad||memory_bad||queue_bad||disk_bad) {
            caption="异常原因";color=AMBER;
            reason=cpu_bad||queue_bad?"持续高负载":memory_bad?(strstr(text(memory,"summary"),"提交")?"内存提交量偏高":strstr(text(memory,"summary"),"换页")?"内存换页压力偏高":cJSON_IsNumber(item(p,"availableMemoryMb"))&&num(p,"availableMemoryMb")<512?"可用内存不足":"内存占用偏高"):"磁盘响应偏慢";
            char cpu_value[24],memory_value[24],free_value[24],queue_value[24],disk_value[24];
            health_number(m,"cpuPercent",cpu_value,sizeof(cpu_value),"%");health_number(m,"memoryPercent",memory_value,sizeof(memory_value),"%");
            health_number(p,"availableMemoryMb",free_value,sizeof(free_value)," MB");health_number(p,"processorQueue",queue_value,sizeof(queue_value),"");
            health_number(p,"diskLatencyMs",disk_value,sizeof(disk_value)," ms");
            size_t used=0;
            if(cpu_bad)used+=(size_t)snprintf(evidence+used,sizeof(evidence)-used,"CPU %s\n",cpu_value);
            if(memory_bad) {
                if(strstr(reason,"提交"))used+=(size_t)snprintf(evidence+used,sizeof(evidence)-used,"已提交 %.0f%% · 可用 %s\n",num(p,"commitPercent"),free_value);
                else if(strstr(reason,"换页"))used+=(size_t)snprintf(evidence+used,sizeof(evidence)-used,"内存换页偏多 · 磁盘 %s\n",disk_value);
                else used+=(size_t)snprintf(evidence+used,sizeof(evidence)-used,"内存 %s · 可用 %s\n",memory_value,free_value);
            }
            if(queue_bad)used+=(size_t)snprintf(evidence+used,sizeof(evidence)-used,"待运行线程 %s\n",queue_value);
            if(disk_bad)snprintf(evidence+used,sizeof(evidence)-used,"磁盘响应 %s",disk_value);
            action=queue_bad?"减少并行任务":cpu_bad?"检查高 CPU 占用程序":memory_bad?"关闭占用内存较多的程序":"检查磁盘读写";
            if(cpu_bad||queue_bad)snprintf(suggestions,sizeof(suggestions),"%s%s%s",memory_bad?"关闭不用的程序，释放内存":"",memory_bad&&disk_bad?"\n":"",disk_bad?"检查大量读写磁盘的程序":"");
            else if(memory_bad&&disk_bad)snprintf(suggestions,sizeof(suggestions),"检查大量读写磁盘的程序");
        } else if(!strcmp(text(cpu,"state"),"ok")&&!strcmp(text(memory,"state"),"ok")&&!strcmp(text(queue,"state"),"ok")&&!strcmp(text(disk,"state"),"ok")) {
            reason="未发现系统异常";action="无需处理";color=GREEN;snprintf(suggestions,sizeof(suggestions),"后台继续监控");
        } else snprintf(evidence,sizeof(evidence),"当前检测尚未确认");
    }
    ui_text(health_detail_reason_caption,caption);ui_text(health_detail_reason,reason);tint(health_detail_reason,color);
    ui_text(health_detail_text,evidence);ui_text(health_detail_action,action);ui_text(health_detail_advice,suggestions);
}
static void health_detail_group(int group) {
    health_detail_selected=group;ui_text(health_detail_title,group==0?"VPN / PAC 详情":group==2?"连接与监控详情":"系统负载详情");
    health_detail_refresh();visible(health_detail_layer,true);
}
static bool health_back(void) {
    if(current_page!=4||!health_open)return false;
    if(!lv_obj_has_flag(health_detail_layer,LV_OBJ_FLAG_HIDDEN))health_detail_close(NULL);else health_close(NULL);
    return true;
}
static void health_detail_open(lv_event_t *event) {health_detail_group((int)(intptr_t)lv_event_get_user_data(event));}
static void health_summary_detail(lv_event_t *event) {
    (void)event;cJSON *alert=cJSON_GetArrayItem(item(item(shown,"runtimeHealth"),"alerts"),0);const char *key=text(alert,"key");
    health_detail_group(!strcmp(key,"codex")||!strcmp(key,"bridge")?2:!strcmp(key,"cpu")||!strcmp(key,"memory")||!strcmp(key,"queue")||!strcmp(key,"disk")?1:0);
}
static lv_obj_t *health_line(lv_obj_t *parent,const char *value,int x,int y,int width,int height) {
    lv_obj_t *line=label(parent,value,x,y,width,false);lv_obj_set_height(line,height);lv_label_set_long_mode(line,LV_LABEL_LONG_WRAP);return line;
}
static UI_INIT_SMALL void health_create(lv_obj_t *parent) {
    health_layer=lv_obj_create(parent);lv_obj_remove_style_all(health_layer);lv_obj_set_size(health_layer,1220,516);
    ui_bg_color(health_layer,lv_color_hex(UI_BACKGROUND),0);ui_bg_opa(health_layer,LV_OPA_COVER,0);lv_obj_remove_flag(health_layer,LV_OBJ_FLAG_SCROLLABLE);
    lv_obj_t *network=health_network_card=card(health_layer,0,0,598,368),*system=card(health_layer,622,0,598,368);
    label(network,"VPN 响应",6,6,300,false);health_vpn=health_line(network,"待确认",376,6,166,34);lv_obj_set_style_text_align(health_vpn,LV_TEXT_ALIGN_RIGHT,0);
    health_latency=label(network,"-- ms",6,52,540,true);
    muted(network,"响应趋势",6,111,260);health_trend_scale=muted(network,"1000 ms",350,111,192);lv_obj_set_style_text_align(health_trend_scale,LV_TEXT_ALIGN_RIGHT,0);
    health_trend=lv_chart_create(network);lv_obj_set_pos(health_trend,6,153);lv_obj_set_size(health_trend,536,80);
    ui_bg_opa(health_trend,LV_OPA_TRANSP,0);lv_obj_set_style_border_width(health_trend,0,0);lv_obj_set_style_pad_all(health_trend,0,0);
    lv_obj_set_style_line_color(health_trend,lv_color_hex(TRACK),0);lv_chart_set_div_line_count(health_trend,2,0);
    lv_chart_set_type(health_trend,LV_CHART_TYPE_LINE);lv_chart_set_point_count(health_trend,10);
    lv_obj_set_style_line_width(health_trend,3,LV_PART_ITEMS);lv_obj_set_style_size(health_trend,5,5,LV_PART_INDICATOR);
    health_trend_series=lv_chart_add_series(health_trend,lv_color_hex(BLUE),LV_CHART_AXIS_PRIMARY_Y);
    lv_chart_set_all_value(health_trend,health_trend_series,LV_CHART_POINT_NONE);
    health_trend_note=muted(network,"等待采样",6,240,540);
    shape(network,6,282,536,1,TRACK,0);label(network,"PAC 路由",6,300,280,false);
    health_pac=health_line(network,"待确认",376,300,166,34);lv_obj_set_style_text_align(health_pac,LV_TEXT_ALIGN_RIGHT,0);
    label(system,"电脑负载",6,6,300,false);health_system_state=health_line(system,"待确认",356,6,186,34);lv_obj_set_style_text_align(health_system_state,LV_TEXT_ALIGN_RIGHT,0);
    label(system,"CPU",6,63,220,false);health_cpu=health_line(system,"--",356,63,186,34);lv_obj_set_style_text_align(health_cpu,LV_TEXT_ALIGN_RIGHT,0);
    health_bars[0]=vbar(system,6,108,536,BLUE);
    label(system,"内存",6,151,220,false);health_memory=health_line(system,"--",356,151,186,34);lv_obj_set_style_text_align(health_memory,LV_TEXT_ALIGN_RIGHT,0);
    health_bars[1]=vbar(system,6,196,536,GREEN);
    shape(system,6,238,536,1,TRACK,0);
    muted(system,"CPU 等待",6,256,246);muted(system,"磁盘响应",292,256,250);
    health_queue=label(system,"--",6,288,246,true);health_disk=label(system,"-- ms",292,288,250,true);
    lv_obj_t *footer=card(health_layer,0,392,1220,112);
    health_status_dot=shape(footer,6,12,10,10,MUTED,5);
    health_alarm_title=health_line(footer,"等待检测确认",32,0,860,40);ui_text_font(health_alarm_title,&tab5_font_30,0);
    health_advice=health_line(footer,"",32,44,860,32);tint(health_advice,MUTED);
    lv_label_set_long_mode(health_alarm_title,LV_LABEL_LONG_DOT);lv_label_set_long_mode(health_advice,LV_LABEL_LONG_DOT);
    button(footer,"检测详情",956,16,216,health_summary_detail,0);
    lv_obj_t *cards[]={network,system};
    for(int i=0;i<2;i++){lv_obj_add_flag(cards[i],LV_OBJ_FLAG_CLICKABLE);lv_obj_add_event_cb(cards[i],health_detail_open,LV_EVENT_CLICKED,(void *)(intptr_t)i);}
    lv_obj_add_flag(footer,LV_OBJ_FLAG_CLICKABLE);lv_obj_add_event_cb(footer,health_summary_detail,LV_EVENT_CLICKED,NULL);
    health_detail_layer=lv_obj_create(health_layer);lv_obj_remove_style_all(health_detail_layer);lv_obj_set_size(health_detail_layer,1220,516);
    ui_bg_color(health_detail_layer,lv_color_hex(UI_BACKGROUND),0);ui_bg_opa(health_detail_layer,LV_OPA_COVER,0);lv_obj_remove_flag(health_detail_layer,LV_OBJ_FLAG_SCROLLABLE);
    health_detail_title=label(health_detail_layer,"检测详情",0,6,1180,false);tint(health_detail_title,MUTED);
    lv_obj_t *cause=card(health_detail_layer,0,68,598,392),*solution=card(health_detail_layer,622,68,598,392);
    health_detail_reason_caption=muted(cause,"异常原因",6,6,536);
    health_detail_reason=label(cause,"等待检测",6,54,536,false);ui_text_font(health_detail_reason,&tab5_font_30,0);
    health_detail_text=label(cause,"",6,128,536,false);lv_label_set_long_mode(health_detail_text,LV_LABEL_LONG_WRAP);lv_obj_set_style_text_line_space(health_detail_text,14,0);
    muted(solution,"处理建议",6,6,536);
    health_detail_action=label(solution,"",6,54,536,false);ui_text_font(health_detail_action,&tab5_font_30,0);
    health_detail_advice=label(solution,"",6,128,536,false);lv_label_set_long_mode(health_detail_advice,LV_LABEL_LONG_WRAP);lv_obj_set_style_text_line_space(health_detail_advice,18,0);
    visible(health_layer,false);
}
static const char *health_notice_title(cJSON *alert) {
    const char *key=text(alert,"key"),*title=text(alert,"title");
    // Keep the source prefix and the cause readable within the shared header.
    // The monitoring detail page retains the complete original diagnostic.
    if(!strcmp(key,"memory")) {
        if(strstr(title,"提交"))return "提交量偏高";
        if(strstr(title,"换页"))return "换页压力大";
    }
    if(!strcmp(key,"queue"))return "等待偏多";
    if(!strcmp(key,"codex"))return strstr(title,"登录")?"Codex 登录":strstr(title,"连接")?"Codex 断连":"Codex 异常";
    if(!strcmp(key,"bridge"))return strstr(title,"采样")?"采样停滞":"桥接异常";
    if(!strcmp(key,"settings"))return "设置异常";
    return title;
}
static void health_notice_tick(int64_t now) {
    cJSON *h=item(shown,"runtimeHealth"),*alert=health_fresh()?cJSON_GetArrayItem(item(h,"alerts"),0):NULL;
    const char *session=text(h,"session");uint64_t seq=(uint64_t)num(h,"sequence");
    if(alert&&prefs.alerts&&(strcmp(health_notice_session,session)||health_notice_sequence!=seq)) {
        snprintf(health_notice_session,sizeof(health_notice_session),"%s",session);health_notice_sequence=seq;voice_remote_notify_health();if(saver)saver_leave();
    }
    // Task/approval banners keep their ten-second window. Health remains
    // visible afterwards, without repeated sound or forced page switching.
    if(now<alert_until){health_banner_active=false;visible(health_banner_icon,false);return;}
    const char *message=alert&&prefs.alerts?health_notice_title(alert):current_page==0&&cJSON_IsObject(h)&&!health_fresh()?"监控已中断":"";
    health_banner_active=message[0]!=0;
    visible(health_banner_icon,health_banner_active);
    if(health_banner_active){
        uint32_t color=alert?health_color(text(alert,"state")):AMBER;
        ui_text_fmt(banner,"系统监控：%s",message);tint(banner,color);tint(health_banner_icon,color);
    }
    if(health_open&&current_page==4)health_render();
}
