// Task writing and time selection are separate interactions, not a grid of date fields.
static lv_obj_t *agenda_editor,*agenda_editor_heading,*agenda_editor_name,*agenda_editor_times,*agenda_editor_untimed;
static bool agenda_editor_has_time;
static lv_obj_t *agenda_editor_repeat,*agenda_editor_lead,*agenda_editor_feedback,*agenda_editor_name_caption;
static lv_obj_t *agenda_editor_keyboard,*agenda_editor_ime,*agenda_editor_back,*agenda_editor_save_button;
static lv_obj_t *agenda_editor_rule,*agenda_editor_divider;
static lv_obj_t *agenda_editor_endpoints[2],*agenda_editor_clocks[2],*agenda_editor_days[2],*agenda_editor_durations[3];
static lv_obj_t *agenda_editor_custom_duration;
static int64_t agenda_editor_values[2];
static char agenda_editor_id[33],agenda_editor_edit[33];
static bool agenda_voice_waiting,agenda_voice_cancelled;
static lv_obj_t *agenda_editor_voice_button,*agenda_editor_voice_cancel,*agenda_editor_voice_switch;
static lv_obj_t *agenda_editor_voice_panel,*agenda_editor_mic_glyph,*agenda_editor_stop_glyph,*agenda_editor_voice_meter;
static void agenda_editor_voice_action(lv_event_t *event);
static void agenda_picker_show(lv_event_t *event);
static void agenda_picker_create(void);
static void agenda_picker_close(lv_event_t *event);
static bool agenda_picker_open(void);
static const unsigned agenda_leads[]={0,1,3,5,10,15,30,60};
static const unsigned agenda_durations[]={1800,3600,7200};
static bool agenda_editor_open(void){return agenda_editor&&!lv_obj_has_flag(agenda_editor,LV_OBJ_FLAG_HIDDEN);}
static void agenda_editor_time_render(void) {
    if(!agenda_editor_clocks[0])return;
    for(unsigned row=0;row<1;row++) {
        time_t stamp=(time_t)agenda_editor_values[row];struct tm t;gmtime_r(&stamp,&t);char clock[16],day[40];strftime(clock,sizeof(clock),"%H:%M",&t);
        time_t now=(time_t)agenda_civil_now();struct tm today;gmtime_r(&now,&today);
        if(t.tm_year==today.tm_year)snprintf(day,sizeof(day),"%d 月 %d 日",t.tm_mon+1,t.tm_mday);
        else snprintf(day,sizeof(day),"%d/%02d/%02d",t.tm_year+1900,t.tm_mon+1,t.tm_mday);
        if(row&&agenda_editor_values[1]/86400==agenda_editor_values[0]/86400)day[0]=0;
        ui_text(agenda_editor_clocks[row],clock);ui_text(agenda_editor_days[row],day);
    }
    for(unsigned n=0;n<3;n++){bool active=agenda_editor_values[1]-agenda_editor_values[0]==agenda_durations[n];ui_bg_color(agenda_editor_durations[n],lv_color_hex(active?BLUE:SURFACE),0);tint(lv_obj_get_child(agenda_editor_durations[n],0),active?0x173642:MUTED);}
    unsigned minutes=(unsigned)((agenda_editor_values[1]-agenda_editor_values[0])/60);bool custom=minutes!=30&&minutes!=60&&minutes!=120;
    if(custom&&minutes<=1440)ui_text_fmt(agenda_editor_custom_duration,"%u 时 %u 分",minutes/60,minutes%60);else ui_text(agenda_editor_custom_duration,"");
    ui_bg_color(agenda_editor_endpoints[1],lv_color_hex(custom?BLUE:SURFACE),0);tint(lv_obj_get_child(agenda_editor_endpoints[1],0),custom?0x173642:MUTED);
}
static void agenda_editor_set_date(unsigned row,int64_t value){agenda_editor_values[row]=value;agenda_editor_time_render();}
static int64_t agenda_editor_date(unsigned row){return agenda_editor_values[row];}
static void agenda_editor_feedback_layout(void) {
    bool keyboard=!lv_obj_has_flag(agenda_editor_keyboard,LV_OBJ_FLAG_HIDDEN);
    // Idle messages belong to the name field, not to an invisible cancel icon.
    // Keep that left edge while recording; actions use their own row/column.
    lv_obj_set_pos(agenda_editor_feedback,0,0);
    lv_obj_set_size(agenda_editor_feedback,keyboard?(agenda_voice_waiting?864:1152):600,70);
    lv_obj_set_pos(agenda_editor_voice_meter,0,keyboard?68:76);
    lv_obj_set_width(agenda_editor_voice_meter,keyboard?864:600);
    lv_obj_set_pos(agenda_editor_voice_cancel,keyboard?880:0,keyboard?0:96);
    lv_obj_set_pos(agenda_editor_voice_switch,keyboard?960:88,keyboard?0:96);
    lv_obj_set_size(agenda_editor_voice_switch,keyboard?192:260,keyboard?72:56);
}
static void agenda_editor_keyboard_hide(lv_event_t *event) {
    (void)event;tab5_ime_show_for(agenda_editor_ime,false);visible(agenda_editor_keyboard,false);visible(agenda_editor_rule,true);visible(agenda_editor_divider,true);
    visible(agenda_editor_times,agenda_editor_has_time);visible(agenda_editor_untimed,!agenda_editor_has_time);
    lv_obj_set_pos(agenda_editor_name,64,198);lv_obj_set_size(agenda_editor_name,496,116);lv_obj_set_style_pad_ver(agenda_editor_name,38,0);lv_obj_set_y(agenda_editor_name_caption,150);
    lv_obj_set_pos(agenda_editor_voice_button,592,220);
    lv_obj_set_pos(agenda_editor_voice_panel,64,338);lv_obj_set_size(agenda_editor_voice_panel,600,176);
    agenda_editor_feedback_layout();ui_text(lv_obj_get_child(agenda_editor_back,0),"返回");
}
static void agenda_editor_keyboard_show(lv_event_t *event) {
    (void)event;if(agenda_voice_waiting)return;visible(agenda_editor_rule,false);visible(agenda_editor_divider,false);
    visible(agenda_editor_times,false);visible(agenda_editor_untimed,false);
    lv_obj_set_pos(agenda_editor_name,64,142);lv_obj_set_size(agenda_editor_name,1056,96);lv_obj_set_style_pad_ver(agenda_editor_name,28,0);lv_obj_set_y(agenda_editor_name_caption,102);
    lv_obj_set_pos(agenda_editor_voice_button,1144,154);
    lv_obj_set_pos(agenda_editor_voice_panel,64,260);lv_obj_set_size(agenda_editor_voice_panel,1152,72);
    visible(agenda_editor_keyboard,true);lv_keyboard_set_textarea(agenda_editor_keyboard,agenda_editor_name);tab5_ime_show_for(agenda_editor_ime,true);ui_text(lv_obj_get_child(agenda_editor_back,0),"完成输入");
    agenda_editor_feedback_layout();
}
static void agenda_editor_duration(lv_event_t *event){unsigned n=(unsigned)(uintptr_t)lv_event_get_user_data(event);if(n<3)agenda_editor_set_date(1,agenda_editor_values[0]+agenda_durations[n]);}
static void agenda_editor_close(lv_event_t *event) {
    (void)event;if(agenda_pending_editor)return;
    if(agenda_voice_waiting){voice_remote_command(2);agenda_voice_cancelled=true;ui_text(agenda_editor_feedback,"正在取消录音，原名称已保留");return;}
    if(agenda_picker_open()){agenda_picker_close(NULL);return;}
    if(!lv_obj_has_flag(agenda_editor_keyboard,LV_OBJ_FLAG_HIDDEN)){agenda_editor_keyboard_hide(NULL);return;}
    lv_dropdown_close(agenda_editor_repeat);lv_dropdown_close(agenda_editor_lead);visible(agenda_editor,false);lv_display_trigger_activity(NULL);
}
static void agenda_editor_result(void) {
    lv_obj_remove_state(agenda_editor_save_button,LV_STATE_DISABLED);tint(lv_obj_get_child(agenda_editor_save_button,0),0x173642);
    if(agenda_ui.last_success){strcpy(agenda_selected,agenda_ui.saved_id);agenda_today=false;agenda_editor_keyboard_hide(NULL);visible(agenda_editor,false);}
    else {agenda_editor_keyboard_hide(NULL);ui_text(agenda_editor_feedback,agenda_ui.message);}
}
static void agenda_editor_save_click(lv_event_t *event) {
    (void)event;if(agenda_pending_request||agenda_voice_waiting||agenda_picker_open())return;tab5_ime_finish_for(agenda_editor_ime);
    if(tab5_ime_pending_for(agenda_editor_ime)[0]){ui_text(agenda_editor_feedback,"名称最多 32 个字，请先缩短内容");return;}
    const char *title=lv_textarea_get_text(agenda_editor_name);while(*title==' ')title++;size_t length=strlen(title);while(length&&title[length-1]==' ')length--;
    if(!length||length>96){ui_text(agenda_editor_feedback,"请输入任务名称，最多 32 个字");return;}
    agenda_item value={0};memcpy(value.title,title,length);strcpy(value.id,agenda_editor_id);
    if(agenda_editor_has_time) {
        value.due=agenda_editor_date(0);value.end=agenda_editor_date(1);value.repeat=lv_dropdown_get_selected(agenda_editor_repeat);value.lead=agenda_leads[lv_dropdown_get_selected(agenda_editor_lead)];
        if(value.due<946684800LL||value.due>4102444799LL||value.end<=value.due||value.end-value.due>86400){ui_text(agenda_editor_feedback,"结束须晚于开始，单次最长 24 小时");return;}
    }
    if(!agenda_editor_id[0]&&agenda_expired(&value,agenda_civil_now())){ui_text(agenda_editor_feedback,"结束时间已过，请选择未来时间");return;}
    agenda_pending_request=agenda_save(&value,agenda_editor_edit);agenda_pending_editor=agenda_pending_request!=0;
    if(agenda_pending_editor){lv_obj_add_state(agenda_editor_save_button,LV_STATE_DISABLED);tint(lv_obj_get_child(agenda_editor_save_button,0),MUTED);ui_text(agenda_editor_feedback,"正在保存到本机…");}
    else ui_text(agenda_editor_feedback,"未保存，请稍后重试");
}
static void agenda_editor_show(lv_event_t *event) {
    if(agenda_pending_request||agenda_voice_waiting)return;
    agenda_get(&agenda_ui);bool editing=(intptr_t)lv_event_get_user_data(event)!=0;const agenda_item *item=editing?agenda_selected_item(agenda_selected):NULL;if(editing&&!item)return;
    agenda_editor_id[0]=agenda_editor_edit[0]=0;if(item){strcpy(agenda_editor_id,item->id);strcpy(agenda_editor_edit,item->edit);}
    ui_text(agenda_editor_heading,editing?"编辑事项":"新建事项");lv_textarea_set_text(agenda_editor_name,item?item->title:"");
    int64_t start=item&&item->due?item->due:agenda_civil_now()+3600;if(start<946684800LL)start=1767258000LL;start=start/60*60;
    agenda_editor_set_date(0,start);agenda_editor_set_date(1,item&&item->end?item->end:start+3600);
    lv_dropdown_set_selected(agenda_editor_repeat,item?item->repeat:0);unsigned lead=0;for(unsigned n=0;n<sizeof(agenda_leads)/sizeof(agenda_leads[0]);n++)if(item&&item->lead==agenda_leads[n])lead=n;lv_dropdown_set_selected(agenda_editor_lead,lead);
    agenda_editor_has_time=!item||item->due!=0;
    ui_text(agenda_editor_feedback,agenda_civil_now()?"":"设备时间未校准，保存后需先校准时间才能提醒");tint(agenda_editor_feedback,agenda_civil_now()?MUTED:AMBER);lv_obj_remove_state(agenda_editor_save_button,LV_STATE_DISABLED);tint(lv_obj_get_child(agenda_editor_save_button,0),0x173642);
    visible(agenda_editor,true);lv_obj_move_foreground(agenda_editor);agenda_picker_close(NULL);agenda_editor_keyboard_hide(NULL);
}
static void agenda_editor_create(lv_obj_t *screen) {
    agenda_editor=shape(screen,0,0,1280,720,UI_BACKGROUND,0);lv_obj_add_flag(agenda_editor,LV_OBJ_FLAG_CLICKABLE);
    agenda_editor_heading=label(agenda_editor,"新建事项",64,32,700,false);ui_text_font(agenda_editor_heading,&tab5_font_30,0);
    agenda_editor_back=button(agenda_editor,"返回",902,24,174,agenda_editor_close,0);
    agenda_editor_save_button=button(agenda_editor,"保存",1092,24,124,agenda_editor_save_click,0);ui_bg_color(agenda_editor_save_button,lv_color_hex(BLUE),0);tint(lv_obj_get_child(agenda_editor_save_button,0),0x173642);
    agenda_editor_rule=shape(agenda_editor,64,108,1152,1,TRACK,0);agenda_editor_divider=shape(agenda_editor,712,148,1,454,TRACK,0);
    agenda_editor_name_caption=muted(agenda_editor,"任务名称",64,150,600);
    agenda_editor_name=lv_textarea_create(agenda_editor);lv_obj_set_pos(agenda_editor_name,64,198);lv_obj_set_size(agenda_editor_name,496,116);lv_textarea_set_one_line(agenda_editor_name,true);lv_textarea_set_max_length(agenda_editor_name,32);lv_textarea_set_placeholder_text(agenda_editor_name,"输入任务名称");
    lv_obj_set_style_anim_duration(agenda_editor_name,0,LV_PART_CURSOR);ui_text_font(agenda_editor_name,&tab5_font_30,0);ui_text_color(agenda_editor_name,lv_color_hex(INK),0);ui_bg_color(agenda_editor_name,lv_color_hex(SURFACE),0);ui_bg_color(agenda_editor_name,lv_color_hex(SURFACE),LV_STATE_DISABLED);ui_text_color(agenda_editor_name,lv_color_hex(MUTED),LV_STATE_DISABLED);lv_obj_set_style_color_filter_opa(agenda_editor_name,LV_OPA_TRANSP,LV_STATE_DISABLED);
    lv_obj_set_style_border_width(agenda_editor_name,0,0);lv_obj_set_style_radius(agenda_editor_name,UI_CONTROL_RADIUS,0);lv_obj_set_style_pad_hor(agenda_editor_name,22,0);lv_obj_add_event_cb(agenda_editor_name,agenda_editor_keyboard_show,LV_EVENT_CLICKED,NULL);
    agenda_editor_voice_panel=shape(agenda_editor,64,350,600,160,UI_BACKGROUND,0);ui_bg_opa(agenda_editor_voice_panel,LV_OPA_TRANSP,0);
    // Voice controls use the same icon and state language as the Codex composer.
    agenda_editor_voice_button=button(agenda_editor,"",592,220,72,agenda_editor_voice_action,0);lv_obj_set_height(agenda_editor_voice_button,72);lv_obj_set_style_pad_all(agenda_editor_voice_button,0,0);lv_obj_set_style_radius(agenda_editor_voice_button,LV_RADIUS_CIRCLE,0);ui_bg_color(agenda_editor_voice_button,lv_color_hex(0x354247),0);
    voice_button_glyphs(agenda_editor_voice_button,&agenda_editor_mic_glyph,&agenda_editor_stop_glyph);
    agenda_editor_voice_cancel=button(agenda_editor_voice_panel,LV_SYMBOL_TRASH,88,0,72,agenda_editor_voice_action,2);lv_obj_set_height(agenda_editor_voice_cancel,72);lv_obj_set_style_pad_all(agenda_editor_voice_cancel,0,0);lv_obj_set_style_radius(agenda_editor_voice_cancel,LV_RADIUS_CIRCLE,0);
    lv_obj_t *cancel_glyph=lv_obj_get_child(agenda_editor_voice_cancel,0);ui_text_font(cancel_glyph,&lv_font_montserrat_24,0);lv_obj_center(cancel_glyph);visible(agenda_editor_voice_cancel,false);
    agenda_editor_voice_switch=button(agenda_editor_voice_panel,"改用 TAB5",176,100,260,agenda_editor_voice_action,3);lv_obj_set_height(agenda_editor_voice_switch,56);visible(agenda_editor_voice_switch,false);
    agenda_editor_voice_meter=lv_bar_create(agenda_editor_voice_panel);lv_obj_set_pos(agenda_editor_voice_meter,176,80);lv_obj_set_size(agenda_editor_voice_meter,424,6);ui_bg_color(agenda_editor_voice_meter,lv_color_hex(TRACK),LV_PART_MAIN);ui_bg_color(agenda_editor_voice_meter,lv_color_hex(GREEN),LV_PART_INDICATOR);visible(agenda_editor_voice_meter,false);
    agenda_editor_untimed=muted(agenda_editor,"无时间待办",784,298,416);
    agenda_editor_times=shape(agenda_editor,748,136,468,484,UI_BACKGROUND,0);
    for(unsigned row=0;row<1;row++) {
        lv_obj_t *endpoint=agenda_editor_endpoints[row]=button(agenda_editor_times,"",0,row?280:0,468,agenda_picker_show,row);lv_obj_set_height(endpoint,row?80:136);ui_bg_color(endpoint,lv_color_hex(UI_BACKGROUND),0);visible(lv_obj_get_child(endpoint,0),false);
        muted(endpoint,"开始",20,8,160);
        agenda_editor_clocks[row]=label(endpoint,"--:--",row?180:20,row?10:44,row?220:292,false);ui_text_font(agenda_editor_clocks[row],row?&tab5_font_30:&tab5_digits_72,0);
        agenda_editor_days[row]=muted(endpoint,"",row?180:268,row?48:12,row?240:180);
        lv_obj_t *arrow=label(endpoint,LV_SYMBOL_RIGHT,420,row?26:72,24,false);ui_text_font(arrow,&lv_font_montserrat_24,0);tint(arrow,MUTED);
    }
    shape(agenda_editor_times,20,140,428,1,TRACK,0);
    muted(agenda_editor_times,"持续时长",20,166,240);
    agenda_editor_custom_duration=muted(agenda_editor_times,"",260,166,188);
    const char *durations[]={"30分钟","1小时","2小时"};for(unsigned n=0;n<3;n++){agenda_editor_durations[n]=button(agenda_editor_times,durations[n],n*120,206,108,agenda_editor_duration,n);lv_obj_set_height(agenda_editor_durations[n],48);}
    agenda_editor_endpoints[1]=button(agenda_editor_times,"自定义",360,206,108,agenda_picker_show,1);lv_obj_set_height(agenda_editor_endpoints[1],48);
    muted(agenda_editor_times,"重复",20,314,112);muted(agenda_editor_times,"提醒",20,378,112);
    agenda_editor_repeat=dropdown(agenda_editor_times,"不重复\n每天\n周一至周五\n每周\n工作日",144,304,304,0,agenda_editor_keyboard_hide,0);
    agenda_editor_lead=dropdown(agenda_editor_times,"准时提醒\n提前 1 分钟\n提前 3 分钟\n提前 5 分钟\n提前 10 分钟\n提前 15 分钟\n提前 30 分钟\n提前 60 分钟",144,368,304,0,agenda_editor_keyboard_hide,0);
    agenda_editor_feedback=muted(agenda_editor_voice_panel,"",176,0,424);lv_obj_set_height(agenda_editor_feedback,70);lv_label_set_long_mode(agenda_editor_feedback,LV_LABEL_LONG_DOT);
    agenda_editor_keyboard=lv_keyboard_create(agenda_editor);lv_obj_set_align(agenda_editor_keyboard,LV_ALIGN_TOP_LEFT);lv_obj_set_pos(agenda_editor_keyboard,30,400);lv_obj_set_size(agenda_editor_keyboard,1220,300);
    ui_bg_color(agenda_editor_keyboard,lv_color_hex(UI_BACKGROUND),0);ui_bg_color(agenda_editor_keyboard,lv_color_hex(SURFACE),LV_PART_ITEMS);ui_text_color(agenda_editor_keyboard,lv_color_hex(INK),LV_PART_ITEMS);ui_text_font(agenda_editor_keyboard,&lv_font_montserrat_24,LV_PART_ITEMS);
    lv_obj_set_style_border_width(agenda_editor_keyboard,0,0);lv_obj_set_style_pad_all(agenda_editor_keyboard,8,0);lv_obj_set_style_pad_row(agenda_editor_keyboard,8,0);lv_obj_set_style_pad_column(agenda_editor_keyboard,8,0);lv_obj_set_style_border_width(agenda_editor_keyboard,0,LV_PART_ITEMS);lv_obj_set_style_radius(agenda_editor_keyboard,8,LV_PART_ITEMS);
    ui_bg_color(agenda_editor_keyboard,lv_color_hex(0x40525A),LV_PART_ITEMS|LV_STATE_CHECKED);ui_text_color(agenda_editor_keyboard,lv_color_hex(INK),LV_PART_ITEMS|LV_STATE_CHECKED);ui_bg_color(agenda_editor_keyboard,lv_color_hex(BLUE),LV_PART_ITEMS|LV_STATE_PRESSED);ui_text_color(agenda_editor_keyboard,lv_color_hex(0x173642),LV_PART_ITEMS|LV_STATE_PRESSED);
    lv_obj_add_event_cb(agenda_editor_keyboard,agenda_editor_keyboard_hide,LV_EVENT_READY,NULL);lv_obj_add_event_cb(agenda_editor_keyboard,agenda_editor_keyboard_hide,LV_EVENT_CANCEL,NULL);agenda_editor_ime=tab5_ime_create(agenda_editor,agenda_editor_keyboard,agenda_editor_name,&tab5_font_24);lv_obj_set_pos(agenda_editor_ime,30,344);
    agenda_picker_create();visible(agenda_editor_keyboard,false);visible(agenda_editor,false);
}
#include "ui_agenda_time_picker.h"
#include "ui_agenda_voice.h"
