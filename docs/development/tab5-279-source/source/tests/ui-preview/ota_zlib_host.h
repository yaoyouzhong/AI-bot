#pragma once
#include <windows.h>
#include "miniz.h"
#define MALLOC_CAP_SPIRAM 1
#define MALLOC_CAP_8BIT 2
#ifndef TAB5_TEST_EXTERNAL_HEAP
static bool fail_workspace;
static void *workspace;
static void *heap_caps_malloc(size_t n,unsigned caps) {
    assert(caps==3);
    if(n!=sizeof(tinfl_decompressor))return malloc(n);
    assert(!workspace);if(fail_workspace)return NULL;
    workspace=malloc(n);return workspace;
}
static void release(void *p){assert(p==workspace);workspace=NULL;free(p);}
#endif
tinfl_status tinfl_decompress(tinfl_decompressor *state,const mz_uint8 *input,size_t *length,mz_uint8 *start,mz_uint8 *out,size_t *capacity,const mz_uint32 flags) {
    assert(state&&start==out&&flags==(TINFL_FLAG_PARSE_ZLIB_HEADER|TINFL_FLAG_USING_NON_WRAPPING_OUTPUT_BUF));
#ifndef TAB5_TEST_EXTERNAL_HEAP
    assert(state==workspace);
#endif
    typedef int (__cdecl *uncompress_fn)(void*,unsigned long*,const void*,unsigned long*);
    static uncompress_fn inflate;
    if(!inflate){HMODULE dll=LoadLibraryA(getenv("TAB5_TEST_ZLIB")?getenv("TAB5_TEST_ZLIB"):"C:\\Program Files\\Git\\ucrt64\\bin\\zlib1.dll");assert(dll);inflate=(uncompress_fn)GetProcAddress(dll,"uncompress2");assert(inflate);}
    unsigned long produced=(unsigned long)*capacity,consumed=(unsigned long)*length;
    int result=inflate(out,&produced,input,&consumed);*length=consumed;*capacity=produced;
    return result==0?TINFL_STATUS_DONE:TINFL_STATUS_FAILED;
}
#ifndef TAB5_TEST_EXTERNAL_HEAP
#define free release
#endif
#include "../../firmware/main/ota_zlib.h"
#undef free
