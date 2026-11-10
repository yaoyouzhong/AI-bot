#include "countdown.h"
static countdown_timer clock_countdown={.seconds=300,.remaining_ms=300000};
static bool timer_notice_pending;
static lv_obj_t *timer_layer,*timer_value,*timer_hint,*timer_action_button,*timer_fields[3],*timer_presets[3];
static lv_obj_t *timer_header,*timer_setup,*timer_controls,*timer_reset_button;
static int64_t timer_controls_until;
LV_FONT_DECLARE(tab5_timer_digits_192);
static bool timer_open(void){return timer_layer&&!lv_obj_has_flag(timer_layer,LV_OBJ_FLAG_HIDDEN);}
static void timer_controls_refresh(void) {
    bool editing=clock_countdown.phase==COUNTDOWN_READY;
    bool controls=clock_countdown.phase!=COUNTDOWN_RUNNING||timer_controls_until>esp_timer_get_time()/1000;
    visible(timer_header,controls);visible(timer_controls,controls);
    visible(timer_setup,editing);visible(timer_value,!editing);
    visible(timer_hint,clock_countdown.phase==COUNTDOWN_PAUSED||clock_countdown.phase==COUNTDOWN_DONE);
    visible(timer_reset_button,!editing);
    lv_obj_set_x(timer_action_button,editing?432:596);
}
static void timer_render(void) {
    if(!timer_layer)return;
    unsigned seconds=(unsigned)((clock_countdown.remaining_ms+999)/1000);
    ui_text_fmt(timer_value,"%02u:%02u:%02u",seconds/3600,seconds/60%60,seconds%60);
    const char *states[]={"","","已暂停","时间到"};
    ui_text(timer_hint,states[clock_countdown.phase]);
    tint(timer_value,clock_countdown.phase==COUNTDOWN_DONE?GREEN:INK);
    ui_text(lv_obj_get_child(timer_action_button,0),clock_countdown.phase==COUNTDOWN_RUNNING?"暂停":clock_countdown.phase==COUNTDOWN_PAUSED?"继续":clock_countdown.phase==COUNTDOWN_DONE?"再来一次":"开始");
    bool editing=clock_countdown.phase==COUNTDOWN_READY;
    const unsigned presets[]={300,900,1500};
    for(int i=0;i<3;i++) {
        if(editing){lv_obj_remove_state(timer_fields[i],LV_STATE_DISABLED);lv_obj_remove_state(timer_presets[i],LV_STATE_DISABLED);}
        else {lv_obj_add_state(timer_fields[i],LV_STATE_DISABLED);lv_obj_add_state(timer_presets[i],LV_STATE_DISABLED);}
        bool selected=editing&&clock_countdown.seconds==presets[i];
        ui_bg_color(timer_presets[i],lv_color_hex(selected?0x405963:0x354247),0);
        lv_obj_set_style_border_width(timer_presets[i],selected?1:0,0);
        ui_border_color(timer_presets[i],lv_color_hex(BLUE),0);
    }
    if(clock_countdown.seconds)lv_obj_remove_state(timer_action_button,LV_STATE_DISABLED);else lv_obj_add_state(timer_action_button,LV_STATE_DISABLED);
    timer_controls_refresh();
}
static void timer_tick(int64_t now) {
    int64_t previous=(clock_countdown.remaining_ms+999)/1000;
    bool completed=countdown_tick(&clock_countdown,now);
    if(completed)timer_notice_pending=true;
    if(timer_notice_pending&&!ota_update_busy()) {
        timer_notice_pending=false;voice_remote_notify_timer();saver_leave();lv_display_trigger_activity(NULL);
        clock_picker_close(NULL);
        visible(timer_layer,true);lv_obj_move_foreground(timer_layer);
        timer_render();
    }
    if(completed||(timer_open()&&previous!=(clock_countdown.remaining_ms+999)/1000))timer_render();
    if(timer_open()&&clock_countdown.phase==COUNTDOWN_RUNNING&&timer_controls_until&&now>=timer_controls_until) {
        timer_controls_until=0;timer_controls_refresh();
    }
}
static void timer_show(lv_event_t *e){(void)e;timer_controls_until=0;timer_render();visible(timer_layer,true);lv_obj_move_foreground(timer_layer);}
static void timer_close(lv_event_t *e){(void)e;visible(timer_layer,false);lv_display_trigger_activity(NULL);last_change=esp_timer_get_time()/1000;}
static void timer_back(lv_event_t *e) {
    timer_close(e);
    // Share the clock's return destination; completion overlays on other
    // pages simply uncover the page the user was viewing.
    if(current_page==7)workbench_return(e);
}
static void clock_segment_clicked(lv_event_t *e) {
    if((intptr_t)lv_event_get_user_data(e))timer_show(e);
    else {timer_close(e);page(7);}
}
static lv_obj_t *clock_segments_create(lv_obj_t *parent,int selected) {
    lv_obj_t *group=shape(parent,96,12,284,56,SURFACE,16);
    const char *labels[]={"时钟","倒计时"};
    for(int i=0;i<2;i++) {
        lv_obj_t *b=button(group,labels[i],4+i*140,4,136,clock_segment_clicked,i);
        lv_obj_set_style_radius(b,12,0);
        ui_bg_color(b,lv_color_hex(i==selected?BLUE:SURFACE),0);
        ui_text_color(b,lv_color_hex(i==selected?0x173642:MUTED),0);
        if(i==selected)lv_obj_add_state(b,LV_STATE_CHECKED);
    }
    return group;
}
static void timer_canvas_touch(lv_event_t *e) {
    (void)e;if(clock_countdown.phase!=COUNTDOWN_RUNNING)return;
    timer_controls_until=lv_obj_has_flag(timer_controls,LV_OBJ_FLAG_HIDDEN)?esp_timer_get_time()/1000+5000:0;
    timer_controls_refresh();
}
static void timer_action(lv_event_t *e) {
    (void)e;int64_t now=esp_timer_get_time()/1000;
    bool was_running=clock_countdown.phase==COUNTDOWN_RUNNING;
    timer_tick(now);
    if(was_running&&clock_countdown.phase==COUNTDOWN_DONE)return;
    if(clock_countdown.phase==COUNTDOWN_RUNNING)countdown_pause(&clock_countdown,now);else countdown_start(&clock_countdown,now);
    timer_controls_until=0;
    timer_render();
}
static void timer_reset_clicked(lv_event_t *e){(void)e;timer_notice_pending=false;countdown_reset(&clock_countdown,clock_countdown.seconds);timer_render();}
static void timer_duration_changed(lv_event_t *e) {
    (void)e;if(clock_countdown.phase!=COUNTDOWN_READY)return;
    countdown_reset(&clock_countdown,lv_dropdown_get_selected(timer_fields[0])*3600+lv_dropdown_get_selected(timer_fields[1])*60+lv_dropdown_get_selected(timer_fields[2]));timer_render();
}
static void timer_preset(lv_event_t *e) {
    if(clock_countdown.phase!=COUNTDOWN_READY)return;
    lv_dropdown_set_selected(timer_fields[0],0);lv_dropdown_set_selected(timer_fields[1],(unsigned)(intptr_t)lv_event_get_user_data(e));lv_dropdown_set_selected(timer_fields[2],0);timer_duration_changed(NULL);
}
static UI_INIT_SMALL void clock_countdown_create(lv_obj_t *screen) {
    timer_layer=shape(screen,0,0,1280,720,UI_BACKGROUND,0);lv_obj_add_flag(timer_layer,LV_OBJ_FLAG_CLICKABLE);
    lv_obj_add_event_cb(timer_layer,timer_canvas_touch,LV_EVENT_CLICKED,NULL);
    timer_value=label(timer_layer,"00:05:00",64,0,1152,true);ui_text_font(timer_value,&tab5_timer_digits_192,0);
    lv_obj_set_style_text_align(timer_value,LV_TEXT_ALIGN_CENTER,0);lv_obj_align(timer_value,LV_ALIGN_CENTER,0,0);
    timer_hint=muted(timer_layer,"",64,476,1152);lv_obj_set_style_text_align(timer_hint,LV_TEXT_ALIGN_CENTER,0);
    timer_header=shape(timer_layer,0,0,1280,116,UI_BACKGROUND,0);
    clock_segments_create(timer_header,1);
    workbench_back_button(timer_header,timer_back);
    timer_setup=shape(timer_layer,192,142,896,370,UI_BACKGROUND,0);
    shape(timer_setup,0,0,896,274,SURFACE,UI_CARD_RADIUS);
    muted(timer_setup,"设置时长",32,26,832);
    const char *units[]={"时","分","秒"};
    for(int i=0;i<3;i++) {
        char options[200];int used=0,count=i==0?24:60;
        for(int n=0;n<count;n++)used+=snprintf(options+used,sizeof(options)-used,"%s%02d",n?"\n":"",n);
        timer_fields[i]=dropdown(timer_setup,options,32+i*288,78,256,i==1?5:0,timer_duration_changed,0);
        lv_obj_set_height(timer_fields[i],116);lv_obj_set_style_pad_top(timer_fields[i],14,0);
        lv_obj_set_style_pad_left(timer_fields[i],24,0);ui_text_font(timer_fields[i],&tab5_digits_72,0);
        ui_text_font(lv_dropdown_get_list(timer_fields[i]),&tab5_font_30,0);lv_obj_set_height(lv_dropdown_get_list(timer_fields[i]),244);
        lv_obj_t *unit=muted(timer_setup,units[i],32+i*288,212,256);lv_obj_set_style_text_align(unit,LV_TEXT_ALIGN_CENTER,0);
    }
    int minutes[]={5,15,25};for(int i=0;i<3;i++){char s[32];snprintf(s,sizeof(s),"%d 分钟",minutes[i]);timer_presets[i]=button(timer_setup,s,32+i*288,302,256,timer_preset,minutes[i]);}
    timer_controls=shape(timer_layer,64,568,1152,122,UI_BACKGROUND,0);
    timer_reset_button=button(timer_controls,"重置",292,0,240,timer_reset_clicked,0);
    timer_action_button=button(timer_controls,"开始",432,0,288,timer_action,0);
    lv_obj_set_height(timer_action_button,64);lv_obj_center(lv_obj_get_child(timer_action_button,0));
    lv_obj_set_height(timer_reset_button,64);lv_obj_center(lv_obj_get_child(timer_reset_button,0));
    ui_bg_color(timer_action_button,lv_color_hex(BLUE),0);ui_text_color(timer_action_button,lv_color_hex(0x173642),0);
    visible(timer_layer,false);timer_render();
}
