#pragma once
#include <stdatomic.h>
#include <stddef.h>
#include <stdbool.h>
#include <stdint.h>
#include <string.h>

// One HTTP producer and one authenticated-stream consumer. Unverified bytes
// remain here, never in a persisted checkpoint. Each task owns disjoint spans.
#define GALLERY_HTTP_READ_BYTES 16384u
#define GALLERY_HTTP_AHEAD_BYTES 65536u
typedef struct {
    uint8_t *bytes;
    size_t capacity;
    atomic_size_t produced,consumed;
    atomic_bool stop,failed,finished;
} gallery_read_pipeline;
static inline void gallery_read_init(gallery_read_pipeline *p,uint8_t *bytes,size_t capacity) {
    p->bytes=bytes;p->capacity=capacity;
    atomic_init(&p->produced,0);atomic_init(&p->consumed,0);
    atomic_init(&p->stop,false);atomic_init(&p->failed,false);atomic_init(&p->finished,false);
}
static inline size_t gallery_read_write_span(gallery_read_pipeline *p,uint8_t **out) {
    size_t write=atomic_load_explicit(&p->produced,memory_order_relaxed);
    size_t read=atomic_load_explicit(&p->consumed,memory_order_acquire);
    if(atomic_load(&p->stop)||write-read>=p->capacity)return 0;
    size_t at=write%p->capacity,n=p->capacity-(write-read);
    if(n>p->capacity-at)n=p->capacity-at;
    if(n>GALLERY_HTTP_READ_BYTES)n=GALLERY_HTTP_READ_BYTES;
    *out=p->bytes+at;return n;
}
static inline void gallery_read_publish(gallery_read_pipeline *p,size_t count) {
    atomic_fetch_add_explicit(&p->produced,count,memory_order_release);
}
static inline size_t gallery_read_take(gallery_read_pipeline *p,uint8_t *out,size_t count) {
    size_t read=atomic_load_explicit(&p->consumed,memory_order_relaxed);
    size_t write=atomic_load_explicit(&p->produced,memory_order_acquire),n=write-read;
    if(n>count)n=count;
    size_t at=read%p->capacity;
    if(n>p->capacity-at)n=p->capacity-at;
    if(n){memcpy(out,p->bytes+at,n);atomic_fetch_add_explicit(&p->consumed,n,memory_order_release);}
    return n;
}
// -2: still receiving, -1: transport failed after its buffered prefix, 0: EOF.
static inline int gallery_read_poll(gallery_read_pipeline *p,uint8_t *out,size_t count) {
    size_t n=gallery_read_take(p,out,count);if(n)return (int)n;
    if(!atomic_load_explicit(&p->finished,memory_order_acquire))return -2;
    // Final publication may have raced the first empty read.
    n=gallery_read_take(p,out,count);if(n)return (int)n;
    return atomic_load(&p->failed)?-1:0;
}
