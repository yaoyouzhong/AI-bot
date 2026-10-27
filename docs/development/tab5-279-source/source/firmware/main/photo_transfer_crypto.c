#include "photo_transfer_crypto.h"
#include "photo_display_probe.h"
#include "esp_heap_caps.h"
#include "esp_timer.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "mbedtls/gcm.h"
#include "mbedtls/sha256.h"
#include "mbedtls/platform_util.h"
#include <stdatomic.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static atomic_uint serial,phase,phase_at,encrypt_ms,hash_ms,crypto_chunks;
static atomic_int final_status;
void photo_tx_begin(void) {
    atomic_fetch_add(&serial,1);atomic_store(&encrypt_ms,0);atomic_store(&hash_ms,0);
    atomic_store(&crypto_chunks,0);atomic_store(&final_status,-1);photo_tx_phase(PHOTO_TX_COPY);
}
void photo_tx_phase(unsigned value) {
    atomic_store(&phase_at,(unsigned)(esp_timer_get_time()/1000));atomic_store(&phase,value);
}
void photo_tx_finish(int status) {atomic_store(&final_status,status);photo_tx_phase(PHOTO_TX_DONE);}
void photo_tx_diagnostics(char *out,unsigned size) {
    snprintf(out,size,"c,1,%u,%u,%u,%u,%u,%u,%u,%d",atomic_load(&serial),atomic_load(&phase),
        atomic_load(&phase_at),atomic_load(&encrypt_ms),atomic_load(&hash_ms),
        atomic_load(&crypto_chunks),PHOTO_TX_CRYPTO_CHUNK,atomic_load(&final_status));
}
static void pause_chunk(void){vTaskDelay(1);}
void photo_tx_copy(uint8_t *output,const uint8_t *input,size_t size) {
    // Bound CPU/cache traffic too; ordinary memcpy of a full JPEG can evict
    // the scanout working set even though it does not launch crypto DMA.
    for(size_t at=0;at<size;) {
        size_t count=size-at;if(count>4096)count=4096;
        memcpy(output+at,input+at,count);at+=count;if(at<size)pause_chunk();
    }
}
static void erase_output(uint8_t *output,size_t size) {
    for(size_t at=0;at<size;){size_t count=size-at;if(count>4096)count=4096;
        mbedtls_platform_zeroize(output+at,count);at+=count;if(at<size)pause_chunk();}
}
typedef struct {
    mbedtls_gcm_context ctx;
    uint8_t input[PHOTO_TX_CRYPTO_CHUNK];
    uint8_t output[PHOTO_TX_CRYPTO_CHUNK+16];
    uint8_t iv[12],aad[32],tag[16];
} photo_gcm_work;
static int encrypt_chunks(const uint8_t key[32],const uint8_t iv[12],const uint8_t aad[32],
                     const uint8_t *input,size_t size,uint8_t *output,uint8_t tag[16],bool tracked) {
    if(!key||!iv||!aad||(!input&&size)||(!output&&size)||!tag)return MBEDTLS_ERR_GCM_BAD_INPUT;
    if(tracked)photo_display_probe_mark(PHOTO_DISPLAY_PROBE_CRYPT);
    if(tracked){photo_tx_phase(PHOTO_TX_CRYPT);}
    int64_t started=esp_timer_get_time();
    // Both buffers AND the context are internal, even when the worker stack
    // lives in PSRAM. Never hand a JPEG/packet PSRAM range to AES DMA.
    photo_gcm_work *work=heap_caps_calloc(1,sizeof(*work),MALLOC_CAP_INTERNAL|MALLOC_CAP_DMA|MALLOC_CAP_8BIT);
    if(!work){memset(tag,0,16);erase_output(output,size);return MBEDTLS_ERR_CIPHER_ALLOC_FAILED;}
    memcpy(work->iv,iv,12);memcpy(work->aad,aad,32);mbedtls_gcm_init(&work->ctx);
    int result=mbedtls_gcm_setkey(&work->ctx,MBEDTLS_CIPHER_ID_AES,key,256);
    if(!result)result=mbedtls_gcm_starts(&work->ctx,MBEDTLS_GCM_ENCRYPT,work->iv,12);
    if(!result)result=mbedtls_gcm_update_ad(&work->ctx,work->aad,32);
    size_t written=0;
    for(size_t at=0;!result&&at<size;) {
        size_t count=size-at;if(count>PHOTO_TX_CRYPTO_CHUNK)count=PHOTO_TX_CRYPTO_CHUNK;
        memcpy(work->input,input+at,count);size_t produced=0;
        result=mbedtls_gcm_update(&work->ctx,work->input,count,work->output,sizeof(work->output),&produced);
        if(!result&&(produced>count||produced>size-written))result=MBEDTLS_ERR_GCM_BAD_INPUT;
        if(!result){memcpy(output+written,work->output,produced);written+=produced;at+=count;if(tracked)atomic_fetch_add(&crypto_chunks,1);if(at<size)pause_chunk();}
    }
    size_t produced=0;
    if(!result)result=mbedtls_gcm_finish(&work->ctx,work->output,sizeof(work->output),&produced,work->tag,16);
    if(!result&&(produced>size-written||written+produced!=size))result=MBEDTLS_ERR_GCM_BAD_INPUT;
    if(!result){if(produced)memcpy(output+written,work->output,produced);memcpy(tag,work->tag,16);}
    else {memset(tag,0,16);erase_output(output,size);}
    mbedtls_gcm_free(&work->ctx);mbedtls_platform_zeroize(work,sizeof(*work));free(work);
    if(tracked){atomic_fetch_add(&encrypt_ms,(unsigned)((esp_timer_get_time()-started)/1000));}
    return result;
}
typedef struct {mbedtls_sha256_context ctx;uint8_t input[PHOTO_TX_CRYPTO_CHUNK],hash[32];} photo_sha_work;
static int sha_chunks(const uint8_t *input,size_t size,uint8_t hash[32],bool tracked,bool (*progress)(void *),void *context) {
    if((!input&&size)||!hash)return MBEDTLS_ERR_SHA256_BAD_INPUT_DATA;
    if(tracked)photo_display_probe_mark(PHOTO_DISPLAY_PROBE_HASH);
    if(tracked){photo_tx_phase(PHOTO_TX_HASH);}
    int64_t started=esp_timer_get_time();
    photo_sha_work *work=heap_caps_calloc(1,sizeof(*work),MALLOC_CAP_INTERNAL|MALLOC_CAP_DMA|MALLOC_CAP_8BIT);
    if(!work){memset(hash,0,32);return MBEDTLS_ERR_SHA256_BAD_INPUT_DATA;}
    mbedtls_sha256_init(&work->ctx);int result=mbedtls_sha256_starts(&work->ctx,0);
    size_t quantum=0;int64_t quantum_at=esp_timer_get_time();
    for(size_t at=0;!result&&at<size;) {
        size_t count=size-at;if(count>PHOTO_TX_CRYPTO_CHUNK)count=PHOTO_TX_CRYPTO_CHUNK;
        memcpy(work->input,input+at,count);result=mbedtls_sha256_update(&work->ctx,work->input,count);
        at+=count;quantum+=count;
        if(!result) {
            if(tracked)atomic_fetch_add(&crypto_chunks,1);
            if(at<size) {
                if(!progress)pause_chunk();
                else if(quantum>=16384||esp_timer_get_time()-quantum_at>=1000) {
                    if(!progress(context)){result=MBEDTLS_ERR_SHA256_BAD_INPUT_DATA;break;}
                    quantum=0;quantum_at=esp_timer_get_time();
                }
            }
        }
    }
    if(!result)result=mbedtls_sha256_finish(&work->ctx,work->hash);
    if(!result)memcpy(hash,work->hash,32);else memset(hash,0,32);
    mbedtls_sha256_free(&work->ctx);mbedtls_platform_zeroize(work,sizeof(*work));free(work);
    if(tracked){atomic_fetch_add(&hash_ms,(unsigned)((esp_timer_get_time()-started)/1000));}
    return result;
}

int photo_tx_encrypt(const uint8_t key[32],const uint8_t iv[12],const uint8_t aad[32],const uint8_t *input,size_t size,uint8_t *output,uint8_t tag[16]) {
    return encrypt_chunks(key,iv,aad,input,size,output,tag,true);
}
int rpc_payload_encrypt(const uint8_t key[32],const uint8_t iv[12],const uint8_t aad[32],const uint8_t *input,size_t size,uint8_t *output,uint8_t tag[16]) {
    return encrypt_chunks(key,iv,aad,input,size,output,tag,false);
}
int photo_tx_sha256(const uint8_t *input,size_t size,uint8_t hash[32]) { return sha_chunks(input,size,hash,true,NULL,NULL); }
int rpc_payload_sha256(const uint8_t *input,size_t size,uint8_t hash[32]) { return sha_chunks(input,size,hash,false,NULL,NULL); }
int preview_payload_sha256(const uint8_t *input,size_t size,uint8_t hash[32],bool (*progress)(void *),void *context) {
    return sha_chunks(input,size,hash,false,progress,context);
}
