#include "camera_viewport.h"
#include "camera_compositor.h"
#include "photo_display_probe.h"
#include "display_bandwidth.h"
#include "esp_lvgl_port.h"
#include "esp_heap_caps.h"
#include "esp_timer.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "src/core/lv_obj_draw_private.h"
#include <stdatomic.h>
#include <stdlib.h>
#include <string.h>

static lv_obj_t *controls[2];
static lv_display_t *display;
static lv_display_t *still_display;
static atomic_bool still_active;
static bool sensor_clockwise;
static camera_overlay overlays[2];
static atomic_uint present_count, compose_time, present_time, direct_mode;

void camera_viewport_set_controls(lv_obj_t *shutter, lv_obj_t *close) {
    controls[0]=shutter;controls[1]=close;
}
static void free_overlays(void) {
    for(unsigned i=0;i<2;i++) {free((void *)overlays[i].pixels);memset(&overlays[i],0,sizeof(overlays[i]));}
}
// Keep the actual widget artwork, including text and anti-aliased circles.
// Snapshots are taken once, before the worker starts composing any frames.
static bool snapshot_control(lv_obj_t *obj,camera_overlay *out,lv_display_rotation_t rotation) {
    if(!obj)return false;
    bool hidden=lv_obj_has_flag(obj,LV_OBJ_FLAG_HIDDEN);
    lv_state_t restore=lv_obj_get_state(obj);
    lv_obj_remove_flag(obj,LV_OBJ_FLAG_HIDDEN);
    lv_obj_remove_state(obj,LV_STATE_DISABLED|LV_STATE_PRESSED|LV_STATE_FOCUSED|LV_STATE_FOCUS_KEY);
    lv_obj_update_layout(obj);
    lv_area_t a;lv_obj_get_coords(obj,&a);
    int ext=lv_obj_get_ext_draw_size(obj);lv_area_increase(&a,ext,ext);
    unsigned w=(unsigned)lv_area_get_width(&a),h=(unsigned)lv_area_get_height(&a);
    unsigned stride=lv_draw_buf_width_to_stride(w,LV_COLOR_FORMAT_ARGB8888);
    void *pixels=heap_caps_aligned_alloc(16,(size_t)stride*h+16,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    lv_draw_buf_t snap;
    bool ok=pixels&&lv_draw_buf_init(&snap,w,h,LV_COLOR_FORMAT_ARGB8888,stride,pixels,stride*h+16)==LV_RESULT_OK&&
        lv_snapshot_take_to_draw_buf(obj,LV_COLOR_FORMAT_ARGB8888,&snap)==LV_RESULT_OK;
    lv_obj_add_state(obj,restore);
    if(hidden)lv_obj_add_flag(obj,LV_OBJ_FLAG_HIDDEN);
    if(ok) {
        // Clip theme extents at the edge of the landscape panel before rotation.
        int sw=lv_display_get_horizontal_resolution(NULL),sh=lv_display_get_vertical_resolution(NULL);
        int x1=LV_MAX(0,a.x1),y1=LV_MAX(0,a.y1),x2=LV_MIN(sw-1,a.x2),y2=LV_MIN(sh-1,a.y2);
        ok=x2>=x1&&y2>=y1;
        if(ok) {
            bool quarter=rotation==LV_DISPLAY_ROTATION_90||rotation==LV_DISPLAY_ROTATION_270;
            unsigned rw=(unsigned)(quarter?y2-y1+1:x2-x1+1),rh=(unsigned)(quarter?x2-x1+1:y2-y1+1);
            uint32_t *rotated=heap_caps_malloc((size_t)rw*rh*4,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
            ok=rotated!=NULL;
            if(ok) {
                for(int y=y1;y<=y2;y++) {
                    const uint32_t *row=(const uint32_t *)(snap.data+(y-a.y1)*snap.header.stride);
                    for(int x=x1;x<=x2;x++) {
                        size_t index=rotation==LV_DISPLAY_ROTATION_90?(size_t)(x2-x)*rw+(y-y1):
                            rotation==LV_DISPLAY_ROTATION_270?(size_t)(x-x1)*rw+(y2-y):
                            rotation==LV_DISPLAY_ROTATION_180?(size_t)(y2-y)*rw+(x2-x):
                            (size_t)(y-y1)*rw+(x-x1);
                        rotated[index]=row[x-a.x1];
                    }
                }
                *out=(camera_overlay){.pixels=rotated,.width=rw,.height=rh,.stride=rw*4,
                    .x=rotation==LV_DISPLAY_ROTATION_90?y1:rotation==LV_DISPLAY_ROTATION_270?719-y2:rotation==LV_DISPLAY_ROTATION_180?719-x2:x1,
                    .y=rotation==LV_DISPLAY_ROTATION_90?1279-x2:rotation==LV_DISPLAY_ROTATION_270?x1:rotation==LV_DISPLAY_ROTATION_180?1279-y2:y1};
            }
        }
    }
    free(pixels);return ok;
}
int camera_viewport_begin(lv_obj_t *canvas) {
    if(display||still_display)return -1;
    atomic_store(&present_count,0);atomic_store(&compose_time,0);atomic_store(&present_time,0);
    atomic_store(&direct_mode,0);
    lv_display_rotation_t rotation=canvas?lv_display_get_rotation(lv_obj_get_display(canvas)):LV_DISPLAY_ROTATION_0;
    if(!canvas)return 0;
    sensor_clockwise=rotation==LV_DISPLAY_ROTATION_0||rotation==LV_DISPLAY_ROTATION_180;
    if(!snapshot_control(controls[0],&overlays[0],rotation)||!snapshot_control(controls[1],&overlays[1],rotation)) {
        free_overlays();return false;
    }
    lv_display_t *candidate=lv_obj_get_display(canvas);
    if(!lvgl_port_camera_begin(candidate)) {
        free_overlays();
        if(lvgl_port_camera_end(candidate,250))return 0;
        display=candidate;atomic_store(&direct_mode,2);return -1;
    }
    display=candidate;atomic_store(&direct_mode,1);return true;
}
typedef struct {bool (*keep)(void *);void *ctx;int64_t yielded;} pacing;
static bool yield_frame(void *context) {
    pacing *p=context;
    if(p->keep&&!p->keep(p->ctx))return false;
    int64_t now=esp_timer_get_time();
    if(now-p->yielded>=8000){vTaskDelay(1);p->yielded=esp_timer_get_time();}
    return !p->keep||p->keep(p->ctx);
}
bool camera_viewport_frame(const uint16_t *source,bool (*keep)(void *),void *ctx) {
    if(!display)return false;
    uint16_t *back=lvgl_port_camera_back_buffer(display);
    if(!back)return false;
    int64_t start=esp_timer_get_time();pacing p={keep,ctx,start};
    bool composed=camera_composite_frame_oriented(back,source,sensor_clockwise,overlays,2,yield_frame,&p);
    atomic_store(&compose_time,(unsigned)(esp_timer_get_time()-start));
    if(!composed||(keep&&!keep(ctx)))return false;
    start=esp_timer_get_time();
    bool presented=lvgl_port_camera_present(display,250);
    atomic_store(&present_time,(unsigned)(esp_timer_get_time()-start));
    if(presented)atomic_fetch_add(&present_count,1);
    else atomic_store(&direct_mode,2);
    return presented;
}
bool camera_viewport_end(void) {
    if(!display)return true;
    bool ok=lvgl_port_camera_end(display,250);
    // The LCD holds its own framebuffers; it never reads snapshot/CSI storage.
    free_overlays();if(ok)display=NULL;atomic_store(&direct_mode,ok?0:2);return ok;
}
int camera_viewport_still_begin(lv_obj_t *canvas) {
    if(!canvas||display)return 0;
    if(still_display)return -1;
    lv_display_t *candidate=lv_obj_get_display(canvas);
    if(!candidate)return 0;
    // Keep the current scanout unchanged; only drain and pause ordinary
    // rendering. Failure acquires nothing, so it needs no camera-end cleanup.
    if(!lvgl_port_display_freeze(candidate,250))return 0;
    still_display=candidate;atomic_store(&still_active,true);
    // Scanout keeps reading PSRAM while every photo preparation/upload stage runs.
    display_bandwidth_photo_begin();photo_display_probe_begin();
    atomic_store(&direct_mode,3);return 1;
}
bool camera_viewport_still_end(void) {
    if(!still_display)return true;
    bool ok=lvgl_port_display_unfreeze(still_display);
    // The caller holds the LVGL lock. Restore before that turn can render.
    // Failed release retains the owner and saved bandwidth priorities for retry.
    if(ok){photo_display_probe_end();display_bandwidth_photo_end();still_display=NULL;atomic_store(&still_active,false);}
    atomic_store(&direct_mode,ok?0:2);return ok;
}
bool camera_viewport_still_active(void) {return atomic_load(&still_active);}
unsigned camera_viewport_diagnostic_largest(unsigned caps) {
    // Advisory avoidance only: an already-started scan may overlap acquisition.
    return camera_viewport_still_active()?0:(unsigned)heap_caps_get_largest_free_block(caps);
}
void camera_viewport_diagnostics(unsigned *presented,unsigned *compose_us,unsigned *present_us,unsigned *mode) {
    *presented=atomic_load(&present_count);*compose_us=atomic_load(&compose_time);
    *present_us=atomic_load(&present_time);*mode=atomic_load(&direct_mode);
}


