// Exercise LVGL's real double-buffer ownership contract with early and late
// DMA completions. Windows semaphores stand in for the FreeRTOS semaphore;
// this is a host contract test, not an ESP32 DMA/interrupt hardware test.
#undef NDEBUG
#include "lvgl.h"
#include <windows.h>
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>

static HANDLE completed, submitted, worker;
static volatile LONG in_flight, stopping;
static uint8_t *source;
static size_t source_size;
static uint32_t source_hash;
static unsigned transfers, waits, early, late;
static uint8_t rotated[64*8*2]; // One shared DMA source, as with TAB5 rotation.

static uint32_t hash(const uint8_t *bytes,size_t size) {
    uint32_t value=2166136261u;
    for(size_t i=0;i<size;i++)value=(value^bytes[i])*16777619u;
    return value;
}
static void complete_transfer(void) {
    // Nothing may reuse the shared source before the driver has finished.
    assert(hash(source,source_size)==source_hash);
    assert(InterlockedExchange(&in_flight,0)==1);
    assert(ReleaseSemaphore(completed,1,NULL));
}
static DWORD WINAPI dma_worker(void *unused) {
    (void)unused;
    for(;;) {
        assert(WaitForSingleObject(submitted,5000)==WAIT_OBJECT_0);
        if(InterlockedCompareExchange(&stopping,0,0))return 0;
        Sleep(2);complete_transfer();
    }
}
static void flush(lv_display_t *display,const lv_area_t *area,uint8_t *pixels) {
    (void)display;
    assert(InterlockedCompareExchange(&in_flight,0,0)==0);
    source_size=lv_area_get_size(area)*2;
    assert(source_size<=sizeof(rotated));
    memcpy(rotated,pixels,source_size);source=rotated;
    source_hash=hash(source,source_size);
    InterlockedExchange(&in_flight,1);
    transfers++;
    if(transfers%3) {
        // Completion arrives before LVGL calls flush_wait_cb. It must not
        // clear flushing as well, or the token leaks into the next transfer.
        early++;complete_transfer();
    } else {
        late++;assert(ReleaseSemaphore(submitted,1,NULL));
    }
}
static void wait_flush(lv_display_t *display) {
    (void)display;
    assert(WaitForSingleObject(completed,5000)==WAIT_OBJECT_0);
    assert(InterlockedCompareExchange(&in_flight,0,0)==0);
    waits++;
}
int main(void) {
    completed=CreateSemaphoreW(NULL,0,1,NULL);
    submitted=CreateSemaphoreW(NULL,0,1,NULL);
    assert(completed&&submitted);
    worker=CreateThread(NULL,0,dma_worker,NULL,0,NULL);assert(worker);
    lv_init();
    lv_display_t *display=lv_display_create(64,48);
    static uint8_t first[64*8*2],second[64*8*2];
    lv_display_set_color_format(display,LV_COLOR_FORMAT_RGB565);
    lv_display_set_buffers(display,first,second,sizeof(first),LV_DISPLAY_RENDER_MODE_PARTIAL);
    lv_display_set_flush_cb(display,flush);
    lv_display_set_flush_wait_cb(display,wait_flush);
    for(unsigned frame=0;frame<30;frame++) {
        lv_obj_set_style_bg_color(lv_screen_active(),lv_color_hex(0x173251+frame*1319),0);
        lv_obj_invalidate(lv_screen_active());lv_refr_now(display);
    }
    // The last DMA may finish after the final lv_refr_now; consume it exactly
    // once, just as the next redraw would, before destroying the test display.
    if(waits<transfers)wait_flush(display);
    assert(transfers>=180&&waits==transfers&&early&&late);
    assert(WaitForSingleObject(completed,0)==WAIT_TIMEOUT);
    InterlockedExchange(&stopping,1);assert(ReleaseSemaphore(submitted,1,NULL));
    assert(WaitForSingleObject(worker,5000)==WAIT_OBJECT_0);
    CloseHandle(worker);CloseHandle(submitted);CloseHandle(completed);
    lv_display_delete(display);
    printf("DISPLAY_FLUSH_WAIT_PASS: %u transfers, %u early, %u delayed; no stale token or buffer reuse\n",transfers,early,late);
    return 0;
}
