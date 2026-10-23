#ifdef NDEBUG
#undef NDEBUG
#endif
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdatomic.h>
#include "../../firmware/main/photo_attachment.h"
#include "tab5_jpeg.h"
#include "../../firmware/main/photo_display_probe.h"
typedef int esp_err_t;
enum {ESP_OK=0,ESP_ERR_NO_MEM=257,ESP_ERR_INVALID_STATE=259,ESP_ERR_INVALID_SIZE=260};
static atomic_int phase;
static uint8_t *data;
static size_t data_size;
static char error[96];
static int fault,allocations,encodes,previews,attempts;
static bool leased;
void photo_display_probe_mark(unsigned stage){assert(leased&&(stage==PHOTO_DISPLAY_PROBE_PREVIEW||stage==PHOTO_DISPLAY_PROBE_ENCODE));}
static unsigned photo_width=1280,photo_height=720;
static uint8_t still[1280*720*2];
static size_t allocation_limit=PHOTO_MAX_BYTES,allocated_capacity;
#define MALLOC_CAP_SPIRAM 1
#define MALLOC_CAP_8BIT 2
#define MALLOC_CAP_INTERNAL 4
static unsigned largest_calls,free_calls;
static unsigned heap_caps_get_largest_free_block(unsigned caps){(void)caps;largest_calls++;return 999;}
static unsigned heap_caps_get_free_size(unsigned caps){assert(caps==(MALLOC_CAP_INTERNAL|MALLOC_CAP_8BIT));free_calls++;return 12345;}
#include "photo_codec_diagnostic_under_test.inc"
static void camera_preview_dimensions(unsigned *w,unsigned *h){*w=photo_width;*h=photo_height;}
static void *photo_output_alloc(size_t n,size_t *capacity){
    assert(leased&&n>=256*1024&&n<=PHOTO_MAX_BYTES);attempts++;
    if(fault==3||n>allocation_limit)return NULL;
    *capacity=n;allocated_capacity=n;allocations++;return malloc(n);
}
static void tracked_free(void *p){if(p){allocations--;free(p);}}
static const uint8_t *camera_preview_acquire(void){assert(!leased);if(fault==2)return NULL;leased=true;return still;}
static void camera_preview_release(const uint8_t *p){if(p){assert(p==still&&leased);leased=false;}}
static void *heap_caps_realloc(void *p,size_t n,int caps){
    assert(!leased&&n==123456&&caps==3);if(fault==9)return NULL;return realloc(p,n);
}
static void make_preview(const uint16_t *p,unsigned w,unsigned h,unsigned stride){assert(p&&leased&&w==photo_width&&h==photo_height&&stride==w);previews++;}
static void photo_encode_yield(void *p){assert(!p&&leased);}
tab5_jpeg_result tab5_jpeg_encode(const uint16_t *p,size_t n,unsigned w,unsigned h,
    uint8_t *out,size_t capacity,uint32_t *written,void (*yield)(void *),void *ctx){
    assert(p==(const uint16_t *)still&&leased&&n==sizeof still&&out&&capacity==allocated_capacity);
    assert(w==photo_width&&h==photo_height&&yield==photo_encode_yield&&!ctx);yield(ctx);encodes++;
    *written=fault==6?0:fault==8?capacity+1:123456;
    memset(out,0x5a,*written>capacity?capacity:*written);
    return fault==4?TAB5_JPEG_MEMORY:fault==5?TAB5_JPEG_FULL:fault==10?TAB5_JPEG_FAILED:TAB5_JPEG_OK;
}
#define free tracked_free
#include "photo_encode_under_test.inc"
#undef free
static void run_case(int failure,int step,int code){
    fault=failure;encodes=previews=attempts=0;atomic_store(&phase,1);assert(!data&&!allocations&&!leased);
    largest_calls=free_calls=0;atomic_store(&codec_largest,777);atomic_store(&codec_psram,888);
    bool ok=prepare_camera();assert(ok==(failure==0||failure==9));assert(!leased);
    assert(atomic_load(&codec_stage)==step&&atomic_load(&codec_error)==code);
    assert(!largest_calls&&free_calls&&atomic_load(&codec_internal)==12345);
    assert(atomic_load(&codec_largest)==0&&atomic_load(&codec_psram)==0);
    if(ok){assert(data&&data_size==123456&&allocations==1);for(size_t i=0;i<data_size;i++)assert(data[i]==0x5a);tracked_free(data);data=NULL;}
    else {assert(!data&&data_size==0&&!allocations&&atomic_load(&phase)==3);assert(!strstr(error,"重拍"));}
    assert(encodes==(failure!=2&&failure!=3));
}
int main(void){
    run_case(2,2,ESP_ERR_INVALID_STATE);run_case(3,3,ESP_ERR_NO_MEM);
    run_case(4,5,ESP_ERR_NO_MEM);run_case(5,5,ESP_ERR_INVALID_SIZE);
    run_case(6,6,ESP_ERR_INVALID_SIZE);run_case(8,6,ESP_ERR_INVALID_SIZE);
    run_case(10,5,ESP_ERR_INVALID_STATE);run_case(0,7,ESP_OK);run_case(9,7,ESP_OK);
    allocation_limit=1245184;run_case(0,7,ESP_OK);assert(attempts==2&&allocated_capacity==1024*1024);
    allocation_limit=256*1024;run_case(0,7,ESP_OK);assert(attempts==4);
    allocation_limit=128*1024;run_case(3,3,ESP_ERR_NO_MEM);assert(attempts==4);
    allocation_limit=PHOTO_MAX_BYTES;
    for(unsigned i=0;i<128;i++){photo_width=i%2?720:1280;photo_height=i%2?1280:720;run_case(5,5,ESP_ERR_INVALID_SIZE);run_case(0,7,ESP_OK);}
    puts("PHOTO_ENCODE_PASS real camera preparation; software-only immutable lease, no LCD lock or DMA; 128 reopen/retry pairs, bounded output, failures and shrink recovery; actual codec diagnostics make zero heap walks");
}
