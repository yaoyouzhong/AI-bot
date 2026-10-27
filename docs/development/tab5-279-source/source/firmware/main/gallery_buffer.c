#include "gallery_remote.h"
#include <stdatomic.h>
#include <stdlib.h>
#if defined(ESP_PLATFORM) || defined(TAB5_GALLERY_CAMERA_POOL_TEST)
#include "camera_gallery_buffer.h"
#endif

// One spare in addition to the displayed frame. Ownership moves atomically
// between the LVGL task and the decoder worker, including failed requests.
static _Atomic(uint8_t *) spare_buffer;
void gallery_release_buffer(uint8_t *pixels) {
#if defined(ESP_PLATFORM) || defined(TAB5_GALLERY_CAMERA_POOL_TEST)
    if(camera_gallery_buffer_release(pixels))return;
#endif
    free(pixels);
}
void gallery_recycle(uint8_t *pixels) {
    if(pixels)gallery_release_buffer(atomic_exchange(&spare_buffer,pixels));
}
uint8_t *gallery_take_buffer(void) {
    return atomic_exchange(&spare_buffer,NULL);
}
