#pragma once
#include <stddef.h>
#include <stdint.h>
#include <stdbool.h>

// Photo uploads only. Other RPC, OTA and command packets retain their paths.
enum { PHOTO_TX_IDLE, PHOTO_TX_COPY, PHOTO_TX_PACK, PHOTO_TX_CRYPT,
       PHOTO_TX_HASH, PHOTO_TX_EXCHANGE, PHOTO_TX_DONE };
#define PHOTO_TX_CRYPTO_CHUNK 1024
void photo_tx_begin(void);
void photo_tx_phase(unsigned phase);
void photo_tx_finish(int status);
void photo_tx_diagnostics(char *out,unsigned size);
int photo_tx_encrypt(const uint8_t key[32],const uint8_t iv[12],const uint8_t aad[32],
                     const uint8_t *input,size_t size,uint8_t *output,uint8_t tag[16]);
int photo_tx_sha256(const uint8_t *input,size_t size,uint8_t hash[32]);
void photo_tx_copy(uint8_t *output,const uint8_t *input,size_t size);

// Same bounded PSRAM-safe crypto without changing photo diagnostics.
int rpc_payload_encrypt(const uint8_t key[32],const uint8_t iv[12],const uint8_t aad[32],const uint8_t *input,size_t size,uint8_t *output,uint8_t tag[16]);
int rpc_payload_sha256(const uint8_t *input,size_t size,uint8_t hash[32]);

// Preview-only scheduling: the same full SHA and internal1KiB DMA staging.
// Yield/check foreground work after at most16KiB or1ms, without changing
// photo uploads or authenticated RPC packet scheduling.
int preview_payload_sha256(const uint8_t *input,size_t size,uint8_t hash[32],bool (*progress)(void *),void *context);
