// Actual production pause/resume and invalidation gate against real LVGL.
// The only facades are RTOS completion and task wake. A still hold must not
// invoke any LCD selection/cache operation, or require a new frame allocation.
#undef NDEBUG
#include "lvgl.h"
#include "../../firmware/components/esp_lvgl_port/include/tab5_display_direct_state.h"
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
typedef struct {
    int disp_type,direct_lock;
    void *trans_sem;
    tab5_display_direct_state_t direct;
    struct {unsigned direct_mode:1;unsigned full_refresh:1;} flags;
    lv_display_t *disp_drv;
} lvgl_port_display_ctx_t;
#define LVGL_PORT_DISP_TYPE_DSI 1
#define portENTER_CRITICAL(p) ((void)(p))
#define portEXIT_CRITICAL(p) ((void)(p))
#define pdMS_TO_TICKS(t) (t)
#define pdTRUE 1
#define LVGL_PORT_EVENT_DISPLAY 1
static bool timeout,wrong_completion;
static unsigned waits,flushes,touches,business_ticks,wakes,forbidden;
static int xSemaphoreTake(void *sem,unsigned ms){
    assert(ms==250);waits++;if(timeout)return 0;
    if(!wrong_completion)tab5_direct_color_done(sem);return 1;
}
static void lvgl_port_task_wake(int event,void *arg){assert(event==LVGL_PORT_EVENT_DISPLAY&&!arg);wakes++;}
#define esp_lcd_panel_draw_bitmap(...) (++forbidden, 0)
#define esp_cache_msync(...) (++forbidden, 0)
#define esp_lcd_dpi_panel_get_frame_buffer(...) (++forbidden, 0)
#include "display_idle_under_test.inc"
#include "display_freeze_under_test.inc"
#include "display_invalidate_under_test.inc"
static void flush(lv_display_t *d,const lv_area_t *a,uint8_t *p){assert(a&&p);flushes++;lv_display_flush_ready(d);}
static void touch(lv_indev_t *i,lv_indev_data_t *v){(void)i;touches++;v->state=LV_INDEV_STATE_RELEASED;v->point.x=3;v->point.y=3;}
static void business(lv_timer_t *timer){
    business_ticks++;
    lv_display_t *d=lv_timer_get_user_data(timer);
    lv_obj_set_style_bg_color(lv_display_get_screen_active(d),lv_color_hex(0x102030+business_ticks),0);
}
static void ticks(unsigned n){for(unsigned i=0;i<n;i++){lv_tick_inc(10);lv_timer_handler();}}
int main(void){
    lv_init();lv_display_t *d=lv_display_create(64,48);assert(d);
    static uint8_t pixels[64*8*2],saved[sizeof(pixels)];
    lv_display_set_color_format(d,LV_COLOR_FORMAT_RGB565);
    lv_display_set_buffers(d,pixels,NULL,sizeof(pixels),LV_DISPLAY_RENDER_MODE_PARTIAL);
    lv_display_set_flush_cb(d,flush);
    lvgl_port_display_ctx_t ctx={.disp_type=1,.disp_drv=d};
    ctx.trans_sem=&ctx.direct;lv_display_set_driver_data(d,&ctx);
    lv_display_add_event_cb(d,lvgl_port_display_invalidate_callback,LV_EVENT_INVALIDATE_AREA,&ctx);
    lv_display_add_event_cb(d,lvgl_port_display_invalidate_callback,LV_EVENT_REFR_REQUEST,&ctx);
    lv_indev_t *input=lv_indev_create();lv_indev_set_type(input,LV_INDEV_TYPE_POINTER);
    lv_indev_set_display(input,d);lv_indev_set_read_cb(input,touch);
    lv_timer_t *timer=lv_timer_create(business,10,d);
    for(unsigned pose=0;pose<4;pose++){
        lv_display_set_rotation(d,(lv_display_rotation_t)pose);
        ticks(8);assert(flushes);
        assert(lvgl_port_display_freeze(d,250));
        assert(ctx.direct.mode==TAB5_DIRECT_FROZEN&&lv_timer_get_paused(lv_display_get_refr_timer(d)));
        unsigned old_flushes=flushes,old_touches=touches,old_business=business_ticks;
        memcpy(saved,pixels,sizeof(pixels));
        for(unsigned step=0;step<20;step++){
            lv_display_send_event(d,LV_EVENT_REFR_REQUEST,NULL);
            assert(lv_timer_get_paused(lv_display_get_refr_timer(d)));ticks(1);
        }
        assert(flushes==old_flushes&&!memcmp(saved,pixels,sizeof(pixels)));
        assert(touches>old_touches&&business_ticks>old_business&&!forbidden);
        assert(!lvgl_port_display_freeze(d,250));
        assert(!tab5_direct_prepare(&ctx.direct,0)&&!tab5_direct_enter(&ctx.direct));
        assert(lvgl_port_display_unfreeze(d));ticks(8);
        assert(flushes>old_flushes&&ctx.direct.mode==TAB5_DIRECT_NORMAL&&!forbidden);
        assert(lvgl_port_display_unfreeze(d)); // idempotent, no new work
    }
    assert(tab5_direct_normal_submit(&ctx.direct));
    timeout=true;assert(!lvgl_port_display_freeze(d,250));
    assert(ctx.direct.mode==TAB5_DIRECT_NORMAL&&ctx.direct.normal_dma==TAB5_DIRECT_DMA_WAITING);
    timeout=false;wrong_completion=true;
    assert(!lvgl_port_display_freeze(d,250)&&ctx.direct.normal_dma==TAB5_DIRECT_DMA_WAITING);
    wrong_completion=false;assert(lvgl_port_display_freeze(d,250));
    assert(ctx.direct.normal_dma==TAB5_DIRECT_DMA_NONE&&waits==3);
    assert(lvgl_port_display_unfreeze(d));
    const tab5_direct_mode_t others[]={TAB5_DIRECT_CAMERA,TAB5_DIRECT_SWITCHING,TAB5_DIRECT_FAULT};
    for(unsigned i=0;i<3;i++){
        ctx.direct.mode=others[i];
        assert(!lvgl_port_display_freeze(d,250)&&!lvgl_port_display_unfreeze(d));
        assert(ctx.direct.mode==others[i]);
    }
    ctx.direct.mode=TAB5_DIRECT_NORMAL;ctx.flags.direct_mode=1;
    assert(!lvgl_port_display_freeze(d,250));ctx.flags.direct_mode=0;ctx.flags.full_refresh=1;
    assert(!lvgl_port_display_freeze(d,250));ctx.flags.full_refresh=0;
    assert(!lvgl_port_display_freeze(NULL,250)&&!lvgl_port_display_unfreeze(NULL)&&!forbidden);
    lv_timer_delete(timer);lv_indev_delete(input);lv_display_delete(d);lv_deinit();
    puts("DISPLAY_FREEZE_OK actual_port real_LVGL four_rotations no_LCD_or_cache_calls immutable_buffer touch_timers_continue pending_redraw_resumes DMA_timeout_late_completion_wrong_owner");
}
