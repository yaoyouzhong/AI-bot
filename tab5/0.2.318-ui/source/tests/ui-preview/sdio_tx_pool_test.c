#ifdef NDEBUG
#undef NDEBUG
#endif
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <stdbool.h>
#include <string.h>
#include <malloc.h>
#include <errno.h>
#define CONFIG_SPIRAM 1
#define CONFIG_ESP_HOSTED_SDIO_HOST_INTERFACE 1
#define SOC_SDMMC_PSRAM_DMA_CAPABLE 1
#define ESP_OK 0
#define ESP_FAIL -1
#define ESP_ERR_INVALID_ARG 1
#define ESP_ERR_NO_MEM 2
#define ESP_ERR_ESP_NETIF_TX_FAILED 3
#define MALLOC_CAP_SPIRAM 1
#define MALLOC_CAP_8BIT 2
#define MALLOC_CAP_DMA 4
#define ESP_LOGV(...) ((void)0)
#define ALIGN_UP_BY(n,a) (((n)+(a)-1)&~((a)-1))
#define ESP_RETURN_ON_FALSE_ISR(test,err,tag,...) do {if(!(test))return (err);}while(0)
typedef int esp_err_t;
typedef struct {int extra_heap_caps;size_t dma_alignment_bytes;} esp_dma_mem_info_t;
static size_t internal_available,external_available=1000000,cache_line=128,last_bytes;
static int allocations,last_caps;
static esp_err_t esp_cache_get_alignment(int caps,size_t *out){*out=(caps&MALLOC_CAP_SPIRAM)?cache_line:64;return ESP_OK;}
static size_t hal_utils_calc_lcm(size_t a,size_t b){size_t x=a,y=b;while(y){size_t r=x%y;x=y;y=r;}return a/x*b;}
static void *heap_caps_aligned_alloc(size_t alignment,size_t bytes,int caps){
    last_caps=caps;last_bytes=bytes;
    if(bytes>((caps&MALLOC_CAP_SPIRAM)?external_available:internal_available))return NULL;
    void *p=_aligned_malloc(bytes,alignment);assert(p);allocations++;return p;
}
#include "sdio_dma_under_test.inc"
static void *original_alloc(size_t n){esp_dma_mem_info_t info={0,64};void *p=NULL;return esp_dma_capable_malloc(n,&info,&p,NULL)==ESP_OK?p:NULL;}
#define MEM_ALLOC(n) original_alloc(n)
#define H_USE_MEMPOOL 1
#define MEMPOOL_ALIGNMENT_BYTES 64
#define MEMPOOL_ALIGNED(n) (((n)/64+1)*64)
struct mempool_entry {struct mempool_entry *next;};
struct mempool {struct mempool_entry *head;void *spinlock;int block_size;};
#define SLIST_EMPTY(h) (*(h)==NULL)
#define SLIST_FIRST(h) (*(h))
#define SLIST_REMOVE_HEAD(h,e) (*(h)=(*(h))->next)
#define SLIST_INSERT_HEAD(h,p,e) do {(p)->next=*(h);*(h)=(p);}while(0)
static void lock(void *p){(void)p;}
typedef struct {void (*_h_lock_mempool)(void*);void (*_h_unlock_mempool)(void*);void *(*_h_memset)(void*,int,size_t);void *(*_h_memcpy)(void*,const void*,size_t);} test_funcs;
static test_funcs funcs={lock,lock,memset,memcpy};
static struct {test_funcs *funcs;} g_h={&funcs};
#include "sdio_pool_under_test.inc"
#define ESP_STA_IF 0
#define ESP_AP_IF 1
#define ESP_SERIAL_IF 2
#define MAX_TRANSPORT_BUFFER_SIZE 1536
#define H_ESP_PAYLOAD_HEADER_OFFSET 12
#define H_BUFF_ZEROCOPY 1
#define unlikely(x) (x)
static int wifi_tx_throttling,queued,fail_queue;
typedef struct {void *api_chan;struct mempool *memp;} test_channel;
static test_channel channels[3];
static test_channel *chan_arr[]={&channels[0],&channels[1],&channels[2]};
static unsigned char expected[MAX_TRANSPORT_BUFFER_SIZE];
static esp_err_t esp_hosted_tx(int iface,int number,void *p,size_t n,int zcopy,void (*release)(void*)){
    (void)iface;(void)number;queued++;assert(zcopy==H_BUFF_ZEROCOPY);
    assert((uintptr_t)p%(CONFIG_IDF_TARGET_ESP32P4?cache_line:64)==0);
    assert(memcmp((char*)p+H_ESP_PAYLOAD_HEADER_OFFSET,expected,n)==0);
    for(int i=0;i<H_ESP_PAYLOAD_HEADER_OFFSET;i++)assert(((char*)p)[i]==0);
    release(p);return fail_queue?ESP_FAIL:ESP_OK;
}
#include "sdio_tx_under_test.inc"
static void clear_pool(struct mempool *mp){while(mp->head){void *p=mp->head;mp->head=mp->head->next;_aligned_free(p);allocations--;}}
int main(void){
    struct mempool pools[3]={0};
    for(int i=0;i<3;i++){pools[i].block_size=1600;channels[i].memp=&pools[i];channels[i].api_chan=&channels[i];}
    for(int i=0;i<sizeof(expected);i++)expected[i]=(unsigned char)(i*37);
    for(int line=64;line<=128;line*=2){
        cache_line=line;internal_available=CONFIG_IDF_TARGET_ESP32P4?0:100000;
        for(int iface=0;iface<2;iface++){
            esp_err_t (*tx)(void*,void*,size_t)=iface?transport_drv_ap_tx:transport_drv_sta_tx;
            for(int n=1;n<=1524;n+=17)assert(tx(channels[iface].api_chan,expected,n)==ESP_OK);
            assert(allocations==iface+1); // reuse rather than grow for every packet
            assert(!!(last_caps&MALLOC_CAP_SPIRAM)==!!CONFIG_IDF_TARGET_ESP32P4);
            assert(last_bytes>=1536); // full 512-byte SDIO transfer padding fits
            int before=queued;
            assert(tx(channels[iface].api_chan,expected,1525)==ESP_ERR_INVALID_ARG&&queued==before);
            fail_queue=1;assert(tx(channels[iface].api_chan,expected,100)==ESP_FAIL);fail_queue=0;
            assert(allocations==iface+1); // downstream rejection returned ownership
        }
        clear_pool(&pools[0]);clear_pool(&pools[1]);assert(allocations==0);
    }
    internal_available=external_available=0;
    assert(transport_drv_sta_tx(channels[0].api_chan,expected,100)==ESP_ERR_NO_MEM);
    assert(transport_drv_ap_tx(channels[1].api_chan,expected,100)==ESP_ERR_NO_MEM);
    assert(allocations==0);
    wifi_tx_throttling=1;assert(transport_drv_sta_tx(channels[0].api_chan,expected,100)==ESP_ERR_ESP_NETIF_TX_FAILED);
    assert(transport_drv_sta_tx(NULL,NULL,0)==ESP_OK);
    printf("SDIO_TX_POOL_PASS targetP4=%d: zero internal RAM, 64/128 alignment, pool reuse, STA/AP byte integrity, oversize rejection, OOM return, ownership and throttling\n",CONFIG_IDF_TARGET_ESP32P4);
}
