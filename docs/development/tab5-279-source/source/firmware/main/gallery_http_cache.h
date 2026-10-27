#pragma once
#include <stdbool.h>
#include <stdint.h>
#include <string.h>

// The worker owns a borrowed handle. The mutex protects only the single idle
// slot, never an SDK call. A different foreground worker cannot share it.
typedef struct {void *client;char endpoint[128];uint64_t created_ms;unsigned requests;} gallery_http_lease;
static bool gallery_http_valid(const gallery_http_lease *lease,const char *endpoint,uint64_t now) {
    // The paired desktop accepts at most 400 requests/75 s per connection.
    // Leave room for the largest legacy image + paged guide and retries.
    return lease->client&&now>=lease->created_ms&&now-lease->created_ms<60000&&
        lease->requests<=200&&endpoint&&!strcmp(lease->endpoint,endpoint);
}
static gallery_http_lease gallery_http_take_idle(gallery_http_lease *idle) {
    gallery_http_lease lease=*idle;*idle=(gallery_http_lease){0};return lease;
}
static bool gallery_http_return_idle(gallery_http_lease *idle,const gallery_http_lease *lease) {
    if(!lease->client||idle->client)return false;
    *idle=*lease;return true;
}
