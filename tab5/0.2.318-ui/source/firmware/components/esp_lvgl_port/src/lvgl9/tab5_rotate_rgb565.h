#pragma once
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
#include <string.h>

// Quarter-turn RGB565 in small tiles. Reading full image columns repeatedly
// misses the PSRAM cache; a 64x16 tile keeps source and destination lines hot.
// Strides are in bytes, as in lv_draw_sw_rotate. Neither buffer may overlap.
static inline void tab5_rotate_rgb565_scalar(const void *src, void *dst, int width, int height,
                                            size_t src_stride, size_t dst_stride, bool clockwise)
{
    for (int ty = 0; ty < height; ty += 16) {
        int y_end = ty + 16 < height ? ty + 16 : height;
        for (int tx = 0; tx < width; tx += 64) {
            int x_end = tx + 64 < width ? tx + 64 : width;
            for (int y = ty; y < y_end; ++y) {
                const uint16_t *row = (const uint16_t *)((const uint8_t *)src + y * src_stride);
                for (int x = tx; x < x_end; ++x) {
                    int dx = clockwise ? height - 1 - y : y;
                    int dy = clockwise ? x : width - 1 - x;
                    *(uint16_t *)((uint8_t *)dst + dy * dst_stride + dx * sizeof(uint16_t)) = row[x];
                }
            }
        }
    }
}

// ESP32-P4 and the native test host are little endian. Each 2x2 transpose uses
// two aligned word loads/stores instead of four halfword loads/stores. Odd
// rectangles keep this path for their interior; only the edge uses halfwords.
#if defined(__GNUC__) || defined(__clang__)
typedef uint32_t tab5_pixel_pair __attribute__((__may_alias__));
#endif
static inline void tab5_rotate_rgb565_tiles(const void *src, void *dst, int width, int height,
                                     size_t src_stride, size_t dst_stride, bool clockwise, bool tall_tiles)
{
#if (defined(__GNUC__) || defined(__clang__)) && (!defined(__BYTE_ORDER__) || __BYTE_ORDER__ == __ORDER_LITTLE_ENDIAN__)
    if(width > 0 && height > 0 &&
       !(((uintptr_t)src | (uintptr_t)dst | src_stride | dst_stride) & 3)) {
        const int tile_h=tall_tiles?64:16, tile_w=tall_tiles?32:64;
        // A clockwise odd-height strip starts at row one to keep destination
        // pairs word-aligned. LVGL's 81920-pixel buffer often produces 1220x67
        // damage strips, so falling back for the entire strip is expensive.
        const int y_begin=clockwise?(height&1):0;
        const int y_limit=height-((height-y_begin)&1),x_limit=width&~1;
        for(int ty = y_begin; ty < y_limit; ty += tile_h) {
            int y_end = ty + tile_h < y_limit ? ty + tile_h : y_limit;
            for(int tx = 0; tx < x_limit; tx += tile_w) {
                int x_end = tx + tile_w < x_limit ? tx + tile_w : x_limit;
                for(int y = ty; y < y_end; y += 2) {
                    const tab5_pixel_pair *top = (const tab5_pixel_pair *)((const uint8_t *)src + y * src_stride);
                    const tab5_pixel_pair *bottom = (const tab5_pixel_pair *)((const uint8_t *)src + (y + 1) * src_stride);
                    int dx = clockwise ? height - 2 - y : y;
                    for(int x = tx; x < x_end; x += 2) {
                        uint32_t a = top[x / 2], b = bottom[x / 2];
                        int dy = clockwise ? x : width - 1 - x;
                        uint8_t *out = (uint8_t *)dst + dy * dst_stride + dx * 2;
                        if(clockwise) {
                            *(tab5_pixel_pair *)out = (b & 0xffffu) | (a << 16);
                            *(tab5_pixel_pair *)(out + dst_stride) = (b >> 16) | (a & 0xffff0000u);
                        } else {
                            *(tab5_pixel_pair *)out = (a & 0xffffu) | (b << 16);
                            *(tab5_pixel_pair *)(out - dst_stride) = (a >> 16) | (b & 0xffff0000u);
                        }
                    }
                }
            }
        }
        for(int y=0;y<height;y++) {
            const uint16_t *row=(const uint16_t *)((const uint8_t *)src+y*src_stride);
            int start=(y<y_begin||y>=y_limit)?0:x_limit;
            for(int x=start;x<width;x++) {
                int dx=clockwise?height-1-y:y,dy=clockwise?x:width-1-x;
                *(uint16_t *)((uint8_t *)dst+dy*dst_stride+dx*2)=row[x];
            }
        }
        return;
    }
#endif
    tab5_rotate_rgb565_scalar(src, dst, width, height, src_stride, dst_stride, clockwise);
}

