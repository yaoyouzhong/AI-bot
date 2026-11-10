#include "ota_performance.h"
#ifdef NDEBUG
#undef NDEBUG
#endif
#include <assert.h>
#include <limits.h>
#include <stdio.h>
#include <string.h>

static void expect_csv(const ota_performance_t *p, const char *expected) {
    char out[OTA_PERFORMANCE_CSV_CAPACITY];
    size_t length = ota_performance_format(p, out, sizeof(out));
    assert(length == strlen(expected));
    assert(strcmp(out, expected) == 0);
    unsigned fields = 1;
    for (size_t i = 0; i < length; ++i) {
        char c = out[i];
        assert((c >= '0' && c <= '9') || c == ',' || c == '-');
        if (c == ',') ++fields;
    }
    assert(fields == OTA_PERFORMANCE_CSV_FIELDS);
}

static void zero_and_reset(void) {
    ota_performance_t p = {0};
    expect_csv(&p, "1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0");
    ota_performance_add_copy(&p, UINT64_MAX);
    ota_performance_record_flash(&p, 0, 1, 123, 456, true);
    ota_performance_record_flash(&p, -1, -1, 123, 456, true);
    ota_performance_record_verify(&p, OTA_PERFORMANCE_VERIFY_END, 0, 1, 999);
    ota_performance_reset(&p, 2, 64, 8192);
    expect_csv(&p, "1,2,64,8192,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0");
}

static void observed_calls_and_failures(void) {
    ota_performance_t p = {0};
    ota_performance_reset(&p, 1, UINT32_C(0x01020304), 49152);
    ota_performance_add_copy(&p, UINT64_C(1234567890123));
    ota_performance_add_copy(&p, 7);
    ota_performance_record_flash(&p, 0, 0, 11, 512, true);
    ota_performance_record_flash(&p, 0, 1, 17, 256, false);
    ota_performance_record_flash(&p, 1, 0, 23, 1024, true);
    ota_performance_record_flash(&p, -1, 1, 5, 64, true);
    ota_performance_record_flash(&p, 1, 99, 7, 128, false);
    ota_performance_record_flash(&p, INT_MIN, INT_MAX, 9, 128, false);
    ota_performance_record_verify(&p, OTA_PERFORMANCE_VERIFY_END, 0, 1, 101);
    ota_performance_record_verify(&p, OTA_PERFORMANCE_VERIFY_DESCRIPTION, 1, 1, 202);
    ota_performance_record_verify(&p, OTA_PERFORMANCE_VERIFY_SET_BOOT, -99, 99, 303);
    expect_csv(&p, "1,1,16909060,49152,1234567890130,28,512,2,30,1024,2,14,64,2,2,3,7,101,0,1,202,1,1,303,-1,-1");
    // The failed core-0 call counts time and a call, but no additional bytes.
    assert(p.flash[0].elapsed_us == 28 && p.flash[0].successful_bytes == 512);
    assert(p.flash[0].calls == 2);
    assert(p.flash_different_endpoints == 2 && p.flash_unknown_endpoints == 3);
}

static void precision_and_saturation(void) {
    ota_performance_t p = {0};
    uint64_t large = UINT64_C(1) << 40;
    ota_performance_add_copy(&p, large);
    ota_performance_add_copy(&p, 123);
    assert(p.copy_us == large + 123);
    ota_performance_record_flash(&p, 1, 1, large, large, true);
    ota_performance_record_flash(&p, 1, 1, 123, 456, true);
    assert(p.flash[1].elapsed_us == large + 123);
    assert(p.flash[1].successful_bytes == large + 456);
    ota_performance_add_copy(&p, UINT64_MAX);
    ota_performance_add_copy(&p, 1);
    assert(p.copy_us == UINT64_MAX);
    for (unsigned i = 0; i < OTA_PERFORMANCE_FLASH_BUCKETS; ++i) {
        p.flash[i].elapsed_us = UINT64_MAX - 3;
        p.flash[i].successful_bytes = UINT64_MAX - 3;
        p.flash[i].calls = UINT32_MAX - 1;
        int core = i < 2 ? (int)i : -1;
        ota_performance_record_flash(&p, core, 1, 10, 10, true);
        ota_performance_record_flash(&p, core, 1, 10, 10, true);
        assert(p.flash[i].elapsed_us == UINT64_MAX);
        assert(p.flash[i].successful_bytes == UINT64_MAX);
        assert(p.flash[i].calls == UINT32_MAX);
    }
    p.flash[0].successful_bytes = 42;
    ota_performance_record_flash(&p, 0, 0, UINT64_MAX, UINT64_MAX, false);
    assert(p.flash[0].successful_bytes == 42);
    p.flash_different_endpoints = UINT32_MAX;
    p.flash_unknown_endpoints = UINT32_MAX;
    ota_performance_record_flash(&p, 0, 1, 1, 1, true);
    ota_performance_record_flash(&p, -1, 1, 1, 1, true);
    assert(p.flash_different_endpoints == UINT32_MAX);
    assert(p.flash_unknown_endpoints == UINT32_MAX);
}

