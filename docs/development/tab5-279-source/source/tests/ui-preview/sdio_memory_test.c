#ifdef NDEBUG
#undef NDEBUG
#endif
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <malloc.h>
#include <setjmp.h>

#define CONFIG_SPIRAM 1
#define SOC_SDMMC_PSRAM_DMA_CAPABLE 1
#define H_SDIO_RX_BLOCK_ONLY_XFER 1
#define ESP_BLOCK_SIZE 512
#define ESP_OK 0
#define ESP_ERR_INVALID_ARG 1
#define ESP_ERR_NO_MEM 2
#define MALLOC_CAP_SPIRAM 1
#define MALLOC_CAP_8BIT 2
#define MALLOC_CAP_DMA 4
#define ESP_LOGD(...) ((void)0)
#define ALIGN_UP_BY(n,a) (((n)+(a)-1)&~((a)-1))
#define ESP_RETURN_ON_FALSE_ISR(test,err,tag,...) do {if(!(test))return (err);}while(0)
typedef int esp_err_t;
typedef struct {int extra_heap_caps;size_t dma_alignment_bytes;} esp_dma_mem_info_t;
static size_t internal_available=32768,external_available=1024*1024;
static size_t last_alignment,last_bytes,cache_line=128;
static int last_caps,allocations;
static esp_err_t esp_cache_get_alignment(int caps,size_t *out) {*out=(caps&MALLOC_CAP_SPIRAM)?cache_line:64;return ESP_OK;}
static size_t hal_utils_calc_lcm(size_t a,size_t b) {size_t x=a,y=b;while(y){size_t r=x%y;x=y;y=r;}return a/x*b;}
static void *heap_caps_aligned_alloc(size_t alignment,size_t bytes,int caps) {
    last_caps=caps;last_alignment=alignment;last_bytes=bytes;
    if(bytes>((caps&MALLOC_CAP_SPIRAM)?external_available:internal_available))return NULL;
    void *p=_aligned_malloc(bytes,alignment);assert(p);allocations++;return p;
}
static void release(void *p){if(p){_aligned_free(p);allocations--;}}
#include "sdio_dma_under_test.inc"
static void *original_alloc(size_t len) {
    esp_dma_mem_info_t info={.extra_heap_caps=0,.dma_alignment_bytes=64};void *p=NULL;
    return esp_dma_capable_malloc(len,&info,&p,NULL)==ESP_OK?p:NULL;
}
#define MEM_ALLOC(n) original_alloc(n)
static struct {int write_index;struct {uint8_t *buf;size_t buf_size;} buffer[2];} double_buf;
typedef struct {void (*_h_free)(void*);} funcs_t;
static funcs_t funcs={release};
static struct {funcs_t *funcs;} g_h={&funcs};

// Catch the driver's existing assertion so allocation failures are verified,
// not silently bypassed. The production source keeps the SDK assert handler.
static jmp_buf failed;
#undef assert
#define assert(x) do {if(!(x))longjmp(failed,1);}while(0)
#define CONFIG_IDF_TARGET_ESP32P4 1
#include "sdio_buffer_under_test.inc"
#undef CONFIG_IDF_TARGET_ESP32P4
#define CONFIG_IDF_TARGET_ESP32P4 0
#define sdio_rx_get_buffer original_sdio_rx_get_buffer
#include "sdio_buffer_under_test.inc"
#undef sdio_rx_get_buffer
#undef assert
#define assert(x) do {if(!(x)){fprintf(stderr,"FAIL line %d: %s\n",__LINE__,#x);exit(1);}}while(0)

static void reset(void) {for(int i=0;i<2;i++)release(double_buf.buffer[i].buf);memset(&double_buf,0,sizeof(double_buf));assert(allocations==0);}
int main(void) {
    // Original allocator fails the same way under internal DMA pressure.
    internal_available=1024;
    if(!setjmp(failed)){original_sdio_rx_get_buffer(4097);assert(0);}
    assert(allocations==0&&!(last_caps&MALLOC_CAP_SPIRAM)); reset();
    for(int line=64;line<=128;line*=2) {
        cache_line=line;internal_available=0;
        uint8_t *a=sdio_rx_get_buffer(4097);
        assert(a&&last_bytes==4608&&last_caps==(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT));
        assert(last_alignment==(size_t)line&&(uintptr_t)a%line==0);
        assert(sdio_rx_get_buffer(4000)==a&&allocations==1); // reuse smaller burst
        sdio_rx_get_buffer(65535);assert(last_bytes==65536&&allocations==1); // burst growth
        double_buf.write_index=1;sdio_rx_get_buffer(8705);assert(last_bytes==9216&&allocations==2);
        memset(double_buf.buffer[0].buf,0x5a,65536); // read/write spans remain separate
        memset(double_buf.buffer[1].buf,0xa5,9216);
        assert(double_buf.buffer[0].buf[65535]==0x5a);reset();
    }
    internal_available=65536;original_sdio_rx_get_buffer(2049);
    assert(!(last_caps&MALLOC_CAP_SPIRAM)&&last_bytes==2560);reset(); // other targets unchanged
    external_available=0;
    if(!setjmp(failed)){sdio_rx_get_buffer(512);assert(0);}
    assert(allocations==0);reset(); // never hide a real PSRAM allocation failure
    puts("SDIO_MEMORY_PASS: original internal-allocation panic reproduced; P4 PSRAM survives zero internal heap, 64/128 cache alignment, 512 block padding, reuse/growth, double buffers, failure assertion and other-target behavior");
    return 0;
}
