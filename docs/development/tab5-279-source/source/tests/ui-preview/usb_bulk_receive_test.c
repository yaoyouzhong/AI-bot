#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "osal/osal.h"
#include "common/tusb_fifo.h"
#include "common/tusb_private.h"
#include "class/cdc/cdc_device.h"

static bool busy,claimed;
static unsigned submitted;
bool __wrap_usbd_edpt_xfer(uint8_t,uint8_t,uint8_t*,uint16_t,bool);
static bool stream_claim(tu_edpt_stream_t *s){(void)s;if(busy||claimed)return false;claimed=true;return true;}
static bool stream_release(tu_edpt_stream_t *s){(void)s;claimed=false;return true;}
static bool stream_xfer(tu_edpt_stream_t *s,uint16_t count){assert(count<=4096&&count%64==0);return __wrap_usbd_edpt_xfer(s->hwid,s->ep_addr,s->ep_buf,count,false);}
#define tu_edpt_stream_read_xfer __real_tu_edpt_stream_read_xfer
#define tu_edpt_stream_read __real_tu_edpt_stream_read
#include "usb_rx_stream_under_test.inc"
#undef tu_edpt_stream_read_xfer
#undef tu_edpt_stream_read
#include "../../firmware/main/usb_bulk_receive.c"
static bool fail_submit;
bool __real_usbd_edpt_xfer(uint8_t port,uint8_t endpoint,uint8_t *buffer,uint16_t count,bool is_isr) {
    assert(!port&&endpoint==2&&buffer&&!is_isr);
    // Even an immediate IRQ observes the generation recorded before arm.
    assert(!usb_bulk_receive_discard());submitted=count;busy=!fail_submit;return !fail_submit;
}
uint32_t tu_edpt_stream_read_xfer(tu_edpt_stream_t *s){return __wrap_tu_edpt_stream_read_xfer(s);}
uint32_t tu_edpt_stream_read(tu_edpt_stream_t *s,void *out,uint32_t count){return __wrap_tu_edpt_stream_read(s,out,count);}

typedef struct {uint8_t rhport,itf_num,ep_notify;tu_edpt_stream_t tx_stream,rx_stream;} cdcd_interface_t;
static cdcd_interface_t _cdcd_itf[1];
static uint8_t find_cdc_itf(uint8_t ep){assert(!ep);return 0;}
static bool usbd_edpt_open(uint8_t rhport,const tusb_desc_endpoint_t *ep){assert(!rhport&&ep->wMaxPacketSize==64);return true;}
#include "usb_cdc_open_under_test.inc"
static uint8_t ep[8192],fifo[8192],tx_fifo[2048],out[8192];
static const uint8_t descriptors[]={
    9,TUSB_DESC_INTERFACE,0,0,0,TUSB_CLASS_CDC,CDC_COMM_SUBCLASS_ABSTRACT_CONTROL_MODEL,0,0,
    9,TUSB_DESC_INTERFACE,1,0,2,TUSB_CLASS_CDC_DATA,0,0,0,
    7,TUSB_DESC_ENDPOINT,0x02,TUSB_XFER_BULK,64,0,0,
    7,TUSB_DESC_ENDPOINT,0x82,TUSB_XFER_BULK,64,0,0};
int main(void){
    tu_edpt_stream_t *rx=&_cdcd_itf[0].rx_stream,*tx=&_cdcd_itf[0].tx_stream;
    rx->ep_buf=ep;assert(tu_fifo_config(&rx->ff,fifo,sizeof(fifo),false));
    assert(tu_fifo_config(&tx->ff,tx_fifo,sizeof(tx_fifo),false));
    // Real current cdcd_open defaults to one MPS despite its 8192-byte EP buffer.
    assert(cdcd_open(0,(const tusb_desc_interface_t*)descriptors,sizeof(descriptors))==sizeof(descriptors));
    assert(rx->xfer_len==64&&submitted==64);busy=claimed=false;
    unsigned cases=0;
    for(unsigned length=1;length<=65535;length+=31){
        usb_bulk_receive_select(true);tu_fifo_clear(&rx->ff);busy=claimed=false;
        assert(tu_edpt_stream_read_xfer(rx)==4096&&submitted==4096);
        unsigned wire_length=length+(length%64==0),at=0,events=0;
        while(at<wire_length){unsigned n=wire_length-at;if(n>submitted)n=submitted;
            // A large receive completes on its requested size or a short
            // final packet. Without the bridge terminator, aligned tails hang.
            assert(n==submitted||n%64!=0);
            for(unsigned i=0;i<n;i++)ep[i]=at+i<length?(uint8_t)((at+i)*13+length):'\n';
            busy=claimed=false;tu_edpt_stream_read_xfer_complete(rx,n);
            assert(tu_edpt_stream_read(rx,out,sizeof(out))==n);
            for(unsigned i=0;i<n;i++)assert(out[i]==(at+i<length?(uint8_t)((at+i)*13+length):'\n'));
            assert(submitted==4096);at+=n;events++;
        }
        assert(events==(wire_length+4095)/4096);
        // Body completion leaves an outstanding bulk receive. A short next
        // control write completes it, then all legacy receives return to64.
        usb_bulk_receive_select(false);busy=claimed=false;
        tu_edpt_stream_read_xfer_complete(rx,1);
        assert(tu_edpt_stream_read(rx,out,sizeof(out))==1&&submitted==64);
        cases++;
    }
    tu_edpt_stream_t other={.is_host=true,.mps=64,.xfer_len=512};
    select_size(&other);assert(other.xfer_len==512);
    other.is_host=false;other.ep_addr=0x03;select_size(&other);assert(other.xfer_len==512);
    // A partial bulk DMA can survive DTR close. Only its stale completion is
    // dropped, including the new bridge's separate one-byte open barrier.
    busy=claimed=false;usb_bulk_receive_select(true);tu_fifo_clear(&rx->ff);
    assert(tu_edpt_stream_read_xfer(rx)==4096);
    usb_bulk_receive_close();assert(usb_bulk_receive_discard());
    memset(ep,0xa7,128);ep[128]='\n';busy=claimed=false;
    tu_edpt_stream_read_xfer_complete(rx,129);
    assert(usb_bulk_receive_discard());tu_edpt_stream_clear(rx);
    assert(tu_edpt_stream_read_xfer(rx)==64&&!usb_bulk_receive_discard()&&!tu_fifo_count(&rx->ff));
    const char ping[]="@AIBOT {\"version\":1,\"type\":\"tab5_ping\"}\n";
    memcpy(ep,ping,sizeof(ping)-1);busy=claimed=false;tu_edpt_stream_read_xfer_complete(rx,sizeof(ping)-1);
    assert(tu_edpt_stream_read(rx,out,sizeof(out))==sizeof(ping)-1&&!memcmp(out,ping,sizeof(ping)-1));
    usb_bulk_receive_close();assert(!usb_bulk_receive_discard()); // legacy64 is never dropped
    busy=claimed=false;usb_bulk_receive_select(true);fail_submit=true;
    assert(!tu_edpt_stream_read_xfer(rx)&&!atomic_load(&armed_bulk));fail_submit=false;
    puts("USB_BULK_RECEIVE_OK actual CDC single-MPS open; negotiated4KiB; exact bytes/FIFO; body completion and next short control restore64; other endpoints unchanged");
    printf("USB_BULK_RECEIVE_CASES %u lengths\n",cases);return 0;
}
