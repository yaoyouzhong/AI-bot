"""Fix pinned IDF 5.4.2 JPEG descriptor allocation and input cache range.

esp_psram_extram_reserve_dma_pool deliberately omits MALLOC_CAP_DEFAULT.
Descriptor storage must also cover the whole cache range synced by the driver.
Keep general codec allocations and image buffers intact. Match the upstream
release/v5.4 correction: synchronize the caller's byte count, not bit depth.
https://github.com/espressif/esp-idf/blob/release/v5.4/components/esp_driver_jpeg/jpeg_encode.c
"""
from pathlib import Path
import sys

OLD = 'heap_caps_aligned_calloc(alignment, 1, sizeof(dma2d_descriptor_t), MALLOC_CAP_DMA | MALLOC_CAP_INTERNAL | JPEG_MEM_ALLOC_CAPS)'
NEW = 'heap_caps_aligned_calloc(alignment, 1, dma_desc_mem_size, MALLOC_CAP_DMA | MALLOC_CAP_INTERNAL | MALLOC_CAP_8BIT)'
MARKER = '    // TAB5: DMA reserve excludes DEFAULT; allocate the entire synced cache line.\n'
DECODE_OLD = 'heap_caps_aligned_calloc(alignment, 1, dma_desc_mem_size, MALLOC_CAP_DMA | MALLOC_CAP_INTERNAL | JPEG_MEM_ALLOC_CAPS)'
DECODE_MARKER = '    // TAB5: decoder descriptors may use the reserved DMA heap (no DEFAULT).\n'
CACHE_OLD = 'ret = esp_cache_msync((void*)raw_buffer, encoder_engine->header_info->origin_v * encoder_engine->header_info->origin_h * encoder_engine->bytes_per_pixel, ESP_CACHE_MSYNC_FLAG_DIR_C2M | ESP_CACHE_MSYNC_FLAG_UNALIGNED);'
CACHE_NEW = 'ret = esp_cache_msync((void*)raw_buffer, inbuf_size, ESP_CACHE_MSYNC_FLAG_DIR_C2M | ESP_CACHE_MSYNC_FLAG_UNALIGNED);'
CACHE_BOUNDED = CACHE_NEW.replace('esp_cache_msync(', 'tab5_jpeg_sync_image(')
SYNC_HELPER = '''// TAB5: bound image cache critical sections so LCD frame interrupts can run.
static esp_err_t tab5_jpeg_sync_image(void *buffer, size_t size, int flags)
{
    for (size_t offset = 0; offset < size;) {
        size_t part = size - offset;
        if (part > 16384) part = 16384;
        esp_err_t result = esp_cache_msync((uint8_t *)buffer + offset, part, flags);
        if (result != ESP_OK) return result;
        offset += part;
        if (offset < size) vTaskDelay(1);
    }
    return ESP_OK;
}

'''
INPUT_ANCHOR = '    s_jpeg_enc_select_sample_mode(encoder_engine);\n'
INPUT_GUARD = '''    // TAB5: bytes_per_pixel is bit depth. Validate before any TX DMA setup.
    uint64_t input_pixels = (uint64_t)encode_cfg->width * encode_cfg->height;
    ESP_GOTO_ON_FALSE(input_pixels > 0 && encoder_engine->bytes_per_pixel > 0 &&
                      input_pixels <= ((uint64_t)inbuf_size * 8) / encoder_engine->bytes_per_pixel &&
                      (input_pixels * encoder_engine->bytes_per_pixel) % 8 == 0,
                      ESP_ERR_INVALID_ARG, err2, TAG, "invalid JPEG input geometry or buffer size");
'''

def patch_input_range(source: str) -> str:
    if source.count(CACHE_BOUNDED) == 1 and CACHE_OLD not in source and source.count(INPUT_GUARD) == 1:
        return source
    if source.count(CACHE_NEW) == 1 and CACHE_OLD not in source and source.count(INPUT_GUARD) == 1:
        return source
    if source.count(CACHE_OLD) != 1 or CACHE_NEW in source or INPUT_GUARD in source or source.count(INPUT_ANCHOR) != 1:
        raise RuntimeError('Unknown JPEG encoder input cache range; inspect SDK before patching')
    return source.replace(INPUT_ANCHOR, INPUT_ANCHOR + INPUT_GUARD).replace(CACHE_OLD, CACHE_NEW)

