#include "draw_orthogonal.h"
#include <stdint.h>
volatile unsigned tab5_orthogonal_calls,tab5_orthogonal_pixels,tab5_orthogonal_us,tab5_orthogonal_peak_us;

// Keep LVGL's fixed-point sampling and output formats, including edge alpha.
// A quarter-turn walks distant source rows. Visit a short column of destination
// pixels together so those reads reuse the same source cache lines in PSRAM.
// No extra image, allocation, approximate angle, or change of alpha semantics.
typedef struct {int32_t x,y,dx,dy;} sample_row;
static lv_color32_t mix_neighbour(lv_color32_t pixel,lv_color32_t neighbour,unsigned weight) {
    // At exact right-angle sample centres, the reference's /256 alpha
    // arithmetic reduces a differing opaque sample by one. Preserve that
    // result directly without entering its general color-mix routine.
    if(!weight) {
        if(pixel.alpha&&(!neighbour.alpha||!lv_color32_eq(pixel,neighbour)))pixel.alpha--;
        return pixel;
    }
    if(!neighbour.alpha)pixel.alpha=(pixel.alpha*(255-weight))>>8;
    else if(!lv_color32_eq(pixel,neighbour)) {
        if(pixel.alpha)pixel.alpha=(neighbour.alpha*weight+pixel.alpha*(255-weight))>>8;
        neighbour.alpha=(uint8_t)weight;pixel=lv_color_mix32(neighbour,pixel);
    }
    return pixel;
}
static void inverse(const lv_draw_image_dsc_t *d,int32_t s,int32_t c,int32_t x,int32_t y,int32_t *sx,int32_t *sy) {
    x-=d->pivot.x;y-=d->pivot.y;
    *sx=((c*x-s*y)>>2)+d->pivot.x*256;
    *sy=((s*x+c*y)>>2)+d->pivot.y*256;
}
bool tab5_draw_orthogonal(const lv_area_t *area,const void *source,int32_t width,int32_t height,
                          int32_t stride,const lv_draw_image_dsc_t *d,lv_color_format_t format,void *destination) {
    bool argb=format==LV_COLOR_FORMAT_ARGB8888;
    unsigned pixel_bytes=argb?4:2;
    if((!argb&&format!=LV_COLOR_FORMAT_RGB565)||!area||!source||!destination||!d||width<1||height<1||
       width>2048||height>2048||stride<(int32_t)(width*pixel_bytes)||(stride&(pixel_bytes-1))||
       d->scale_x!=LV_SCALE_NONE||d->scale_y!=LV_SCALE_NONE||d->skew_x||d->skew_y||
       (d->rotation!=900&&d->rotation!=1800&&d->rotation!=2700)||
       area->x1<-4096||area->y1<-4096||area->x2>4096||area->y2>4096||
       d->pivot.x<-4096||d->pivot.x>4096||d->pivot.y<-4096||d->pivot.y>4096)return false;
    int32_t w=lv_area_get_width(area),h=lv_area_get_height(area);
    if(w<1||h<1||w>4096||h>4096)return false;
    int32_t s=lv_trigo_sin(-d->rotation/10)>>(LV_TRIGO_SHIFT-10);
    int32_t c=lv_trigo_sin(90-d->rotation/10)>>(LV_TRIGO_SHIFT-10);
    uint16_t *colors=destination;
    lv_color32_t *colors32=destination;
    uint8_t *alpha=(uint8_t *)destination+(size_t)w*h*2;
    const uint8_t *pixels=source;
    for(int32_t top=0;top<h;top+=8) {
        int32_t rows=h-top<8?h-top:8;
        sample_row sample[8];
        for(int32_t row=0;row<rows;row++) {
            int32_t far_x,far_y;
            inverse(d,s,c,area->x1,area->y1+top+row,&sample[row].x,&sample[row].y);
            inverse(d,s,c,area->x2,area->y1+top+row,&far_x,&far_y);
            sample[row].dx=w>1?256*(far_x-sample[row].x)/(w-1):0;
            sample[row].dy=w>1?256*(far_y-sample[row].y)/(w-1):0;
            sample[row].x+=128;sample[row].y+=128;
        }
        for(int32_t left=0;left<w;left+=32) {
            int32_t end=w-left<32?w:left+32;
            for(int32_t row=0;row<rows;row++)for(int32_t x=left;x<end;x++) {
                int32_t sx=sample[row].x+((sample[row].dx*x)>>8);
                int32_t sy=sample[row].y+((sample[row].dy*x)>>8);
                int32_t ix=sx>>8,iy=sy>>8;
                size_t at=(size_t)(top+row)*w+x;
                if(ix<0||iy<0||ix>=width||iy>=height) {
                    if(argb)colors32[at]=(lv_color32_t){0};else alpha[at]=0;
                    continue;
                }
                unsigned fx=sx&255,fy=sy&255;
                int nx=fx<128?-1:1,ny=fy<128?-1:1;
                fx=fx<128?127-fx:fx-128;
                fy=fy<128?127-fy:fy-128;
                bool interior=d->antialias&&ix+nx>=0&&ix+nx<width&&iy+ny>=0&&iy+ny<height;
                if(argb) {
                    const lv_color32_t *pixel=(const lv_color32_t *)(pixels+(size_t)iy*stride+ix*4);
                    lv_color32_t color=*pixel;
                    if(interior) {
                        color=mix_neighbour(color,*(const lv_color32_t *)((const uint8_t *)pixel+ny*stride),fy);
                        color=mix_neighbour(color,pixel[nx],fx);
                    }else if((ix==0&&nx<0)||(ix==width-1&&nx>0))color.alpha=(color.alpha*(127-fx))>>7;
                    else if((iy==0&&ny<0)||(iy==height-1&&ny>0))color.alpha=(color.alpha*(127-fy))>>7;
                    colors32[at]=color;continue;
                }
                fx*=2;fy*=2;
                const uint16_t *pixel=(const uint16_t *)(pixels+(size_t)iy*stride+ix*2);
                uint16_t color=*pixel;
                if(interior) {
                    // Exact integer sampling needs no neighbouring color reads.
                    if(fx||fy) {
                        uint16_t horizontal=pixel[nx];
                        uint16_t vertical=*(const uint16_t *)((const uint8_t *)pixel+ny*stride);
                        if(color!=horizontal||color!=vertical) {
                            uint16_t a=lv_color_16_16_mix(vertical,color,(lv_opa_t)fy);
                            uint16_t b=lv_color_16_16_mix(horizontal,color,(lv_opa_t)fx);
                            color=lv_color_16_16_mix(b,a,LV_OPA_50);
                        }
                    }
                    alpha[at]=255;
                }else {
                    alpha[at]=(ix==0&&nx<0)||(ix==width-1&&nx>0)?(255*(255-fx))>>8:
                        (iy==0&&ny<0)||(iy==height-1&&ny>0)?(255*(255-fy))>>8:255;
                }
                colors[at]=color;
            }
        }
    }
    return true;
}
#if defined(ESP_PLATFORM) || defined(TAB5_TRANSFORM_PREVIEW)
#include "src/draw/sw/lv_draw_sw.h"
#ifdef ESP_PLATFORM
#include "esp_timer.h"
#endif
#ifdef TAB5_TRANSFORM_PREVIEW
#include <stdlib.h>
#include <stdio.h>
#define __real_lv_draw_sw_transform tab5_lvgl_reference_transform
#define __wrap_lv_draw_sw_transform lv_draw_sw_transform
#endif
void __real_lv_draw_sw_transform(lv_draw_unit_t *,const lv_area_t *,const void *,int32_t,int32_t,int32_t,
                               const lv_draw_image_dsc_t *,const lv_draw_image_sup_t *,lv_color_format_t,void *);
