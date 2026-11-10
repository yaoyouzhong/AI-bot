#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdlib.h>
#include <windows.h>
#include "miniz.h"
static unsigned attempts,live,fail_at;static bool corrupt;
void *heap_caps_malloc(size_t n,unsigned caps){assert(caps==3);attempts++;if(attempts==fail_at)return NULL;void *p=malloc(n);if(p)live++;return p;}
static void checked_free(void *p){if(p){assert(live);live--;free(p);}}
tinfl_status tinfl_decompress(tinfl_decompressor *state,const mz_uint8 *input,size_t *length,mz_uint8 *start,mz_uint8 *out,size_t *capacity,const mz_uint32 flags){
    assert(state&&start==out&&flags==(TINFL_FLAG_PARSE_ZLIB_HEADER|TINFL_FLAG_USING_NON_WRAPPING_OUTPUT_BUF));
    typedef int (__cdecl *inflate_fn)(void*,unsigned long*,const void*,unsigned long*);
    static inflate_fn fn;if(!fn){HMODULE dll=LoadLibraryA(getenv("TAB5_TEST_ZLIB")?getenv("TAB5_TEST_ZLIB"):"C:\\Program Files\\Git\\ucrt64\\bin\\zlib1.dll");assert(dll);fn=(inflate_fn)GetProcAddress(dll,"uncompress2");assert(fn);}
    if(corrupt)return TINFL_STATUS_FAILED;
    unsigned long produced=(unsigned long)*capacity,consumed=(unsigned long)*length;
    int result=fn(out,&produced,input,&consumed);*length=consumed;*capacity=produced;
    return result==0?TINFL_STATUS_DONE:TINFL_STATUS_FAILED;
}
#define free checked_free
#include "../../firmware/main/tab5_pinyin_data.c"
#undef free
void pinyin_lifecycle_test(void){
    tab5_pinyin_data out={0};assert(!tab5_pinyin_data_init(NULL));
    fail_at=1;assert(!tab5_pinyin_data_init(&out)&&!out.rows&&!live);
    fail_at=attempts+2;assert(!tab5_pinyin_data_init(&out)&&!out.rows&&!live);
    fail_at=0;corrupt=true;assert(!tab5_pinyin_data_init(&out)&&!out.rows&&!live);corrupt=false;
    uint8_t *raw=malloc(PINYIN_RAW_BYTES),*bad=malloc(sizeof(pinyin_packed)+1);assert(raw&&bad);
    memcpy(bad,pinyin_packed,sizeof(pinyin_packed));bad[sizeof(pinyin_packed)/2]^=1;
    assert(!dictionary_decode(bad,sizeof(pinyin_packed),raw,PINYIN_RAW_BYTES));
    assert(!dictionary_decode(pinyin_packed,sizeof(pinyin_packed)-1,raw,PINYIN_RAW_BYTES));
    memcpy(bad,pinyin_packed,sizeof(pinyin_packed));bad[sizeof(pinyin_packed)]=0;
    assert(!dictionary_decode(bad,sizeof(pinyin_packed)+1,raw,PINYIN_RAW_BYTES));
    assert(!dictionary_decode(pinyin_packed,sizeof(pinyin_packed),raw,PINYIN_RAW_BYTES-1));
    assert(!dictionary_decode(pinyin_packed,sizeof(pinyin_packed),raw,PINYIN_RAW_BYTES+1));
    free(raw);free(bad);assert(!live);
    assert(tab5_pinyin_data_init(&out)&&out.count==PINYIN_ROWS&&live==1);
    unsigned before=attempts;tab5_pinyin_data again={0};assert(tab5_pinyin_data_init(&again)&&again.rows==out.rows&&attempts==before&&live==1);
}
unsigned pinyin_allocations(void){return attempts;}
