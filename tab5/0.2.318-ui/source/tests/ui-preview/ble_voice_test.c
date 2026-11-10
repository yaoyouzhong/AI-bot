#undef NDEBUG
#include <assert.h>
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef void *SemaphoreHandle_t;
#define portMAX_DELAY 0
#define pdTRUE 1
static int take(void *semaphore,unsigned wait);
#define xSemaphoreTake(a,b) take(a,b)
#define xSemaphoreGive(a) ((void)0)
#define xSemaphoreCreateBinary() ((void*)2)
#define vSemaphoreDelete(s) ((void)0)
#define pdMS_TO_TICKS(n) (n)
#define BLE_GATT_ACCESS_OP_READ_CHR 1
#define BLE_ATT_ERR_INSUFFICIENT_AUTHEN 5
#define BLE_ATT_ERR_INSUFFICIENT_RES 17
#define BLE_ATT_ERR_INVALID_ATTR_VALUE_LEN 13
#define BLE_ATT_ERR_UNLIKELY 14
typedef struct {uint8_t data[500];unsigned len;} fake_mbuf;
struct ble_gatt_access_ctxt {int op;fake_mbuf *om;};
#define OS_MBUF_PKTLEN(m) ((m)->len)
static int os_mbuf_append(fake_mbuf *m,const void *p,unsigned n){assert(n<=500);memcpy(m->data,p,n);m->len=n;return 0;}
static int ble_hs_mbuf_to_flat(fake_mbuf *m,void *p,unsigned n,void *unused){(void)unused;memcpy(p,m->data,n);return 0;}
typedef struct {bool paired;} fake_config;
static fake_config device_get_config(void){return (fake_config){true};}
static bool ble_connected=true;
static bool ble_host_authorized(void){return ble_connected;}
static int64_t now=1000;
static int64_t esp_timer_get_time(void){return now*1000;}
static void delay(unsigned ms);
static unsigned poll_sleeps,completion_waits;
#define vTaskDelay(ms) (poll_sleeps++,delay(ms))
#include "../../firmware/main/ble_mailbox_window.h"
typedef struct {uint16_t conn,attr;} mailbox_notify_target;
static unsigned notification_at;
static int ble_att_mtu(uint16_t conn){(void)conn;return 517;}
static bool mailbox_notify(const uint8_t *data,size_t size,void *ctx);
#include "../../firmware/main/ble_voice.inc"
static int mode;
static int take(void *semaphore,unsigned wait){if(semaphore==(void*)2&&wait){completion_waits++;delay(mode==2?wait:1);return mode==2?0:pdTRUE;}return pdTRUE;}
static uint8_t expected[12288],response[8192];static size_t expected_size,response_size;
static int call(int op,fake_mbuf *m){struct ble_gatt_access_ctxt ctx={op,m};return voice_access(0,0,&ctx,NULL);}
static bool mailbox_notify(const uint8_t *data,size_t size,void *ctx) {
    (void)ctx;assert(size<=488&&read32(data)==voice_active&&read16(data+4)==notification_at);
    assert(!memcmp(data+8,expected+notification_at,size-8));notification_at+=size-8;return true;
}
static void delay(unsigned ms) {
    now+=ms;
    if(mode==1){ble_connected=false;voice_reset();return;}
    if(mode==2)return;
    fake_mbuf m={0};unsigned at=0;
    while(at<expected_size) {
        assert(call(1,&m)==0&&read32(m.data)==voice_active&&read16(m.data+4)==at&&read16(m.data+6)==expected_size);
        assert(!memcmp(m.data+8,expected+at,m.len-8));at+=m.len-8;
        if(mode==3) {
            while(at<expected_size) {
                m.len=8;m.data[0]=3;write32(m.data+1,voice_active);write16(m.data+5,at);m.data[7]=4;
                notification_at=at;assert(call(2,&m)==0);assert(notification_at>at);at=notification_at;
            }
            break;
        }
        if(at<expected_size){m.len=7;m.data[0]=1;write32(m.data+1,voice_active);write16(m.data+5,at);assert(call(2,&m)==0);}
    }
    // Out-of-order reply must be rejected before touching the buffer.
    m.len=10;m.data[0]=2;write32(m.data+1,voice_active);write16(m.data+5,1);write16(m.data+7,response_size);
    assert(call(2,&m)==BLE_ATT_ERR_INVALID_ATTR_VALUE_LEN&&voice_received==0);
    for(at=0;at<response_size;) {
        unsigned n=response_size-at;if(n>235)n=235;
        m.len=9+n;m.data[0]=2;write32(m.data+1,voice_active);write16(m.data+5,at);write16(m.data+7,response_size);
        memcpy(m.data+9,response+at,n);assert(call(2,&m)==0);at+=n;
    }
}
int main(void) {
    voice_lock=(void*)1;voice_out=malloc(VOICE_CAPACITY);voice_in=malloc(VOICE_CAPACITY);assert(voice_out&&voice_in);
    char nonce[33],proof[65];memset(nonce,'a',32);nonce[32]=0;memset(proof,'b',64);proof[64]=0;
    memcpy(expected,nonce,32);memcpy(expected+32,proof,64);
    for(unsigned i=96;i<sizeof(expected);i++)expected[i]=(uint8_t)i;
    for(unsigned i=0;i<sizeof(response);i++)response[i]=(uint8_t)(i*3);
    for(mode=0;mode<4;mode++) {
        ble_connected=true;voice_peer_seen=now;expected_size=4097;response_size=8192;size_t size=0;
        uint8_t *reply=ble_voice_exchange(nonce,proof,expected+96,expected_size-96,&size);
        if(mode==0||mode==3)assert(reply&&size==response_size&&!memcmp(reply,response,size));else assert(!reply&&!size);
        assert(!voice_active);free(reply);
    }
    size_t size=0;assert(!ble_voice_exchange(nonce,proof,expected,VOICE_CAPACITY,&size));
    assert(completion_waits==4&&!poll_sleeps&&!voice_done);free(voice_out);free(voice_in);
    puts("BLE_VOICE_MAILBOX_OK fragmented request/reply, wrong offset, oversize, timeout, disconnect reset");return 0;
}
