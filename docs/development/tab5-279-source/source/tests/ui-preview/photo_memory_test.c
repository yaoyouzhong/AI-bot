#ifdef NDEBUG
#undef NDEBUG
#endif
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdatomic.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <malloc.h>
#include "../../firmware/main/photo_geometry.h"
#include "../../firmware/main/photo_attachment.h"
enum {ESP_OK=0,MALLOC_CAP_SPIRAM=1,MALLOC_CAP_8BIT=2,ESP_CACHE_MSYNC_FLAG_DIR_M2C=4};
static unsigned yields,calls,fault;
static size_t extent,synced;
static uint8_t *owned;
static uint16_t *preview;
static atomic_int phase;
static atomic_bool working;
static unsigned preview_width,preview_height;
static void vTaskDelay(unsigned ticks){assert(ticks==1);yields++;}
static int esp_cache_get_alignment(int caps,size_t *alignment){assert(caps==1);*alignment=128;return fault==1?1:0;}
static void *heap_caps_aligned_alloc(size_t alignment,size_t bytes,int caps){
    assert(alignment==128&&caps==3&&!owned&&!(bytes%128));
    if(fault==2)return NULL;
    extent=bytes;owned=_aligned_malloc(bytes,alignment);assert(owned);memset(owned,0xA5,bytes);return owned;
}
static int esp_cache_msync(void *p,size_t bytes,int flags){
    assert(p==owned+synced&&bytes<=16384&&!(bytes%128)&&flags==4);calls++;
    if(fault==3&&calls==2)return 1;
    synced+=bytes;assert(synced<=extent);return 0;
}
static void release(void *p){assert(p==owned);_aligned_free(p);owned=NULL;}
#define free release
#include "photo_output_alloc_under_test.inc"
#undef free
#include "photo_preview_under_test.inc"
#include "photo_take_preview_under_test.inc"
int main(void){
    for(fault=0;fault<4;fault++){
        yields=calls=0;synced=0;size_t capacity=0;
        uint8_t *p=photo_output_alloc(PHOTO_MAX_BYTES,&capacity);
        if(fault){assert(!p&&!owned&&!capacity);continue;}
        assert(capacity==PHOTO_MAX_BYTES&&synced==capacity&&calls==128&&yields==127);
        for(size_t i=0;i<capacity;i++)assert(p[i]==0xA5);release(p);
    }
    preview=malloc(PHOTO_PREVIEW_W*PHOTO_PREVIEW_H*2);assert(preview);
    uint16_t *pixels=malloc(1280*720*2);assert(pixels);
    for(unsigned pose=0;pose<2;pose++){
        unsigned w=pose?720:1280,h=pose?1280:720;yields=0;
        for(unsigned y=0;y<h;y++)for(unsigned x=0;x<w;x++)pixels[y*w+x]=(uint16_t)(1+(x/2+y/2)%65534);
        make_preview(pixels,w,h,w);
        assert(preview_width==(pose?360:640)&&preview_height==(pose?640:360));
        assert(preview_width*preview_height==230400&&yields==preview_height/4);
        for(unsigned y=0;y<preview_height;y++)for(unsigned x=0;x<preview_width;x++)
            assert(preview[y*preview_width+x]==pixels[(y*2)*w+x*2]);
    }
    atomic_store(&phase,2);atomic_store(&working,true);assert(!photo_take_preview());
    atomic_store(&working,false);atomic_store(&phase,3);assert(!photo_take_preview());
    atomic_store(&phase,2);uint16_t *original=preview,*taken=photo_take_preview();
    assert(taken==original&&!preview&&!photo_take_preview());
    free(pixels);free(taken);
    puts("PHOTO_MEMORY_OK aligned non-clearing output; 128 bounded cache spans; failure cleanup; both orientations use full preview budget without letterbox");
}
