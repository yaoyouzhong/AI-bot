#include "lvgl.h"
#include "src/draw/sw/lv_draw_sw.h"
#include "draw_orthogonal.h"
#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>
#ifdef TAB5_TRANSFORM_REFERENCE
void tab5_lvgl_reference_transform(lv_draw_unit_t *,const lv_area_t *,const void *,int32_t,int32_t,int32_t,
                                  const lv_draw_image_dsc_t *,const lv_draw_image_sup_t *,lv_color_format_t,void *);
#define lv_draw_sw_transform tab5_lvgl_reference_transform
#endif
static unsigned cases;
static void check(int width,int height,int stride,const lv_draw_image_dsc_t *d,const lv_area_t *area,unsigned pattern,lv_color_format_t format) {
    size_t count=(size_t)lv_area_get_width(area)*lv_area_get_height(area)*(format==LV_COLOR_FORMAT_ARGB8888?4:3);
    uint8_t *src=malloc((size_t)stride*height),*expected=malloc(count),*actual=malloc(count);assert(src&&expected&&actual);
    for(size_t i=0;i<(size_t)stride*height;i++)src[i]=pattern?(uint8_t)(i*97+(i>>8)*29):0x95;
    memset(expected,0x5a,count);memcpy(actual,expected,count);
    lv_draw_sw_transform(NULL,area,src,width,height,stride,d,NULL,format,expected);
    assert(tab5_draw_orthogonal(area,src,width,height,stride,d,format,actual));
    if(memcmp(expected,actual,count)) {
        size_t at=0;while(at<count&&expected[at]==actual[at])at++;
        fprintf(stderr,"TRANSFORM_MISMATCH angle=%ld aa=%u size=%dx%d pivot=%ld,%ld area=%d,%d,%d,%d byte=%zu expected=%u actual=%u\n",
                (long)d->rotation,d->antialias,width,height,(long)d->pivot.x,(long)d->pivot.y,area->x1,area->y1,area->x2,area->y2,at,expected[at],actual[at]);exit(1);
    }
    free(src);free(expected);free(actual);cases++;
}
static void benchmark(int angle,lv_color_format_t format) {
    int width=730,height=1290,stride=width*(format==LV_COLOR_FORMAT_ARGB8888?4:2);
    uint8_t *src=malloc((size_t)stride*height),*out=malloc(1280*8*4);assert(src&&out);
    for(int i=0;i<stride*height;i++)src[i]=(uint8_t)(i/128);
    lv_draw_image_dsc_t d;lv_draw_image_dsc_init(&d);d.rotation=angle;
    LARGE_INTEGER frequency,start,end;QueryPerformanceFrequency(&frequency);
    for(int variant=0;variant<2;variant++) {
        QueryPerformanceCounter(&start);
        for(int pass=0;pass<3;pass++)for(int y=0;y<720;y+=8) {
            lv_area_t area=angle==900?(lv_area_t){-1279,y,0,y+7}:(lv_area_t){0,-719+y,1279,-712+y};
            if(variant)assert(tab5_draw_orthogonal(&area,src,width,height,stride,&d,format,out));
            else lv_draw_sw_transform(NULL,&area,src,width,height,stride,&d,NULL,format,out);
        }
        QueryPerformanceCounter(&end);
        printf("TRANSFORM_NATIVE_TIMING angle=%d format=%d variant=%s us=%lld desktop_only\n",angle,format,variant?"tiled":"reference",(end.QuadPart-start.QuadPart)*1000000/frequency.QuadPart);
    }
    free(src);free(out);
}
int main(void) {
    lv_init();
    const int widths[]={1,2,17,31,32,33,1279,1280};
    for(int angle=900;angle<=2700;angle+=900)for(int aa=0;aa<2;aa++)for(int pattern=0;pattern<2;pattern++)
        for(int pivot=0;pivot<4;pivot++)for(int i=0;i<8;i++)for(int rows=1;rows<=9;rows+=4) {
            lv_draw_image_dsc_t d;lv_draw_image_dsc_init(&d);d.rotation=angle;d.antialias=aa;d.pivot=(lv_point_t){pivot*73-80,pivot*127-140};
            lv_area_t area={-19+17*pivot,-21+13*pivot,widths[i]-20+17*pivot,rows-22+13*pivot};
            check(129,97,272,&d,&area,pattern,LV_COLOR_FORMAT_RGB565);
            check(129,97,544,&d,&area,pattern,LV_COLOR_FORMAT_ARGB8888);
        }
    for(int angle=900;angle<=2700;angle+=900)for(int offset=0;offset<4;offset++) {
        lv_draw_image_dsc_t d;lv_draw_image_dsc_init(&d);d.rotation=angle;d.pivot=(lv_point_t){offset*127,offset*89};
        lv_area_t area={-1279,-719,0,-712};check(720,1280,1440,&d,&area,1,LV_COLOR_FORMAT_RGB565);
        check(730,1290,2920,&d,&area,1,LV_COLOR_FORMAT_ARGB8888);
        area=(lv_area_t){0,0,1279,7};check(720,1280,1440,&d,&area,0,LV_COLOR_FORMAT_RGB565);
        check(730,1290,2920,&d,&area,0,LV_COLOR_FORMAT_ARGB8888);
    }
    uint8_t data[128];memset(data,0x96,sizeof(data));uint8_t original[128];memcpy(original,data,sizeof(data));
    lv_area_t area={0,0,3,3};lv_draw_image_dsc_t d;lv_draw_image_dsc_init(&d);d.rotation=899;
    assert(!tab5_draw_orthogonal(&area,data,4,4,8,&d,LV_COLOR_FORMAT_RGB565,data));
    d.rotation=900;d.scale_x=255;assert(!tab5_draw_orthogonal(&area,data,4,4,8,&d,LV_COLOR_FORMAT_RGB565,data));
    d.scale_x=256;assert(!tab5_draw_orthogonal(&area,data,4,4,16,&d,LV_COLOR_FORMAT_RGB888,data));
    assert(!memcmp(data,original,sizeof(data)));
    printf("DRAW_ORTHOGONAL_OK %u bit_exact_against_actual_LVGL; unchanged_unsupported_outputs\n",cases);
    benchmark(900,LV_COLOR_FORMAT_RGB565);benchmark(2700,LV_COLOR_FORMAT_RGB565);
    benchmark(900,LV_COLOR_FORMAT_ARGB8888);benchmark(2700,LV_COLOR_FORMAT_ARGB8888);return 0;
}
