#include "usb_bulk_receive.h"
#include "tusb.h"
#include "common/tusb_private.h"
#include <stdatomic.h>
#ifdef ESP_PLATFORM
_Static_assert(CFG_TUD_CDC_EP_BUFSIZE>=USB_BULK_RECEIVE_BYTES&&CFG_TUD_CDC_RX_BUFSIZE>=USB_BULK_RECEIVE_BYTES,"USB binary batch exceeds allocated CDC buffers");
#endif

// This negotiated mode is used only for a binary reply from a supporting
// bridge. Its writes terminate with a short packet. Legacy control stays at
// one MPS; the current CDC/TinyUSB same-core ordering policy remains required.
static atomic_bool binary_receive;
static atomic_uint generation,armed_generation;
static atomic_bool armed_bulk;
void usb_bulk_receive_select(bool enabled) {atomic_store(&binary_receive,enabled);}
void usb_bulk_receive_close(void) {atomic_fetch_add(&generation,1);usb_bulk_receive_select(false);}
bool usb_bulk_receive_discard(void) {return atomic_load(&armed_bulk)&&atomic_load(&armed_generation)!=atomic_load(&generation);}
static void select_size(tu_edpt_stream_t *s) {
    if(!s->is_host&&s->hwid==0&&s->ep_addr==0x02&&s->mps==64)
        s->xfer_len=atomic_load(&binary_receive)?USB_BULK_RECEIVE_BYTES:s->mps;
}
uint32_t __real_tu_edpt_stream_read_xfer(tu_edpt_stream_t *s);
uint32_t __real_tu_edpt_stream_read(tu_edpt_stream_t *s,void *out,uint32_t count);
bool __real_usbd_edpt_xfer(uint8_t port,uint8_t endpoint,uint8_t *buffer,uint16_t count,bool is_isr);
bool __wrap_usbd_edpt_xfer(uint8_t port,uint8_t endpoint,uint8_t *buffer,uint16_t count,bool is_isr) {
    bool cdc=port==0&&endpoint==0x02;
    // Record ownership before the real controller can complete its DMA and
    // wake TinyUSB. Sampling after submission would race an immediate IRQ.
    if(cdc){atomic_store(&armed_generation,atomic_load(&generation));atomic_store(&armed_bulk,count>64);}
    bool ok=__real_usbd_edpt_xfer(port,endpoint,buffer,count,is_isr);
    if(cdc&&!ok)atomic_store(&armed_bulk,false);
    return ok;
}
uint32_t __wrap_tu_edpt_stream_read_xfer(tu_edpt_stream_t *s) {
    select_size(s);return __real_tu_edpt_stream_read_xfer(s);
}
uint32_t __wrap_tu_edpt_stream_read(tu_edpt_stream_t *s,void *out,uint32_t count) {
    select_size(s);return __real_tu_edpt_stream_read(s,out,count);
}
