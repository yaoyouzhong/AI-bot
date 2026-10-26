// Compile the actual viewport body with a deterministic display-port facade.
// Real LVGL snapshots and the real compositor produce every pixel under test.
#undef NDEBUG
#include "lvgl.h"
#include "src/core/lv_obj_private.h"
#include "src/core/lv_obj_draw_private.h"
#include "camera_compositor.h"
#include "camera_canvas.h"
#include "camera_viewport.h"
#include "soc/icm_sys_struct.h"
#include <assert.h>
#include <stdatomic.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#ifdef _WIN32
#include <windows.h>
#endif

#define SW 1280u
#define SH 720u
#define DW 720u
#define DH 1280u
#define BYTES ((size_t)SW*SH*2)
#define MALLOC_CAP_SPIRAM 1
#define MALLOC_CAP_8BIT 2
#define MALLOC_CAP_INTERNAL 4

typedef struct { void *raw; uint8_t *ptr; size_t size; } allocation;
static allocation allocations[8];
static unsigned allocation_calls, fail_allocation, live_allocations;
static unsigned begin_calls, back_calls, present_calls, end_calls, delays, freeze_calls, unfreeze_calls;
static unsigned check_count,probe_begins,probe_ends,diagnostic_heap_calls,diagnostic_free_calls;
static bool diagnostic_largest_zero;
static size_t heap_caps_get_largest_free_block(unsigned caps){diagnostic_heap_calls++;return diagnostic_largest_zero?0:1000+caps;}
static size_t heap_caps_get_free_size(unsigned caps){diagnostic_free_calls++;return 10000+caps;}
static bool probe_active;
static bool begin_ok=true, back_ok=true, present_ok=true, end_ok=true, port_active;
static bool ordinary_refresh_paused, freeze_ok=true, unfreeze_ok=true;
static uint16_t *framebuffers[2], *source_pixels, *render_pixels, *expected;
static unsigned front_index;
static lv_display_t *fixture_display;
static lv_obj_t *fixture_canvas, *fixture_shutter, *fixture_close;
static int64_t fake_time;

static void *allocate(size_t size,size_t align) {
    allocation_calls++;
    if(fail_allocation==allocation_calls)return NULL;
    unsigned slot=0;while(slot<8 && allocations[slot].raw)slot++;
    assert(slot<8 && align && !(align&(align-1)));
    uint8_t *raw=malloc(size+align+32);assert(raw);
    uintptr_t aligned=((uintptr_t)(raw+16)+align-1)&~(uintptr_t)(align-1);
    uint8_t *ptr=(uint8_t *)aligned;
    memset(ptr-16,0xa5,16);memset(ptr+size,0xa5,16);
    allocations[slot]=(allocation){raw,ptr,size};live_allocations++;
    return ptr;
}
static void *heap_caps_aligned_alloc(size_t align,size_t size,unsigned caps) {
    assert(caps==(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT));return allocate(size,align);
}
static void *heap_caps_malloc(size_t size,unsigned caps) {
    assert(caps==(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT));return allocate(size,16);
}
static void tracked_free(void *ptr) {
    if(!ptr)return;
    unsigned slot=0;while(slot<8 && allocations[slot].ptr!=ptr)slot++;
    assert(slot<8);
    allocation *a=&allocations[slot];
    for(unsigned i=0;i<16;i++) {assert(a->ptr[(int)i-16]==0xa5);assert(a->ptr[a->size+i]==0xa5);}
    free(a->raw);memset(a,0,sizeof(*a));live_allocations--;
}
static int64_t esp_timer_get_time(void) { fake_time+=2300;return fake_time; }
static void vTaskDelay(unsigned ticks) { assert(ticks==1);delays++;fake_time+=1000; }
static bool lvgl_port_camera_begin(lv_display_t *d) {
    assert(d==fixture_display);begin_calls++;port_active=true;ordinary_refresh_paused=true;
    return begin_ok;
}
static uint16_t *lvgl_port_camera_back_buffer(lv_display_t *d) {
    assert(d==fixture_display && port_active);back_calls++;
    return back_ok?framebuffers[1-front_index]:NULL;
}
static bool lvgl_port_camera_present(lv_display_t *d,unsigned timeout) {
    assert(d==fixture_display && port_active && timeout==250);present_calls++;
    if(present_ok)front_index=1-front_index;
    return present_ok;
}
static bool lvgl_port_camera_end(lv_display_t *d,unsigned timeout) {
    assert(d==fixture_display && timeout==250);end_calls++;
    if(end_ok){port_active=false;ordinary_refresh_paused=false;}
    return end_ok;
}

