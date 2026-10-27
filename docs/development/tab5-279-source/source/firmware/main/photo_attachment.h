#pragma once
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
#define PHOTO_MAX_BYTES (2*1024*1024)
#define PHOTO_PREVIEW_W 640
#define PHOTO_PREVIEW_H 360
#define PHOTO_LIST_COUNT 20
typedef struct {char name[256];bool folder;} photo_entry;
typedef struct {int state,count,page;bool more;char directory[512],error[96];photo_entry entries[PHOTO_LIST_COUNT];} photo_listing;
// All filesystem and codec work runs on a bounded PSRAM worker, never on LVGL.
bool photo_prepare_camera(void);
bool photo_prepare_file(const char *path);
bool photo_list_directory(const char *path,int page);
const photo_listing *photo_get_listing(void);
int photo_state(void); // 0 idle, 1 working, 2 preview ready, 3 error
const char *photo_error(void);
void photo_diagnostics(char *out,unsigned size);
const uint8_t *photo_data(size_t *size);
const uint16_t *photo_preview(void);
// Move the completed preview to an attachment; the caller becomes its owner.
uint16_t *photo_take_preview(void);
void photo_preview_dimensions(unsigned *width,unsigned *height);
void photo_discard(void);
