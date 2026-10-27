#pragma once
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

/* Sample the real panel buffer, undoing the same software rotation as its
 * renderer. No panel write, graphics replay or second full-screen buffer. */
static inline bool tab5_preview_sample(const uint16_t *source,int nw,int nh,unsigned rotation,
                                      uint16_t *out,int width,int height) {
    if(!source||!out||nw<=0||nh<=0||rotation>3||width<=0||height<=0)return false;
    int sw=(rotation&1)?nh:nw,sh=(rotation&1)?nw:nh;
    if(width>sw||height>sh)return false;
    for(int ty=0;ty<height;ty+=16)for(int tx=0;tx<width;tx+=16)
        for(int y=ty;y<height&&y<ty+16;y++) {
          int sy=height==sh?y:(int)(((int64_t)y*2+1)*sh/(height*2));
          for(int x=tx;x<width&&x<tx+16;x++) {
            int sx=width==sw?x:(int)(((int64_t)x*2+1)*sw/(width*2));
            int nx=sx,ny=sy;
            if(rotation==1){nx=sy;ny=nh-1-sx;}
            else if(rotation==2){nx=nw-1-sx;ny=nh-1-sy;}
            else if(rotation==3){nx=nw-1-sy;ny=sx;}
            out[(size_t)y*width+x]=source[(size_t)ny*nw+nx];
          }
        }
    return true;
}
