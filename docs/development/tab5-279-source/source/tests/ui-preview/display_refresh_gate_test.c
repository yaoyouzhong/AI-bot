// Compile the production invalidation callback against real LVGL. CMake extracts
// the LAST occurrence of "static void lvgl_port_display_invalidate_callback("
// through EOF from esp_lvgl_port_disp.c into display_invalidate_under_test.inc.
// Only the RTOS lock and task wake are host facades; gate logic is not copied.
#undef NDEBUG
#include "lvgl.h"
#include "../../firmware/components/esp_lvgl_port/include/tab5_display_direct_state.h"
#include <assert.h>
#include <stdint.h>
#include <stdio.h>

typedef struct {
    unsigned direct_lock;
    tab5_display_direct_state_t direct;
    lv_display_t *disp_drv;
} lvgl_port_display_ctx_t;

#define portENTER_CRITICAL(lock) ((void)(lock))
#define portEXIT_CRITICAL(lock) ((void)(lock))
#define LVGL_PORT_EVENT_DISPLAY 1

static unsigned wakes, flushes, touch_reads, business_ticks;

static void lvgl_port_task_wake(int event, void *display)
{
    assert(event == LVGL_PORT_EVENT_DISPLAY);
    (void)display;
    wakes++;
}

#include "display_invalidate_under_test.inc"

static void flush(lv_display_t *display, const lv_area_t *area, uint8_t *pixels)
{
    assert(area && pixels);
    flushes++;
    lv_display_flush_ready(display);
}

static void read_touch(lv_indev_t *input, lv_indev_data_t *data)
{
    (void)input;
    touch_reads++;
    data->state = LV_INDEV_STATE_RELEASED;
    data->point.x = 4;
    data->point.y = 4;
}

static void business_tick(lv_timer_t *timer)
{
    business_ticks++;
    // Model the still-running UI business timer requesting new screen content.
    lv_display_t *display = lv_timer_get_user_data(timer);
    lv_obj_invalidate(lv_display_get_screen_active(display));
}

static void run_ticks(unsigned count)
{
    for (unsigned i = 0; i < count; i++) {
        lv_tick_inc(10);
        lv_timer_handler();
    }
}

int main(void)
{
    lv_init();
    lv_display_t *display = lv_display_create(64, 48);
    static uint8_t pixels[64 * 8 * 2];
    lv_display_set_color_format(display, LV_COLOR_FORMAT_RGB565);
    lv_display_set_buffers(display, pixels, NULL, sizeof(pixels), LV_DISPLAY_RENDER_MODE_PARTIAL);
    lv_display_set_flush_cb(display, flush);
    lvgl_port_display_ctx_t ctx = {.disp_drv = display};
    // lv_display_create already registered LVGL's default REFR_REQUEST handler.
    // Register the real port callback afterwards, exactly as the port does.
    lv_display_add_event_cb(display, lvgl_port_display_invalidate_callback, LV_EVENT_INVALIDATE_AREA, &ctx);
    lv_display_add_event_cb(display, lvgl_port_display_invalidate_callback, LV_EVENT_REFR_REQUEST, &ctx);
    lv_timer_t *refresh = lv_display_get_refr_timer(display);
    assert(refresh);
    lv_obj_t *screen = lv_display_get_screen_active(display);
    lv_obj_set_style_bg_color(screen, lv_color_hex(0x172b39), 0);
    lv_obj_set_style_bg_opa(screen, LV_OPA_COVER, 0);
    lv_refr_now(display); // Clear the creation-time invalid area first.

    unsigned before = flushes;
    lv_timer_pause(refresh);
    assert(lv_timer_get_paused(refresh));
    lv_obj_invalidate(screen);
    assert(!lv_timer_get_paused(refresh));
    run_ticks(8);
    assert(flushes > before);

    lv_indev_t *input = lv_indev_create();
    lv_indev_set_type(input, LV_INDEV_TYPE_POINTER);
    lv_indev_set_display(input, display);
    lv_indev_set_read_cb(input, read_touch);
    lv_timer_t *business = lv_timer_create(business_tick, 10, display);
    const tab5_direct_mode_t modes[] = {
        TAB5_DIRECT_CAMERA, TAB5_DIRECT_SWITCHING, TAB5_DIRECT_FAULT, TAB5_DIRECT_FROZEN,
    };
    for (unsigned i = 0; i < sizeof(modes) / sizeof(modes[0]); i++) {
        ctx.direct.mode = modes[i];
        lv_timer_pause(refresh);
        unsigned old_flushes = flushes, old_touch = touch_reads;
        unsigned old_business = business_ticks, old_wakes = wakes;
        lv_obj_set_style_bg_color(screen, lv_color_hex(0x304050 + i * 0x10101), 0);
        for (unsigned step = 0; step < 20; step++) {
            // Exercise the default resume on every iteration, even when the
            // invalid rectangle is already queued and LVGL coalesces it.
            lv_display_send_event(display, LV_EVENT_REFR_REQUEST, NULL);
            assert(lv_timer_get_paused(refresh));
            run_ticks(1);
            assert(lv_timer_get_paused(refresh));
        }
        assert(flushes == old_flushes);
        assert(wakes == old_wakes);
        assert(touch_reads > old_touch);
        assert(business_ticks > old_business);
    }

    // Recover while the refresh timer is paused and invalidations accumulated.
    // This is the production end path's public LVGL sequence after ownership
    // has been confirmed; the callback must now permit an actual new flush.
    ctx.direct.mode = TAB5_DIRECT_NORMAL;
    before = flushes;
    lv_obj_invalidate(screen);
    lv_obj_invalidate(lv_display_get_layer_top(display));
    lv_obj_invalidate(lv_display_get_layer_sys(display));
    lv_timer_resume(refresh);
    assert(!lv_timer_get_paused(refresh));
    run_ticks(8);
    assert(flushes > before);
    assert(wakes > 0);

    lv_timer_delete(business);
    lv_indev_delete(input);
    lv_display_delete(display);
    printf("DISPLAY_REFRESH_GATE_PASS: normal resume, camera/switching/fault/frozen gating, touch=%u business=%u, restored flush\n",
           touch_reads, business_ticks);
    return 0;
}
