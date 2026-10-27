#pragma once
#include "lvgl.h"

// LVGL-thread-only: no widget or input replacement, no LCD buffer ownership.
void tab5_draw_cache_init(void);
void tab5_draw_cache_step(lv_obj_t *obj);
void tab5_draw_cache_step_budget(lv_obj_t *obj,unsigned pixels);
// Refresh a dirty part of an existing complete image, without allocating or
// starting a new full-page capture while a touch/scroll is in progress.
void tab5_draw_cache_repair(lv_obj_t *obj);
void tab5_draw_cache_drop(void);
// Release optional offscreen snapshots only, between LVGL render passes.
// Visible pages and active input gestures keep their snapshots.
unsigned tab5_draw_cache_trim_hidden(unsigned bytes);
bool tab5_draw_cache_is_ready(const lv_obj_t *obj);
// A complete previous revision remains usable during a horizontal gesture.
// Idle scheduling must distinguish it from a page that has never been captured.
bool tab5_draw_cache_has_image(const lv_obj_t *obj);
extern volatile unsigned tab5_cache_hits,tab5_cache_builds,tab5_cache_bytes;
extern volatile unsigned tab5_cache_snapshot_count,tab5_native_snapshot_count,tab5_cache_restart_count;

// LVGL-thread interaction: 0 = normal, 1 = pause capture (live button press),
// 2 = pause capture and display the last complete snapshot (horizontal drag).
extern int (*tab5_draw_cache_interaction)(const lv_obj_t *);

// Optional quarter-turn cache for the opaque horizontal main-page strip.
// Rendering uses LVGL's normal partial buffers; the port consumes the marker
// before submitting that buffer through its existing DMA completion path.
extern lv_obj_t *tab5_draw_cache_native_strip;
bool tab5_draw_cache_native_render(lv_layer_t *layer);
bool tab5_draw_cache_take_native(const void *pixels,const lv_area_t *area);
extern volatile unsigned tab5_native_cache_hits,tab5_native_cache_bytes;

unsigned tab5_draw_cache_reclaim_staging(void);
