#pragma once
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
bool ble_voice_ready(void);
uint8_t *ble_voice_exchange(const char *nonce,const char *proof,const uint8_t *packet,size_t size,size_t *reply_size);
