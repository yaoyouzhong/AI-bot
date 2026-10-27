#ifdef NDEBUG
#undef NDEBUG
#endif
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>

// Only heap/RTOS/peripheral boundaries are simulated. Both constructors below
// are extracted from the real SDK, with just the two allocations differing.
typedef int esp_err_t;
enum {ESP_OK=0,ESP_ERR_NO_MEM=257,ESP_ERR_INVALID_ARG=258};
enum {MALLOC_CAP_8BIT=1<<2,MALLOC_CAP_32BIT=1<<1,MALLOC_CAP_DMA=1<<3,
      MALLOC_CAP_INTERNAL=1<<11,MALLOC_CAP_DEFAULT=1<<12};
#define JPEG_MEM_ALLOC_CAPS MALLOC_CAP_DEFAULT
#define JPEG_ALIGN_UP(n,a) (((n)+(a)-1)&~((a)-1))
#define ESP_RETURN_ON_FALSE(c,r,...) do{if(!(c))return(r);}while(0)
#define ESP_GOTO_ON_FALSE(c,r,label,...) do{if(!(c)){ret=(r);goto label;}}while(0)
#define ESP_GOTO_ON_ERROR(e,label,...) do{ret=(e);if(ret!=ESP_OK)goto label;}while(0)
#define ESP_ERROR_CHECK(e) assert((e)==ESP_OK)
#define JPEG_LL_ENCODER_EVENT_INTR 1
#define JPEG_LL_DECODER_EVENT_INTR 2
#define JPEG_ALLOW_INTR_PRIORITY_MASK 0xff
#define JPEG_INTR_ALLOC_FLAG 1
#define CACHE_LL_LEVEL_EXT_MEM 2
#define CACHE_TYPE_DATA 1
#define portMAX_DELAY -1
#define pdMS_TO_TICKS(n) (n)
typedef struct {uint32_t words[8];} dma2d_descriptor_t;
typedef struct {int dummy;} dma2d_trans_t;
#define SIZEOF_DMA2D_TRANS_T sizeof(dma2d_trans_t)
typedef struct {void *dev;} jpeg_hal_context_t;
typedef struct {jpeg_hal_context_t hal;} codec_base_t;
typedef struct {unsigned words[140];} jpeg_enc_header_info_t;
typedef struct {unsigned words[2];} jpeg_enc_dma2d_evt_t;
typedef struct jpeg_encoder_t {
    dma2d_descriptor_t *rxlink,*txlink;
    uint32_t dma_desc_size;
    codec_base_t *codec_base;
    void *evt_queue,*intr_handle,*dma2d_group_handle;
    int timeout_tick;
    dma2d_trans_t *trans_desc;
    jpeg_enc_header_info_t *header_info;
} jpeg_encoder_t;
typedef jpeg_encoder_t *jpeg_encoder_handle_t;
typedef struct {int timeout_ms,intr_priority;} jpeg_encode_engine_cfg_t;
typedef jpeg_encoder_t jpeg_decoder_t;
typedef jpeg_decoder_t *jpeg_decoder_handle_t;
typedef jpeg_encode_engine_cfg_t jpeg_decode_engine_cfg_t;
typedef jpeg_enc_header_info_t jpeg_dec_header_info_t;
typedef jpeg_enc_dma2d_evt_t jpeg_dma2d_dec_evt_t;
typedef struct {int pool_id;} dma2d_pool_config_t;
static unsigned line_size=128,descriptor_calls,descriptor_bytes[2],descriptor_caps[2];
static int allocations,fail_descriptor;
static bool ordinary_dma_available;
#include "jpeg_reserved_caps_under_test.inc"
static void *heap_caps_calloc(size_t n,size_t size,unsigned caps) {
    (void)caps;void *p=calloc(n,size);assert(p);allocations++;return p;
}
static void release(void *p){if(p){allocations--;free(p);}}
static void *heap_caps_aligned_calloc(size_t alignment,size_t n,size_t size,unsigned caps) {
    assert(alignment==line_size&&descriptor_calls<2);
    unsigned index=descriptor_calls++;
    descriptor_bytes[index]=(unsigned)(n*size);descriptor_caps[index]=caps;
    unsigned available=reserved_caps[0]|reserved_caps[1]|reserved_caps[2];
    if((caps&available)!=caps&&!ordinary_dma_available)return NULL;
    if(fail_descriptor==(int)descriptor_calls)return NULL;
    return heap_caps_calloc(n,size,caps);
}
static unsigned cache_hal_get_cache_line_size(int level,int type){assert(level==2&&type==1);return line_size;}
static esp_err_t jpeg_acquire_codec_handle(codec_base_t **p){*p=heap_caps_calloc(1,sizeof(**p),0);return ESP_OK;}
static void *xQueueCreateWithCaps(int count,size_t size,unsigned caps){return heap_caps_calloc(count,size,caps);}
static void jpeg_ll_clear_intr_mask(void *dev,int mask){(void)dev;(void)mask;}
static void jpeg_ll_enable_intr_mask(void *dev,int mask){(void)dev;assert(mask==JPEG_LL_DECODER_EVENT_INTR);}
static esp_err_t jpeg_check_intr_priority(codec_base_t *p,int n){assert(p&&n==0);return ESP_OK;}
static void jpeg_encoder_isr_handle_default(void *p){(void)p;}
static void jpeg_decoder_isr_handle_default(void *p){(void)p;}
static esp_err_t jpeg_isr_register(codec_base_t *p,void(*cb)(void*),void *arg,int mask,int flags,void **handle) {
    assert(p&&cb&&arg&&mask&&flags);*handle=heap_caps_calloc(1,4,0);return ESP_OK;
}
static esp_err_t dma2d_acquire_pool(const dma2d_pool_config_t *p,void **handle){assert(p->pool_id==0);*handle=heap_caps_calloc(1,4,0);return ESP_OK;}
static esp_err_t jpeg_del_encoder_engine(jpeg_encoder_handle_t e) {
    release(e->rxlink);release(e->txlink);release(e->codec_base);release(e->evt_queue);
    release(e->intr_handle);release(e->dma2d_group_handle);release(e->trans_desc);release(e->header_info);release(e);
    return ESP_OK;
}
#include "jpeg_encoder_original_under_test.inc"
#include "jpeg_encoder_fixed_under_test.inc"
static esp_err_t jpeg_del_decoder_engine(jpeg_decoder_handle_t e){return jpeg_del_encoder_engine(e);}
#include "jpeg_decoder_original_under_test.inc"
#include "jpeg_decoder_fixed_under_test.inc"
static void run(bool decoder,bool fixed,bool ordinary,unsigned alignment,int failed_descriptor)
{
    assert(allocations==0);ordinary_dma_available=ordinary;line_size=alignment;
    descriptor_calls=0;fail_descriptor=failed_descriptor;
    jpeg_encoder_handle_t engine=NULL;const jpeg_encode_engine_cfg_t cfg={.timeout_ms=3000};
    esp_err_t result=decoder?(fixed?jpeg_new_decoder_engine(&cfg,&engine):jpeg_new_decoder_engine_original(&cfg,&engine)):
        (fixed?jpeg_new_encoder_engine(&cfg,&engine):jpeg_new_encoder_engine_original(&cfg,&engine));
    bool success=(fixed||ordinary)&&!failed_descriptor;
    assert(result==(success?ESP_OK:ESP_ERR_NO_MEM));
    if(success){
        assert(engine&&descriptor_calls==2);
        for(unsigned i=0;i<2;i++) {
            if(fixed){assert(descriptor_bytes[i]>=engine->dma_desc_size);assert(!(descriptor_caps[i]&MALLOC_CAP_DEFAULT));}
            else {assert(decoder?descriptor_bytes[i]>=engine->dma_desc_size:descriptor_bytes[i]<engine->dma_desc_size);assert(descriptor_caps[i]&MALLOC_CAP_DEFAULT);}
        }
        jpeg_del_encoder_engine(engine);
    } else assert(engine==NULL);
    assert(allocations==0);
}
int main(void)
{
    assert(!(reserved_caps[0]&MALLOC_CAP_DEFAULT)&&!(reserved_caps[1]&MALLOC_CAP_DEFAULT)&&!(reserved_caps[2]&MALLOC_CAP_DEFAULT));
    for(int decoder=0;decoder<2;decoder++) {
        run(decoder,false,false,128,0); // reproduces 257 with an available reserve
        run(decoder,true,false,128,0);run(decoder,true,false,64,0);
        run(decoder,false,true,128,0);
        run(decoder,true,true,128,0);run(decoder,true,false,128,1);run(decoder,true,false,128,2);
    }
    puts("JPEG_DMA_MEMORY_PASS: 14 encoder/decoder cases; original reproduces 257, patched SDK constructors use reserved DMA pool and full cache span; allocation-failure cleanup");
    return 0;
}
