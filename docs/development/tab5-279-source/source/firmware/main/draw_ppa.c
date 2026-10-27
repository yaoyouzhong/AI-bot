#include "lvgl.h"
#include "src/draw/sw/blend/lv_draw_sw_blend.h"
#include "src/draw/sw/blend/lv_draw_sw_blend_private.h"
#include "src/draw/lv_draw_private.h"
#include "src/misc/lv_area_private.h"
#ifdef TAB5_DRAW_PPA_TEST
#include "draw_ppa_test_stubs.h"
#else
#include "driver/ppa.h"
#include "esp_heap_caps.h"
#include "esp_memory_utils.h"
#endif
#include <stdint.h>
#include <stdio.h>

// Only large opaque RGB565 fills in the current draw buffer use PPA. Rounded
// edges, glyph masks, images and translucent colors keep the software path.
static ppa_client_handle_t fill_client;
static bool fill_attempted;
volatile unsigned tab5_ppa_fills,tab5_ppa_errors,tab5_ppa_ready;

static ppa_fill_oper_config_t fill_config(void *buffer,unsigned bytes,unsigned stride,unsigned height,
                                        unsigned x,unsigned y,unsigned w,unsigned h,lv_color_t color) {
    return (ppa_fill_oper_config_t){
        .out={.buffer=buffer,.buffer_size=bytes,.pic_w=stride/2,.pic_h=height,
              .block_offset_x=x,.block_offset_y=y,.fill_cm=PPA_FILL_COLOR_MODE_RGB565},
        .fill_block_w=w,.fill_block_h=h,
        .fill_argb_color={.a=255,.r=color.red,.g=color.green,.b=color.blue},
        .mode=PPA_TRANS_MODE_BLOCKING
    };
}
static bool fill_init(void) {
    if(fill_attempted)return tab5_ppa_ready==1;
    fill_attempted=true;
    ppa_client_config_t cfg={.oper_type=PPA_OPERATION_FILL,.max_pending_trans_num=1,
                            .data_burst_length=PPA_DATA_BURST_LENGTH_32};
    esp_err_t result=ppa_register_client(&cfg,&fill_client);
    if(result!=ESP_OK){tab5_ppa_errors++;return false;}
    // Verify real byte order, cache coherence and preservation outside a dirty
    // subrectangle before allowing the accelerator to touch a display buffer.
    uint16_t *probe=heap_caps_aligned_alloc(128,2048,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    bool valid=probe!=NULL;
    const uint32_t colors[]={0x123456,0xabcdef,0xffffff,0x000000};
    for(unsigned c=0;valid&&c<4;c++) {
        for(unsigned i=0;i<1024;i++)probe[i]=0x5a96;
        lv_color_t color=lv_color_hex(colors[c]);
        ppa_fill_oper_config_t op=fill_config(probe,2048,256,8,7,1,48,6,color);
        result=ppa_do_fill(fill_client,&op);
        valid=result==ESP_OK;
        for(unsigned y=0;valid&&y<8;y++)for(unsigned x=0;x<128;x++) {
            uint16_t expected=x>=7&&x<55&&y>=1&&y<7?lv_color_to_u16(color):0x5a96;
            if(probe[y*128+x]!=expected){valid=false;break;}
        }
    }
    heap_caps_free(probe);
    if(!valid){tab5_ppa_errors++;ppa_unregister_client(fill_client);fill_client=NULL;return false;}
    tab5_ppa_ready=1;
    return true;
}
bool tab5_ppa_fill(lv_draw_unit_t *unit,const lv_draw_sw_blend_dsc_t *dsc) {
    if(dsc->src_buf||dsc->opa<LV_OPA_MAX||
       (dsc->mask_buf&&dsc->mask_res!=LV_DRAW_SW_MASK_RES_FULL_COVER))return false;
    lv_layer_t *layer=unit->target_layer;
    lv_draw_buf_t *buffer=layer->draw_buf;
    if(layer->color_format!=LV_COLOR_FORMAT_RGB565||!buffer||!buffer->data||
       !esp_ptr_external_ram(buffer->data)||((uintptr_t)buffer->data&127)||
       (buffer->data_size&127)||(buffer->header.stride&1)||
       (uint64_t)buffer->header.stride*buffer->header.h>buffer->data_size)return false;
    lv_area_t area;
    if(!lv_area_intersect(&area,dsc->blend_area,unit->clip_area))return false;
    lv_area_move(&area,-layer->buf_area.x1,-layer->buf_area.y1);
    unsigned w=lv_area_get_width(&area),h=lv_area_get_height(&area);
    if(area.x1<0||area.y1<0||area.x2>=(int)buffer->header.w||area.y2>=(int)buffer->header.h||
       buffer->header.stride<(unsigned)buffer->header.w*2||
       !w||!h||w*h<4096||w>=4096||h>=4096)return false;
    if(!fill_init())return false;
    ppa_fill_oper_config_t op=fill_config(buffer->data,buffer->data_size,buffer->header.stride,
        buffer->header.h,area.x1,area.y1,w,h,dsc->color);
    // IDF 5.4.2 ppa_do_fill writes back and invalidates whole affected rows;
    // BLOCKING returns ownership before the following CPU draw or LCD flush.
    if(ppa_do_fill(fill_client,&op)!=ESP_OK){tab5_ppa_errors++;return false;}
    tab5_ppa_fills++;return true;
}
