#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "gallery_jpeg_cache.h"
static gallery_image key(int category,int frame,bool portrait,const char *date) {
    gallery_image value={.category=category,.frame=frame,.frames=3,.portrait=portrait};
    snprintf(value.date,sizeof(value.date),"%s",date);return value;
}
static gallery_jpeg blob(gallery_image key,size_t bytes) {
    uint8_t *data=malloc(bytes);assert(data);memset(data,key.category*20+key.frame*2+key.portrait+1,bytes);
    return (gallery_jpeg){.key=key,.data=data,.size=bytes,.capacity=bytes};
}
int main(void) {
    gallery_jpeg_cache cache={0};
    // Both galleries, each page and each layout remain distinct after the two
    // output bitmaps have been recycled. Revisit without any network provider.
    for(int c=0;c<2;c++)for(int f=0;f<3;f++)for(int p=0;p<2;p++)
        gallery_jpeg_put(&cache,blob(key(c,f,p,"2026-10-07"),131072),cache.generation);
    for(int pass=0;pass<4;pass++)for(int c=0;c<2;c++)for(int f=0;f<3;f++)for(int p=0;p<2;p++) {
        gallery_image k=key(c,f,p,"2026-10-07");assert(gallery_jpeg_has(&cache,&k));
        gallery_jpeg value=gallery_jpeg_take(&cache,&k);assert(value.data&&value.key.frames==3);
        assert(value.data[0]==c*20+f*2+p+1&&value.data[value.size-1]==value.data[0]);
        gallery_jpeg_put(&cache,value,cache.generation);
    }
    assert(cache.hits==48&&cache.bytes==12*131072);
    gallery_image tomorrow=key(0,0,false,"2026-10-08");assert(!gallery_jpeg_has(&cache,&tomorrow));
    assert(!gallery_jpeg_take(&cache,&tomorrow).data);
    // A validated new day replaces the compressed daily collection.
    gallery_jpeg_put(&cache,blob(tomorrow,1048576),cache.generation);
    assert(cache.bytes==1048576&&gallery_jpeg_has(&cache,&tomorrow));
    gallery_image oldest=key(0,0,false,"2026-10-07");assert(!gallery_jpeg_has(&cache,&oldest));
    gallery_jpeg_put(&cache,blob(oldest,100),cache.generation);assert(cache.bytes==1048576);
    // 30 page/layout JPEGs fit the most complex curated day (8.91 MiB).
    gallery_jpeg_clear(&cache);
    for(int i=0;i<30;i++){
        gallery_image k=key(i/16,i%16/2,i%2,"2026-10-08");k.frames=8;
        gallery_jpeg_put(&cache,blob(k,320000),cache.generation);
    }
    assert(cache.bytes==9600000);
    for(int i=0;i<30;i++){
        gallery_image k=key(i/16,i%16/2,i%2,"2026-10-08");assert(gallery_jpeg_has(&cache,&k));
    }
    gallery_jpeg_clear(&cache);gallery_jpeg_put(&cache,blob(tomorrow,1048576),cache.generation);
    unsigned generation=cache.generation;
    gallery_jpeg decoding=gallery_jpeg_take(&cache,&tomorrow);assert(decoding.data);
    gallery_jpeg_clear(&cache);assert(cache.bytes==0);
    // A camera/voice/OTA reclaim may overlap decoding. The decoder's borrowed
    // allocation survives; its old generation cannot refill the emptied cache.
    assert(decoding.data[decoding.size-1]==1);
    gallery_jpeg_put(&cache,decoding,generation);assert(cache.bytes==0);
    gallery_jpeg_put(&cache,blob(tomorrow,GALLERY_JPEG_CACHE_BYTES+1),cache.generation);
    assert(cache.bytes==0);
    gallery_jpeg_put(&cache,blob(tomorrow,100),cache.generation);
    gallery_jpeg_put(&cache,blob(tomorrow,200),cache.generation);assert(cache.bytes==200);
    gallery_jpeg_clear(&cache);assert(cache.bytes==0);
    puts("GALLERY_JPEG_CACHE_OK 48 offline revisits; 30 layouts/9.6 MB day retained; validated rollover; late old-day rejection; bounded ownership and foreground reclaim");
    return 0;
}
