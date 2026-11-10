#pragma once
#include <stdbool.h>
#include <stdint.h>
#include <stddef.h>
typedef struct {uint8_t *pixels;int category,frame,frames;char date[11],work_id[80];bool portrait;} gallery_image;
typedef struct {
    char date[11],work_id[80],title[256],author[128],period[96],museum[80],brief[256];
    char overview[640],looking[384],transcription[8192],explanation[512];
    // Separately bounded wire pages; never enlarge the original label field.
    int transcription_page_count;
    char transcription_sha256[65];
    char *transcription_pages[32];
} gallery_guide;
bool gallery_guide_copy(int category,const char *date,const char *work_id,gallery_guide *out);
bool gallery_guide_page_copy(int category,const char *date,const char *work_id,int page,gallery_guide *out);
// A reader owns this deep snapshot until release, across pack/cache replacement.
bool gallery_guide_reader_copy(int category,const char *date,const char *work_id,gallery_guide *out);
void gallery_guide_reader_release(gallery_guide *guide);
// Only recycle decoder buffers after detaching them from LVGL.
void gallery_recycle(uint8_t *pixels);
// Permanently detach a gallery owner, returning a borrowed camera frame safely.
void gallery_release_buffer(uint8_t *pixels);
uint8_t *gallery_take_buffer(void);
bool gallery_request_ready(void);
// Apply authenticated pack digests only while workers are idle. Returns the
// affected categories; absent fields from older bridges preserve local data.
unsigned gallery_pack_sync(const char *painting,const char *calligraphy);
bool gallery_request_available(int category,const char *date,int frame,bool portrait);
// Background JPEG-only synchronization; no additional RGB output frame.
void gallery_prefetch_day(const char *date);
// Persist both installed collections in the background over USB/Wi-Fi.
void gallery_sync_library(void);
bool gallery_library_pending(void);
void gallery_library_supported(bool enabled);
void gallery_storage_status(char *out,size_t size);
void gallery_storage_enable(bool enabled); // Explicit user choice, default off.
#if TAB5_LOCAL_ART
void gallery_cache_reclaim(void);
// Camera worker only: wait for cancelled workers, release idle HTTP and spare
// decoder storage. Never free frames still referenced by LVGL.
bool gallery_camera_prepare(void);
#else
static inline void gallery_cache_reclaim(void){}
static inline bool gallery_camera_prepare(void){return true;}
#endif
// Takes ownership of an optional decoder-allocated output buffer, even on rejection.
bool gallery_request(int category,const char *date,int frame,bool portrait,uint8_t *spare);
bool gallery_take(gallery_image *out);
// 0: waiting/available, 1: optional pack missing, 2: installed pack incomplete.
int gallery_pack_status(int category);
// Last foreground decode outcome for this category/orientation only.
enum {GALLERY_DECODE_OK, GALLERY_DECODE_NO_MEMORY, GALLERY_DECODE_FAILED};
int gallery_decode_status(int category,bool portrait);
void gallery_diagnostics(char *out,size_t size);
unsigned gallery_take_failures(void);

void gallery_transfer_diagnostics(char *out,size_t size);

// Eight real samples, no installed gallery/cache/cursor changes.
bool gallery_speed_start(void);
void gallery_speed_cancel(void);
void gallery_speed_diagnostics(char *out,size_t size);
