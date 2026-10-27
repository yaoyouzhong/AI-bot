#pragma once
#include <stdint.h>
#include <stddef.h>
#include <string.h>
#if defined(__GNUC__) || defined(__clang__)
typedef uint32_t tab5_copy_word __attribute__((__may_alias__));
#else
typedef uint32_t tab5_copy_word;
#endif
// Pixels may be halfword aligned after clipping a horizontally moving page.
// Keep word stores even when source/destination word alignment differs. Never
// read before the source or beyond the final pixel of an odd-width row.
static inline void tab5_rgb565_copy(void *destination,const void *source,size_t pixels) {
    if(((uintptr_t)destination|(uintptr_t)source)&1){memcpy(destination,source,pixels*2);return;}
    uint16_t *out=destination;const uint16_t *in=source;
    if(pixels&&((uintptr_t)out&2)){*out++=*in++;pixels--;}
    tab5_copy_word *words=(tab5_copy_word*)out;
    while(pixels>=8) {
        words[0]=(uint32_t)in[0]|((uint32_t)in[1]<<16);
        words[1]=(uint32_t)in[2]|((uint32_t)in[3]<<16);
        words[2]=(uint32_t)in[4]|((uint32_t)in[5]<<16);
        words[3]=(uint32_t)in[6]|((uint32_t)in[7]<<16);
        words+=4;in+=8;pixels-=8;
    }
    while(pixels>=2){*words++=(uint32_t)in[0]|((uint32_t)in[1]<<16);in+=2;pixels-=2;}
    if(pixels)*(uint16_t*)words=*in;
}
