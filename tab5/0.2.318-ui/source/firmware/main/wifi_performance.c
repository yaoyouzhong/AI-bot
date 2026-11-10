#include "wifi_performance.h"
#include "esp_wifi.h"
#include "freertos/FreeRTOS.h"
#include "freertos/semphr.h"
#include <stdio.h>

// Only worker tasks enter this lock. ESP-Hosted calls may block; never hold
// device_mutex, run on NimBLE, or persist this temporary policy to NVS.
static SemaphoreHandle_t lock;
static unsigned users;
static wifi_ps_type_t original;
static bool changed;
static int last_error;
void wifi_performance_init(void){if(!lock)lock=xSemaphoreCreateMutex();}
static void restore(void) {
    if(changed&&!users){last_error=esp_wifi_set_ps(original);if(last_error==ESP_OK)changed=false;}
}
void wifi_performance_bulk(bool active) {
    if(!lock)return;
    xSemaphoreTake(lock,portMAX_DELAY);
    if(active) {
        if(users++==0&&!changed) {
            last_error=esp_wifi_get_ps(&original);
            if(last_error==ESP_OK&&original!=WIFI_PS_NONE){last_error=esp_wifi_set_ps(WIFI_PS_NONE);changed=last_error==ESP_OK;}
        }
    }else if(users){users--;restore();}
    xSemaphoreGive(lock);
}
void wifi_performance_idle(void){if(!lock)return;xSemaphoreTake(lock,portMAX_DELAY);restore();xSemaphoreGive(lock);}
void wifi_performance_diagnostics(char *out,size_t size) {
    if(!lock){snprintf(out,size,"0,0,-1");return;}
    // Diagnostics must never stall the USB receive loop behind ESP-Hosted.
    if(xSemaphoreTake(lock,0)!=pdTRUE){snprintf(out,size,"busy");return;}
    snprintf(out,size,"%u,%u,%d",users,changed?1u:0u,last_error);xSemaphoreGive(lock);
}
