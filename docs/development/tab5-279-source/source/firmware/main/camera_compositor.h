#pragma once
#include <stdbool.h>
#include <stdint.h>

#define CAMERA_COMPOSITOR_SOURCE_WIDTH 1280u
#define CAMERA_COMPOSITOR_SOURCE_HEIGHT 720u
#define CAMERA_COMPOSITOR_TARGET_WIDTH 720u
#define CAMERA_COMPOSITOR_TARGET_HEIGHT 1280u

typedef struct {
    // Straight-alpha ARGB8888, already rotated to native portrait coordinates.
    const uint32_t *pixels;
    uint32_t width, height;
    uint32_t stride; // Bytes per source row, including padding; multiple of 4.
    int32_t x, y;
} camera_overlay;

// Rotate packed 1280x720 RGB565 to packed 720x1280 RGB565, then blend overlays
// in array order. Every rectangle must fit the target. The caller provides
// the declared storage extents and holds all inputs stable throughout this
// call. Portrait must not overlap any input. No allocation, LVGL object APIs,
// DMA, PPA or display submission.
// yield_cb is optional, called after each of 20 source groups (64 columns).
// false cancels immediately. A false result means portrait MUST NOT be
// presented: cancellation can leave a partially written back buffer.
bool camera_composite_frame(uint16_t *portrait, const uint16_t *landscape,
                            const camera_overlay *overlays, unsigned count,
                            bool (*yield_cb)(void *), void *ctx);


// Portrait holds of the front-facing camera need the opposite sensor-to-panel turn.
bool camera_composite_frame_oriented(uint16_t *portrait,const uint16_t *landscape,bool clockwise,
                            const camera_overlay *overlays,unsigned count,
                            bool (*yield_cb)(void *),void *ctx);
