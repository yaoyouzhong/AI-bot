#pragma once
#include <stdatomic.h>
#include <stddef.h>
#include <stdbool.h>

// The existing <=1 MiB JPEG is the bounded buffer. A verified prefix becomes
// immutable; network and card tasks own disjoint ranges, with two checkpoints
// of producer lead allowed. No second full JPEG or unbounded write queue.
typedef struct {
    atomic_size_t verified,written;
    atomic_bool closed,failed,finished;
} gallery_write_pipeline;
static inline void gallery_write_init(gallery_write_pipeline *p,size_t offset) {
    atomic_init(&p->verified,offset);atomic_init(&p->written,offset);
    atomic_init(&p->closed,false);atomic_init(&p->failed,false);atomic_init(&p->finished,false);
}
static inline size_t gallery_write_next(gallery_write_pipeline *p,size_t block) {
    size_t written=atomic_load(&p->written),verified=atomic_load(&p->verified);
    if(verified<=written||(!atomic_load(&p->closed)&&verified-written<block))return written;
    return verified-written>block?written+block:verified;
}
