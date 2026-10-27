#pragma once
#include <stdbool.h>
#include <stdint.h>

// A164 Character mode. Called from the LVGL task; one queued event per call.
bool tab5_keyboard_poll(char character[10], uint8_t *modifier);
bool tab5_keyboard_present(void);
