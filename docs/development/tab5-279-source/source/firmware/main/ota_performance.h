#pragma once
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

// Plain, single-owner numeric state. Zero initialization is valid. Callers
// supply observations; this module never reads clocks, tasks, heap or devices.
enum {
    OTA_PERFORMANCE_CORE_COUNT = 2,
    OTA_PERFORMANCE_UNKNOWN_CORE = 2,
    OTA_PERFORMANCE_FLASH_BUCKETS = 3,
    OTA_PERFORMANCE_CSV_FIELDS = 26,
    // 10 uint64 fields (20 digits), 7 uint32 fields (10), one int32 (11),
    // 6 normalized core fields (2), version + mask (1 each), 25 commas.
    OTA_PERFORMANCE_CSV_MAX_CHARS = 320,
    OTA_PERFORMANCE_CSV_CAPACITY = OTA_PERFORMANCE_CSV_MAX_CHARS + 1,
};

typedef enum {
    OTA_PERFORMANCE_VERIFY_END = 0,
    OTA_PERFORMANCE_VERIFY_DESCRIPTION = 1,
    OTA_PERFORMANCE_VERIFY_SET_BOOT = 2,
    OTA_PERFORMANCE_VERIFY_COUNT = 3,
} ota_performance_verify_phase;

typedef struct {
    uint64_t elapsed_us;
    uint64_t successful_bytes;
    uint32_t calls;
} ota_performance_flash;

typedef struct {
    uint64_t elapsed_us;
    int8_t start_core;
    int8_t end_core;
} ota_performance_verify;

typedef struct {
    int32_t transport;
    uint32_t target_address;
    uint32_t staging_bytes;
    uint64_t copy_us;
    // Bucket 2 receives calls whose observed starting core is invalid.
    ota_performance_flash flash[OTA_PERFORMANCE_FLASH_BUCKETS];
    // Counts calls with two valid, unequal endpoint cores. This is NOT a
    // migration count: moves away and back within one call are unobserved.
    uint32_t flash_different_endpoints;
    uint32_t flash_unknown_endpoints;
    uint32_t verify_valid_mask;
    ota_performance_verify verify[OTA_PERFORMANCE_VERIFY_COUNT];
} ota_performance_t;

void ota_performance_reset(ota_performance_t *p, int32_t transport,
                           uint32_t target_address, uint32_t staging_bytes);
void ota_performance_add_copy(ota_performance_t *p, uint64_t elapsed_us);
// Elapsed time includes failures, scheduling and lock waits. It is not CPU
// time. Successful bytes are counted only when the supplied success is true.
void ota_performance_record_flash(ota_performance_t *p, int start_core,
                                  int end_core, uint64_t elapsed_us,
                                  uint64_t bytes, bool success);
// Each phase is one observation; a repeat replaces it. Invalid phases are
// ignored. Invalid cores become -1. Only valid-mask bits make phases present.
void ota_performance_record_verify(ota_performance_t *p,
                                   ota_performance_verify_phase phase,
                                   int start_core, int end_core,
                                   uint64_t elapsed_us);

// Version-1 CSV order:
// version,transport,targetAddress,stagingBytes,copyUs,
// core0Us,core0SuccessfulBytes,core0Calls,
// core1Us,core1SuccessfulBytes,core1Calls,
// unknownStartUs,unknownStartSuccessfulBytes,unknownStartCalls,
// differentEndpoints,unknownEndpoints,verifyValidMask,
// endUs,endStartCore,endFinishCore,
// descriptionUs,descriptionStartCore,descriptionFinishCore,
// setBootUs,setBootStartCore,setBootFinishCore.
// Aggregates saturate at their unsigned type limit. The mask is limited to
// three phase bits. Returns bytes excluding NUL, or 0 and an empty output on
// invalid input/insufficient capacity. No truncated record is ever returned.
size_t ota_performance_format(const ota_performance_t *p, char *out, size_t size);
