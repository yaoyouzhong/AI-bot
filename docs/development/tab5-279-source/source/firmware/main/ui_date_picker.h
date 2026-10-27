// Calendar edits stay in a draft until the user confirms the selected date.
static lv_calendar_date_t date_picker_draft;
static lv_obj_t *date_picker_year,*date_picker_month,*date_picker_days[42],*date_picker_selection;
static void date_picker_refresh(void) {
    lv_dropdown_set_selected(date_picker_year,date_picker_draft.year-2000);
    lv_dropdown_set_selected(date_picker_month,date_picker_draft.month-1);
    int first=(countdown_civil_day(date_picker_draft.year,date_picker_draft.month,1)-countdown_civil_day(1970,1,1)+4)%7;if(first<0)first+=7;
    int days=countdown_month_days(date_picker_draft.year,date_picker_draft.month);
    if(date_picker_draft.day>days)date_picker_draft.day=days;
    for(int i=0;i<42;i++){
        int day=i-first+1;bool valid=day>=1&&day<=days,selected=valid&&day==date_picker_draft.day;
        if(valid)ui_text_fmt(lv_obj_get_child(date_picker_days[i],0),"%d",day);else ui_text(lv_obj_get_child(date_picker_days[i],0),"");
        visible(date_picker_days[i],valid);
        if(valid)lv_obj_remove_state(date_picker_days[i],LV_STATE_DISABLED);else lv_obj_add_state(date_picker_days[i],LV_STATE_DISABLED);
        ui_bg_color(date_picker_days[i],lv_color_hex(selected?BLUE:SURFACE),0);tint(lv_obj_get_child(date_picker_days[i],0),selected?0x173642:INK);
    }
    ui_text_fmt(date_picker_selection,"%04u 年 %u 月 %u 日",date_picker_draft.year,date_picker_draft.month,date_picker_draft.day);
}
static void date_picker_change(lv_event_t *e) {
    (void)e;date_picker_draft.year=2000+lv_dropdown_get_selected(date_picker_year);date_picker_draft.month=1+lv_dropdown_get_selected(date_picker_month);date_picker_refresh();
}
static void date_picker_step(lv_event_t *e) {
    int month=(date_picker_draft.year-2000)*12+date_picker_draft.month-1+(int)(intptr_t)lv_event_get_user_data(e);
    if(month<0||month>=2400)return;
    date_picker_draft.year=2000+month/12;date_picker_draft.month=1+month%12;date_picker_refresh();
}
static void date_picker_day(lv_event_t *e) {
    const char *s=lv_label_get_text(lv_obj_get_child(lv_event_get_target(e),0));int d=atoi(s);if(d<1)return;
    date_picker_draft.day=d;date_picker_refresh();
}
static void date_picker_confirm(lv_event_t *e) {
    (void)e;date_edit.year=date_picker_draft.year;date_edit.month=date_picker_draft.month;date_edit.day=date_picker_draft.day;
    ui_text_fmt(date_selected_label,"%04u 年 %u 月 %u 日",date_edit.year,date_edit.month,date_edit.day);visible(date_picker,false);
}
static void date_picker_show(lv_event_t *e) {
    (void)e;if(date_voice_waiting)return;date_keyboard_hide(NULL);
    date_picker_draft=(lv_calendar_date_t){date_edit.year,date_edit.month,date_edit.day};date_picker_refresh();visible(date_picker,true);lv_obj_move_foreground(date_picker);
}
static void date_picker_create(void) {
    date_picker=shape(date_editor,0,0,1280,720,UI_BACKGROUND,0);lv_obj_add_flag(date_picker,LV_OBJ_FLAG_CLICKABLE);
    workbench_back_button(date_picker,date_header_clicked);lv_obj_t *title=label(date_picker,"选择目标日期",96,21,800,false);ui_text_font(title,&tab5_font_30,0);
    lv_obj_t *panel=shape(date_picker,224,100,832,488,SURFACE,UI_CARD_RADIUS);
    char years[1200];int used=0;for(int i=2000;i<=2199;i++)used+=snprintf(years+used,sizeof(years)-used,"%s%d",i==2000?"":"\n",i);
    date_picker_year=dropdown(panel,years,216,16,200,0,date_picker_change,0);
    date_picker_month=dropdown(panel,"1 月\n2 月\n3 月\n4 月\n5 月\n6 月\n7 月\n8 月\n9 月\n10 月\n11 月\n12 月",432,16,184,0,date_picker_change,0);
    lv_obj_set_height(lv_dropdown_get_list(date_picker_year),280);lv_obj_set_height(lv_dropdown_get_list(date_picker_month),280);
    lv_obj_t *prev=button(panel,LV_SYMBOL_LEFT,24,16,64,date_picker_step,-1),*next=button(panel,LV_SYMBOL_RIGHT,744,16,64,date_picker_step,1);
    ui_text_font(lv_obj_get_child(prev,0),&lv_font_montserrat_24,0);ui_text_font(lv_obj_get_child(next,0),&lv_font_montserrat_24,0);
    const char *week[]={"日","一","二","三","四","五","六"};
    for(int i=0;i<7;i++){lv_obj_t *w=muted(panel,week[i],24+i*112,84,112);lv_obj_set_style_text_align(w,LV_TEXT_ALIGN_CENTER,0);}
    for(int i=0;i<42;i++){date_picker_days[i]=button(panel,"",24+(i%7)*112,126+(i/7)*56,104,date_picker_day,0);lv_obj_set_height(date_picker_days[i],48);lv_obj_center(lv_obj_get_child(date_picker_days[i],0));}
    date_picker_selection=muted(date_picker,"",224,628,560);
    lv_obj_t *ok=button(date_picker,"确定日期",832,616,224,date_picker_confirm,0);ui_bg_color(ok,lv_color_hex(BLUE),0);tint(lv_obj_get_child(ok,0),0x173642);
    visible(date_picker,false);
}
