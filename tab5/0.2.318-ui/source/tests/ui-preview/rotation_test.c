// Exercise the actual LVGL CPU rotation used by TAB5's display port.
#undef NDEBUG
#include "lvgl.h"
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../../firmware/components/esp_lvgl_port/src/lvgl9/tab5_rotate_rgb565.h"

static unsigned checked;
static void check(int w, int h, lv_display_rotation_t rotation, unsigned padding, unsigned offset) {
    int dw = rotation == LV_DISPLAY_ROTATION_180 ? w : h;
    int dh = rotation == LV_DISPLAY_ROTATION_180 ? h : w;
    uint32_t src_stride = lv_draw_buf_width_to_stride(w, LV_COLOR_FORMAT_RGB565) + padding;
    uint32_t dst_stride = lv_draw_buf_width_to_stride(dw, LV_COLOR_FORMAT_RGB565) + padding;
    size_t size = dst_stride * dh;
    uint8_t *input = calloc(1, h * src_stride + 32), *guarded = malloc(size + 32 + offset);
    assert(input && guarded);
    uint8_t *src = input + 16 + offset;
    memset(guarded, 0xa5, size + 32 + offset);
    uint8_t *dst = guarded + 16 + offset;
    for (int y = 0; y < h; ++y)
        for (int x = 0; x < w; ++x)
            ((uint16_t *)(src + y * src_stride))[x] = (uint16_t)(y * w + x + 1);
    lv_draw_sw_rotate(src, dst, w, h, src_stride, dst_stride, rotation, LV_COLOR_FORMAT_RGB565);
    // Compare the display-port tiled path against actual LVGL pixels, including
    // dimensions that cross both tile boundaries and leave partial edge tiles.
    if (rotation != LV_DISPLAY_ROTATION_180) {
        uint8_t *tiled = malloc(size + 32 + offset); assert(tiled);
        memset(tiled, 0xa5, size + 32 + offset);
        tab5_rotate_rgb565(src, tiled + 16 + offset, w, h, src_stride, dst_stride,
                          rotation == LV_DISPLAY_ROTATION_270);
        assert(!memcmp(tiled + 16 + offset, dst, size));
        for (unsigned i = 0; i < 16 + offset; ++i) assert(tiled[i] == 0xa5);
        for (int i = 0; i < 16; ++i) assert(tiled[size + 16 + offset + i] == 0xa5);
        memset(tiled,0xa5,size+32+offset);
        tab5_rotate_rgb565_tiles(src,tiled+16+offset,w,h,src_stride,dst_stride,
                                rotation==LV_DISPLAY_ROTATION_270,true);
        assert(!memcmp(tiled+16+offset,dst,size));
        for (unsigned i = 0; i < 16 + offset; ++i) assert(tiled[i] == 0xa5);
        for (int i = 0; i < 16; ++i) assert(tiled[size + 16 + offset + i] == 0xa5);
        uint16_t workspace[4096];
        memset(tiled,0xa5,size+32+offset);
        tab5_rotate_rgb565_staged(src,tiled+16+offset,w,h,src_stride,dst_stride,rotation==LV_DISPLAY_ROTATION_270,workspace);
        assert(!memcmp(tiled+16+offset,dst,size));
        for(unsigned i=0;i<16+offset;i++)assert(tiled[i]==0xa5);
        for(int i=0;i<16;i++)assert(tiled[size+16+offset+i]==0xa5);
        memset(tiled,0xa5,size+32+offset);
        tab5_rotate_rgb565_staged(src,tiled+16+offset,w,h,src_stride,dst_stride,rotation==LV_DISPLAY_ROTATION_270,NULL);
        assert(!memcmp(tiled+16+offset,dst,size));
        memset(tiled,0xa5,size+32+offset);
        tab5_rotate_rgb565_output_rows(src,tiled+16+offset,w,h,src_stride,dst_stride,rotation==LV_DISPLAY_ROTATION_270);
        assert(!memcmp(tiled+16+offset,dst,size));
        for(unsigned i=0;i<16+offset;i++)assert(tiled[i]==0xa5);
        for(int i=0;i<16;i++)assert(tiled[size+16+offset+i]==0xa5);
        free(tiled);
    }
    for (int y = 0; y < h; ++y) {
        for (int x = 0; x < w; ++x) {
            int dx, dy;
            if (rotation == LV_DISPLAY_ROTATION_90) { dx = y; dy = w - 1 - x; }
            else if (rotation == LV_DISPLAY_ROTATION_180) { dx = w - 1 - x; dy = h - 1 - y; }
            else { dx = h - 1 - y; dy = x; }
            assert(((uint16_t *)(dst + dy * dst_stride))[dx] == (uint16_t)(y * w + x + 1));
        }
    }
    for (unsigned i = 0; i < 16 + offset; ++i) assert(guarded[i] == 0xa5);
    for (int i = 0; i < 16; ++i) assert(guarded[size + 16 + offset + i] == 0xa5);
    free(input); free(guarded);checked++;
}

int main(void) {
    lv_init();
    const int shapes[][2] = {{3, 2}, {1, 1}, {1, 80}, {1280, 45}, {720, 80}, {1279, 7}, {65, 17},
                            {1280, 720}, {1280, 64}, {1280, 16}, {66, 18}, {2, 2},
                            {34,66},{32,64},{33,65},{1220,25},{1280,24},{1220,67},{1220,47},{1199,68},{1219,67}};
    for (unsigned i = 0; i < sizeof(shapes) / sizeof(shapes[0]); ++i)
        for (int rotation = LV_DISPLAY_ROTATION_90; rotation <= LV_DISPLAY_ROTATION_270; ++rotation)
            for(unsigned padding=0;padding<=4;padding+=2)
                for(unsigned offset=0;offset<=2;offset+=2)
                    check(shapes[i][0], shapes[i][1], (lv_display_rotation_t)rotation,padding,offset);
    printf("DISPLAY_ROTATION_PASS: %u pixel layouts, word/scalar paths, padded/unaligned buffers and output guards\n",checked);
    return 0;
}
