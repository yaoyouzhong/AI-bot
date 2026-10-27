#pragma once
#include "gallery_remote.h"

#define GALLERY_JPEG_CACHE_BYTES (10u*1024u*1024u)
#define GALLERY_JPEG_CACHE_SLOTS 64
typedef struct {
    gallery_image key;
    uint8_t *data;
    size_t size,capacity;
    unsigned stamp;
} gallery_jpeg;
// The owner serializes access. Taking an entry transfers its allocation to the
// decoder, so reclaiming the remaining cache cannot free an in-use JPEG.
typedef struct {
    gallery_jpeg entries[GALLERY_JPEG_CACHE_SLOTS];
    size_t bytes;
    unsigned stamp,generation,hits,misses;
    char date[11];
} gallery_jpeg_cache;
bool gallery_jpeg_has(const gallery_jpeg_cache *cache,const gallery_image *key);
gallery_jpeg gallery_jpeg_take(gallery_jpeg_cache *cache,const gallery_image *key);
void gallery_jpeg_put(gallery_jpeg_cache *cache,gallery_jpeg value,unsigned generation);
void gallery_jpeg_clear(gallery_jpeg_cache *cache);
void gallery_jpeg_clear_category(gallery_jpeg_cache *cache,int category);
