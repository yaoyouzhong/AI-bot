#pragma once
// Included after gallery_remote.h, cJSON, allocation and SHA-256 declarations.
// Pointer ownership stays with the worker/cache. LVGL receives only text copies.
#ifndef GALLERY_TEXT_ALLOC
#define GALLERY_TEXT_ALLOC(bytes) malloc(bytes)
#endif
static void gallery_guide_free(gallery_guide *g) {
    if(!g)return;
    for(int i=0;i<32;i++)free(g->transcription_pages[i]);
    free(g);
}
static bool gallery_text_hash(const char *value,const char *expected) {
    if(!value||!expected||strlen(expected)!=64||strspn(expected,"0123456789abcdef")!=64)return false;
    uint8_t hash[32];char hex[65];if(mbedtls_sha256((const uint8_t *)value,strlen(value),hash,0))return false;
    for(int i=0;i<32;i++)sprintf(hex+2*i,"%02x",hash[i]);
    return !strcmp(hex,expected);
}
static bool gallery_text_page_set(gallery_guide *g,int page,const char *value) {
    if(!g||page<0||page>=g->transcription_page_count||page>=32||!value||!value[0]||strlen(value)>4095)return false;
    size_t n=strlen(value);char *copy=GALLERY_TEXT_ALLOC(n+1);if(!copy)return false;
    memcpy(copy,value,n+1);free(g->transcription_pages[page]);g->transcription_pages[page]=copy;return true;
}
static bool gallery_text_pages_complete(const gallery_guide *g) {
    if(!g||g->transcription_page_count<2||g->transcription_page_count>32)return false;
    size_t bytes=0;
    for(int i=0;i<g->transcription_page_count;i++) {
        if(!g->transcription_pages[i]||!g->transcription_pages[i][0]||strlen(g->transcription_pages[i])>4095)return false;
        bytes+=strlen(g->transcription_pages[i]);
    }
    if(bytes>32760)return false;
    char *complete=GALLERY_TEXT_ALLOC(bytes+1);if(!complete)return false;
    size_t at=0;
    for(int i=0;i<g->transcription_page_count;i++){size_t n=strlen(g->transcription_pages[i]);memcpy(complete+at,g->transcription_pages[i],n);at+=n;}
    complete[at]=0;bool ok=gallery_text_hash(complete,g->transcription_sha256);free(complete);return ok;
}
