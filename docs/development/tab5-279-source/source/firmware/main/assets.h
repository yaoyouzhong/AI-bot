#pragma once
#include "cJSON.h"
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
typedef struct {
    uint8_t *pixels;
    int width, height, frames;
    uint16_t delays[8];
    uint32_t revision, crc;
} asset_image;
bool assets_receive(cJSON *chunk);                                 // caller holds device_mutex
bool assets_copy(int slot, uint32_t previous, asset_image *image); // caller holds device_mutex
unsigned assets_ready(void);
int assets_progress(int slot); // caller holds device_mutex
bool assets_pending(void); // caller holds device_mutex
void assets_init(void); // before transport tasks start
void assets_ids(char output[27]); // caller holds device_mutex
unsigned assets_cached(void);
uint32_t asset_crc32(const uint8_t *data, size_t size);
bool asset_unpack(const uint8_t *data, size_t size, uint8_t *output, size_t capacity);