static inline void tab5_rotate_rgb565(const void *src, void *dst, int width, int height,
                                     size_t src_stride, size_t dst_stride, bool clockwise)
{
    tab5_rotate_rgb565_tiles(src,dst,width,height,src_stride,dst_stride,clockwise,false);
}

// Transpose in a caller-owned 8 KiB internal-RAM workspace. External RAM is
// accessed only through contiguous row copies; no random halfword writes.
// Also covers clipped/odd damage, padding and both quarter-turn directions.
static inline void tab5_rotate_rgb565_staged(const void *src,void *dst,int width,int height,
                                            size_t src_stride,size_t dst_stride,bool clockwise,
                                            uint16_t *workspace)
{
    if(!workspace){tab5_rotate_rgb565(src,dst,width,height,src_stride,dst_stride,clockwise);return;}
    uint16_t *input=workspace,*output=workspace+2048;
    for(int ty=0;ty<height;ty+=64)for(int tx=0;tx<width;tx+=32) {
        int w=width-tx<32?width-tx:32,h=height-ty<64?height-ty:64;
        for(int y=0;y<h;y++)memcpy(input+y*32,(const uint8_t *)src+(ty+y)*src_stride+tx*2,w*2);
        tab5_rotate_rgb565_tiles(input,output,w,h,64,128,clockwise,true);
        int dx=clockwise?height-ty-h:ty,dy=clockwise?tx:width-tx-w;
        for(int y=0;y<w;y++)memcpy((uint8_t *)dst+(dy+y)*dst_stride+dx*2,output+y*64,h*2);
    }
}

// Prefer sequential external-RAM stores when the source strip is in internal
// RAM. Reads stride through the source; adaptive timing decides applicability.
static inline void tab5_rotate_rgb565_output_rows(const void *src,void *dst,int width,int height,
                                                  size_t src_stride,size_t dst_stride,bool clockwise)
{
    for(int dy=0;dy<width;dy++) {
        int x=clockwise?dy:width-1-dy;
        uint8_t *out=(uint8_t *)dst+dy*dst_stride;
        int dx=0;
#if (defined(__GNUC__) || defined(__clang__)) && (!defined(__BYTE_ORDER__) || __BYTE_ORDER__ == __ORDER_LITTLE_ENDIAN__)
        if(!((uintptr_t)out&3))for(;dx+1<height;dx+=2) {
            int y=clockwise?height-1-dx:dx;
            uint32_t a=*(const uint16_t *)((const uint8_t *)src+y*src_stride+x*2);
            uint32_t b=*(const uint16_t *)((const uint8_t *)src+(y+(clockwise?-1:1))*src_stride+x*2);
            *(tab5_pixel_pair *)(out+dx*2)=a|(b<<16);
        }
#endif
        for(;dx<height;dx++) {
            int y=clockwise?height-1-dx:dx;
            *(uint16_t *)(out+dx*2)=*(const uint16_t *)((const uint8_t *)src+y*src_stride+x*2);
        }
    }
}
