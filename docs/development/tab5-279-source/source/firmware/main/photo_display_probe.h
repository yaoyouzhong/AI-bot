#pragma once
#include <stdint.h>

// Passive diagnostic, not a display fix. Every entry point leaves hardware
// configuration unchanged; begin/frame/end only sample existing registers.
enum {
    PHOTO_DISPLAY_PROBE_CLICK=1, PHOTO_DISPLAY_PROBE_FROZEN,
    PHOTO_DISPLAY_PROBE_PREVIEW, PHOTO_DISPLAY_PROBE_ENCODE,
    PHOTO_DISPLAY_PROBE_PACK, PHOTO_DISPLAY_PROBE_CRYPT,
    PHOTO_DISPLAY_PROBE_HASH, PHOTO_DISPLAY_PROBE_NETWORK,
    PHOTO_DISPLAY_PROBE_UNFREEZE, PHOTO_DISPLAY_PROBE_DONE
};
// CLICK may reset timing while inactive, without touching hardware. Other
// marks are ignored outside the successfully frozen camera-photo window.
void photo_display_probe_mark(unsigned stage);
void photo_display_probe_begin(void);
void photo_display_probe_end(void);
// Called only from the existing IRAM frame ISR; no PSRAM/framebuffer access.
// Only complete intervals between two samples in this active window count.
// Unsigned time subtraction supports a timer wrap between consecutive samples.
void photo_display_probe_frame(uint32_t now_us);
// pd,1 keeps its original 32 fields, then appends max_frame_interval_us and
// frame_intervals_over_18ms (strictly >18000us, saturating at UINT32_MAX).
void photo_display_probe_format(char *out,unsigned size);
