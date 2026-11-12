#!/usr/bin/env python3
"""Run pinned TAB5 scanout and copy-hook code against host register doubles."""
import argparse
import os
from pathlib import Path
import re
import shlex
import subprocess
import tempfile


DPI = Path("components/esp_lcd/dsi/esp_lcd_panel_dpi.c")


def read_function(path, name, next_marker):
    source = path.read_text()
    start = source.index(name)
    return source[start:source.index(next_marker, start)]


DRIVER = r"""
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stddef.h>
#define unlikely(x) (x)
#define MIPI_DSI_LL_EVENT_UNDERRUN 1u
typedef void *dw_gdma_channel_handle_t;
typedef void *dw_gdma_link_list_handle_t;
typedef int dw_gdma_trans_done_event_data_t;
typedef struct { bool is_valid, is_last; } dw_gdma_block_markers_t;
typedef struct { void *bridge; } mipi_dsi_hal_context_t;
typedef struct { mipi_dsi_hal_context_t hal; } bus_t;
typedef struct esp_lcd_dpi_panel_t {
    bus_t *bus;
    uint8_t cur_fb_index;
    dw_gdma_link_list_handle_t link_lists[2];
    int base;
    void *user_ctx;
    bool (*on_refresh_done)(int *, void *, void *);
} esp_lcd_dpi_panel_t;
static unsigned sticky, clears, ticks, faults, warnings, enables, step, first_log, first_enable;
static void *bound;
static uint32_t mipi_dsi_brg_ll_get_interrupt_status(void *bridge) { return sticky; }
static void mipi_dsi_brg_ll_clear_interrupt_status(void *bridge, uint32_t mask) {
    assert(mask == sticky); sticky &= ~mask; clears++;
}
#define ESP_DRAM_LOGE(tag, message) do { warnings++; first_log=++step; } while(0)
static void *dw_gdma_link_list_get_item(void *list, int i) { assert(i==0); return list; }
static void dw_gdma_lli_set_block_markers(void *item, dw_gdma_block_markers_t markers) {
    assert(item && markers.is_valid && markers.is_last);
}
static void dw_gdma_channel_use_link_list(void *channel, void *list) { bound=list; }
static void dw_gdma_channel_enable_ctrl(void *channel, bool on) {
    assert(on && bound); enables++; first_enable=++step;
}
static bool refresh(int *base, void *event, void *user) { assert(enables); return true; }
"""

DRIVER_MAIN = r"""
void tab5_lcd_frame_tick(void) { ticks++; }
void tab5_lcd_underrun(void) { assert(!sticky); faults++; }
int main(void) {
    bus_t bus = {0};
    esp_lcd_dpi_panel_t panel = {
        .bus=&bus, .cur_fb_index=1, .link_lists={0,(void*)1}, .on_refresh_done=refresh
    };
    sticky=MIPI_DSI_LL_EVENT_UNDERRUN;
    assert(dma_trans_done_cb(0,0,&panel));
    assert(bound==panel.link_lists[1] && enables==1 && clears==1 && ticks==1 && faults==1);
#if EXPECT_R5_LOG
    assert(warnings==1 && first_log<first_enable);
#else
    assert(warnings==0 && first_log==0 && first_enable==1);
#endif
    assert(dma_trans_done_cb(0,0,&panel));
    assert(enables==2 && ticks==2 && clears==2 && faults==1);
    return 0;
}
"""

DIAGNOSTIC = r"""
#include <assert.h>
#include <stdint.h>
#define IRAM_ATTR
struct { struct { unsigned underrun_int_raw; } int_raw; } MIPI_DSI_BRIDGE;
static uint64_t clock_us;
static uint64_t esp_timer_get_time(void) { return clock_us; }
static unsigned crash_record_runtime_display_stages(void) { return 32; }
volatile int tab5_ui_page, tab5_saver_active;
"""

