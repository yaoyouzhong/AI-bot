#pragma once
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
typedef int esp_err_t;
#define ESP_OK 0
#define PPA_OPERATION_FILL 1
#define PPA_DATA_BURST_LENGTH_32 32
#define PPA_FILL_COLOR_MODE_RGB565 1
#define PPA_TRANS_MODE_BLOCKING 0
#define MALLOC_CAP_SPIRAM 1
#define MALLOC_CAP_8BIT 2
typedef void *ppa_client_handle_t;
typedef struct {unsigned oper_type,max_pending_trans_num,data_burst_length;} ppa_client_config_t;
typedef struct {
    struct {void *buffer;unsigned buffer_size,pic_w,pic_h,block_offset_x,block_offset_y,fill_cm;} out;
    unsigned fill_block_w,fill_block_h;
    struct {uint8_t a,r,g,b;} fill_argb_color;
    int mode;
} ppa_fill_oper_config_t;
bool esp_ptr_external_ram(const void *);
void *heap_caps_aligned_alloc(size_t,size_t,unsigned);
void heap_caps_free(void *);
esp_err_t ppa_register_client(const ppa_client_config_t *,ppa_client_handle_t *);
esp_err_t ppa_unregister_client(ppa_client_handle_t);
esp_err_t ppa_do_fill(ppa_client_handle_t,const ppa_fill_oper_config_t *);
