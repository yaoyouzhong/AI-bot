#pragma once
#include "codex_companion.h"
#define CLAUDE_COMPANION_FRAME_BYTES (40 * 33 * 3)
typedef struct {
    unsigned frame, width, height, anchor_x2, scale_q8, anchor_y2, saver_scale, saver_scale_q8, saver_lift;
} claude_companion_frame_info;
bool claude_companion_asset_available(void);
unsigned claude_companion_frame_count(void);
claude_companion_frame_info claude_companion_frame(codex_companion_state state, int64_t elapsed, bool repeat_completion);
bool claude_companion_decode(unsigned frame, uint8_t *output, size_t capacity, unsigned *width, unsigned *height);
