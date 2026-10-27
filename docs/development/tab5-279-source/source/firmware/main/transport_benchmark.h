#pragma once
#include <stdbool.h>
#include <stddef.h>
bool transport_benchmark_start(void);
bool transport_benchmark_start_ble_ota_probe(void);
bool transport_benchmark_start_ble_isolated_probe(void);
bool transport_benchmark_start_firmware_probe(bool isolate);
bool transport_benchmark_busy(void);
void transport_benchmark_cancel(void);
void transport_benchmark_diagnostics(char *out,size_t size);