static bool lvgl_port_display_freeze(lv_display_t *d,unsigned timeout) {
    assert(d==fixture_display && timeout==250);freeze_calls++;
    if(!freeze_ok)return false;
    assert(!port_active);port_active=true;ordinary_refresh_paused=true;return true;
}
static bool lvgl_port_display_unfreeze(lv_display_t *d) {
    assert(d==fixture_display && camera_viewport_still_active());unfreeze_calls++;
    if(unfreeze_ok){port_active=false;ordinary_refresh_paused=false;}
    return unfreeze_ok;
}

// Exercise the real bandwidth lease in every real viewport lifecycle below.
// Only the RTOS lock is replaced; register fields are the pinned SDK layout.
#define DW_GDMA_LL_MASTER_PORT_MEMORY 1
typedef int portMUX_TYPE;
#define portMUX_INITIALIZER_UNLOCKED 0
static bool bandwidth_locked;
#define portENTER_CRITICAL(p) do{assert(!bandwidth_locked);bandwidth_locked=true;(void)(p);}while(0)
#define portEXIT_CRITICAL(p) do{assert(bandwidth_locked);bandwidth_locked=false;(void)(p);}while(0)
axi_icm_dev_t AXI_ICM;
#include "display_bandwidth_under_test.inc"
static unsigned starting_leases,starting_releases;
static void bandwidth_applied(void) {
    assert(active && AXI_ICM.mst_arb_priority_reg0.val==0x876f4321u);
    assert(AXI_ICM.mst_arqos_reg0.val==0x123f5678u);
    assert(AXI_ICM.mst_awqos_reg0.val==0xabcdef01u);
}
static void bandwidth_restored(void) {
    assert(!active && !bandwidth_locked);
    assert(AXI_ICM.mst_arb_priority_reg0.val==0x87654321u);
    assert(AXI_ICM.mst_arqos_reg0.val==0x12345678u);
    assert(AXI_ICM.mst_awqos_reg0.val==0xabcdef01u);
}

static void photo_display_probe_begin(void) {
    assert(ordinary_refresh_paused && port_active && !probe_active && camera_viewport_still_active());
    bandwidth_applied();
    assert(leases==starting_leases+1 && releases==starting_releases);
    probe_begins++;probe_active=true;
}
static void photo_display_probe_end(void) {
    assert(!ordinary_refresh_paused && !port_active && probe_active && camera_viewport_still_active());
    bandwidth_applied(); // Lease must cover the successful unfreeze itself.
    probe_ends++;probe_active=false;
}

#define free tracked_free
#include "camera_viewport_under_test.inc"
#undef free
typedef struct {uint64_t encode_us,exchange_us,decode_us;unsigned calls,wire_tx,wire_rx,min_internal,min_psram,min_largest;} rpc_profile;
#include "rpc_memory_diagnostic_under_test.inc"

