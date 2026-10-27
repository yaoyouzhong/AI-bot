#pragma once
#include "cJSON.h"
#include "mbedtls/base64.h"
#include <stdint.h>
#include <stdbool.h>
#include <stdlib.h>
#include <string.h>
// Authenticated cleartext envelope: T5R1, metadata length u32 LE, JSON, raw data.
// T5R1 keeps its original bounds. Authenticated rpcBulk negotiation permits
// T5R2 with 48 KiB payloads; no larger frame is sent to a legacy peer.
#define RPC_BULK_DATA 49152u
static inline uint8_t *rpc_binary_pack_bulk(cJSON *request,const uint8_t *raw,size_t count,size_t *length,bool bulk) {
    if(!raw||!count||count>(bulk?RPC_BULK_DATA:8192)||cJSON_HasObjectItem(request,"data"))return NULL;
    char *metadata=cJSON_PrintUnformatted(request);if(!metadata)return NULL;
    size_t m=strlen(metadata);uint8_t *out=m>=2&&m<=4096?malloc(8+m+count):NULL;
    if(out){memcpy(out,bulk?"T5R2":"T5R1",4);for(int i=0;i<4;i++)out[4+i]=(uint8_t)(m>>(8*i));
        memcpy(out+8,metadata,m);memcpy(out+8+m,raw,count);*length=8+m+count;}
    free(metadata);return out;
}
static inline uint8_t *rpc_binary_pack(cJSON *request,const uint8_t *raw,size_t count,size_t *length) {
    return rpc_binary_pack_bulk(request,raw,count,length,false);
}
static inline uint8_t *rpc_binary_encode(cJSON *request,size_t *length) {
    cJSON *data=cJSON_DetachItemFromObjectCaseSensitive(request,"data");
    if(!cJSON_IsString(data)||!data->valuestring[0]){if(data)cJSON_AddItemToObject(request,"data",data);return NULL;}
    char *metadata=cJSON_PrintUnformatted(request);cJSON_AddItemToObject(request,"data",data);
    if(!metadata)return NULL;
    size_t m=strlen(metadata),raw=0;
    if(m>4096||mbedtls_base64_decode(NULL,0,&raw,(uint8_t*)data->valuestring,strlen(data->valuestring))!=MBEDTLS_ERR_BASE64_BUFFER_TOO_SMALL||!raw||raw>8192){free(metadata);return NULL;}
    uint8_t *out=malloc(8+m+raw);
    if(out){memcpy(out,"T5R1",4);for(int i=0;i<4;i++)out[4+i]=(uint8_t)(m>>(8*i));memcpy(out+8,metadata,m);
        if(mbedtls_base64_decode(out+8+m,raw,&raw,(uint8_t*)data->valuestring,strlen(data->valuestring))!=0){free(out);out=NULL;}
        else *length=8+m+raw;
    }
    free(metadata);return out;
}
static inline cJSON *rpc_binary_unpack(const char *clear,size_t size,uint8_t *raw,size_t capacity,size_t *count) {
    if(raw&&!count)return NULL;
    if(count)*count=0;
    bool bulk=size>=4&&!memcmp(clear,"T5R2",4);
    if(!bulk&&(size<4||memcmp(clear,"T5R1",4))) {
        cJSON *root=cJSON_ParseWithLength(clear,size);
        if(raw) {
            cJSON *body=cJSON_GetObjectItemCaseSensitive(root,"body"),*data=cJSON_GetObjectItemCaseSensitive(body,"data");
            if(data&&(!cJSON_IsString(data)||mbedtls_base64_decode(raw,capacity,count,(uint8_t*)data->valuestring,strlen(data->valuestring))!=0)) {
                if(count)*count=0;
                cJSON_Delete(root);return NULL;
            }
            cJSON_DeleteItemFromObjectCaseSensitive(body,"data");
        }
        return root;
    }
    if(size<9)return NULL;
    const uint8_t *p=(const uint8_t*)clear;uint32_t m=0;for(int i=0;i<4;i++)m|=(uint32_t)p[4+i]<<(8*i);
    if(m<2||m>4096||m>size-8||size-8-m<1||size-8-m>(bulk?RPC_BULK_DATA:16384))return NULL;
    cJSON *root=cJSON_ParseWithLength(clear+8,m);cJSON *body=cJSON_GetObjectItemCaseSensitive(root,"body");
    if(!cJSON_IsObject(body)||cJSON_HasObjectItem(body,"data")){cJSON_Delete(root);return NULL;}
    if(raw){size_t n=size-8-m;if(n>capacity){cJSON_Delete(root);return NULL;}
        memcpy(raw,p+8+m,n);*count=n;return root;}
    size_t n=size-8-m,encoded_size=4*((n+2)/3)+1,written=0;unsigned char *encoded=malloc(encoded_size);
    if(!encoded||mbedtls_base64_encode(encoded,encoded_size,&written,p+8+m,n)!=0){free(encoded);cJSON_Delete(root);return NULL;}
    encoded[written]=0;cJSON *data=cJSON_CreateString((char*)encoded);free(encoded);
    if(!data){cJSON_Delete(root);return NULL;}cJSON_AddItemToObject(body,"data",data);return root;
}
static inline cJSON *rpc_binary_decode(const char *clear,size_t size) {return rpc_binary_unpack(clear,size,NULL,0,NULL);}