DIAGNOSTIC_MAIN = r"""
int main(void) {
    frame_count=1;
    tab5_lcd_underrun();
    assert(!underrun_copy_valid);

    frame_count=120; clock_us=UINT32_MAX-100;
    MIPI_DSI_BRIDGE.int_raw.underrun_int_raw=1;
    tab5_lcd_copy_begin(40352);
    assert(MIPI_DSI_BRIDGE.int_raw.underrun_int_raw==1);
    clock_us+=4254; frame_count++;
    MIPI_DSI_BRIDGE.int_raw.underrun_int_raw=0; // driver reads and clears
    tab5_lcd_underrun();
    assert(underrun_copy_valid && underrun_copy_active);
    assert(underrun_copy_start_age==4254 && underrun_copy_pixels==40352);
    assert(underrun_begin_latch==1 && underrun_begin_frame==120 && underrun_read_frame==121);

    tab5_lcd_copy_end();
    frame_count=200; clock_us+=1000;
    tab5_lcd_copy_begin(28000);
    assert(copy_end_latch==2 && copy_end_frame==0);
    assert(copy_begin_latch==0);
    MIPI_DSI_BRIDGE.int_raw.underrun_int_raw=1;
    clock_us+=700; tab5_lcd_copy_end();
    assert(MIPI_DSI_BRIDGE.int_raw.underrun_int_raw==1);
    frame_count++; clock_us+=50;
    MIPI_DSI_BRIDGE.int_raw.underrun_int_raw=0;
    tab5_lcd_underrun();
    assert(underrun_copy_valid && !underrun_copy_active);
    assert(underrun_begin_latch==0 && underrun_end_latch==1 && underrun_end_frame==200);
    assert(underrun_copy_start_age==750 && underrun_copy_end_age==50);
    copy_sequence++; // simulate interrupted publication
    tab5_lcd_underrun();
    assert(!underrun_copy_valid);
    return 0;
}
"""


def run(cc, directory, name, source, *flags):
    c_file = directory / f"{name}.c"
    executable = directory / name
    c_file.write_text(source)
    subprocess.run([*cc, "-std=c11", "-Wall", "-Wextra", "-Werror", "-Wno-unused-parameter",
                    *flags, str(c_file), "-o", str(executable)], check=True)
    subprocess.run([str(executable)], check=True)
    print(f"{name}: passed")


def main():
    args = argparse.ArgumentParser(description=__doc__)
    args.add_argument("--r5-sdk", type=Path, required=True)
    args.add_argument("--r6-sdk", type=Path, required=True)
    args.add_argument("--diagnostics", type=Path, required=True)
    options = args.parse_args()
    cc = shlex.split(os.getenv("CC", "cc"))
    with tempfile.TemporaryDirectory(prefix="tab5-scanout-") as tmp:
        directory = Path(tmp)
        for sdk, label, expects_log in ((options.r5_sdk, "r5_uart_precedes_rearm", 1),
                                        (options.r6_sdk, "r6_rearms_without_uart", 0)):
            body = read_function(sdk / DPI, "static bool dma_trans_done_cb(",
                                 "// Please note, errors happened")
            run(cc, directory, label, DRIVER + body + DRIVER_MAIN, f"-DEXPECT_R5_LOG={expects_log}")
        # Keep the real recorder and drop only unrelated declarations following it.
        source = options.diagnostics.read_text()
        start = source.index("volatile unsigned tab5_lcd_underruns;")
        end = source.index("\n}", source.index("void IRAM_ATTR tab5_lcd_underrun")) + 2
        diagnostics = source[start:end]
        diagnostics, fences = re.subn(
            r'__asm__ volatile\("fence rw,rw" ::: "memory"\)', "((void)0)", diagnostics)
        if not fences:
            raise RuntimeError("expected RISC-V fences in the production recorder")
        run(cc, directory, "r6_copy_window", DIAGNOSTIC + diagnostics + DIAGNOSTIC_MAIN,
            "-Wno-unused-variable")
        usb = read_function(options.diagnostics.with_name("usb_link.c"),
                            "static size_t append_display_diagnostic_suffix(", "static void write_reply(")
        run(cc, directory, "r6_legacy_reply_capacity",
            "#include <assert.h>\n#include <stddef.h>\n#include <string.h>\n" + usb + r"""
int main(void) {
    const char *legacy="@AIBOT {\"displayDiag\":\"legacy;op,1\",\"hostNonce\":\"retained\"}\n";
    const char *suffix=";copywindow,1,1,1,0,0,0,2";
    char frame[160];
    strcpy(frame,legacy);
    size_t size=strlen(frame);
    assert(append_display_diagnostic_suffix(frame,size+strlen(suffix),size,suffix)==size);
    assert(!strcmp(frame,legacy));
    assert(append_display_diagnostic_suffix(frame,sizeof(frame),size,suffix)==size+strlen(suffix));
    assert(!strcmp(frame,"@AIBOT {\"displayDiag\":\"legacy;op,1;copywindow,1,1,1,0,0,0,2\",\"hostNonce\":\"retained\"}\n"));
    return 0;
}
""")


if __name__ == "__main__":
    main()
