// Shared state/timeline for the overview and the opt-in Codex saver.
#include "codex_companion.h"
#include "ui_claude_companion.h"
static lv_obj_t *home_companion,*companion_saver_layer,*companion_saver_image,*companion_saver_clock;
static codex_companion_animation companion_animation;
static lv_image_dsc_t companion_descriptor;
static uint8_t *companion_pixels;
static unsigned companion_rendered_frame=UINT32_MAX;
static bool companion_home_enabled;
static lv_obj_t *companion_saver_fallback;
static lv_obj_t *companion_saver_background;
static uint16_t *companion_background_pixels;
static lv_image_dsc_t companion_background_descriptor;
static int companion_background_theme=-1;

static void companion_backdrop(bool enabled,bool warm) {
    if(!companion_saver_background)return;
    visible(companion_saver_background,enabled);if(!enabled)return;
    if(!companion_background_pixels)companion_background_pixels=lv_malloc(640u*360u*2u);
    if(!companion_background_pixels){visible(companion_saver_background,false);return;}
    if(companion_background_theme==(int)warm)return;
    // A static, shared PSRAM texture avoids dark RGB565 gradient bands. Stable
    // spatial dithering is generated only when the selected pet changes.
    const int cool_top[3]={14,26,36},cool_bottom[3]={24,43,52},cool_glow[3]={8,16,25};
    const int warm_top[3]={30,23,21},warm_bottom[3]={47,34,29},warm_glow[3]={18,10,5};
    const int *top=warm?warm_top:cool_top,*bottom=warm?warm_bottom:cool_bottom,*light=warm?warm_glow:cool_glow;
    for(int y=0;y<360;y++)for(int x=0;x<640;x++) {
        int dx=(x-320)*256/400,dy=(y-155)*256/250;
        int falloff=LV_MAX(0,65536-dx*dx-dy*dy);falloff=(falloff>>8)*(falloff>>8);
        unsigned shade[3];
        uint32_t noise=(uint32_t)x*374761393u+(uint32_t)y*668265263u;
        noise=(noise^(noise>>13))*1274126177u;noise=(noise^(noise>>16))&255u;
        for(int c=0;c<3;c++)shade[c]=(unsigned)(top[c]+(bottom[c]-top[c])*y/359+light[c]*falloff/65536);
        companion_background_pixels[y*640+x]=(uint16_t)(((shade[0]*31+noise)/255<<11)|((shade[1]*63+noise)/255<<5)|((shade[2]*31+noise)/255));
    }
    lv_image_cache_drop(&companion_background_descriptor);
    companion_background_descriptor=(lv_image_dsc_t){.header={.magic=LV_IMAGE_HEADER_MAGIC,.cf=LV_COLOR_FORMAT_RGB565,
        .w=640,.h=360,.stride=1280},.data_size=640u*360u*2u,.data=(uint8_t *)companion_background_pixels};
    lv_image_set_src(companion_saver_background,&companion_background_descriptor);
    companion_background_theme=warm;
}

