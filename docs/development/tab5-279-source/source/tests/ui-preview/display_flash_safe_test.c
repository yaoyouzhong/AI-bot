#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdio.h>
#define IRAM_ATTR
#include "../../firmware/components/esp_lvgl_port/include/tab5_display_direct_state.h"
typedef void *esp_lcd_panel_handle_t;
typedef void esp_lcd_dpi_panel_event_data_t;
typedef int BaseType_t;
enum {pdFALSE=0,pdTRUE=1};
typedef struct {int direct_lock;tab5_display_direct_state_t direct;void *camera_refresh_sem;} lvgl_port_display_ctx_t;
static int locked,signals;
#define portENTER_CRITICAL_ISR(p) do {assert(!locked);locked=1;} while(0)
#define portEXIT_CRITICAL_ISR(p) do {assert(locked);locked=0;} while(0)
static void xSemaphoreGiveFromISR(void *sem,BaseType_t *wake) {assert(sem&&!locked);signals++;*wake=pdTRUE;}
// Compile the real ISR body. There are deliberately no LVGL helpers available:
// it must operate only on its internal context while flash cache is unavailable.
#include "display_flash_safe_under_test.inc"
int main(void) {
    lvgl_port_display_ctx_t ctx={.camera_refresh_sem=(void*)1};
    for(int i=0;i<10000;i++)assert(!lvgl_port_camera_refresh_callback(NULL,NULL,&ctx));
    assert(ctx.direct.refresh_sequence==10000&&signals==0);
    assert(tab5_direct_enter(&ctx.direct));tab5_direct_arm(&ctx.direct);
    assert(lvgl_port_camera_refresh_callback(NULL,NULL,&ctx));
    assert(!tab5_direct_complete(&ctx.direct));
    assert(lvgl_port_camera_refresh_callback(NULL,NULL,&ctx));
    assert(tab5_direct_complete(&ctx.direct));
    assert(signals==2&&tab5_direct_leave(&ctx.direct));
    puts("DISPLAY_FLASH_SAFE_PASS: continuous scanout and two-refresh ownership fence");
}
