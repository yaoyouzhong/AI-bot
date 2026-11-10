#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdatomic.h>
#include <stdlib.h>
#include <stdio.h>
#include "../../firmware/main/gallery_http_cache.h"
static bool held;
static int device_mutex;
#define portMAX_DELAY 0
#define pdMS_TO_TICKS(ms) (ms)
static atomic_bool working,prefetching,cancel_prefetch,keep_running;
static gallery_http_lease library_http_idle;
static unsigned closed,cleared,released,delays;
static int jpeg_cache;
static int64_t now_us;
static bool complete_worker;
static uint8_t *spare;
typedef struct {uint8_t *pixels;} gallery_image;
static gallery_image result;
static void xSemaphoreTake(int m,int wait){(void)m;(void)wait;assert(!held);held=true;}
static void xSemaphoreGive(int m){(void)m;assert(held);held=false;}
static void esp_http_client_cleanup(void *client){assert(client&&!held);closed++;}
static void gallery_jpeg_clear(int *cache){assert(cache==&jpeg_cache&&held);cleared++;}
static uint8_t *gallery_take_buffer(void){uint8_t *p=spare;spare=NULL;return p;}
static void tracked_free(void *p){assert(p&&!held);released++;free(p);}
static void gallery_release_buffer(uint8_t *p){tracked_free(p);}
static bool pool_idle=true;
static bool camera_gallery_buffers_idle(void){return pool_idle;}
static int64_t esp_timer_get_time(void){return now_us;}
static void vTaskDelay(unsigned ms){
    assert(!held);now_us+=ms*1000;delays++;
    if(complete_worker&&delays==3){working=false;prefetching=false;spare=malloc(32);result.pixels=malloc(32);}
}
#define free tracked_free
#include "gallery_camera_prepare_under_test.inc"
#undef free
#include "camera_wait_for_gallery_under_test.inc"
int main(void) {
#ifdef _WIN32
    _set_error_mode(_OUT_TO_STDERR);
    _set_abort_behavior(0,_WRITE_ABORT_MSG|_CALL_REPORTFAULT);
#endif
    // Camera must wait for the active owners to unwind before using their RAM.
    working=true;prefetching=true;keep_running=true;complete_worker=true;
    library_http_idle=(gallery_http_lease){.client=(void*)1};
    assert(camera_wait_for_gallery());assert(delays==3&&closed==1&&released==2&&cancel_prefetch&&!spare&&!result.pixels&&!held);
    assert(gallery_camera_prepare()&&closed==1&&released==2); // No duplicate close/free.
    // A stuck network worker gets a bounded busy outcome, never a false memory
    // success or a forced free of buffers still owned by that worker.
    complete_worker=false;working=true;prefetching=false;delays=0;now_us=0;
    result.pixels=malloc(32);uint8_t *owned=result.pixels;
    assert(!camera_wait_for_gallery()&&now_us==4000000&&delays==200&&released==2&&result.pixels==owned);
    keep_running=false;delays=0;assert(!camera_wait_for_gallery()&&!delays&&!held);
    working=false;keep_running=true;assert(camera_wait_for_gallery()&&released==3&&!result.pixels);
    pool_idle=false;assert(!gallery_camera_prepare());
    pool_idle=true;assert(gallery_camera_prepare());
    puts("CAMERA_MEMORY_HANDOFF_OK: worker drain, pending decoded result and spare reclamation, idle HTTP outside mutex, timeout, cancellation and reopen");
}
