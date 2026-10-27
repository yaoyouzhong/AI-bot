#pragma once
#include "gallery_jpeg_cache.h"
#include "cJSON.h"

typedef uint8_t *(*gallery_store_allocator)(size_t bytes,size_t *capacity);
// Portable filesystem core; the caller owns the shared TF-card lock.
// Each record binds the JPEG and all reading text to one date/layout/revision.
bool gallery_store_get(const char *root,const gallery_image *key,const char *revision,
                       gallery_store_allocator allocate,gallery_jpeg *out,cJSON **guide);
bool gallery_store_put(const char *root,const gallery_jpeg *value,const char *revision,const gallery_guide *guide);
// Rebind unchanged verified JPEG bytes through alternating metadata-only records.
// The referenced complete image remains immutable; interrupted metadata cannot
// replace the last valid image/text record.
bool gallery_store_refresh(const char *root,const gallery_jpeg *value,const char *revision,const gallery_guide *guide);
bool gallery_store_media_error(void);
typedef struct { unsigned read_bytes,write_bytes,read_us,write_us,sync_us,sync_calls,bulk_bytes,edge_bytes; } gallery_io_profile;
void gallery_store_profile_reset(void);
gallery_io_profile gallery_store_profile_get(void);
// One bounded in-progress image per category. Checksummed append records are
// never display sources; final image/text commit still uses alternating .aig.
#define GALLERY_RESUME_BLOCK (192u*1024u)
typedef struct { size_t offset,file_bytes; bool direct; } gallery_resume;
bool gallery_resume_load(const char *root,const gallery_image *key,const char *revision,const char *sha,
                         uint8_t *jpeg,size_t total,gallery_resume *state);
bool gallery_resume_append(const char *root,const gallery_image *key,const char *revision,const char *sha,
                           const uint8_t *jpeg,size_t total,size_t end,gallery_resume *state);
bool gallery_store_finish(const char *root,const gallery_jpeg *value,const char *revision,const gallery_guide *guide,const gallery_resume *state);
bool gallery_resume_clear(const char *root,const gallery_image *key,const char *revision,const char *sha,size_t total);
typedef struct { char revision[65]; unsigned works,next; } gallery_catalog;
bool gallery_catalog_load(const char *root,int category,const char *revision,gallery_catalog *out);
bool gallery_catalog_save(const char *root,int category,const gallery_catalog *value);
bool gallery_catalog_date(unsigned index,char out[11]);
bool gallery_catalog_key(const char *date,unsigned works,char out[11]);

// Reserved, bounded diagnostic namespace; existing library remains untouched.
bool gallery_speed_prepare(const char *mount);
