// Exercise the production probe with the pinned SDK register layout. Register
// access spies prove format/inactive calls do not read status. Const-qualified
// register views reject configuration writes in every production API at compile
// time; full register snapshots verify all probe calls leave hardware unchanged.
#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include "freertos/FreeRTOS.h"
#include "soc/mipi_dsi_bridge_struct.h"
#include "soc/mipi_dsi_host_struct.h"
static dsi_brg_dev_t fake_bridge;
static dsi_host_dev_t fake_host;
static dsi_brg_dev_t bridge_before_call;
static dsi_host_dev_t host_before_call;
static uint32_t fake_time;
static unsigned io_accesses,task_locks,isr_locks;
static bool locked,locked_isr;
int64_t esp_timer_get_time(void) {return fake_time;}
void probe_test_enter(portMUX_TYPE *lock,bool isr) {
    (void)lock;assert(!locked);locked=true;locked_isr=isr;
    bridge_before_call=fake_bridge;host_before_call=fake_host;
    if(isr)isr_locks++;else task_locks++;
}
void probe_test_exit(portMUX_TYPE *lock,bool isr) {
    (void)lock;assert(locked&&locked_isr==isr);
    assert(!memcmp(&fake_bridge,&bridge_before_call,sizeof(fake_bridge)));
    assert(!memcmp(&fake_host,&host_before_call,sizeof(fake_host)));
    locked=false;
}
static const dsi_brg_dev_t *bridge(void) {assert(locked);io_accesses++;return &fake_bridge;}
static const dsi_host_dev_t *host(void) {assert(locked);io_accesses++;return &fake_host;}
#define MIPI_DSI_BRIDGE (*bridge())
#define MIPI_DSI_HOST (*host())
#include "../../firmware/main/photo_display_probe.c"

