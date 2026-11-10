#undef NDEBUG
#include <assert.h>
#include <stdint.h>
#include <stdbool.h>
#include <stdlib.h>
#include <string.h>
#include <stdio.h>
#include "cJSON.h"
#include "rpc_reply.h"
#define TAB5_MAX_FRAME 65536
#define MALLOC_CAP_SPIRAM 1
#define MALLOC_CAP_8BIT 2
#define MBEDTLS_CIPHER_ID_AES 1
typedef struct {bool paired;uint8_t key[32];} device_config;
typedef struct {int unused;} mbedtls_gcm_context;
static bool paired=true,allocation_failure;
static int key_error,auth_error,auth_calls,allocations;
static device_config device_get_config(void){return (device_config){.paired=paired};}
static void *heap_caps_calloc(size_t n,size_t size,int caps){assert(caps==3);allocations++;return allocation_failure?NULL:calloc(n,size);}
static void mbedtls_gcm_init(mbedtls_gcm_context *ctx){(void)ctx;}
static void mbedtls_gcm_free(mbedtls_gcm_context *ctx){(void)ctx;}
static int mbedtls_gcm_setkey(mbedtls_gcm_context *ctx,int kind,const uint8_t *key,unsigned bits){(void)ctx;(void)key;assert(kind==1&&bits==256);return key_error;}
static int mbedtls_gcm_auth_decrypt(mbedtls_gcm_context *ctx,size_t n,const uint8_t *iv,size_t iv_size,const uint8_t *aad,size_t aad_size,const uint8_t *tag,size_t tag_size,const uint8_t *in,uint8_t *out){
    (void)ctx;(void)iv;(void)aad;(void)tag;assert(iv_size==12&&aad_size==32&&tag_size==16);auth_calls++;if(auth_error)return auth_error;memcpy(out,in,n);return 0;
}
#include "device_decrypt_under_test.inc"
static void *no_allocate(size_t n){(void)n;assert(!"reply-body transfer unexpectedly allocated");return NULL;}
int main(void){
    uint8_t packet[49180];memset(packet,0x35,sizeof(packet));char challenge[33];memset(challenge,'n',32);challenge[32]=0;int error=0;
    char *clear=device_decrypt_checked(packet,sizeof(packet),challenge,65535,&error);assert(clear&&error==0&&clear[49152]==0);free(clear);
    assert(!device_decrypt_checked(packet,sizeof(packet),challenge,32768,&error)&&error==-1001);
    paired=false;assert(!device_decrypt_checked(packet,sizeof(packet),challenge,65535,&error)&&error==-1002);paired=true;
    allocation_failure=true;assert(!device_decrypt_checked(packet,sizeof(packet),challenge,65535,&error)&&error==-1003);allocation_failure=false;
    int before=auth_calls;key_error=-32;assert(!device_decrypt_checked(packet,sizeof(packet),challenge,65535,&error)&&error==-32&&auth_calls==before);key_error=0;
    auth_error=-18;assert(!device_decrypt_checked(packet,sizeof(packet),challenge,65535,&error)&&error==-18);auth_error=0;
    cJSON *root=cJSON_Parse("{\"status\":200,\"body\":{\"offset\":49152}}");assert(root);
    cJSON_Hooks hooks={no_allocate,free};cJSON_InitHooks(&hooks);cJSON *body=rpc_reply_body(root,&error);assert(body&&error==200);cJSON_Delete(root);
    assert(cJSON_GetObjectItemCaseSensitive(body,"offset")->valueint==49152);cJSON_Delete(body);cJSON_InitHooks(NULL);
    const char *bad[]={"{}","{\"status\":200.5,\"body\":{}}","{\"status\":200,\"body\":[]}","{\"status\":0,\"body\":{}}"};
    for(unsigned i=0;i<sizeof(bad)/sizeof(bad[0]);i++){root=cJSON_Parse(bad[i]);assert(!rpc_reply_body(root,&error)&&error==-1006);cJSON_Delete(root);}
    puts("RPC_DECODE_OK bounded PSRAM cleartext; allocation/key/auth failures distinct; no unauthenticated cleartext; reply ownership survives root deletion without allocation; malformed status/body rejected");
}