static void companion_home_create(lv_obj_t *parent) {
    home_companion=lv_image_create(parent);
    // Reduce the overview art slightly around its existing center.
    lv_obj_set_pos(home_companion,528,34);
    lv_image_set_pivot(home_companion,0,0);lv_image_set_scale(home_companion,220);
    lv_obj_remove_flag(home_companion,LV_OBJ_FLAG_CLICKABLE|LV_OBJ_FLAG_SCROLLABLE);
    visible(home_companion,false);
    claude_pet_create(&home_claude,parent,458,4,284,215);
}
static void companion_saver_create(lv_obj_t *parent) {
    companion_saver_background=lv_image_create(parent);
    lv_image_set_pivot(companion_saver_background,0,0);lv_image_set_scale(companion_saver_background,512);
    lv_image_set_antialias(companion_saver_background,false);lv_obj_move_to_index(companion_saver_background,0);
    lv_obj_remove_flag(companion_saver_background,LV_OBJ_FLAG_CLICKABLE|LV_OBJ_FLAG_SCROLLABLE);
    visible(companion_saver_background,false);
    companion_saver_layer=lv_obj_create(parent);lv_obj_remove_style_all(companion_saver_layer);
    lv_obj_set_size(companion_saver_layer,1280,720);
    lv_obj_remove_flag(companion_saver_layer,LV_OBJ_FLAG_CLICKABLE|LV_OBJ_FLAG_SCROLLABLE);
    companion_saver_image=lv_image_create(companion_saver_layer);
    lv_obj_set_pos(companion_saver_image,448,92);lv_image_set_pivot(companion_saver_image,0,0);lv_image_set_scale(companion_saver_image,512);
    lv_obj_remove_flag(companion_saver_image,LV_OBJ_FLAG_CLICKABLE|LV_OBJ_FLAG_SCROLLABLE);
    claude_pet_create(&saver_claude,companion_saver_layer,240,64,800,520);
    companion_saver_fallback=label(companion_saver_layer,"萌宠资源暂不可用",180,536,920,false);
    lv_obj_set_style_text_align(companion_saver_fallback,LV_TEXT_ALIGN_CENTER,0);visible(companion_saver_fallback,false);
    companion_saver_clock=label(companion_saver_layer,"--:--",530,636,220,false);
    ui_text_font(companion_saver_clock,&lv_font_montserrat_32,0);tint(companion_saver_clock,0xAAB6BE);
    lv_obj_set_style_text_letter_space(companion_saver_clock,2,0);
    lv_obj_set_style_text_align(companion_saver_clock,LV_TEXT_ALIGN_CENTER,0);
    visible(companion_saver_layer,false);
}
static uint32_t companion_color(codex_companion_state state) {
    if(state==CODEX_COMPANION_APPROVAL||state==CODEX_COMPANION_QUESTION||state==CODEX_COMPANION_WAITING)return AMBER;
    if(state==CODEX_COMPANION_WORKING||state==CODEX_COMPANION_DONE)return GREEN;
    if(state==CODEX_COMPANION_BLOCKED)return 0xEFB6AC;
    return MUTED;
}
static void companion_set_value(codex_companion_value value,int64_t now) {
    bool changed=codex_companion_select(&companion_animation,value,now);
    if(!changed)return;
    lv_obj_t *images[]={home_companion,companion_saver_image};
    for(unsigned i=0;i<2;i++)if(images[i]) {
        lv_obj_set_style_opa(images[i],value.state==CODEX_COMPANION_UNKNOWN?LV_OPA_80:LV_OPA_COVER,0);
        lv_image_set_rotation(images[i],value.state==CODEX_COMPANION_STOPPED?57:0);
    }
}
static void companion_saver_time(const struct tm *local) {
    char clock[8];strftime(clock,sizeof(clock),"%H:%M",local);ui_text(companion_saver_clock,clock);
    int shift=(local->tm_min%5-2)*4;
    lv_obj_set_pos(companion_saver_layer,shift,-shift);
}
static void companion_tick(int64_t now,bool show_home,bool show_saver) {
    show_home=show_home&&companion_home_enabled;
    if((!show_home&&!show_saver)||!codex_companion_asset_available())return;
    if(!companion_pixels) {
        companion_pixels=lv_malloc(CODEX_COMPANION_FRAME_BYTES);
        if(!companion_pixels)return; // Short status remains available without artwork.
    }
    unsigned frame=show_saver?codex_companion_saver_frame(&companion_animation,now):codex_companion_frame(&companion_animation,now);
    if(frame!=companion_rendered_frame) {
        if(!codex_companion_decode(frame,companion_pixels,CODEX_COMPANION_FRAME_BYTES))return;
        lv_image_cache_drop(&companion_descriptor);
        companion_descriptor=(lv_image_dsc_t){.header={.magic=LV_IMAGE_HEADER_MAGIC,.cf=LV_COLOR_FORMAT_RGB565A8,
            .w=CODEX_COMPANION_WIDTH,.h=CODEX_COMPANION_HEIGHT,.stride=CODEX_COMPANION_WIDTH*2},
            .data_size=CODEX_COMPANION_FRAME_BYTES,.data=companion_pixels};
        if(home_companion)lv_image_set_src(home_companion,&companion_descriptor);
        if(companion_saver_image)lv_image_set_src(companion_saver_image,&companion_descriptor);
        companion_rendered_frame=frame;
    }
    if(home_companion)visible(home_companion,companion_home_enabled);
}