def patch_bounded_sync(source: str) -> str:
    output = 'esp_cache_msync((void*)(bit_stream + encoder_engine->header_info->header_len), _compressed_size, ESP_CACHE_MSYNC_FLAG_DIR_M2C)'
    bounded_output = output.replace('esp_cache_msync(', 'tab5_jpeg_sync_image(')
    if source.count(SYNC_HELPER) == 1 and source.count(CACHE_BOUNDED) == 1 and source.count(bounded_output) == 1:
        return source
    anchor = 'static const char *TAG = "jpeg.encoder";\n\n'
    if source.count(anchor) != 1 or source.count(CACHE_NEW) != 1 or source.count(output) != 1:
        raise RuntimeError('Unknown JPEG image synchronization; inspect SDK')
    source = source.replace('#include "freertos/queue.h"', '#include "freertos/queue.h"\n#include "freertos/task.h"')
    return source.replace(anchor, anchor + SYNC_HELPER).replace(CACHE_NEW, CACHE_BOUNDED).replace(output, bounded_output)

def patch(source: str) -> str:
    if source.count(NEW) == 2 and OLD not in source and source.count(MARKER) == 1:
        return source
    if source.count(OLD) != 2 or NEW in source or MARKER in source:
        raise RuntimeError('Unknown JPEG encoder descriptor allocation; inspect SDK before patching')
    anchor = '    encoder_engine->rxlink = (dma2d_descriptor_t*)' + OLD
    if source.count(anchor) != 1:
        raise RuntimeError('JPEG encoder allocation anchor not found')
    return source.replace(anchor, MARKER + anchor).replace(OLD, NEW)

def patch_decoder(source: str) -> str:
    if source.count(NEW) == 2 and DECODE_OLD not in source and source.count(DECODE_MARKER) == 1:
        return source
    if source.count(DECODE_OLD) != 2 or NEW in source or DECODE_MARKER in source:
        raise RuntimeError('Unknown JPEG decoder descriptor allocation; inspect SDK before patching')
    anchor = '    decoder_engine->rxlink = (dma2d_descriptor_t*)' + DECODE_OLD
    if source.count(anchor) != 1:
        raise RuntimeError('JPEG decoder allocation anchor not found')
    return source.replace(anchor, DECODE_MARKER + anchor).replace(DECODE_OLD, NEW)

def patch_encoder_burst(source: str) -> str:
    start = source.find('static void jpeg_enc_config_dma_trans_ability(jpeg_encoder_handle_t encoder_engine)\n{')
    end = source.find('\nstatic bool s_jpeg_enc_transaction_on_job_picked(', start)
    if start < 0 or end < 0:
        raise RuntimeError('Unknown JPEG encoder DMA configuration; inspect SDK')
    body = source[start:end]
    old = '.data_burst_length = DMA2D_DATA_BURST_LENGTH_128,'
    new = '.data_burst_length = DMA2D_DATA_BURST_LENGTH_16,'
    marker = '    // TAB5: short codec bursts leave PSRAM arbitration opportunities for LCD scanout.\n'
    if body.count(new) == 2 and old not in body and body.count(marker) == 1:
        return source
    if body.count(old) != 2 or new in body or marker in body:
        raise RuntimeError('Unknown JPEG encoder DMA burst settings; inspect SDK')
    return source[:start] + body.replace('    // set transfer ability\n', marker).replace(old, new) + source[end:]

def patch_sdk(root: Path) -> None:
    # Validate both pinned sources before modifying either one.
    updates = []
    for name, transform in [('jpeg_encode.c', lambda text: patch_encoder_burst(patch_bounded_sync(patch_input_range(patch(text))))), ('jpeg_decode.c', patch_decoder)]:
        file = root / 'components/esp_driver_jpeg' / name
        source = file.read_text(encoding='utf-8')
        updates.append((file, source, transform(source)))
    for file, source, updated in updates:
        if updated != source:
            file.write_text(updated, encoding='utf-8')

if __name__ == '__main__':
    patch_sdk(Path(sys.argv[1]))
    print('TAB5 JPEG DMA reserve, descriptor size, bounded cache and short codec bursts ready')