void __wrap_lv_draw_sw_transform(lv_draw_unit_t *unit,const lv_area_t *area,const void *source,
                                int32_t width,int32_t height,int32_t stride,const lv_draw_image_dsc_t *dsc,
                                const lv_draw_image_sup_t *sup,lv_color_format_t format,void *destination) {
#ifdef TAB5_TRANSFORM_PREVIEW
    static int bypass=-1,audit=-1;static unsigned traces;
    if(bypass<0)bypass=getenv("TAB5_ORTHOGONAL_REFERENCE")!=NULL;
    if(audit<0)audit=getenv("TAB5_TRANSFORM_AUDIT")!=NULL;
    if(audit&&width>=700&&width<=800&&height>=200&&traces++<48)
        fprintf(stderr,"DRAW_LAYER_SAMPLE size=%ld,%ld angle=%ld format=%d\n",(long)width,(long)height,(long)dsc->rotation,format);
    if(bypass){__real_lv_draw_sw_transform(unit,area,source,width,height,stride,dsc,sup,format,destination);return;}
#endif
    unsigned elapsed=0;
#ifdef ESP_PLATFORM
    int64_t started=esp_timer_get_time();
#endif
    if(!tab5_draw_orthogonal(area,source,width,height,stride,dsc,format,destination)) {
        __real_lv_draw_sw_transform(unit,area,source,width,height,stride,dsc,sup,format,destination);
        return;
    }
#ifdef ESP_PLATFORM
    elapsed=(unsigned)(esp_timer_get_time()-started);
#endif
    tab5_orthogonal_calls++;tab5_orthogonal_pixels+=(unsigned)lv_area_get_size(area);
    tab5_orthogonal_us+=elapsed;if(elapsed>tab5_orthogonal_peak_us)tab5_orthogonal_peak_us=elapsed;
}
#endif
