#pragma once
#include <stddef.h>
#include <stdint.h>
#include "ota_performance.h"

// Numeric progress only: never persist firmware URLs, credentials or user text.
enum {OTA_TRACE_IDLE,OTA_TRACE_CHECK,OTA_TRACE_CONNECT,OTA_TRACE_BEGIN,
      OTA_TRACE_READ,OTA_TRACE_WRITE,OTA_TRACE_VERIFY,OTA_TRACE_IMAGE,
      OTA_TRACE_BOOT,OTA_TRACE_DONE,OTA_TRACE_FAILED};
void crash_record_init(void);
void ota_trace_begin(unsigned total);
void ota_trace_update(unsigned stage,unsigned offset,int error);
void ota_trace_timing(unsigned elapsed_ms,unsigned read_ms,unsigned write_ms);
typedef struct {
    int transport;
    unsigned prepare_ms,write_ms,free_wait_ms,ready_wait_ms,verify_ms,install_ms;
} ota_phase_timing;
void ota_trace_phases(const ota_phase_timing *timing);
// Constant-size numeric snapshot; no heap scan or Flash I/O. The formatter
// returns an optional ;op,origin,versioned-CSV suffix (origin 0=current,1=previous
// boot). Empty means unknown, never a measured zero-duration upgrade.
enum { OTA_PERFORMANCE_SUFFIX_CAPACITY=OTA_PERFORMANCE_CSV_CAPACITY+6 };
// Boot init copies a valid retained record once; begin hides any previous run
// until a current snapshot is published. Storage lives in the last RTC object.
void ota_performance_trace_init(bool retained_valid);
void ota_performance_trace_begin(void);
void ota_trace_performance(const ota_performance_t *performance);
void ota_trace_performance_format(char *out,size_t size);
void crash_record_json(char *out,size_t size);
void boot_trace_update(unsigned stage,unsigned healthy,int error);
enum {RUNTIME_UI,RUNTIME_REFRESH,RUNTIME_FLUSH,RUNTIME_CACHE,RUNTIME_FLASH,RUNTIME_CHANNELS};
void crash_record_checkpoint(unsigned channel,unsigned stage,unsigned detail);
void crash_record_previous_runtime(char *out,size_t size);
