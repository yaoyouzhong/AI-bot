#include "camera_compositor.h"
#include <limits.h>
#include <stddef.h>
#include "../components/esp_lvgl_port/src/lvgl9/tab5_rotate_rgb565.h"
#include "lvgl.h"
#include "src/draw/sw/blend/lv_draw_sw_blend_private.h"
#include "src/draw/sw/blend/lv_draw_sw_blend_to_rgb565.h"

#define FRAME_BYTES ((size_t)CAMERA_COMPOSITOR_SOURCE_WIDTH * CAMERA_COMPOSITOR_SOURCE_HEIGHT * 2u)

static bool valid_extent(const void *buffer, size_t size) {
    return buffer && size && (uintptr_t)buffer <= UINTPTR_MAX - size;
}

static bool overlaps(const void *a, size_t a_size, const void *b, size_t b_size) {
    return (uintptr_t)a < (uintptr_t)b + b_size && (uintptr_t)b < (uintptr_t)a + a_size;
}

bool camera_composite_frame_oriented(uint16_t *portrait, const uint16_t *landscape, bool clockwise,
                            const camera_overlay *overlays, unsigned count,
                            bool (*yield_cb)(void *), void *ctx) {
    if(!valid_extent(portrait,FRAME_BYTES) || !valid_extent(landscape,FRAME_BYTES) ||
       (((uintptr_t)portrait | (uintptr_t)landscape) & 1u) ||
       overlaps(portrait,FRAME_BYTES,landscape,FRAME_BYTES) || (count && !overlays))
        return false;
    if(count) {
        if(sizeof(*overlays)>SIZE_MAX/count)return false;
        size_t descriptors=(size_t)count*sizeof(*overlays);
        if(!valid_extent(overlays,descriptors) ||
           ((uintptr_t)overlays % _Alignof(camera_overlay)) ||
           overlaps(portrait,FRAME_BYTES,overlays,descriptors))return false;
    }
    // Validate every input before touching the destination, including later
    // overlays, so malformed input cannot leave an apparently usable frame.
    for(unsigned i=0;i<count;i++) {
        const camera_overlay *o=&overlays[i];
        if(!o->width || !o->height || o->width>CAMERA_COMPOSITOR_TARGET_WIDTH ||
           o->height>CAMERA_COMPOSITOR_TARGET_HEIGHT || o->x<0 || o->y<0 ||
           (uint32_t)o->x>CAMERA_COMPOSITOR_TARGET_WIDTH-o->width ||
           (uint32_t)o->y>CAMERA_COMPOSITOR_TARGET_HEIGHT-o->height ||
           o->stride<o->width*4u || o->stride>INT32_MAX || (o->stride & 3u) ||
           ((uintptr_t)o->pixels & 3u))
            return false;
        size_t row_bytes=(size_t)o->width*4u;
        if(o->height>1u && o->stride>(SIZE_MAX-row_bytes)/(o->height-1u))
            return false;
        size_t extent=(size_t)(o->height-1u)*o->stride+row_bytes;
        if(!valid_extent(o->pixels,extent) || overlaps(portrait,FRAME_BYTES,o->pixels,extent))
            return false;
    }
    // Complete 64 native rows before advancing to the next group. Each inner
    // tile appends 16 native columns to the same rows, so partially written
    // cache lines are revisited immediately instead of after a full frame-wide
    // source strip. The whole group's source and target pixels total 180 KiB.
    for(unsigned x=0;x<CAMERA_COMPOSITOR_SOURCE_WIDTH;x+=64u) {
        uint16_t *group=portrait+(size_t)(clockwise?x:CAMERA_COMPOSITOR_SOURCE_WIDTH-x-64u)*CAMERA_COMPOSITOR_TARGET_WIDTH;
        for(unsigned y=0;y<CAMERA_COMPOSITOR_SOURCE_HEIGHT;y+=16u) {
            tab5_rotate_rgb565(landscape+(size_t)y*CAMERA_COMPOSITOR_SOURCE_WIDTH+x,group+(clockwise?CAMERA_COMPOSITOR_SOURCE_HEIGHT-y-16u:y),
                              64,16,CAMERA_COMPOSITOR_SOURCE_WIDTH*2u,
                              CAMERA_COMPOSITOR_TARGET_WIDTH*2u,clockwise);
        }
        if(yield_cb && !yield_cb(ctx))return false;
    }
    for(unsigned i=0;i<count;i++) {
        const camera_overlay *o=&overlays[i];
        lv_draw_sw_blend_image_dsc_t dsc={0};
        dsc.dest_buf=portrait+(size_t)o->y*CAMERA_COMPOSITOR_TARGET_WIDTH+(uint32_t)o->x;
        dsc.dest_w=(int32_t)o->width;
        dsc.dest_h=(int32_t)o->height;
        dsc.dest_stride=CAMERA_COMPOSITOR_TARGET_WIDTH*2u;
        dsc.src_buf=o->pixels;
        dsc.src_stride=(int32_t)o->stride;
        dsc.src_color_format=LV_COLOR_FORMAT_ARGB8888;
        dsc.opa=LV_OPA_COVER;
        dsc.blend_mode=LV_BLEND_MODE_NORMAL;
        dsc.relative_area=(lv_area_t){o->x,o->y,o->x+(int32_t)o->width-1,o->y+(int32_t)o->height-1};
        dsc.src_area=(lv_area_t){0,0,(int32_t)o->width-1,(int32_t)o->height-1};
        lv_draw_sw_blend_image_to_rgb565(&dsc);
    }
    return true;
}



bool camera_composite_frame(uint16_t *portrait,const uint16_t *landscape,
                            const camera_overlay *overlays,unsigned count,
                            bool (*yield_cb)(void *),void *ctx) {
    return camera_composite_frame_oriented(portrait,landscape,false,overlays,count,yield_cb,ctx);
}
