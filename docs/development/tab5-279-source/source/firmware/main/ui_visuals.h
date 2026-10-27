// Internal LVGL view helpers. Included by ui.c after its data and widget
// helpers.
#define NET_UP 0xE9C48C
#define NET_DOWN 0x65D99A
typedef struct {
    lv_obj_t *arc, *number, *caption;
} visual_meter;
static visual_meter system_meter[2];
static lv_obj_t *model_state[2], *model_dot[2];
static lv_obj_t *weather_temp;
static lv_obj_t *weather_condition,*weather_low,*weather_high,*weather_aqi_title,*weather_aqi_category,*weather_pressure_kind;
static lv_obj_t *weather_facts[4], *weather_humidity, *weather_icon;
static lv_obj_t *network_rates[2], *chart_scale, *network_ticks[5];
static lv_obj_t *music_artist, *music_album, *music_alias, *music_state, *music_time, *music_duration, *pet_dot[2], *pet_hint[2];
static lv_obj_t *music_empty;
static lv_obj_t *clock_hands[3];
static lv_obj_t *clock_face,*clock_numbers[12],*clock_cap,*clock_name;
static int clock_style;
static bool clock_style_error;
static void tint(lv_obj_t *o, uint32_t color) { ui_text_color(o, lv_color_hex(color), 0); }
static lv_obj_t *muted(lv_obj_t *p, const char *s, int x, int y, int w) {
    lv_obj_t *o = label(p, s, x, y, w, false);
    tint(o, MUTED);
    return o;
}
static UI_INIT_SMALL __attribute__((noinline)) lv_obj_t *shape(lv_obj_t *p, int x, int y, int w, int h, uint32_t color, int radius) {
    lv_obj_t *o = lv_obj_create(p);
    lv_obj_remove_style_all(o);
    lv_obj_set_pos(o, x, y);
    lv_obj_set_size(o, w, h);
    ui_bg_color(o, lv_color_hex(color), 0);
    ui_bg_opa(o, LV_OPA_COVER, 0);
    lv_obj_set_style_radius(o, radius, 0);
    lv_obj_remove_flag(o, LV_OBJ_FLAG_CLICKABLE | LV_OBJ_FLAG_SCROLLABLE);
    return o;
}
// Shared Codex/agenda voice affordance; the hit target can grow while the
// independently drawn microphone and stop icons retain the same visual weight.
static void voice_button_glyphs(lv_obj_t *button,lv_obj_t **microphone,lv_obj_t **stop) {
    lv_obj_clean(button);
    lv_obj_t *mic=*microphone=lv_obj_create(button);lv_obj_remove_style_all(mic);
    lv_obj_set_size(mic,56,56);lv_obj_center(mic);lv_obj_remove_flag(mic,LV_OBJ_FLAG_CLICKABLE|LV_OBJ_FLAG_SCROLLABLE);
    lv_obj_t *capsule=shape(mic,22,9,12,24,INK,6);ui_bg_opa(capsule,LV_OPA_TRANSP,0);
    lv_obj_set_style_border_width(capsule,2,0);ui_border_color(capsule,lv_color_hex(INK),0);
    lv_obj_t *arc=lv_arc_create(mic);lv_obj_remove_style_all(arc);lv_obj_set_pos(arc,15,15);lv_obj_set_size(arc,26,26);
    lv_arc_set_bg_angles(arc,0,180);lv_arc_set_angles(arc,0,0);
    lv_obj_set_style_arc_width(arc,2,LV_PART_MAIN);lv_obj_set_style_arc_color(arc,lv_color_hex(INK),LV_PART_MAIN);
    lv_obj_set_style_arc_opa(arc,LV_OPA_COVER,LV_PART_MAIN);lv_obj_set_style_arc_rounded(arc,true,LV_PART_MAIN);lv_obj_remove_flag(arc,LV_OBJ_FLAG_CLICKABLE);
    shape(mic,27,40,2,7,INK,1);shape(mic,21,46,14,2,INK,1);
    *stop=shape(button,0,0,18,18,INK,4);lv_obj_center(*stop);lv_obj_add_flag(*stop,LV_OBJ_FLAG_HIDDEN);
}
static lv_obj_t *notification_upgrade_arrow(lv_obj_t *parent) {
    lv_obj_t *arrow=lv_obj_create(parent);
    lv_obj_remove_style_all(arrow);lv_obj_set_size(arrow,26,26);
    lv_obj_align(arrow,LV_ALIGN_TOP_RIGHT,4,-8);
    ui_bg_color(arrow,lv_color_hex(NOTICE_GREEN),0);
    ui_bg_opa(arrow,LV_OPA_COVER,0);
    lv_obj_set_style_radius(arrow,LV_RADIUS_CIRCLE,0);
    lv_obj_remove_flag(arrow,LV_OBJ_FLAG_SCROLLABLE|LV_OBJ_FLAG_CLICKABLE);
    shape(arrow,12,7,3,13,0xFFFFFF,2);
    for(int step=0;step<=5;step++) {
        shape(arrow,7+step,12-step,3,3,0xFFFFFF,LV_RADIUS_CIRCLE);
        shape(arrow,17-step,12-step,3,3,0xFFFFFF,LV_RADIUS_CIRCLE);
    }
    return arrow;
}
static lv_obj_t *vbar(lv_obj_t *p, int x, int y, int w, uint32_t color) {
    lv_obj_t *o = lv_bar_create(p);
    lv_obj_set_pos(o, x, y);
    lv_obj_set_size(o, w, 10);
    ui_bg_color(o, lv_color_hex(TRACK), LV_PART_MAIN);
    ui_bg_opa(o, LV_OPA_COVER, LV_PART_MAIN);
    ui_bg_color(o, lv_color_hex(color), LV_PART_INDICATOR);
    lv_bar_set_value(o, 0, LV_ANIM_OFF);
    return o;
}
static int percentage(cJSON *o, const char *key) {
    double n = num(o, key);
    return n < 0 ? 0 : n > 100 ? 100 : (int)n;
}
static visual_meter meter(lv_obj_t *p, const char *title, int x, int y, uint32_t color) {
    visual_meter m;
    muted(p, title, x, y, 270);
    m.arc = lv_arc_create(p);
    // LVGL anchors a non-square arc's circle to its shorter side, not its
    // bounding-box center. Keep the arc square so text and circle share a center.
    lv_obj_set_pos(m.arc, x + 42, y + 32);
    lv_obj_set_size(m.arc, 170, 170);
    lv_obj_set_style_pad_all(m.arc,0,0);
    lv_arc_set_rotation(m.arc, 135);
    lv_arc_set_bg_angles(m.arc, 0, 270);
    lv_arc_set_range(m.arc, 0, 100);
    lv_arc_set_value(m.arc, 0);
    lv_obj_remove_style(m.arc, NULL, LV_PART_KNOB);
    lv_obj_remove_flag(m.arc, LV_OBJ_FLAG_CLICKABLE | LV_OBJ_FLAG_SCROLLABLE);
    lv_obj_set_style_arc_width(m.arc, 12, LV_PART_MAIN);
    lv_obj_set_style_arc_width(m.arc, 12, LV_PART_INDICATOR);
    lv_obj_set_style_arc_color(m.arc, lv_color_hex(TRACK), LV_PART_MAIN);
    lv_obj_set_style_arc_color(m.arc, lv_color_hex(color), LV_PART_INDICATOR);
    m.number = label(p, "--", x + 27, y + 96, 200, true);
    lv_obj_set_style_text_align(m.number, LV_TEXT_ALIGN_CENTER, 0);
    lv_label_set_long_mode(m.number,LV_LABEL_LONG_CLIP);
    lv_obj_align_to(m.number,m.arc,LV_ALIGN_CENTER,0,0);
    m.caption = muted(p, "等待数据", x, y + 211, 330);
    return m;
}
static void meter_update(visual_meter *m, cJSON *o, const char *key, uint32_t normal) {
    char b[32];
    bool known = cJSON_IsNumber(item(o, key));
    double sample=num(o,key);
    int n=sample<0?0:sample>100?100:(int)(sample+0.5);
    if(known)snprintf(b,sizeof(b),"%d%%",n);else snprintf(b,sizeof(b),"--");
    ui_text(m->number, b);
    lv_obj_align_to(m->number,m->arc,LV_ALIGN_CENTER,0,0);
    lv_arc_set_value(m->arc, n);
    lv_obj_set_style_arc_opa(m->arc, known ? LV_OPA_COVER : LV_OPA_TRANSP, LV_PART_INDICATOR);
    lv_obj_set_style_arc_color(m->arc, lv_color_hex(n >= 90 ? RED : n >= 75 ? AMBER : normal), LV_PART_INDICATOR);
}
static uint32_t activity_color(cJSON *o) {
    if (yes(o, "needsInput"))
        return AMBER;
    if (yes(o, "completionActive"))
        return GREEN;
    return !strcmp(text(o, "state"), "working") ? GREEN : !strcmp(text(o, "state"), "idle") ? BLUE : MUTED;
}
static lv_obj_t *home_weather_icon;
static int weather_icon_type;
static void weather_icon_rect(lv_layer_t *layer,const lv_area_t *box,int scale,
                              int x,int y,int w,int h,uint32_t color,int radius) {
    lv_area_t a={box->x1+x*scale/256,box->y1+y*scale/256,0,0};
    a.x2=a.x1+(w*scale/256>0?w*scale/256:1)-1;
    a.y2=a.y1+(h*scale/256>0?h*scale/256:1)-1;
    lv_draw_rect_dsc_t d;lv_draw_rect_dsc_init(&d);
    d.bg_color=lv_color_hex(color);d.bg_opa=LV_OPA_COVER;
    d.radius=radius==LV_RADIUS_CIRCLE?radius:radius*scale/256;
    lv_draw_rect(layer,&d,&a);
}
static void weather_icon_draw_type(lv_event_t *e,int weather_icon_type) {
    lv_obj_t *o=lv_event_get_target(e);
    lv_layer_t *layer=lv_event_get_layer(e);
    lv_area_t box;lv_obj_get_coords(o,&box);
    int scale=lv_area_get_width(&box)*256/210;
    if(weather_icon_type==0) {
        weather_icon_rect(layer,&box,scale,72,84,28,5,MUTED,2);
        weather_icon_rect(layer,&box,scale,110,84,28,5,MUTED,2);
        return;
    }
    if(weather_icon_type<=2) {
        weather_icon_rect(layer,&box,scale,75,38,78,78,AMBER,LV_RADIUS_CIRCLE);
        for(int i=0;i<8;i++){
            int angle=i*45;
            int x=111+lv_trigo_cos(angle)*65/32768;
            int y=74+lv_trigo_sin(angle)*65/32768;
            weather_icon_rect(layer,&box,scale,x,y,7,7,AMBER,LV_RADIUS_CIRCLE);
        }
    }
    if(weather_icon_type>=2) {
        weather_icon_rect(layer,&box,scale,48,60,73,73,0xC3D7ED,LV_RADIUS_CIRCLE);
        weather_icon_rect(layer,&box,scale,96,70,65,65,0xC3D7ED,LV_RADIUS_CIRCLE);
        weather_icon_rect(layer,&box,scale,25,86,151,51,0xC3D7ED,25);
    }
    if(weather_icon_type==3)for(int i=0;i<4;i++)
        weather_icon_rect(layer,&box,scale,48+i*32,148,6,22,BLUE,3);
    if(weather_icon_type==4)for(int i=0;i<4;i++)
        weather_icon_rect(layer,&box,scale,48+i*32,151,9,9,INK,LV_RADIUS_CIRCLE);
}
static void weather_icon_draw(lv_event_t *e) {
    intptr_t type=(intptr_t)lv_obj_get_user_data(lv_event_get_target(e));
    weather_icon_draw_type(e,type?(int)type-1:weather_icon_type);
}
static lv_obj_t *weather_icon_create_set(lv_obj_t *p,int x,int y,int scale) {
    lv_obj_t *o=lv_obj_create(p);
    lv_obj_remove_style_all(o);
    lv_obj_set_pos(o,x,y);lv_obj_set_size(o,210*scale/256,180*scale/256);
    lv_obj_remove_flag(o,LV_OBJ_FLAG_SCROLLABLE|LV_OBJ_FLAG_CLICKABLE);
    lv_obj_add_event_cb(o,weather_icon_draw,LV_EVENT_DRAW_MAIN,NULL);
    return o;
}
static void weather_icon_create(lv_obj_t *p){weather_icon=weather_icon_create_set(p,307,106,256);}
static void weather_icon_update(const char *condition) {
    int type = 0;
    if (strstr(condition, "雪"))
        type = 4;
    else if (strstr(condition, "雨") || strstr(condition, "雷"))
        type = 3;
    else if (strstr(condition, "云") || strstr(condition, "阴") || strstr(condition, "雾") || strstr(condition, "霾"))
        type = 2;
    else if (strstr(condition, "晴"))
        type = 1;
    if(type!=weather_icon_type){weather_icon_type=type;lv_obj_invalidate(weather_icon);lv_obj_invalidate(home_weather_icon);}
}
#include "ui_clock_faces.h"
