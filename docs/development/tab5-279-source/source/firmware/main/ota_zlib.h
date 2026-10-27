#pragma once
#include <stdbool.h>
#include <stdint.h>
#include <stddef.h>

// Caller authenticates the envelope and validates offset/decodedSize first.
// ROM workspace lives in PSRAM, never on the 8 KiB OTA receiver task stack.
static bool ota_zlib_decode(const uint8_t *input,size_t length,uint8_t *out,size_t expected) {
    if(!input||!out||!length||!expected||expected>49152||length>=expected)return false;
    tinfl_decompressor *state=heap_caps_malloc(sizeof(*state),MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    if(!state)return false;
    tinfl_init(state);size_t consumed=length,produced=expected;
    tinfl_status status=tinfl_decompress(state,input,&consumed,out,out,&produced,
        TINFL_FLAG_PARSE_ZLIB_HEADER|TINFL_FLAG_USING_NON_WRAPPING_OUTPUT_BUF);
    bool ok=status==TINFL_STATUS_DONE&&consumed==length&&produced==expected;
    free(state);return ok;
}
static bool ota_range_decode(cJSON *reply,const uint8_t *input,size_t size,uint8_t *out,int count,int offset,bool negotiated) {
    if(!input||!out||count<1||count>49152||offset<0||!size||size>(size_t)count)return false;
    cJSON *at=cJSON_GetObjectItemCaseSensitive(reply,"offset");
    if(!cJSON_IsNumber(at)||at->valuedouble!=offset)return false;
    cJSON *encoding=cJSON_GetObjectItemCaseSensitive(reply,"encoding");
    if(encoding) {
        cJSON *decoded=cJSON_GetObjectItemCaseSensitive(reply,"decodedSize");
        return negotiated&&cJSON_IsString(encoding)&&!strcmp(encoding->valuestring,"zlib")&&
            cJSON_IsNumber(decoded)&&decoded->valuedouble==count&&ota_zlib_decode(input,size,out,count);
    }
    if(size!=(size_t)count)return false;
    if(input!=out)memcpy(out,input,size);
    return true;
}
