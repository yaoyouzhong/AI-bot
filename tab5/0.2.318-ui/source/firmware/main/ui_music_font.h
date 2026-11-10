#pragma once
#include "lvgl.h"
LV_FONT_DECLARE(tab5_font_24);
#if defined(ESP_PLATFORM) && defined(__GNUC__)
#define MUSIC_FONT_SMALL __attribute__((optimize("Os")))
#else
#define MUSIC_FONT_SMALL
#endif

// The ordinary 24 px font has broader coverage. Reuse only its otherwise
// missing glyphs at the title's 30 px size, without another large bitmap table.
static int music_font_scale(int n) { return (n*5+(n<0?-2:2))/4; }
static MUSIC_FONT_SMALL bool music_extended_dsc(const lv_font_t *font,lv_font_glyph_dsc_t *out,uint32_t letter,uint32_t next) {
    (void)font;
    if(!tab5_font_24.get_glyph_dsc(&tab5_font_24,out,letter,next))return false;
    out->adv_w=music_font_scale(out->adv_w);
    out->box_w=music_font_scale(out->box_w);out->box_h=music_font_scale(out->box_h);
    out->ofs_x=music_font_scale(out->ofs_x);out->ofs_y=music_font_scale(out->ofs_y);
    out->format=LV_FONT_GLYPH_FORMAT_A8;out->gid.index=letter;
    return true;
}
static MUSIC_FONT_SMALL const void *music_extended_bitmap(lv_font_glyph_dsc_t *glyph,lv_draw_buf_t *target) {
    lv_font_glyph_dsc_t original={0};
    if(!lv_font_get_glyph_dsc(&tab5_font_24,&original,glyph->gid.index,0)||original.is_placeholder||
       !original.box_w||!original.box_h||!target)return NULL;
    if(target->header.stride<glyph->box_w||target->data_size<(unsigned)glyph->box_h*target->header.stride)return NULL;
    // The bundled source's largest padded glyph is 700 bytes. Keep this bound
    // checked at runtime too, so future font extensions cannot overflow it.
    uint8_t pixels[1024] LV_ATTRIBUTE_MEM_ALIGN;
    unsigned stride=lv_draw_buf_width_to_stride(original.box_w,LV_COLOR_FORMAT_A8);
    lv_draw_buf_t source;
    if(stride*original.box_h>sizeof(pixels)||
       lv_draw_buf_init(&source,original.box_w,original.box_h,LV_COLOR_FORMAT_A8,stride,pixels,sizeof(pixels))!=LV_RESULT_OK||
       !lv_font_get_glyph_bitmap(&original,&source))return NULL;
    for(unsigned y=0;y<glyph->box_h;y++) {
        unsigned sy=glyph->box_h>1?y*(original.box_h-1)*256/(glyph->box_h-1):0;
        unsigned y0=sy>>8,y1=LV_MIN(y0+1,original.box_h-1),fy=sy&255;
        uint8_t *row=target->data+y*target->header.stride;
        for(unsigned x=0;x<glyph->box_w;x++) {
            unsigned sx=glyph->box_w>1?x*(original.box_w-1)*256/(glyph->box_w-1):0;
            unsigned x0=sx>>8,x1=LV_MIN(x0+1,original.box_w-1),fx=sx&255;
            unsigned top=pixels[y0*stride+x0]*(256-fx)+pixels[y0*stride+x1]*fx;
            unsigned bottom=pixels[y1*stride+x0]*(256-fx)+pixels[y1*stride+x1]*fx;
            row[x]=(top*(256-fy)+bottom*fy+32768)>>16;
        }
    }
    return target;
}
static const lv_font_t music_extended_30={
    .get_glyph_dsc=music_extended_dsc,.get_glyph_bitmap=music_extended_bitmap,
    .line_height=39,.base_line=9
};
