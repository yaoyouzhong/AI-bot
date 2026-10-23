#include "ota_performance.h"

static uint64_t add_u64(uint64_t a, uint64_t b) {
    return UINT64_MAX - a < b ? UINT64_MAX : a + b;
}
static void increment(uint32_t *value) {
    if (*value != UINT32_MAX) ++*value;
}
static bool core_valid(int core) {
    return core >= 0 && core < OTA_PERFORMANCE_CORE_COUNT;
}
static int core_value(int core) {
    return core_valid(core) ? core : -1;
}

void ota_performance_reset(ota_performance_t *p, int32_t transport,
                           uint32_t target_address, uint32_t staging_bytes) {
    if (!p) return;
    *p = (ota_performance_t){
        .transport = transport,
        .target_address = target_address,
        .staging_bytes = staging_bytes,
    };
}
void ota_performance_add_copy(ota_performance_t *p, uint64_t elapsed_us) {
    if (p) p->copy_us = add_u64(p->copy_us, elapsed_us);
}
void ota_performance_record_flash(ota_performance_t *p, int start_core,
                                  int end_core, uint64_t elapsed_us,
                                  uint64_t bytes, bool success) {
    if (!p) return;
    bool start_valid = core_valid(start_core), end_valid = core_valid(end_core);
    unsigned bucket = start_valid ? (unsigned)start_core : OTA_PERFORMANCE_UNKNOWN_CORE;
    ota_performance_flash *flash = &p->flash[bucket];
    flash->elapsed_us = add_u64(flash->elapsed_us, elapsed_us);
    increment(&flash->calls);
    if (success) flash->successful_bytes = add_u64(flash->successful_bytes, bytes);
    if (!start_valid || !end_valid) increment(&p->flash_unknown_endpoints);
    else if (start_core != end_core) increment(&p->flash_different_endpoints);
}
void ota_performance_record_verify(ota_performance_t *p,
                                   ota_performance_verify_phase phase,
                                   int start_core, int end_core,
                                   uint64_t elapsed_us) {
    if (!p || (unsigned)phase >= OTA_PERFORMANCE_VERIFY_COUNT) return;
    p->verify[phase] = (ota_performance_verify){
        .elapsed_us = elapsed_us,
        .start_core = (int8_t)core_value(start_core),
        .end_core = (int8_t)core_value(end_core),
    };
    p->verify_valid_mask |= UINT32_C(1) << phase;
}

typedef struct {
    char *out;
    size_t size;
    size_t used;
    bool valid;
} csv_writer;

static void csv_char(csv_writer *w, char c) {
    if (!w->valid) return;
    if (w->used + 1 >= w->size) { w->valid = false; return; }
    w->out[w->used++] = c;
}
static void csv_unsigned(csv_writer *w, uint64_t value) {
    char digits[20];
    unsigned count = 0;
    if (w->used) csv_char(w, ',');
    do {
        digits[count++] = (char)('0' + value % 10);
        value /= 10;
    } while (value);
    while (count) csv_char(w, digits[--count]);
}
static void csv_signed(csv_writer *w, int32_t value) {
    if (value >= 0) { csv_unsigned(w, (uint32_t)value); return; }
    // Do not negate INT32_MIN in its own signed type.
    uint64_t magnitude = (uint64_t)(-(int64_t)value);
    char digits[10];
    unsigned count = 0;
    if (w->used) csv_char(w, ',');
    csv_char(w, '-');
    do {
        digits[count++] = (char)('0' + magnitude % 10);
        magnitude /= 10;
    } while (magnitude);
    while (count) csv_char(w, digits[--count]);
}
size_t ota_performance_format(const ota_performance_t *p, char *out, size_t size) {
    if (!out || !size) return 0;
    out[0] = 0;
    if (!p) return 0;
    csv_writer w = {.out = out, .size = size, .valid = true};
    csv_unsigned(&w, 1);
    csv_signed(&w, p->transport);
    csv_unsigned(&w, p->target_address);
    csv_unsigned(&w, p->staging_bytes);
    csv_unsigned(&w, p->copy_us);
    for (unsigned i = 0; i < OTA_PERFORMANCE_FLASH_BUCKETS; ++i) {
        csv_unsigned(&w, p->flash[i].elapsed_us);
        csv_unsigned(&w, p->flash[i].successful_bytes);
        csv_unsigned(&w, p->flash[i].calls);
    }
    csv_unsigned(&w, p->flash_different_endpoints);
    csv_unsigned(&w, p->flash_unknown_endpoints);
    csv_unsigned(&w, p->verify_valid_mask & ((UINT32_C(1) << OTA_PERFORMANCE_VERIFY_COUNT) - 1));
    for (unsigned i = 0; i < OTA_PERFORMANCE_VERIFY_COUNT; ++i) {
        csv_unsigned(&w, p->verify[i].elapsed_us);
        csv_signed(&w, core_value(p->verify[i].start_core));
        csv_signed(&w, core_value(p->verify[i].end_core));
    }
    if (!w.valid) { out[0] = 0; return 0; }
    out[w.used] = 0;
    return w.used;
}