static void diagnostic_sampling(bool held) {
    assert(camera_viewport_still_active()==held);
    for(unsigned caps=3;caps<=6;caps+=3) {
        unsigned before=diagnostic_heap_calls;
        assert(camera_viewport_diagnostic_largest(caps)==(held?0:1000+caps));
        assert(diagnostic_heap_calls==before+(held?0:1));
    }
    rpc_profile p={.min_internal=50000,.min_psram=50000,.min_largest=50000};
    unsigned before=diagnostic_heap_calls,free_before=diagnostic_free_calls;
    profile_memory(&p);
    assert(diagnostic_heap_calls==before+(held?0:1));
    assert(diagnostic_free_calls==free_before+(held?0:2));
    assert(p.min_internal==(held?50000:10006));
    assert(p.min_psram==(held?50000:10003));
    assert(p.min_largest==(held?50000:1006));
    if(!held) {
        diagnostic_largest_zero=true;profile_memory(&p);
        assert(p.min_largest==0);diagnostic_largest_zero=false;
    }
}
static void flush(lv_display_t *d,const lv_area_t *a,uint8_t *pixels) {
    (void)a;(void)pixels;lv_display_flush_ready(d);
}
static lv_obj_t *plain(lv_obj_t *parent,int x,int y,int w,int h) {
    lv_obj_t *o=lv_obj_create(parent);lv_obj_remove_style_all(o);
    lv_obj_set_pos(o,x,y);lv_obj_set_size(o,w,h);
    lv_obj_remove_flag(o,LV_OBJ_FLAG_SCROLLABLE);return o;
}
static lv_obj_t *caption(lv_obj_t *parent,const char *value,int x,int y) {
    lv_obj_t *o=lv_label_create(parent);lv_obj_remove_style_all(o);
    lv_label_set_text(o,value);lv_obj_set_pos(o,x,y);
    lv_obj_set_style_text_font(o,&lv_font_montserrat_24,0);
    lv_obj_set_style_text_color(o,lv_color_hex(0xf1f7ff),0);return o;
}
static void fixtures(void) {
    lv_obj_t *screen=lv_screen_active();lv_obj_remove_style_all(screen);
    lv_obj_set_style_bg_opa(screen,LV_OPA_COVER,0);
    fixture_canvas=lv_canvas_create(screen);
    lv_canvas_set_buffer(fixture_canvas,source_pixels,SW,SH,LV_COLOR_FORMAT_RGB565);
    lv_obj_set_pos(fixture_canvas,0,0);
    fixture_shutter=plain(screen,0,208,188,304);
    lv_obj_set_style_opa(fixture_shutter,LV_OPA_40,LV_STATE_DISABLED);
    lv_obj_t *ring=plain(fixture_shutter,30,64,128,128);
    lv_obj_set_style_radius(ring,LV_RADIUS_CIRCLE,0);
    lv_obj_set_style_border_width(ring,4,0);
    lv_obj_set_style_border_color(ring,lv_color_hex(0xf1f7ff),0);
    lv_obj_t *disc=plain(fixture_shutter,42,76,104,104);
    lv_obj_set_style_radius(disc,LV_RADIUS_CIRCLE,0);
    lv_obj_set_style_bg_color(disc,lv_color_hex(0xf1f7ff),0);
    lv_obj_set_style_bg_opa(disc,LV_OPA_COVER,0);
    caption(fixture_shutter,"Shoot",54,218);
    fixture_close=plain(screen,1104,16,148,56);
    lv_obj_set_style_radius(fixture_close,14,0);
    lv_obj_set_style_bg_opa(fixture_close,LV_OPA_60,0);
    lv_obj_set_style_bg_color(fixture_close,lv_color_hex(0x324654),0);
    lv_obj_set_style_bg_color(fixture_close,lv_color_hex(0xff0033),LV_STATE_DISABLED);
    caption(fixture_close,"Close",35,14);
    camera_viewport_set_controls(fixture_shutter,fixture_close);
}
static void oracle(void) {
    // LVGL renders its own flattened widget sprites over the actual camera
    // source. Its native rotation is an independent oracle for the viewport.
    lv_obj_t *widgets[]={fixture_shutter,fixture_close}, *images[2];
    lv_draw_buf_t *snapshots[2];
    for(unsigned i=0;i<2;i++) {
        lv_obj_remove_flag(widgets[i],LV_OBJ_FLAG_HIDDEN);
        lv_obj_remove_state(widgets[i],LV_STATE_ANY);lv_obj_update_layout(widgets[i]);
        lv_area_t a;lv_obj_get_coords(widgets[i],&a);
        int ext=lv_obj_get_ext_draw_size(widgets[i]);
        snapshots[i]=lv_snapshot_take(widgets[i],LV_COLOR_FORMAT_ARGB8888);assert(snapshots[i]);
        lv_obj_add_flag(widgets[i],LV_OBJ_FLAG_HIDDEN);
        images[i]=lv_image_create(lv_screen_active());
        lv_image_set_src(images[i],snapshots[i]);lv_obj_set_pos(images[i],a.x1-ext,a.y1-ext);
    }
    lv_obj_invalidate(lv_screen_active());lv_refr_now(fixture_display);
    unsigned w=lv_display_get_horizontal_resolution(fixture_display),h=lv_display_get_vertical_resolution(fixture_display);
    if(lv_display_get_rotation(fixture_display)==LV_DISPLAY_ROTATION_0)memcpy(expected,render_pixels,BYTES);
    else lv_draw_sw_rotate(render_pixels,expected,w,h,w*2,DW*2,
                      lv_display_get_rotation(fixture_display),LV_COLOR_FORMAT_RGB565);
    for(unsigned i=0;i<2;i++) {lv_obj_delete(images[i]);lv_draw_buf_destroy(snapshots[i]);}
    lv_obj_add_flag(fixture_shutter,LV_OBJ_FLAG_HIDDEN);
    lv_obj_add_state(fixture_shutter,LV_STATE_DISABLED|LV_STATE_PRESSED);
    lv_obj_remove_flag(fixture_close,LV_OBJ_FLAG_HIDDEN);
    lv_obj_add_state(fixture_close,LV_STATE_DISABLED|LV_STATE_FOCUSED|LV_STATE_FOCUS_KEY);
}
static void states_restored(void) {
    assert(lv_obj_has_flag(fixture_shutter,LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_get_state(fixture_shutter)==(LV_STATE_DISABLED|LV_STATE_PRESSED));
    assert(!lv_obj_has_flag(fixture_close,LV_OBJ_FLAG_HIDDEN));
    assert(lv_obj_get_state(fixture_close)==(LV_STATE_DISABLED|LV_STATE_FOCUSED|LV_STATE_FOCUS_KEY));
}
static void reset_facade(void) {
    assert(!live_allocations && !port_active && !ordinary_refresh_paused);
    assert(!camera_viewport_still_active() && !probe_active);diagnostic_sampling(false);
    bandwidth_restored();
    assert(leases==releases);starting_leases=leases;starting_releases=releases;
    probe_begins=probe_ends=0;
    allocation_calls=fail_allocation=begin_calls=back_calls=present_calls=end_calls=delays=0;
    begin_ok=back_ok=present_ok=end_ok=freeze_ok=unfreeze_ok=true;
    freeze_calls=unfreeze_calls=0;
    front_index=0;memset(framebuffers[0],0x5a,BYTES);memset(framebuffers[1],0xa5,BYTES);
}
static void diagnostics(unsigned count,unsigned mode) {
    unsigned n,c,p,m;camera_viewport_diagnostics(&n,&c,&p,&m);
    assert(n==count && m==mode);
    if(n)assert(c>0 && p>0);
}
static void successful_session(void) {
    reset_facade();assert(camera_viewport_begin(fixture_canvas)==1);
    assert(live_allocations==2 && allocation_calls==4 && begin_calls==1);
    states_restored();diagnostics(0,1);
    assert(camera_viewport_begin(fixture_canvas)==-1);
    assert(camera_viewport_frame(source_pixels,NULL,NULL));
    if(memcmp(framebuffers[front_index],expected,BYTES)) {
        unsigned differing=0,outside=0,first=0;
        for(unsigned i=0;i<SW*SH;i++)if(framebuffers[front_index][i]!=expected[i]) {
            if(!differing)first=i;differing++;
            unsigned x=i%DW,y=i/DW;bool widget=false;
            for(unsigned j=0;j<2;j++)widget|=x>=(unsigned)overlays[j].x&&x<(unsigned)overlays[j].x+overlays[j].width&&
                y>=(unsigned)overlays[j].y&&y<(unsigned)overlays[j].y+overlays[j].height;
            outside+=!widget;
        }
        fprintf(stderr,"VIEWPORT_DIRECTION_MISMATCH rotation=%d pixels=%u outside=%u first=%u,%u actual=%04x expected=%04x\n",
            lv_display_get_rotation(fixture_display),differing,outside,first%DW,first/DW,framebuffers[front_index][first],expected[first]);
    }
    assert(!memcmp(framebuffers[front_index],expected,BYTES));
    assert(front_index==1 && present_calls==1 && delays>0);diagnostics(1,1);
    assert(camera_viewport_frame(source_pixels,NULL,NULL));
    assert(front_index==0 && present_calls==2);
    assert(!memcmp(framebuffers[front_index],expected,BYTES));diagnostics(2,1);
    assert(camera_viewport_end());assert(!live_allocations && !port_active);
    assert(end_calls==1);assert(camera_viewport_end());assert(end_calls==1);
    assert(!camera_viewport_frame(source_pixels,NULL,NULL));diagnostics(2,0);
    states_restored();check_count++;
}
typedef struct { unsigned calls, stop; } cancellation;
static bool keep_frame(void *context) {
    cancellation *c=context;return ++c->calls<c->stop;
}
static void cancellation_sessions(void) {
    unsigned stops[]={1,20,40,41};
    for(unsigned i=0;i<sizeof(stops)/sizeof(stops[0]);i++) {
        reset_facade();assert(camera_viewport_begin(fixture_canvas)==1);
        cancellation c={0,stops[i]};
        assert(!camera_viewport_frame(source_pixels,keep_frame,&c));
        assert(c.calls==stops[i] && !present_calls && front_index==0);
        for(size_t j=0;j<BYTES/2;j++)assert(framebuffers[front_index][j]==0x5a5a);
        diagnostics(0,1);assert(camera_viewport_end());assert(!live_allocations);check_count++;
    }
}
static void allocation_failures(void) {
    for(unsigned fail=1;fail<=4;fail++) {
        reset_facade();fail_allocation=fail;
        assert(camera_viewport_begin(fixture_canvas)==0);
        assert(!live_allocations && !begin_calls && !port_active);
        assert(!camera_viewport_frame(source_pixels,NULL,NULL));
        states_restored();assert(camera_viewport_end());diagnostics(0,0);check_count++;
    }
    reset_facade();assert(camera_viewport_begin(NULL)==0);assert(!allocation_calls);
    camera_viewport_set_controls(fixture_shutter,NULL);
    assert(camera_viewport_begin(fixture_canvas)==0);assert(!live_allocations && !begin_calls);
    camera_viewport_set_controls(fixture_shutter,fixture_close);states_restored();check_count++;
}
static void port_failures(void) {
    reset_facade();begin_ok=false;
    assert(camera_viewport_begin(fixture_canvas)==0);
    assert(begin_calls==1 && end_calls==1 && !live_allocations && !port_active);
    diagnostics(0,0);check_count++;

    reset_facade();begin_ok=false;end_ok=false;
    assert(camera_viewport_begin(fixture_canvas)==-1);
    assert(port_active && begin_calls==1 && end_calls==1);diagnostics(0,2);
    assert(camera_viewport_begin(fixture_canvas)==-1);assert(begin_calls==1);
    assert(!camera_viewport_end());assert(port_active);
    end_ok=true;assert(camera_viewport_end());assert(!port_active && !live_allocations);
    diagnostics(0,0);check_count++;

    reset_facade();assert(camera_viewport_begin(fixture_canvas)==1);back_ok=false;
    assert(!camera_viewport_frame(source_pixels,NULL,NULL));assert(!present_calls && back_calls==1);
    assert(camera_viewport_end());assert(!live_allocations);check_count++;

    reset_facade();assert(camera_viewport_begin(fixture_canvas)==1);present_ok=false;
    assert(!camera_viewport_frame(source_pixels,NULL,NULL));assert(present_calls==1 && front_index==0);
    diagnostics(0,2);assert(camera_viewport_end());assert(!live_allocations);check_count++;

    reset_facade();assert(camera_viewport_begin(fixture_canvas)==1);end_ok=false;
    assert(!camera_viewport_end());assert(port_active);diagnostics(0,2);
    assert(camera_viewport_begin(fixture_canvas)==-1);
    end_ok=true;assert(camera_viewport_end());assert(!port_active && !live_allocations);
    check_count++;
}
static void still_sessions(void) {
    reset_facade();assert(camera_viewport_still_begin(NULL)==0);
    memcpy(framebuffers[0],expected,BYTES);
    assert(camera_viewport_still_begin(fixture_canvas)==1);
    assert(camera_viewport_still_active() && ordinary_refresh_paused && port_active);diagnostic_sampling(true);
    assert(freeze_calls==1 && !unfreeze_calls && !begin_calls && !end_calls && !allocation_calls);
    assert(probe_begins==1 && !probe_ends && probe_active);
    bandwidth_applied();assert(leases==starting_leases+1 && releases==starting_releases);
    assert(!back_calls && !present_calls && !delays && !live_allocations);
    assert(!memcmp(framebuffers[0],expected,BYTES));
    assert(camera_viewport_still_begin(fixture_canvas)==-1 && freeze_calls==1);diagnostic_sampling(true);
    assert(camera_viewport_begin(fixture_canvas)==-1 && !begin_calls);
    assert(!camera_viewport_frame(source_pixels,NULL,NULL));
    assert(camera_viewport_end() && !end_calls && camera_viewport_still_active());
    unsigned n,c,p,m;camera_viewport_diagnostics(&n,&c,&p,&m);assert(m==3);
    assert(camera_viewport_still_end());
    assert(!ordinary_refresh_paused && !port_active && !camera_viewport_still_active());diagnostic_sampling(false);
    assert(!memcmp(framebuffers[0],expected,BYTES));
    assert(camera_viewport_still_end() && unfreeze_calls==1 && !end_calls);diagnostic_sampling(false);
    assert(probe_begins==1 && probe_ends==1 && !probe_active);
    bandwidth_restored();assert(leases==starting_leases+1 && releases==starting_releases+1);check_count++;

    // A failed freeze never acquires scanout and must not call camera_end to
    // "recover": that would select/cache-sync the very frame being preserved.
    reset_facade();freeze_ok=false;
    assert(camera_viewport_still_begin(fixture_canvas)==0);
    assert(freeze_calls==1 && !unfreeze_calls && !begin_calls && !end_calls);
    assert(!probe_begins && !probe_ends && !probe_active);
    bandwidth_restored();assert(leases==starting_leases && releases==starting_releases);
    assert(!port_active && !ordinary_refresh_paused && !camera_viewport_still_active());diagnostic_sampling(false);
    assert(!allocation_calls);freeze_ok=true;
    assert(camera_viewport_still_begin(fixture_canvas)==1);
    assert(camera_viewport_still_end() && !begin_calls && !end_calls);check_count++;

    // Wrong-owner release cannot expose frames to a new live session.
    reset_facade();assert(camera_viewport_still_begin(fixture_canvas)==1);unfreeze_ok=false;
    assert(!camera_viewport_still_end() && camera_viewport_still_active());diagnostic_sampling(true);
    assert(probe_begins==1 && !probe_ends && probe_active);
    assert(camera_viewport_begin(fixture_canvas)==-1 && !begin_calls);
    assert(ordinary_refresh_paused && !allocation_calls);
    unfreeze_ok=true;assert(camera_viewport_still_end());assert(!ordinary_refresh_paused);diagnostic_sampling(false);
    assert(probe_begins==1 && probe_ends==1 && !probe_active);check_count++;

    // A still request cannot acquire or release the live camera's display.
    reset_facade();assert(camera_viewport_begin(fixture_canvas)==1);
    assert(camera_viewport_still_begin(fixture_canvas)==0 && begin_calls==1 && !freeze_calls);
    assert(!camera_viewport_still_active());
    assert(camera_viewport_still_end() && !end_calls && !unfreeze_calls && port_active);
    assert(camera_viewport_end() && end_calls==1);check_count++;

    reset_facade();camera_viewport_set_controls(NULL,NULL);
    assert(camera_viewport_still_begin(fixture_canvas)==1 && !allocation_calls);
    assert(camera_viewport_still_end() && !begin_calls && !end_calls);
    camera_viewport_set_controls(fixture_shutter,fixture_close);check_count++;
}
int main(void) {
#ifdef _WIN32
    SetErrorMode(SEM_FAILCRITICALERRORS|SEM_NOGPFAULTERRORBOX);
    _set_error_mode(_OUT_TO_STDERR);_set_abort_behavior(0,_WRITE_ABORT_MSG|_CALL_REPORTFAULT);
#endif
    AXI_ICM.mst_arb_priority_reg0.val=0x87654321u;
    AXI_ICM.mst_arqos_reg0.val=0x12345678u;
    AXI_ICM.mst_awqos_reg0.val=0xabcdef01u;
    lv_init();source_pixels=malloc(BYTES);render_pixels=malloc(BYTES);expected=malloc(BYTES);
    framebuffers[0]=malloc(BYTES);framebuffers[1]=malloc(BYTES);
    assert(source_pixels && render_pixels && expected && framebuffers[0] && framebuffers[1]);
    for(unsigned y=0;y<SH;y++)for(unsigned x=0;x<SW;x++)
        source_pixels[(size_t)y*SW+x]=(uint16_t)(((x+y*7)&31)<<11|((x*3+y)&63)<<5|((x*13+y*3)&31));
    // Match the board's native portrait panel and all four camera poses.
    // Compare every composed pixel to the actual LVGL image and widget render.
    fixture_display=lv_display_create(DW,DH);assert(fixture_display);
    lv_display_set_rotation(fixture_display,LV_DISPLAY_ROTATION_90);
    lv_display_set_color_format(fixture_display,LV_COLOR_FORMAT_RGB565);
    lv_display_set_buffers(fixture_display,render_pixels,NULL,BYTES,LV_DISPLAY_RENDER_MODE_FULL);
    lv_display_set_flush_cb(fixture_display,flush);
    fixtures();camera_canvas_init(fixture_canvas);
    int poses[]={0,2,1,-1};
    for(unsigned pose_index=0;pose_index<4;pose_index++) {
        int pose=poses[pose_index];camera_geometry g=camera_geometry_for(pose);
        lv_display_set_rotation(fixture_display,(lv_display_rotation_t)((5-pose)%4));
        lv_display_set_buffers(fixture_display,render_pixels,NULL,BYTES,LV_DISPLAY_RENDER_MODE_FULL);
        lv_obj_set_pos(fixture_shutter,g.shutter_x,g.shutter_y);
        lv_obj_set_pos(fixture_close,g.width-176,16);
        camera_canvas_direction(fixture_canvas,pose);
        oracle();successful_session();allocation_failures();cancellation_sessions();port_failures();still_sessions();
        // Retake reopens after every failure, rebuilding snapshots without leaks.
        for(unsigned i=0;i<3;i++)successful_session();
    }
    assert(!live_allocations);lv_display_delete(fixture_display);lv_deinit();
    free(framebuffers[0]);free(framebuffers[1]);free(expected);free(render_pixels);free(source_pixels);
    printf("CAMERA_VIEWPORT_PASS: %u sessions; actual snapshots, exact frame pixels, state restoration, allocation and port failures, cancellation, immutable still hold/recovery, real QoS restore/retry, atomic still window and diagnostic scan skip/resume, repeat open/close\n",check_count);
    return 0;
}
