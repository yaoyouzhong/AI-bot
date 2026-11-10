#pragma once
#include <stdbool.h>
#include <stdint.h>
#include <stddef.h>
#include <string.h>

// Additive GATT mailbox command: 3, request-id:u32, next-offset:u16, credits:u8.
// The reader/writer retain their existing id, cursor, length and completion checks.
// Negotiated windows up to 32 bound host memory and queued mbufs. No waits
// or retries run on the NimBLE host task; failure terminates this window.
static inline bool ble_mailbox_window(const uint8_t *grant,size_t size,size_t capacity,
    size_t (*read_packet)(uint8_t *,size_t,void *),
    bool (*write_packet)(const uint8_t *,size_t,void *),
    bool (*notify_packet)(const uint8_t *,size_t,void *),void *ctx) {
    if(size!=8||grant[0]!=3||!grant[7]||grant[7]>32||capacity<20||capacity>488)return false;
    uint8_t ack[7],packet[488];memcpy(ack,grant,7);ack[0]=1;
    if(!write_packet(ack,sizeof(ack),ctx))return false;
    unsigned expected=grant[5]|((unsigned)grant[6]<<8);
    for(unsigned i=0;i<grant[7];i++) {
        size_t n=read_packet(packet,capacity,ctx);
        if(n<=8||n>capacity||memcmp(packet,grant+1,4))return false;
        unsigned offset=packet[4]|((unsigned)packet[5]<<8),total=packet[6]|((unsigned)packet[7]<<8);
        if(offset!=expected||offset+n-8>total)return false;
        if(!notify_packet(packet,n,ctx))return false;
        expected=offset+n-8;
        if(expected==total||i+1==grant[7])return true;
        ack[5]=expected;ack[6]=expected>>8;
        if(!write_packet(ack,sizeof(ack),ctx))return false;
    }
    return true;
}
