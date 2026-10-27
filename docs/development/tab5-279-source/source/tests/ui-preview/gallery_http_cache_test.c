#include "gallery_http_cache.h"
#include <assert.h>
#include <stdio.h>
int main(void) {
    gallery_http_lease idle={0},first={.client=(void *)1,.endpoint="http://bridge:18765/tab5/v1/rpc",.created_ms=100,.requests=2};
    const char *url=first.endpoint;
    assert(!gallery_http_valid(&idle,url,100));
    for(int image=0;image<8;image++) {
        assert(gallery_http_return_idle(&idle,&first));
        gallery_http_lease active=gallery_http_take_idle(&idle);assert(active.client==(void *)1&&!idle.client);
        assert(gallery_http_valid(&active,url,100+image*2000));
        assert(!gallery_http_take_idle(&idle).client); // No shared active handle.
        active.requests+=2;first=active;
    }
    assert(!gallery_http_valid(&first,"http://different:18765/tab5/v1/rpc",1000));
    assert(!gallery_http_valid(&first,url,99));
    assert(gallery_http_valid(&first,url,60099));assert(!gallery_http_valid(&first,url,60100));
    first.requests=200;assert(gallery_http_valid(&first,url,1000));
    first.requests=201;assert(!gallery_http_valid(&first,url,1000));
    first.requests=2;assert(gallery_http_return_idle(&idle,&first));
    gallery_http_lease other=first;other.client=(void *)2;
    assert(!gallery_http_return_idle(&idle,&other)&&idle.client==(void *)1);
    gallery_http_lease empty={0};assert(!gallery_http_return_idle(&empty,&empty));
    puts("GALLERY_HTTP_CACHE_OK 8 images one exclusive handle; endpoint/clock/TTL/request expiry; no active alias or idle overwrite");return 0;
}
