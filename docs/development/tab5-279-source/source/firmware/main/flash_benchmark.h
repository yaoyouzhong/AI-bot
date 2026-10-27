#pragma once
#include <stdbool.h>
#include <stddef.h>

// Explicit local diagnostic only; never selects a boot partition or installs an image.
bool flash_benchmark_start(void);
bool flash_benchmark_busy(void);
void flash_benchmark_cancel(void);
void flash_benchmark_status(char *out,size_t size);
// Numeric result for the current boot; null before an explicit test.
void flash_benchmark_json(char *out,size_t size);
