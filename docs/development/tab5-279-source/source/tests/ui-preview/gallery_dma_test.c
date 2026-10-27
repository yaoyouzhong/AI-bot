#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdatomic.h>
#ifdef _WIN32
#include <windows.h>
#endif
typedef int esp_err_t;
static atomic_int diagnostic_stage,diagnostic_error;
static atomic_uint diagnostic_allocations;
#include "gallery_remote.h"
static gallery_image job;
static atomic_int decode_status[4];
#include <stdlib.h>
#include <string.h>
enum { ESP_OK=0, ESP_ERR_NO_MEM=257, ESP_CACHE_MSYNC_FLAG_DIR_C2M=1, ESP_CACHE_MSYNC_FLAG_INVALIDATE=2,
    JPEG_DECODE_OUT_FORMAT_RGB565=0, JPEG_DEC_RGB_ELEMENT_ORDER_BGR=0, JPEG_YUV_RGB_CONV_STD_BT601=0 };
typedef struct {int timeout_ms;} jpeg_decode_engine_cfg_t;
typedef struct {int output_format,rgb_order,conv_std;} jpeg_decode_cfg_t;
static bool dirty,omit_sync,fail_alloc,fail_sync,fail_decode,short_write;
static unsigned decodes;
static uint8_t *borrowed_frame;
static uint8_t *camera_gallery_buffer_take(void){uint8_t *p=borrowed_frame;borrowed_frame=NULL;return p;}
static void *jpeg_alloc_decoder_mem(size_t bytes,const void *config,size_t *capacity) {
    (void)config;*capacity=bytes;dirty=true;return fail_alloc?NULL:calloc(1,bytes);
}
static int esp_cache_msync(void *pixels,size_t capacity,int flags) {
    assert(pixels&&capacity==1280*720*2);
    assert(flags==(ESP_CACHE_MSYNC_FLAG_DIR_C2M|ESP_CACHE_MSYNC_FLAG_INVALIDATE));
    if(fail_sync)return -1;
    if(!omit_sync)dirty=false;
    return ESP_OK;
}
static int jpeg_new_decoder_engine(const jpeg_decode_engine_cfg_t *config,void **codec) {
    assert(config->timeout_ms==3000);*codec=(void *)1;return ESP_OK;
}
static int jpeg_decoder_process(void *codec,const jpeg_decode_cfg_t *format,void *jpeg,int length,
                                uint8_t *pixels,size_t capacity,uint32_t *written) {
    (void)codec;(void)format;(void)jpeg;(void)length;decodes++;
    if(fail_decode)return -1;
    memset(pixels,0xA5,capacity);
    // Model dirty calloc tail lines evicted while DMA has already written them.
    // Invalidating AFTER DMA cannot undo memory already overwritten by eviction.
    if(dirty)memset(pixels+capacity-65536,0,65536);
    dirty=false;*written=(uint32_t)capacity-(short_write?2:0);return ESP_OK;
}
static uint8_t *decode_with_spare(uint8_t *spare) {
    uint8_t *pixels=spare,*jpeg=NULL;size_t capacity=0;int output=0,length=0;void *codec=NULL;
    // A foreground request clears its own outcome before entering this body.
    atomic_store(&decode_status[job.category*2+job.portrait],GALLERY_DECODE_OK);
#define record(counter,value) atomic_store(&(counter),(value))
#ifdef TAB5_GALLERY_BASELINE
#include "gallery_decode_baseline.inc"
#else
#include "gallery_decode_under_test.inc"
#endif
#undef record
    return pixels;
done:
    gallery_recycle(pixels);return NULL;
}
static uint8_t *decode(void){return decode_with_spare(NULL);}
int main(void) {
#ifdef _WIN32
    SetErrorMode(SEM_FAILCRITICALERRORS|SEM_NOGPFAULTERRORBOX);
    _set_error_mode(_OUT_TO_STDERR);_set_abort_behavior(0,_WRITE_ABORT_MSG|_CALL_REPORTFAULT);
#endif
    omit_sync=true;uint8_t *pixels=decode();assert(pixels);
    assert(pixels[0]==0xA5&&pixels[1280*720*2-1]==0);free(pixels);
    omit_sync=false;
    for(int page=0;page<12;page++) {
        pixels=decode();assert(pixels);
        for(size_t i=0;i<1280*720*2;i++)assert(pixels[i]==0xA5);
        free(pixels);
    }
    // Negative control: .250 cannot decode a second layout when the general
    // heap has no contiguous full frame. A reserved loan must complete without
    // relaxing that allocation failure or changing any output pixels.
    fail_alloc=true;
    borrowed_frame=calloc(1,1280*720*2);assert(borrowed_frame);
    uint8_t *loan=borrowed_frame;unsigned allocations=atomic_load(&diagnostic_allocations);
    pixels=decode();assert(pixels==loan&&!borrowed_frame);
    assert(atomic_load(&diagnostic_allocations)==allocations);
    for(size_t i=0;i<1280*720*2;i++)assert(pixels[i]==0xA5);
    free(pixels);fail_alloc=false;
    puts("GALLERY_RESERVED_FRAME_OK fragmented-heap decode uses reserved frame; all 921600 pixels intact");
    unsigned before=decodes;
    fail_alloc=true;assert(!decode());fail_alloc=false;
    assert(atomic_load(&decode_status[0])==GALLERY_DECODE_NO_MEMORY);
    // Even when another 1.8 MiB output allocation cannot succeed, a detached
    // decoder buffer is reused, synchronized and completely replaced by DMA.
    uint8_t *spare=calloc(1,1280*720*2);assert(spare);dirty=true;fail_alloc=true;
    pixels=decode_with_spare(spare);assert(pixels==spare&&pixels[0]==0xA5&&pixels[1280*720*2-1]==0xA5);free(pixels);
    fail_alloc=false;before=decodes;
    fail_sync=true;assert(!decode());fail_sync=false;assert(decodes==before);
    assert(atomic_load(&decode_status[0])==GALLERY_DECODE_FAILED);
    fail_decode=true;assert(!decode());fail_decode=false;
    assert(atomic_load(&decode_status[0])==GALLERY_DECODE_FAILED);
    short_write=true;assert(!decode());short_write=false;
    assert(atomic_load(&decode_status[0])==GALLERY_DECODE_FAILED);
    uint8_t *retained=gallery_take_buffer();assert(retained);
    gallery_recycle(retained);fail_alloc=true;
    for(int step=0;step<12;step++){
        uint8_t *buffer=gallery_take_buffer();assert(buffer==retained);
        fail_decode=step%3==0;
        pixels=decode_with_spare(buffer);
        if(fail_decode){assert(!pixels);assert(atomic_load(&diagnostic_stage)==9);}
        else {assert(pixels==retained&&pixels[1280*720*2-1]==0xA5);gallery_recycle(pixels);}
    }
    fail_decode=false;fail_alloc=false;
    puts("GALLERY_RETRY_BUFFER_OK failed decodes retain the same buffer; later retries need no allocation");
    free(gallery_take_buffer());
    puts("GALLERY_DMA_OK dirty-tail negative control, 12 complete frames, allocation/sync/decode/length failure rejection");
}
