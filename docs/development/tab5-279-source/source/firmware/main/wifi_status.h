#pragma once
#include <stddef.h>
#include <stdbool.h>
extern volatile bool tab5_wifi_initialized;
// Display-only snapshot: no credentials, transport commands or wire changes.
typedef struct {
    char network[104],address[16];
    const char *state,*bridge;
    bool connected,authenticated;
} tab5_wifi_status;
void tab5_wifi_status_read(tab5_wifi_status *out);
void tab5_wifi_diagnostics(char *out,size_t size);
void tab5_wifi_numeric_diagnostics(char *out,size_t size);
void tab5_wifi_recovery_diagnostics(char *out,size_t size);
void tab5_wifi_http_diagnostics(char *out,size_t size);
void tab5_wifi_path_diagnostics(char *out,size_t size);
void tab5_wifi_reconnect(void);
void tab5_wifi_wake(void);
void device_discovered_host(const char *host,int port,unsigned host_epoch);
