// Overview and quota detail: keep account limits separate from local token counts.
#define PLAN_GOLD 0xE8D09D
static uint32_t usage_color(int used) {return used>=100?RED:used>=90?AMBER:BLUE;}
static uint32_t codex_usage_color(double used) {return used<0?MUTED:used>90?RED:used>=75?AMBER:GREEN;}
static lv_obj_t *home_date, *home_day, *home_weekday, *home_lunar, *home_holiday;
static lv_obj_t *home_weather, *home_temperature, *home_panels[3];
static lv_obj_t *home_brand_labels[3],*home_brand_marks[3];
// Provider order matches the existing quotaDetails contract; zero means auto.
static unsigned home_models;
static void render_quota(void);
static const char *home_provider_names[]={"Claude","Codex","百炼","Kimi","MiniMax","DeepSeek","智谱","阶跃","百度","小米","火山方舟"};
static const lv_image_dsc_t *home_provider_logos[]={&brand_claude_image,&brand_openai_option_image,&brand_alibaba_image,
    &brand_kimi_image,&brand_minimax_image,&brand_deepseek_image,&brand_zhipu_image,&brand_stepfun_image,&brand_baidu_image,&brand_xiaomi_image,&brand_volcengine_image};
static const uint32_t home_provider_colors[]={0xE6B39F,GREEN,0xB9AFFF,0x95C9F5,0xEDACC9,
    0xA2B7FF,0xCDB7ED,0xA5DBDB,0xB4C1F5,0xF5CF8F,0x98D9FA};
