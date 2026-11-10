/* SPDX-License-Identifier: Apache-2.0 */
#pragma once

#include <stdbool.h>
#include <stdint.h>

/* Shared by the display driver and its host ownership tests. Callers serialize
 * every access (including refresh callbacks) with the display's spinlock. */
typedef enum {
    TAB5_DIRECT_NORMAL,
    TAB5_DIRECT_CAMERA,
    TAB5_DIRECT_SWITCHING,
    TAB5_DIRECT_FAULT,
    TAB5_DIRECT_FROZEN,
} tab5_direct_mode_t;

typedef enum {
    TAB5_DIRECT_DMA_NONE,
    TAB5_DIRECT_DMA_WAITING,
    TAB5_DIRECT_DMA_DONE,
} tab5_direct_dma_t;

typedef struct {
    tab5_direct_mode_t mode;
    tab5_direct_dma_t normal_dma;
    uint32_t refresh_sequence, armed_sequence;
    unsigned front, submitted;
    bool armed;
} tab5_display_direct_state_t;

static inline bool tab5_direct_normal_submit(tab5_display_direct_state_t *s)
{
    if (s->mode != TAB5_DIRECT_NORMAL || s->normal_dma != TAB5_DIRECT_DMA_NONE) return false;
    s->normal_dma = TAB5_DIRECT_DMA_WAITING;
    return true;
}

/* Only a real ordinary transfer may deliver the ordinary flush semaphore.
 * The synchronous color callback of a direct framebuffer selection is ignored. */
static inline bool tab5_direct_color_done(tab5_display_direct_state_t *s)
{
    if (s->mode != TAB5_DIRECT_NORMAL || s->normal_dma != TAB5_DIRECT_DMA_WAITING) return false;
    s->normal_dma = TAB5_DIRECT_DMA_DONE;
    return true;
}

static inline bool tab5_direct_normal_consume(tab5_display_direct_state_t *s)
{
    if (s->normal_dma != TAB5_DIRECT_DMA_DONE) return false;
    s->normal_dma = TAB5_DIRECT_DMA_NONE;
    return true;
}

static inline bool tab5_direct_enter(tab5_display_direct_state_t *s)
{
    if (s->mode != TAB5_DIRECT_NORMAL || s->normal_dma != TAB5_DIRECT_DMA_NONE) return false;
    s->mode = TAB5_DIRECT_SWITCHING;
    s->submitted = 0;
    s->armed = false;
    return true;
}

static inline int tab5_direct_back(const tab5_display_direct_state_t *s)
{
    return s->mode == TAB5_DIRECT_CAMERA ? (int)(s->front ^ 1u) : -1;
}

static inline bool tab5_direct_prepare(tab5_display_direct_state_t *s, unsigned index)
{
    if ((s->mode != TAB5_DIRECT_CAMERA && s->mode != TAB5_DIRECT_SWITCHING &&
         s->mode != TAB5_DIRECT_FAULT) || index > 1) return false;
    s->mode = TAB5_DIRECT_SWITCHING;
    s->submitted = index;
    s->armed = false;
    return true;
}

/* Arm only AFTER draw_bitmap returns. Its cur_fb_index write can race the ISR's
 * earlier index read. The first subsequent callback can still describe the old
 * selection; a second callback proves that selection has stopped being read. */
static inline void tab5_direct_arm(tab5_display_direct_state_t *s)
{
    s->armed_sequence = s->refresh_sequence;
    s->armed = true;
}

static inline bool tab5_direct_refresh(tab5_display_direct_state_t *s)
{
    s->refresh_sequence++;
    return s->mode == TAB5_DIRECT_SWITCHING && s->armed;
}

static inline bool tab5_direct_complete(tab5_display_direct_state_t *s)
{
    if (s->mode != TAB5_DIRECT_SWITCHING || !s->armed ||
        (uint32_t)(s->refresh_sequence - s->armed_sequence) < 2u) return false;
    s->front = s->submitted;
    s->mode = TAB5_DIRECT_CAMERA;
    s->armed = false;
    return true;
}

static inline void tab5_direct_fault(tab5_display_direct_state_t *s)
{
    s->mode = TAB5_DIRECT_FAULT;
    s->armed = false;
}

static inline bool tab5_direct_leave(tab5_display_direct_state_t *s)
{
    if (s->mode != TAB5_DIRECT_CAMERA || s->normal_dma != TAB5_DIRECT_DMA_NONE) return false;
    s->mode = TAB5_DIRECT_NORMAL;
    return true;
}


// An immutable still does not need a new scanout buffer or a cache writeback.
// Only enter after the ordinary DMA token has been consumed. Live-camera
// operations cannot borrow a buffer until the still owner has released it.
static inline bool tab5_direct_freeze(tab5_display_direct_state_t *s)
{
    if (s->mode != TAB5_DIRECT_NORMAL || s->normal_dma != TAB5_DIRECT_DMA_NONE) return false;
    s->mode = TAB5_DIRECT_FROZEN;
    return true;
}

static inline bool tab5_direct_unfreeze(tab5_display_direct_state_t *s)
{
    if (s->mode != TAB5_DIRECT_FROZEN || s->normal_dma != TAB5_DIRECT_DMA_NONE) return false;
    s->mode = TAB5_DIRECT_NORMAL;
    return true;
}
