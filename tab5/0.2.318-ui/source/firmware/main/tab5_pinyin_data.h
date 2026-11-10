#pragma once
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
typedef struct {
    const char *keys,*words;
    const uint8_t (*rows)[11];
    size_t count,raw_bytes,packed_bytes;
} tab5_pinyin_data;
// Boot/UI-thread initialization only. Immutable storage lasts until reset.
bool tab5_pinyin_data_init(tab5_pinyin_data *out);
