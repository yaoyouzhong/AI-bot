"""Production receive/CRC/RLE/copy regression with a bounded heap.

Transport base64 and the hardware JPEG engine are doubles. JPEG workspace sizes
follow the production allocator; this tests ownership/budget, not hardware decode.
Run the real LVGL preview separately to validate cache admission and reclamation.
"""
from pathlib import Path
import argparse, subprocess
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);p.add_argument('--json-include',type=Path,required=True);args=p.parse_args()
r=Path(__file__).resolve().parents[1];s=(r/'firmware/main/assets.c').read_text()
slots=s[s.index('typedef struct {'):s.index('// Two alternating copies')]
helpers=s[s.index('static int number('):s.index('static bool decode_jpeg(')]
receive=s[s.index('volatile unsigned tab5_asset_receive_alloc_failures'):s.index('unsigned assets_cached(')]
header=r'''
#include "assets.h"
#define ASSET_CONTROL_SMALL
#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stddef.h>
static size_t budget,used,peak,fail_size;
typedef union { max_align_t aligned; size_t size; } allocation;
static void *bounded_malloc(size_t size) {
    if(size==fail_size||used>budget||size>budget-used)return NULL;
    allocation *a=malloc(sizeof(*a)+size);assert(a);a->size=size;used+=size;
    if(used>peak)peak=used;
    return a+1;
}
static void bounded_free(void *p) {if(p){allocation *a=(allocation*)p-1;assert(used>=a->size);used-=a->size;free(a);}}
static const uint8_t *wire;static size_t wire_size;
static int mbedtls_base64_decode(uint8_t *out,size_t cap,size_t *len,const uint8_t *text,size_t n) {
    (void)text;(void)n;if(wire_size>cap)return -1;memcpy(out,wire,wire_size);*len=wire_size;return 0;
}
unsigned tab5_draw_cache_trim_hidden(unsigned bytes){(void)bytes;return 0;}
#define malloc bounded_malloc
#define free bounded_free
'''
mock=r'''
static void cache_save(int slot,const asset_slot *s){(void)slot;(void)s;}
static bool decode_jpeg(const uint8_t *packed,size_t size,uint8_t *pixels,int w,int h) {
    (void)packed;
    void *input=malloc(size),*output=malloc(((w+15)&~15)*((h+15)&~15)*2);
    bool ok=input&&output;if(ok)memset(pixels,0x6b,w*h*2);
    free(input);free(output);return ok;
}
'''
test=r'''
#undef malloc
#undef free
static bool send_image(uint8_t *data,unsigned size,int w,int h,bool jpeg,bool corrupt) {
    char id[9];snprintf(id,sizeof(id),"%08x",asset_crc32(data,size)^(corrupt?1:0));
    bool accepted=true;
    for(unsigned offset=0;offset<size;offset+=1024){
        cJSON *c=cJSON_CreateObject();assert(c);
        cJSON_AddNumberToObject(c,"slot",2);cJSON_AddNumberToObject(c,"total",size);
        cJSON_AddNumberToObject(c,"offset",offset);cJSON_AddNumberToObject(c,"width",w);
        cJSON_AddNumberToObject(c,"height",h);cJSON_AddNumberToObject(c,"frames",1);
        cJSON_AddStringToObject(c,"id",id);cJSON_AddStringToObject(c,"bytes","fixture");
        cJSON_AddStringToObject(c,"encoding",jpeg?"jpeg":"rle565");
        cJSON *delays=cJSON_AddArrayToObject(c,"delays");cJSON_AddItemToArray(delays,cJSON_CreateNumber(1000));
        wire=data+offset;wire_size=size-offset>1024?1024:size-offset;
        accepted=assets_receive(c)&&accepted;cJSON_Delete(c);
    }
    return accepted;
}
static void reset(void){for(int i=0;i<3;i++){bounded_free(slots[i].packed);bounded_free(slots[i].ready.pixels);}memset(slots,0,sizeof(slots));assert(!used);fail_size=peak=0;}
int main(void) {
    // Worst RLE cover: every pixel has its own run (451584 bytes).
    const unsigned pixels=336u*336u,packed=pixels*4;
    uint8_t *rle=malloc(packed);assert(rle);
    for(unsigned i=0;i<pixels;i++){rle[i*4]=1;rle[i*4+1]=0;rle[i*4+2]=i;rle[i*4+3]=i>>8;}
    budget=170000;assert(!send_image(rle,packed,336,336,false,false));
    assert(tab5_asset_receive_alloc_failures>0&&!slots[2].ready.pixels&&!slots[2].packed);
    budget=3u*1024u*1024u;
    assert(send_image(rle,packed,336,336,false,false));
    asset_image ui={0};assert(assets_copy(2,0,&ui));
    for(unsigned i=0;i<pixels;i++)assert(ui.pixels[i*2]==(uint8_t)i&&ui.pixels[i*2+1]==(uint8_t)(i>>8));
    assert(assets_progress(2)==100&&!assets_pending());
    uint8_t *old=slots[2].ready.pixels;unsigned revision=slots[2].ready.revision;
    rle[2]^=1;fail_size=pixels*2;
    assert(!send_image(rle,packed,336,336,false,false));
    assert(slots[2].ready.pixels==old&&slots[2].ready.revision==revision);
    fail_size=0;assert(!send_image(rle,packed,336,336,false,true));assert(tab5_asset_crc_failures==1);
    rle[0]=0;assert(!send_image(rle,packed,336,336,false,false));assert(tab5_asset_decode_failures==1);
    rle[0]=1;assert(send_image(rle,packed,336,336,false,false));
    assert(slots[2].ready.revision==revision+1);
    bounded_free(ui.pixels);reset();
    // Two retained wide JPEG pictures plus packed input, padded DMA output,
    // decoded replacement and new display copy all fit the reserved budget.
    uint8_t *jpeg=calloc(1,524288);assert(jpeg);
    assert(send_image(jpeg,524288,560,336,true,false));
    assert(assets_copy(2,0,&ui));old=slots[2].ready.pixels;revision=slots[2].ready.revision;
    jpeg[0]=1;assert(send_image(jpeg,524288,560,336,true,false));
    asset_image next={0};assert(assets_copy(2,revision,&next));
    assert(next.revision==revision+1&&next.pixels[0]==0x6b&&peak<=budget);
    printf("COVER_RECEIVE_MEMORY_OK low-memory reproduction; max RLE exact pixels; max JPEG workspace peak=%zu/3145728; CRC/decode/allocation failures preserve prior ready; bounded recovery\n",peak);
    bounded_free(ui.pixels);bounded_free(next.pixels);reset();free(jpeg);free(rle);return 0;
}
'''
args.output.mkdir(parents=True,exist_ok=True);c=args.output/'cover_receive_memory_test.c';c.write_text(header+slots+helpers+mock+receive+test)
exe=args.output/'cover_receive_memory_test'
subprocess.run(['cc','-std=c11','-Wall','-Wextra','-fsanitize=undefined','-I'+str(r/'firmware/main'),'-I'+str(args.json_include),str(c),str(r/'firmware/main/asset_codec.c'),str(args.json_include/'cJSON.c'),'-o',str(exe)],check=True)
subprocess.run([str(exe.resolve())],check=True,timeout=30)
