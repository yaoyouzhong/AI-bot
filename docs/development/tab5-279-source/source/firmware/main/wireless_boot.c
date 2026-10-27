#ifndef TAB5_WIRELESS_BOOT_TEST
#include "wireless_boot.h"
#include "bsp/m5stack_tab5.h"
#include "esp_hosted_api.h"
#include "esp_system.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#endif
#include <stdio.h>
static int boot_reason,boot_cycled,boot_result;

esp_err_t tab5_wireless_boot_prepare(void) {
    // OTA resets the P4, while the independently powered C6 can retain state.
    // Do this before any SDK SDIO task, Wi-Fi association or BLE session exists.
    // Never power-cycle a live radio in response to an ordinary AP failure.
    boot_reason=esp_reset_reason();boot_cycled=boot_reason==ESP_RST_SW;
    if(boot_cycled) {
        bsp_set_wifi_power_enable(false);
        vTaskDelay(pdMS_TO_TICKS(500));
    }
    bsp_set_wifi_power_enable(true);
    // Keep the original SDK's reset/handshake and error reporting. There is no
    // dependency patch, credential write, or assumed successful connection.
    boot_result=esp_hosted_init();return boot_result;
}
void tab5_wireless_boot_diagnostics(char *out,size_t size) {
    snprintf(out,size,"1,%d,%d,%d",boot_reason,boot_cycled,boot_result);
}
