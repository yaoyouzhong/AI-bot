#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "mbedtls/gcm.h"
#include "mbedtls/sha256.h"
#include "mbedtls/platform_util.h"
#include "photo_transfer_crypto.h"
#include "photo_display_probe.h"
void photo_display_probe_mark(unsigned stage){assert(stage==PHOTO_DISPLAY_PROBE_CRYPT||stage==PHOTO_DISPLAY_PROBE_HASH);}
#include "esp_heap_caps.h"
static void *allocation;static size_t allocation_size,peak;
static unsigned ticks,calls,inject,fail_alloc;static int64_t now;
void *heap_caps_calloc(size_t count,size_t size,unsigned caps){
    assert(caps==(MALLOC_CAP_INTERNAL|MALLOC_CAP_DMA|MALLOC_CAP_8BIT));assert(!allocation);
    if(fail_alloc)return NULL;
    allocation_size=count*size;if(allocation_size>peak)peak=allocation_size;
    allocation=calloc(count,size);assert(allocation);return allocation;
}
static bool internal(const void *p,size_t n){return (uintptr_t)p>=(uintptr_t)allocation&&(uintptr_t)p+n<=(uintptr_t)allocation+allocation_size;}
static void release(void *p){assert(p==allocation);for(size_t i=0;i<allocation_size;i++)assert(((uint8_t *)p)[i]==0);free(p);allocation=NULL;}
int64_t esp_timer_get_time(void){return now;}
void vTaskDelay(unsigned n){assert(n==1);ticks++;now+=1000;}
static int crypt_update(mbedtls_gcm_context *ctx,const uint8_t *in,size_t n,uint8_t *out,size_t cap,size_t *written){
    assert(internal(ctx,sizeof(*ctx))&&internal(in,n)&&internal(out,cap));assert(n<=PHOTO_TX_CRYPTO_CHUNK);
    if(++calls==inject)return -777;
    return mbedtls_gcm_update(ctx,in,n,out,cap,written);
}
static int hash_update(mbedtls_sha256_context *ctx,const uint8_t *in,size_t n){
    assert(internal(ctx,sizeof(*ctx))&&internal(in,n)&&n<=PHOTO_TX_CRYPTO_CHUNK);
    if(++calls==inject)return -777;
    return mbedtls_sha256_update(ctx,in,n);
}
static int crypt_finish(mbedtls_gcm_context *ctx,uint8_t *out,size_t cap,size_t *written,uint8_t *tag,size_t n){
    assert(internal(ctx,sizeof(*ctx))&&internal(out,cap)&&internal(tag,n));
    if(++calls==inject)return -777;
    return mbedtls_gcm_finish(ctx,out,cap,written,tag,n);
}
#define free release
#define mbedtls_gcm_update crypt_update
#define mbedtls_sha256_update hash_update
#define mbedtls_gcm_finish crypt_finish
#include "../../firmware/main/photo_transfer_crypto.c"
#undef free
#undef mbedtls_gcm_update
#undef mbedtls_sha256_update
#undef mbedtls_gcm_finish
static void zeros(const uint8_t *p,size_t n){for(size_t i=0;i<n;i++)assert(p[i]==0);}
int main(void){
    uint8_t key[32],iv[12],aad[32],expected_tag[16],tag[16],digest[32],expected_digest[32];
    for(unsigned i=0;i<32;i++){key[i]=(uint8_t)(i*7);aad[i]=(uint8_t)(i*3+1);if(i<12)iv[i]=(uint8_t)(i*11);}
    size_t sizes[]={0,1,15,16,17,1023,1024,1025,2047,2048,2049,8192,49152,49153,161111,2*1024*1024};
    unsigned cases=0;
    for(unsigned s=0;s<sizeof(sizes)/sizeof(*sizes);s++){
        size_t n=sizes[s];uint8_t *input=malloc(n+16),*output=malloc(n+16),*expected=malloc(n+16),*restored=malloc(n+16);
        assert(input&&output&&expected&&restored);for(size_t i=0;i<n;i++)input[i]=(uint8_t)(i*13+s);
        mbedtls_gcm_context ctx;mbedtls_gcm_init(&ctx);assert(!mbedtls_gcm_setkey(&ctx,MBEDTLS_CIPHER_ID_AES,key,256));
        assert(!mbedtls_gcm_crypt_and_tag(&ctx,MBEDTLS_GCM_ENCRYPT,n,iv,12,aad,32,input,expected,16,expected_tag));
        assert(!mbedtls_sha256(input,n,expected_digest,0));
        for(unsigned inplace=0;inplace<2;inplace++){
            memcpy(output,input,n);memset(output+n,0xa5,16);calls=inject=ticks=0;photo_tx_begin();
            assert(!photo_tx_encrypt(key,iv,aad,inplace?output:input,n,output,tag));
            assert(!memcmp(output,expected,n)&&!memcmp(tag,expected_tag,16));for(unsigned i=0;i<16;i++)assert(output[n+i]==0xa5);
            assert(!mbedtls_gcm_auth_decrypt(&ctx,n,iv,12,aad,32,tag,16,output,restored)&&!memcmp(restored,input,n));
            if(n>PHOTO_TX_CRYPTO_CHUNK)assert(ticks>0);
            assert(!photo_tx_sha256(input,n,digest)&&!memcmp(digest,expected_digest,32));assert(!allocation);cases++;
        }
        mbedtls_gcm_free(&ctx);free(restored);free(expected);free(output);free(input);
    }
    uint8_t input[4097],output[4097];memset(input,0x53,sizeof(input));
    for(unsigned fault=1;fault<=6;fault++){
        memset(output,0x5a,sizeof(output));calls=0;inject=fault;
        assert(photo_tx_encrypt(key,iv,aad,input,sizeof(input),output,tag)==-777);zeros(tag,16);assert(!allocation);
        zeros(output,sizeof(output));
    }
    inject=0;fail_alloc=1;assert(photo_tx_encrypt(key,iv,aad,input,sizeof(input),output,tag));zeros(tag,16);
    assert(photo_tx_sha256(input,sizeof(input),digest));zeros(digest,32);assert(!allocation);fail_alloc=0;
    for(unsigned fault=1;fault<=5;fault++){calls=0;inject=fault;assert(photo_tx_sha256(input,sizeof(input),digest)==-777);zeros(digest,32);assert(!allocation);}
    inject=0;calls=0;assert(!photo_tx_encrypt(key,iv,aad,input,sizeof(input),output,tag));
    photo_tx_finish(200);char diagnostic[160];photo_tx_diagnostics(diagnostic,sizeof(diagnostic));assert(strstr(diagnostic,",1024,200"));
    assert(peak<4096);printf("PHOTO_TRANSFER_CRYPTO_PASS: %u real mbedTLS GCM/SHA equality cases, internal-buffer bounds, errors/retry/zeroization; peak=%zu\n",cases,peak);return 0;
}
