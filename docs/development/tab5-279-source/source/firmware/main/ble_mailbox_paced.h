#pragma once
#include <stdbool.h>
#include <stdint.h>
#include <stddef.h>
#include <string.h>

// Host-queue-owned state. A grant is accepted once; each tick submits at most
// eight packets and leaves eight mbufs worth of queue occupancy as the bound.
typedef struct {
    uint8_t grant[8],packet[488];
    size_t capacity;
    unsigned expected,remaining;
} ble_mailbox_paced;
static inline bool ble_mailbox_paced_start(ble_mailbox_paced *s,const uint8_t *grant,size_t size,size_t capacity,
    bool (*write_packet)(const uint8_t *,size_t,void *),void *ctx) {
    if(s->remaining||size!=8||grant[0]!=3||!grant[7]||grant[7]>32||capacity<20||capacity>488)return false;
    uint8_t ack[7];memcpy(ack,grant,7);ack[0]=1;
    if(!write_packet(ack,sizeof(ack),ctx))return false;
    memcpy(s->grant,grant,8);s->capacity=capacity;
    s->expected=grant[5]|((unsigned)grant[6]<<8);s->remaining=grant[7];return true;
}
// 0=finished, 1=yield/retry before submission, -1=fatal. A failed notification
// may already have sent an ACL fragment, so it is never silently retransmitted.
static inline int ble_mailbox_paced_step(ble_mailbox_paced *s,
    bool (*room)(void *),size_t (*read_packet)(uint8_t *,size_t,void *),
    bool (*write_packet)(const uint8_t *,size_t,void *),
    bool (*notify_packet)(const uint8_t *,size_t,void *),void *ctx) {
    for(unsigned i=0;i<8&&s->remaining;i++) {
        if(!room(ctx))return 1;
        size_t n=read_packet(s->packet,s->capacity,ctx);
        if(n<=8||n>s->capacity||memcmp(s->packet,s->grant+1,4))goto fail;
        unsigned offset=s->packet[4]|((unsigned)s->packet[5]<<8),total=s->packet[6]|((unsigned)s->packet[7]<<8);
        if(offset!=s->expected||offset+n-8>total||!notify_packet(s->packet,n,ctx))goto fail;
        s->expected+=n-8;s->remaining--;
        if(s->expected==total||!s->remaining){s->remaining=0;return 0;}
        uint8_t ack[7];memcpy(ack,s->grant,7);ack[0]=1;ack[5]=s->expected;ack[6]=s->expected>>8;
        if(!write_packet(ack,sizeof(ack),ctx))goto fail;
    }
    return s->remaining?1:0;
fail:
    s->remaining=0;return -1;
}
