#pragma once
#include <stddef.h>
#define MBEDTLS_ERR_BASE64_BUFFER_TOO_SMALL -42
int mbedtls_base64_encode(unsigned char *,size_t,size_t *,const unsigned char *,size_t);
int mbedtls_base64_decode(unsigned char *,size_t,size_t *,const unsigned char *,size_t);
