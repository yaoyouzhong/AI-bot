#ifdef NDEBUG
#undef NDEBUG
#endif
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>

typedef int esp_err_t;
enum {ESP_OK=0,ESP_ERR_INVALID_ARG=258};
enum {ESP_CACHE_MSYNC_FLAG_DIR_C2M=1,ESP_CACHE_MSYNC_FLAG_UNALIGNED=2,ESP_CACHE_MSYNC_FLAG_DIR_M2C=4};
#define ESP_GOTO_ON_FALSE(c,r,label,...) do{if(!(c)){ret=(r);goto label;}}while(0)
typedef struct {uint32_t width,height;} jpeg_encode_cfg_t;
typedef struct {uint32_t origin_h,origin_v;} header_t;
typedef struct {header_t *header_info;uint32_t bytes_per_pixel;} encoder_t;
static void *allocation;
static size_t capacity,sync_size;
static unsigned sync_count,dma_count,yields,fail_call;
static int expected_flags;
static bool outside_input;
static esp_err_t esp_cache_msync(void *address,size_t size,int flags) {
    assert(address==(uint8_t *)allocation+sync_size);
    assert(flags==expected_flags);
    assert(size<=16384);sync_count++;
    if(sync_count==fail_call)return 511;
    sync_size+=size;outside_input=sync_size>capacity;
    return ESP_OK;
}
static void vTaskDelay(unsigned ticks){assert(ticks==1);yields++;}
#include "jpeg_image_sync_under_test.inc"
static esp_err_t process(uint32_t width,uint32_t height,uint32_t bit_depth,uint32_t inbuf_size) {
    jpeg_encode_cfg_t config={width,height};const jpeg_encode_cfg_t *encode_cfg=&config;
    header_t header={width,height};encoder_t encoder={&header,bit_depth};encoder_t *encoder_engine=&encoder;
    void *raw_buffer=allocation;esp_err_t ret=ESP_OK;
    #include "jpeg_input_range_under_test.inc"
    // Hardware TX descriptor programming follows input validation in the SDK.
    dma_count++;
    #include "jpeg_input_sync_under_test.inc"
err2:
    return ret;
}
static void reset(uint32_t size) {
    capacity=size;sync_size=0;sync_count=0;dma_count=0;yields=0;outside_input=false;fail_call=0;
    expected_flags=ESP_CACHE_MSYNC_FLAG_DIR_C2M|ESP_CACHE_MSYNC_FLAG_UNALIGNED;
}
int main(void) {
    // Mock only the cache boundary: no photo data or enormous allocation needed.
    allocation=malloc(4*1024*1024);assert(allocation);
    reset(1280u*720u*2u);
    esp_err_t result=process(1280,720,16,(uint32_t)capacity);
    if(result!=ESP_OK||outside_input||sync_size!=capacity) {
        fprintf(stderr,"JPEG_CACHE_RANGE_FAIL: RGB565 allocation=%zu sync=%zu excess=%zu\n",capacity,sync_size,sync_size>capacity?sync_size-capacity:0);
        free(allocation);return 1;
    }
    unsigned cases=0;
    // Both photo orientations, all four pinned encoder formats, and caller padding.
    const uint32_t depths[]={16,24,8,16};
    for(unsigned d=0;d<4;d++)for(unsigned portrait=0;portrait<2;portrait++)for(unsigned pad=0;pad<2;pad++) {
        uint32_t w=portrait?720:1280,h=portrait?1280:720;
        uint32_t required=w*h*depths[d]/8;
        reset(required+pad*128);
        assert(process(w,h,depths[d],(uint32_t)capacity)==ESP_OK);
        assert(sync_count==(capacity+16383)/16384&&yields+1==sync_count&&dma_count==1&&!outside_input&&sync_size==capacity);
        reset(required-1);
        assert(process(w,h,depths[d],(uint32_t)capacity)==ESP_ERR_INVALID_ARG);
        assert(sync_count==0&&dma_count==0);
        cases+=2;
    }
    const uint32_t invalid[][4]={{0,720,16,1843200},{1280,0,16,1843200},
        {1280,720,0,1843200},{1280,720,16,0},{65536,65536,16,UINT32_MAX},
        {UINT32_MAX,UINT32_MAX,24,UINT32_MAX},{1,1,12,2}};
    for(unsigned i=0;i<sizeof(invalid)/sizeof(invalid[0]);i++) {
        reset(invalid[i][3]);
        assert(process(invalid[i][0],invalid[i][1],invalid[i][2],invalid[i][3])==ESP_ERR_INVALID_ARG);
        assert(sync_count==0&&dma_count==0);cases++;
    }
    // Both cache directions preserve flags and byte boundaries, including
    // short tails and the exact failing chunk. No later range may be touched.
    const uint32_t sizes[]={0,128,16384,16512,2*1024*1024};
    for(unsigned direction=0;direction<2;direction++)for(unsigned i=0;i<5;i++){
        reset(sizes[i]);expected_flags=direction?ESP_CACHE_MSYNC_FLAG_DIR_M2C:3;
        assert(tab5_jpeg_sync_image(allocation,capacity,expected_flags)==ESP_OK);
        assert(sync_size==capacity&&!outside_input&&sync_count==(capacity+16383)/16384);
        assert(yields==(sync_count?sync_count-1:0));cases++;
    }
    for(unsigned direction=0;direction<2;direction++){
        reset(2*1024*1024);expected_flags=direction?ESP_CACHE_MSYNC_FLAG_DIR_M2C:3;fail_call=2;
        assert(tab5_jpeg_sync_image(allocation,capacity,expected_flags)==511);
        assert(sync_count==2&&sync_size==16384&&yields==1);cases++;
    }
    free(allocation);
    printf("JPEG_CACHE_RANGE_PASS: %u cases; actual SDK input bounds and bidirectional 16 KiB sync; failures stop immediately\n",cases);
    return 0;
}
