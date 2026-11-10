#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include <stdint.h>
#include <stdbool.h>
#include <stdlib.h>
#include <string.h>
#include "cJSON.h"
#include "ota_zlib_host.h"
#include "../../firmware/main/ota_stream.h"
typedef struct {uint8_t *bytes;int size,at,fragment;} input_stream;
static int read_bytes(void *context,uint8_t *out,int count) {
    input_stream *s=context;if(count>s->fragment)count=s->fragment;
    if(count>s->size-s->at)count=s->size-s->at;
    memcpy(out,s->bytes+s->at,count);s->at+=count;return count;
}
static uint8_t *load(const char *path,int *size) {
    FILE *f=fopen(path,"rb");assert(f);fseek(f,0,SEEK_END);*size=(int)ftell(f);rewind(f);
    uint8_t *bytes=malloc(*size);assert(bytes&&fread(bytes,1,*size,f)==(size_t)*size);fclose(f);return bytes;
}
static bool decode(uint8_t *wire,int size,const uint8_t *image,int length,int fragment) {
    input_stream s={wire,size,0,fragment};int64_t remaining=size;
    for(int offset=0;offset<length;offset+=49152) {
        int count=length-offset;if(count>49152)count=49152;
        uint8_t out[49152];int got=ota_stream_block(read_bytes,&s,out,count,offset,&remaining);
        if(got!=count||memcmp(out,image+offset,count))return false;
    }
    return remaining==0&&s.at==size;
}
int main(int argc,char **argv) {
    assert(argc==3);int size,length;uint8_t *wire=load(argv[1],&size),*image=load(argv[2],&length);
    for(int fragment=1;fragment<=32768;fragment*=32)assert(decode(wire,size,image,length,fragment));
    assert(!workspace);
    // Validate production framing against the .NET encoded image, including
    // exact offsets, full image bytes, fragmented reads and terminal boundary.
    assert(!decode(wire,size-1,image,length,137));
    uint8_t *copy=malloc(size+1);memcpy(copy,wire,size);copy[size]=0;
    assert(!decode(copy,size+1,image,length,137));
    for(int field=0;field<4;field++) {
        memcpy(copy,wire,size);memset(copy+field*4,0xff,4);
        assert(!decode(copy,size,image,length,137));
    }
    memcpy(copy,wire,size);copy[16]^=1;assert(!decode(copy,size,image,length,137));
    fail_workspace=true;assert(!decode(wire,size,image,length,137));fail_workspace=false;
    uint8_t raw[273],packet[289];for(unsigned i=0;i<sizeof(raw);i++)raw[i]=(uint8_t)(i*97);
    uint32_t header[4]={0,sizeof(raw),sizeof(raw),0};memcpy(packet,header,16);memcpy(packet+16,raw,sizeof(raw));
    assert(decode(packet,sizeof(packet),raw,sizeof(raw),1));
    header[2]--;memcpy(packet,header,16);assert(!decode(packet,sizeof(packet),raw,sizeof(raw),137));
    free(copy);free(wire);free(image);assert(!workspace);
    puts("OTA_STREAM_OK exact_dotnet_image fragmented_1_32_1024_32768_reads raw_fallback corrupt_truncated_trailing_bad_headers_allocation_failure");
}
