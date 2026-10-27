// Uses the workbench's existing palette, typography and detail-page navigation.
#if defined(__GNUC__) && !defined(__clang__)
#pragma GCC push_options
#pragma GCC optimize ("Os")
#endif
static lv_obj_t *agenda_rows[AGENDA_LIMIT],*agenda_row_titles[AGENDA_LIMIT],*agenda_row_times[AGENDA_LIMIT];
static lv_obj_t *agenda_row_dates[AGENDA_LIMIT],*agenda_markers[AGENDA_LIMIT],*agenda_filters[2];
static lv_obj_t *agenda_list,*agenda_empty,*agenda_title,*agenda_when,*agenda_repeat,*agenda_status,*agenda_snooze,*agenda_popup_snooze;
static lv_obj_t *agenda_popup,*agenda_popup_title,*agenda_popup_status,*agenda_popup_time,*agenda_popup_dismiss;
static lv_obj_t *agenda_new,*agenda_edit_button,*agenda_delete_button,*agenda_detail_surface,*agenda_filter_track;
static lv_obj_t *agenda_delete_layer,*agenda_delete_title,*agenda_delete_status,*agenda_delete_yes,*agenda_delete_no;
static char agenda_delete_id[33],agenda_delete_edit[33];
static bool agenda_pending_delete;
static uint32_t agenda_pending_request;
static bool agenda_pending_alarm;
static agenda_state agenda_ui;
static char agenda_selected[33],agenda_alarm_id[33],agenda_alarm_edit[33];
static int64_t agenda_alarm_occurrence,agenda_ui_next;
static bool agenda_today=true;
static bool agenda_pending_editor;
static void agenda_editor_show(lv_event_t *event);
static void agenda_editor_create(lv_obj_t *screen);
static void agenda_editor_result(void);
static bool agenda_editor_open(void);
static bool agenda_delete_open(void){return agenda_delete_layer&&!lv_obj_has_flag(agenda_delete_layer,LV_OBJ_FLAG_HIDDEN);}
static void agenda_editor_voice_tick(void);
static void agenda_popup_busy(bool busy) {
    lv_obj_t *buttons[]={agenda_popup_snooze,agenda_popup_dismiss};
    for(unsigned n=0;n<2;n++)if(buttons[n]) {
        if(busy)lv_obj_add_state(buttons[n],LV_STATE_DISABLED);else lv_obj_remove_state(buttons[n],LV_STATE_DISABLED);
    }
}
static void agenda_operation_result(void) {
    if(!agenda_pending_request||agenda_ui.completed_request!=agenda_pending_request)return;
    if(agenda_pending_alarm){if(agenda_ui.last_success)visible(agenda_popup,false);else {ui_text(agenda_popup_status,agenda_ui.message);tint(agenda_popup_status,RED);}}
    if(agenda_pending_editor){agenda_editor_result();agenda_pending_editor=false;}
    if(agenda_pending_delete){
        if(agenda_ui.last_success)visible(agenda_delete_layer,false);else {ui_text(agenda_delete_status,agenda_ui.message);tint(agenda_delete_status,RED);}
        lv_obj_remove_state(agenda_delete_yes,LV_STATE_DISABLED);lv_obj_remove_state(agenda_delete_no,LV_STATE_DISABLED);agenda_pending_delete=false;
    }
    agenda_pending_request=0;agenda_pending_alarm=false;agenda_popup_busy(false);
}
static int64_t agenda_civil_now(void) {
    struct tm t;if(!gallery_clock_local(esp_timer_get_time()/1000,&t))return 0;
    return (int64_t)(countdown_civil_day(t.tm_year+1900,t.tm_mon+1,t.tm_mday)-countdown_civil_day(1970,1,1))*86400+t.tm_hour*3600+t.tm_min*60+t.tm_sec;
}
static const agenda_item *agenda_selected_item(const char *id) {
    for(unsigned i=0;i<agenda_ui.document.count;i++)if(!strcmp(agenda_ui.document.items[i].id,id))return &agenda_ui.document.items[i];
    return NULL;
}
static void agenda_render(void) {
    if(!agenda_list)return;
    agenda_get(&agenda_ui);agenda_operation_result();int64_t now=agenda_civil_now();int row=0;unsigned visible_indices[AGENDA_LIMIT];bool selection_visible=false;
    // Sorting a bounded index array keeps row callbacks stable and allocations small.
    unsigned order[AGENDA_LIMIT];for(unsigned i=0;i<agenda_ui.document.count;i++)order[i]=i;
    for(unsigned i=0;i<agenda_ui.document.count;i++)for(unsigned j=i+1;j<agenda_ui.document.count;j++) {
        const agenda_item *a=&agenda_ui.document.items[order[i]],*b=&agenda_ui.document.items[order[j]];
        agenda_view av=agenda_view_at(a,now),bv=agenda_view_at(b,now);
        int64_t at=a->due?av.occurrence:INT64_MAX,bt=b->due?bv.occurrence:INT64_MAX;
        if((av.complete&&!bv.complete)||(av.complete==bv.complete&&(av.complete?bt>at:bt<at))){unsigned swap=order[i];order[i]=order[j];order[j]=swap;}
    }
    for(unsigned n=0;n<agenda_ui.document.count;n++) {
        const agenda_item *i=&agenda_ui.document.items[order[n]];agenda_view v=agenda_view_at(i,now);
        if(agenda_today&&(v.complete||(i->due&&(!now||v.occurrence/86400>now/86400))))continue;
        visible_indices[row]=order[n];if(!strcmp(agenda_selected,i->id))selection_visible=true;row++;
    }
    if(!selection_visible){agenda_selected[0]=0;if(row)strcpy(agenda_selected,agenda_ui.document.items[visible_indices[0]].id);}
    for(int n=0;n<row;n++) {
        const agenda_item *i=&agenda_ui.document.items[visible_indices[n]];agenda_view v=agenda_view_at(i,now);bool selected=!strcmp(agenda_selected,i->id);
        lv_obj_t *button=agenda_rows[n];lv_obj_set_user_data(button,(void*)(uintptr_t)(visible_indices[n]+1));
        ui_text(agenda_row_titles[n],i->title);char when[80]="待办",day[40]="";
        if(v.calendar_pending){strcpy(when,"--:--");strcpy(day,"待更新");}
        else if(i->due){time_t t=(time_t)v.occurrence;struct tm local;gmtime_r(&t,&local);strftime(when,sizeof(when),"%H:%M",&local);if(now&&v.occurrence/86400==now/86400)day[0]=0;else strftime(day,sizeof(day),"%m-%d",&local);}
        if(v.complete)snprintf(when,sizeof(when),"已结束");
        ui_text(agenda_row_times[n],when);ui_text(agenda_row_dates[n],day);
        ui_bg_color(button,lv_color_hex(selected?0x30464E:UI_BACKGROUND),0);ui_bg_color(agenda_markers[n],lv_color_hex(v.complete?MUTED:now&&i->due&&v.alert_at<=now?AMBER:selected?BLUE:TRACK),0);
        lv_obj_set_y(button,n*74);visible(button,true);
    }
    for(int i=row;i<AGENDA_LIMIT;i++)visible(agenda_rows[i],false);
    visible(agenda_empty,!row);visible(agenda_list,row!=0);
    ui_text(agenda_empty,agenda_ui.ready?(agenda_today&&agenda_ui.document.count?"今天没有事项":"暂无事项"):agenda_ui.message);
    // Empty views group their available actions around the message, not a vacant toolbar.
    bool other_items=!row&&agenda_ui.document.count>0;
    visible(agenda_filter_track,row!=0);
    visible(agenda_filters[0],row!=0);visible(agenda_filters[1],row!=0||other_items);
    lv_obj_set_pos(agenda_filters[1],row?160:412,row?32:246);lv_obj_set_size(agenda_filters[1],row?124:176,row?48:56);
    lv_obj_set_pos(agenda_new,row?568:other_items?632:522,row?32:246);lv_obj_set_height(agenda_new,row?48:56);
    const agenda_item *selected=agenda_selected_item(agenda_selected);agenda_view v={0};if(selected)v=agenda_view_at(selected,now);
    if(agenda_ui.ready&&!agenda_pending_request)lv_obj_remove_state(agenda_new,LV_STATE_DISABLED);else lv_obj_add_state(agenda_new,LV_STATE_DISABLED);
    if(selected&&agenda_ui.ready&&!agenda_pending_request)lv_obj_remove_state(agenda_edit_button,LV_STATE_DISABLED);else lv_obj_add_state(agenda_edit_button,LV_STATE_DISABLED);
    if(selected&&agenda_ui.ready&&!agenda_pending_request)lv_obj_remove_state(agenda_delete_button,LV_STATE_DISABLED);else lv_obj_add_state(agenda_delete_button,LV_STATE_DISABLED);
    visible(agenda_when,selected!=NULL);visible(agenda_title,selected!=NULL);visible(agenda_repeat,selected!=NULL);
    visible(agenda_detail_surface,selected!=NULL);visible(agenda_edit_button,selected!=NULL);visible(agenda_delete_button,selected!=NULL);
    ui_text(agenda_title,selected?selected->title:"暂无待处理事项");char when[64]="--:--";
    if(selected&&selected->due&&!v.calendar_pending){time_t t=(time_t)v.occurrence;struct tm local;gmtime_r(&t,&local);strftime(when,sizeof(when),"%H:%M",&local);}
    ui_text(agenda_when,when);char detail[160]="";
    if(selected){
        if(!selected->due)detail[0]=0;
        else {
            int64_t minutes=now?(v.occurrence-now+59)/60:0;char remaining[64]="等待校时";
            if(now)snprintf(remaining,sizeof(remaining),"%s",v.occurrence<=now?"进行中":"");
            if(now&&minutes>0&&minutes<60)snprintf(remaining,sizeof(remaining),"还有 %lld 分钟",(long long)minutes);
            else if(now&&minutes>=60&&minutes<1440)snprintf(remaining,sizeof(remaining),"还有 %lld 小时 %lld 分钟",(long long)(minutes/60),(long long)(minutes%60));
            else if(now&&minutes>=1440){time_t t=(time_t)v.occurrence;struct tm local;gmtime_r(&t,&local);strftime(remaining,sizeof(remaining),"%m-%d",&local);}
            if(selected->snooze_occurrence==v.occurrence&&now&&v.alert_at>now)snprintf(remaining,sizeof(remaining),"%lld 分钟后提醒",(long long)((v.alert_at-now+59)/60));
            snprintf(detail,sizeof(detail),"%s",remaining);
        }}
    if(v.calendar_pending)snprintf(detail,sizeof(detail),"工作日安排待更新，提醒暂停");
    else if(selected&&v.complete)snprintf(detail,sizeof(detail),"已结束");
    ui_text(agenda_repeat,detail);visible(agenda_snooze,selected&&selected->due&&!v.complete&&!v.calendar_pending&&now&&v.alert_at<=now&&now+600<agenda_ends_at(selected,v.occurrence));
    const char *message=agenda_ui.message;
    bool exceptional=strstr(message,"失败")||strstr(message,"无法")||strstr(message,"未应用")||strstr(message,"未保存")||strstr(message,"未启动")||strstr(message,"冲突")||strstr(message,"副本")||strstr(message,"保留两份")||strstr(message,"更新电脑")||strstr(message,"需更新");
    ui_text(agenda_status,agenda_pending_request?"正在保存…":!now?"请先校准设备时间":exceptional?message:agenda_ui.pending?"已保存，待同步电脑":"");
    for(int i=0;i<2;i++){bool active=agenda_today==(i==0);ui_bg_color(agenda_filters[i],lv_color_hex(active?0x40575F:SURFACE),0);ui_text_color(agenda_filters[i],lv_color_hex(active?INK:MUTED),0);}
}
static void agenda_row_click(lv_event_t *event) {
    unsigned index=(unsigned)(uintptr_t)lv_obj_get_user_data(lv_event_get_target(event));if(index==0||index>agenda_ui.document.count)return;
    strcpy(agenda_selected,agenda_ui.document.items[index-1].id);agenda_render();
}
static void agenda_delete_show(lv_event_t *event) {
    (void)event;if(agenda_pending_request)return;const agenda_item *item=agenda_selected_item(agenda_selected);if(!item)return;
    strcpy(agenda_delete_id,item->id);strcpy(agenda_delete_edit,item->edit);ui_text(agenda_delete_title,item->title);ui_text(agenda_delete_status,"删除后会同步到电脑，离线时将在重连后同步");tint(agenda_delete_status,MUTED);
    visible(agenda_delete_layer,true);lv_obj_move_foreground(agenda_delete_layer);
}
static void agenda_delete_cancel(lv_event_t *event){(void)event;if(!agenda_pending_delete)visible(agenda_delete_layer,false);}
static void agenda_delete_confirm(lv_event_t *event) {
    (void)event;if(agenda_pending_request)return;agenda_pending_request=agenda_remove(agenda_delete_id,agenda_delete_edit);agenda_pending_delete=agenda_pending_request!=0;
    ui_text(agenda_delete_status,agenda_pending_delete?"正在保存删除…":"删除未发起，原事项仍保留");tint(agenda_delete_status,agenda_pending_delete?MUTED:RED);
    if(agenda_pending_delete){lv_obj_add_state(agenda_delete_yes,LV_STATE_DISABLED);lv_obj_add_state(agenda_delete_no,LV_STATE_DISABLED);}
}
static void agenda_filter(lv_event_t *event){intptr_t index=(intptr_t)lv_event_get_user_data(event);agenda_today=index==0;lv_obj_scroll_to_y(agenda_list,0,LV_ANIM_OFF);agenda_render();}
static void agenda_perform(lv_event_t *event) {
    if(agenda_pending_request)return;
    (void)event;const agenda_item *i=agenda_selected_item(agenda_selected);int64_t now=agenda_civil_now();
    if(i&&now&&now+600<agenda_ends_at(i,agenda_view_at(i,now).occurrence))agenda_pending_request=agenda_action(i->id,i->edit,agenda_view_at(i,now).occurrence,false,now+600);
    if(agenda_pending_request)ui_text(agenda_status,"正在保存事项…");else ui_text(agenda_status,"操作未保存，请稍后重试");
}
static void agenda_alarm_action(lv_event_t *event) {
    if(agenda_pending_request)return;
    int op=(int)(intptr_t)lv_event_get_user_data(event);if(op==2){visible(agenda_popup,false);return;}
    int64_t now=agenda_civil_now();if(!now&&op==1)return;
    const agenda_item *item=agenda_selected_item(agenda_alarm_id);if(!item||now+600>=agenda_ends_at(item,agenda_alarm_occurrence))return;
    agenda_pending_request=agenda_action(agenda_alarm_id,agenda_alarm_edit,agenda_alarm_occurrence,false,now+600);
    agenda_pending_alarm=agenda_pending_request!=0;ui_text(agenda_popup_status,agenda_pending_request?"正在保存…":"操作未保存，请稍后重试");
    tint(agenda_popup_status,agenda_pending_request?MUTED:RED);agenda_popup_busy(agenda_pending_alarm);
}
static void agenda_create(lv_obj_t *parent,lv_obj_t *screen) {
    (void)screen;
    // Keep the controls with their list; the selected appointment has its own quiet surface.
    agenda_filter_track=shape(parent,24,28,264,56,SURFACE,16);
    agenda_filters[0]=button(parent,"今日",28,32,124,agenda_filter,0);agenda_filters[1]=button(parent,"全部",160,32,124,agenda_filter,1);
    agenda_new=button(parent,"新建",568,32,176,agenda_editor_show,0);ui_bg_color(agenda_new,lv_color_hex(BLUE),0);tint(lv_obj_get_child(agenda_new,0),0x173642);
    agenda_detail_surface=shape(parent,788,16,432,466,SURFACE,24);
    agenda_when=label(parent,"--:--",816,64,376,false);ui_text_font(agenda_when,&tab5_digits_72,0);tint(agenda_when,BLUE);
    agenda_title=label(parent,"",816,160,376,false);ui_text_font(agenda_title,&tab5_font_30,0);lv_obj_set_height(agenda_title,117);lv_label_set_long_mode(agenda_title,LV_LABEL_LONG_DOT);
    agenda_repeat=muted(parent,"",816,286,376);lv_obj_set_height(agenda_repeat,62);lv_label_set_long_mode(agenda_repeat,LV_LABEL_LONG_WRAP);
    agenda_edit_button=button(parent,"编辑",816,418,180,agenda_editor_show,1);agenda_delete_button=button(parent,"删除",1012,418,180,agenda_delete_show,0);
    agenda_list=lv_obj_create(parent);lv_obj_remove_style_all(agenda_list);lv_obj_set_pos(agenda_list,16,108);lv_obj_set_size(agenda_list,748,370);lv_obj_set_scroll_dir(agenda_list,LV_DIR_VER);lv_obj_set_scrollbar_mode(agenda_list,LV_SCROLLBAR_MODE_AUTO);
    for(int i=0;i<AGENDA_LIMIT;i++) {
        agenda_rows[i]=button(agenda_list,"",0,i*74,728,agenda_row_click,0);lv_obj_set_height(agenda_rows[i],74);lv_obj_set_style_radius(agenda_rows[i],14,0);visible(lv_obj_get_child(agenda_rows[i],0),false);
        shape(agenda_rows[i],140,0,2,74,TRACK,0);agenda_markers[i]=shape(agenda_rows[i],135,31,12,12,TRACK,LV_RADIUS_CIRCLE);
        agenda_row_titles[i]=label(agenda_rows[i],"",164,6,544,false);lv_obj_set_height(agenda_row_titles[i],62);lv_label_set_long_mode(agenda_row_titles[i],LV_LABEL_LONG_DOT);
        agenda_row_times[i]=label(agenda_rows[i],"",16,4,122,false);ui_text_font(agenda_row_times[i],&tab5_font_30,0);
        agenda_row_dates[i]=muted(agenda_rows[i],"",16,39,122);visible(agenda_rows[i],false);
    }
    agenda_empty=muted(parent,"",0,180,1220);ui_text_font(agenda_empty,&tab5_font_30,0);lv_obj_set_style_text_align(agenda_empty,LV_TEXT_ALIGN_CENTER,0);
    agenda_status=muted(parent,"",0,484,1220);lv_label_set_long_mode(agenda_status,LV_LABEL_LONG_DOT);
    agenda_snooze=button(parent,"10 分钟后再提醒",816,358,376,agenda_perform,1);
    agenda_popup=shape(screen,0,0,1280,720,0x000000,0);ui_bg_opa(agenda_popup,LV_OPA_60,0);lv_obj_add_flag(agenda_popup,LV_OBJ_FLAG_CLICKABLE);
    lv_obj_t *reminder=shape(agenda_popup,240,180,800,360,SURFACE,UI_CARD_RADIUS);
    lv_obj_set_style_border_width(reminder,1,0);ui_border_color(reminder,lv_color_hex(TRACK),0);
    shape(reminder,40,30,4,24,BLUE,2);lv_obj_t *heading=label(reminder,"日程提醒",58,26,400,false);tint(heading,BLUE);
    agenda_popup_time=muted(reminder,"",580,26,180);lv_obj_set_style_text_align(agenda_popup_time,LV_TEXT_ALIGN_RIGHT,0);
    agenda_popup_title=label(reminder,"",40,80,720,false);ui_text_font(agenda_popup_title,&tab5_font_30,0);
    lv_obj_set_height(agenda_popup_title,84);lv_obj_set_style_text_line_space(agenda_popup_title,4,0);lv_label_set_long_mode(agenda_popup_title,LV_LABEL_LONG_WRAP);
    agenda_popup_status=muted(reminder,"",40,180,720);lv_obj_set_height(agenda_popup_status,60);lv_label_set_long_mode(agenda_popup_status,LV_LABEL_LONG_WRAP);
    shape(reminder,40,248,720,1,TRACK,0);
    agenda_popup_snooze=button(reminder,"10 分钟后再提醒",296,264,280,agenda_alarm_action,1);lv_obj_set_height(agenda_popup_snooze,64);
    agenda_popup_dismiss=button(reminder,"知道了",592,264,168,agenda_alarm_action,2);lv_obj_set_height(agenda_popup_dismiss,64);
    ui_bg_color(agenda_popup_dismiss,lv_color_hex(BLUE),0);tint(lv_obj_get_child(agenda_popup_dismiss,0),0x173642);
    visible(agenda_popup,false);
    agenda_delete_layer=shape(screen,0,0,1280,720,0x000000,0);ui_bg_opa(agenda_delete_layer,LV_OPA_60,0);lv_obj_add_flag(agenda_delete_layer,LV_OBJ_FLAG_CLICKABLE);
    lv_obj_t *confirmation=shape(agenda_delete_layer,240,180,800,360,SURFACE,UI_CARD_RADIUS);
    lv_obj_set_style_border_width(confirmation,1,0);ui_border_color(confirmation,lv_color_hex(TRACK),0);
    shape(confirmation,40,30,4,24,RED,2);lv_obj_t *delete_heading=label(confirmation,"删除事项",58,26,660,false);tint(delete_heading,RED);
    agenda_delete_title=label(confirmation,"",40,80,720,false);ui_text_font(agenda_delete_title,&tab5_font_30,0);
    lv_obj_set_height(agenda_delete_title,84);lv_obj_set_style_text_line_space(agenda_delete_title,4,0);lv_label_set_long_mode(agenda_delete_title,LV_LABEL_LONG_WRAP);
    agenda_delete_status=muted(confirmation,"",40,180,720);lv_obj_set_height(agenda_delete_status,60);lv_label_set_long_mode(agenda_delete_status,LV_LABEL_LONG_WRAP);
    shape(confirmation,40,248,720,1,TRACK,0);
    agenda_delete_no=button(confirmation,"取消",408,264,168,agenda_delete_cancel,0);lv_obj_set_height(agenda_delete_no,64);
    agenda_delete_yes=button(confirmation,"删除",592,264,168,agenda_delete_confirm,0);lv_obj_set_height(agenda_delete_yes,64);ui_bg_color(agenda_delete_yes,lv_color_hex(RED),0);tint(lv_obj_get_child(agenda_delete_yes,0),0x34242A);
    visible(agenda_delete_layer,false);agenda_render();
}
static void agenda_tick(int64_t now) {
    agenda_offer(text(item(shown,"agenda"),"revision"));if(now<agenda_ui_next)return;agenda_ui_next=now+500;
    agenda_editor_voice_tick();
    agenda_get(&agenda_ui);agenda_operation_result();if(current_page==13)agenda_render();
    int64_t civil=agenda_civil_now();const agenda_item *alarm=agenda_selected_item(agenda_alarm_id);
    if(!lv_obj_has_flag(agenda_popup,LV_OBJ_FLAG_HIDDEN)) {
        if(!alarm||strcmp(alarm->edit,agenda_alarm_edit)||(civil&&civil>=agenda_ends_at(alarm,agenda_alarm_occurrence)))visible(agenda_popup,false);
        else {visible(agenda_popup_snooze,civil&&civil+600<agenda_ends_at(alarm,agenda_alarm_occurrence));if(!agenda_pending_request&&civil&&civil+600>=agenda_ends_at(alarm,agenda_alarm_occurrence))ui_text(agenda_popup_status,"即将结束，不再延后提醒");}
    }
    // Do not replace an outstanding reminder or interrupt voice entry.
    voice_remote_state voice;voice_remote_get(&voice);if(voice.busy||agenda_delete_open()||!lv_obj_has_flag(agenda_popup,LV_OBJ_FLAG_HIDDEN))return;
    char title[97],id[33];int64_t occurrence;
    if(!agenda_alert(now,title,id,&occurrence))return;
    agenda_get(&agenda_ui);const agenda_item *i=agenda_selected_item(id);if(!i||!civil||civil>=agenda_ends_at(i,occurrence))return;
    strcpy(agenda_alarm_id,id);strcpy(agenda_alarm_edit,i->edit);agenda_alarm_occurrence=occurrence;
    ui_text(agenda_popup_title,title);int seconds=(int)(occurrence%86400);
    ui_text_fmt(agenda_popup_time,"%02d:%02d 开始",seconds/3600,seconds/60%60);
    bool can_snooze=civil+600<agenda_ends_at(i,occurrence);visible(agenda_popup_snooze,can_snooze);agenda_popup_busy(false);
    ui_text(agenda_popup_status,can_snooze?"选择稍后提醒，或关闭本次提醒":"即将结束，不再延后提醒");tint(agenda_popup_status,can_snooze?MUTED:AMBER);saver_leave();lv_display_trigger_activity(NULL);visible(agenda_popup,true);lv_obj_move_foreground(agenda_popup);voice_remote_notify_agenda();
}
#include "ui_agenda_editor.h"
#if defined(__GNUC__) && !defined(__clang__)
#pragma GCC pop_options
#endif
