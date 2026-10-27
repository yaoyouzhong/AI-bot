#include "sd_storage.h"
#ifndef SD_STORAGE_TEST
#include "bsp/m5stack_tab5.h"
#include "esp_timer.h"
#include "freertos/FreeRTOS.h"
#include "freertos/semphr.h"
#include "freertos/task.h"
#endif
#include <stdatomic.h>

static SemaphoreHandle_t mutex;
static atomic_int state;
static atomic_int_fast64_t retry_at;
static TaskHandle_t monitor;
void sd_storage_init(void){mutex=xSemaphoreCreateMutex();}
int sd_storage_state(void){return atomic_load(&state);}
bool sd_storage_available(void){return mutex&&esp_timer_get_time()/1000>=atomic_load(&retry_at);}
bool sd_storage_lock(void) {
    if(!sd_storage_available()||xSemaphoreTake(mutex,pdMS_TO_TICKS(1000))!=pdTRUE)return false;
    if(atomic_load(&state)!=1) {
        if(esp_timer_get_time()/1000<atomic_load(&retry_at)||bsp_sdcard_init(SD_STORAGE_MOUNT,6)!=ESP_OK) {
            atomic_store(&state,-1);atomic_store(&retry_at,esp_timer_get_time()/1000+30000);
            xSemaphoreGive(mutex);return false;
        }
        atomic_store(&state,1);
    }
    return true;
}
void sd_storage_unlock(bool media_error) {
    // Call only after closing every file/directory. Ordinary missing files and
    // full/read-only cards do not unmount the shared photo/gallery filesystem.
    if(media_error) {
        bsp_sdcard_deinit(SD_STORAGE_MOUNT);atomic_store(&state,-1);
        atomic_store(&retry_at,esp_timer_get_time()/1000+30000);
    }
    xSemaphoreGive(mutex);
}
static void sd_storage_poll(void) {
    // No UI-thread I/O. Sharing the lock with readers also makes removal safe.
    if(sd_storage_lock())sd_storage_unlock(bsp_sdcard_check()!=ESP_OK);
}
static void sd_storage_monitor(void *arg) {
    (void)arg;
    for(;;){sd_storage_poll();vTaskDelay(pdMS_TO_TICKS(5000));}
}
void sd_storage_start(void) {
    if(mutex&&!monitor)xTaskCreate(sd_storage_monitor,"tf_status",4096,NULL,1,&monitor);
}
