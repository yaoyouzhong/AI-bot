#pragma once
#include <stdint.h>
#include <stddef.h>
#include <string.h>

#define CAMERA_CAPTURE_WIDTH 1280
#define CAMERA_CAPTURE_HEIGHT 720
#define CAMERA_VIEW_WIDTH CAMERA_CAPTURE_WIDTH
#define CAMERA_VIEW_HEIGHT CAMERA_CAPTURE_HEIGHT

// Preserve a native full-screen still outside the LVGL mutex. Live preview
// retains the driver buffer; only capture/close needs this copy, with no resampling.
static inline void camera_frame_view(uint16_t *out,const uint8_t *source,size_t stride) {
    if(stride==CAMERA_VIEW_WIDTH*2) {
        memcpy(out,source,CAMERA_VIEW_WIDTH*CAMERA_VIEW_HEIGHT*2);
        return;
    }
    for(unsigned y=0;y<CAMERA_VIEW_HEIGHT;y++) {
        memcpy(out+y*CAMERA_VIEW_WIDTH,source+y*stride,CAMERA_VIEW_WIDTH*2);
    }
}
