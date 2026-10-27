#ifdef NDEBUG
#undef NDEBUG
#endif
#include <assert.h>
#include <stdio.h>
#include <string.h>
#include "lvgl.h"
#include "src/draw/sw/blend/lv_draw_sw_blend_to_rgb565.h"
#define __real_lv_font_get_bitmap_fmt_txt lv_font_get_bitmap_fmt_txt
#define __real_lv_draw_sw_blend_color_to_rgb565 lv_draw_sw_blend_color_to_rgb565
#define __real_lv_draw_sw_blend_image_to_rgb565 lv_draw_sw_blend_image_to_rgb565
#include "../../firmware/main/draw_fast.c"

static uint32_t seed=12345;
extern const lv_font_t tab5_font_24,tab5_font_30;
static uint32_t random_value(void){seed=seed*1664525u+1013904223u;return seed;}
int main(void) {
    lv_init();unsigned cases=0;
    copy_accelerated[0]=copy_accelerated[1]=true;
    unsigned char source[5200],copied[5200],reference[5200];
    for(int i=0;i<5200;i++)source[i]=(unsigned char)random_value();
    for(int a=0;a<4;a++)for(int b=0;b<4;b++)for(int width=0;width<=1281;width++) {
        memset(copied,0x95,sizeof(copied));memcpy(reference,copied,sizeof(copied));
        memcpy(reference+b,source+a,width*2);tab5_rgb565_copy(copied+b,source+a,width);
        assert(!memcmp(copied,reference,sizeof(copied)));cases++;
    }
    puts("RGB565_COPY_OK 20512 aligned, halfword/byte-offset, odd-width and guard-byte cases");
    uint16_t expected[4096],actual[4096];uint8_t masks[4096];
    uint16_t image[4096];for(int i=0;i<4096;i++)image[i]=(uint16_t)random_value();
    for(unsigned test=0;test<300;test++) {
        int width=1+(int)(test%129),height=1+(int)(test%17);
        memset(expected,0x37,sizeof(expected));memcpy(actual,expected,sizeof(actual));memset(masks,173,sizeof(masks));
        lv_draw_sw_blend_image_dsc_t d={.dest_buf=expected+test%2,.dest_w=width,.dest_h=height,.dest_stride=(width+5)*2,
            .src_buf=image+(test/2)%2,.src_stride=(width+7)*2,.src_color_format=LV_COLOR_FORMAT_RGB565,
            .blend_mode=LV_BLEND_MODE_NORMAL,.opa=test%3?255:113,.mask_buf=test%5?NULL:masks,.mask_stride=width};
        lv_draw_sw_blend_image_to_rgb565(&d);d.dest_buf=actual+test%2;__wrap_lv_draw_sw_blend_image_to_rgb565(&d);
        assert(!memcmp(expected,actual,sizeof(actual)));cases++;
    }
    for(unsigned test=0;test<256;test++) {
        int width=16+test%96,height=1+test%24;
        memset(expected,0x95,sizeof(expected));memcpy(actual,expected,sizeof(actual));
        copy_accelerated[0]=copy_accelerated[1]=test%2;
        lv_draw_sw_blend_image_dsc_t d={.dest_buf=expected+test%2,.dest_w=width,.dest_h=height,.dest_stride=width*2,
            .src_buf=image+(test/2)%2,.src_stride=width*2,.src_color_format=LV_COLOR_FORMAT_RGB565,
            .blend_mode=LV_BLEND_MODE_NORMAL,.opa=255};
        lv_draw_sw_blend_image_to_rgb565(&d);d.dest_buf=actual+test%2;__wrap_lv_draw_sw_blend_image_to_rgb565(&d);
        assert(!memcmp(expected,actual,sizeof(actual)));cases++;
    }
    for(unsigned test=0;test<1800;test++) {
        int width=1+(int)(test%129),height=1+(int)(test%17),stride=(width+5)*2,offset=test%2;
        for(int i=0;i<4096;i++){expected[i]=(test%3)?0x4269:(uint16_t)random_value();masks[i]=(uint8_t)(i+test);}
        memcpy(actual,expected,sizeof(actual));
        lv_draw_sw_blend_fill_dsc_t d={.dest_buf=expected+offset,.dest_w=width,.dest_h=height,.dest_stride=stride,
            .mask_buf=(test%2)?masks+(test%3):NULL,.mask_stride=width+3,.color=lv_color_hex(random_value()&0xffffff),
            .opa=(test%4)?255:(test%256)};
        lv_draw_sw_blend_color_to_rgb565(&d);d.dest_buf=actual+offset;
        __wrap_lv_draw_sw_blend_color_to_rgb565(&d);
        assert(!memcmp(expected,actual,sizeof(actual)));cases++;
    }
    const lv_font_t *fonts[]={&lv_font_montserrat_14,&tab5_font_24,&tab5_font_30};
    for(int pass=0;pass<3;pass++)for(unsigned fi=0;fi<3;fi++)for(unsigned sample=0;sample<190;sample++) {
        unsigned ch=sample<94?33+sample:0x4e00+(sample-94)*13;
        lv_font_glyph_dsc_t g={0};if(!lv_font_get_glyph_dsc(fonts[fi],&g,ch,0))continue;
        if(!g.box_w||!g.box_h)continue;
        lv_draw_buf_t *a=lv_draw_buf_create(g.box_w,g.box_h,LV_COLOR_FORMAT_A8,LV_STRIDE_AUTO);
        lv_draw_buf_t *b=lv_draw_buf_create(g.box_w,g.box_h,LV_COLOR_FORMAT_A8,LV_STRIDE_AUTO);
        assert(a&&b);memset(a->data,0,a->data_size);memset(b->data,0,b->data_size);
        assert(lv_font_get_bitmap_fmt_txt(&g,a)==a);
        assert(__wrap_lv_font_get_bitmap_fmt_txt(&g,b)==b);
        assert(!memcmp(a->data,b->data,(size_t)a->header.stride*a->header.h));
        lv_draw_buf_destroy(a);lv_draw_buf_destroy(b);cases++;
    }
    assert(tab5_glyph_hits>0&&tab5_fast_masks>0&&tab5_fast_fills>0);
    printf("DRAW_FAST_OK %u pixel-exact cases; glyph_hits=%u glyph_misses=%u fills=%u masks=%u\n",cases,
        tab5_glyph_hits,tab5_glyph_misses,tab5_fast_fills,tab5_fast_masks);
}
