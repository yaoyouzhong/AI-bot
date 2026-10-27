#ifndef AIBOT_ARD_KEY_AGREEMENT_H
#define AIBOT_ARD_KEY_AGREEMENT_H
#include <stddef.h>
#include <stdint.h>
/* Ephemeral DH only. Outputs are fixed-width big endian. No Keychain or files. */
int32_t aibot_ard_agree(const uint8_t *prime, size_t length, uint16_t generator,
                      const uint8_t *peer, uint8_t *public_value, uint8_t *shared);
#endif
