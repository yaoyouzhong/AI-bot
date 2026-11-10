#pragma once
#include "link_state.h"
#include "cJSON.h"
#include <stdbool.h>
extern int device_mutex;
extern bool ble_connected;
extern link_state device_links;
extern cJSON *device_snapshots[3];
unsigned device_host_epoch(void);
bool device_host_changing(void);
bool device_host_usb_owned(void);
bool tab5_usb_connected(void);
bool ble_host_authorized(void);
#define portMAX_DELAY 0
#define pdPASS 1
#define pdMS_TO_TICKS(x) (x)
void xSemaphoreTake(int mutex,int timeout);
void xSemaphoreGive(int mutex);
int xTaskCreate(void (*run)(void *),const char *name,int stack,void *arg,int priority,void *handle);
void vTaskDelay(unsigned ms);
void vTaskDelete(void *handle);
