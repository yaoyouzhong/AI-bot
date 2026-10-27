#include "esp_timer.h"
#include "lvgl.h"
#include "src/draw/sw/lv_draw_sw.h"
#include "src/draw/sw/blend/lv_draw_sw_blend.h"
#include "src/draw/lv_draw_private.h"

// Single software draw unit, called under the LVGL lock. Link wrappers observe
// the original implementation; no changed pixels, allocations or DMA behavior.
// Image/fill/label times include their nested blend time, so do not sum all four.
static unsigned current[4];
static unsigned full_image_pixels;
volatile unsigned tab5_draw_image_us,tab5_draw_fill_us,tab5_draw_label_us,tab5_draw_blend_us;
volatile unsigned tab5_full_image_frames;
void tab5_draw_timing(bool begin) {
    if(begin){for(unsigned i=0;i<4;i++)current[i]=0;full_image_pixels=0;}
    else {
        tab5_draw_image_us=current[0];tab5_draw_fill_us=current[1];
        tab5_draw_label_us=current[2];tab5_draw_blend_us=current[3];
        if(full_image_pixels>=1280*720)tab5_full_image_frames++;
    }
}

#define OBSERVE_DRAW(name,type,index) \
    void __real_##name(lv_draw_unit_t *,type *,const lv_area_t *); \
    void __wrap_##name(lv_draw_unit_t *unit,type *dsc,const lv_area_t *area) { \
        int64_t start=esp_timer_get_time();__real_##name(unit,dsc,area); \
        current[index]+=(unsigned)(esp_timer_get_time()-start); \
    }
OBSERVE_DRAW(lv_draw_sw_fill,lv_draw_fill_dsc_t,1)
OBSERVE_DRAW(lv_draw_sw_label,const lv_draw_label_dsc_t,2)

void __real_lv_draw_sw_image(lv_draw_unit_t *,const lv_draw_image_dsc_t *,const lv_area_t *);
void __wrap_lv_draw_sw_image(lv_draw_unit_t *unit,const lv_draw_image_dsc_t *dsc,const lv_area_t *area) {
    int64_t start=esp_timer_get_time();__real_lv_draw_sw_image(unit,dsc,area);
    current[0]+=(unsigned)(esp_timer_get_time()-start);
    // Count a whole frame, excluding small image patches redrawn because an
    // obscured clock label changed. The strips must together cover 720p.
    if(dsc->header.w==1280&&dsc->header.h==720&&dsc->header.cf==LV_COLOR_FORMAT_RGB565) {
        int x1=LV_MAX(area->x1,unit->clip_area->x1),x2=LV_MIN(area->x2,unit->clip_area->x2);
        int y1=LV_MAX(area->y1,unit->clip_area->y1),y2=LV_MIN(area->y2,unit->clip_area->y2);
        if(x2>=x1&&y2>=y1)full_image_pixels+=(unsigned)((x2-x1+1)*(y2-y1+1));
    }
}

void __real_lv_draw_sw_blend(lv_draw_unit_t *,const lv_draw_sw_blend_dsc_t *);
bool tab5_ppa_fill(lv_draw_unit_t *,const lv_draw_sw_blend_dsc_t *);
void __wrap_lv_draw_sw_blend(lv_draw_unit_t *unit,const lv_draw_sw_blend_dsc_t *dsc) {
    int64_t start=esp_timer_get_time();
    if(!tab5_ppa_fill(unit,dsc))__real_lv_draw_sw_blend(unit,dsc);
    current[3]+=(unsigned)(esp_timer_get_time()-start);
}
