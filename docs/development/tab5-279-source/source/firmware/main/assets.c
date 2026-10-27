#include "assets.h"
#include "esp_partition.h"
#include "freertos/FreeRTOS.h"
#include "freertos/queue.h"
#include "freertos/task.h"
#include "mbedtls/base64.h"
#include "driver/jpeg_decode.h"
#include "esp_cache.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef struct {
    asset_image ready;
    uint8_t *packed;
    size_t total;
    uint8_t present[512];
    uint32_t crc, ready_crc;
    int width, height, frames;
    bool jpeg;
    uint16_t delays[8];
} asset_slot;
static asset_slot slots[3];
// Two alternating copies per pet. A header is committed only after its payload.
// Music covers remain in RAM to avoid flash wear when tracks change.
#define CACHE_BANK_SIZE 0x80000u
#define CACHE_PAYLOAD 0x1000u
#define CACHE_MAGIC 0x54425032u
typedef struct {
    uint32_t magic, version, generation, crc, width, height, frames, size;
    uint16_t delays[8];
    uint32_t header_crc;
} cache_header;
typedef struct {
    int slot;
    cache_header header;
    uint8_t *packed;
} cache_job;
static const esp_partition_t *cache_partition;
static QueueHandle_t cache_queue;
static uint32_t generations[2];
static int active_bank[2];
static volatile uint32_t cached_crc[2];
static volatile bool cached_valid[2];
static bool valid_header(const cache_header *h) {
    if (h->magic != CACHE_MAGIC || h->version != 1 ||
        h->header_crc != asset_crc32((const uint8_t *)h, offsetof(cache_header, header_crc)) || h->width < 1 ||
        h->width > 120 || h->height < 1 || h->height > 120 || h->frames < 1 || h->frames > 8 || !h->size ||
        h->size > CACHE_BANK_SIZE - CACHE_PAYLOAD)
        return false;
    for (unsigned i = 0; i < h->frames; i++)
        if (h->delays[i] < 20 || h->delays[i] > 60000)
            return false;
    return true;
}
static bool load_bank(int slot, int bank, asset_image *image, uint32_t *generation) {
    size_t base = (slot * 2 + bank) * CACHE_BANK_SIZE;
    cache_header h;
    if (esp_partition_read(cache_partition, base, &h, sizeof(h)) != ESP_OK || !valid_header(&h))
        return false;
    uint8_t *packed = malloc(h.size);
    size_t size = (size_t)h.width * h.height * h.frames * 2;
    uint8_t *pixels = malloc(size);
    bool valid = packed && pixels &&
                 esp_partition_read(cache_partition, base + CACHE_PAYLOAD, packed, h.size) == ESP_OK &&
                 asset_crc32(packed, h.size) == h.crc && asset_unpack(packed, h.size, pixels, size);
    free(packed);
    if (!valid) {
        free(pixels);
        return false;
    }
    *image = (asset_image){
        .pixels = pixels, .width = h.width, .height = h.height, .frames = h.frames, .revision = 1, .crc = h.crc};
    memcpy(image->delays, h.delays, sizeof(h.delays));
    *generation = h.generation;
    return true;
}
static void cache_writer(void *arg) {
    (void)arg;
    cache_job job;
    while (xQueueReceive(cache_queue, &job, portMAX_DELAY) == pdTRUE) {
        int slot = job.slot, bank = 1 - active_bank[slot];
        cache_header *h = &job.header;
        h->magic = CACHE_MAGIC;
        h->version = 1;
        h->generation = generations[slot] + 1;
        h->header_crc = asset_crc32((const uint8_t *)h, offsetof(cache_header, header_crc));
        size_t base = (slot * 2 + bank) * CACHE_BANK_SIZE;
        size_t erase = (CACHE_PAYLOAD + h->size + 4095) & ~(size_t)4095;
        bool ok = esp_partition_erase_range(cache_partition, base, erase) == ESP_OK &&
                  esp_partition_write(cache_partition, base + CACHE_PAYLOAD, job.packed, h->size) == ESP_OK &&
                  esp_partition_write(cache_partition, base, h, sizeof(*h)) == ESP_OK;
        if (ok) {
            generations[slot] = h->generation;
            active_bank[slot] = bank;
            cached_crc[slot] = h->crc;
            cached_valid[slot] = true;
        }
        free(job.packed);
    }
}
void assets_init(void) {
    cache_partition = esp_partition_find_first(ESP_PARTITION_TYPE_DATA, 0x40, "assets");
    if (!cache_partition || cache_partition->size < 4 * CACHE_BANK_SIZE) {
        cache_partition = NULL;
        return;
    }
    for (int slot = 0; slot < 2; slot++) {
        asset_image best = {0};
        uint32_t generation = 0;
        for (int bank = 0; bank < 2; bank++) {
            asset_image candidate;
            uint32_t candidate_generation;
            if (load_bank(slot, bank, &candidate, &candidate_generation)) {
                if (!best.pixels || (int32_t)(candidate_generation - generation) > 0) {
                    free(best.pixels);
                    best = candidate;
                    generation = candidate_generation;
                    active_bank[slot] = bank;
                } else
                    free(candidate.pixels);
            }
        }
        if (best.pixels) {
            slots[slot].ready = best;
            slots[slot].ready_crc = best.crc;
            generations[slot] = generation;
            cached_crc[slot] = best.crc;
            cached_valid[slot] = true;
        }
    }
    cache_queue = xQueueCreate(2, sizeof(cache_job));
    if (cache_queue && xTaskCreate(cache_writer, "tab5_cache", 4096, NULL, 2, NULL) != pdPASS) {
        vQueueDelete(cache_queue);
        cache_queue = NULL;
    }
}
static void cache_save(int slot, const asset_slot *s) {
    if (slot >= 2 || !cache_queue || (cached_valid[slot] && cached_crc[slot] == s->crc))
        return;
    cache_job job = {
        .slot = slot,
        .header = {.crc = s->crc, .width = s->width, .height = s->height, .frames = s->frames, .size = s->total}};
    memcpy(job.header.delays, s->delays, s->frames * 2);
    job.packed = malloc(s->total);
    if (!job.packed)
        return;
    memcpy(job.packed, s->packed, s->total);
    if (xQueueSend(cache_queue, &job, 0) != pdTRUE)
        free(job.packed);
}
static int number(cJSON *o, const char *key) {
    cJSON *v = cJSON_GetObjectItemCaseSensitive(o, key);
    return cJSON_IsNumber(v) ? v->valueint : -1;
}
static const char *text(cJSON *o, const char *key) {
    cJSON *v = cJSON_GetObjectItemCaseSensitive(o, key);
    return cJSON_IsString(v) ? v->valuestring : NULL;
}
static bool decode_jpeg(const uint8_t *packed,size_t size,uint8_t *pixels,int width,int height) {
    if(size<16||packed[0]!=0xff||packed[1]!=0xd8)return false;
    jpeg_decode_picture_info_t info={0};
    if(jpeg_decoder_get_info(packed,size,&info)!=ESP_OK||info.width!=width||info.height!=height)return false;
    size_t in_capacity=0,out_capacity=0;
    const jpeg_decode_memory_alloc_cfg_t in_cfg={.buffer_direction=JPEG_DEC_ALLOC_INPUT_BUFFER},out_cfg={.buffer_direction=JPEG_DEC_ALLOC_OUTPUT_BUFFER};
    unsigned stride=(width+15)&~15u,rows=(height+15)&~15u;
    uint8_t *input=jpeg_alloc_decoder_mem(size,&in_cfg,&in_capacity);
    uint8_t *output=jpeg_alloc_decoder_mem(stride*rows*2,&out_cfg,&out_capacity);
    jpeg_decoder_handle_t engine=NULL;const jpeg_decode_engine_cfg_t engine_cfg={.timeout_ms=500};
    const jpeg_decode_cfg_t cfg={.output_format=JPEG_DECODE_OUT_FORMAT_RGB565,.rgb_order=JPEG_DEC_RGB_ELEMENT_ORDER_BGR,.conv_std=JPEG_YUV_RGB_CONV_STD_BT601};
    uint32_t written=0;bool ok=false;
    if(input&&output&&in_capacity>=size) {
        memcpy(input,packed,size);
        // Retire CPU-written allocator cache lines before DMA owns this buffer.
        ok=esp_cache_msync(output,out_capacity,ESP_CACHE_MSYNC_FLAG_DIR_C2M|ESP_CACHE_MSYNC_FLAG_INVALIDATE)==ESP_OK&&
           jpeg_new_decoder_engine(&engine_cfg,&engine)==ESP_OK&&
           jpeg_decoder_process(engine,&cfg,input,size,output,out_capacity,&written)==ESP_OK&&written>=stride*height*2;
        if(ok)for(int y=0;y<height;y++)memcpy(pixels+(size_t)y*width*2,output+(size_t)y*stride*2,width*2);
    }
    if(engine)jpeg_del_decoder_engine(engine);
    free(input);free(output);return ok;
}
bool assets_receive(cJSON *chunk) {
    if (!cJSON_IsObject(chunk))
        return false;
    int slot = number(chunk, "slot"), total = number(chunk, "total"), offset = number(chunk, "offset");
    int w = number(chunk, "width"), h = number(chunk, "height"), frames = number(chunk, "frames");
    const char *id = text(chunk, "id"), *encoded = text(chunk, "bytes");
    const char *encoding=text(chunk,"encoding");
    bool jpeg=encoding&&!strcmp(encoding,"jpeg");
    unsigned int crc = 0;
    char extra;
    if (slot < 0 || slot > 2 || total <= 0 || total > 524288 || offset < 0 || offset >= total || offset % 1024 ||
        (encoding&&strcmp(encoding,"rle565")&&!jpeg) || (jpeg&&slot!=2) ||
        w < 1 || w > (slot == 2 ? (jpeg?560:336) : 120) || h < 1 || h > (slot == 2 ? 336 : 120) ||
        frames < 1 || frames > (slot == 2 ? 1 : 8) || !id || strlen(id) != 8 ||
        sscanf(id, "%8x%c", &crc, &extra) != 1 || !encoded || strlen(encoded) > 1368)
        return false;
    uint16_t delays[8];
    cJSON *list = cJSON_GetObjectItemCaseSensitive(chunk, "delays");
    if (cJSON_GetArraySize(list) != frames)
        return false;
    for (int i = 0; i < frames; i++) {
        cJSON *v = cJSON_GetArrayItem(list, i);
        if (!cJSON_IsNumber(v) || v->valueint < 20 || v->valueint > 60000)
            return false;
        delays[i] = v->valueint;
    }
    asset_slot *s = &slots[slot];
    if (s->ready.pixels && s->ready_crc == crc && s->ready.width == w && s->ready.height == h &&
        s->ready.frames == frames && !memcmp(s->ready.delays, delays, frames * 2))
        return true;
    if (!s->packed || s->crc != crc || s->total != (size_t)total || s->width != w || s->height != h ||
        s->frames != frames || s->jpeg!=jpeg || memcmp(s->delays, delays, frames * 2)) {
        uint8_t *next = malloc(total);
        if (!next)
            return false;
        free(s->packed);
        s->packed = next;
        s->total = total;
        s->crc = crc;
        s->width = w;
        s->height = h;
        s->frames = frames;
        s->jpeg=jpeg;
        memcpy(s->delays, delays, frames * 2);
        memset(s->present, 0, sizeof(s->present));
    }
    size_t length = 0, expected = (size_t)(total - offset) > 1024 ? 1024 : (size_t)(total - offset);
    if (mbedtls_base64_decode(s->packed + offset, expected, &length, (const uint8_t *)encoded, strlen(encoded)) ||
        length != expected)
        return false;
    s->present[offset / 1024] = 1;
    for (int i = 0; i < (total + 1023) / 1024; i++)
        if (!s->present[i])
            return true;
    size_t bytes = (size_t)w * h * frames * 2;
    uint8_t *pixels = malloc(bytes);
    bool valid = pixels && asset_crc32(s->packed, total) == crc &&
        (jpeg?decode_jpeg(s->packed,total,pixels,w,h):asset_unpack(s->packed, total, pixels, bytes));
    if (valid) {
        free(s->ready.pixels);
        s->ready.pixels = pixels;
        s->ready.width = w;
        s->ready.height = h;
        s->ready.frames = frames;
        s->ready_crc = crc;
        s->ready.crc = crc;
        memcpy(s->ready.delays, delays, frames * 2);
        s->ready.revision++;
        if (!s->ready.revision)
            s->ready.revision = 1;
        cache_save(slot, s);
    } else
        free(pixels);
    free(s->packed);
    s->packed = NULL;
    return valid;
}
bool assets_copy(int slot, uint32_t previous, asset_image *image) {
    if (slot < 0 || slot > 2)
        return false;
    asset_image *source = &slots[slot].ready;
    if (!source->pixels || source->revision == previous)
        return false;
    size_t bytes = (size_t)source->width * source->height * source->frames * 2;
    uint8_t *copy = malloc(bytes);
    if (!copy)
        return false;
    memcpy(copy, source->pixels, bytes);
    *image = *source;
    image->pixels = copy;
    return true;
}
unsigned assets_ready(void) {
    unsigned mask = 0;
    for (int i = 0; i < 3; i++)
        if (slots[i].ready.pixels)
            mask |= 1u << i;
    return mask;
}
bool assets_pending(void) {
    for(int i=0;i<3;i++)if(slots[i].packed)return true;
    return false;
}
int assets_progress(int slot) {
    if (slot < 0 || slot > 2)
        return 0;
    asset_slot *s = &slots[slot];
    if (!s->packed)
        return s->ready.pixels ? 100 : 0;
    int count = (int)((s->total + 1023) / 1024), received = 0;
    for (int i = 0; i < count; i++)
        if (s->present[i])
            received++;
    return count ? received * 100 / count : 0;
}
void assets_ids(char output[27]) {
    char parts[3][9];
    for (int i = 0; i < 3; i++) {
        if (slots[i].ready.pixels)
            snprintf(parts[i], sizeof(parts[i]), "%08x", (unsigned)slots[i].ready.crc);
        else
            strcpy(parts[i], "-");
    }
    snprintf(output, 27, "%s,%s,%s", parts[0], parts[1], parts[2]);
}
unsigned assets_cached(void) {
    unsigned result = 0;
    for (int i = 0; i < 2; i++)
        if (cached_valid[i] && cached_crc[i] == slots[i].ready.crc)
            result |= 1u << i;
    return result;
}
