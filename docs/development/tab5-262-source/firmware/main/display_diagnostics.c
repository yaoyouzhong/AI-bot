#include "display_diagnostics.h"
#include "display_bandwidth.h"
#include "photo_display_probe.h"
#include "draw_orthogonal.h"
#include "crash_record.h"
#include "esp_attr.h"
#include "esp_timer.h"
#include "esp_heap_caps.h"
#include "esp_task_wdt.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include <stdint.h>
#include <stdio.h>
#include <string.h>

static volatile uint32_t frame_count, frame_previous_us, frame_peak_us;
static volatile uint32_t frame_gap_count, frame_gap_at_us;
static volatile uint32_t flash_count, flash_start_us, flash_peak_us, flash_end_us;
static volatile uint32_t watchdog_count, watchdog_at_us;
static char watchdog_task[2][configMAX_TASK_NAME_LEN];
extern volatile unsigned tab5_display_wait_us, tab5_display_wait_peak_us;
extern volatile unsigned tab5_lvgl_handler_us, tab5_lvgl_handler_peak_us;
extern volatile unsigned tab5_display_render_us, tab5_display_frame_flush_us, tab5_display_frame_rotate_us;
extern volatile unsigned tab5_draw_image_us, tab5_draw_fill_us, tab5_draw_label_us, tab5_draw_blend_us;
extern volatile unsigned tab5_full_image_frames;
extern volatile unsigned tab5_ppa_fills,tab5_ppa_errors,tab5_ppa_ready;
extern volatile unsigned tab5_rotate_choice,tab5_rotate_baseline_us,tab5_rotate_candidate_us;
extern volatile unsigned tab5_ui_internal_buffer_bytes,tab5_ui_internal_free;
extern volatile unsigned tab5_cache_hits,tab5_cache_builds,tab5_cache_bytes;
extern volatile unsigned tab5_native_cache_hits,tab5_native_cache_bytes;
extern volatile unsigned tab5_cache_snapshot_count,tab5_native_snapshot_count,tab5_cache_restart_count;
extern volatile unsigned tab5_display_large_frames,tab5_display_large_us,tab5_display_large_peak_us;
extern volatile unsigned tab5_display_large_total_ms,tab5_display_large_pixels,tab5_display_large_period_us;
extern volatile unsigned tab5_copy_choice,tab5_copy_baseline_us,tab5_copy_candidate_us;
extern volatile unsigned tab5_display_large_flush_us,tab5_display_large_rotate_us;
extern volatile unsigned tab5_display_large_copy_us,tab5_display_large_wait_us,tab5_display_large_yield_us;
extern volatile unsigned tab5_display_large_parts,tab5_display_large_native_parts;
extern volatile unsigned tab5_gallery_reader_clicks,tab5_gallery_reader_guide_us,tab5_gallery_reader_layout_us,tab5_gallery_reader_paint_us;

// IDF's non-panic watchdog prints from an ISR every 5 seconds if a core is
// starved. Record whether those warnings coincide with the observed flashes;
// do not feed, disable, reconfigure, or suppress the watchdog.
void esp_task_wdt_isr_user_handler(void) {
    watchdog_count++;
    watchdog_at_us = (uint32_t)esp_timer_get_time();
    for (unsigned core = 0; core < 2; ++core) {
        const char *name = pcTaskGetName(xTaskGetCurrentTaskHandleForCore(core));
        unsigned i = 0;
        for (; name[i] && i + 1 < sizeof(watchdog_task[core]); ++i) {
            char c = name[i];
            watchdog_task[core][i] = ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                                     (c >= '0' && c <= '9') || c == '_') ? c : '_';
        }
        watchdog_task[core][i] = 0;
    }
}

// Called from the DPI DMA ISR before its normal rearm. No allocation, logging,
// RTOS calls, or flash-resident data may be used here or in the flash hooks.
void IRAM_ATTR tab5_lcd_frame_tick(void) {
    uint32_t now = (uint32_t)esp_timer_get_time();
    if (frame_count) {
        uint32_t gap = now - frame_previous_us;
        if (gap > frame_peak_us) frame_peak_us = gap;
        if (gap > 50000) { frame_gap_count++; frame_gap_at_us = now; }
    }
    frame_previous_us = now;
    frame_count++;
    photo_display_probe_frame(now);
}

