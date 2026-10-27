#include "lvgl.h"
#include "src/draw/sw/blend/lv_draw_sw_blend_private.h"
#include <string.h>
#include <stdint.h>
#include <stdlib.h>
#include "rgb565_copy.h"
#ifdef ESP_PLATFORM
#include "esp_timer.h"
#include "esp_heap_caps.h"
#endif

volatile unsigned tab5_copy_choice,tab5_copy_baseline_us,tab5_copy_candidate_us;
static bool copy_accelerated[2];
void tab5_fast_copy_init(void) {
#ifdef ESP_PLATFORM
    // Evaluate on PSRAM during startup, not during the user's first gesture.
    uint16_t *src=heap_caps_aligned_alloc(128,32768,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    uint16_t *dst=heap_caps_aligned_alloc(128,32768,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    if(!src||!dst){free(src);free(dst);tab5_copy_choice=1;return;}
    for(int i=0;i<16384;i++)src[i]=(uint16_t)(i*7919u);
    for(int alignment=0;alignment<2;alignment++) {
        unsigned best[2]={UINT32_MAX,UINT32_MAX};
        for(int pass=0;pass<6;pass++)for(int trial=0;trial<2;trial++) {
            int variant=(trial+pass)&1;int64_t started=esp_timer_get_time();
            for(int row=0;row<12;row++) {
                if(variant)tab5_rgb565_copy(dst+row*1280,src+row*1280+alignment,1220);
                else memcpy(dst+row*1280,src+row*1280+alignment,2440);
            }
            unsigned elapsed=(unsigned)(esp_timer_get_time()-started);if(elapsed<best[variant])best[variant]=elapsed;
            for(int row=0;row<12;row++)if(memcmp(dst+row*1280,src+row*1280+alignment,2440)){free(src);free(dst);tab5_copy_choice=1;return;}
        }
        copy_accelerated[alignment]=(uint64_t)best[1]*10<(uint64_t)best[0]*9;
        tab5_copy_baseline_us+=best[0];tab5_copy_candidate_us+=best[1];
    }
    free(src);free(dst);tab5_copy_choice=1+(copy_accelerated[0]?2:0)+(copy_accelerated[1]?4:0);
#endif
}
void __real_lv_draw_sw_blend_image_to_rgb565(lv_draw_sw_blend_image_dsc_t *);
void __wrap_lv_draw_sw_blend_image_to_rgb565(lv_draw_sw_blend_image_dsc_t *d) {
    if(d->src_color_format!=LV_COLOR_FORMAT_RGB565||d->blend_mode!=LV_BLEND_MODE_NORMAL||d->opa<LV_OPA_MAX||d->mask_buf||
       d->dest_w<16||d->dest_h<1||(((uintptr_t)d->src_buf|(uintptr_t)d->dest_buf|d->src_stride|d->dest_stride)&1)) {
        __real_lv_draw_sw_blend_image_to_rgb565(d);return;
    }
    const uint8_t *src=d->src_buf;uint8_t *dst=d->dest_buf;
    if(d->src_stride==d->dest_w*2&&d->dest_stride==d->dest_w*2) {
        unsigned alignment=(((uintptr_t)src^(uintptr_t)dst)&2)?1:0;
        if(copy_accelerated[alignment])tab5_rgb565_copy(dst,src,(size_t)d->dest_w*d->dest_h);
        else memcpy(dst,src,(size_t)d->dest_w*d->dest_h*2);
        return;
    }
    for(int y=0;y<d->dest_h;y++,src+=d->src_stride,dst+=d->dest_stride) {
        unsigned alignment=(((uintptr_t)src^(uintptr_t)dst)&2)?1:0;
        if(copy_accelerated[alignment])tab5_rgb565_copy(dst,src,d->dest_w);
        else memcpy(dst,src,d->dest_w*2);
    }
}

// Single LVGL software draw unit. Only immutable built-in fmt_txt fonts are
// cached; return the caller's buffer so LVGL retains its normal ownership.
#define GLYPH_SLOTS 512
#define GLYPH_BYTES 1024
typedef struct {
    const lv_font_t *font;
    uint32_t glyph, stride, height;
    uint8_t pixels[GLYPH_BYTES];
} glyph_entry;
static glyph_entry *glyphs;
static bool glyph_allocation_attempted;
volatile unsigned tab5_glyph_hits,tab5_glyph_misses,tab5_fast_fills,tab5_fast_masks;

const void * __real_lv_font_get_bitmap_fmt_txt(lv_font_glyph_dsc_t *,lv_draw_buf_t *);
const void * __wrap_lv_font_get_bitmap_fmt_txt(lv_font_glyph_dsc_t *g,lv_draw_buf_t *buf) {
    const lv_font_t *font=g->resolved_font;
    const lv_font_fmt_txt_dsc_t *f=font->dsc;
    size_t bytes=(size_t)buf->header.stride*buf->header.h;
    if(!g->gid.index||font->release_glyph||f->bitmap_format>LV_FONT_FMT_TXT_COMPRESSED||
       !bytes||bytes>GLYPH_BYTES||buf->header.cf!=LV_COLOR_FORMAT_A8)
        return __real_lv_font_get_bitmap_fmt_txt(g,buf);
    if(!glyph_allocation_attempted) {
        glyph_allocation_attempted=true;
        // LVGL allocations use PSRAM, preserving internal DMA/voice memory.
        glyphs=lv_malloc(sizeof(*glyphs)*GLYPH_SLOTS);
        if(glyphs)memset(glyphs,0,sizeof(*glyphs)*GLYPH_SLOTS);
    }
    if(!glyphs)return __real_lv_font_get_bitmap_fmt_txt(g,buf);
    unsigned slot=(g->gid.index*2654435761u+(uint32_t)((uintptr_t)font>>4))&(GLYPH_SLOTS-1);
    glyph_entry *entry=&glyphs[slot];
    if(entry->font==font&&entry->glyph==g->gid.index&&entry->stride==buf->header.stride&&entry->height==buf->header.h) {
        memcpy(buf->data,entry->pixels,bytes);tab5_glyph_hits++;return buf;
    }
    const void *result=__real_lv_font_get_bitmap_fmt_txt(g,buf);
    tab5_glyph_misses++;
    if(result==buf) {
        memcpy(entry->pixels,buf->data,bytes);
        entry->font=font;entry->glyph=g->gid.index;
        entry->stride=buf->header.stride;entry->height=buf->header.h;
    }
    return result;
}

typedef struct {bool valid;uint16_t foreground,background,pixels[256];} color_entry;
static color_entry colors[8];
static unsigned next_color;
void __real_lv_draw_sw_blend_color_to_rgb565(lv_draw_sw_blend_fill_dsc_t *);
void __wrap_lv_draw_sw_blend_color_to_rgb565(lv_draw_sw_blend_fill_dsc_t *d) {
    if(d->dest_w<=0||d->dest_h<=0||d->opa<LV_OPA_MAX) {
        __real_lv_draw_sw_blend_color_to_rgb565(d);return;
    }
    uint16_t color=lv_color_to_u16(d->color);
    uint8_t *row=d->dest_buf;
    if(!d->mask_buf) {
        // Fill one row, then use the P4 ROM memcpy path for identical rows.
        if(d->dest_h<2||d->dest_w<16) {__real_lv_draw_sw_blend_color_to_rgb565(d);return;}
        uint16_t *first=(uint16_t *)row;
        for(int x=0;x<d->dest_w;x++)first[x]=color;
        for(int y=1;y<d->dest_h;y++)memcpy(row+(size_t)y*d->dest_stride,row,(size_t)d->dest_w*2);
        tab5_fast_fills++;return;
    }
    // Glyphs and rounded edges commonly blend onto a uniform card color.
    // Use the exact LVGL mix results; arbitrary backgrounds take its original
    // arithmetic path, preserving all 256 mask levels without quantization.
    uint16_t background=*(uint16_t *)row;
    color_entry *entry=NULL;
    for(unsigned i=0;i<8;i++)if(colors[i].valid&&colors[i].foreground==color&&colors[i].background==background){entry=&colors[i];break;}
    if(!entry) {
        entry=&colors[next_color++%8];entry->valid=true;entry->foreground=color;entry->background=background;
        for(unsigned i=0;i<256;i++)entry->pixels[i]=lv_color_16_16_mix(color,background,(uint8_t)i);
    }
    for(int y=0;y<d->dest_h;y++) {
        uint16_t *dest=(uint16_t *)(row+(size_t)y*d->dest_stride);
        const uint8_t *mask=d->mask_buf+(size_t)y*d->mask_stride;
        for(int x=0;x<d->dest_w;x++) {
            uint8_t alpha=mask[x];
            if(!alpha)continue;
            if(alpha==255){dest[x]=color;continue;}
            dest[x]=dest[x]==background?entry->pixels[alpha]:lv_color_16_16_mix(color,dest[x],alpha);
        }
    }
    tab5_fast_masks++;
}
