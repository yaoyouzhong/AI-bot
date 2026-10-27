#pragma once
#include "lvgl.h"

// Shared workbench tokens. Dedicated clock/art canvases keep their palettes.
#define UI_BACKGROUND 0x20272A
#define INK 0xEFF4F2
#define MUTED 0xB9C5C9
#define BLUE 0xB9DCEB
#define CORAL 0xC5BCCF
#define GREEN 0xA8D6BB
#define NOTICE_GREEN 0x21854A
#define AMBER 0xE9C48C
#define RED 0xF0ABA4
#define SURFACE 0x2B3539
#define TRACK 0x495C65
#define UI_CARD_RADIUS 22
#define UI_CARD_PADDING 18
#define UI_CONTROL_RADIUS 12

// Data heartbeats frequently repeat the same value. LVGL's style setter still
// invalidates the object (and inherited descendants) in that case. Compare the
// exact LOCAL selector: comparing the effective theme/inherited value would
// incorrectly skip a new override, or confuse pressed and default states.
static inline void ui_style_color(lv_obj_t *obj,lv_style_prop_t prop,lv_color_t color,
                                 lv_style_selector_t selector) {
    lv_style_value_t old;
    if(lv_obj_get_local_style_prop(obj,prop,&old,selector)==LV_STYLE_RES_FOUND&&
       lv_color_eq(old.color,color))return;
    lv_style_value_t value={.color=color};
    lv_obj_set_local_style_prop(obj,prop,value,selector);
}
static inline void ui_bg_color(lv_obj_t *obj,lv_color_t color,lv_style_selector_t selector) {
    ui_style_color(obj,LV_STYLE_BG_COLOR,color,selector);
}
static inline void ui_text_color(lv_obj_t *obj,lv_color_t color,lv_style_selector_t selector) {
    ui_style_color(obj,LV_STYLE_TEXT_COLOR,color,selector);
}
static inline void ui_border_color(lv_obj_t *obj,lv_color_t color,lv_style_selector_t selector) {
    ui_style_color(obj,LV_STYLE_BORDER_COLOR,color,selector);
}
static inline void ui_bg_opa(lv_obj_t *obj,lv_opa_t opa,lv_style_selector_t selector) {
    lv_style_value_t old;
    if(lv_obj_get_local_style_prop(obj,LV_STYLE_BG_OPA,&old,selector)==LV_STYLE_RES_FOUND&&
       old.num==opa)return;
    lv_obj_set_style_bg_opa(obj,opa,selector);
}
static inline void ui_text_font(lv_obj_t *obj,const lv_font_t *font,lv_style_selector_t selector) {
    lv_style_value_t old;
    if(lv_obj_get_local_style_prop(obj,LV_STYLE_TEXT_FONT,&old,selector)==LV_STYLE_RES_FOUND&&
       old.ptr==font)return;
    lv_obj_set_style_text_font(obj,font,selector);
}
static inline void ui_border_opa(lv_obj_t *obj,lv_opa_t opa,lv_style_selector_t selector) {
    lv_style_value_t old;
    if(lv_obj_get_local_style_prop(obj,LV_STYLE_BORDER_OPA,&old,selector)==LV_STYLE_RES_FOUND&&old.num==opa)return;
    lv_obj_set_style_border_opa(obj,opa,selector);
}
