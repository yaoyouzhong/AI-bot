#pragma once
#include "rpc_binary.h"
#include "rpc_reply.h"

#define GALLERY_STREAM_IMAGE_MAX 1048576u
typedef int (*gallery_stream_read)(void *context,uint8_t *out,size_t size);
typedef char *(*gallery_stream_decrypt)(void *context,const uint8_t *packet,size_t size,const char *nonce,int *error);
typedef bool (*gallery_stream_progress)(void *context,size_t verified);
static inline uint32_t gallery_stream_u32(const uint8_t *p){return (uint32_t)p[0]|(uint32_t)p[1]<<8|(uint32_t)p[2]<<16|(uint32_t)p[3]<<24;}
static inline bool gallery_stream_exact(gallery_stream_read read,void *context,uint8_t *out,size_t size) {
    while(size){int n=read(context,out,size);if(n<=0||(size_t)n>size)return false;out+=n;size-=n;}return true;
}
static inline bool gallery_stream_number(cJSON *body,const char *name,size_t expected) {
    cJSON *v=cJSON_GetObjectItemCaseSensitive(body,name);return cJSON_IsNumber(v)&&v->valuedouble==(double)expected;
}
// No large HTTP/AES buffer: verify a <=64 KiB block, copy to the existing JPEG
// buffer, then make only that verified prefix eligible for the card writer.
static inline cJSON *gallery_stream_receive(gallery_stream_read read,gallery_stream_decrypt decrypt,
    gallery_stream_progress progress,void *context,size_t wire,const char *nonce,uint8_t *out,size_t capacity,
    size_t offset,size_t count,int *status,size_t *received) {
    *status=0;*received=0;uint8_t header[16];
    if(!out||count<1||count>GALLERY_STREAM_IMAGE_MAX||offset>GALLERY_STREAM_IMAGE_MAX-count||capacity<count||wire<28||
       !gallery_stream_exact(read,context,header,4))return NULL;
    if(memcmp(header,"T5S1",4)) {
        // Rejected/busy requests retain the ordinary bounded encrypted reply.
        if(wire>65535)return NULL;
        uint8_t *packet=malloc(wire);if(!packet)return NULL;memcpy(packet,header,4);
        bool got=gallery_stream_exact(read,context,packet+4,wire-4);int error=0;
        char *clear=got?decrypt(context,packet,wire,nonce,&error):NULL;free(packet);
        cJSON *root=clear?rpc_binary_unpack(clear,wire-28,NULL,0,NULL):NULL;free(clear);
        cJSON *body=root?rpc_reply_body(root,status):NULL;cJSON_Delete(root);
        if(body&&*status==200){cJSON_Delete(body);body=NULL;*status=-1004;}
        if(!body&&error)*status=error;
        return body;
    }
    unsigned blocks=(count+RPC_BULK_DATA-1)/RPC_BULK_DATA;
    if(wire>16+count+blocks*(4+4096+8+28)||!gallery_stream_exact(read,context,header+4,12)||
       gallery_stream_u32(header+4)!=offset||gallery_stream_u32(header+8)!=count||gallery_stream_u32(header+12)!=blocks)return NULL;
    size_t remaining=wire-16;cJSON *result=NULL;
    for(unsigned block=0;block<blocks;block++) {
        uint8_t length[4];
        if(remaining<4||!gallery_stream_exact(read,context,length,4))goto failed;
        size_t n=gallery_stream_u32(length);remaining-=4;
        if(n<37||n>65535||n>remaining)goto failed;
        uint8_t *packet=malloc(n);if(!packet)goto failed;
        bool got=gallery_stream_exact(read,context,packet,n);int error=0;
        char *clear=got?decrypt(context,packet,n,nonce,&error):NULL;free(packet);remaining-=n;
        if(!clear){if(error)*status=error;goto failed;}
        size_t size=0;int code=0;
        cJSON *root=rpc_binary_unpack(clear,n-28,out+*received,count-*received,&size);free(clear);
        cJSON *body=root?rpc_reply_body(root,&code):NULL;cJSON_Delete(root);
        size_t want=count-*received;if(want>RPC_BULK_DATA)want=RPC_BULK_DATA;
        bool valid=body&&code==200&&size==want&&gallery_stream_number(body,"offset",offset+*received)&&
            gallery_stream_number(body,"count",size)&&gallery_stream_number(body,"streamOffset",offset)&&
            gallery_stream_number(body,"streamBytes",count)&&gallery_stream_number(body,"block",block)&&gallery_stream_number(body,"blocks",blocks);
        cJSON_Delete(body);if(!valid){*status=-1004;goto failed;}
        *received+=size;
        if(progress&&!progress(context,*received))goto failed;
    }
    if(remaining||*received!=count)goto failed;
    result=cJSON_CreateObject();if(!result)return NULL;
    if(!cJSON_AddNumberToObject(result,"offset",offset)||!cJSON_AddNumberToObject(result,"count",count)){cJSON_Delete(result);return NULL;}
    *status=200;return result;
failed:
    // Earlier authenticated blocks remain resumable; unverified bytes never
    // advance received or become a card checkpoint/display source.
    return NULL;
}
