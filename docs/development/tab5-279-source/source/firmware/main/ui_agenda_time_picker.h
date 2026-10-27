// A private draft: canceling a picker never changes the editor's time range.
static lv_obj_t *agenda_picker,*agenda_picker_heading,*agenda_picker_month;
static lv_obj_t *agenda_picker_hours,*agenda_picker_minutes,*agenda_picker_feedback,*agenda_picker_confirm;
static lv_obj_t *agenda_picker_days[2],*agenda_picker_dates[42];
static lv_calendar_date_t agenda_picker_day;
static unsigned agenda_picker_row;
static bool agenda_picker_open(void){return agenda_picker&&!lv_obj_has_flag(agenda_picker,LV_OBJ_FLAG_HIDDEN);}
static void agenda_picker_close(lv_event_t *event){(void)event;if(agenda_picker)visible(agenda_picker,false);}
static void agenda_picker_render_date(void) {
    char month[40];snprintf(month,sizeof(month),"%u 年 %u 月",agenda_picker_day.year,(unsigned)agenda_picker_day.month);ui_text(agenda_picker_month,month);
    int first=(countdown_civil_day(agenda_picker_day.year,agenda_picker_day.month,1)-countdown_civil_day(1970,1,1)+4)%7;
    int days=countdown_month_days(agenda_picker_day.year,agenda_picker_day.month);
    for(int n=0;n<42;n++) {
        int day=n-first+1;visible(agenda_picker_dates[n],day>0&&day<=days);if(day<=0||day>days)continue;
        char number[4];snprintf(number,sizeof(number),"%d",day);ui_text(lv_obj_get_child(agenda_picker_dates[n],0),number);
        bool active=day==agenda_picker_day.day;ui_bg_color(agenda_picker_dates[n],lv_color_hex(active?BLUE:UI_BACKGROUND),0);tint(lv_obj_get_child(agenda_picker_dates[n],0),active?0x173642:INK);
    }
    int64_t today=agenda_civil_now()/86400;
    int64_t selected=countdown_civil_day(agenda_picker_day.year,agenda_picker_day.month,agenda_picker_day.day)-countdown_civil_day(1970,1,1);
    for(unsigned n=0;n<2;n++){bool active=today&&selected==today+n;ui_bg_color(agenda_picker_days[n],lv_color_hex(active?BLUE:SURFACE),0);tint(lv_obj_get_child(agenda_picker_days[n],0),active?0x173642:MUTED);}
    ui_text(agenda_picker_feedback,"");
}
static void agenda_picker_calendar_changed(lv_event_t *event) {
    int first=(countdown_civil_day(agenda_picker_day.year,agenda_picker_day.month,1)-countdown_civil_day(1970,1,1)+4)%7;
    agenda_picker_day.day=(intptr_t)lv_event_get_user_data(event)-first+1;agenda_picker_render_date();
}
static void agenda_picker_month_step(lv_event_t *event) {
    int month=agenda_picker_day.year*12+agenda_picker_day.month-1+(int)(intptr_t)lv_event_get_user_data(event);
    if(month<2000*12||month>2100*12)return;
    agenda_picker_day.year=month/12;agenda_picker_day.month=month%12+1;
    int next=month+1;int days=countdown_civil_day(next/12,next%12+1,1)-countdown_civil_day(month/12,month%12+1,1);
    if(agenda_picker_day.day>days)agenda_picker_day.day=days;
    agenda_picker_render_date();
}
static void agenda_picker_quick_day(lv_event_t *event) {
    int64_t now=agenda_civil_now();if(!now){ui_text(agenda_picker_feedback,"设备时间未校准");return;}
    time_t stamp=(time_t)(now+(intptr_t)lv_event_get_user_data(event)*86400);struct tm date;gmtime_r(&stamp,&date);
    agenda_picker_day=(lv_calendar_date_t){date.tm_year+1900,date.tm_mon+1,date.tm_mday};agenda_picker_render_date();
}
static void agenda_picker_commit(lv_event_t *event) {
    (void)event;
    int64_t value=(int64_t)(countdown_civil_day(agenda_picker_day.year,agenda_picker_day.month,agenda_picker_day.day)-countdown_civil_day(1970,1,1))*86400+
        lv_roller_get_selected(agenda_picker_hours)*3600+lv_roller_get_selected(agenda_picker_minutes)*60;
    if((!agenda_picker_row&&(value<946684800LL||value>4102444799LL))||
       (agenda_picker_row&&(value<=agenda_editor_values[0]||value-agenda_editor_values[0]>86400))){ui_text(agenda_picker_feedback,agenda_picker_row?"结束须晚于开始，最长 24 小时":"请选择 2000 至 2099 年的时间");return;}
    if(!agenda_picker_row){int64_t duration=agenda_editor_values[1]-agenda_editor_values[0];if(duration<=0||duration>86400)duration=3600;agenda_editor_values[1]=value+duration;}
    agenda_editor_set_date(agenda_picker_row,value);agenda_picker_close(NULL);
}
static void agenda_picker_show(lv_event_t *event) {
    if(agenda_pending_request||agenda_voice_waiting)return;
    agenda_picker_row=(unsigned)(uintptr_t)lv_event_get_user_data(event);if(agenda_picker_row>1)return;
    agenda_editor_keyboard_hide(NULL);lv_dropdown_close(agenda_editor_repeat);lv_dropdown_close(agenda_editor_lead);
    time_t stamp=(time_t)agenda_editor_values[agenda_picker_row];struct tm date;gmtime_r(&stamp,&date);
    agenda_picker_day=(lv_calendar_date_t){date.tm_year+1900,date.tm_mon+1,date.tm_mday};
    lv_roller_set_selected(agenda_picker_hours,date.tm_hour,LV_ANIM_OFF);lv_roller_set_selected(agenda_picker_minutes,date.tm_min,LV_ANIM_OFF);
    ui_text(agenda_picker_heading,agenda_picker_row?"结束时间":"开始时间");agenda_picker_render_date();
    visible(agenda_picker,true);lv_obj_move_foreground(agenda_picker);
}
static void agenda_picker_create(void) {
    agenda_picker=shape(agenda_editor,0,0,1280,720,UI_BACKGROUND,0);lv_obj_add_flag(agenda_picker,LV_OBJ_FLAG_CLICKABLE);
    agenda_picker_heading=label(agenda_picker,"开始时间",64,32,700,false);ui_text_font(agenda_picker_heading,&tab5_font_30,0);
    button(agenda_picker,"取消",902,24,174,agenda_picker_close,0);
    agenda_picker_confirm=button(agenda_picker,"确定",1092,24,124,agenda_picker_commit,0);ui_bg_color(agenda_picker_confirm,lv_color_hex(BLUE),0);tint(lv_obj_get_child(agenda_picker_confirm,0),0x173642);
    shape(agenda_picker,64,108,1152,1,TRACK,0);shape(agenda_picker,744,156,1,474,TRACK,0);
    lv_obj_t *previous=button(agenda_picker,LV_SYMBOL_LEFT,64,144,72,agenda_picker_month_step,-1);ui_text_font(lv_obj_get_child(previous,0),&lv_font_montserrat_24,0);
    lv_obj_t *next=button(agenda_picker,LV_SYMBOL_RIGHT,608,144,72,agenda_picker_month_step,1);ui_text_font(lv_obj_get_child(next,0),&lv_font_montserrat_24,0);
    lv_obj_add_event_cb(previous,agenda_picker_month_step,LV_EVENT_LONG_PRESSED_REPEAT,(void *)(intptr_t)-1);lv_obj_add_event_cb(next,agenda_picker_month_step,LV_EVENT_LONG_PRESSED_REPEAT,(void *)(intptr_t)1);
    agenda_picker_month=label(agenda_picker,"",152,160,440,true);ui_text_font(agenda_picker_month,&tab5_font_30,0);lv_obj_set_style_text_align(agenda_picker_month,LV_TEXT_ALIGN_CENTER,0);
    static const char *days[]={"日","一","二","三","四","五","六"};
    for(int n=0;n<7;n++){lv_obj_t *day=muted(agenda_picker,days[n],64+n*88,228,82);lv_obj_set_style_text_align(day,LV_TEXT_ALIGN_CENTER,0);}
    for(int n=0;n<42;n++){lv_obj_t *day=agenda_picker_dates[n]=button(agenda_picker,"",64+n%7*88,272+n/7*54,82,agenda_picker_calendar_changed,n);ui_text_font(lv_obj_get_child(day,0),&lv_font_montserrat_24,0);}
    for(unsigned n=0;n<2;n++){agenda_picker_days[n]=button(agenda_picker,n?"明天":"今天",64+n*316,618,300,agenda_picker_quick_day,n);lv_obj_set_height(agenda_picker_days[n],54);}
    lv_obj_t *hours=muted(agenda_picker,"小时",800,192,184),*minutes=muted(agenda_picker,"分钟",1012,192,184);lv_obj_set_style_text_align(hours,LV_TEXT_ALIGN_CENTER,0);lv_obj_set_style_text_align(minutes,LV_TEXT_ALIGN_CENTER,0);
    for(unsigned n=0;n<2;n++) {
        char options[180];size_t used=0;unsigned count=n?60:24;for(unsigned i=0;i<count;i++)used+=snprintf(options+used,sizeof(options)-used,i+1<count?"%02u\n":"%02u",i);
        lv_obj_t *roller=lv_roller_create(agenda_picker);if(n)agenda_picker_minutes=roller;else agenda_picker_hours=roller;
        lv_roller_set_options(roller,options,LV_ROLLER_MODE_NORMAL);ui_text_font(roller,&lv_font_montserrat_40,0);ui_text_font(roller,&lv_font_montserrat_40,LV_PART_SELECTED);lv_obj_set_style_text_line_space(roller,32,0);lv_roller_set_visible_row_count(roller,3);lv_obj_set_pos(roller,800+n*212,250);lv_obj_set_width(roller,184);
        lv_obj_set_style_border_width(roller,0,0);lv_obj_set_style_radius(roller,12,0);ui_bg_color(roller,lv_color_hex(UI_BACKGROUND),0);ui_text_color(roller,lv_color_hex(MUTED),0);ui_bg_color(roller,lv_color_hex(SURFACE),LV_PART_SELECTED);ui_text_color(roller,lv_color_hex(INK),LV_PART_SELECTED);
    }
    lv_obj_t *colon=label(agenda_picker,":",982,330,30,true);ui_text_font(colon,&lv_font_montserrat_40,0);lv_obj_set_style_text_align(colon,LV_TEXT_ALIGN_CENTER,0);lv_obj_align_to(colon,agenda_picker_hours,LV_ALIGN_OUT_RIGHT_MID,-1,0);
    agenda_picker_feedback=muted(agenda_picker,"",800,526,416);lv_label_set_long_mode(agenda_picker_feedback,LV_LABEL_LONG_WRAP);tint(agenda_picker_feedback,AMBER);
    visible(agenda_picker,false);
}
