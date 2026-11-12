#!/usr/bin/env python3
"""Exercise the TAB5 pre-underrun diagnostic against the actual candidate C."""
import argparse
import os
from pathlib import Path
import re
import shlex
import tempfile

from test_tab5_scanout_rearm import DPI, DIAGNOSTIC, DRIVER, DRIVER_MAIN, read_function, run


RECORDER_MAIN = r"""
static void at(unsigned us, unsigned latch) {
    clock_us=us;
    MIPI_DSI_BRIDGE.int_raw.underrun_int_raw=latch;
}
static void capture(unsigned now) {
    frame_count++;
    at(now,0); // the scan driver has read and cleared the latch
    tab5_lcd_underrun();
    tab5_lcd_capture_before();
}
static void fault_during(unsigned stage, const char *expected) {
    const unsigned t=1916637061u;
    frame_count=125265;
    at(t-7000,0); tab5_lcd_copy_begin(40000);
    at(t-6200,0); tab5_lcd_render_boundary(false);
    at(t-4500,stage==1); tab5_lcd_copy_end();
    at(t-3900,stage==1 || stage==2); tab5_lcd_render_boundary(true);
    at(t-3400,stage==1 || stage==2); tab5_lcd_rotate_boundary(false);
    at(t-2200,stage!=4); tab5_lcd_rotate_boundary(true);
    at(t-1900,stage!=4); tab5_lcd_submit_boundary(false);
    at(t-1808,1); tab5_lcd_copy_begin(18720);
    at(t-1740,1); tab5_lcd_submit_boundary(true);
    capture(t);
    assert(underrun_copy_start_age==1808 && underrun_copy_end_age==4500);
    assert(underrun_begin_latch==1 && underrun_begin_frame==125265 && underrun_read_frame==125266);
    char out[176]={0};
    format_before(out,sizeof(out),tab5_lcd_underruns);
    assert(!strcmp(out,expected));
    out[0]=0;
    format_before(out,sizeof(out),tab5_lcd_underruns-1);
    assert(!out[0]); // never pair a history snapshot with a different fault
    format_before(out,20,tab5_lcd_underruns);
    assert(!out[0]); // no partial record
    copy_history=(tab5_history){0};
    ui_history=(tab5_history){0};
}
int main(void) {
    fault_during(1,";before,1,1,5,1,1808,9,1,4500,4,1,7000,33,1,1740,29,1,1900,25,1,2200,21,1,3400,17,1,3900,12,1,6200");
    fault_during(2,";before,1,2,5,1,1808,8,1,4500,4,1,7000,33,1,1740,29,1,1900,25,1,2200,21,1,3400,17,1,3900,12,1,6200");
    fault_during(3,";before,1,3,5,1,1808,8,1,4500,4,1,7000,33,1,1740,29,1,1900,25,1,2200,20,1,3400,16,1,3900,12,1,6200");
    fault_during(4,";before,1,4,5,1,1808,8,1,4500,4,1,7000,33,1,1740,28,1,1900,24,1,2200,20,1,3400,16,1,3900,12,1,6200");
    tab5_history ring={0};
    tab5_history_event out[8];
    tab5_history_mark(&ring,1,UINT32_MAX,1,UINT32_MAX-20);
    tab5_history_mark(&ring,2,0,0,11); // after fault
    tab5_history_mark(&ring,3,0,0,9);
    ring.slots[2].ticket=0; // writer interrupted before publishing
    tab5_history_mark(&ring,4,0,0,10u-50001u); // too old by time
    tab5_history_mark(&ring,5,0u-9u,0,8); // too old by frame
    assert(tab5_history_collect(&ring,10,0,out,8)==1);
    assert(out[0].tag==5 && out[0].frame_age==1 && out[0].age_us==31);
    for(unsigned i=0;i<12;i++)tab5_history_mark(&ring,6,0,2,20+i);
    assert(tab5_history_collect(&ring,40,0,out,4)==4);
    assert(out[0].tag==26 && out[0].age_us==9 && out[3].age_us==12);
    assert(tab5_history_collect(&ring,40,0,out,8)==8 && out[7].age_us==16);
    return 0;
}
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sdk", required=True, type=Path)
    parser.add_argument("--source", required=True, type=Path)
    options = parser.parse_args()
    cc = shlex.split(os.getenv("CC", "cc"))
    path = (options.source / "firmware/main").resolve()
    diagnostics = path / "display_diagnostics.c"
    recorder = read_function(diagnostics, "volatile unsigned tab5_lcd_underruns;",
                             "static volatile uint32_t frame_gap_count")
    recorder, count = re.subn(r'__asm__ volatile\("fence rw,rw" ::: "memory"\)', "((void)0)", recorder)
    if not count:
        raise RuntimeError("expected RISC-V fences in the production recorder")
    formatter = read_function(diagnostics, "static void format_before(", "void display_diagnostics_format(")
    driver = read_function(options.sdk / DPI, "static bool dma_trans_done_cb(",
                           "// Please note, errors happened")
    driver_test = DRIVER_MAIN.replace(
        "int main(void) {", """
void tab5_lcd_capture_before(void) {
    assert(enables==1 && faults==1 && !sticky);
    captures++;
}
int main(void) {""").replace("return 0;", "assert(captures==1); return 0;")
    driver_mock = DRIVER.replace("static bool refresh", "static unsigned captures;\nstatic bool refresh")
    driver_mock = driver_mock.replace("assert(enables); return true;", "assert(enables && captures==1); return true;")
    usb = read_function(path / "usb_link.c", "static size_t append_display_diagnostic_suffix(",
                        "static void write_reply(")
    with tempfile.TemporaryDirectory(prefix="tab5-scanout-history-") as tmp:
        directory = Path(tmp)
        run(cc, directory, "r7_rearm_before_history", driver_mock + driver + driver_test, "-DEXPECT_R5_LOG=0")
        run(cc, directory, "r7_distinguishes_copy_render_rotation_and_submit",
            '#include "scanout_history.h"\n#include <stdio.h>\n#include <string.h>\n'
            + DIAGNOSTIC + recorder + formatter + RECORDER_MAIN, "-I", str(path), "-Wno-unused-variable")
        run(cc, directory, "r7_optional_history_preserves_legacy_reply",
            "#include <assert.h>\n#include <stddef.h>\n#include <string.h>\n" + usb + r"""
int main(void) {
    const char *legacy="@AIBOT {\"displayDiag\":\"legacy;copywindow,1,1\",\"hostNonce\":\"retained\"}\n";
    const char *suffix=";before,1,1,5,1,1808";
    char frame[160];
    strcpy(frame,legacy);
    size_t size=strlen(frame);
    assert(append_display_diagnostic_suffix(frame,size+strlen(suffix),size,suffix)==size);
    assert(!strcmp(frame,legacy));
    assert(append_display_diagnostic_suffix(frame,sizeof(frame),size,suffix)==size+strlen(suffix));
    assert(!strcmp(frame,"@AIBOT {\"displayDiag\":\"legacy;copywindow,1,1;before,1,1,5,1,1808\",\"hostNonce\":\"retained\"}\n"));
    return 0;
}
""")


if __name__ == "__main__":
    main()
