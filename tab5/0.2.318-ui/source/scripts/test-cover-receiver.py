"""Exercise the production chunk receiver and padded RGB565 copy with fake DMA.

The hardware JPEG decoder is mocked; real decoder/display acceptance stays open.
Compile the generated C file with cJSON.c and asset_codec.c on Windows.
"""
import argparse,re
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
root=Path(__file__).resolve().parents[1];s=(root/'firmware/main/assets.c').read_text(encoding='utf-8')
slot=s[s.index('typedef struct {'):s.index('// Two alternating copies')]
body=s[s.index('static int number('):s.index('bool assets_copy(')]
header=r'''
#include "assets.h"
#include <assert.h>
#include <windows.h>
#include <wincrypt.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#define ESP_OK 0
#define JPEG_DEC_ALLOC_INPUT_BUFFER 0
#define JPEG_DEC_ALLOC_OUTPUT_BUFFER 1
#define JPEG_DECODE_OUT_FORMAT_RGB565 0
#define JPEG_DEC_RGB_ELEMENT_ORDER_BGR 0
#define JPEG_YUV_RGB_CONV_STD_BT601 0
typedef void *jpeg_decoder_handle_t;
typedef struct {int buffer_direction;} jpeg_decode_memory_alloc_cfg_t;
typedef struct {int timeout_ms;} jpeg_decode_engine_cfg_t;
typedef struct {int output_format,rgb_order,conv_std;} jpeg_decode_cfg_t;
typedef struct {int width,height;} jpeg_decode_picture_info_t;
static bool fail_decode;
static uint8_t *jpeg_alloc_decoder_mem(size_t n,const jpeg_decode_memory_alloc_cfg_t *cfg,size_t *capacity){(void)cfg;*capacity=n;return calloc(n,1);}
static int jpeg_decoder_get_info(const uint8_t *p,size_t n,jpeg_decode_picture_info_t *info){if(n<16)return -1;info->width=p[2]|p[3]<<8;info->height=p[4]|p[5]<<8;return 0;}
static int jpeg_new_decoder_engine(const jpeg_decode_engine_cfg_t *cfg,jpeg_decoder_handle_t *engine){assert(cfg->timeout_ms==500);*engine=(void*)1;return 0;}
static int jpeg_del_decoder_engine(jpeg_decoder_handle_t engine){(void)engine;return 0;}
static int jpeg_decoder_process(jpeg_decoder_handle_t engine,const jpeg_decode_cfg_t *cfg,const uint8_t *p,size_t n,uint8_t *out,size_t capacity,uint32_t *written){
    (void)engine;(void)n;assert(cfg->output_format==JPEG_DECODE_OUT_FORMAT_RGB565);if(fail_decode)return -1;
    int w=p[2]|p[3]<<8,h=p[4]|p[5]<<8,stride=(w+15)&~15,rows=(h+15)&~15;assert(capacity>=(size_t)stride*rows*2);
    for(int y=0;y<rows;y++)for(int x=0;x<stride;x++){uint16_t color=x<w&&y<h?(uint16_t)(y*w+x):0xffff;out[(y*stride+x)*2]=(uint8_t)color;out[(y*stride+x)*2+1]=(uint8_t)(color>>8);}
    *written=stride*rows*2;return 0;
}
static int mbedtls_base64_decode(uint8_t *out,size_t capacity,size_t *used,const uint8_t *input,size_t n){DWORD size=(DWORD)capacity;if(!CryptStringToBinaryA((const char*)input,(DWORD)n,CRYPT_STRING_BASE64|CRYPT_STRING_STRICT,out,&size,NULL,NULL))return -1;*used=size;return 0;}
'''
cache='static void cache_save(int slot,const asset_slot *s){(void)slot;(void)s;}\n'
test=r'''
static cJSON *chunk(const uint8_t *packed,int total,int offset,int width,int height,int slot,const char *encoding){
    int n=total-offset;if(n>1024)n=1024;DWORD size=0;CryptBinaryToStringA(packed+offset,n,CRYPT_STRING_BASE64|CRYPT_STRING_NOCRLF,NULL,&size);char *text=calloc(size,1);assert(text);CryptBinaryToStringA(packed+offset,n,CRYPT_STRING_BASE64|CRYPT_STRING_NOCRLF,text,&size);
    char id[9];snprintf(id,sizeof(id),"%08x",(unsigned)asset_crc32(packed,total));cJSON *c=cJSON_CreateObject();
    cJSON_AddNumberToObject(c,"slot",slot);cJSON_AddNumberToObject(c,"width",width);cJSON_AddNumberToObject(c,"height",height);cJSON_AddNumberToObject(c,"frames",1);cJSON *delays=cJSON_AddArrayToObject(c,"delays");cJSON_AddItemToArray(delays,cJSON_CreateNumber(1000));
    cJSON_AddStringToObject(c,"id",id);cJSON_AddStringToObject(c,"encoding",encoding);cJSON_AddNumberToObject(c,"offset",offset);cJSON_AddNumberToObject(c,"total",total);cJSON_AddStringToObject(c,"bytes",text);free(text);return c;
}
int main(void){
    _set_error_mode(_OUT_TO_STDERR);_set_abort_behavior(0,_WRITE_ABORT_MSG|_CALL_REPORTFAULT);
    uint8_t packed[2060]={0xff,0xd8,0x2f,0x02,0x3b,0x01}; // 559x315: exercise both padded dimensions.
    for(int i=6;i<2060;i++)packed[i]=(uint8_t)i;
    for(int at=2048;at>=0;at-=1024){cJSON *c=chunk(packed,2060,at,559,315,2,"jpeg");assert(assets_receive(c));cJSON_Delete(c);}
    assert(slots[2].ready.width==559&&slots[2].ready.height==315&&slots[2].ready.revision==1);
    for(int y=0;y<315;y++)for(int x=0;x<559;x++){const uint8_t *p=slots[2].ready.pixels+(y*559+x)*2;assert((uint16_t)(p[0]|p[1]<<8)==(uint16_t)(y*559+x));}
    uint8_t *before=slots[2].ready.pixels;uint32_t crc=slots[2].ready_crc;
    cJSON *c=chunk(packed,2060,0,559,315,0,"jpeg");assert(!assets_receive(c));cJSON_Delete(c);
    c=chunk(packed,2060,0,561,315,2,"jpeg");assert(!assets_receive(c));cJSON_Delete(c);
    c=chunk(packed,2060,0,559,337,2,"jpeg");assert(!assets_receive(c));cJSON_Delete(c);
    c=chunk(packed,2060,0,559,315,2,"unknown");assert(!assets_receive(c));cJSON_Delete(c);
    packed[10]++;fail_decode=true;
    for(int at=0;at<2060;at+=1024){c=chunk(packed,2060,at,559,315,2,"jpeg");assert(assets_receive(c)==(at!=2048));cJSON_Delete(c);}
    assert(slots[2].ready.pixels==before&&slots[2].ready_crc==crc&&!slots[2].packed);
    fail_decode=false;
    for(int at=0;at<2060;at+=1024){c=chunk(packed,2060,at,559,315,2,"jpeg");cJSON_ReplaceItemInObject(c,"id",cJSON_CreateString("00000000"));assert(assets_receive(c)==(at!=2048));cJSON_Delete(c);}
    assert(slots[2].ready.pixels==before&&slots[2].ready_crc==crc&&!slots[2].packed);
    free(before);puts("COVER_RECEIVER_OK out-of-order fragments/padded RGB565 rows/CRC/bounds/codec-failure retains previous image; hardware decoder mocked");
}
'''
a.output.mkdir(parents=True,exist_ok=True);(a.output/'cover_receiver_test.c').write_text(header+slot+cache+body+test,encoding='utf-8')
print(a.output/'cover_receiver_test.c')
