#ifdef NDEBUG
#undef NDEBUG
#endif
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include "../../firmware/components/esp_lvgl_port/include/tab5_display_direct_state.h"
#define portENTER_CRITICAL(p) ((void)(p))
#define portEXIT_CRITICAL(p) ((void)(p))
typedef struct { bool resumed; } lv_timer_t;
typedef struct {
    int direct_lock;
    tab5_display_direct_state_t direct;
    void *camera_refresh_sem;
    uint16_t *camera_fbs[2];
    unsigned native_width, native_height;
} lvgl_port_display_ctx_t;
typedef struct { lvgl_port_display_ctx_t *ctx; lv_timer_t timer; } lv_display_t;
static unsigned invalidations, selections;
static bool select_fails;
static uint16_t expected[32];
static void *lv_display_get_driver_data(lv_display_t *d) { return d->ctx; }
static void *lv_display_get_screen_active(lv_display_t *d) { return d; }
static void *lv_display_get_layer_top(lv_display_t *d) { return d; }
static void *lv_display_get_layer_sys(lv_display_t *d) { return d; }
static void lv_obj_invalidate(void *obj) { assert(obj); invalidations++; }
static lv_timer_t *lv_display_get_refr_timer(lv_display_t *d) { return &d->timer; }
static void lv_timer_resume(lv_timer_t *timer) { timer->resumed = true; }
static bool lvgl_port_camera_select(lvgl_port_display_ctx_t *ctx, unsigned index, uint32_t timeout)
{
    assert(index == 0 && timeout == 250);
    assert(memcmp(ctx->camera_fbs[0], expected, sizeof(expected)) == 0);
    selections++;
    if (select_fails) { tab5_direct_fault(&ctx->direct); return false; }
    assert(tab5_direct_prepare(&ctx->direct, index));
    tab5_direct_arm(&ctx->direct);
    tab5_direct_refresh(&ctx->direct);
    assert(!tab5_direct_complete(&ctx->direct));
    tab5_direct_refresh(&ctx->direct);
    assert(tab5_direct_complete(&ctx->direct));
    return true;
}
#include "camera_end_under_test.inc"
static void run_case(tab5_direct_mode_t mode, unsigned front, bool fail)
{
    uint16_t fb0[32], fb1[32];
    for (unsigned i=0;i<32;i++) { fb0[i]=(uint16_t)(100+i); fb1[i]=(uint16_t)(200+i); }
    // fb0 represents a cancelled partial composition when fb1 is front.
    memcpy(expected, mode == TAB5_DIRECT_CAMERA && front == 1 ? fb1 : fb0, sizeof(expected));
    lvgl_port_display_ctx_t ctx = {.direct={.mode=mode,.front=front},
        .camera_refresh_sem=&ctx, .camera_fbs={fb0,fb1}, .native_width=8,.native_height=4};
    lv_display_t display = {.ctx=&ctx};
    invalidations=selections=0; select_fails=fail;
    bool result=lvgl_port_camera_end(&display,250);
    assert(result == (mode == TAB5_DIRECT_NORMAL || !fail));
    assert(memcmp(fb0,expected,sizeof(expected)) == 0);
    for(unsigned i=0;i<32;i++) assert(fb1[i] == 200+i);
    if(mode == TAB5_DIRECT_NORMAL) { assert(selections==0 && invalidations==0); }
    else if(fail) { assert(ctx.direct.mode==TAB5_DIRECT_FAULT && !display.timer.resumed && invalidations==0); }
    else { assert(ctx.direct.mode==TAB5_DIRECT_NORMAL && ctx.direct.front==0 && display.timer.resumed && invalidations==3); }
}
int main(void)
{
    run_case(TAB5_DIRECT_NORMAL,0,false);
    run_case(TAB5_DIRECT_CAMERA,0,false);
    run_case(TAB5_DIRECT_CAMERA,1,false);
    run_case(TAB5_DIRECT_CAMERA,1,true);
    run_case(TAB5_DIRECT_FAULT,1,false);
    run_case(TAB5_DIRECT_FAULT,1,true);
    puts("CAMERA_END_PASS: 6 cases; actual exit body, cancelled back buffer, confirmed front, fault and timeout ownership");
    return 0;
}
