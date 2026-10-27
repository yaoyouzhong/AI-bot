#pragma once
#include "lvgl.h"
#include "camera_frame.h"
#include "camera_geometry.h"
#include <stdbool.h>

typedef enum {
    CAMERA_PREVIEW_OFF,
    CAMERA_PREVIEW_STARTING,
    CAMERA_PREVIEW_LIVE,
    CAMERA_PREVIEW_CAPTURED,
    CAMERA_PREVIEW_ERROR,
    CAMERA_PREVIEW_CAPTURING
} camera_preview_state;

// Call on the LVGL thread/under its mutex; hides the old still before reuse.
bool camera_preview_start(lv_obj_t *canvas);
// Register on the UI thread before starting a worker. The handler runs under
// the LVGL mutex only after direct scanout has been released; false defers a
// change (for example while a touch gesture is pressed).
void camera_preview_set_orientation_handler(bool (*apply)(int direction));
void camera_preview_request_orientation(int direction);
bool camera_preview_capture(void);
void camera_preview_stop(void);
camera_preview_state camera_preview_get_state(void);
// Lease the immutable captured frame until release; blocks retake while JPEG reads it.
const uint8_t *camera_preview_acquire(void);
// Dimensions remain stable while the immutable still is leased.
void camera_preview_dimensions(unsigned *width,unsigned *height);
void camera_preview_release(const uint8_t *pixels);
bool camera_preview_busy(void);
const char *camera_preview_error(void);
void camera_preview_init_diagnostics(void);
// Startup only, before assets/UI/attachments. Reserve three aligned full frames
// for this boot; failed partial allocation unwinds without affecting other apps.
bool camera_preview_init_buffers(void);
void camera_preview_diagnostics(char *out, unsigned size);
void camera_preview_driver_error_hex(char *out,unsigned size);
