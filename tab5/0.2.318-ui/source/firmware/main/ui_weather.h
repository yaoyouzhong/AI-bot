#include <math.h>
// Fixed objects preserve scroll position and avoid rebuilding on heartbeats.
static lv_obj_t *weather_panels[3],*weather_nav[3],*forecast_note,*forecast_empty[2];
static lv_obj_t *hour_cards[24],*hour_time[24],*hour_date[24],*hour_icon[24],*hour_temp[24],*hour_rain[24];
static lv_obj_t *hour_plot,*hour_segments[23],*hour_dots[24];
static lv_obj_t *hour_rain_bars[24];
static lv_point_precise_t hour_points[23][2];
#define WEATHER_COOL 0xA5D8F4
#define WEATHER_WARM 0xF2BE82
static lv_obj_t *weather_range,*weather_range_dot;
static bool weather_temperature_known(cJSON *value) {
    return cJSON_IsNumber(value)&&isfinite(value->valuedouble)&&value->valuedouble>=-100&&value->valuedouble<=100;
}
static void weather_temperature_render(cJSON *w) {
    cJSON *low=item(w,"low"),*high=item(w,"high"),*current=item(w,"temperature");
    bool range_known=weather_temperature_known(low)&&weather_temperature_known(high)&&low->valuedouble<=high->valuedouble;
    if(weather_temperature_known(low))ui_text_fmt(weather_low,"%.1f°",low->valuedouble);else ui_text(weather_low,"--");
    if(weather_temperature_known(high))ui_text_fmt(weather_high,"%.1f°",high->valuedouble);else ui_text(weather_high,"--");
    // Keep a neutral track when bounds are unavailable; never imply a position
    // from missing, inverted or out-of-range observations.
    visible(weather_range,range_known);
    bool positioned=range_known&&weather_temperature_known(current)&&current->valuedouble>=low->valuedouble&&current->valuedouble<=high->valuedouble;
    if(positioned) {
        double span=high->valuedouble-low->valuedouble;
        double position=span>0?(current->valuedouble-low->valuedouble)/span:0.5;
        int x=10+(int)lround(position*510)-8;
        if(lv_obj_get_x(weather_range_dot)!=x)lv_obj_set_x(weather_range_dot,x);
    }
    visible(weather_range_dot,positioned);
}
#define HOUR_STEP 132
static void weather_line_task(lv_event_t *event) {
    // Same raw-end path as the validated network chart; avoid diagonal end-mask clipping.
    lv_draw_line_dsc_t *line=lv_draw_task_get_line_dsc(lv_event_get_draw_task(event));
    if(line)line->raw_end=1;
}
static void weather_hour_curve(cJSON *hours,int count) {
    double low=1000,high=-1000,temperatures[24]={0};bool valid[24]={0};int y[24]={0};
    for(int i=0;i<count;i++) {
        cJSON *value=item(cJSON_GetArrayItem(hours,i),"temperature");
        valid[i]=cJSON_IsNumber(value)&&isfinite(value->valuedouble)&&value->valuedouble>=-100&&value->valuedouble<=100;
        if(valid[i]){temperatures[i]=value->valuedouble;if(temperatures[i]<low)low=temperatures[i];if(temperatures[i]>high)high=temperatures[i];}
    }
    // A minimum range keeps an almost-flat forecast from looking dramatic.
    double span=high-low;if(span<4){low-=(4-span)/2;span=4;}
    for(int i=0;i<24;i++) {
        y[i]=valid[i]?298-(int)((temperatures[i]-low)*92/span):252;
        visible(hour_dots[i],i<count&&valid[i]);
        lv_obj_set_pos(hour_dots[i],i*HOUR_STEP+HOUR_STEP/2-4,y[i]-4);
        lv_obj_set_y(hour_temp[i],y[i]-40);
        if(i<count&&!valid[i])ui_text(hour_temp[i],"--");
        if(i) {
            bool connected=i<count&&valid[i-1]&&valid[i];visible(hour_segments[i-1],connected);
            if(connected) {
                lv_point_precise_t *p=hour_points[i-1];
                int x=(i-1)*HOUR_STEP+HOUR_STEP/2;
                if(p[0].x!=x||p[0].y!=y[i-1]||p[1].x!=x+HOUR_STEP||p[1].y!=y[i]) {
                    p[0]=(lv_point_precise_t){x,y[i-1]};p[1]=(lv_point_precise_t){x+HOUR_STEP,y[i]};
                    lv_line_set_points(hour_segments[i-1],p,2);
                }
            }
        }
    }
}
static lv_obj_t *day_rows[7],*day_date[7],*day_icon[7],*day_condition[7],*day_low[7],*day_high[7],*day_rain[7];
static int weather_section;
static void weather_choose(lv_event_t *e) {
    weather_section=(int)(intptr_t)lv_event_get_user_data(e);
    for(int i=0;i<3;i++) {
        visible(weather_panels[i],i==weather_section);
        ui_bg_color(weather_nav[i],lv_color_hex(i==weather_section?0x354A52:SURFACE),0);
        tint(lv_obj_get_child(weather_nav[i],0),i==weather_section?BLUE:MUTED);
    }
}
static void forecast_icon(lv_obj_t *o,const char *condition) {
    int type=strstr(condition,"雪")?4:strstr(condition,"雨")||strstr(condition,"雷")?3:
        strstr(condition,"云")||strstr(condition,"阴")||strstr(condition,"雾")||strstr(condition,"霾")?2:strstr(condition,"晴")?1:0;
    if((intptr_t)lv_obj_get_user_data(o)!=type+1){lv_obj_set_user_data(o,(void*)(intptr_t)(type+1));lv_obj_invalidate(o);}
}
static void forecast_number(lv_obj_t *o,cJSON *data,const char *key,const char *suffix) {
    cJSON *n=item(data,key);
    if(cJSON_IsNumber(n))ui_text_fmt(o,"%.0f%s",n->valuedouble,suffix);else ui_text(o,"--");
}
static void weather_forecast_render(cJSON *w) {
    cJSON *hours=item(w,"hourly"),*days=item(w,"daily");
    const char *provider=text(w,"source");
    const char *city=text(w,"city");char source[256];
    snprintf(source,sizeof(source),"· %s%s",!strcmp(provider,"qweather")?"和风天气":!strcmp(provider,"open-meteo")?"Open-Meteo":provider,
        yes(w,"stale")||yes(w,"hourlyStale")||yes(w,"dailyStale")?" · 缓存":"");
    // Measure source data: LONG_DOT labels can temporarily expose truncated text.
    lv_point_t city_size,source_size;
    lv_text_get_size(&city_size,city,&tab5_font_30,0,0,LV_COORD_MAX,LV_TEXT_FLAG_NONE);
    lv_text_get_size(&source_size,source,&tab5_font_24,0,0,LV_COORD_MAX,LV_TEXT_FLAG_NONE);
    int source_width=source_size.x+4>280?280:source_size.x+4;
    int city_width=city_size.x+4>630-source_width-20?630-source_width-20:city_size.x+4;
    lv_obj_set_width(weather,city_width);lv_obj_set_width(forecast_note,source_width);
    lv_obj_set_x(forecast_note,city_width+20);
    ui_text(weather,city);ui_text(forecast_note,source);
    int nh=cJSON_GetArraySize(hours),nd=cJSON_GetArraySize(days);if(nh>24)nh=24;
    visible(forecast_empty[0],nh==0);visible(forecast_empty[1],nd==0);
    visible(hour_plot,nh>0);lv_obj_set_width(hour_plot,nh*HOUR_STEP>1220?nh*HOUR_STEP:1220);
    for(int i=0;i<24;i++) {
        cJSON *h=cJSON_GetArrayItem(hours,i);visible(hour_cards[i],h!=NULL);if(!h)continue;
        const char *stamp=text(h,"time");
        ui_text_fmt(hour_time[i],"%.*s",strlen(stamp)>=16?5:0,strlen(stamp)>=16?stamp+11:"");
        ui_text_fmt(hour_date[i],"%.*s",strlen(stamp)>=10?5:0,strlen(stamp)>=10?stamp+5:"");
        forecast_icon(hour_icon[i],text(h,"condition"));forecast_number(hour_temp[i],h,"temperature","°");
        cJSON *rain=item(h,"rainProbability");
        bool rain_known=cJSON_IsNumber(rain)&&isfinite(rain->valuedouble)&&rain->valuedouble>=0&&rain->valuedouble<=100;
        if(rain_known)ui_text_fmt(hour_rain[i],"降雨 %.0f%%",rain->valuedouble);else ui_text(hour_rain[i],"降雨 --");
        int rain_height=rain_known?(int)(rain->valuedouble*0.45):0;
        visible(hour_rain_bars[i],rain_height>0);
        if(rain_height>0){lv_obj_set_height(hour_rain_bars[i],rain_height);lv_obj_set_y(hour_rain_bars[i],402-rain_height);}
    }
    weather_hour_curve(hours,nh);
    for(int i=0;i<7;i++) {
        cJSON *d=cJSON_GetArrayItem(days,i);visible(day_rows[i],d!=NULL);if(!d)continue;
        const char *date=text(d,"date");ui_text_fmt(day_date[i],"%.*s",strlen(date)>=10?5:0,strlen(date)>=10?date+5:"");
        ui_text(day_condition[i],text(d,"condition"));forecast_icon(day_icon[i],text(d,"condition"));
        forecast_number(day_low[i],d,"low","°");forecast_number(day_high[i],d,"high","°");
        if(cJSON_IsNumber(item(d,"rainProbability")))ui_text_fmt(day_rain[i],"降雨 %.0f%%",num(d,"rainProbability"));
        else if(cJSON_IsNumber(item(d,"precipitationMm")))ui_text_fmt(day_rain[i],"降水 %.1f mm",num(d,"precipitationMm"));
        else ui_text(day_rain[i],"降水 --");
    }
}
static UI_INIT_SMALL void weather_create(lv_obj_t *p) {
    weather=label(p,"等待天气",0,6,390,false);ui_text_font(weather,&tab5_font_30,0);
    lv_obj_set_height(weather,40);lv_label_set_long_mode(weather,LV_LABEL_LONG_DOT);tint(weather,0xDFEBEE);
    forecast_note=muted(p,"等待更新",410,12,220);tint(forecast_note,0x8EA5AF);
    lv_obj_set_height(forecast_note,32);lv_label_set_long_mode(forecast_note,LV_LABEL_LONG_DOT);
    const char *names[]={"实况","24小时预报","未来 7 天"};
    for(int i=0;i<3;i++) {
        weather_nav[i]=button(p,names[i],650+i*192,0,186,weather_choose,i);
        weather_panels[i]=lv_obj_create(p);lv_obj_remove_style_all(weather_panels[i]);
        lv_obj_set_pos(weather_panels[i],0,72);lv_obj_set_size(weather_panels[i],1220,428);
        lv_obj_set_scroll_dir(weather_panels[i],LV_DIR_VER);lv_obj_set_scrollbar_mode(weather_panels[i],LV_SCROLLBAR_MODE_AUTO);
        visible(weather_panels[i],i==0);
    }
    lv_obj_t *wc=card(weather_panels[0],0,0,572,428);
    weather_condition=label(wc,"--",12,10,270,false);
    weather_temp=label(wc,"--°",6,86,300,true);ui_text_font(weather_temp,&tab5_digits_96,0);
    weather_icon=weather_icon_create_set(wc,310,57,256);
    shape(wc,10,232,510,1,0x3F5A70,0);
    const uint32_t temperature_colors[]={WEATHER_COOL,WEATHER_WARM};
    lv_obj_t **temperature_values[]={&weather_low,&weather_high};
    for(int i=0;i<2;i++) {
        lv_obj_t *panel=shape(wc,10+i*262,256,248,108,i?0x423A33:0x2C3E47,14);
        lv_obj_t *arrow=label(panel,i?LV_SYMBOL_UP:LV_SYMBOL_DOWN,16,17,28,false);
        ui_text_font(arrow,&lv_font_montserrat_24,0);tint(arrow,temperature_colors[i]);
        muted(panel,i?"最高温度":"最低温度",56,16,180);
        *temperature_values[i]=label(panel,"--",56,52,180,true);tint(*temperature_values[i],temperature_colors[i]);
    }
    shape(wc,10,382,510,6,TRACK,LV_RADIUS_CIRCLE);
    weather_range=shape(wc,10,382,510,6,WEATHER_COOL,LV_RADIUS_CIRCLE);
    lv_obj_set_style_bg_grad_color(weather_range,lv_color_hex(WEATHER_WARM),0);
    lv_obj_set_style_bg_grad_dir(weather_range,LV_GRAD_DIR_HOR,0);
    weather_range_dot=shape(wc,10,377,16,16,INK,LV_RADIUS_CIRCLE);
    lv_obj_set_style_border_width(weather_range_dot,3,0);ui_border_color(weather_range_dot,lv_color_hex(SURFACE),0);
    visible(weather_range,false);visible(weather_range_dot,false);
    const char *factnames[]={"空气湿度","PM2.5","空气质量指数","大气压强"};
    for(int i=0;i<4;i++) {
        lv_obj_t *c=card(weather_panels[0],592+(i%2)*318,(i/2)*222,302,206);
        lv_obj_t *title=muted(c,factnames[i],0,0,266);if(i==2)weather_aqi_title=title;
        weather_facts[i]=label(c,"--",0,55,258,true);
        if(i==0)weather_humidity=vbar(c,0,139,258,BLUE);
        if(i==1)muted(c,"微克/立方米",0,139,258);
        if(i==2)weather_aqi_category=label(c,"--",0,139,258,false);
        if(i==3)weather_pressure_kind=muted(c,"hPa",0,139,258);
    }
    lv_obj_set_scroll_dir(weather_panels[1],LV_DIR_HOR);
    lv_obj_remove_flag(weather_panels[1],LV_OBJ_FLAG_SCROLL_CHAIN|LV_OBJ_FLAG_GESTURE_BUBBLE);
    hour_plot=lv_obj_create(weather_panels[1]);lv_obj_remove_style_all(hour_plot);
    lv_obj_set_size(hour_plot,24*HOUR_STEP,412);lv_obj_remove_flag(hour_plot,LV_OBJ_FLAG_SCROLLABLE);
    ui_bg_color(hour_plot,lv_color_hex(SURFACE),0);ui_bg_opa(hour_plot,LV_OPA_COVER,0);
    for(int i=0;i<3;i++)shape(hour_plot,0,206+i*46,LV_PCT(100),1,0x354247,0);
    shape(hour_plot,0,332,LV_PCT(100),1,0x435056,0);
    for(int i=0;i<23;i++) {
        hour_segments[i]=lv_line_create(hour_plot);
        lv_obj_set_style_line_color(hour_segments[i],lv_color_hex(BLUE),0);
        lv_obj_set_style_line_width(hour_segments[i],3,0);lv_obj_set_style_line_rounded(hour_segments[i],true,0);
        lv_obj_add_flag(hour_segments[i],LV_OBJ_FLAG_SEND_DRAW_TASK_EVENTS);
        lv_obj_add_event_cb(hour_segments[i],weather_line_task,LV_EVENT_DRAW_TASK_ADDED,NULL);
    }
    for(int i=0;i<24;i++) {
        lv_obj_t *c=lv_obj_create(hour_plot);lv_obj_remove_style_all(c);
        lv_obj_set_pos(c,i*HOUR_STEP,0);lv_obj_set_size(c,HOUR_STEP,404);lv_obj_remove_flag(c,LV_OBJ_FLAG_SCROLLABLE);hour_cards[i]=c;
        hour_time[i]=label(c,"--:--",0,16,HOUR_STEP,false);
        hour_date[i]=muted(c,"",0,50,HOUR_STEP);
        hour_icon[i]=weather_icon_create_set(c,28,83,76);
        hour_temp[i]=label(c,"--",0,170,HOUR_STEP,false);tint(hour_temp[i],BLUE);
        hour_rain[i]=muted(c,"降雨 --",0,335,HOUR_STEP);
        hour_rain_bars[i]=shape(c,50,402,32,1,BLUE,4);ui_bg_opa(hour_rain_bars[i],LV_OPA_50,0);
        lv_obj_t *centered[]={hour_time[i],hour_date[i],hour_temp[i],hour_rain[i]};
        for(int j=0;j<4;j++)lv_obj_set_style_text_align(centered[j],LV_TEXT_ALIGN_CENTER,0);
        hour_dots[i]=shape(hour_plot,i*HOUR_STEP+HOUR_STEP/2-4,248,8,8,BLUE,LV_RADIUS_CIRCLE);
    }
    for(int i=0;i<7;i++) {
        lv_obj_t *c=card(weather_panels[2],0,i*61,1206,55);lv_obj_set_style_pad_all(c,0,0);lv_obj_set_style_radius(c,12,0);day_rows[i]=c;
        day_date[i]=label(c,"--",24,12,144,false);day_icon[i]=weather_icon_create_set(c,175,0,75);
        day_condition[i]=label(c,"--",280,12,310,false);lv_obj_set_height(day_condition[i],32);lv_label_set_long_mode(day_condition[i],LV_LABEL_LONG_DOT);
        day_low[i]=label(c,"--",612,12,120,false);tint(day_low[i],BLUE);
        day_high[i]=label(c,"--",750,12,120,false);tint(day_high[i],CORAL);
        day_rain[i]=muted(c,"--",944,12,245);
    }
    for(int i=0;i<2;i++){forecast_empty[i]=label(weather_panels[i+1],"暂无预报，等待更新",0,160,1220,false);lv_obj_set_style_text_align(forecast_empty[i],LV_TEXT_ALIGN_CENTER,0);}
    lv_obj_send_event(weather_nav[0],LV_EVENT_CLICKED,NULL);
}
