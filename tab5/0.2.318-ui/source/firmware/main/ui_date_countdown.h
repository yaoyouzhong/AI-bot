#include "countdown.h"
static ui_date_countdown date_countdown;
static lv_obj_t *date_saver_layer,*date_saver_title,*date_saver_days,*date_saver_note,*date_saver_target;
static lv_obj_t *date_editor,*date_title_input,*date_selected_label,*date_feedback,*date_keyboard,*date_ime;
static lv_obj_t *date_form,*date_target_fields,*date_footer,*date_header_button,*date_title_count;
static lv_obj_t *date_settings_row,*screen_sound_group;
static lv_obj_t *tf_storage_row,*tf_card_value,*tf_storage_value;
static lv_obj_t *tf_cache_selector,*tf_cache_feedback;
static bool tf_cache_enabled;
extern const uint8_t tab5_date_serif_data[];
extern const size_t tab5_date_serif_size;
static lv_font_t *date_vector_fonts[5];
static int date_vector_sizes[5];
static struct {int height,width;char text[97];} date_vector_keys[5];
static ui_date_countdown date_edit;
static lv_obj_t *date_saver_rule,*date_saver_unit,*date_picker;
static lv_obj_t *date_editor_voice_button,*date_editor_voice_cancel,*date_editor_voice_switch,*date_editor_voice_meter;
static lv_obj_t *date_editor_mic_glyph,*date_editor_stop_glyph,*date_editor_save_button;
static bool date_voice_waiting,date_voice_cancelled;
static void date_editor_voice_action(lv_event_t *e);
static void date_picker_show(lv_event_t *e);
static void date_picker_create(void);
static int date_scaled_text(lv_obj_t *obj,const lv_font_t *font,int height,int width) {
    ui_text_font(obj,font,0);lv_obj_set_size(obj,LV_SIZE_CONTENT,LV_SIZE_CONTENT);
    lv_point_t size;lv_text_get_size(&size,lv_label_get_text(obj),font,0,0,LV_COORD_MAX,LV_TEXT_FLAG_NONE);
    int scale=height*256/font->line_height;if(size.x>0&&size.x*scale/256>width)scale=width*256/size.x;
    lv_obj_set_style_transform_pivot_x(obj,0,0);lv_obj_set_style_transform_pivot_y(obj,0,0);
    lv_obj_set_style_transform_scale(obj,scale,0);return size.x*scale/256;
}
// Each label owns its size and small glyph cache. Never scale up a rasterized outline.
static const lv_font_t *date_vector_font(lv_obj_t *obj) {
    lv_obj_t *labels[]={date_saver_title,date_saver_days,date_saver_note,date_saver_target,date_saver_unit};
    int index=0;while(index<5&&labels[index]!=obj)index++;
    if(index==5)return &tab5_font_30;
    if(!date_vector_fonts[index]){
        date_vector_fonts[index]=lv_tiny_ttf_create_data_ex(tab5_date_serif_data,tab5_date_serif_size,64,LV_FONT_KERNING_NONE,index==0?12:index==1?6:16);
        date_vector_sizes[index]=64;
    }
    if(!date_vector_fonts[index])return index==1?&tab5_annual_digits_144:&tab5_font_30;
    const char *text=lv_label_get_text(obj);uint32_t at=0;
    while(text[at]){uint32_t ch=lv_text_encoded_next(text,&at);lv_font_glyph_dsc_t glyph;
        if(!lv_font_get_glyph_dsc(date_vector_fonts[index],&glyph,ch,0)||glyph.is_placeholder)return &tab5_font_30;}
    return date_vector_fonts[index];
}
static int date_vector_text(lv_obj_t *obj,int height,int max_width) {
    const lv_font_t *font=date_vector_font(obj);int index=0;
    while(index<5&&date_vector_fonts[index]!=font)index++;
    if(index==5)return date_scaled_text(obj,font,height,max_width);
    lv_point_t measured;lv_text_get_size(&measured,lv_label_get_text(obj),font,0,0,LV_COORD_MAX,LV_TEXT_FLAG_NONE);
    if(lv_obj_get_style_text_font(obj,0)==font&&lv_obj_get_style_transform_scale_x(obj,0)==256&&date_vector_keys[index].height==height&&date_vector_keys[index].width==max_width&&!strcmp(date_vector_keys[index].text,lv_label_get_text(obj)))return measured.x;
    int size=LV_MAX(1,date_vector_sizes[index]*height/font->line_height);
    if(measured.x>0)size=LV_MIN(size,LV_MAX(1,date_vector_sizes[index]*max_width/measured.x));
    if(size!=date_vector_sizes[index]){lv_tiny_ttf_set_size(date_vector_fonts[index],size);date_vector_sizes[index]=size;}
    lv_text_get_size(&measured,lv_label_get_text(obj),font,0,0,LV_COORD_MAX,LV_TEXT_FLAG_NONE);
    // Rounding font advances can exceed the estimate by a pixel per character.
    while(measured.x>max_width&&size>1){lv_tiny_ttf_set_size(date_vector_fonts[index],--size);date_vector_sizes[index]=size;
        lv_text_get_size(&measured,lv_label_get_text(obj),font,0,0,LV_COORD_MAX,LV_TEXT_FLAG_NONE);}
    ui_text_font(obj,font,0);lv_obj_set_size(obj,LV_SIZE_CONTENT,LV_SIZE_CONTENT);
    lv_obj_set_style_transform_scale(obj,256,0);lv_obj_refresh_self_size(obj);lv_obj_invalidate(obj);
    date_vector_keys[index].height=height;date_vector_keys[index].width=max_width;
    snprintf(date_vector_keys[index].text,sizeof(date_vector_keys[index].text),"%s",lv_label_get_text(obj));
    return measured.x;
}
static int date_font_baseline(lv_obj_t *obj) {
    const lv_font_t *font=lv_obj_get_style_text_font(obj,0);
    return (font->line_height-font->base_line)*lv_obj_get_style_transform_scale_x(obj,0)/256;
}
static int date_ink_ascent(lv_obj_t *obj) {
    const lv_font_t *font=lv_obj_get_style_text_font(obj,0);const char *value=lv_label_get_text(obj);uint32_t at=0;int ascent=0;
    while(value[at]){lv_font_glyph_dsc_t g;uint32_t cp=lv_text_encoded_next(value,&at);
        if(lv_font_get_glyph_dsc(font,&g,cp,0))ascent=LV_MAX(ascent,g.box_h+g.ofs_y);}
    return ascent*lv_obj_get_style_transform_scale_x(obj,0)/256;
}
static void date_title_changed(lv_event_t *e) {
    (void)e;unsigned count=lv_text_get_encoded_length(lv_textarea_get_text(date_title_input));
    ui_text_fmt(date_title_count,"%u / 10",count);tint(date_title_count,count>10?RED:MUTED);
    if(date_editor_save_button){
        bool disabled=date_voice_waiting||!count||count>10;
        if(disabled)lv_obj_add_state(date_editor_save_button,LV_STATE_DISABLED);else lv_obj_remove_state(date_editor_save_button,LV_STATE_DISABLED);
        tint(lv_obj_get_child(date_editor_save_button,0),disabled?MUTED:0x173642);
    }
    if(count>10){ui_text_fmt(date_feedback,"已保留完整内容，请删减 %u 个字后保存",count-10);tint(date_feedback,AMBER);}
    else if(e){ui_text(date_feedback,"编辑完成后请保存");tint(date_feedback,MUTED);}
}
static void date_title_insert(lv_event_t *e) {
    const char *added=lv_event_get_param(e);if(!added||added[0]==LV_KEY_DEL)return;
    const char *base=lv_textarea_get_text(date_title_input);
    if(strlen(added)>96||strlen(base)+strlen(added)>96||lv_text_get_encoded_length(base)+lv_text_get_encoded_length(added)>10){
        lv_textarea_set_insert_replace(date_title_input,"");ui_text(date_feedback,"名称最多 10 个字，请缩短后输入");tint(date_feedback,AMBER);
    }
}
static void date_settings_refresh(void) {
    if(!date_settings_row)return;
    bool selected=saver_style==UI_SAVER_COUNTDOWN;
    visible(date_settings_row,selected);
    lv_obj_set_y(screen_sound_group,selected?466:386);
    lv_obj_set_height(lv_obj_get_parent(date_settings_row),selected?536:456);
}
static bool date_editor_open(void){return date_editor&&!lv_obj_has_flag(date_editor,LV_OBJ_FLAG_HIDDEN);}
static void date_saver_update(const struct tm *local) {
    bool day=local&&local->tm_year>=100&&local->tm_hour>=7&&local->tm_hour<19;
    uint32_t bg=day?0xF2F0E9:0x1C2022,ink=day?0x292D2E:0xF0EDE4,gold=day?0x9D8355:0xC8B58B;
    ui_bg_color(date_saver_layer,lv_color_hex(bg),0);
    ui_text(date_saver_title,date_countdown.version?date_countdown.title:"日期倒计时");
    int width=date_vector_text(date_saver_title,210,500);lv_obj_set_pos(date_saver_title,80,352-date_font_baseline(date_saver_title));
    lv_obj_set_width(date_saver_rule,LV_CLAMP(96,width*2/3,400));ui_bg_color(date_saver_rule,lv_color_hex(gold),0);
    tint(date_saver_title,ink);tint(date_saver_days,ink);tint(date_saver_note,gold);tint(date_saver_unit,gold);tint(date_saver_target,day?0x77746C:0xA9AAA4);
    bool valid=date_countdown.version&&local&&local->tm_year>=100;visible(date_saver_unit,valid);
    if(!valid){
        ui_text(date_saver_days,"");ui_text(date_saver_note,date_countdown.version?"等待同步日期":"请先设置目标日期");
        date_scaled_text(date_saver_note,&tab5_font_30,32,520);lv_obj_set_pos(date_saver_note,660,330);ui_text(date_saver_target,"");return;
    }
    int days=countdown_civil_day(date_countdown.year,date_countdown.month,date_countdown.day)-countdown_civil_day(local->tm_year+1900,local->tm_mon+1,local->tm_mday);
    ui_text_fmt(date_saver_days,"%d",days<0?-days:days);ui_text(date_saver_note,days>0?"还有":days==0?"就是今天":"已过去");
    ui_text_fmt(date_saver_target,"%04u 年 %u 月 %u 日",date_countdown.year,date_countdown.month,date_countdown.day);
    date_vector_text(date_saver_target,44,500);lv_obj_set_pos(date_saver_target,80,446);
    int digits=date_vector_text(date_saver_days,420,454),unit=date_vector_text(date_saver_unit,84,70);
    int x=650+(550-digits-unit-20)/2;
    int baseline=454,digit_y=baseline-date_font_baseline(date_saver_days);
    lv_obj_set_pos(date_saver_days,x,digit_y);
    lv_obj_set_pos(date_saver_unit,x+digits+20,baseline-date_font_baseline(date_saver_unit));
    date_vector_text(date_saver_note,56,520);lv_obj_set_pos(date_saver_note,x,baseline-date_ink_ascent(date_saver_days)-32-date_font_baseline(date_saver_note));
    if(!days){visible(date_saver_unit,false);ui_text(date_saver_days,"");int today=date_vector_text(date_saver_note,76,520);lv_obj_set_pos(date_saver_note,650+(550-today)/2,290);}
    int shift=(local->tm_min%3-1)*4;lv_obj_set_pos(date_saver_layer,shift-4,-shift-4);
}
static void date_keyboard_hide(lv_event_t *e) {
    (void)e;tab5_ime_show_for(date_ime,false);visible(date_keyboard,false);
    visible(date_target_fields,true);visible(date_footer,true);visible(date_feedback,true);
    lv_obj_set_height(date_form,368);lv_obj_set_y(date_feedback,536);
}
static void date_keyboard_show(lv_event_t *e) {
    (void)e;if(date_voice_waiting)return;visible(date_keyboard,true);lv_keyboard_set_textarea(date_keyboard,date_title_input);tab5_ime_show_for(date_ime,true);
    visible(date_target_fields,false);visible(date_footer,false);visible(date_feedback,true);
    lv_obj_set_height(date_form,180);lv_obj_set_y(date_feedback,336);
}
static void date_editor_close(lv_event_t *e) {
    (void)e;if(date_voice_waiting){lv_obj_send_event(date_editor_voice_cancel,LV_EVENT_CLICKED,NULL);return;}visible(date_picker,false);date_keyboard_hide(NULL);visible(date_editor,false);lv_display_trigger_activity(NULL);last_change=esp_timer_get_time()/1000;
}
static void date_header_clicked(lv_event_t *e) {
    if(date_voice_waiting){lv_obj_send_event(date_editor_voice_cancel,LV_EVENT_CLICKED,NULL);return;}
    if(!lv_obj_has_flag(date_picker,LV_OBJ_FLAG_HIDDEN)){visible(date_picker,false);return;}
    if(!lv_obj_has_flag(date_keyboard,LV_OBJ_FLAG_HIDDEN))date_keyboard_hide(e);else date_editor_close(e);
}
static void date_editor_save(lv_event_t *e) {
    (void)e;if(date_voice_waiting)return;tab5_ime_finish_for(date_ime);
    if(tab5_ime_pending_for(date_ime)[0]){ui_text(date_feedback,"名称最多 10 个字，请先删除部分内容");return;}
    ui_date_countdown next=date_edit;next.version=1;
    const char *title=lv_textarea_get_text(date_title_input);
    if(!title[0]||strlen(title)>=sizeof(next.title)||lv_text_get_encoded_length(title)>10){ui_text(date_feedback,"请输入事件名称，最多 10 个字");return;}
    if(!countdown_date_valid(next.year,next.month,next.day)){ui_text(date_feedback,"日期无效，请检查月份和天数");return;}
    snprintf(next.title,sizeof(next.title),"%s",title);
    if(!ui_date_countdown_save(&next)){ui_text(date_feedback,"保存失败，请重试");return;}
    date_countdown=next;date_saver_update(saver_has_time?&saver_local_time:NULL);date_editor_close(NULL);
}
static void date_editor_show(lv_event_t *e) {
    (void)e;ui_date_countdown value=date_countdown;
    if(!value.version){value.year=saver_has_time?saver_local_time.tm_year+1900:2026;value.month=saver_has_time?saver_local_time.tm_mon+1:1;
        value.day=saver_has_time?saver_local_time.tm_mday:1;snprintf(value.title,sizeof(value.title),"重要日子");}
    lv_textarea_set_text(date_title_input,value.title);
    date_edit=value;ui_text_fmt(date_selected_label,"%04u 年 %u 月 %u 日",value.year,value.month,value.day);date_title_changed(NULL);visible(date_picker,false);
    ui_text(date_feedback,"");
    visible(date_editor,true);lv_obj_move_foreground(date_editor);date_keyboard_hide(NULL);
}
static void date_saver_create(void) {
    date_saver_layer=shape(saver_layer,-4,-4,1288,728,0x1C2022,0);
    date_saver_title=label(date_saver_layer,"日期倒计时",80,256,500,false);
    date_saver_days=label(date_saver_layer,"",680,234,500,false);
    date_saver_note=label(date_saver_layer,"请先设置目标日期",680,174,500,false);
    date_saver_target=label(date_saver_layer,"",80,446,500,false);
    date_saver_unit=label(date_saver_layer,"天",1120,400,70,false);
    date_saver_rule=shape(date_saver_layer,80,410,180,2,0xC8B58B,0);
    lv_obj_t *labels[]={date_saver_title,date_saver_days,date_saver_note,date_saver_target,date_saver_unit};
    for(int i=0;i<5;i++){lv_obj_set_style_text_align(labels[i],LV_TEXT_ALIGN_LEFT,0);lv_label_set_long_mode(labels[i],LV_LABEL_LONG_CLIP);}
    clock_touch_target(date_saver_layer);lv_obj_remove_flag(date_saver_layer,LV_OBJ_FLAG_CLICKABLE);visible(date_saver_layer,false);
    ui_date_countdown_load(&date_countdown);
}
static UI_INIT_SMALL void date_editor_create(lv_obj_t *screen) {
    date_editor=shape(screen,0,0,1280,720,UI_BACKGROUND,0);lv_obj_add_flag(date_editor,LV_OBJ_FLAG_CLICKABLE);
    lv_obj_t *title=label(date_editor,"日期倒计时",96,21,700,false);ui_text_font(title,&tab5_font_30,0);
    muted(date_editor,"设置事件名称与目标日期",96,96,900);
    date_header_button=workbench_back_button(date_editor,date_header_clicked);
    date_form=shape(date_editor,240,152,800,368,SURFACE,UI_CARD_RADIUS);
    muted(date_form,"事件名称",32,24,560);date_title_count=muted(date_form,"0 / 10",656,24,112);
    date_title_input=lv_textarea_create(date_form);lv_obj_set_pos(date_title_input,32,68);lv_obj_set_size(date_title_input,640,76);
    lv_textarea_set_one_line(date_title_input,true);lv_obj_set_height(date_title_input,76);lv_textarea_set_max_length(date_title_input,0);
    // A hidden editor must not leave a perpetual cursor animation blocking cache prewarming.
    lv_obj_set_style_anim_duration(date_title_input,0,LV_PART_CURSOR);
    lv_textarea_set_cursor_pos(date_title_input,0);
    ui_text_font(date_title_input,&tab5_font_30,0);ui_text_color(date_title_input,lv_color_hex(INK),0);ui_bg_color(date_title_input,lv_color_hex(0x354247),0);
    lv_obj_set_style_color_filter_opa(date_title_input,LV_OPA_TRANSP,LV_STATE_DISABLED);
    ui_bg_color(date_title_input,lv_color_hex(0x354247),LV_STATE_DISABLED);ui_text_color(date_title_input,lv_color_hex(MUTED),LV_STATE_DISABLED);
    lv_obj_set_style_radius(date_title_input,UI_CONTROL_RADIUS,0);lv_obj_set_style_border_width(date_title_input,1,0);
    ui_border_color(date_title_input,lv_color_hex(TRACK),0);ui_border_color(date_title_input,lv_color_hex(BLUE),LV_STATE_FOCUSED);
    lv_obj_set_style_pad_hor(date_title_input,20,0);lv_obj_set_style_pad_ver(date_title_input,12,0);
    lv_obj_add_event_cb(date_title_input,date_keyboard_show,LV_EVENT_CLICKED,NULL);
    lv_obj_add_event_cb(date_title_input,date_title_insert,LV_EVENT_INSERT,NULL);
    lv_obj_add_event_cb(date_title_input,date_title_changed,LV_EVENT_VALUE_CHANGED,NULL);
    date_editor_voice_button=button(date_form,"",696,70,72,date_editor_voice_action,0);lv_obj_set_height(date_editor_voice_button,72);
    lv_obj_set_style_pad_all(date_editor_voice_button,0,0);lv_obj_set_style_radius(date_editor_voice_button,LV_RADIUS_CIRCLE,0);
    voice_button_glyphs(date_editor_voice_button,&date_editor_mic_glyph,&date_editor_stop_glyph);
    date_target_fields=shape(date_form,32,196,736,144,SURFACE,0);muted(date_target_fields,"目标日期",0,0,736);
    lv_obj_t *date_select=button(date_target_fields,"",0,46,640,date_picker_show,0);lv_obj_set_height(date_select,76);
    lv_obj_set_style_border_width(date_select,1,0);ui_border_color(date_select,lv_color_hex(TRACK),0);
    date_selected_label=label(date_select,"",20,16,600,false);ui_text_font(date_selected_label,&tab5_font_30,0);
    lv_obj_t *date_open=button(date_target_fields,"",664,48,72,date_picker_show,0);lv_obj_set_height(date_open,72);
    lv_obj_set_style_pad_all(date_open,0,0);lv_obj_set_style_radius(date_open,LV_RADIUS_CIRCLE,0);
    lv_obj_t *icon=shape(date_open,21,23,30,28,0x354247,4);lv_obj_set_style_bg_opa(icon,LV_OPA_TRANSP,0);
    lv_obj_set_style_border_width(icon,2,0);ui_border_color(icon,lv_color_hex(INK),0);
    shape(date_open,27,19,2,9,INK,1);shape(date_open,43,19,2,9,INK,1);shape(date_open,22,31,28,2,INK,0);
    for(int row=0;row<2;row++)for(int col=0;col<3;col++)shape(date_open,27+col*7,37+row*6,3,3,INK,0);
    date_feedback=muted(date_editor,"",240,536,800);lv_obj_set_height(date_feedback,68);lv_label_set_long_mode(date_feedback,LV_LABEL_LONG_WRAP);tint(date_feedback,AMBER);
    date_footer=shape(date_editor,240,620,800,80,UI_BACKGROUND,0);
    date_editor_save_button=button(date_footer,"保存",576,0,224,date_editor_save,0);lv_obj_set_height(date_editor_save_button,56);lv_obj_center(lv_obj_get_child(date_editor_save_button,0));
    ui_bg_color(date_editor_save_button,lv_color_hex(BLUE),0);tint(lv_obj_get_child(date_editor_save_button,0),0x173642);
    date_editor_voice_cancel=button(date_footer,LV_SYMBOL_TRASH,0,0,64,date_editor_voice_action,2);ui_text_font(lv_obj_get_child(date_editor_voice_cancel,0),&lv_font_montserrat_24,0);visible(date_editor_voice_cancel,false);
    date_editor_voice_switch=button(date_footer,"改用 TAB5",80,0,260,date_editor_voice_action,3);visible(date_editor_voice_switch,false);
    date_editor_voice_meter=lv_bar_create(date_footer);lv_obj_set_pos(date_editor_voice_meter,0,68);lv_obj_set_size(date_editor_voice_meter,536,4);
    ui_bg_color(date_editor_voice_meter,lv_color_hex(TRACK),LV_PART_MAIN);ui_bg_color(date_editor_voice_meter,lv_color_hex(GREEN),LV_PART_INDICATOR);visible(date_editor_voice_meter,false);
    date_keyboard=lv_keyboard_create(date_editor);lv_obj_set_align(date_keyboard,LV_ALIGN_TOP_LEFT);lv_obj_set_pos(date_keyboard,30,444);lv_obj_set_size(date_keyboard,1220,266);
    ui_bg_color(date_keyboard,lv_color_hex(UI_BACKGROUND),0);ui_bg_color(date_keyboard,lv_color_hex(SURFACE),LV_PART_ITEMS);ui_text_color(date_keyboard,lv_color_hex(INK),LV_PART_ITEMS);
    lv_obj_add_event_cb(date_keyboard,date_keyboard_hide,LV_EVENT_READY,NULL);lv_obj_add_event_cb(date_keyboard,date_keyboard_hide,LV_EVENT_CANCEL,NULL);
    date_ime=tab5_ime_create(date_editor,date_keyboard,date_title_input,&tab5_font_24);lv_obj_set_pos(date_ime,30,388);
    date_picker_create();visible(date_keyboard,false);visible(date_editor,false);
}

#include "ui_date_picker.h"
#include "ui_date_voice.h"
