#pragma once
#include <stdbool.h>
#include <stdint.h>
#include <stddef.h>
typedef struct {
    // 0: normal landscape, 2: inverted landscape, +/-1: portrait raw-Y sign.
    int orientation,x,y,z,error;
    bool ready;
    int64_t sampled_ms;
    int stage,i2c_error,reg,chip_id,attempts;
} art_orientation_state;
void art_orientation_start(void);
art_orientation_state art_orientation_get(void);
void art_orientation_diagnostics(char *out,size_t size);
