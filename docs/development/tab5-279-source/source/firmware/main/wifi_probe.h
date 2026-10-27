#pragma once
#include <stdbool.h>
#include <stddef.h>
bool wifi_probe_begin(void);
bool wifi_probe_begin_firmware(void);
// A foreground gallery download may isolate Wi-Fi in explicit BLE-only mode,
// without the USB control cable required by diagnostic benchmarks.
bool wifi_probe_begin_gallery(void);
// Briefly retain a successfully acquired radio pause for the next layout.
void wifi_probe_release_gallery(void);
// Actual bulk RPCs renew a35-second inactivity deadline only on authenticated
// success. Legacy gallery/probe leases keep their original absolute deadlines.
bool wifi_probe_begin_rpc_bulk(void);
void wifi_probe_release_rpc_bulk(bool succeeded);
bool wifi_probe_requires_usb(void);
void wifi_probe_end(void);
bool wifi_probe_ready(void);
bool wifi_probe_pending(void);
// Only the Wi-Fi worker may call this; true suppresses ordinary Wi-Fi work.
bool wifi_probe_service(bool allowed,bool *reconnect);
void wifi_probe_diagnostics(char *out,size_t size);
