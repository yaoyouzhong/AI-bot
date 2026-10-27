// Production range checks with a host zlib adapter. The device uses P4 ROM
// miniz; this is wire-format and lifecycle evidence, not ROM speed evidence.
#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include <stdint.h>
#include <stdbool.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>
#include "cJSON.h"
#include "ota_zlib_host.h"
static uint8_t *read_file(const char *dir,const char *name,const char *ext,size_t *size) {
    char path[2048];snprintf(path,sizeof(path),"%s/%s%s",dir,name,ext);FILE *f=fopen(path,"rb");assert(f);
    fseek(f,0,SEEK_END);long n=ftell(f);assert(n>=0);rewind(f);uint8_t *data=calloc((size_t)n+2,1);assert(data);
    assert(fread(data,1,n,f)==(size_t)n);fclose(f);*size=n;return data;
}
int main(int argc,char **argv) {
    assert(argc==2);size_t n;uint8_t *index=read_file(argv[1],"vectors",".json",&n);cJSON *vectors=cJSON_ParseWithLength((char*)index,n);free(index);assert(cJSON_IsArray(vectors));
    unsigned good=0,bad=0,zlib=0;uint64_t bytes=0;
    cJSON *v; cJSON_ArrayForEach(v,vectors) {
        const char *name=cJSON_GetObjectItemCaseSensitive(v,"name")->valuestring;
        int offset=cJSON_GetObjectItemCaseSensitive(v,"offset")->valueint;
        size_t raw_n,wire_n,meta_n;uint8_t *raw=read_file(argv[1],name,".raw",&raw_n),*wire=read_file(argv[1],name,".wire",&wire_n),*meta=read_file(argv[1],name,".json",&meta_n);
        cJSON *reply=cJSON_ParseWithLength((char*)meta,meta_n);assert(reply);free(meta);
        uint8_t *out=malloc(raw_n+16);memset(out,0xa5,raw_n+16);
        assert(ota_range_decode(reply,wire,wire_n,out,(int)raw_n,offset,true)&&!memcmp(out,raw,raw_n)&&!workspace);good++;bytes+=raw_n;
        assert(!ota_range_decode(reply,wire,wire_n,out,(int)raw_n,offset+1,true));bad++;
        assert(!ota_range_decode(reply,wire,wire_n,out,49153,offset,true));bad++;
        assert(!ota_range_decode(reply,wire,0,out,(int)raw_n,offset,true));bad++;
        if(cJSON_HasObjectItem(reply,"encoding")) {
            zlib++;
            assert(!ota_range_decode(reply,wire,wire_n,out,(int)raw_n,offset,false));bad++;
            assert(!ota_range_decode(reply,wire,wire_n-1,out,(int)raw_n,offset,true));bad++;
            wire[wire_n]=0;assert(!ota_range_decode(reply,wire,wire_n+1,out,(int)raw_n,offset,true));bad++;
            wire[wire_n-1]^=0x80;assert(!ota_range_decode(reply,wire,wire_n,out,(int)raw_n,offset,true));wire[wire_n-1]^=0x80;bad++;
            fail_workspace=true;assert(!ota_range_decode(reply,wire,wire_n,out,(int)raw_n,offset,true)&&!workspace);fail_workspace=false;bad++;
            cJSON_ReplaceItemInObject(reply,"decodedSize",cJSON_CreateNumber((double)raw_n+0.5));assert(!ota_range_decode(reply,wire,wire_n,out,(int)raw_n,offset,true));bad++;
            cJSON_ReplaceItemInObject(reply,"decodedSize",cJSON_CreateNumber((double)raw_n-1));assert(!ota_range_decode(reply,wire,wire_n,out,(int)raw_n-1,offset,true));bad++;
            cJSON_ReplaceItemInObject(reply,"decodedSize",cJSON_CreateNumber((double)raw_n));
            cJSON_ReplaceItemInObject(reply,"encoding",cJSON_CreateString("unknown"));assert(!ota_range_decode(reply,wire,wire_n,out,(int)raw_n,offset,true));bad++;
        } else {
            assert(ota_range_decode(reply,wire,wire_n,out,(int)raw_n,offset,false));good++;
            if(raw_n>1){assert(!ota_range_decode(reply,wire,wire_n-1,out,(int)raw_n,offset,true));bad++;}
        }
        for(size_t i=raw_n;i<raw_n+16;i++)assert(out[i]==0xa5);
        assert(!workspace);free(raw);free(wire);free(out);cJSON_Delete(reply);
    }
    cJSON_Delete(vectors);assert(zlib>0);
    printf("TAB5_OTA_ZLIB_NATIVE_OK good=%u rejects=%u compressed=%u bytes=%llu workspace=%zu exact_bytes_bounds_trailing_truncated_checksum_oom_legacy\n",good,bad,zlib,(unsigned long long)bytes,sizeof(tinfl_decompressor));return 0;
}
