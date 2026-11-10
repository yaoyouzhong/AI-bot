#pragma once
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
#include "cJSON.h"

typedef enum {
    CODEX_COMPANION_IDLE, CODEX_COMPANION_WORKING, CODEX_COMPANION_APPROVAL,
    CODEX_COMPANION_QUESTION, CODEX_COMPANION_WAITING, CODEX_COMPANION_DONE,
    CODEX_COMPANION_BLOCKED, CODEX_COMPANION_STOPPED, CODEX_COMPANION_UNKNOWN
} codex_companion_state;

typedef struct {
    codex_companion_state state;
    char task[37], turn[64];
} codex_companion_value;

typedef struct {
    codex_companion_value value;
    int64_t started_at;
    bool initialized;
} codex_companion_animation;

#define CODEX_COMPANION_WIDTH 192
#define CODEX_COMPANION_HEIGHT 208
#define CODEX_COMPANION_PIXELS (CODEX_COMPANION_WIDTH * CODEX_COMPANION_HEIGHT)
#define CODEX_COMPANION_FRAME_BYTES (CODEX_COMPANION_PIXELS * 3)

codex_companion_value codex_companion_resolve(const cJSON *snapshot, const cJSON *task, bool fresh, bool unread, const char *unread_turn);
const char *codex_companion_label(codex_companion_state state);
bool codex_companion_select(codex_companion_animation *animation, codex_companion_value value, int64_t now);
unsigned codex_companion_frame(const codex_companion_animation *animation, int64_t now);
unsigned codex_companion_saver_frame(const codex_companion_animation *animation, int64_t now);
bool codex_companion_asset_available(void);
bool codex_companion_decode(unsigned frame, uint8_t *output, size_t capacity);
bool codex_companion_unpack(const uint8_t *data, size_t size, const uint16_t palette[256], const uint8_t alpha[256], uint8_t *output, size_t pixels);
