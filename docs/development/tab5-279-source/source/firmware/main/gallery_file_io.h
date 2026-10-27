#pragma once
#include <stdatomic.h>
#include <stdint.h>
#ifndef GALLERY_STORE_TEST
#include "esp_heap_caps.h"
#include "esp_timer.h"
#endif

// ESP-IDF 5.4 sdmmc_cmd.c falls back to one command per sector for
// unaligned buffers. Old .aig layouts place JPEGs after an 80-byte header
// (or variable metadata), so merely aligning the JPEG allocation is not
// enough. Keep the on-card format: handle the edge separately, then copy
// bounded multi-sector spans through one aligned PSRAM buffer.
#define STORE_IO_BLOCK (32u*1024u)
#define STORE_IO_ALIGNMENT 512u
#ifdef GALLERY_STORE_TEST
#ifndef STORE_NATIVE_WRITE
#define STORE_NATIVE_WRITE _write
#endif
#define STORE_NATIVE_READ _read
#define STORE_FILENO _fileno
#else
#define STORE_NATIVE_WRITE write
#define STORE_NATIVE_READ read
#define STORE_FILENO fileno
#endif
static atomic_uint io_read_bytes,io_write_bytes,io_read_us,io_write_us,io_sync_us,io_sync_calls,io_bulk_bytes,io_edge_bytes;
static unsigned store_clock(void) {
#ifdef GALLERY_STORE_TEST
    return 0;
#else
    return (unsigned)esp_timer_get_time();
#endif
}
void gallery_store_profile_reset(void) {
    atomic_store(&io_read_bytes,0);atomic_store(&io_write_bytes,0);atomic_store(&io_read_us,0);atomic_store(&io_write_us,0);
    atomic_store(&io_sync_us,0);atomic_store(&io_sync_calls,0);atomic_store(&io_bulk_bytes,0);atomic_store(&io_edge_bytes,0);
}
gallery_io_profile gallery_store_profile_get(void) {
    return (gallery_io_profile){atomic_load(&io_read_bytes),atomic_load(&io_write_bytes),atomic_load(&io_read_us),atomic_load(&io_write_us),
        atomic_load(&io_sync_us),atomic_load(&io_sync_calls),atomic_load(&io_bulk_bytes),atomic_load(&io_edge_bytes)};
}
static FILE *store_fopen(const char *path,const char *mode) {
    FILE *file=fopen(path,mode);
    // Do not let newlib's default small stdio buffer split the bulk syscall.
    if(file&&setvbuf(file,NULL,_IONBF,0)){fclose(file);return NULL;}
    return file;
}
static size_t store_transfer(FILE *file,void *bytes,size_t size,bool writing) {
    if(!size)return 0;
    long position=ftell(file);if(position<0)return 0;
    uint8_t *buffer=NULL;size_t done=0;
    unsigned began=store_clock();
    while(done<size) {
        size_t edge=((size_t)position+done)%STORE_IO_ALIGNMENT;
        size_t count=size-done;
        bool bulk=!edge&&count>=STORE_IO_ALIGNMENT;
        if(edge){size_t limit=STORE_IO_ALIGNMENT-edge;if(count>limit)count=limit;}
        else if(bulk){if(count>STORE_IO_BLOCK)count=STORE_IO_BLOCK;count-=count%STORE_IO_ALIGNMENT;}
        uint8_t *part=(uint8_t *)bytes+done;
        if(bulk) {
            if(!buffer) {
#ifdef GALLERY_STORE_TEST
                buffer=_aligned_malloc(STORE_IO_BLOCK,STORE_IO_ALIGNMENT);
#else
                buffer=heap_caps_aligned_alloc(STORE_IO_ALIGNMENT,STORE_IO_BLOCK,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
#endif
                if(!buffer){errno=ENOMEM;break;}
            }
            if(writing)memcpy(buffer,part,count);
            part=buffer;
        }
        long result;
        do {result=writing?STORE_NATIVE_WRITE(STORE_FILENO(file),part,count):STORE_NATIVE_READ(STORE_FILENO(file),part,count);}while(result<0&&errno==EINTR);
        size_t n=result>0?(size_t)result:0;
        if(!writing&&bulk&&n)memcpy((uint8_t *)bytes+done,part,n);
        atomic_fetch_add(bulk?&io_bulk_bytes:&io_edge_bytes,n);
        done+=n;if(n!=count)break;
    }
    int saved_errno=errno;
    // Reconcile FILE's cached position after direct POSIX I/O; all handles
    // are unbuffered. Later metadata/seek/truncate code sees the exact offset.
    if(fseek(file,position+(long)done,SEEK_SET)){done=0;saved_errno=errno;}
#ifdef GALLERY_STORE_TEST
    _aligned_free(buffer);
#else
    free(buffer);
#endif
    atomic_fetch_add(writing?&io_write_bytes:&io_read_bytes,done);
    atomic_fetch_add(writing?&io_write_us:&io_read_us,store_clock()-began);
    errno=saved_errno;if(done<size)io_error();return done;
}
static size_t store_fread(void *bytes,size_t size,size_t count,FILE *file){return size?store_transfer(file,bytes,size*count,false)/size:0;}
static size_t store_fwrite(const void *bytes,size_t size,size_t count,FILE *file){return size?store_transfer(file,(void *)bytes,size*count,true)/size:0;}
static int store_sync_profiled(FILE *file) {
    unsigned began=store_clock();int result=store_sync(file);
    atomic_fetch_add(&io_sync_us,store_clock()-began);atomic_fetch_add(&io_sync_calls,1);return result;
}
