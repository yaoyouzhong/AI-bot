// Fault injection at each actual allocator step; diagnostics must precede unwind.
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <stdarg.h>
#include <string.h>
#define MALLOC_CAP_8BIT 4
#define MALLOC_CAP_SPIRAM 1024
#define MALLOC_CAP_CACHE_ALIGNED (1u << 19)
#define V4L2_MEMORY_MMAP 1
#define V4L2_MEMORY_USERPTR 2
#define ESP_OK 0
#define ELEMENT_SET_FREE(e) ((e)->free = true)
typedef int esp_err_t;
struct esp_video_buffer_info { uint32_t count,size,align_size,caps,memory_type; };
struct esp_video_buffer;
struct esp_video_buffer_element {
    bool free; void *node; struct esp_video_buffer *video_buffer;
    uint32_t index; uint8_t *buffer; uint32_t valid_size;
};
struct esp_video_buffer {
    struct esp_video_buffer_info info; struct esp_video_buffer_element element[0];
};
static unsigned call_index,fail_at,live_blocks,live_bytes,logged_bytes,query_count;
static char diagnostic[180];
static const unsigned capacity=12u*1024*1024;
static void *allocate(size_t bytes,bool clear) {
    if (++call_index==fail_at)return NULL;
    size_t *block=clear?calloc(1,sizeof(size_t)+bytes):malloc(sizeof(size_t)+bytes);
    assert(block);*block=bytes;live_blocks++;live_bytes+=(unsigned)bytes;
    return block+1;
}
static void *heap_caps_calloc(size_t n,size_t size,unsigned caps) {
    assert(caps==(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT|MALLOC_CAP_CACHE_ALIGNED));
    return allocate(n*size,true);
}
static void *heap_caps_aligned_alloc(size_t alignment,size_t size,unsigned caps) {
    assert(alignment==64&&size==1280*720*2);
    assert(caps==(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT|MALLOC_CAP_CACHE_ALIGNED));
    return allocate(size,false);
}
static void heap_caps_free(void *p) {
    if(!p)return;
    size_t *block=(size_t *)p-1;
    assert(live_blocks&&live_bytes>=*block);
    live_blocks--;live_bytes-=(unsigned)*block;free(block);
}
static size_t heap_caps_get_free_size(unsigned caps) {
    assert(caps==(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT));
    query_count++;return capacity-live_bytes;
}
static size_t heap_caps_get_largest_free_block(unsigned caps) {
    assert(caps==(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT));
    query_count++;return (capacity-live_bytes)/2;
}
static void record_error(const char *format,...) {
    va_list args;va_start(args,format);
    vsnprintf(diagnostic,sizeof(diagnostic),format,args);va_end(args);
    logged_bytes=live_bytes;
}
#define ESP_LOGE(tag,...) record_error(__VA_ARGS__)
#include "camera_buffer_alloc_under_test.inc"
static void reset(unsigned failure) {
    assert(!live_blocks&&!live_bytes);
    fail_at=failure;call_index=query_count=logged_bytes=0;diagnostic[0]=0;
}
int main(void) {
    const struct esp_video_buffer_info info={3,1280*720*2,64,
        MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT|MALLOC_CAP_CACHE_ALIGNED,V4L2_MEMORY_MMAP};
    for(unsigned failure=1;failure<=4;failure++) {
        reset(failure);
        assert(!esp_video_buffer_create(&info));
        assert(!live_blocks&&!live_bytes&&query_count==2);
        if(failure==1)assert(strstr(diagnostic,"metadata size="));
        else {
            char expected[32];snprintf(expected,sizeof(expected),"frame=%u/3 ",failure-1);
            assert(strstr(diagnostic,expected));
            assert(strstr(diagnostic,"size=1843200 align=64"));
            assert(logged_bytes==(sizeof(struct esp_video_buffer)+3*sizeof(struct esp_video_buffer_element))+(failure-2)*info.size);
        }
        // A failed partial pool must not prevent the next full-resolution retry.
        reset(0);
        struct esp_video_buffer *buffer=esp_video_buffer_create(&info);
        assert(buffer&&live_blocks==4&&!diagnostic[0]);
        for(unsigned i=0;i<3;i++)assert(buffer->element[i].index==i&&buffer->element[i].free&&
            buffer->element[i].video_buffer==buffer&&buffer->element[i].buffer);
        assert(esp_video_buffer_destroy(buffer)==ESP_OK);
    }
    reset(0);
    struct esp_video_buffer_info external=info;external.memory_type=V4L2_MEMORY_USERPTR;
    struct esp_video_buffer *buffer=esp_video_buffer_create(&external);
    assert(buffer&&live_blocks==1);
    for(unsigned i=0;i<3;i++)buffer->element[i].buffer=(void *)(uintptr_t)(i+1);
    assert(esp_video_buffer_destroy(buffer)==ESP_OK&&!live_blocks&&!live_bytes);
    puts("CAMERA_BUFFER_ALLOC_OK: metadata/three frame failures captured before cleanup; retry and USERPTR ownership preserved");
}
