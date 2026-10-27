#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include "../../firmware/main/ble_mailbox_window.h"
#include "../../firmware/main/ble_mailbox_paced.h"
#include "../../firmware/main/rpc_mailbox.inc"
static uint8_t outgoing[RPC_REQUEST_MAX],incoming[RPC_RESPONSE_MAX];
static rpc_mailbox m;
static unsigned at,notifications,fail_notification;
static bool has_room;
static bool bounded_queue;
static unsigned pending,peak;
static bool room(void *ctx){(void)ctx;return has_room&&(!bounded_queue||pending<8);}
static size_t read_packet(uint8_t *out,size_t capacity,void *ctx){(void)ctx;return rpc_read(&m,out,capacity);}
static bool write_packet(const uint8_t *data,size_t size,void *ctx){(void)ctx;return rpc_write(&m,data,size);}
static bool notify_packet(const uint8_t *data,size_t size,void *ctx) {
    (void)ctx;notifications++;if(notifications==fail_notification)return false;
    if(bounded_queue){pending++;if(pending>peak)peak=pending;assert(pending<=8);}
    assert(size>8&&size<=488&&rpc_u32(data)==42&&rpc_u16(data+4)==at&&rpc_u16(data+6)==sizeof(outgoing));
    assert(!memcmp(data+8,outgoing+at,size-8));at+=size-8;return true;
}
int main(void) {
    for(unsigned i=0;i<sizeof(outgoing);i++)outgoing[i]=(uint8_t)i;
    unsigned capacities[]={20,244,488};uint8_t first[488],grant[8]={3};rpc_put32(grant+1,42);grant[7]=4;
    for(unsigned credits=4;credits<=32;credits*=2)for(unsigned i=0;i<3;i++) {
        grant[7]=credits;
        m=(rpc_mailbox){.out=outgoing,.in=incoming,.active=42,.size=sizeof(outgoing)};
        notifications=fail_notification=0;at=rpc_read(&m,first,sizeof(first))-8;
        while(at<sizeof(outgoing)) {
            rpc_put16(grant+5,at);unsigned previous=notifications;
            assert(ble_mailbox_window(grant,8,capacities[i],read_packet,write_packet,notify_packet,NULL));
            assert(notifications>previous&&notifications-previous<=credits);
        }
        assert(m.offered==sizeof(outgoing));
        uint8_t response[37]={2};rpc_put32(response+1,42);rpc_put16(response+7,28);
        assert(rpc_write(&m,response,sizeof(response))&&m.complete);
    }
    m=(rpc_mailbox){.out=outgoing,.in=incoming,.active=42,.size=sizeof(outgoing)};
    at=rpc_read(&m,first,sizeof(first))-8;rpc_put16(grant+5,at);grant[7]=33;
    assert(!ble_mailbox_window(grant,8,488,read_packet,write_packet,notify_packet,NULL));grant[7]=4;
    grant[1]=41;assert(!ble_mailbox_window(grant,8,488,read_packet,write_packet,notify_packet,NULL));grant[1]=42;
    rpc_put16(grant+5,at+1);assert(!ble_mailbox_window(grant,8,488,read_packet,write_packet,notify_packet,NULL));rpc_put16(grant+5,at);
    notifications=0;fail_notification=2;
    assert(!ble_mailbox_window(grant,8,488,read_packet,write_packet,notify_packet,NULL));assert(!m.complete&&notifications==2);
    for(unsigned i=0;i<3;i++) {
        ble_mailbox_paced paced={0};grant[7]=32;
        m=(rpc_mailbox){.out=outgoing,.in=incoming,.active=42,.size=sizeof(outgoing)};
        at=rpc_read(&m,first,sizeof(first))-8;notifications=fail_notification=0;
        while(at<sizeof(outgoing)) {
            rpc_put16(grant+5,at);
            assert(ble_mailbox_paced_start(&paced,grant,8,capacities[i],write_packet,NULL));
            assert(!ble_mailbox_paced_start(&paced,grant,8,capacities[i],write_packet,NULL));
            has_room=false;unsigned before=at;
            assert(ble_mailbox_paced_step(&paced,room,read_packet,write_packet,notify_packet,NULL)==1&&at==before);
            has_room=true;
            while(paced.remaining) {
                unsigned count=notifications;
                assert(ble_mailbox_paced_step(&paced,room,read_packet,write_packet,notify_packet,NULL)>=0);
                assert(notifications-count<=8);
            }
        }
        assert(at==sizeof(outgoing));
        m=(rpc_mailbox){.out=outgoing,.in=incoming,.active=42,.size=sizeof(outgoing)};
        at=rpc_read(&m,first,sizeof(first))-8;rpc_put16(grant+5,at);
        notifications=0;fail_notification=2;
        assert(ble_mailbox_paced_start(&paced,grant,8,capacities[i],write_packet,NULL));
        assert(ble_mailbox_paced_step(&paced,room,read_packet,write_packet,notify_packet,NULL)==-1&&!paced.remaining&&notifications==2);
        assert(ble_mailbox_paced_step(&paced,room,read_packet,write_packet,notify_packet,NULL)==0&&notifications==2);
    }
    // Filling eight slots in one tick removes an unnecessary half-window
    // wakeup, while a full host queue still prevents any further submission.
    m=(rpc_mailbox){.out=outgoing,.in=incoming,.active=42,.size=sizeof(outgoing)};
    at=rpc_read(&m,first,sizeof(first))-8;rpc_put16(grant+5,at);grant[7]=32;
    ble_mailbox_paced paced={0};bounded_queue=has_room=true;pending=peak=notifications=fail_notification=0;
    assert(ble_mailbox_paced_start(&paced,grant,8,488,write_packet,NULL));
    assert(ble_mailbox_paced_step(&paced,room,read_packet,write_packet,notify_packet,NULL)==1&&pending==8&&notifications==8);
    unsigned before=at;
    assert(ble_mailbox_paced_step(&paced,room,read_packet,write_packet,notify_packet,NULL)==1&&at==before&&notifications==8);
    while(paced.remaining){pending=0;assert(ble_mailbox_paced_step(&paced,room,read_packet,write_packet,notify_packet,NULL)>=0);}
    assert(peak==8&&notifications==32&&at==480+32*480);
    puts("BLE_MAILBOX_WINDOW_OK actual RPC core; MTU 23/247/517; eight-packet tick and queue bound; full-queue backpressure; exact bytes; stale/skip/overflow/notify failure rejected");
}
