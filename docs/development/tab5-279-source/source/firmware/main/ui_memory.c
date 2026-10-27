#include "esp_heap_caps.h"
#include "lvgl.h"

#if LV_USE_STDLIB_MALLOC != LV_STDLIB_CLIB
#error "TAB5 UI allocator requires the LVGL CLIB memory backend"
#endif

// LVGL objects and text are CPU data, not DMA buffers or FreeRTOS objects.
// Route even small allocations to PSRAM so opening pages cannot consume the
// internal heap needed by camera/audio drivers. Do not fall back to internal RAM.
// Keep LVGL's public zero-size handling and realloc failure semantics intact.
void *__wrap_lv_malloc_core(size_t size) {
    return heap_caps_malloc(size, MALLOC_CAP_SPIRAM | MALLOC_CAP_8BIT);
}

void *__wrap_lv_realloc_core(void *ptr, size_t size) {
    return heap_caps_realloc(ptr, size, MALLOC_CAP_SPIRAM | MALLOC_CAP_8BIT);
}

void __wrap_lv_free_core(void *ptr) {
    heap_caps_free(ptr);
}
