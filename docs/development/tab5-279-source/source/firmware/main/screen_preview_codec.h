#pragma once
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

/* RGB565 little endian, lossless word PackBits. A 16-bit control has a count
 * in bits 0..14 (1..32767); bit 15 selects a repeated word, otherwise literal
 * words follow. No zero counts, trailing data or implicit pixels are allowed. */
static inline void preview_word(uint8_t *out,uint16_t value){out[0]=(uint8_t)value;out[1]=(uint8_t)(value>>8);}
static inline size_t preview_pack(const uint16_t *pixels,size_t count,uint8_t *out,size_t capacity) {
    if(!pixels||!out||!count)return 0;
    size_t pos=0,at=0;
    while(at<count) {
        size_t run=1;
        while(at+run<count&&run<32767&&pixels[at+run]==pixels[at])run++;
        if(run>=3) {
            if(capacity-pos<4)return 0;
            preview_word(out+pos,(uint16_t)(0x8000|run));preview_word(out+pos+2,pixels[at]);pos+=4;at+=run;
        } else {
            size_t start=at;at+=run;
            while(at<count&&at-start<32767) {
                run=1;while(at+run<count&&run<3&&pixels[at+run]==pixels[at])run++;
                if(run>=3)break;
                size_t room=32767-(at-start);at+=run<room?run:room;
            }
            size_t n=at-start;
            if(n>(capacity-pos)/2||capacity-pos<2+n*2)return 0;
            preview_word(out+pos,(uint16_t)n);pos+=2;
            for(size_t i=start;i<at;i++){preview_word(out+pos,pixels[i]);pos+=2;}
        }
    }
    return pos;
}
