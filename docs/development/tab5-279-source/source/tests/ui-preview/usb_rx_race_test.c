#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include <string.h>
#include <crtdbg.h>
#include "osal/osal.h"
#include "common/tusb_fifo.h"
#include "common/tusb_private.h"
#include "cJSON.h"
#include "../../firmware/main/usb_task_policy.h"

// Real TinyUSB FIFO and stream rearm code; only the hardware DMA and scheduler
// are simulated. usbd.c clears endpoint busy before cdcd_xfer_cb copies OUT
// bytes. Inject the other-core read at that exact boundary.
static uint8_t endpoint[64],fifo[4096],first[64],next[64];
static bool busy,claimed,copy_pending,overwrote;
static bool burst_mode;static unsigned burst_rearm;
static bool stream_claim(tu_edpt_stream_t *s){(void)s;if(busy||claimed)return false;claimed=true;return true;}
static bool stream_release(tu_edpt_stream_t *s){(void)s;claimed=false;return true;}
static bool stream_xfer(tu_edpt_stream_t *s,uint16_t count){
    if(burst_mode){assert(count<=8192&&count%64==0);burst_rearm=count;busy=true;return true;}
    assert(s->ep_buf==endpoint&&count==64);busy=true;
    if(copy_pending){memcpy(endpoint,next,64);overwrote=true;} // next OUT DMA
    return true;
}
#include "usb_rx_stream_under_test.inc"

static bool run(int worker_core,int stack_core,int worker_priority,int stack_priority,int occupied){
    tu_edpt_stream_t s={.mps=64,.xfer_len=64,.ep_buf=endpoint,.ep_addr=2};
    assert(tu_fifo_config(&s.ff,fifo,sizeof(fifo),false));
    uint8_t padding[4096];memset(padding,0x71,sizeof(padding));
    assert(tu_fifo_write_n(&s.ff,padding,(uint16_t)occupied)==occupied);
    memcpy(endpoint,first,64);busy=claimed=false;copy_pending=true;overwrote=false;
    uint8_t consumed[64];
    // The stack RX path has no read-side FIFO lock/wait. A lower-priority
    // receiver on this same core cannot run before that copy completes.
    bool can_run=worker_core!=stack_core||worker_priority>=stack_priority;
    if(can_run)(void)tu_edpt_stream_read(&s,consumed,sizeof(consumed));
    tu_edpt_stream_read_xfer_complete(&s,64);copy_pending=false;
    if(!can_run)(void)tu_edpt_stream_read(&s,consumed,sizeof(consumed));
    uint8_t actual[4096];uint16_t got=tu_fifo_read_n(&s.ff,actual,sizeof(actual));
    assert(got==(unsigned)occupied);
    return !memcmp(actual+got-64,first,64)&&!overwrote;
}
static void metrics_dispatch(bool race) {
    // A real protocol-shaped metrics line, longer than three USB packets.
    // Feed the damaged byte stream through the same JSON/type gate as usb_link.
    const char wire[]="@AIBOT {\"version\":1,\"type\":\"tab5_metrics\",\"deviceId\":\"001122334455\",\"session\":\"0123456789abcdef0123456789abcdef\",\"sequence\":42,\"systemMetrics\":{\"cpuPercent\":9,\"memoryPercent\":55,\"sampleSequence\":7,\"samples\":[{\"upload\":12345,\"download\":54321}]}}\n";
    tu_edpt_stream_t s={.mps=64,.xfer_len=64,.ep_buf=endpoint,.ep_addr=2};
    assert(tu_fifo_config(&s.ff,fifo,sizeof(fifo),false));
    assert(tu_fifo_write_n(&s.ff,wire,64)==64);
    memcpy(endpoint,wire+64,64);memcpy(next,wire+128,64);
    busy=claimed=false;copy_pending=true;overwrote=false;
    char delivered[sizeof(wire)]={0};
    if(race)assert(tu_edpt_stream_read(&s,delivered,64)==64);
    tu_edpt_stream_read_xfer_complete(&s,64);copy_pending=false;
    if(!race)assert(tu_edpt_stream_read(&s,delivered,64)==64);
    assert(tu_fifo_read_n(&s.ff,delivered+64,64)==64);
    memcpy(delivered+128,wire+128,sizeof(wire)-128);
    cJSON *root=cJSON_Parse(delivered+7);
    cJSON *type=cJSON_GetObjectItemCaseSensitive(root,"type");
    bool dispatches=cJSON_IsString(type)&&!strcmp(type->valuestring,"tab5_metrics");
    if(race)assert(overwrote&&!root&&!dispatches);
    else assert(!overwrote&&dispatches&&!memcmp(delivered,wire,sizeof(wire)));
    cJSON_Delete(root);
}
static void burst_delivery(void) {
    static uint8_t ep[8192],rx[8192],out[8192];
    const unsigned lengths[]={1,63,64,65,512,8191,8192};
    burst_mode=true;copy_pending=false;
    for(unsigned test=0;test<sizeof(lengths)/sizeof(*lengths);test++) {
        tu_edpt_stream_t s={.mps=64,.xfer_len=8192,.ep_buf=ep,.ep_addr=2};
        assert(tu_fifo_config(&s.ff,rx,sizeof(rx),false));
        busy=claimed=false;burst_rearm=0;
        assert(tu_edpt_stream_read_xfer(&s)==8192&&burst_rearm==8192);
        unsigned size=lengths[test];for(unsigned i=0;i<size;i++)ep[i]=(uint8_t)(i*13+test);
        busy=claimed=false;
        tu_edpt_stream_read_xfer_complete(&s,size);
        assert(tu_edpt_stream_read(&s,out,sizeof(out))==size&&!memcmp(out,ep,size));
        assert(burst_rearm==8192&&!tu_fifo_count(&s.ff));
    }
    burst_mode=false;
    puts("USB_RX_BURST_OK real TinyUSB FIFO, 8KiB rearm, short/exact/full packets, exact bytes and empty FIFO after batch drain");
}
int main(void){
    _CrtSetReportMode(_CRT_ASSERT,_CRTDBG_MODE_FILE);_CrtSetReportFile(_CRT_ASSERT,_CRTDBG_FILE_STDERR);
    for(unsigned i=0;i<64;i++){first[i]=(uint8_t)(i*7+11);next[i]=(uint8_t)(i*3+97);}
    unsigned cases=0;
    // An OUT transfer reserves at least one packet of free FIFO capacity.
    for(int occupied=64;occupied<=4096-64;occupied+=64){
        assert(!run(-1,1,6,8,occupied)); // old unpinned task: corruption
        assert(!run(1,1,8,8,occupied)); // equal priority is insufficient
        assert(run(TAB5_USB_IO_CORE,TAB5_USB_IO_CORE,TAB5_USB_RECEIVER_PRIORITY,TAB5_USB_STACK_PRIORITY,occupied));
        cases++;
    }
    metrics_dispatch(true);metrics_dispatch(false);burst_delivery();
    puts("USB_RX_RACE_OK 63 FIFO occupancy cases; real TinyUSB rearm/FIFO; cross-core and equal-priority corruption controls; same-core lower-priority receiver preserves bytes");
    puts("USB_METRICS_DISPATCH_OK overwritten packet keeps byte count but fails JSON/type dispatch (no ACK); serialized delivery preserves exact metrics line");
    return 0;
}
