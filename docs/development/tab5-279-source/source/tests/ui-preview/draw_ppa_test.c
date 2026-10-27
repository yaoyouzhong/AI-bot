#undef NDEBUG
#include "lvgl.h"
#include "src/draw/sw/blend/lv_draw_sw_blend_private.h"
#include "src/draw/lv_draw_private.h"
#include "draw_ppa_test_stubs.h"
#include <assert.h>
#include <stdlib.h>
#include <string.h>
#include <stdio.h>
#include <malloc.h>
static int fault,calls;
bool esp_ptr_external_ram(const void *p){return p!=NULL;}
void *heap_caps_aligned_alloc(size_t a,size_t n,unsigned caps){(void)caps;return fault==2?NULL:_aligned_malloc(n,a);}
void heap_caps_free(void *p){_aligned_free(p);}
esp_err_t ppa_register_client(const ppa_client_config_t *c,ppa_client_handle_t *out){assert(c->max_pending_trans_num==1);*out=(void *)1;return fault==1?-1:0;}
esp_err_t ppa_unregister_client(ppa_client_handle_t c){(void)c;return 0;}
esp_err_t ppa_do_fill(ppa_client_handle_t c,const ppa_fill_oper_config_t *op){
    (void)c;calls++;assert(op->mode==PPA_TRANS_MODE_BLOCKING);
    if(fault==3)return -1;
    uint16_t color=((op->fill_argb_color.r>>3)<<11)|((op->fill_argb_color.g>>2)<<5)|(op->fill_argb_color.b>>3);
    if(fault==4)color^=1;
    assert(op->out.pic_w*op->out.pic_h*2<=op->out.buffer_size);
    uint16_t *p=op->out.buffer;
    for(unsigned y=0;y<op->fill_block_h;y++)for(unsigned x=0;x<op->fill_block_w;x++)
        p[(op->out.block_offset_y+y)*op->out.pic_w+op->out.block_offset_x+x]=color;
    return 0;
}
bool tab5_ppa_fill(lv_draw_unit_t *,const lv_draw_sw_blend_dsc_t *);
extern volatile unsigned tab5_ppa_fills,tab5_ppa_errors,tab5_ppa_ready;
int main(int argc,char **argv){
    if(argc>1)fault=atoi(argv[1]);
    lv_init();
    uint16_t *pixels=_aligned_malloc(163840,128);assert(pixels);
    lv_draw_buf_t buffer;assert(lv_draw_buf_init(&buffer,1280,64,LV_COLOR_FORMAT_RGB565,2560,pixels,163840)==LV_RESULT_OK);
    lv_layer_t layer={.draw_buf=&buffer,.buf_area={30,100,1309,163},.color_format=LV_COLOR_FORMAT_RGB565};
    lv_area_t clip={37,109,780,151},area={33,102,890,158};
    lv_draw_unit_t unit={.target_layer=&layer,.clip_area=&clip};
    lv_draw_sw_blend_dsc_t d={.blend_area=&area,.opa=255,.color=lv_color_hex(0x89abcd)};
    for(unsigned i=0;i<81920;i++)pixels[i]=0x1957;
    bool filled=tab5_ppa_fill(&unit,&d);
    if(fault){assert(!filled&&!tab5_ppa_ready&&tab5_ppa_errors==1);for(unsigned i=0;i<81920;i++)assert(pixels[i]==0x1957);}
    else {
        assert(filled&&tab5_ppa_ready==1&&tab5_ppa_fills==1);
        for(unsigned y=0;y<64;y++)for(unsigned x=0;x<1280;x++)
            assert(pixels[y*1280+x]==(x>=7&&x<=750&&y>=9&&y<=51?lv_color_to_u16(d.color):0x1957));
        int before=calls;
        d.opa=200;assert(!tab5_ppa_fill(&unit,&d));d.opa=255;
        d.src_buf=pixels;assert(!tab5_ppa_fill(&unit,&d));d.src_buf=NULL;
        d.mask_buf=(void *)1;d.mask_res=LV_DRAW_SW_MASK_RES_CHANGED;assert(!tab5_ppa_fill(&unit,&d));d.mask_buf=NULL;
        buffer.data+=2;assert(!tab5_ppa_fill(&unit,&d));buffer.data-=2;
        buffer.data_size-=2;assert(!tab5_ppa_fill(&unit,&d));buffer.data_size+=2;
        area=(lv_area_t){0,0,10,10};assert(!tab5_ppa_fill(&unit,&d));
        assert(calls==before);
    }
    _aligned_free(pixels);printf("PPA_FILL_PLAN_PASS fault=%d bounds_pixels_and_software_fallback\n",fault);return 0;
}
