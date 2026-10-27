#undef NDEBUG
#include <assert.h>
#include <stdint.h>
#include <stdbool.h>
#include <string.h>
#include <stdio.h>
#include "../../firmware/main/rpc_mailbox.inc"
int main(void) {
    uint8_t outgoing[RPC_REQUEST_MAX],incoming[RPC_RESPONSE_MAX],fragment[16393];
    for(unsigned i=0;i<sizeof(outgoing);i++)outgoing[i]=(uint8_t)i;
    unsigned chunks[]={11,235,479,480,2048,16384};
    for(unsigned index=0;index<sizeof(chunks)/sizeof(chunks[0]);index++) {
        unsigned chunk=chunks[index];
        rpc_mailbox m={.out=outgoing,.in=incoming,.active=42,.size=sizeof(outgoing)};
        unsigned at=0;
        while(at<m.size) {
            size_t n=rpc_read(&m,fragment,chunk+8);assert(rpc_u32(fragment)==42&&rpc_u16(fragment+4)==at);
            assert(!memcmp(fragment+8,outgoing+at,n-8));
            uint8_t ack[7]={1};rpc_put32(ack+1,42);rpc_put16(ack+5,at+1);assert(!rpc_write(&m,ack,7));
            at+=n-8;rpc_put16(ack+5,at);assert(rpc_write(&m,ack,7));
        }
        for(at=0;at<RPC_RESPONSE_MAX;) {
            unsigned n=RPC_RESPONSE_MAX-at;if(n>chunk)n=chunk;
            fragment[0]=2;rpc_put32(fragment+1,42);rpc_put16(fragment+5,at);rpc_put16(fragment+7,RPC_RESPONSE_MAX);
            memset(fragment+9,0x3b,n);
            rpc_put32(fragment+1,41);assert(!rpc_write(&m,fragment,n+9));rpc_put32(fragment+1,42);
            rpc_put16(fragment+5,at+1);assert(!rpc_write(&m,fragment,n+9));rpc_put16(fragment+5,at);
            assert(rpc_write(&m,fragment,n+9));assert(!rpc_write(&m,fragment,n+9));at+=n;
        }
        assert(m.complete&&m.received==RPC_RESPONSE_MAX);
        for(at=0;at<RPC_RESPONSE_MAX;at++)assert(incoming[at]==0x3b);
        m.active=0;assert(!rpc_write(&m,fragment,20));assert(rpc_read(&m,fragment,sizeof(fragment))==8&&!rpc_u32(fragment));
    }
    // A lost write command in a 32/64-fragment receive window cannot be hidden by
    // the final acknowledged write; every later offset remains rejected.
    unsigned drops[]={0,7,15,30,31,32,62,63};
    for(unsigned window=32;window<=64;window*=2)for(unsigned d=0;d<sizeof(drops)/sizeof(drops[0]);d++) {
        if(drops[d]>=window)continue;
        rpc_mailbox m={.out=outgoing,.in=incoming,.active=42,.size=124,.offered=124};
        for(unsigned i=0;i<window;i++) {
            if(i==drops[d])continue;
            fragment[0]=2;rpc_put32(fragment+1,42);rpc_put16(fragment+5,i*479);rpc_put16(fragment+7,window*479);
            memset(fragment+9,0x3b,479);
            assert(rpc_write(&m,fragment,488)==(i<drops[d]));
        }
        assert(!m.complete&&m.received==drops[d]*479);
        // BLE alone negotiates byte-exact replay. Repeat the window with
        // confirmed writes: old prefix is accepted, the gap is filled, and
        // completion still requires every byte. USB remains strict above.
        for(unsigned i=0;i<window;i++) {
            fragment[0]=2;rpc_put32(fragment+1,42);rpc_put16(fragment+5,i*479);rpc_put16(fragment+7,window*479);
            memset(fragment+9,0x3b,479);
            unsigned previous=m.received;
            assert(rpc_write_with_replay(&m,fragment,488,true));
            assert(m.received==(i<drops[d]?previous:(i+1)*479));
            assert(m.complete==(i+1==window));
            // A repeated fragment cannot change the accepted response.
            fragment[9]^=1;assert(!rpc_write_with_replay(&m,fragment,488,true));fragment[9]^=1;
            rpc_put32(fragment+1,41);assert(!rpc_write_with_replay(&m,fragment,488,true));rpc_put32(fragment+1,42);
            rpc_put16(fragment+7,window*479+1);assert(!rpc_write_with_replay(&m,fragment,488,true));rpc_put16(fragment+7,window*479);
            assert(rpc_write_with_replay(&m,fragment,488,true));
        }
        assert(m.complete&&m.received==window*479);
        for(unsigned i=0;i<window*479;i++)assert(incoming[i]==0x3b);
        m.active=0;assert(!rpc_write_with_replay(&m,fragment,488,true));
    }
    puts("RPC_MAILBOX_OK USB strict duplicates; BLE exact replay repairs dropped window32/64; stale/id/changed bytes/total rejected; complete only after all bytes");
}
