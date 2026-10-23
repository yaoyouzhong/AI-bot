#pragma once
#include "lvgl.h"
#include <stdbool.h>
#include <stdint.h>

// Registration and begin/end run under the LVGL mutex. One camera task owns
// frame() until end(); controls are immutable until end(), and must be rebuilt
// after a display/touch direction change before the next begin().
void camera_viewport_set_controls(lv_obj_t *shutter, lv_obj_t *close);
// 1: acquired, 0: safe LVGL fallback, -1: display fault (end still required).
int camera_viewport_begin(lv_obj_t *canvas);
bool camera_viewport_frame(const uint16_t *source, bool (*keep)(void *), void *ctx);
bool camera_viewport_end(void);
// Freeze the already completed review frame while encoding/uploading. Call on
// an LVGL input/timer turn before starting work, never in draw/flush callbacks.
// Drain earlier normal DMA, then pause rendering without selecting a panel
// framebuffer or performing full-frame cache sync, copying or allocation.
// 1: held, 0: unavailable with no acquisition, -1: already held (end required).
int camera_viewport_still_begin(lv_obj_t *canvas);
// Release and update the destination UI in the same LVGL turn. Failure retains the
// display owner and suppresses ordinary writes; call again to retry recovery.
bool camera_viewport_still_end(void);
// Atomic observation for other tasks; it does not acquire the display owner.
bool camera_viewport_still_active(void);
// Pure diagnostics only. During a still hold, 0 means unknown/skipped, not
// exhausted memory. Outside that window, return the ordinary largest-block sample.
// This advisory check does not exclude a scan already racing with acquisition.
unsigned camera_viewport_diagnostic_largest(unsigned caps);
void camera_viewport_diagnostics(unsigned *presented, unsigned *compose_us,
                                 unsigned *present_us, unsigned *mode);
