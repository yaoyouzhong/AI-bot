// Device-only saver style: separate NVS key preserves the existing preference
// blob and bridge display-settings protocol. Gallery bitmaps are pulled on demand.
LV_FONT_DECLARE(tab5_annual_digits_144);
LV_FONT_DECLARE(tab5_annual_text_26);
static lv_obj_t *classic_seconds,*classic_rule;
LV_FONT_DECLARE(tab5_timer_digits_192);
static int saver_style,annual_palette=1,annual_year,annual_day,annual_days;
static bool saver_preview,saver_has_time;
static struct tm saver_local_time;
static lv_obj_t *annual_layer,*annual_title,*annual_percent,*annual_passed,*annual_remaining,*annual_date;
static lv_obj_t *saver_selector,*saver_style_status,*saver_preview_button;
static void saver_enter(bool offline);
static bool saver_is_gallery(void){return TAB5_LOCAL_ART&&saver_style>=6&&saver_style<=7;}
#include "ui_date_countdown.h"
#include "ui_monthly_saver.h"
#if TAB5_LOCAL_ART
#include "ui_gallery_saver.h"
#endif
static bool saver_is_annual(void) {return (saver_style>=1&&saver_style<=3)||saver_style==5;}
static uint32_t annual_color(uint32_t day,uint32_t night,uint32_t warm) {
    return annual_palette==2?night:annual_palette==3?warm:day;
}
static int annual_auto_palette(const struct tm *local) {return local->tm_hour>=7&&local->tm_hour<19?1:2;}
static void classic_palette_apply(int palette) {
    bool day=palette==1;
    ui_bg_color(saver_layer,lv_color_hex(day?0xF1F3F6:0x000000),0);
    tint(saver_time,day?0x243447:0xCCD2DA);
    tint(classic_seconds,day?0x64748B:0x667382);
    ui_bg_color(classic_rule,lv_color_hex(day?0xD0D8E2:0x28313A),0);
    tint(saver_date,day?0x536275:0x85919E);
}
static int saver_year_days(int year) {return year%4==0&&(year%100!=0||year%400==0)?366:365;}
static void annual_draw(lv_event_t *e) {
    if(!annual_day)return;
    lv_layer_t *layer=lv_event_get_layer(e);lv_area_t bounds;
    lv_obj_get_coords(lv_event_get_target(e),&bounds);
    lv_draw_rect_dsc_t dot;lv_draw_rect_dsc_init(&dot);dot.radius=LV_RADIUS_CIRCLE;
    for(int i=0;i<annual_days;i++) {
        bool today=i==annual_day-1;int radius=today?8:5;
        int x=bounds.x1+632+(i%20)*27,y=bounds.y1+148+(i/20)*25;
        dot.bg_color=lv_color_hex(i<annual_day-1?annual_color(0x293FC6,0x657B99,0x8D7964):annual_color(0xDAE0ED,0x252D39,0x302B25));
        dot.bg_opa=today?LV_OPA_TRANSP:LV_OPA_COVER;
        dot.border_width=today?3:0;dot.border_color=lv_color_hex(annual_color(0x293FC6,0xA6B5C8,0xC6B69F));
        lv_area_t area={x-radius,y-radius,x+radius-1,y+radius-1};lv_draw_rect(layer,&dot,&area);
    }
}
static void annual_palette_apply(int palette) {
    annual_palette=palette;
    ui_bg_color(saver_layer,lv_color_hex(palette==1?0xF1F3F6:0x000000),0);
    tint(annual_title,annual_color(0x637393,0x828D9C,0x978C80));
    tint(annual_percent,annual_color(0x2438B8,0x8294AE,0xAD9983));
    tint(annual_passed,annual_color(0x536A9B,0x828D9C,0x978C80));
    tint(annual_remaining,annual_color(0x687B9D,0x828D9C,0x978C80));
    tint(annual_date,annual_color(0x687B9D,0x828D9C,0x978C80));
    lv_obj_invalidate(annual_layer);
}
static void saver_style_apply(void) {
    bool annual=saver_is_annual();
    ui_bg_color(saver_layer,lv_color_hex(0x000000),0);
    visible(saver_time,saver_style==0);visible(saver_date,saver_style==0);visible(classic_seconds,saver_style==0);visible(classic_rule,saver_style==0);visible(annual_layer,annual);visible(monthly_layer,saver_style==4);
    if(saver_style==0)classic_palette_apply(saver_has_time?annual_auto_palette(&saver_local_time):2);
#if TAB5_LOCAL_ART
    visible(gallery_layer,saver_is_gallery());if(saver_is_gallery())gallery_show();
#endif
    visible(date_saver_layer,saver_style==UI_SAVER_COUNTDOWN);
    if(saver_style==UI_SAVER_COUNTDOWN)date_saver_update(saver_has_time?&saver_local_time:NULL);
    if(annual)annual_palette_apply(saver_style==5?(saver_has_time?annual_auto_palette(&saver_local_time):2):saver_style);
    monthly_preview_month=0;monthly_rendered_day=0;
}
static void saver_update(const struct tm *local) {
#if TAB5_LOCAL_ART
    if(saver_is_gallery()){gallery_update(local);return;}
#endif
    if(saver_style==UI_SAVER_COUNTDOWN){date_saver_update(local);return;}
    if(saver_style==4){monthly_update(local);return;}
    char date[20],clock[20];strftime(date,sizeof(date),"%Y-%m-%d",local);strftime(clock,sizeof(clock),"%H:%M:%S",local);
    if(saver_style==0) {
        classic_palette_apply(annual_auto_palette(local));
        strftime(clock,sizeof(clock),"%H:%M",local);ui_text(saver_time,clock);
        ui_text_fmt(classic_seconds,"%02d",local->tm_sec);
        static const char *week[]={"周日","周一","周二","周三","周四","周五","周六"};
        ui_text_fmt(saver_date,"%04d / %02d / %02d   %s",local->tm_year+1900,local->tm_mon+1,local->tm_mday,week[(local->tm_wday+7)%7]);
        int shift=((local->tm_hour*60+local->tm_min)%5-2)*6;
        lv_obj_update_layout(saver_time);lv_obj_update_layout(classic_seconds);
        int time_width=lv_obj_get_width(saver_time),seconds_width=lv_obj_get_width(classic_seconds);
        int left=(1280-time_width-32-seconds_width)/2+shift;
        lv_obj_set_pos(saver_time,left,224-shift);
        lv_obj_set_pos(classic_seconds,left+time_width+32,310-shift);
        lv_obj_set_pos(classic_rule,420+shift,454-shift);
        lv_obj_set_pos(saver_date,180+shift,494-shift);return;
    }
    if(saver_style==5) {
        int palette=annual_auto_palette(local);
        if(palette!=annual_palette)annual_palette_apply(palette);
    }
    int year=local->tm_year+1900,day=local->tm_yday+1,days=saver_year_days(year);
    if(year<2000||day<1||day>days)return;
    if(annual_year!=year||annual_day!=day) {
        annual_year=year;annual_day=day;annual_days=days;
        ui_text_fmt(annual_title,"%d  ·  一天一格",year);
        ui_text_fmt(annual_percent,"%d%%",(day-1)*100/days);
        ui_text_fmt(annual_passed,"已过 %d 天",day-1);
        ui_text_fmt(annual_remaining,"今天之后，还剩 %d 天",days-day);
        ui_text(annual_date,date);lv_obj_invalidate(annual_layer);
    }
    // Move gently once per minute; no continuous animation or daily flash writes.
    int shift=(local->tm_min%3-1)*4;
    lv_obj_set_pos(annual_layer,shift,-shift);
}
static void saver_style_changed(lv_event_t *e) {
    int selected=ui_saver_style_from_option((int)lv_dropdown_get_selected(lv_event_get_target(e)));
    if(selected==saver_style)return;
    if(!ui_saver_style_save(selected)) {
        lv_dropdown_set_selected(saver_selector,ui_saver_option_from_style(saver_style));ui_text(saver_style_status,"保存失败，请重试");return;
    }
    saver_style=selected;ui_text(saver_style_status,"屏保类型");
    date_settings_refresh();
}
static void saver_preview_clicked(lv_event_t *e) {
    (void)e;saver_enter(false);saver_preview=true;
    set_backlight(saver_restore_brightness);
    if(saver_style==4&&saver_has_time)monthly_update(&saver_local_time);
}
static void annual_create(void) {
    annual_layer=lv_obj_create(saver_layer);lv_obj_remove_style_all(annual_layer);
    lv_obj_set_size(annual_layer,1280,720);lv_obj_remove_flag(annual_layer,LV_OBJ_FLAG_SCROLLABLE|LV_OBJ_FLAG_CLICKABLE);
    lv_obj_add_event_cb(annual_layer,annual_draw,LV_EVENT_DRAW_MAIN,NULL);
    annual_title=label(annual_layer,"年度点阵 · 等待校时",72,52,600,false);tint(annual_title,0x637393);
    annual_percent=label(annual_layer,"--%",68,230,500,false);ui_text_font(annual_percent,&tab5_annual_digits_144,0);
    tint(annual_percent,0x2438B8);
    annual_passed=label(annual_layer,"等待同步日期",76,398,450,false);tint(annual_passed,0x536A9B);
    annual_remaining=label(annual_layer,"",76,628,540,false);tint(annual_remaining,0x687B9D);
    annual_date=label(annual_layer,"",888,52,318,false);tint(annual_date,0x687B9D);
    lv_obj_t *small[]={annual_title,annual_passed,annual_remaining,annual_date};
    for(int i=0;i<4;i++)ui_text_font(small[i],&tab5_annual_text_26,0);
    lv_obj_set_style_text_align(annual_date,LV_TEXT_ALIGN_RIGHT,0);
    clock_touch_target(annual_layer);visible(annual_layer,false);
}
