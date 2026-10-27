#pragma once
#include <stddef.h>
#include <stdint.h>
typedef int nvs_handle_t;
typedef int esp_err_t;
#define ESP_OK 0
#define NVS_READONLY 0
#define NVS_READWRITE 1
int nvs_open(const char *name,int mode,nvs_handle_t *h);
void nvs_close(nvs_handle_t h);
int nvs_get_blob(nvs_handle_t h,const char *key,void *data,size_t *length);
int nvs_set_blob(nvs_handle_t h,const char *key,const void *data,size_t length);
int nvs_commit(nvs_handle_t h);
int nvs_get_u8(nvs_handle_t h,const char *key,uint8_t *value);
int nvs_set_u8(nvs_handle_t h,const char *key,uint8_t value);
int nvs_get_u16(nvs_handle_t h,const char *key,uint16_t *value);
int nvs_set_u16(nvs_handle_t h,const char *key,uint16_t value);
