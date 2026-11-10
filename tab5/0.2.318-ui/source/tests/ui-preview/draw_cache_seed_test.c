#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "lvgl.h"
#ifndef CACHE_SOURCE
#define CACHE_SOURCE "../../firmware/main/draw_cache.c"
#define CACHE_ROTATE "../../firmware/components/esp_lvgl_port/src/lvgl9/tab5_rotate_rgb565.h"
#endif
#undef assert
#define assert(x) do { if(!(x)){fprintf(stderr,"CHECK_FAILED %s at %s:%d\n",#x,__FILE__,__LINE__);exit(2);} } while(0)
// Count the real cache's seed copies, not LVGL's unrelated raster operations.
#include CACHE_ROTATE
static size_t copied, largest, total;
static void *counted_copy(void *d,const void *s,size_t n) {
    copied+=n;total+=n;if(n>largest)largest=n;return memcpy(d,s,n);
}
#define memcpy counted_copy
#include CACHE_SOURCE
#undef memcpy

static unsigned worst_tick;
static unsigned step(lv_obj_t *page,unsigned pixels,bool bounded) {
    copied=0;tab5_draw_cache_step_budget(page,pixels);
    if(copied>worst_tick)worst_tick=(unsigned)copied;
    if(bounded)assert(copied<=(size_t)pixels*2);
    return (unsigned)copied;
}
static void warm(lv_obj_t *page,bool bounded) {
    for(unsigned i=0;i<100&&!tab5_draw_cache_is_ready(page);i++)step(page,1280*64,bounded);
    assert(tab5_draw_cache_is_ready(page));
}
static lv_obj_t *make_page(lv_obj_t *parent,int x) {
    lv_obj_t *p=lv_obj_create(parent);lv_obj_remove_style_all(p);
    lv_obj_set_size(p,1220,516);lv_obj_set_pos(p,x,0);
    lv_obj_set_style_bg_color(p,lv_color_hex(0x102030),0);
    lv_obj_set_style_bg_opa(p,LV_OPA_COVER,0);
    lv_obj_remove_flag(p,LV_OBJ_FLAG_SCROLLABLE);return p;
}
static void check_image(lv_obj_t *page) {
    cache_entry *e=find(page);assert(e&&e->valid&&e->native_valid);
    size_t n=e->pixels->data_size;uint8_t *normal=malloc(n),*native=malloc(n);
    assert(normal&&native);memcpy(normal,e->pixels->data,n);memcpy(native,e->native,n);
    // Force an independent full raster of the same widget tree and compare
    // both orientation buffers byte-for-byte, including untouched regions.
    e->has_image=false;e->native_valid=false;e->valid=false;
    e->has_dirty=false;e->revision++;warm(page,true);
    assert(!memcmp(normal,e->pixels->data,n));
    assert(!memcmp(native,e->native,n));free(normal);free(native);
}
static void flush(lv_display_t *d,const lv_area_t *a,uint8_t *p){(void)a;(void)p;lv_display_flush_ready(d);}
int main(int argc,char **argv) {
    bool baseline=argc>1&&!strcmp(argv[1],"baseline");
    printf("CACHE_TEST_MODE baseline=%d argc=%d\n",baseline,argc);fflush(stdout);
    lv_init();lv_display_t *d=lv_display_create(1280,720);
    static uint16_t display_buffer[1280*32];
    lv_display_set_buffers(d,display_buffer,NULL,sizeof(display_buffer),LV_DISPLAY_RENDER_MODE_PARTIAL);
    lv_display_set_flush_cb(d,flush);
    lv_obj_t *strip=lv_obj_create(lv_screen_active());lv_obj_remove_style_all(strip);
    lv_obj_set_size(strip,2440,516);lv_obj_remove_flag(strip,LV_OBJ_FLAG_SCROLLABLE);
    lv_obj_t *a=make_page(strip,0),*b=make_page(strip,1220);
    lv_obj_t *tile=lv_obj_create(a);lv_obj_remove_style_all(tile);
    lv_obj_set_pos(tile,20,20);lv_obj_set_size(tile,32,24);
    lv_obj_set_style_bg_opa(tile,LV_OPA_COVER,0);
    lv_obj_set_style_bg_color(tile,lv_color_hex(0x112233),0);
    lv_obj_update_layout(lv_screen_active());
    tab5_draw_cache_init();tab5_draw_cache_native_strip=strip;
    warm(a,!baseline);warm(b,!baseline);warm(a,!baseline);
    total=largest=worst_tick=0;
    for(unsigned i=0;i<30;i++) {
        lv_obj_set_style_bg_color(tile,lv_color_hex(0x334400+i*257),0);
        // Model updates at different positions while an old complete
        // snapshot remains available to a swipe.
        lv_obj_set_pos(tile,20+(int)(i%4)*40,20+(int)(i%3)*32);
        lv_obj_update_layout(a);warm(a,!baseline);
    }
    printf("CACHE_REPAIR_MEASURE baseline=%d copies=%zu largest=%zu worstTick=%u\n",baseline,total,largest,worst_tick);
    fflush(stdout);
    if(baseline)return 0;
    assert(worst_tick<=1280*64*2);
    assert(total<8u*1024u*1024u);
    check_image(a);
    // Switching staging owners, incoming updates during seeding, and
    // reclamation must retain the old published image until a full commit.
    lv_obj_set_style_bg_color(b,lv_color_hex(0x789ABC),0);warm(b,true);
    lv_obj_set_style_bg_color(tile,lv_color_hex(0xAB3421),0);
    cache_entry *e=find(a);uint8_t *published=e->pixels->data;
    uint8_t *saved=malloc(e->pixels->data_size);assert(saved);
    memcpy(saved,published,e->pixels->data_size);
    step(a,512,true);assert(!memcmp(saved,published,e->pixels->data_size));
    lv_obj_set_pos(tile,710,350);lv_obj_update_layout(a);
    step(a,512,true);assert(!memcmp(saved,published,e->pixels->data_size));
    tab5_draw_cache_reclaim_staging();
    assert(!memcmp(saved,published,e->pixels->data_size));free(saved);
    warm(a,true);check_image(a);
    lv_obj_set_size(a,1000,500);lv_obj_update_layout(a);warm(a,true);check_image(a);
    tab5_draw_cache_drop();lv_obj_delete(strip);
    puts("CACHE_SEED_OK bounded copies, complete publication, both rotations, owner switch, mid-copy update, reclaim, resize");
    return 0;
}
