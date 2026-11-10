"""Compile the production asset-copy path with a controlled allocation failure.

The cache reclaimer is stubbed; real LVGL cache preservation is validated by the
UI preview target. Hardware confirmation is recorded separately.
"""
import argparse, subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);p.add_argument('--cc',default='cc');p.add_argument('--json-include',type=Path,required=True);p.add_argument('--baseline',action='store_true');args=p.parse_args()
root=Path(__file__).resolve().parents[1];source=(root/'firmware/main/assets.c').read_text()
slots=source[source.index('typedef struct {'):source.index('// Two alternating copies')]
start=source.find('volatile unsigned tab5_asset_copy_failures')
body=source[start if start>=0 else source.index('bool assets_copy('):source.index('void assets_ids(')]
header='''#include "assets.h"
#define ASSET_CONTROL_SMALL
#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
static unsigned attempts,trims;
static bool available,evictable;
static uint8_t pixels[334*336*2];
static void *limited_malloc(size_t bytes){attempts++;return available?malloc(bytes):NULL;}
unsigned tab5_draw_cache_trim_hidden(unsigned bytes){trims++;if(!evictable)return 0;available=true;return bytes;}
#define malloc limited_malloc
'''
test='''
#undef malloc
int main(void) {
    slots[2].ready=(asset_image){.pixels=pixels,.width=334,.height=336,.frames=1,.revision=7,.crc=0x12345678};
    memset(pixels,0x5a,sizeof(pixels));
    assert(assets_progress(2)==100 && (assets_ready()&4));
    asset_image copied={0};evictable=true;
    bool result=assets_copy(2,0,&copied);
#ifdef BASELINE
    assert(!result && attempts==1 && trims==0 && slots[2].ready.pixels==pixels);
    puts("COVER_DISPLAY_BASELINE_REPRODUCED received=100 ready=1 copy_failed=1 no_reclaim=1");return 0;
#else
    assert(result&&attempts==2&&trims==1&&copied.pixels!=pixels);
    assert(tab5_asset_copy_failures==1&&tab5_asset_copy_recoveries==1&&tab5_asset_copy_reclaimed==sizeof(pixels));
    assert(copied.crc==slots[2].ready.crc&&copied.revision==7&&!memcmp(copied.pixels,pixels,sizeof(pixels)));
    free(copied.pixels);attempts=trims=0;
    assert(!assets_copy(2,7,&copied)&&attempts==0&&trims==0);
    assert(!assets_copy(-1,0,&copied)&&!assets_copy(3,0,&copied)&&attempts==0&&trims==0);
    available=true;assert(assets_copy(2,0,&copied)&&attempts==1&&trims==0);free(copied.pixels);
    attempts=trims=0;available=evictable=false;copied=(asset_image){.pixels=(void*)1,.crc=99};
    assert(!assets_copy(2,0,&copied)&&attempts<=2&&trims==1);
    assert(copied.pixels==(void*)1&&copied.crc==99&&slots[2].ready.pixels==pixels&&slots[2].ready.revision==7);
    assert(tab5_asset_copy_failures==2&&tab5_asset_copy_recoveries==1&&tab5_asset_copy_reclaimed==sizeof(pixels));
    puts("COVER_DISPLAY_MEMORY_OK reclaim retry; exact pixels/revision; no eviction when allocation succeeds; bounded failure preserves both images");return 0;
#endif
}
'''
args.output.mkdir(parents=True,exist_ok=True);c=args.output/'cover_display_memory_test.c';c.write_text(header+slots+body+test)
exe=args.output/'cover_display_memory_test'
command=[args.cc,'-std=c11','-Wall','-Wextra','-fsanitize=undefined','-I'+str(root/'firmware/main'),'-I'+str(args.json_include),str(c),'-o',str(exe)]
if args.baseline:command.insert(1,'-DBASELINE')
subprocess.run(command,check=True);subprocess.run([str(exe.resolve())],check=True,timeout=20)
