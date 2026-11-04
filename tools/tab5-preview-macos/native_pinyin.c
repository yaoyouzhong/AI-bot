#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdlib.h>
#include <zlib.h>
#include "miniz.h"
void *heap_caps_malloc(size_t n,unsigned caps){assert(caps==3);return malloc(n);}
tinfl_status tinfl_decompress(tinfl_decompressor *state,const mz_uint8 *input,size_t *length,mz_uint8 *start,mz_uint8 *out,size_t *capacity,const mz_uint32 flags){
    assert(state&&start==out&&flags==(TINFL_FLAG_PARSE_ZLIB_HEADER|TINFL_FLAG_USING_NON_WRAPPING_OUTPUT_BUF));
    uLong produced=(uLong)*capacity,consumed=(uLong)*length;
    int result=uncompress2(out,&produced,input,&consumed);*length=consumed;*capacity=produced;
    return result==Z_OK?TINFL_STATUS_DONE:TINFL_STATUS_FAILED;
}