static const int home_provider_order[]={1,0,2,3,4,5,6,7,8,9,10};
static int home_provider_ids[3]={-2,-2,-2};
static lv_obj_t *home_values[3],*home_captions[3],*home_bars[3],*home_model_checks[UI_HOME_PROVIDERS],*home_auto_check;
static void home_provider_open(lv_event_t *e) {
    int slot=(int)(intptr_t)lv_event_get_user_data(e),id=home_provider_ids[slot];
    selected_quota=id<0?1:id;lv_dropdown_set_selected(quota_picker,selected_quota);render_quota();page(1);
}
static lv_obj_t *quota_plan, *quota_status, *quota_empty, *quota_rows[3], *quota_values[3], *quota_bars[3];
static lv_obj_t *quota_resets, *quota_reset_title, *quota_split, *credit_count, *credit_list;
static lv_obj_t *quota_weekly_remaining,*quota_weekly_time;
static lv_obj_t *quota_time_markers[3],*quota_time_faces[3],*quota_time_stems[3],*quota_time_gaps[3];
static lv_obj_t *quota_bar_dividers[3][6];
static lv_obj_t *trend_title, *trend_note, *trend_plot, *trend_bars[7], *trend_values[7], *trend_dates[7];
static lv_obj_t *quota_panels[4],*payg_panels[2],*payg_values[2],*payg_currency[2],*payg_notes[2];
static bool codex_plus(const char *plan) {
    while(*plan==' ')plan++;
    const char *plus="PLUS";
    for(int i=0;i<4;i++) {
        if(!plan[i])return false;
        char c=plan[i]>='a'&&plan[i]<='z'?plan[i]-'a'+'A':plan[i];
        if(c!=plus[i])return false;
    }
    plan+=4;while(*plan==' ')plan++;return !*plan;
}
static bool coding_plan(const char *plan) {
    char upper[96];size_t i=0;
    for(;plan[i]&&i<sizeof(upper)-1;i++)upper[i]=plan[i]>='a'&&plan[i]<='z'?plan[i]-'a'+'A':plan[i];
    upper[i]=0;return strstr(upper,"CODING")!=NULL;
}
static void visible(lv_obj_t *o, bool show) {
    // LVGL invalidates even when adding/removing an already-set visibility
    // flag. In particular, camera_tick must not redraw a full still every 100 ms.
    if (lv_obj_has_flag(o, LV_OBJ_FLAG_HIDDEN) == !show) return;
    if (show) lv_obj_remove_flag(o, LV_OBJ_FLAG_HIDDEN);
    else lv_obj_add_flag(o, LV_OBJ_FLAG_HIDDEN);
}
static lv_obj_t *plan_badge(lv_obj_t *p, int x, int y) {
    lv_obj_t *o=label(p,"",x,y,LV_SIZE_CONTENT,false);
    lv_obj_set_style_pad_hor(o,12,0); lv_obj_set_style_pad_ver(o,4,0);
    lv_obj_set_style_border_width(o,1,0); ui_border_color(o,lv_color_hex(0xC9A867),0);
    ui_bg_color(o,lv_color_hex(0xC9A867),0);ui_bg_opa(o,LV_OPA_10,0);
    lv_obj_set_style_radius(o,8,0); tint(o,0xE8D09D);
    return o;
}
static void set_plan(lv_obj_t *badge, const char *plan) {
    char normalized[64]; size_t i=0;
    for (; plan[i] && i<sizeof(normalized)-1; i++)
        normalized[i]=(plan[i]>='a'&&plan[i]<='z')?plan[i]-'a'+'A':plan[i];
    normalized[i]=0;
    ui_text(badge, normalized);
    visible(badge, normalized[0] && strcmp(normalized,"--"));
}
static lv_obj_t *home_task_title,*home_task_note,*home_task_action;
static lv_obj_t *home_system_captions[2],*home_system_values[2];
static bool home_task_unread;
static char home_task_turn[64];
static char home_task_id[37],home_task_project[17];
static lv_obj_t *home_calendar_action,*home_weather_action;
static lv_obj_t *overview_hit(lv_obj_t *parent,int x,int y,int width,int height,int target) {
    lv_obj_t *hit=lv_obj_create(parent);lv_obj_remove_style_all(hit);lv_obj_set_pos(hit,x,y);lv_obj_set_size(hit,width,height);
    lv_obj_remove_flag(hit,LV_OBJ_FLAG_SCROLLABLE);lv_obj_add_flag(hit,LV_OBJ_FLAG_CLICKABLE);
    ui_bg_color(hit,lv_color_hex(BLUE),LV_STATE_PRESSED);ui_bg_opa(hit,LV_OPA_10,LV_STATE_PRESSED);
    lv_obj_set_style_radius(hit,12,0);lv_obj_add_event_cb(hit,overview_open,LV_EVENT_CLICKED,(void *)(intptr_t)target);return hit;
}
static UI_INIT_SMALL void overview_create(lv_obj_t *p) {
    lv_obj_t *task=card(p,0,0,760,328);
    model_dot[1]=shape(task,8,14,10,10,MUTED,LV_RADIUS_CIRCLE);
    model_state[1]=label(task,"等待数据",32,4,300,false);
    home_task_note=muted(task,"",360,4,350);
    lv_obj_set_style_text_align(home_task_note,LV_TEXT_ALIGN_RIGHT,0);
    lv_obj_set_height(home_task_note,34);lv_label_set_long_mode(home_task_note,LV_LABEL_LONG_DOT);
    home_task_title=label(task,"等待电脑上的任务",8,78,706,false);
    ui_text_font(home_task_title,&tab5_font_30,0);
    lv_obj_set_height(home_task_title,82);lv_label_set_long_mode(home_task_title,LV_LABEL_LONG_DOT);
    for(int i=0;i<2;i++) {
        home_system_captions[i]=muted(task,i?"内存占用":"CPU 占用",8+i*364,72,330);
        home_system_values[i]=label(task,"--",8+i*364,118,330,true);
        tint(home_system_values[i],i?GREEN:BLUE);
        visible(home_system_captions[i],false);visible(home_system_values[i],false);
    }

    lv_obj_t *go=home_task_action=button(task,"继续 Codex 任务",8,224,706,workbench_go,-3);
    lv_obj_set_height(go,62);ui_bg_color(go,lv_color_hex(BLUE),0);
    ui_text_color(go,lv_color_hex(0x173642),0);lv_obj_center(lv_obj_get_child(go,0));
    lv_obj_t *date=card(p,784,0,436,512);
    home_date=muted(date,"等待日期",6,12,388);
    home_day=label(date,"--:--",3,63,396,true);
    ui_text_font(home_day,&tab5_digits_96,0);
    home_weekday=label(date,"",6,187,388,false);
    home_lunar=muted(date,"",6,235,388);
    home_holiday=muted(date,"",6,283,388);
    shape(date,6,340,388,1,TRACK,0);
    home_temperature=label(date,"--",6,361,280,true);
    home_weather_icon=weather_icon_create_set(date,294,349,112);
    home_weather=muted(date,"等待天气",6,431,388);
    lv_obj_t *single[]={home_date,home_weekday,home_lunar,home_holiday,home_weather};
    for(int i=0;i<5;i++){lv_obj_set_height(single[i],32);lv_label_set_long_mode(single[i],LV_LABEL_LONG_DOT);}
    home_calendar_action=overview_hit(date,0,0,400,330,12);
    home_weather_action=overview_hit(date,0,344,400,132,2);
    for(int i=0;i<3;i++) {
        lv_obj_t *c=home_panels[i]=card(p,0,352,240,160);
        lv_obj_add_event_cb(c,home_provider_open,LV_EVENT_CLICKED,(void *)(intptr_t)i);
        ui_bg_color(c,lv_color_hex(0x3C4D52),LV_STATE_PRESSED);
        home_brand_labels[i]=label(c,i==0?"Codex":i==1?"DeepSeek":"智谱",0,0,162,false);
        tint(home_brand_labels[i],i==0?GREEN:i==1?0xA2B7FF:0xCDB7ED);
        home_brand_marks[i]=lv_image_create(c);
        const lv_image_dsc_t *logos[]={&brand_openai_option_image,&brand_deepseek_image,&brand_zhipu_image};
        lv_image_set_src(home_brand_marks[i],logos[i]);
        lv_obj_remove_flag(home_brand_marks[i],LV_OBJ_FLAG_CLICKABLE);
        lv_obj_set_pos(home_brand_marks[i],168,-3);
        home_captions[i]=muted(c,"额度待同步",0,35,210);
        lv_obj_set_height(home_captions[i],32);lv_label_set_long_mode(home_captions[i],LV_LABEL_LONG_DOT);
        home_values[i]=label(c,"--",0,71,215,true);
        home_bars[i]=vbar(c,0,120,210,BLUE);lv_obj_set_height(home_bars[i],4);
        lv_obj_set_height(home_values[i],46);lv_label_set_long_mode(home_values[i],LV_LABEL_LONG_DOT);
        tint(home_brand_labels[i],INK);
    }
}
static void overview_layout(void) {
    int count=0,ids[3]={-1,-1,-1};
    for(int i=0;i<UI_HOME_PROVIDERS&&count<3;i++) {
        int id=home_provider_order[i];cJSON *q=cJSON_GetArrayItem(item(shown,"quotaDetails"),id);
        bool known=cJSON_IsNumber(item(q,"weekly"))||cJSON_IsNumber(item(q,"primary"))||
            cJSON_IsNumber(item(q,"planPercent"))||cJSON_IsNumber(item(q,"balance"));
        if(home_models?(home_models&(1u<<id))!=0:known)ids[count++]=id;
    }
    if(!count)count=1;
    // Geometry setters and image sources invalidate LVGL caches even when
    // repeated. Heartbeats must keep an unchanged overview snapshot reusable.
    if(!memcmp(ids,home_provider_ids,sizeof(ids)))return;
    memcpy(home_provider_ids,ids,sizeof(ids));
    int width=(760-(count-1)*16)/count;
    for(int i=0;i<3;i++) {
        bool show=i<count;visible(home_panels[i],show);if(!show)continue;
        lv_obj_set_pos(home_panels[i],i*(width+16),352);lv_obj_set_size(home_panels[i],width,160);
        lv_obj_set_width(home_brand_labels[i],width-78);
        lv_obj_set_x(home_brand_marks[i],width-72);
        lv_obj_set_width(home_captions[i],width-36);lv_obj_set_width(home_bars[i],width-36);
        lv_obj_set_width(home_values[i],width-36);
    }
}
static void overview_domestic(void) {
    overview_layout();
    for(int i=0;i<3;i++) {
        int id=home_provider_ids[i];cJSON *q=cJSON_GetArrayItem(item(shown,"quotaDetails"),id);
        const lv_image_dsc_t *logo=id>=0?home_provider_logos[id]:NULL;
        visible(home_brand_marks[i],logo!=NULL);
        if(logo&&lv_image_get_src(home_brand_marks[i])!=logo)lv_image_set_src(home_brand_marks[i],logo);
        int name_width=lv_obj_get_width(home_panels[i])-(logo?78:36);
        if(lv_obj_get_width(home_brand_labels[i])!=name_width)lv_obj_set_width(home_brand_labels[i],name_width);
        ui_text(home_brand_labels[i],id<0?"模型额度":home_provider_names[id]);
        tint(home_brand_labels[i],id<0?INK:home_provider_colors[id]);
        bool payg=!strcmp(text(q,"billingMode"),"payg");
        char main[64];uint32_t color=MUTED;bool known=false;const char *key="weekly";
        if(payg) {
            cJSON *balance=item(q,"balance");
            if(cJSON_IsNumber(balance))snprintf(main,sizeof(main),"%.2f",balance->valuedouble);else snprintf(main,sizeof(main),"--");
            known=cJSON_IsNumber(balance);color=known&&!yes(q,"stale")?home_provider_colors[id]:MUTED;
            ui_text_fmt(home_captions[i],"%s · %s",yes(q,"stale")?"缓存余额":"余额",text(q,"currency"));
        } else {
            key=cJSON_IsNumber(item(q,"weekly"))?"weekly":cJSON_IsNumber(item(q,"primary"))&&
                (id!=1||codex_plus(text(q,"plan")))?"primary":"planPercent";
            known=cJSON_IsNumber(item(q,key));
            if(id==1)integer_value(q,key,main,sizeof(main),"%");else value(q,key,main,sizeof(main),"%");
            color=!known||yes(q,"stale")?MUTED:id==1?codex_usage_color(num(q,key)):usage_color(percentage(q,key));
            const char *caption=!known?(id==10?"额度采集待接入":"额度待同步"):!strcmp(key,"weekly")?"本周已用":!strcmp(key,"primary")?"短周期已用":"套餐已用";
            ui_text_fmt(home_captions[i],"%s%s",caption,known&&yes(q,"stale")?" · 缓存":"");
            lv_bar_set_value(home_bars[i],percentage(q,key),LV_ANIM_OFF);
        }
        if(id<0){snprintf(main,sizeof(main),"--");ui_text(home_captions[i],"在电脑端授权后显示");}
        visible(home_bars[i],known&&!payg);ui_bg_color(home_bars[i],lv_color_hex(color),LV_PART_INDICATOR);
        tint(home_values[i],color);tint(home_captions[i],MUTED);ui_text(home_values[i],main);
    }
}
static void quota_clock_line(lv_layer_t *layer,const lv_area_t *box,int size,
                             int x1,int y1,int x2,int y2,int width,uint32_t color) {
    lv_draw_line_dsc_t d;lv_draw_line_dsc_init(&d);
    d.p1.x=box->x1+x1*size/28;d.p1.y=box->y1+y1*size/28;
    d.p2.x=box->x1+x2*size/28;d.p2.y=box->y1+y2*size/28;
    d.color=lv_color_hex(color);d.width=width;d.round_start=d.round_end=1;
    lv_draw_line(layer,&d);
}
static void quota_clock_draw(lv_event_t *e) {
    lv_obj_t *o=lv_event_get_target(e);lv_layer_t *layer=lv_event_get_layer(e);
    lv_area_t box;lv_obj_get_coords(o,&box);int size=lv_area_get_width(&box);
    lv_draw_rect_dsc_t d;lv_draw_rect_dsc_init(&d);
    d.bg_color=lv_color_hex(0xEEF4F2);d.bg_opa=LV_OPA_COVER;d.radius=LV_RADIUS_CIRCLE;
    d.border_color=lv_color_hex(BLUE);d.border_width=size>=24?2:1;d.border_opa=LV_OPA_COVER;
    lv_draw_rect(layer,&d,&box);
    quota_clock_line(layer,&box,size,14,6,14,7,1,0x8A9EA4);
    quota_clock_line(layer,&box,size,22,14,21,14,1,0x8A9EA4);
    quota_clock_line(layer,&box,size,14,22,14,21,1,0x8A9EA4);
    quota_clock_line(layer,&box,size,6,14,7,14,1,0x8A9EA4);
    quota_clock_line(layer,&box,size,14,8,14,14,2,0x2D4650);
    quota_clock_line(layer,&box,size,14,14,19,17,2,0x2D4650);
}
static void quota_time_update(int64_t now_epoch) {
    cJSON *q=cJSON_GetArrayItem(item(shown,"quotaDetails"),selected_quota);
    const char *resets[]={"primaryResetEpoch","weeklyResetEpoch","planResetEpoch"};
    const char *windows[]={"primaryWindowSeconds","weeklyWindowSeconds","planWindowSeconds"};
    for(int i=0;i<3;i++) {
        cJSON *reset=item(q,resets[i]),*window=item(q,windows[i]);
        bool show=strcmp(text(q,"billingMode"),"payg")&&now_epoch>0&&
            !lv_obj_has_flag(quota_bars[i],LV_OBJ_FLAG_HIDDEN)&&cJSON_IsNumber(reset)&&cJSON_IsNumber(window)&&
            reset->valuedouble>now_epoch&&reset->valuedouble<253402300800.0&&
            window->valuedouble>0&&window->valuedouble<=2147483647.0&&
            window->valuedouble==(int64_t)window->valuedouble&&
            now_epoch>=reset->valuedouble-window->valuedouble;
        visible(quota_time_markers[i],show);
        if(!show)continue;
        double progress=(now_epoch-(reset->valuedouble-window->valuedouble))/window->valuedouble;
        int size=lv_obj_get_width(quota_time_faces[i]);
        int x=lv_obj_get_x(quota_bars[i])+(int)(progress*lv_obj_get_width(quota_bars[i])+0.5)-size/2;
        int y=lv_obj_get_y(quota_bars[i])-size-(size==26?3:1);
        if(lv_obj_get_x(quota_time_markers[i])!=x||lv_obj_get_y(quota_time_markers[i])!=y)
            lv_obj_set_pos(quota_time_markers[i],x,y);
    }
}
static UI_INIT_SMALL void quota_create(lv_obj_t *p) {
    quota_plan=plan_badge(p,304,4);
    quota_status=muted(p,"",520,11,660);
    lv_obj_t *usage=quota_panels[0]=card(p,0,64,748,224);
    quota_empty=label(usage,"该模型尚未提供额度数据",10,80,690,false);
    const char *titles[]={"5 小时已使用","本周已使用","套餐周期已使用"};
    for(int i=0;i<3;i++) {
        quota_rows[i]=label(usage,titles[i],10,0,440,false);
        quota_values[i]=label(usage,"--",478,0,225,false);
        lv_obj_set_style_text_align(quota_values[i],LV_TEXT_ALIGN_RIGHT,0);
        quota_bars[i]=vbar(usage,6,0,700,i==0?BLUE:i==1?GREEN:CORAL);
    }
    for(int i=0;i<3;i++)for(int cut=0;cut<6;cut++) {
        quota_bar_dividers[i][cut]=shape(quota_bars[i],0,0,2,LV_PCT(100),SURFACE,0);
        visible(quota_bar_dividers[i][cut],false);
    }
    for(int i=0;i<3;i++) {
        lv_obj_t *marker=quota_time_markers[i]=lv_obj_create(usage);
        lv_obj_remove_style_all(marker);lv_obj_set_size(marker,26,49);
        lv_obj_remove_flag(marker,LV_OBJ_FLAG_CLICKABLE|LV_OBJ_FLAG_SCROLLABLE);
        quota_time_gaps[i]=shape(marker,10,25,7,24,SURFACE,2);
        quota_time_stems[i]=shape(marker,12,25,3,24,BLUE,2);
        lv_obj_t *face=quota_time_faces[i]=lv_obj_create(marker);
        lv_obj_remove_style_all(face);lv_obj_set_size(face,26,26);
        lv_obj_remove_flag(face,LV_OBJ_FLAG_CLICKABLE|LV_OBJ_FLAG_SCROLLABLE);
        lv_obj_add_event_cb(face,quota_clock_draw,LV_EVENT_DRAW_MAIN,NULL);
        visible(marker,false);
    }
    quota_weekly_remaining=muted(usage,"",392,101,310);
    ui_text_font(quota_weekly_remaining,&tab5_countdown_24,0);
    tint(quota_weekly_remaining,0x94A6AD);
    lv_obj_set_style_text_align(quota_weekly_remaining,LV_TEXT_ALIGN_RIGHT,0);
    visible(quota_weekly_remaining,false);
    quota_weekly_time=label(usage,"",392,101,310,false);
    ui_text_font(quota_weekly_time,&tab5_countdown_24,0);
    lv_obj_set_style_text_align(quota_weekly_time,LV_TEXT_ALIGN_RIGHT,0);
    visible(quota_weekly_time,false);
    lv_obj_t *resets=quota_panels[1]=card(p,772,64,448,448);
    quota_reset_title=muted(resets,"额度重置",6,4,400);
    quota_resets=label(resets,"等待重置时间",6,52,400,false);
    lv_obj_set_style_text_line_space(quota_resets,0,0);
    quota_split=shape(resets,6,132,388,1,TRACK,0);
    lv_obj_t *credits=quota_panels[2]=lv_obj_create(resets);
    lv_obj_remove_style_all(credits);
    lv_obj_set_pos(credits,0,148);lv_obj_set_size(credits,400,252);
    lv_obj_remove_flag(credits,LV_OBJ_FLAG_SCROLLABLE);
    label(credits,"重置卡",6,4,250,false);
    credit_count=label(credits,"--",258,0,136,false); tint(credit_count,0xE8D09D);
    ui_bg_color(credit_count,lv_color_hex(0xC9A867),0);
    ui_bg_opa(credit_count,LV_OPA_10,0);
    lv_obj_set_style_radius(credit_count,8,0);lv_obj_set_style_pad_all(credit_count,4,0);
    lv_obj_set_style_text_align(credit_count,LV_TEXT_ALIGN_CENTER,0);
    credit_list=lv_obj_create(credits);lv_obj_remove_style_all(credit_list);
    lv_obj_set_pos(credit_list,6,48);lv_obj_set_size(credit_list,388,204);
    lv_obj_set_scroll_dir(credit_list,LV_DIR_VER);
    lv_obj_set_scrollbar_mode(credit_list,LV_SCROLLBAR_MODE_ON);
    ui_bg_color(credit_list,lv_color_hex(0xC9A867),LV_PART_SCROLLBAR);
    lv_obj_set_style_width(credit_list,5,LV_PART_SCROLLBAR);
    lv_obj_t *trend=quota_panels[3]=card(p,0,312,748,200);
    trend_title=label(trend,"Codex 每日用量",6,0,530,false);
    trend_plot=lv_obj_create(trend);lv_obj_remove_style_all(trend_plot);
    lv_obj_set_pos(trend_plot,6,32);lv_obj_set_size(trend_plot,700,128);
    lv_obj_remove_flag(trend_plot,LV_OBJ_FLAG_SCROLLABLE|LV_OBJ_FLAG_CLICKABLE);
    shape(trend_plot,0,92,698,1,TRACK,0);
    for(int i=0;i<7;i++) {
        trend_bars[i]=shape(trend_plot,i*99+32,40,34,33,BLUE,3);
        trend_values[i]=label(trend_plot,"--",i*99,0,96,false);
        trend_dates[i]=muted(trend_plot,"--",i*99,96,96);
        lv_obj_set_style_text_align(trend_values[i],LV_TEXT_ALIGN_CENTER,0);
        lv_obj_set_style_text_align(trend_dates[i],LV_TEXT_ALIGN_CENTER,0);
        ui_text_font(trend_values[i],&lv_font_montserrat_24,0);
        ui_text_font(trend_dates[i],&lv_font_montserrat_24,0);
    }
    trend_note=muted(trend,"",4,75,698);
    for(int i=0;i<2;i++) {
        lv_obj_t *money=payg_panels[i]=card(p,i*616,66,604,444);
        label(money,i?"已使用金额":"可用余额",10,14,400,false);
        payg_currency[i]=muted(money,"",426,14,132);
        lv_obj_set_style_text_align(payg_currency[i],LV_TEXT_ALIGN_RIGHT,0);
        shape(money,10,77,544,1,TRACK,0);
        payg_values[i]=label(money,"--",10,135,345,true);
        ui_text_font(payg_values[i],&tab5_digits_72,0);
        tint(payg_values[i],i?AMBER:GREEN);
        payg_notes[i]=muted(money,"",10,318,544);
        visible(money,false);
    }
}
static void quota_countdown(int64_t now_epoch) {
    quota_time_update(now_epoch);
    cJSON *q=cJSON_GetArrayItem(item(shown,"quotaDetails"),selected_quota);
    cJSON *reset=item(q,"weeklyResetEpoch");
    bool single_weekly=!(codex_plus(text(q,"plan"))&&cJSON_IsNumber(item(q,"primary")))&&!cJSON_IsNumber(item(q,"planPercent"));
    bool show=selected_quota==1&&single_weekly&&cJSON_IsNumber(item(q,"weekly"))&&cJSON_IsNumber(reset)&&now_epoch>0&&reset->valuedouble>0&&reset->valuedouble<253402300800.0;
    visible(quota_weekly_remaining,show);
    visible(quota_weekly_time,show&&reset->valuedouble>now_epoch);
    if(!show)return;
    int64_t seconds=(int64_t)reset->valuedouble-now_epoch;
    if(seconds<=0) {
        lv_obj_set_width(quota_weekly_remaining,310);
        ui_text(quota_weekly_remaining,"等待重置同步");return;
    }
    if(seconds<3600)ui_text_fmt(quota_weekly_time,"%lldm",(long long)((seconds+59)/60));
    else if(seconds<86400)ui_text_fmt(quota_weekly_time,"%lldh %lldm",(long long)(seconds/3600),(long long)(seconds%3600/60));
    else ui_text_fmt(quota_weekly_time,"%lldd %lldh",(long long)(seconds/86400),(long long)(seconds%86400/3600));
    const char *value=lv_label_get_text(quota_weekly_time);
    int width=lv_text_get_width(value,strlen(value),&tab5_countdown_24,0);
    int gap=lv_text_get_width("  ",seconds<3600?1:2,&tab5_countdown_24,0);
    lv_obj_set_width(quota_weekly_remaining,310-width-gap);
    ui_text(quota_weekly_remaining,"距重置");
}
static void quota_render(void) {
    cJSON *q=cJSON_GetArrayItem(item(shown,"quotaDetails"),selected_quota);
    const char *keys[]={"primary","weekly","planPercent"};
    const char *reset[]={"primaryReset","weeklyReset","planReset"};
    const char *plan=text(q,"plan");
    char primary_caption[48];
    cJSON *primary_window=item(q,"primaryWindowSeconds");
    bool primary_duration=cJSON_IsNumber(primary_window)&&primary_window->valuedouble>0&&primary_window->valuedouble<=86400;
    if(primary_duration) {
        double hours=primary_window->valuedouble/3600;
        if(hours==(int)hours)snprintf(primary_caption,sizeof(primary_caption),"%.0f 小时",hours);
        else snprintf(primary_caption,sizeof(primary_caption),"%.0f 分钟",primary_window->valuedouble/60);
    } else snprintf(primary_caption,sizeof(primary_caption),"5 小时");
    ui_text_fmt(quota_rows[0],"%s已使用",primary_caption);
    ui_text(quota_empty,selected_quota==10?"火山方舟额度采集待接入":"该模型尚未提供额度数据");
    set_plan(quota_plan,plan);
    bool payg=!strcmp(text(q,"billingMode"),"payg");
    for(int i=0;i<3;i++)visible(quota_time_markers[i],false);
    for(int i=0;i<3;i++)for(int cut=0;cut<6;cut++)visible(quota_bar_dividers[i][cut],false);
    bool coding=!payg&&coding_plan(plan);
    if(payg)ui_text(quota_plan,"API PAYG");
    else if(coding)ui_text(quota_plan,"Coding Plan");
    visible(quota_plan,payg||coding||(!lv_obj_has_flag(quota_plan,LV_OBJ_FLAG_HIDDEN)));
    for(int i=0;i<4;i++)visible(quota_panels[i],!payg);
    for(int i=0;i<2;i++)visible(payg_panels[i],payg);
    if(payg) {
        ui_text(quota_status,yes(q,"stale")?"缓存":"");
        const char *money_keys[]={"balance","cost"};
        for(int i=0;i<2;i++) {
            cJSON *v=item(q,money_keys[i]);bool known=cJSON_IsNumber(v);
            char amount[48];if(known)snprintf(amount,sizeof(amount),"%.2f",v->valuedouble);else snprintf(amount,sizeof(amount),"--");
            ui_text(payg_values[i],amount);
            ui_text(payg_currency[i],text(q,"currency"));
            ui_text(payg_notes[i],known?(i?"账户累计消费":"当前可用金额"):"当前数据源未提供");
        }
        return;
    }
    ui_text(quota_status,yes(q,"stale")?"缓存":"");
    bool primary_allowed=selected_quota!=1||codex_plus(text(q,"plan"));
    int count=0;for(int i=0;i<3;i++) count+=(i!=0||primary_allowed)&&cJSON_IsNumber(item(q,keys[i]))?1:0;
    visible(quota_empty,count==0);
    int row=0;char resets[240]="";
    for(int i=0;i<3;i++) {
        bool known=(i!=0||primary_allowed)&&cJSON_IsNumber(item(q,keys[i]));
        visible(quota_rows[i],known);visible(quota_values[i],known);visible(quota_bars[i],known);
        if(!known)continue;
        int y=count==1?8:row*62;
        lv_obj_set_pos(quota_rows[i],6,y);lv_obj_set_pos(quota_values[i],count==1?6:478,count==1?72:y);
        lv_obj_set_width(quota_values[i],count==1?680:225);
        lv_obj_set_style_text_align(quota_values[i],count==1?LV_TEXT_ALIGN_LEFT:LV_TEXT_ALIGN_RIGHT,0);
        ui_text_font(quota_values[i],count==1?&tab5_digits_72:&tab5_font_24,0);
        char v[32];
        if(selected_quota==1)integer_value(q,keys[i],v,sizeof(v),"%");else value(q,keys[i],v,sizeof(v),"%");
        ui_text(quota_values[i],v);
        uint32_t color=selected_quota==1?codex_usage_color(yes(q,"stale")?-1:num(q,keys[i])):usage_color(percentage(q,keys[i]));
        tint(quota_rows[i],selected_quota==1?color:INK);
        tint(quota_values[i],color);
        lv_obj_set_pos(quota_bars[i],6,count==1?172:y+52);
        lv_obj_set_height(quota_bars[i],count==1?10:6);
        lv_bar_set_value(quota_bars[i],percentage(q,keys[i]),LV_ANIM_OFF);
        ui_bg_color(quota_bars[i],lv_color_hex(color),LV_PART_INDICATOR);
        int parts=i==1?7:5;
        if(i==0&&primary_duration&&primary_window->valuedouble==((int)primary_window->valuedouble/3600)*3600&&
            primary_window->valuedouble<=7*3600)parts=(int)primary_window->valuedouble/3600;
        for(int cut=0;cut<parts-1;cut++) {
            lv_obj_set_x(quota_bar_dividers[i][cut],(cut+1)*700/parts-1);
            visible(quota_bar_dividers[i][cut],true);
        }
        int size=count==1?26:20,stem=count==1?24:11;
        lv_obj_set_size(quota_time_markers[i],size,size+stem-1);
        lv_obj_set_size(quota_time_faces[i],size,size);
        lv_obj_set_pos(quota_time_gaps[i],size/2-3,size-1);lv_obj_set_height(quota_time_gaps[i],stem);
        lv_obj_set_pos(quota_time_stems[i],size/2-1,size-1);lv_obj_set_height(quota_time_stems[i],stem);
        const char *time=text(q,reset[i]);
        if(strcmp(time,"--")) {
            const char *window=i==0?primary_caption:i==1?"周额度":"套餐额度";
            size_t used=strlen(resets);snprintf(resets+used,sizeof(resets)-used,"%s%s · %s",used?"\n":"",window,time);
        }
        row++;
    }
    lv_obj_update_layout(quota_panels[0]);
    quota_countdown((int64_t)num(shown,"epochUtc"));
    ui_text(quota_reset_title,"额度重置");
    if(coding) {
        const char *expiry=text(q,"planExpiry");
        size_t used=strlen(resets);
        snprintf(resets+used,sizeof(resets)-used,"%s到期日 · %s",used?"\n":"",strcmp(expiry,"--")?expiry:"未提供");
    }
    ui_text(quota_resets,resets[0]?resets:"未提供重置时间");
    int reset_lines=1;
    for(const char *p=resets;*p;p++)if(*p=='\n')reset_lines++;
    int credit_y=116+(reset_lines-1)*32;
    if(credit_y>212)credit_y=212;
    lv_obj_set_y(quota_split,credit_y-16);
    lv_obj_set_y(quota_panels[2],credit_y);
    lv_obj_set_height(quota_panels[2],400-credit_y);
    lv_obj_set_height(credit_list,400-credit_y-48);
    cJSON *expires=item(q,"creditExpiries");int dates=cJSON_GetArraySize(expires);
    bool has_count=cJSON_IsNumber(item(q,"credits"));
    int credits=has_count?(int)num(q,"credits"):dates;
    if(credits<dates)credits=dates;
    int rows=dates+(credits>dates?1:0);if(!rows)rows=1;
    bool credit_scroll=(rows-1)*68+64>lv_obj_get_height(credit_list);
    if(has_count||dates)ui_text_fmt(credit_count,credit_scroll?"%d 张 · 上滑":"%d 张",credits);
    else ui_text(credit_count,"--");
    lv_obj_set_scrollbar_mode(credit_list,credit_scroll?LV_SCROLLBAR_MODE_ON:LV_SCROLLBAR_MODE_OFF);
    if(lv_obj_get_child_count(credit_list)!=(uint32_t)rows) {
        lv_obj_clean(credit_list);
        for(int i=0;i<rows;i++) {
            lv_obj_t *ticket=card(credit_list,0,i*68,384,64);
            lv_obj_set_style_pad_all(ticket,0,0);
            ui_bg_color(ticket,lv_color_hex(0x354247),0);
            lv_obj_set_style_radius(ticket,12,0);
            lv_obj_set_style_border_width(ticket,0,0);
            lv_obj_t *icon_bg=shape(ticket,14,8,48,48,0x493F30,LV_RADIUS_CIRCLE);
            lv_obj_set_style_border_width(icon_bg,1,0);
            ui_border_color(icon_bg,lv_color_hex(0xA78753),0);
            lv_obj_t *icon=label(icon_bg,LV_SYMBOL_REFRESH,0,9,48,false);
            ui_text_font(icon,&lv_font_montserrat_24,0);
            lv_obj_set_style_text_align(icon,LV_TEXT_ALIGN_CENTER,0);tint(icon,PLAN_GOLD);
            lv_obj_t *date=label(ticket,"",82,14,290,false);
            ui_text_font(date,&tab5_font_24,0);
            lv_obj_set_height(date,34);lv_label_set_long_mode(date,LV_LABEL_LONG_DOT);
        }
    }
    for(int i=0;i<rows;i++) {
        lv_obj_t *ticket=lv_obj_get_child(credit_list,i),*expiry=lv_obj_get_child(ticket,1);
        if(i<dates) {
            cJSON *date=cJSON_GetArrayItem(expires,i);
            int y,m,d,h,min;
            const char *raw=cJSON_IsString(date)?date->valuestring:"到期时间未知";
            if(sscanf(raw,"%d-%d-%d %d:%d",&y,&m,&d,&h,&min)==5) {
                ui_text_fmt(expiry,"%04d-%02d-%02d  %02d:%02d",y,m,d,h,min);
            } else ui_text(expiry,raw);
        } else {
            if(credits>dates)ui_text_fmt(expiry,"%d 张到期时间未知",credits-dates);
            else ui_text(expiry,has_count?"暂无可用重置卡":"未提供重置卡信息");
        }
    }
    cJSON *trend=item(shown,"quotaTrend"),*days=item(trend,"days");
    bool codex=selected_quota==1;visible(trend_plot,codex);
    ui_text(trend_title,codex?"近7日 · 周额度使用（百分比）":"每日用量");
    double top=1;
    for(int i=0;i<7;i++){cJSON *d=cJSON_GetArrayItem(days,i);if(num(d,"used")>top)top=num(d,"used");}
    for(int i=0;i<7;i++) {
        cJSON *d=cJSON_GetArrayItem(days,i);bool known=cJSON_IsNumber(item(d,"used"));
        double used=num(d,"used");int height=(int)(used/top*58);
        visible(trend_bars[i],known&&used>0);lv_obj_set_pos(trend_bars[i],i*99+32,92-height);
        lv_obj_set_height(trend_bars[i],height>0?height:1);
        ui_bg_color(trend_bars[i],lv_color_hex(yes(d,"partial")?AMBER:BLUE),0);
        char v[24];integer_value(d,"used",v,sizeof(v),"");
        ui_text(trend_values[i],v);
        ui_text(trend_dates[i],text(d,"label"));
    }
    visible(trend_note,!codex);
    ui_text(trend_note,"暂未支持此模型的每日统计");
    // Keep the axis dates, but no explanatory text underneath the bars.
}