static void inactive_and_owned_window(void) {
    char text[350];
    fake_bridge.dpi_rsv_dpi_data.val=0xc0003fffu;
    fake_bridge.dpi_misc_config.fifo_underrun_discard_vcnt=720;
    fake_bridge.pixel_type.val=2;
    fake_bridge.int_ena.val=1;fake_bridge.int_clr.val=0x76543210u;
    fake_host.int_st0.val=0x20;fake_host.int_st1.val=0x80;
    dsi_brg_dev_t before=fake_bridge;dsi_host_dev_t host_before=fake_host;
    photo_display_probe_mark(PHOTO_DISPLAY_PROBE_ENCODE);
    photo_display_probe_mark(0);photo_display_probe_mark(11);
    photo_display_probe_frame(1000);photo_display_probe_end();
    photo_display_probe_format(text,sizeof(text));
    assert(!io_accesses && !probe.active && !probe.pending);
    fake_time=5000;photo_display_probe_mark(PHOTO_DISPLAY_PROBE_CLICK);
    assert(!io_accesses && probe.pending && probe.sequence==1);
    // Failed freeze has no begin call: waiting/formatting still cannot touch HW.
    photo_display_probe_frame(6000);photo_display_probe_format(text,sizeof(text));
    assert(!io_accesses);
    fake_time=7000;photo_display_probe_begin();
    assert(probe.active && probe.stage==PHOTO_DISPLAY_PROBE_FROZEN);
    assert(probe.dwell_us[PHOTO_DISPLAY_PROBE_CLICK]==2000);
    assert(probe.host0_before==0x20 && probe.host1_before==0x80);
    assert(!memcmp(&fake_bridge,&before,sizeof(before)) && !memcmp(&fake_host,&host_before,sizeof(host_before)));
    unsigned accesses=io_accesses;
    photo_display_probe_begin();photo_display_probe_mark(PHOTO_DISPLAY_PROBE_CLICK);
    photo_display_probe_format(text,sizeof(text));
    assert(io_accesses==accesses && probe.sequence==1 && probe.fill_before==0xc0003fffu);
    fake_bridge.int_raw.val=1;fake_bridge.int_st.val=0;fake_bridge.int_ena.val=0;
    fake_host.int_st0.val=0x10000;fake_host.int_st1.val=0;
    photo_display_probe_frame(8000);
    assert(probe.frames==1 && probe.error_frames==1);
    assert(probe.bridge_raw==1 && probe.bridge_status==0 && probe.host0==0x10000);
    assert(probe.error_stages==(1u<<PHOTO_DISPLAY_PROBE_FROZEN));
    assert(fake_bridge.int_clr.val==0x76543210u && fake_bridge.int_raw.val==1);
    fake_bridge.int_raw.val=0;fake_host.int_st0.val=0;
    fake_time=10000;photo_display_probe_mark(PHOTO_DISPLAY_PROBE_ENCODE);
    photo_display_probe_frame(11000);assert(probe.frames==2&&probe.error_frames==1);
    fake_time=20000;photo_display_probe_mark(PHOTO_DISPLAY_PROBE_CRYPT);
    fake_time=25000;photo_display_probe_mark(PHOTO_DISPLAY_PROBE_HASH);
    fake_time=30000;photo_display_probe_mark(PHOTO_DISPLAY_PROBE_NETWORK);
    fake_time=40000;photo_display_probe_mark(PHOTO_DISPLAY_PROBE_CRYPT);
    fake_time=45000;photo_display_probe_mark(PHOTO_DISPLAY_PROBE_HASH);
    fake_time=50000;photo_display_probe_mark(PHOTO_DISPLAY_PROBE_NETWORK);
    fake_time=55000;photo_display_probe_mark(PHOTO_DISPLAY_PROBE_NETWORK);
    assert(probe.dwell_us[PHOTO_DISPLAY_PROBE_CRYPT]==10000);
    assert(probe.dwell_us[PHOTO_DISPLAY_PROBE_HASH]==10000);
    accesses=io_accesses;photo_display_probe_format(text,sizeof(text));
    assert(io_accesses==accesses && strstr(text,",10,10,15,0,0"));
    // End takes a final sample even if no additional frame occurred.
    fake_host.int_st1.val=1u<<19;
    fake_time=60000;photo_display_probe_end();
    assert(!probe.active && probe.fill_unchanged && fake_bridge.dpi_rsv_dpi_data.val==0xc0003fffu);
    assert(probe.host1==(1u<<19) && (probe.error_stages&(1u<<PHOTO_DISPLAY_PROBE_NETWORK)));
    assert(probe.frames==2 && probe.error_frames==1);
    assert(probe.dwell_us[PHOTO_DISPLAY_PROBE_NETWORK]==20000);
    accesses=io_accesses;photo_display_probe_end();photo_display_probe_frame(61000);
    photo_display_probe_mark(PHOTO_DISPLAY_PROBE_ENCODE);photo_display_probe_format(text,sizeof(text));
    assert(io_accesses==accesses);
    assert(strstr(text,",1,00000002,720,") && strstr(text,",10,10,20,0,0"));
    // Lock down every legacy field and position; .262 only adds two fields.
    assert(!strcmp(text,"pd,1,1,0,10,55,7d6,104,2,1,00000001,00000000,00000000,00010000,00080000,00000020,00000080,c0003fff,c0003fff,1,00000002,720,2,3,0,10,0,10,10,20,0,0,3000,0"));
}
static void repeated_attempts_and_wrap(void) {
    fake_time=UINT32_MAX-999;photo_display_probe_mark(PHOTO_DISPLAY_PROBE_CLICK);
    fake_time=1000;photo_display_probe_begin();
    assert(probe.sequence==2 && probe.dwell_us[PHOTO_DISPLAY_PROBE_CLICK]==2000);
    fake_time=2000;photo_display_probe_mark(PHOTO_DISPLAY_PROBE_PREVIEW);
    photo_display_probe_frame(3000);fake_time=4000;photo_display_probe_end();
    assert(probe.elapsed_us==5000 && probe.dwell_us[PHOTO_DISPLAY_PROBE_PREVIEW]==2000);
    assert(probe.fill_unchanged && fake_bridge.dpi_rsv_dpi_data.val==0xc0003fffu);
    fake_time=5000;photo_display_probe_begin(); // Defensive caller without CLICK.
    assert(probe.sequence==3 && probe.dwell_us[PHOTO_DISPLAY_PROBE_CLICK]==0);
    photo_display_probe_end();assert(probe.fill_unchanged);
}
static void external_fill_change_is_only_observed(void) {
    // A different owner may change configuration: the probe reports this and
    // must not restore the earlier value or trigger a shadow-register update.
    fake_time=6000;photo_display_probe_begin();
    assert(probe.fill_before==0xc0003fffu);
    fake_bridge.dpi_rsv_dpi_data.val=0x12345678u;
    fake_bridge.dpi_config_update.val=0xa5a5a5a5u;
    fake_time=7000;photo_display_probe_end();
    assert(!probe.fill_unchanged && probe.fill_after==0x12345678u);
    assert(fake_bridge.dpi_rsv_dpi_data.val==0x12345678u);
    assert(fake_bridge.dpi_config_update.val==0xa5a5a5a5u);
    char text[350];photo_display_probe_format(text,sizeof(text));
    assert(strstr(text,",c0003fff,12345678,0,00000002,720,"));
}
static void scoped_frame_intervals(void) {
    char text[350];
    fake_time=100000;photo_display_probe_mark(PHOTO_DISPLAY_PROBE_CLICK);
    assert(!probe.frame_seen && !probe.frame_interval_max_us && !probe.frame_intervals_over_18ms);
    unsigned accesses=io_accesses;
    photo_display_probe_frame(101000); // Failed/pending freeze must not seed timing.
    assert(!probe.frame_seen && io_accesses==accesses);
    fake_time=102000;photo_display_probe_begin();
    accesses=io_accesses;
    photo_display_probe_frame(101999); // ISR timestamp acquired before begin's lock.
    assert(!probe.frame_seen && !probe.frames && io_accesses==accesses);
    photo_display_probe_frame(120000); // Leading 18ms partial interval is omitted.
    assert(probe.frame_seen && probe.frames==1 && !probe.frame_interval_max_us && !probe.frame_intervals_over_18ms);
    photo_display_probe_frame(138000);
    assert(probe.frame_interval_max_us==18000 && !probe.frame_intervals_over_18ms);
    photo_display_probe_frame(156001);
    assert(probe.frame_interval_max_us==18001 && probe.frame_intervals_over_18ms==1);
    fake_time=160000;photo_display_probe_begin(); // Duplicate begin must preserve baseline.
    photo_display_probe_frame(166001);
    assert(probe.frame_interval_max_us==18001 && probe.frame_intervals_over_18ms==1);
    fake_time=300000;photo_display_probe_end(); // Do not add the trailing partial interval.
    accesses=io_accesses;photo_display_probe_frame(400000);
    assert(probe.frame_interval_max_us==18001 && probe.frame_intervals_over_18ms==1 && io_accesses==accesses);
    photo_display_probe_format(text,sizeof(text));
    assert(!strcmp(text+strlen(text)-8,",18001,1"));

    fake_time=500000;photo_display_probe_begin(); // No CLICK: new window still resets.
    assert(!probe.frame_seen && !probe.frame_interval_max_us && !probe.frame_intervals_over_18ms);
    photo_display_probe_frame(500000); // Timestamp equal to begin is in the window.
    photo_display_probe_frame(515138);
    assert(probe.frame_interval_max_us==15138 && !probe.frame_intervals_over_18ms);
    fake_time=520000;photo_display_probe_end();
}
static void frame_interval_wrap_and_saturation(void) {
    fake_time=UINT32_MAX-19999;photo_display_probe_begin();
    photo_display_probe_frame(UINT32_MAX-20000); // Stale first sample near wrap.
    assert(!probe.frame_seen && !probe.frames);
    photo_display_probe_frame(UINT32_MAX-9999);
    photo_display_probe_frame(8000);
    assert(probe.frame_interval_max_us==18000 && !probe.frame_intervals_over_18ms);
    photo_display_probe_frame(26001);
    assert(probe.frame_interval_max_us==18001 && probe.frame_intervals_over_18ms==1);
    // Legacy frame-count wrap must not silently turn the next sample into a first frame.
    probe.frames=UINT32_MAX;probe.frame_intervals_over_18ms=UINT32_MAX-1;
    photo_display_probe_frame(46001);
    assert(!probe.frames && probe.frame_interval_max_us==20000 && probe.frame_intervals_over_18ms==UINT32_MAX);
    photo_display_probe_frame(67001);
    assert(probe.frames==1 && probe.frame_interval_max_us==21000 && probe.frame_intervals_over_18ms==UINT32_MAX);
    fake_time=70000;photo_display_probe_end();

    // A window begun before timer wrap also accepts its first sample after wrap.
    fake_time=UINT32_MAX-999;photo_display_probe_begin();
    photo_display_probe_frame(500);photo_display_probe_frame(19500);
    assert(probe.frames==2 && probe.frame_interval_max_us==19000 && probe.frame_intervals_over_18ms==1);
    fake_time=20000;photo_display_probe_end();
}
static void maximum_format(void) {
    // Maximize every printable field, including all ten accumulated durations.
    memset(&probe,0xff,sizeof(probe));probe.active=false;probe.pending=false;
    probe.fill_unchanged=true;probe.frame_seen=false;probe.stage=PHOTO_DISPLAY_PROBE_DONE;
    char text[350],small[8];unsigned accesses=io_accesses;
    photo_display_probe_format(text,sizeof(text));
    assert(strlen(text)==273 && strlen(text)<sizeof(text)-1);
    assert(strstr(text,",4294967,4294967,4294967,4294967,4294967,4294967,4294967,4294967,4294967,4294967"));
    assert(!strcmp(text+strlen(text)-22,",4294967295,4294967295"));
    unsigned fields=1;for(const char *p=text;*p;p++)if(*p==',')fields++;
    assert(fields==34);
    // Real producer's photo_diag[512] includes the worst old six-field prefix.
    char photo[512];int written=snprintf(photo,sizeof(photo),"-2147483648,-2147483648,4294967295,4294967295,4294967295,4294967295;%s",text);
    assert(written==341 && (size_t)written<sizeof(photo));
    struct {char text[273];char guard;} exact_short;
    memset(&exact_short,'?',sizeof(exact_short));photo_display_probe_format(exact_short.text,sizeof(exact_short.text));
    assert(exact_short.text[272]==0 && exact_short.guard=='?');
    memset(small,'?',sizeof(small));photo_display_probe_format(small,sizeof(small));
    assert(small[7]==0);photo_display_probe_format(NULL,0);
    assert(io_accesses==accesses && !locked);
    printf("PHOTO_DISPLAY_PROBE_PASS max_suffix=%zu photo_diag=%d; legacy field positions, scoped frame gaps, first/last boundaries, >18ms threshold, wrap/saturation, active-only hardware access, read-only register views, full hardware invariance, observed fill equality, masked RAW, baseline Host bits, repeated phases, ISR lock, format purity\n",strlen(text),written);
}
int main(void) {
    inactive_and_owned_window();repeated_attempts_and_wrap();external_fill_change_is_only_observed();
    scoped_frame_intervals();frame_interval_wrap_and_saturation();maximum_format();
    assert(isr_locks && task_locks && !locked);return 0;
}