void IRAM_ATTR tab5_flash_guard_begin(void) {
    crash_record_checkpoint(RUNTIME_FLASH,1,0);
    flash_start_us = (uint32_t)esp_timer_get_time();
    flash_count++;
}

void IRAM_ATTR tab5_flash_guard_end(void) {
    uint32_t now = (uint32_t)esp_timer_get_time();
    uint32_t duration = now - flash_start_us;
    if (duration > flash_peak_us) flash_peak_us = duration;
    flash_end_us = now;
    crash_record_checkpoint(RUNTIME_FLASH,0,0);
}

void display_diagnostics_format(char *out, unsigned size) {
    snprintf(out, size, "%u,%u,%u,%u,%u,%u,%u,%u,%u,%s,%s,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u", (unsigned)frame_count,
             (unsigned)frame_peak_us, (unsigned)frame_gap_count, (unsigned)frame_gap_at_us,
             (unsigned)flash_count, (unsigned)flash_peak_us, (unsigned)flash_end_us,
             (unsigned)watchdog_count, (unsigned)watchdog_at_us, watchdog_task[0], watchdog_task[1],
             tab5_display_wait_us, tab5_display_wait_peak_us, tab5_lvgl_handler_us, tab5_lvgl_handler_peak_us,
             tab5_display_render_us, tab5_display_frame_flush_us, tab5_display_frame_rotate_us,
             tab5_draw_image_us,tab5_draw_fill_us,tab5_draw_label_us,tab5_draw_blend_us,tab5_full_image_frames,
             tab5_ppa_fills,tab5_ppa_errors,tab5_ppa_ready,
             tab5_rotate_choice,tab5_rotate_baseline_us,tab5_rotate_candidate_us,
             tab5_cache_hits,tab5_cache_builds,tab5_cache_bytes,
             tab5_display_large_frames,tab5_display_large_us,tab5_display_large_peak_us,
             tab5_display_large_total_ms,tab5_display_large_pixels,tab5_display_large_period_us);
    unsigned used=(unsigned)strlen(out);
    if(used<size)snprintf(out+used,size-used,",%u,%u,%u,%u,%u,%u,%u",tab5_copy_choice,tab5_copy_baseline_us,tab5_copy_candidate_us,
        tab5_display_large_flush_us,tab5_display_large_rotate_us,tab5_ui_internal_buffer_bytes,tab5_ui_internal_free);
    used=(unsigned)strlen(out);
    if(used<size)snprintf(out+used,size-used,",%u,%u",tab5_native_cache_hits,tab5_native_cache_bytes);
    used=(unsigned)strlen(out);
    if(used<size)snprintf(out+used,size-used,",%u,%u,%u,%u,%u",tab5_display_large_copy_us,
        tab5_display_large_wait_us,tab5_display_large_yield_us,tab5_display_large_parts,tab5_display_large_native_parts);
    used=(unsigned)strlen(out);
    if(used<size)snprintf(out+used,size-used,",%u,%u,%u,%u",tab5_cache_snapshot_count,
        tab5_native_snapshot_count,tab5_cache_restart_count,(unsigned)heap_caps_get_free_size(MALLOC_CAP_SPIRAM));
    used=(unsigned)strlen(out);
    if(used+1<size){out[used++]=',';crash_record_previous_runtime(out+used,size-used);}
    used=(unsigned)strlen(out);
    // Optional versioned suffix; preserve every earlier field and crash record.
    if(used<size)snprintf(out+used,size-used,";ortho,1,%u,%u,%u,%u",tab5_orthogonal_calls,
        tab5_orthogonal_pixels,tab5_orthogonal_us,tab5_orthogonal_peak_us);
    used=(unsigned)strlen(out);
    if(used<size)snprintf(out+used,size-used,";reader,1,%u,%u,%u,%u",tab5_gallery_reader_clicks,
        tab5_gallery_reader_guide_us,tab5_gallery_reader_layout_us,tab5_gallery_reader_paint_us);
    used=(unsigned)strlen(out);
    if(used+1<size){out[used++]=';';display_bandwidth_format(out+used,size-used);}
}
