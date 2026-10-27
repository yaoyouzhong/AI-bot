#include "device.h"
#include "rpc_link.h"
#include "assets.h"
#include "voice_remote.h"
#include "camera_preview.h"
#include "wifi_status.h"
#include "wireless_boot.h"
#include "crash_record.h"
#include "battery_status.h"
#include "sd_storage.h"
#include "art_orientation.h"
#include "gallery_clock.h"
#include "agenda.h"
#include "nvs_flash.h"
#include "esp_ota_ops.h"
#include "bsp/m5stack_tab5.h"
#include "heap_memory_layout.h"
#include "soc/soc.h"
#include "esp_attr.h"
#include <stdio.h>

volatile unsigned tab5_lcd_underruns;
extern unsigned tab5_ui_age_ms(void);
void IRAM_ATTR tab5_lcd_underrun(void) { tab5_lcd_underruns++; }

// IDF 5.4.2 offers P4 TCM as generic internal heap, but its FreeRTOS
// TCB/stack validators exclude that address range. Reserve the remaining
// 8 KiB pool before heap initialization; keep all RTOS safety checks intact.
// Hardware reproduction: Tmr Svc TCB allocated at 0x301001e8, rejected at boot.
extern int _tcm_data_end;
SOC_RESERVE_MEMORY_REGION((intptr_t)&_tcm_data_end, SOC_TCM_HIGH, tab5_tcm_not_general_heap);

void app_main(void) {
    crash_record_init();
    // Preserve existing NVS; an incompatible/full store must be handled explicitly.
    ESP_ERROR_CHECK(nvs_flash_init());device_init();
    ESP_ERROR_CHECK(bsp_i2c_init());bsp_io_expander_pi4ioe_init(bsp_i2c_get_handle());
    bsp_set_charge_qc_en(true);bsp_set_charge_en(true);
    ESP_ERROR_CHECK(tab5_wireless_boot_prepare());
    // Keep radio DMA/tasks ahead of large USB/audio/UI allocations, as in the
    // original constructor, while making the board-power ordering explicit.
    sd_storage_init();rpc_link_init();camera_preview_init_diagnostics();
    if(!camera_preview_init_buffers())printf("TAB5_CAMERA_INIT unavailable\n");
    assets_init();usb_start();
    if(!voice_remote_init())printf("TAB5_VOICE_INIT unavailable\n");
    sd_storage_start();
    battery_status_start();
    gallery_clock_start();
    agenda_start();
    ui_start();tab5_wifi_start();
    art_orientation_start();
    boot_trace_update(2,0,0);
    // Boot checks above must succeed before a newly installed OTA image is accepted.
    // A crash during early device/display initialization leaves the image pending
    // and allows the bootloader to return to the previous slot.
    const esp_partition_t *running=esp_ota_get_running_partition();
    esp_ota_img_states_t state;
    if(running&&esp_ota_get_state_partition(running,&state)==ESP_OK&&state==ESP_OTA_IMG_PENDING_VERIFY) {
        unsigned healthy=0;
        // Require 30 consecutive healthy seconds, not a remote PC or Internet.
        for(unsigned elapsed=0;elapsed<90;elapsed++) {
            vTaskDelay(pdMS_TO_TICKS(1000));
            healthy=tab5_ui_age_ms()<2000&&tab5_wifi_initialized?healthy+1:0;
            boot_trace_update(3,healthy,0);
            if(healthy==30){
                esp_err_t result=esp_ota_mark_app_valid_cancel_rollback();
                boot_trace_update(4,healthy,result);ESP_ERROR_CHECK(result);return;
            }
        }
        boot_trace_update(5,healthy,ESP_ERR_TIMEOUT);
        esp_ota_mark_app_invalid_rollback_and_reboot();
    }
    boot_trace_update(4,0,0);
}
