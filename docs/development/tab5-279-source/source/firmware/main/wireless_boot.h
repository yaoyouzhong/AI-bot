#pragma once
#include "esp_err.h"
#include <stddef.h>
// Only app_main calls this, before esp_wifi_init and NimBLE startup.
esp_err_t tab5_wireless_boot_prepare(void);
void tab5_wireless_boot_diagnostics(char *out,size_t size);
