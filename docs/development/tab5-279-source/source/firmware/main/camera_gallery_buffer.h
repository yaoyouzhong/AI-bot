#pragma once
#include <stdbool.h>
#include <stdint.h>

// Borrow at most two idle, aligned camera frames. The retained photograph is
// excluded; the camera must drain every gallery owner before starting CSI.
uint8_t *camera_gallery_buffer_take(void);
bool camera_gallery_buffer_release(uint8_t *pixels);
bool camera_gallery_buffers_idle(void);
