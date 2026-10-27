#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include "../../firmware/components/esp_lvgl_port/include/tab5_display_direct_state.h"
#define portENTER_CRITICAL(p) ((void)(p))
#define portEXIT_CRITICAL(p) ((void)(p))
#define pdMS_TO_TICKS(t) (t)
#define pdTRUE 1
#define LVGL_PORT_DISP_TYPE_DSI 1
typedef struct{int disp_type,direct_lock;void *trans_sem;tab5_display_direct_state_t direct;}lvgl_port_display_ctx_t;
typedef struct{lvgl_port_display_ctx_t *ctx;}lv_display_t;
static bool timeout,wrong_completion;static unsigned ready,waits;
static void *lv_display_get_driver_data(lv_display_t *d){return d->ctx;}
static int xSemaphoreTake(void *sem,unsigned ms){assert(ms==300);waits++;if(timeout)return 0;if(!wrong_completion)tab5_direct_color_done(sem);return 1;}
static void lv_disp_flush_ready(lv_display_t *d){assert(d);ready++;}
#include "display_idle_under_test.inc"
int main(void){
    lvgl_port_display_ctx_t ctx={.disp_type=1};ctx.trans_sem=&ctx.direct;lv_display_t d={&ctx};
    assert(lvgl_port_display_wait_idle(&d,300)&&!ready&&!waits);
    assert(tab5_direct_normal_submit(&ctx.direct));timeout=true;
    assert(!lvgl_port_display_wait_idle(&d,300)&&ctx.direct.normal_dma!=TAB5_DIRECT_DMA_NONE&&!ready);
    timeout=false;wrong_completion=true;assert(!lvgl_port_display_wait_idle(&d,300)&&!ready);
    wrong_completion=false;assert(lvgl_port_display_wait_idle(&d,300)&&ready==1&&ctx.direct.normal_dma==TAB5_DIRECT_DMA_NONE);
    assert(lvgl_port_display_wait_idle(&d,300)&&ready==1);
    ctx.direct.mode=TAB5_DIRECT_CAMERA;assert(!lvgl_port_display_wait_idle(&d,300));
    ctx.direct.mode=TAB5_DIRECT_FAULT;assert(!lvgl_port_display_wait_idle(&d,300));
    assert(!lvgl_port_display_wait_idle(NULL,300));
    puts("DISPLAY_IDLE_OK actual drain; idle/in-flight/timeout/late completion/camera/fault; no framebuffer selection or clearing");return 0;
}
