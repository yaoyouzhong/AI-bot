#include "gallery_jpeg_cache.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>

static int find(const gallery_jpeg_cache *cache,const gallery_image *key) {
    for(int i=0;i<GALLERY_JPEG_CACHE_SLOTS;i++) {
        const gallery_jpeg *entry=&cache->entries[i];
        if(entry->data&&entry->key.category==key->category&&entry->key.frame==key->frame&&
           entry->key.portrait==key->portrait&&!strcmp(entry->key.date,key->date))return i;
    }
    return -1;
}
static void drop(gallery_jpeg_cache *cache,int slot) {
    cache->bytes-=cache->entries[slot].capacity;
    free(cache->entries[slot].data);cache->entries[slot]=(gallery_jpeg){0};
}
bool gallery_jpeg_has(const gallery_jpeg_cache *cache,const gallery_image *key){return find(cache,key)>=0;}
gallery_jpeg gallery_jpeg_take(gallery_jpeg_cache *cache,const gallery_image *key) {
    int slot=find(cache,key);
    if(slot<0){cache->misses++;return (gallery_jpeg){0};}
    gallery_jpeg value=cache->entries[slot];cache->entries[slot]=(gallery_jpeg){0};
    cache->bytes-=value.capacity;cache->hits++;return value;
}
void gallery_jpeg_clear(gallery_jpeg_cache *cache) {
    for(int i=0;i<GALLERY_JPEG_CACHE_SLOTS;i++)drop(cache,i);
    cache->date[0]=0;cache->generation++;
}
void gallery_jpeg_clear_category(gallery_jpeg_cache *cache,int category) {
    if(category<0||category>1)return;
    bool remains=false;
    for(int i=0;i<GALLERY_JPEG_CACHE_SLOTS;i++) {
        if(cache->entries[i].data&&cache->entries[i].key.category==category)drop(cache,i);
        if(cache->entries[i].data)remains=true;
    }
    if(!remains)cache->date[0]=0;
    cache->generation++;
}
void gallery_jpeg_put(gallery_jpeg_cache *cache,gallery_jpeg value,unsigned generation) {
    if(!value.data)return;
    if(generation!=cache->generation||value.size<16||value.size>value.capacity||
       value.capacity>GALLERY_JPEG_CACHE_BYTES||value.key.frames<1||value.key.frames>64||
       value.key.frame<0||value.key.frame>=value.key.frames||value.key.category<0||value.key.category>1||strlen(value.key.date)!=10) {
        free(value.data);return;
    }
    // Keep a single validated day. A failed new-day transfer never removes
    // yesterday's successful cache; a late old-day worker cannot restore it.
    if(cache->date[0]&&strcmp(value.key.date,cache->date)<0){free(value.data);return;}
    if(strcmp(value.key.date,cache->date)){
        for(int i=0;i<GALLERY_JPEG_CACHE_SLOTS;i++)drop(cache,i);
        snprintf(cache->date,sizeof(cache->date),"%s",value.key.date);
    }
    int existing=find(cache,&value.key);if(existing>=0)drop(cache,existing);
    int slot=-1;
    for(;;) {
        int oldest=-1;
        for(int i=0;i<GALLERY_JPEG_CACHE_SLOTS;i++) {
            if(!cache->entries[i].data)slot=i;
            else if(oldest<0||cache->entries[i].stamp<cache->entries[oldest].stamp)oldest=i;
        }
        if(slot>=0&&cache->bytes+value.capacity<=GALLERY_JPEG_CACHE_BYTES)break;
        if(oldest<0){free(value.data);return;}
        drop(cache,oldest);
    }
    value.key.pixels=NULL;value.stamp=++cache->stamp;
    cache->entries[slot]=value;cache->bytes+=value.capacity;
}
