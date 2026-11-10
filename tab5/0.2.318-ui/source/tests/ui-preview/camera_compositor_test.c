// Real LVGL rendering is the oracle; no duplicate alpha-blending implementation.
#undef NDEBUG
#include "lvgl.h"
#include "../../firmware/main/camera_compositor.h"
#include <assert.h>
#include <limits.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define SW CAMERA_COMPOSITOR_SOURCE_WIDTH
#define SH CAMERA_COMPOSITOR_SOURCE_HEIGHT
#define DW CAMERA_COMPOSITOR_TARGET_WIDTH
#define DH CAMERA_COMPOSITOR_TARGET_HEIGHT
#define PIXELS ((size_t)SW*SH)
#define BYTES (PIXELS*2u)
#define GUARD 64u

typedef struct { uint8_t *allocation; uint16_t *pixels; } guarded_frame;
typedef struct {
    lv_obj_t *object;
    lv_draw_buf_t *snapshot;
    uint32_t *native;
    camera_overlay overlay;
    int x, y;
} sprite;
typedef struct { unsigned calls, cancel_at; } yield_state;

static uint16_t *source, *display_pixels, *reference;
static lv_display_t *display;
static unsigned checks;

static guarded_frame make_frame(void) {
    guarded_frame f={0};
    f.allocation=malloc(BYTES+2*GUARD);assert(f.allocation);
    f.pixels=(uint16_t *)(f.allocation+GUARD);
    memset(f.allocation,0xa5,BYTES+2*GUARD);
    return f;
}
static void guards(const guarded_frame *f) {
    for(unsigned i=0;i<GUARD;i++) {
        assert(f->allocation[i]==0xa5);
        assert(f->allocation[GUARD+BYTES+i]==0xa5);
    }
}
static bool yield_check(void *ctx) {
    yield_state *state=ctx;
    state->calls++;
    return !state->cancel_at || state->calls!=state->cancel_at;
}
static void flush(lv_display_t *d,const lv_area_t *area,uint8_t *pixels) {
    (void)area;(void)pixels;
    lv_display_flush_ready(d);
}
static void render_reference(void) {
    lv_obj_invalidate(lv_screen_active());
    lv_refr_now(display);
    lv_draw_sw_rotate(display_pixels,reference,SW,SH,SW*2,DW*2,
                      LV_DISPLAY_ROTATION_90,LV_COLOR_FORMAT_RGB565);
}
static lv_obj_t *plain(lv_obj_t *parent,int x,int y,int w,int h) {
    lv_obj_t *o=lv_obj_create(parent);
    lv_obj_remove_style_all(o);
    lv_obj_set_pos(o,x,y);lv_obj_set_size(o,w,h);
    lv_obj_remove_flag(o,LV_OBJ_FLAG_SCROLLABLE);
    return o;
}
static lv_obj_t *text(lv_obj_t *parent,const char *value,int x,int y,uint32_t color) {
    lv_obj_t *o=lv_label_create(parent);
    lv_obj_remove_style_all(o);
    lv_label_set_text(o,value);
    lv_obj_set_pos(o,x,y);
    lv_obj_set_style_text_font(o,&lv_font_montserrat_24,0);
    lv_obj_set_style_text_color(o,lv_color_hex(color),0);
    return o;
}
static sprite make_sprite(lv_obj_t *parent,int x,int y,bool second) {
    sprite s={0};s.x=x;s.y=y;
    s.object=plain(parent,x,y,300,144);
    if(!second) {
        lv_obj_t *ring=plain(s.object,10,10,94,94);
        lv_obj_set_style_radius(ring,LV_RADIUS_CIRCLE,0);
        lv_obj_set_style_border_width(ring,4,0);
        lv_obj_set_style_border_color(ring,lv_color_hex(0xf5f9ff),0);
        lv_obj_t *disc=plain(s.object,20,20,74,74);
        lv_obj_set_style_radius(disc,LV_RADIUS_CIRCLE,0);
        lv_obj_set_style_bg_color(disc,lv_color_hex(0xe7f2fa),0);
        lv_obj_set_style_bg_opa(disc,LV_OPA_COVER,0);
        text(s.object,"Shoot",16,108,0xf5f9ff);
        lv_obj_t *close=plain(s.object,128,20,156,60);
        lv_obj_set_style_bg_color(close,lv_color_hex(0x25354f),0);
        lv_obj_set_style_bg_opa(close,LV_OPA_60,0);
        lv_obj_set_style_radius(close,16,0);
        text(close,"Close",24,16,0xf7fafc);
    } else {
        lv_obj_t *patch=plain(s.object,24,12,236,112);
        lv_obj_set_style_bg_color(patch,lv_color_hex(0xd13789),0);
        lv_obj_set_style_bg_opa(patch,LV_OPA_50,0);
        lv_obj_set_style_radius(patch,24,0);
        text(patch,"Overlay",20,38,0x56e2c5);
    }
    lv_obj_update_layout(s.object);
    s.snapshot=lv_snapshot_take(s.object,LV_COLOR_FORMAT_ARGB8888);
    assert(s.snapshot && s.snapshot->header.w==300 && s.snapshot->header.h==144);
    assert(!(s.snapshot->header.flags & LV_IMAGE_FLAGS_PREMULTIPLIED));
    if(!second) {
        const uint32_t *row=(const uint32_t *)(s.snapshot->data+30*s.snapshot->header.stride);
        assert(row[206]==((uint32_t)LV_OPA_60<<24|0x25354fu)); // Straight alpha, not premultiplied.
    }
    size_t bytes=(size_t)s.snapshot->header.w*s.snapshot->header.h*4;
    s.native=malloc(bytes);assert(s.native);
    lv_draw_sw_rotate(s.snapshot->data,s.native,300,144,s.snapshot->header.stride,144*4,
                      LV_DISPLAY_ROTATION_90,LV_COLOR_FORMAT_ARGB8888);
    s.overlay=(camera_overlay){s.native,144,300,144*4,y,(int32_t)SW-x-300};
    return s;
}
static void show_snapshot(sprite *s) {
    lv_obj_add_flag(s->object,LV_OBJ_FLAG_HIDDEN);
    lv_obj_t *image=lv_image_create(lv_screen_active());
    lv_image_set_src(image,s->snapshot);
    lv_obj_set_pos(image,s->x,s->y);
}
static void exact_composite(const camera_overlay *overlays,unsigned count) {
    guarded_frame f=make_frame();
    yield_state state={0};
    assert(camera_composite_frame(f.pixels,source,overlays,count,yield_check,&state));
    assert(state.calls==20);
    assert(!memcmp(reference,f.pixels,BYTES));
    guards(&f);free(f.allocation);checks++;
}
static unsigned component_delta(unsigned a,unsigned b) {return a>b?a-b:b-a;}
static void inspect_widget_rounding(const camera_overlay *overlays,unsigned count) {
    guarded_frame f=make_frame();
    assert(camera_composite_frame(f.pixels,source,overlays,count,NULL,NULL));
    unsigned differing=0,max_r=0,max_g=0,max_b=0;
    for(size_t i=0;i<PIXELS;i++)if(reference[i]!=f.pixels[i]) {
        bool in_overlay=false;
        unsigned x=(unsigned)(i%DW),y=(unsigned)(i/DW);
        for(unsigned j=0;j<count;j++) {
            const camera_overlay *o=&overlays[j];
            if(x>=(unsigned)o->x && x<(unsigned)o->x+o->width &&
               y>=(unsigned)o->y && y<(unsigned)o->y+o->height)in_overlay=true;
        }
        assert(in_overlay);
        differing++;
        unsigned a=reference[i],b=f.pixels[i],d;
        d=component_delta((a>>11)&31,(b>>11)&31);if(d>max_r)max_r=d;
        d=component_delta((a>>5)&63,(b>>5)&63);if(d>max_g)max_g=d;
        d=component_delta(a&31,b&31);if(d>max_b)max_b=d;
    }
    // Flattening to ARGB before RGB565 changes quantization order. The exact
    // sprite-reference test remains strict; cap this separate widget fixture
    // at its measured two native channel steps, with no outside differences.
    assert(max_r<=2 && max_g<=2 && max_b<=2);
    printf("CAMERA_COMPOSITOR_WIDGET_SNAPSHOT_DIFFERENCE pixels=%u max_rgb565_steps=%u,%u,%u\n",
           differing,max_r,max_g,max_b);
    guards(&f);free(f.allocation);checks++;
}
static void raw_alpha_and_padding(void) {
    // Native portrait sprite touches the bottom/right edges. In landscape this
    // is an 11x9 ARGB image at x=0,y=711. Source rows include padding sentinels.
    const unsigned w=9,h=11,stride=(w+3)*4;
    uint32_t *native=malloc((size_t)h*stride);assert(native);
    uint32_t *land=malloc((size_t)w*h*4);assert(land);
    for(unsigned y=0;y<h;y++)for(unsigned x=0;x<stride/4;x++) {
        unsigned alpha=x>=w?0xdeu:(x%3==0?0u:x%3==1?255u:(y*23+x*17)%256);
        native[y*(stride/4)+x]=(alpha<<24)|(((x*31)&255)<<16)|(((y*21)&255)<<8)|0x4d;
    }
    lv_draw_sw_rotate(native,land,w,h,stride,h*4,LV_DISPLAY_ROTATION_270,LV_COLOR_FORMAT_ARGB8888);
    lv_image_dsc_t dsc={0};
    dsc.header.magic=LV_IMAGE_HEADER_MAGIC;
    dsc.header.cf=LV_COLOR_FORMAT_ARGB8888;
    dsc.header.w=h;dsc.header.h=w;dsc.header.stride=h*4;
    dsc.data_size=w*h*4;dsc.data=(const uint8_t *)land;
    lv_obj_t *image=lv_image_create(lv_screen_active());
    lv_image_set_src(image,&dsc);lv_obj_set_pos(image,0,SH-w);
    render_reference();
    camera_overlay overlay={native,w,h,stride,DW-w,DH-h};
    exact_composite(&overlay,1);
    lv_obj_delete(image);free(land);free(native);
}
static void expect_invalid(const camera_overlay *overlays,unsigned count,
                           uint16_t *destination,const uint16_t *input,guarded_frame *f) {
    memset(f->pixels,0xa5,BYTES);
    yield_state state={0};
    assert(!camera_composite_frame(destination,input,overlays,count,yield_check,&state));
    assert(!state.calls);
    for(size_t i=0;i<PIXELS;i++)assert(f->pixels[i]==0xa5a5);
    guards(f);checks++;
}
static void bad_inputs(void) {
    guarded_frame f=make_frame();
    uint32_t pixel=0xffffffff;
    camera_overlay base={&pixel,1,1,4,0,0},bad;
    expect_invalid(NULL,1,f.pixels,source,&f);
    expect_invalid((const camera_overlay *)f.pixels,1,f.pixels,source,&f);
    expect_invalid(NULL,0,NULL,source,&f);
    expect_invalid(NULL,0,f.pixels,NULL,&f);
    expect_invalid(NULL,0,f.pixels,f.pixels,&f);
    expect_invalid(NULL,0,f.pixels,f.pixels+1,&f);
    expect_invalid(NULL,0,(uint16_t *)((uint8_t *)f.pixels+1),source,&f);
    expect_invalid(NULL,0,f.pixels,(const uint16_t *)((const uint8_t *)source+1),&f);
    bad=base;bad.pixels=NULL;expect_invalid(&bad,1,f.pixels,source,&f);
    bad=base;bad.width=0;expect_invalid(&bad,1,f.pixels,source,&f);
    bad=base;bad.height=0;expect_invalid(&bad,1,f.pixels,source,&f);
    bad=base;bad.width=DW+1;expect_invalid(&bad,1,f.pixels,source,&f);
    bad=base;bad.height=DH+1;expect_invalid(&bad,1,f.pixels,source,&f);
    bad=base;bad.x=-1;expect_invalid(&bad,1,f.pixels,source,&f);
    bad=base;bad.y=-1;expect_invalid(&bad,1,f.pixels,source,&f);
    bad=base;bad.x=DW;expect_invalid(&bad,1,f.pixels,source,&f);
    bad=base;bad.y=DH;expect_invalid(&bad,1,f.pixels,source,&f);
    bad=base;bad.x=INT32_MAX;expect_invalid(&bad,1,f.pixels,source,&f);
    bad=base;bad.width=UINT32_MAX;expect_invalid(&bad,1,f.pixels,source,&f);
    bad=base;bad.stride=0;expect_invalid(&bad,1,f.pixels,source,&f);
    bad=base;bad.stride=5;expect_invalid(&bad,1,f.pixels,source,&f);
    bad=base;bad.stride=UINT32_MAX;expect_invalid(&bad,1,f.pixels,source,&f);
    bad=base;bad.pixels=(const uint32_t *)((const uint8_t *)&pixel+2);expect_invalid(&bad,1,f.pixels,source,&f);
    bad=base;bad.pixels=(const uint32_t *)f.pixels;expect_invalid(&bad,1,f.pixels,source,&f);
    bad=base;bad.pixels=(const uint32_t *)(UINTPTR_MAX-3u);expect_invalid(&bad,1,f.pixels,source,&f);
    camera_overlay later[2]={base,base};later[1].x=DW;
    expect_invalid(later,2,f.pixels,source,&f);
    free(f.allocation);
}
static void cancellations(void) {
    uint16_t *rotated=malloc(BYTES);assert(rotated);
    lv_draw_sw_rotate(source,rotated,SW,SH,SW*2,DW*2,LV_DISPLAY_ROTATION_90,LV_COLOR_FORMAT_RGB565);
    uint32_t red=0xffff0000;
    camera_overlay overlay={&red,1,1,4,0,0};
    // Every possible stop point: completed native rows must match; the rest must
    // retain the sentinel. Overlays must never be applied after cancellation.
    for(unsigned stop=1;stop<=20;stop++) {
        guarded_frame f=make_frame();
        yield_state state={0,stop};
        assert(!camera_composite_frame(f.pixels,source,&overlay,1,yield_check,&state));
        assert(state.calls==stop);
        for(unsigned y=0;y<DH;y++)for(unsigned x=0;x<DW;x++)
            assert(f.pixels[(size_t)y*DW+x]==(y>=DH-stop*64?rotated[(size_t)y*DW+x]:0xa5a5));
        guards(&f);free(f.allocation);checks++;
    }
    free(rotated);
}
static void halfword_alignment(void) {
    uint8_t *input=malloc(BYTES+4),*output=malloc(BYTES+2*GUARD+4);
    uint16_t *expected=malloc(BYTES);
    assert(input && output && expected);
    memcpy(input+2,source,BYTES);
    memset(output,0xa5,BYTES+2*GUARD+4);
    uint16_t *destination=(uint16_t *)(output+GUARD+2);
    lv_draw_sw_rotate(source,expected,SW,SH,SW*2,DW*2,LV_DISPLAY_ROTATION_90,LV_COLOR_FORMAT_RGB565);
    assert(camera_composite_frame(destination,(const uint16_t *)(input+2),NULL,0,NULL,NULL));
    assert(!memcmp(destination,expected,BYTES));
    assert(!memcmp(input+2,source,BYTES));
    for(unsigned i=0;i<GUARD+2;i++) {
        assert(output[i]==0xa5);
        assert(output[GUARD+2+BYTES+i]==0xa5);
    }
    free(input);free(output);free(expected);checks++;
}
int main(void) {
    lv_init();
    source=malloc(BYTES);display_pixels=malloc(BYTES);reference=malloc(BYTES);
    assert(source && display_pixels && reference);
    for(unsigned y=0;y<SH;y++)for(unsigned x=0;x<SW;x++)
        source[(size_t)y*SW+x]=(uint16_t)(((x*7+y*3)&31)<<11|((x*5+y*11)&63)<<5|((x*13+y*17)&31));
    display=lv_display_create(SW,SH);assert(display);
    lv_display_set_color_format(display,LV_COLOR_FORMAT_RGB565);
    lv_display_set_buffers(display,display_pixels,NULL,BYTES,LV_DISPLAY_RENDER_MODE_FULL);
    lv_display_set_flush_cb(display,flush);
    lv_obj_t *screen=lv_screen_active();
    lv_obj_set_style_bg_opa(screen,LV_OPA_COVER,0);
    lv_obj_t *canvas=lv_canvas_create(screen);
    lv_canvas_set_buffer(canvas,source,SW,SH,LV_COLOR_FORMAT_RGB565);
    lv_obj_set_pos(canvas,0,0);
    render_reference();exact_composite(NULL,0);
    raw_alpha_and_padding();
    sprite first=make_sprite(screen,28,202,false),second=make_sprite(screen,242,274,true);
    camera_overlay overlays[2]={first.overlay,second.overlay};
    render_reference();inspect_widget_rounding(overlays,2);
    show_snapshot(&first);show_snapshot(&second);
    render_reference();exact_composite(overlays,2);
    bad_inputs();cancellations();halfword_alignment();
    for(unsigned y=0;y<SH;y++)for(unsigned x=0;x<SW;x++)
        assert(source[(size_t)y*SW+x]==(uint16_t)(((x*7+y*3)&31)<<11|((x*5+y*11)&63)<<5|((x*13+y*17)&31)));
    lv_display_delete(display);
    lv_draw_buf_destroy(first.snapshot);lv_draw_buf_destroy(second.snapshot);
    free(first.native);free(second.native);
    free(source);free(display_pixels);free(reference);
    lv_deinit();
    printf("CAMERA_COMPOSITOR_PASS: %u checks; real LVGL sprites, alpha, overlap, padded strides, input rejection and 20 cancellation boundaries\n",checks);
    return 0;
}



