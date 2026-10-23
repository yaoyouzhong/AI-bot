#include "photo_display_probe.h"
#include "esp_attr.h"
#include "esp_timer.h"
#include "freertos/FreeRTOS.h"
#include "soc/mipi_dsi_bridge_struct.h"
#include "soc/mipi_dsi_host_struct.h"
#include <stdbool.h>
#include <stdio.h>
#include <string.h>

// This state and its lock stay in internal RAM even when photo workers use a
// PSRAM stack. The frame ISR neither follows pointers nor touches image data.
typedef struct {
    bool active, pending, fill_unchanged, frame_seen;
    unsigned stage;
    uint32_t sequence, click_us, stage_us, elapsed_us, dwell_us[11];
    uint32_t seen, error_stages, frames, error_frames;
    uint32_t bridge_raw, bridge_status, bridge_enable, host0, host1;
    uint32_t host0_before, host1_before, fill_before, fill_after;
    uint32_t pixel_type, discard_vcnt;
    uint32_t frame_start_us, frame_previous_us, frame_interval_max_us, frame_intervals_over_18ms;
} probe_state;
static DRAM_ATTR probe_state probe;
static DRAM_ATTR portMUX_TYPE probe_lock=portMUX_INITIALIZER_UNLOCKED;

static void start_click(uint32_t now) {
    uint32_t sequence=probe.sequence+1;
    memset(&probe,0,sizeof(probe));
    probe.sequence=sequence;probe.pending=true;
    probe.click_us=probe.stage_us=now;probe.stage=PHOTO_DISPLAY_PROBE_CLICK;
    probe.seen=1u<<PHOTO_DISPLAY_PROBE_CLICK;
}
static void change_stage(unsigned stage,uint32_t now) {
    if(probe.stage!=stage) {
        probe.dwell_us[probe.stage]+=now-probe.stage_us;
        probe.stage_us=now;probe.stage=stage;
    }
    probe.elapsed_us=now-probe.click_us;probe.seen|=1u<<stage;
}
void photo_display_probe_mark(unsigned stage) {
    if(stage<PHOTO_DISPLAY_PROBE_CLICK||stage>PHOTO_DISPLAY_PROBE_DONE)return;
    portENTER_CRITICAL(&probe_lock);
    uint32_t now=(uint32_t)esp_timer_get_time();
    if(stage==PHOTO_DISPLAY_PROBE_CLICK) {
        // A rejected duplicate click must not discard the current observation window.
        if(!probe.active)start_click(now);
    } else if(probe.active)change_stage(stage,now);
    portEXIT_CRITICAL(&probe_lock);
}
void photo_display_probe_begin(void) {
    portENTER_CRITICAL(&probe_lock);
    uint32_t now=(uint32_t)esp_timer_get_time();
    if(!probe.active) {
        if(!probe.pending)start_click(now);
        probe.fill_before=MIPI_DSI_BRIDGE.dpi_rsv_dpi_data.val;
        probe.pixel_type=MIPI_DSI_BRIDGE.pixel_type.val;
        probe.discard_vcnt=MIPI_DSI_BRIDGE.dpi_misc_config.fifo_underrun_discard_vcnt;
        // The generated Host header only promises RO, not whether a read can
        // acknowledge a latch. Sample once here and once per frame/end; never
        // in the formatter. Preserve the initial bits separately, so old
        // sticky status is not presented as a newly counted hardware event.
        probe.host0_before=MIPI_DSI_HOST.int_st0.val;
        probe.host1_before=MIPI_DSI_HOST.int_st1.val;
        probe.frame_start_us=now;
        probe.active=true;probe.pending=false;
        change_stage(PHOTO_DISPLAY_PROBE_FROZEN,now);
    }
    portEXIT_CRITICAL(&probe_lock);
}
// Bridge RAW is WTC, with a separate INT_CLR register. Reads do not clear it.
// Leave interrupt enables, discard_vcnt and all clears to the SDK. A RAW bit
// suppressed by hardware before it latches cannot be recovered by this probe.
// Counts below mean samples containing status bits, NOT distinct HW events.
static void IRAM_ATTR sample_status(bool frame) {
    uint32_t raw=MIPI_DSI_BRIDGE.int_raw.val;
    uint32_t status=MIPI_DSI_BRIDGE.int_st.val;
    uint32_t enabled=MIPI_DSI_BRIDGE.int_ena.val;
    uint32_t host0=MIPI_DSI_HOST.int_st0.val,host1=MIPI_DSI_HOST.int_st1.val;
    probe.bridge_raw|=raw;probe.bridge_status|=status;probe.bridge_enable|=enabled;
    probe.host0|=host0;probe.host1|=host1;
    if(frame)probe.frames++;
    if((raw&1u)||(status&1u)||host0||host1) {
        if(frame)probe.error_frames++;
        probe.error_stages|=1u<<probe.stage;
    }
}
void IRAM_ATTR photo_display_probe_frame(uint32_t now_us) {
    portENTER_CRITICAL_ISR(&probe_lock);
    // The ISR timestamp precedes this lock: reject a first sample taken just
    // before begin on another core. A first sample otherwise only establishes
    // the baseline; neither the leading nor trailing partial interval counts.
    if(probe.active && (probe.frame_seen || (int32_t)(now_us-probe.frame_start_us)>=0)) {
        if(probe.frame_seen) {
            uint32_t interval=now_us-probe.frame_previous_us;
            if(interval>probe.frame_interval_max_us)probe.frame_interval_max_us=interval;
            if(interval>18000 && probe.frame_intervals_over_18ms<UINT32_MAX)
                probe.frame_intervals_over_18ms++;
        }
        probe.frame_previous_us=now_us;probe.frame_seen=true;
        sample_status(true);probe.elapsed_us=now_us-probe.click_us;
    }
    portEXIT_CRITICAL_ISR(&probe_lock);
}
void photo_display_probe_end(void) {
    portENTER_CRITICAL(&probe_lock);
    uint32_t now=(uint32_t)esp_timer_get_time();
    if(probe.active) {
        sample_status(false);
        change_stage(PHOTO_DISPLAY_PROBE_UNFREEZE,now);
        probe.fill_after=MIPI_DSI_BRIDGE.dpi_rsv_dpi_data.val;
        probe.fill_unchanged=probe.fill_after==probe.fill_before;
        change_stage(PHOTO_DISPLAY_PROBE_DONE,now);
        probe.active=false;
    }
    portEXIT_CRITICAL(&probe_lock);
}
void photo_display_probe_format(char *out,unsigned size) {
    if(!out||!size)return;
    probe_state snapshot;
    portENTER_CRITICAL(&probe_lock);
    uint32_t now=(uint32_t)esp_timer_get_time();
    snapshot=probe;
    portEXIT_CRITICAL(&probe_lock);
    if(snapshot.active) {
        snapshot.dwell_us[snapshot.stage]+=now-snapshot.stage_us;
        snapshot.elapsed_us=now-snapshot.click_us;
    }
    // pd v1: sequence,active,stage,total_ms,seen,error_stages,frames,error_frames;
    // raw,masked,enable,host0,host1,initial_host0,initial_host1,fill_before,fill_end;
    // fill_unchanged,pixel_type,discard_vcnt; ten stage dwell times in milliseconds.
    // Keep pd,1 field positions: since .261 the former restored flag only compares
    // the two observed fill values. This probe never changes hardware config.
    // Repeated crypto/hash/network chunks accumulate under their stage ID.
    // Since .262, append max frame interval in us and count of intervals >18ms.
    // The first 32 comma-separated fields retain their original positions.
    int used=snprintf(out,size,"pd,1,%u,%u,%u,%u,%03x,%03x,%u,%u,%08x,%08x,%08x,%08x,%08x,%08x,%08x,%08x,%08x,%u,%08x,%u",
        (unsigned)snapshot.sequence,(unsigned)snapshot.active,snapshot.stage,(unsigned)(snapshot.elapsed_us/1000),
        (unsigned)snapshot.seen,(unsigned)snapshot.error_stages,(unsigned)snapshot.frames,(unsigned)snapshot.error_frames,
        (unsigned)snapshot.bridge_raw,(unsigned)snapshot.bridge_status,(unsigned)snapshot.bridge_enable,
        (unsigned)snapshot.host0,(unsigned)snapshot.host1,(unsigned)snapshot.host0_before,(unsigned)snapshot.host1_before,
        (unsigned)snapshot.fill_before,(unsigned)snapshot.fill_after,(unsigned)snapshot.fill_unchanged,
        (unsigned)snapshot.pixel_type,(unsigned)snapshot.discard_vcnt);
    for(unsigned stage=1;stage<=PHOTO_DISPLAY_PROBE_DONE&&used>=0&&(unsigned)used<size;stage++) {
        int appended=snprintf(out+used,size-(unsigned)used,",%u",(unsigned)(snapshot.dwell_us[stage]/1000));
        if(appended<0)break;
        used+=appended;
    }
    if(used>=0 && (unsigned)used<size)
        snprintf(out+used,size-(unsigned)used,",%u,%u",
            (unsigned)snapshot.frame_interval_max_us,(unsigned)snapshot.frame_intervals_over_18ms);
}
