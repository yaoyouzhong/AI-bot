#undef NDEBUG
#include <assert.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
#include <stdio.h>
#define CONFIG_IDF_TARGET_ESP32P4 1
#define CONFIG_SPIRAM 1
#define H_SDIO_HOST_RX_MODE 1
#define H_SDIO_HOST_STREAMING_MODE 1
#define MALLOC_CAP_SPIRAM 1
#define MALLOC_CAP_8BIT 2
#define MAX_SDIO_BUFFER_SIZE 1600
#define ESP_OK 0
#define ESP_FAIL -1
#define ESP_ERR_NO_MEM -2
#define ESP_LOGE(...) ((void)0)
typedef int esp_err_t;
struct esp_payload_header {uint16_t len,offset;};
static int allocated,freed,queued,fail_alloc,fail_queue;
static uint8_t saved[3200];static size_t saved_size;
static void *heap_caps_malloc(size_t size,int caps) {
    assert(caps==(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT));
    if(fail_alloc)return NULL;allocated++;return malloc(size);
}
static void heap_caps_free(void *p){freed++;free(p);}
#include "sdio_rx_helpers_under_test.inc"
static int is_valid_sdio_rx_packet(uint8_t *p,uint16_t *len,uint16_t *offset) {
    struct esp_payload_header h;memcpy(&h,p,sizeof(h));*len=h.len;*offset=h.offset;
    return h.len>0&&h.offset>=sizeof(h);
}
static int sdio_push_pkt_to_queue(uint8_t *p,uint16_t len,uint16_t offset) {
    if(fail_queue)return -1;
    memcpy(saved+saved_size,p,len+offset);saved_size+=len+offset;queued++;
    sdio_rx_packet_free(p);return 0;
}
#include "sdio_rx_body_under_test.inc"
int main(void) {
    uint8_t stream[80];memset(stream,0x5a,sizeof(stream));
    struct esp_payload_header h={36,4};memcpy(stream,&h,4);memcpy(stream+40,&h,4);
    assert(sdio_push_data_to_queue(stream,80)==ESP_OK&&queued==2&&saved_size==80&&!memcmp(saved,stream,80));
    assert(allocated==freed);
    fail_alloc=1;assert(sdio_push_data_to_queue(stream,80)==ESP_ERR_NO_MEM&&queued==2);fail_alloc=0;
    int before=allocated;assert(sdio_push_data_to_queue(stream,3)==ESP_FAIL&&allocated==before);
    assert(sdio_push_data_to_queue(stream,39)==ESP_FAIL&&allocated==before);
    h.len=2000;memcpy(stream,&h,4);assert(sdio_push_data_to_queue(stream,80)==ESP_FAIL&&allocated==before);
    h.len=36;memcpy(stream,&h,4);fail_queue=1;
    assert(sdio_push_data_to_queue(stream,80)==ESP_FAIL&&allocated==freed);
    puts("SDIO_RX_PACKET_OK PSRAM, exact packet copies, allocation failure, bounds, queue failure ownership");
}
