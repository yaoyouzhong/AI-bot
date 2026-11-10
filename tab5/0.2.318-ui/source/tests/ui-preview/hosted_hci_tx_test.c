// Execute actual Hosted staging and both HCI functions with failure injection.
#undef NDEBUG
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#define CONFIG_SPIRAM 1
#define CONFIG_ESP_HOSTED_SDIO_HOST_INTERFACE 1
#define MALLOC_CAP_SPIRAM 1
#define MALLOC_CAP_8BIT 2
#define H_BT_ENABLE_LL_INIT 0
#define ESP_OK 0
#define ESP_FAIL -1
#define HCI_H4_ACL 2
#define HCI_H4_CMD 1
#define ESP_HCI_IF 3
#define H_BUFF_NO_ZEROCOPY 0
#define ESP_LOGE(...) ((void)0)
static int alloc_fail, flatten_fail, transport_rc, transport_calls, psram_allocs, internal_allocs, allocated, freed, mbuf_freed, cmd_freed;
static void *owned;
static uint8_t expected[600];static unsigned expected_size;
static void *allocate(size_t size){if(alloc_fail)return NULL;assert(!owned);owned=malloc(size);assert(owned);allocated++;return owned;}
static void release(void *p){assert(p&&p==owned);free(p);owned=NULL;freed++;}
static void *heap_caps_malloc(size_t size,int caps){assert(caps==(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT));psram_allocs++;return allocate(size);}
static void *internal_alloc(size_t size){internal_allocs++;return allocate(size);}
#define MEM_ALLOC(x) internal_alloc(x)
#define H_DEFLT_FREE_FUNC release
struct os_mbuf {unsigned size;uint8_t data[600];};
#define OS_MBUF_PKTLEN(m) ((m)->size)
static void os_mbuf_free_chain(struct os_mbuf *m){assert(m);free(m);mbuf_freed++;}
static int ble_hs_mbuf_to_flat(struct os_mbuf *m,void *out,unsigned size,void *unused){
    (void)unused;assert(size==m->size);if(flatten_fail)return 7;memcpy(out,m->data,size);return 0;
}
static int esp_hosted_tx(unsigned iface,unsigned index,uint8_t *data,int size,int zcopy,void (*free_fn)(void*)){
    assert(iface==ESP_HCI_IF&&index==0&&zcopy==H_BUFF_NO_ZEROCOPY);
    assert(size==(int)expected_size&&!memcmp(data,expected,size));transport_calls++;
    free_fn(data);return transport_rc;
}
static void ble_transport_free(void *p){free(p);cmd_freed++;}
#include "hosted_hci_helper_under_test.inc"
// Only a pre-submission flatten failure calls free(data) in this source.
#define free(p) release(p)
#include "hosted_hci_tx_under_test.inc"
#undef free
static struct os_mbuf *acl(unsigned size){
    struct os_mbuf *m=calloc(1,sizeof(*m));assert(m&&size<600);m->size=size;expected[0]=HCI_H4_ACL;expected_size=size+1;
    for(unsigned i=0;i<size;i++)m->data[i]=expected[i+1]=(uint8_t)(i*29);return m;
}
static void *command(unsigned payload){
    uint8_t *p=calloc(1,payload+3);assert(p&&payload<256);p[0]=0x01;p[1]=0x20;p[2]=(uint8_t)payload;
    for(unsigned i=0;i<payload;i++)p[i+3]=(uint8_t)(i+7);
    expected[0]=HCI_H4_CMD;expected_size=payload+4;memcpy(expected+1,p,payload+3);return p;
}
int main(void){
    assert(ble_transport_to_ll_acl_impl(acl(516))==ESP_OK&&transport_calls==1&&mbuf_freed==1);
    assert(ble_transport_to_ll_cmd_impl(command(255))==ESP_OK&&transport_calls==2&&cmd_freed==1);
    alloc_fail=1;
    assert(ble_transport_to_ll_acl_impl(acl(64))==ESP_FAIL&&mbuf_freed==2&&transport_calls==2);
    assert(ble_transport_to_ll_cmd_impl(command(0))==ESP_FAIL&&cmd_freed==2&&transport_calls==2);
    alloc_fail=0;flatten_fail=1;
    assert(ble_transport_to_ll_acl_impl(acl(64))==ESP_FAIL&&mbuf_freed==3&&transport_calls==2&&allocated==freed);
    flatten_fail=0;transport_rc=ESP_FAIL;
    assert(ble_transport_to_ll_acl_impl(acl(64))==ESP_FAIL&&mbuf_freed==4&&transport_calls==3&&allocated==freed);
    assert(ble_transport_to_ll_cmd_impl(command(1))==ESP_FAIL&&cmd_freed==3&&transport_calls==4&&allocated==freed);
    transport_rc=0;
    for(unsigned i=0;i<512;i++)assert(ble_transport_to_ll_acl_impl(acl(516))==ESP_OK);
    assert(allocated==freed&&!owned);
#if CONFIG_IDF_TARGET_ESP32P4
    assert(psram_allocs==519&&internal_allocs==0);
#else
    assert(psram_allocs==0&&internal_allocs==519);
#endif
    puts("HOSTED_HCI_TX_OK CPU_only_staging exact_max_packet allocation_flatten_transport_failures ownership_512_cycles");
}
