#undef NDEBUG
#include "lvgl.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>
extern const lv_font_t tab5_font_24,tab5_font_30,baseline_font_24,baseline_font_30;
int main(void) {
    lv_init();unsigned glyphs=0;
    const lv_font_t *before[]={&baseline_font_24,&baseline_font_30};
    const lv_font_t *after[]={&tab5_font_24,&tab5_font_30};
    for(int size=0;size<2;size++) {
        assert(before[size]->line_height==after[size]->line_height&&before[size]->base_line==after[size]->base_line);
        for(unsigned cp=1;cp<=0x10ffff;cp++) {
            lv_font_glyph_dsc_t a={0},b={0};
            bool found=lv_font_get_glyph_dsc(before[size],&a,cp,0);
            assert(found==lv_font_get_glyph_dsc(after[size],&b,cp,0));
            if(!found)continue;
            assert(a.adv_w==b.adv_w&&a.box_w==b.box_w&&a.box_h==b.box_h&&a.ofs_x==b.ofs_x&&a.ofs_y==b.ofs_y);
            if(!a.box_w||!a.box_h)continue;
            lv_draw_buf_t *x=lv_draw_buf_create(a.box_w,a.box_h,LV_COLOR_FORMAT_A8,LV_STRIDE_AUTO);
            lv_draw_buf_t *y=lv_draw_buf_create(b.box_w,b.box_h,LV_COLOR_FORMAT_A8,LV_STRIDE_AUTO);
            assert(x&&y);lv_draw_buf_clear(x,NULL);lv_draw_buf_clear(y,NULL);
            assert(lv_font_get_bitmap_fmt_txt(&a,x)==x&&lv_font_get_bitmap_fmt_txt(&b,y)==y);
            assert(x->data_size==y->data_size&&!memcmp(x->data,y->data,x->data_size));
            lv_draw_buf_destroy(x);lv_draw_buf_destroy(y);glyphs++;
        }
    }
    printf("FONT_LOSSLESS_OK %u glyphs, all Unicode codepoints and exact metrics/pixels\n",glyphs);
}
