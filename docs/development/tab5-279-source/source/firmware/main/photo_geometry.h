#pragma once
#include <stdint.h>

// Fit the actual photograph, without a baked-in landscape letterbox. Portrait
// previews use the same 230400-pixel budget as landscape previews.
static inline void photo_fit(unsigned width,unsigned height,unsigned max_width,unsigned max_height,
                             unsigned *out_width,unsigned *out_height) {
    if(!width||!height||!max_width||!max_height){*out_width=*out_height=0;return;}
    unsigned w=max_width,h=(uint64_t)height*w/width;
    if(h>max_height){h=max_height;w=(uint64_t)width*h/height;}
    *out_width=w?w:1;*out_height=h?h:1;
}