static void invalid_cores_and_phases(void) {
    struct { uint64_t before; ota_performance_t p; uint64_t after; } guarded = {
        .before = UINT64_C(0x0123456789abcdef), .after = UINT64_C(0xfedcba9876543210),
    };
    const int invalid[] = {INT_MIN, -999, -1, 2, 3, 99, INT_MAX};
    for (unsigned i = 0; i < sizeof(invalid) / sizeof(invalid[0]); ++i) {
        ota_performance_record_flash(&guarded.p, invalid[i], invalid[i], 1, 2, true);
        ota_performance_record_flash(&guarded.p, 0, invalid[i], 1, 2, false);
    }
    assert(guarded.p.flash[OTA_PERFORMANCE_UNKNOWN_CORE].calls == 7);
    assert(guarded.p.flash[0].calls == 7 && guarded.p.flash[1].calls == 0);
    assert(guarded.p.flash_unknown_endpoints == 14);
    assert(guarded.p.flash_different_endpoints == 0);
    ota_performance_record_verify(&guarded.p, OTA_PERFORMANCE_VERIFY_DESCRIPTION, 1, 0, UINT64_MAX);
    assert(guarded.p.verify_valid_mask == 2);
    ota_performance_record_verify(&guarded.p, OTA_PERFORMANCE_VERIFY_DESCRIPTION, -55, 55, 17);
    assert(guarded.p.verify_valid_mask == 2);
    assert(guarded.p.verify[1].elapsed_us == 17);
    assert(guarded.p.verify[1].start_core == -1 && guarded.p.verify[1].end_core == -1);
    char before[OTA_PERFORMANCE_CSV_CAPACITY], after[OTA_PERFORMANCE_CSV_CAPACITY];
    assert(ota_performance_format(&guarded.p, before, sizeof(before)));
    ota_performance_record_verify(&guarded.p, (ota_performance_verify_phase)-1, 0, 0, 1);
    ota_performance_record_verify(&guarded.p, OTA_PERFORMANCE_VERIFY_COUNT, 0, 0, 1);
    assert(ota_performance_format(&guarded.p, after, sizeof(after)));
    assert(strcmp(before, after) == 0);
    assert(guarded.before == UINT64_C(0x0123456789abcdef));
    assert(guarded.after == UINT64_C(0xfedcba9876543210));
}

static void maximum_record_and_capacity(void) {
    ota_performance_t p = {0};
    ota_performance_reset(&p, INT32_MIN, UINT32_MAX, UINT32_MAX);
    p.copy_us = UINT64_MAX;
    for (unsigned i = 0; i < OTA_PERFORMANCE_FLASH_BUCKETS; ++i) {
        p.flash[i] = (ota_performance_flash){UINT64_MAX, UINT64_MAX, UINT32_MAX};
    }
    p.flash_different_endpoints = p.flash_unknown_endpoints = UINT32_MAX;
    for (unsigned i = 0; i < OTA_PERFORMANCE_VERIFY_COUNT; ++i)
        ota_performance_record_verify(&p, (ota_performance_verify_phase)i, -1, -1, UINT64_MAX);
    // Formatter also bounds accidentally non-normalized public state.
    p.verify_valid_mask = UINT32_MAX;
    p.verify[0].start_core = INT8_MIN;
    p.verify[0].end_core = INT8_MAX;
    char full[OTA_PERFORMANCE_CSV_CAPACITY];
    size_t length = ota_performance_format(&p, full, sizeof(full));
    assert(length == OTA_PERFORMANCE_CSV_MAX_CHARS);
    assert(strlen(full) == length);
    unsigned commas = 0;
    for (size_t i = 0; i < length; ++i) if (full[i] == ',') ++commas;
    assert(commas + 1 == OTA_PERFORMANCE_CSV_FIELDS);
    for (size_t capacity = 0; capacity <= sizeof(full); ++capacity) {
        char guarded[OTA_PERFORMANCE_CSV_CAPACITY + 2];
        memset(guarded, 'X', sizeof(guarded));
        char *out = guarded + 1;
        size_t result = ota_performance_format(&p, out, capacity);
        if (capacity <= length) {
            assert(result == 0);
            if (capacity) assert(out[0] == 0);
            else assert(out[0] == 'X');
        } else {
            assert(result == length);
            assert(strcmp(out, full) == 0);
        }
        assert(guarded[0] == 'X');
        for (size_t i = capacity + 1; i < sizeof(guarded); ++i) assert(guarded[i] == 'X');
    }
}

static void invalid_input(void) {
    ota_performance_t p = {0};
    char out[4] = "old";
    assert(ota_performance_format(NULL, out, sizeof(out)) == 0 && out[0] == 0);
    assert(ota_performance_format(&p, NULL, 100) == 0);
    assert(ota_performance_format(&p, out, 0) == 0);
    ota_performance_reset(NULL, 0, 0, 0);
    ota_performance_add_copy(NULL, 1);
    ota_performance_record_flash(NULL, 0, 1, 1, 1, true);
    ota_performance_record_verify(NULL, OTA_PERFORMANCE_VERIFY_END, 0, 0, 1);
}

int main(void) {
    zero_and_reset();
    observed_calls_and_failures();
    precision_and_saturation();
    invalid_cores_and_phases();
    maximum_record_and_capacity();
    invalid_input();
    printf("OTA_PERFORMANCE_TEST_OK max_chars=%u fields=%u\n",
           (unsigned)OTA_PERFORMANCE_CSV_MAX_CHARS, (unsigned)OTA_PERFORMANCE_CSV_FIELDS);
    return 0;
}
