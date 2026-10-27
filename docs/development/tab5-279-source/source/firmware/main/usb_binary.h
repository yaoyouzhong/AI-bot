#pragma once
#include "cJSON.h"
#include <stdint.h>
#include <stdbool.h>
#include <string.h>
#define USB_BINARY_MAX 65535
#define USB_BINARY_CHUNK (USB_BINARY_MAX-9)
typedef struct {uint8_t *data;unsigned used,want,chunk;uint32_t tag;int64_t deadline;bool failed;} usb_binary;
static bool usb_binary_wait(cJSON *root,unsigned count,unsigned *wait) {
    cJSON *value=cJSON_GetObjectItemCaseSensitive(root,"waitMs");*wait=0;
    if(!value)return true;
    if(!cJSON_IsNumber(value)||(value->valuedouble!=0&&value->valuedouble!=20)||
       (count&&value->valuedouble!=0))return false;
    *wait=(unsigned)value->valuedouble;return true;
}
static bool usb_binary_expire(usb_binary *b,int64_t now) {
    if(!b->want||now<b->deadline)return false;
    b->want=b->used=0;b->failed=true;return true;
}
static inline bool usb_binary_feed(usb_binary *b,uint8_t value) {
    if(b->failed||!b->data||!b->want||b->want>USB_BINARY_MAX||b->used>=b->want)return false;
    b->data[b->used++]=value;return b->used==b->want;
}
static size_t usb_binary_feed_span(usb_binary *b,const uint8_t *bytes,size_t size) {
    if(b->failed||!b->data||!b->want||b->want>USB_BINARY_MAX||b->used>=b->want)return 0;
    size_t n=b->want-b->used;if(n>size)n=size;
    memcpy(b->data+b->used,bytes,n);b->used+=n;return n;
}
static void usb_binary_header(uint8_t out[16],uint32_t tag,unsigned size,bool ok) {
    memcpy(out,"AIB2",4);
    for(unsigned i=0;i<4;i++){out[4+i]=tag>>(i*8);out[8+i]=size>>(i*8);out[12+i]=(unsigned)ok>>(i*8);}
}
static bool usb_binary_command(cJSON *root,const char *device,uint32_t *tag,unsigned *count,unsigned *chunk) {
    cJSON *v=cJSON_GetObjectItemCaseSensitive(root,"version"),*id=cJSON_GetObjectItemCaseSensitive(root,"deviceId");
    cJSON *t=cJSON_GetObjectItemCaseSensitive(root,"tag"),*n=cJSON_GetObjectItemCaseSensitive(root,"count");
    if(!cJSON_IsNumber(v)||v->valuedouble!=1||!cJSON_IsString(id)||strcmp(id->valuestring,device)||
       !cJSON_IsNumber(t)||t->valuedouble<0||t->valuedouble>UINT32_MAX||t->valuedouble!=(uint32_t)t->valuedouble||
       !cJSON_IsNumber(n)||n->valuedouble<0||n->valuedouble>USB_BINARY_MAX||n->valuedouble!=(unsigned)n->valuedouble)return false;
    cJSON *c=cJSON_GetObjectItemCaseSensitive(root,"chunk");
    unsigned limit=16384; // Existing bridges omit chunk on binary commands.
    if(c) {
        if(!cJSON_IsNumber(c)||(c->valuedouble!=2048&&c->valuedouble!=16384&&c->valuedouble!=USB_BINARY_CHUNK))return false;
        limit=(unsigned)c->valuedouble;
    }
    if(n->valuedouble>limit+9)return false;
    *tag=(uint32_t)t->valuedouble;*count=(unsigned)n->valuedouble;*chunk=limit;return true;
}
