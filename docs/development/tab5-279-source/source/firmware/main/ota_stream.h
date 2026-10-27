#pragma once
#include "ota_zlib.h"

typedef int (*ota_stream_reader)(void *context,uint8_t *out,int count);
static uint32_t ota_stream_u32(const uint8_t *p) {
    return (uint32_t)p[0]|((uint32_t)p[1]<<8)|((uint32_t)p[2]<<16)|((uint32_t)p[3]<<24);
}
static bool ota_stream_exact(ota_stream_reader read,void *context,uint8_t *out,int count,int64_t *remaining) {
    if(count<1||count>*remaining)return false;
    for(int at=0;at<count;) {
        int n=read(context,out+at,count-at);
        if(n<=0||n>count-at)return false;
        at+=n;*remaining-=n;
    }
    return true;
}
static int ota_stream_block(ota_stream_reader read,void *context,uint8_t *out,int count,int offset,int64_t *remaining) {
    uint8_t header[16];
    if(!out||count<1||count>49152||offset<0||!ota_stream_exact(read,context,header,16,remaining))return -1;
    uint32_t at=ota_stream_u32(header),decoded=ota_stream_u32(header+4),encoded=ota_stream_u32(header+8),encoding=ota_stream_u32(header+12);
    if(at!=(uint32_t)offset||decoded!=(uint32_t)count||!encoded||encoded>(uint32_t)count||encoding>1||
        (encoding==0&&encoded!=decoded)||(encoding==1&&encoded>=decoded))return -1;
    if(!encoding)return ota_stream_exact(read,context,out,count,remaining)?count:-1;
    uint8_t *packed=heap_caps_malloc(encoded,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    if(!packed)return -1;
    bool ok=ota_stream_exact(read,context,packed,(int)encoded,remaining)&&ota_zlib_decode(packed,encoded,out,count);
    free(packed);return ok?count:-1;
}
