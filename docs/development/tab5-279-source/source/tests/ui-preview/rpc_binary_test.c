#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include <windows.h>
#include <wincrypt.h>
#include "rpc_binary.h"
int mbedtls_base64_encode(unsigned char *out,size_t cap,size_t *n,const unsigned char *in,size_t size){DWORD need=0;if(!CryptBinaryToStringA(in,(DWORD)size,CRYPT_STRING_BASE64|CRYPT_STRING_NOCRLF,NULL,&need))return -1;*n=need-1;if(cap<need)return -42;return CryptBinaryToStringA(in,(DWORD)size,CRYPT_STRING_BASE64|CRYPT_STRING_NOCRLF,(char*)out,&need)?0:-1;}
int mbedtls_base64_decode(unsigned char *out,size_t cap,size_t *n,const unsigned char *in,size_t size){DWORD need=0;if(!CryptStringToBinaryA((char*)in,(DWORD)size,CRYPT_STRING_BASE64,NULL,&need,NULL,NULL))return -1;*n=need;if(cap<need)return -42;return CryptStringToBinaryA((char*)in,(DWORD)size,CRYPT_STRING_BASE64,out,&need,NULL,NULL)?0:-1;}
int main(int argc,char **argv){assert(argc==2);
    int sizes[]={1,8192,16384,16385,49152};
    for(unsigned j=0;j<5;j++) {
        char path[1024];snprintf(path,sizeof(path),"%s/rpc-binary-%d.bin",argv[1],sizes[j]);FILE *f=fopen(path,"rb");assert(f);
        uint8_t wire[65535];size_t n=fread(wire,1,sizeof(wire),f);fclose(f);
        cJSON *root=rpc_binary_decode((char*)wire,n);assert(root);
        cJSON *body=cJSON_GetObjectItemCaseSensitive(root,"body"),*data=cJSON_GetObjectItemCaseSensitive(body,"data");assert(cJSON_IsString(data));
        uint8_t raw[49152];size_t count=0;assert(!mbedtls_base64_decode(raw,sizeof(raw),&count,(uint8_t*)data->valuestring,strlen(data->valuestring))&&count==sizes[j]);
        for(size_t i=0;i<count;i++)assert(raw[i]==(uint8_t)(i*31+17));
        uint8_t direct[49152];size_t received=99;
        cJSON *fast=rpc_binary_unpack((char*)wire,n,direct,sizeof(direct),&received);
        assert(fast&&received==count&&!memcmp(raw,direct,count)&&!cJSON_HasObjectItem(cJSON_GetObjectItemCaseSensitive(fast,"body"),"data"));cJSON_Delete(fast);
        received=99;assert(!rpc_binary_unpack((char*)wire,n,direct,count-1,&received)&&received==0);
        char *old=cJSON_PrintUnformatted(root);fast=rpc_binary_unpack(old,strlen(old),direct,sizeof(direct),&received);
        assert(fast&&received==count&&!memcmp(raw,direct,count));free(old);cJSON_Delete(fast);
        {cJSON *meta=cJSON_CreateObject();size_t bytes=0;
            uint8_t *packed=rpc_binary_pack_bulk(meta,raw,count,&bytes,true);assert(packed&&!memcmp(packed,"T5R2",4)&&!memcmp(packed+bytes-count,raw,count));free(packed);
            if(count>8192)assert(!rpc_binary_pack(meta,raw,count,&bytes));cJSON_Delete(meta);}
        if(count<=8192){cJSON *meta=cJSON_CreateObject();cJSON_AddStringToObject(meta,"kind","image-chunk");size_t bytes=0;
            uint8_t *packed=rpc_binary_pack(meta,raw,count,&bytes);assert(packed&&!memcmp(packed+bytes-count,raw,count));
            assert(!cJSON_HasObjectItem(meta,"data"));free(packed);cJSON_AddStringToObject(meta,"data","duplicate");
            assert(!rpc_binary_pack(meta,raw,count,&bytes));cJSON_Delete(meta);}
        if(count<=8192){size_t bytes=0;uint8_t *request=rpc_binary_encode(body,&bytes);assert(request&&bytes>=count+8&&!memcmp(request,"T5R1",4));assert(!memcmp(request+bytes-count,raw,count));free(request);assert(cJSON_HasObjectItem(body,"data"));}
        cJSON_Delete(root);assert(!rpc_binary_decode((char*)wire,7));wire[4]=wire[5]=255;assert(!rpc_binary_decode((char*)wire,n));
    }
    char legacy[]="{\"status\":200,\"body\":{\"offset\":3}}";cJSON *root=rpc_binary_decode(legacy,strlen(legacy));assert(root);cJSON_Delete(root);
    puts("RPC_BINARY_OK C# response fixtures, embedded zero bytes, exact payload, C request encode, metadata/truncation bounds, legacy JSON");
}
