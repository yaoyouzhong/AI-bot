#pragma once
#include <stddef.h>
#include <stdint.h>
#ifdef __cplusplus
extern "C" {
#endif
typedef enum { TAB5_JPEG_OK, TAB5_JPEG_MEMORY, TAB5_JPEG_INVALID,
    TAB5_JPEG_FULL, TAB5_JPEG_FAILED } tab5_jpeg_result;
// One serialized camera worker owns the encoder. Reads immutable RGB565 rows;
// quality 85 / YUV420, no DMA and no full RGB888 copy. Output belongs to caller.
tab5_jpeg_result tab5_jpeg_encode(const uint16_t *pixels, size_t input_bytes,
    unsigned width, unsigned height, uint8_t *output, size_t capacity,
    uint32_t *written, void (*yield)(void *), void *context);
#ifdef __cplusplus
}
#endif
